#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// ============================================================================
//  TRADEXUS QUANT SUITE — UTILIDAD INSTITUCIONAL GRATUITA (OPEN SOURCE)
//  Nombre: TRADEXUS_Prop_Guard_Free (Prop Firm EOD Risk & Sizing Engine)
//  Renderizado: SharpDX Direct2D Hardware Accelerated (< 1 ms, Zero Repaint)
//
//  FUNCIONALIDAD:
//    1. Calcula el lotaje máximo seguro en MNQ ($0.50/tick) y MES ($1.25/tick)
//       según el tamaño de cuenta Prop Firm (25K, 50K, 100K, 150K EOD).
//    2. Monitoriza el PnL diario (Realizado + Flotante) de la cuenta activa
//       o simulado y proyecta la distancia exacta al límite diario y EOD.
//    3. Alerta visualmente en GPU Direct2D y despliega escudo de bloqueo
//       visual al alcanzar el umbral de precaución (70%) o bloqueo (90%).
// ============================================================================

namespace NinjaTrader.NinjaScript.Indicators
{
	public enum TradexusPropAccountSize
	{
		Prop_25K_EOD,
		Prop_50K_EOD,
		Prop_100K_EOD,
		Prop_150K_EOD
	}

	public class TRADEXUS_Prop_Guard_Free : Indicator
	{
		#region Variables privadas

		private ATR		atrIndicator;
		private double	currentAtrPoints;
		private Account	monitoredAccount;

		#endregion

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "TRADEXUS Prop Guard Free: Motor visual Direct2D de gestión de riesgo EOD y calculadora de lotaje institucional para cuentas de fondeo (25K, 50K, 100K, 150K) en MNQ y MES.";
				Name						= "TRADEXUS_Prop_Guard_Free";
				Calculate					= Calculate.OnBarClose;
				IsOverlay					= true;
				DisplayInDataBox			= false;
				DrawOnPricePanel			= true;
				PaintPriceMarkers			= false;
				ScaleJustification			= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive	= true;

				// ── 1. Configuración de Cuenta de Fondeo ──
				TamanoCuenta				= TradexusPropAccountSize.Prop_50K_EOD;
				NombreCuentaMonitoreada		= "Sim101";
				PnLDiarioManualOverrideUSD	= 0.0;

