# 🛡️ TRADEXUS Prop Guard Free (NinjaTrader 8 — Direct2D EOD Risk Engine)

[![Platform](https://img.shields.io/badge/Platform-NinjaTrader_8.1+-00D4FF.svg)](https://tradexus.tech/?lang=en)
[![Rendering](https://img.shields.io/badge/GPU-SharpDX_Direct2D-00F5A0.svg)](https://tradexus.tech/?lang=en)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> 🌐 **English:** **`TRADEXUS_Prop_Guard_Free`** is an open-source C# hardware-accelerated (**SharpDX Direct2D**, `< 1 ms` latency, zero repaint) intraday risk & position sizing HUD for **NinjaTrader 8**, engineered for **25K, 50K, 100K & 150K End-of-Day (EOD)** prop firm accounts (Apex, Tradeify, Topstep, MyFundedFutures, FundedNext).  
> 👉 **[Download the Complete TRADEXUS Free Starter Pack (.ZIP Add-On + TRB_PRO Sim101 Strategy + TradingView Scripts) — English Portal](https://tradexus.tech/?lang=en#lead-magnet-section)**

---

**`TRADEXUS_Prop_Guard_Free`** es un motor visual de código abierto desarrollado en C# para **NinjaTrader 8**, acelerado por hardware vía **SharpDX Direct2D (`OnRender`, < 1 ms de latencia, cero repintado)**, diseñado específicamente para proteger cuentas de evaluación y fondeadas End-of-Day (EOD) de **25K, 50K, 100K y 150K** (Apex Trader Funding, Tradeify, Topstep, MyFundedFutures, FundedNext).

👉 **[Descargar el TRADEXUS Free Starter Pack Completo (.ZIP listo para importar en NT8 + Estrategia TRB_PRO Demo + Scripts TradingView) — Portal Español](https://tradexus.tech/?lang=es#lead-magnet-section)**

---

## 📐 Fundamento Matemático: El Problema de las Barreras Absorbentes en Cuentas 50K EOD

En una evaluación estándar de **$50,000 USD** con objetivo de beneficio $U = +\$3,000\text{ USD}$ y límite de pérdida máxima EOD $L = -\$2,000\text{ USD}$ (con límite diario típico $D = -\$1,000\text{ USD}$), la probabilidad de supervivencia depende estrictamente de acotar el riesgo por operación $R_i$ en función del colchón diario remanente $B_t$:

$$B_t = \max\left(0,\; D - |\min(0, \text{PnL}_{\text{realizado}} + \text{PnL}_{\text{flotante}})|\right)$$

$$R_{\text{permitido}} = \min\left(0.90 \cdot B_t,\; D \cdot \rho_{\text{trade}}\right)$$

Donde $\rho_{\text{trade}}$ es el porcentaje máximo del límite diario asignado a un único disparo (recomendado: $20\%$, equivalente a $\$200\text{ USD}$ en una cuenta de 50K).

A partir de $R_{\text{permitido}}$ y la distancia de Stop Loss en ticks ($\text{SL}_{\text{ticks}}$), el motor calcula en cada frame el **lotaje máximo seguro** para futuros Micro del CME:

$$\text{Contratos}_{\text{MNQ}} = \left\lfloor \frac{R_{\text{permitido}}}{\text{SL}_{\text{ticks, MNQ}} \times \$0.50} \right\rfloor, \qquad \text{Contratos}_{\text{MES}} = \left\lfloor \frac{R_{\text{permitido}}}{\text{SL}_{\text{ticks, MES}} \times \$1.25} \right\rfloor$$

---

## ⚡ Características Técnicas

1. **Lectura en Tiempo Real de Cuenta NinjaTrader (`AccountItem.RealizedProfitLoss` + `UnrealizedProfitLoss`):**
   - Audita de forma pasiva la cuenta seleccionada (por ejemplo `Sim101`, `Apex...` o `Tradovate...`) sin bloquear el hilo de renderizado.
2. **Semáforo Dinámico de 3 Estados:**
   - 🟢 **Zona Operativa Segura (`< 65%` del límite diario):** Lotaje completo permitido según calculadora institucional.
   - 🟡 **Alerta de Precaución (`65% – 85%` del límite diario):** Recorte automático del lotaje máximo sugerido al **50%** para frenar rachas adversas.
   - 🔴 **Kill-Switch Visual (`>= 85%` del límite diario):** Lotaje sugerido forzado a **0 contratos** y despliegue de un **Escudo de Bloqueo Visual Direct2D** en el centro del gráfico advirtiendo detener la operativa por el resto de la sesión.
3. **Perfiles Preconfigurados EOD:**
   - `Prop_25K_EOD` (Max EOD DD: -$1,500 | Daily Limit: -$500)
   - `Prop_50K_EOD` (Max EOD DD: -$2,000 | Daily Limit: -$1,000)
   - `Prop_100K_EOD` (Max EOD DD: -$3,000 | Daily Limit: -$1,500)
   - `Prop_150K_EOD` (Max EOD DD: -$4,500 | Daily Limit: -$2,250)

---

## 🚀 Instalación Rápida en NinjaTrader 8

### Opción 1: Descargar el Pack Completo (Recomendado)
Descarga **`TRADEXUS_Free_Starter_Pack.zip`** gratuitamente desde **[https://tradexus.tech](https://tradexus.tech)** e impórtalo desde `Tools > Import > NinjaScript Add-On...`. Además de `TRADEXUS_Prop_Guard_Free`, incluye:
- **`TRADEXUS_TRB_PRO_Demo`:** Estrategia algorítmica completa para `Sim101` / Replay calibrada para cuentas 50K EOD (`MNQ` 5M, 2 contratos, SL 120t, TP 160t, BE 80t+4, Trail 100t).
- **Indicadores Pine Script v6 para TradingView (`Pivot SS` & `SmoothTrendRibbon Lite`).**
- **Guía Rápida PDF/MD de Reglas 50K EOD.**

### Opción 2: Compilación Manual desde este Repositorio
1. Copia [`TRADEXUS_Prop_Guard_Free.cs`](TRADEXUS_Prop_Guard_Free.cs) en `Documentos\NinjaTrader 8\bin\Custom\Indicators\`.
2. Abre `New > NinjaScript Editor` en NinjaTrader 8 y presiona **`F5`**.
3. Añade el indicador `TRADEXUS_Prop_Guard_Free` a cualquier gráfico de `MNQ` o `MES`.

---

## 🔗 Ecosistema Cuantitativo TRADEXUS / Quantitative Ecosystem

- 🌐 **Official Website & Black Friday 2026 Catalog (English — Up to 85% OFF):** [https://tradexus.tech/?lang=en](https://tradexus.tech/?lang=en)
- 🌐 **Web Oficial & Catálogo Black Friday 2026 (Español — Hasta 85% OFF):** [https://tradexus.tech/?lang=es](https://tradexus.tech/?lang=es)
- ✈️ **Canal Oficial de Telegram (Niveles GEX CME Pre-Market):** [https://t.me/tradexus_quant](https://t.me/tradexus_quant)