				// ── 2. Parámetros de Riesgo EOD y Stop Loss ──
				RiesgoMaximoPorTradePct		= 20.0;		// 20% del colchón diario por operación ($200 en 50K)
				StopLossReferenciaTicksMNQ	= 120;		// 120 ticks = 30 puntos MNQ ($60 USD/contrato)
				StopLossReferenciaTicksMES	= 40;		// 40 ticks = 10 puntos MES ($50 USD/contrato)
				UmbralAlertaPrecaucionPct	= 65.0;		// Alerta amarilla al consumir 65% del límite diario
				UmbralBloqueoCriticoPct		= 85.0;		// Bloqueo visual rojo al consumir 85% del límite diario
				MostrarBannerBloqueo		= true;
			}
			else if (State == State.DataLoaded)
			{
				atrIndicator = ATR(14);
				ResolverCuentaMonitoreada();
			}
		}

		private void ResolverCuentaMonitoreada()
		{
			try
			{
				if (Account.All != null && Account.All.Count > 0)
				{
					foreach (Account acc in Account.All)
					{
						if (acc != null && string.Equals(acc.Name, NombreCuentaMonitoreada, StringComparison.OrdinalIgnoreCase))
						{
							monitoredAccount = acc;
							return;
						}
					}
					monitoredAccount = Account.All.FirstOrDefault(a => a != null && a.Name.StartsWith("Sim", StringComparison.OrdinalIgnoreCase));
				}
			}
			catch
			{
				monitoredAccount = null;
			}
		}

		private void ObtenerParametrosCuenta(out string label, out double eodTrailingDD, out double dailyLossLimit, out double profitTarget, out int hardCapMicros)
		{
			switch (TamanoCuenta)
			{
				case TradexusPropAccountSize.Prop_25K_EOD:
					label = "25K EOD";
					eodTrailingDD = 1500.0;
					dailyLossLimit = 500.0;
					profitTarget = 1500.0;
					hardCapMicros = 20;
					break;
				case TradexusPropAccountSize.Prop_100K_EOD:
					label = "100K EOD";
					eodTrailingDD = 3000.0;
					dailyLossLimit = 1500.0;
					profitTarget = 6000.0;
					hardCapMicros = 80;
					break;
				case TradexusPropAccountSize.Prop_150K_EOD:
					label = "150K EOD";
					eodTrailingDD = 4500.0;
					dailyLossLimit = 2250.0;
					profitTarget = 9000.0;
					hardCapMicros = 120;
					break;
				case TradexusPropAccountSize.Prop_50K_EOD:
				default:
					label = "50K EOD";
					eodTrailingDD = 2000.0;
					dailyLossLimit = 1000.0;
					profitTarget = 3000.0;
					hardCapMicros = 40;
					break;
			}
		}

		private double ObtenerPnLDiarioActual()
		{
			if (Math.Abs(PnLDiarioManualOverrideUSD) > 0.001)
				return PnLDiarioManualOverrideUSD;

			try
			{
				if (monitoredAccount == null)
					ResolverCuentaMonitoreada();

				if (monitoredAccount != null)
				{
					double realized = monitoredAccount.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
					double unrealized = monitoredAccount.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
					return realized + unrealized;
				}
			}
			catch
			{
				// Fallback silencioso en hilo de cálculo
			}
			return 0.0;
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 14)
				return;

			currentAtrPoints = atrIndicator[0];
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null || ChartPanel == null)
				return;

			string tierLabel;
			double eodMaxDD;
			double dailyLimit;
			double evalTarget;
			int hardCapMicros;
			ObtenerParametrosCuenta(out tierLabel, out eodMaxDD, out dailyLimit, out evalTarget, out hardCapMicros);

			double currentDailyPnL = ObtenerPnLDiarioActual();
			double dailyLossUsed = currentDailyPnL < 0.0 ? Math.Abs(currentDailyPnL) : 0.0;
			double remainingDailyBuffer = Math.Max(0.0, dailyLimit - dailyLossUsed);
			double lossUsedPct = dailyLimit > 0.0 ? Math.Min(100.0, (dailyLossUsed / dailyLimit) * 100.0) : 0.0;

			// Cálculo de riesgo dólar permitido para el próximo trade
			double allowedTradeRiskUSD = Math.Min(
				remainingDailyBuffer * 0.90,
				dailyLimit * (RiesgoMaximoPorTradePct / 100.0)
			);

			if (lossUsedPct >= UmbralBloqueoCriticoPct)
				allowedTradeRiskUSD = 0.0;
			else if (lossUsedPct >= UmbralAlertaPrecaucionPct)
				allowedTradeRiskUSD *= 0.50;

			// Costo por contrato según Stop Loss en ticks
			// MNQ: $0.50 por tick | MES: $1.25 por tick
			double riskPerContractMNQ = Math.Max(5.0, StopLossReferenciaTicksMNQ * 0.50);
			double riskPerContractMES = Math.Max(5.0, StopLossReferenciaTicksMES * 1.25);

			int maxSafeMNQ = allowedTradeRiskUSD > 0.0
				? Math.Min(hardCapMicros, (int)Math.Floor(allowedTradeRiskUSD / riskPerContractMNQ))
				: 0;
			int maxSafeMES = allowedTradeRiskUSD > 0.0
				? Math.Min(hardCapMicros, (int)Math.Floor(allowedTradeRiskUSD / riskPerContractMES))
				: 0;

			bool isLocked = lossUsedPct >= UmbralBloqueoCriticoPct;
			bool isWarning = !isLocked && lossUsedPct >= UmbralAlertaPrecaucionPct;

			float panelWidth = 340f;
			float panelHeight = 228f;
			float startX = (float)(ChartPanel.X + ChartPanel.W - panelWidth - 18f);
			float startY = (float)(ChartPanel.Y + 18f);

			if (startX < ChartPanel.X + 10f)
				startX = ChartPanel.X + 10f;

			SharpDX.RectangleF hudRect = new SharpDX.RectangleF(startX, startY, panelWidth, panelHeight);
			SharpDX.RectangleF headerRect = new SharpDX.RectangleF(startX, startY, panelWidth, 30f);

			SharpDX.Color bgCol = new SharpDX.Color(8, 16, 32, 232);
			SharpDX.Color headerCol = isLocked
				? new SharpDX.Color(180, 24, 45, 240)
				: (isWarning ? new SharpDX.Color(180, 115, 12, 240) : new SharpDX.Color(0, 168, 215, 65));
			SharpDX.Color borderCol = isLocked
				? new SharpDX.Color(255, 65, 85, 255)
				: (isWarning ? new SharpDX.Color(255, 195, 0, 255) : new SharpDX.Color(0, 212, 255, 180));
			SharpDX.Color statusCol = isLocked
				? new SharpDX.Color(255, 80, 95, 255)
				: (isWarning ? new SharpDX.Color(255, 210, 60, 255) : new SharpDX.Color(0, 245, 160, 255));

			using (var bgBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, bgCol))
			using (var headerBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, headerCol))
			using (var borderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, borderCol))
			using (var statusBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, statusCol))
			using (var whiteBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(242, 246, 252, 255)))
			using (var mutedBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(150, 165, 190, 255)))
			using (var cyanBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(0, 229, 255, 255)))
			using (var barBgBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(25, 38, 62, 255)))
			using (var titleFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Consolas", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, 12f))
			using (var bodyFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Consolas", SharpDX.DirectWrite.FontWeight.Regular, SharpDX.DirectWrite.FontStyle.Normal, 11.5f))
			using (var boldFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Consolas", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, 12.5f))
			{
				RenderTarget.FillRectangle(hudRect, bgBrush);
				RenderTarget.FillRectangle(headerRect, headerBrush);
				RenderTarget.DrawRectangle(hudRect, borderBrush, isLocked ? 2.0f : 1.2f);

				string headerTitle = string.Format("🛡️ TRADEXUS PROP GUARD FREE [{0}]", tierLabel);
				RenderTarget.DrawText(headerTitle, titleFormat, new SharpDX.RectangleF(startX + 10f, startY + 7f, panelWidth - 20f, 20f), whiteBrush);

				float y = startY + 38f;
				string statusText = isLocked
					? "ESTADO: 🔴 KILL-SWITCH ACTIVO (STOP TRADING)"
					: (isWarning ? "ESTADO: 🟡 PRECAUCIÓN (REDUCIR LOTAJE 50%)" : "ESTADO: 🟢 ZONA OPERATIVA SEGURA");
				RenderTarget.DrawText(statusText, boldFormat, new SharpDX.RectangleF(startX + 10f, y, panelWidth - 20f, 18f), statusBrush);

				y += 22f;
				string pnlLine = string.Format("PnL Sesión ({0}): {1:+$#,##0.00;-$#,##0.00;$0.00}",
					monitoredAccount != null ? monitoredAccount.Name : NombreCuentaMonitoreada,
					currentDailyPnL);
				RenderTarget.DrawText(pnlLine, bodyFormat, new SharpDX.RectangleF(startX + 10f, y, panelWidth - 20f, 18f), whiteBrush);

				y += 20f;
				string bufferLine = string.Format("Colchón Diario Restante: ${0:#,##0.00} / ${1:#,##0}", remainingDailyBuffer, dailyLimit);
				RenderTarget.DrawText(bufferLine, bodyFormat, new SharpDX.RectangleF(startX + 10f, y, panelWidth - 20f, 18f), mutedBrush);

				// Barra de progreso de consumo de pérdida diaria
				y += 20f;
				float barW = panelWidth - 24f;
				SharpDX.RectangleF barBgRect = new SharpDX.RectangleF(startX + 12f, y, barW, 8f);
				float fillW = (float)Math.Max(2.0, (barW * lossUsedPct) / 100.0);
				SharpDX.RectangleF barFillRect = new SharpDX.RectangleF(startX + 12f, y, fillW, 8f);
				RenderTarget.FillRectangle(barBgRect, barBgBrush);
				RenderTarget.FillRectangle(barFillRect, statusBrush);

				y += 16f;
				string sizingHeader = string.Format("── LOTAJE MÁXIMO SEGURO (Riesgo ${0:0}) ──", allowedTradeRiskUSD);
				RenderTarget.DrawText(sizingHeader, bodyFormat, new SharpDX.RectangleF(startX + 10f, y, panelWidth - 20f, 18f), cyanBrush);

				y += 20f;
				string mnqLine = string.Format("• MNQ (SL {0}t = ${1:0}/ct): MÁX {2} CONTRATOS",
					StopLossReferenciaTicksMNQ, riskPerContractMNQ, maxSafeMNQ);
				RenderTarget.DrawText(mnqLine, boldFormat, new SharpDX.RectangleF(startX + 10f, y, panelWidth - 20f, 18f), whiteBrush);

				y += 20f;
				string mesLine = string.Format("• MES (SL {0}t = ${1:0}/ct): MÁX {2} CONTRATOS",
					StopLossReferenciaTicksMES, riskPerContractMES, maxSafeMES);
				RenderTarget.DrawText(mesLine, boldFormat, new SharpDX.RectangleF(startX + 10f, y, panelWidth - 20f, 18f), whiteBrush);

				y += 22f;
				string footerLine = string.Format("EOD Max DD: -${0:#,##0} | ATR(14): {1:0.0} pts | tradexus.tech", eodMaxDD, currentAtrPoints);
				RenderTarget.DrawText(footerLine, bodyFormat, new SharpDX.RectangleF(startX + 10f, y, panelWidth - 20f, 18f), mutedBrush);

				// Banner central de Bloqueo Visual si se supera el umbral crítico
				if (isLocked && MostrarBannerBloqueo)
				{
					float bannerW = Math.Min(560f, ChartPanel.W - 40f);
					float bannerH = 74f;
					float bx = ChartPanel.X + (ChartPanel.W - bannerW) / 2f;
					float by = ChartPanel.Y + (ChartPanel.H - bannerH) / 2f;
					SharpDX.RectangleF alertRect = new SharpDX.RectangleF(bx, by, bannerW, bannerH);

					using (var alertBg = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(145, 12, 28, 235)))
					{
						RenderTarget.FillRectangle(alertRect, alertBg);
						RenderTarget.DrawRectangle(alertRect, borderBrush, 2.5f);
						RenderTarget.DrawText(
							"⚠️ BLOQUEO VISUAL PROP GUARD: LÍMITE DIARIO ALCANZADO ⚠️\nNO EJECUTAR MÁS ÓRDENES EN ESTA SESIÓN — PROTEGE TU CUENTA EOD",
							boldFormat,
							new SharpDX.RectangleF(bx + 16f, by + 16f, bannerW - 32f, bannerH - 24f),
							whiteBrush
						);
					}
				}
			}
		}

		#region Propiedades Expuestas en NinjaTrader

		[NinjaScriptProperty]
		[Display(Name="Tamaño de Cuenta Prop Firm", Description="Selecciona el tamaño de evaluación o cuenta fondeada EOD (25K, 50K, 100K, 150K)", Order=1, GroupName="1. Configuración Prop Firm EOD")]
		public TradexusPropAccountSize TamanoCuenta { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Nombre Cuenta NT8", Description="Nombre de la cuenta en NinjaTrader para leer PnL diario (ej. Sim101)", Order=2, GroupName="1. Configuración Prop Firm EOD")]
		public string NombreCuentaMonitoreada { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Simular PnL Diario Manual ($ USD)", Description="Opcional: Introduce un PnL (ej. -700 o -900) para simular alertas de riesgo en el gráfico (0 = lectura automática)", Order=3, GroupName="1. Configuración Prop Firm EOD")]
		public double PnLDiarioManualOverrideUSD { get; set; }

		[NinjaScriptProperty]
		[Range(5.0, 100.0)]
		[Display(Name="% del Límite Diario por Trade", Description="Porcentaje máximo del colchón diario arriesgado en una sola operación (Recomendado: 20%)", Order=4, GroupName="2. Calculadora de Lotaje MNQ / MES")]
		public double RiesgoMaximoPorTradePct { get; set; }

		[NinjaScriptProperty]
		[Range(10, 500)]
		[Display(Name="Stop Loss Referencia MNQ (Ticks)", Description="Stop Loss en ticks para calcular lotaje en MNQ (120 ticks = 30 puntos = $60/contrato)", Order=5, GroupName="2. Calculadora de Lotaje MNQ / MES")]
		public int StopLossReferenciaTicksMNQ { get; set; }

		[NinjaScriptProperty]
		[Range(4, 200)]
		[Display(Name="Stop Loss Referencia MES (Ticks)", Description="Stop Loss en ticks para calcular lotaje en MES (40 ticks = 10 puntos = $50/contrato)", Order=6, GroupName="2. Calculadora de Lotaje MNQ / MES")]
		public int StopLossReferenciaTicksMES { get; set; }

		[NinjaScriptProperty]
		[Range(30.0, 90.0)]
		[Display(Name="Umbral Alerta Precaución (%)", Description="Porcentaje del límite diario consumido para disparar alerta amarilla y reducir lotaje 50%", Order=7, GroupName="3. Escudo Visual Anti-Quiebra")]
		public double UmbralAlertaPrecaucionPct { get; set; }

		[NinjaScriptProperty]
		[Range(50.0, 100.0)]
		[Display(Name="Umbral Bloqueo Crítico (%)", Description="Porcentaje del límite diario consumido para activar el bloqueo visual rojo en pantalla", Order=8, GroupName="3. Escudo Visual Anti-Quiebra")]
		public double UmbralBloqueoCriticoPct { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Mostrar Banner Central de Bloqueo", Description="Despliega alerta roja en el centro del gráfico cuando se alcanza el límite crítico diario", Order=9, GroupName="3. Escudo Visual Anti-Quiebra")]
		public bool MostrarBannerBloqueo { get; set; }

		#endregion
	}
}
