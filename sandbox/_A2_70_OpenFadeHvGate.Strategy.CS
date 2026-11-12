// ## 1. Identity
// Catalogue ID: A2-70
// ID mapping: A2-70 → _A2_70_OpenFadeHvGate.Strategy.CS (class _A2_70_OpenFadeHvGate). The same file is the A2-64 fade this gate sits on (engines-manual example 17).
// Name: Open fade with HV gate
// Header version: 1
// Rules version: catalogue v2.7 A2-64 R-O arm plus the A2-70 gate, as implemented by docs/engines-manual.md §3.10 (example 17)
// Study class: _A2_70_OpenFadeHvGate
// File: sandbox/_A2_70_OpenFadeHvGate.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, headings "A2-64 · 1.0 × daily ATR fade" and "A2-70 · High-volatility skip gate". The code is engines-manual example 17.
// Taken from the source: limit fades at the RTH open ± m × ATR20d, target the open, flat 15:55, disaster stop 2m × ATR20d. The gate blocks new entries while HV20 is above thetaOff and re-enables below 0.8 × thetaOff.
// Research assumption: m is 0.5 in the example. The catalogue core is 1.0 and the sweep is 0.75, 1.0 and 1.25, so 0.5 is outside that grid. thetaOff 0.30 is marked PLACEHOLDER in the example. The reference is the RTH open only. The prior-close reference is not coded. The protective stop is sent on the first closed bar in the trade, so the fill bar is unprotected.
//
// ## 3. Hypothesis
// Mechanism: a move of about one daily ATR away from the open is the book's mean-reversion distance, and the fade targets a return to the open. A2-70 is a high-volatility veto on new entries, with a gap before it turns back on.
// Link: specs/trading-idea-catalogue.md, headings "A2-64 · 1.0 × daily ATR fade from the open or prior close" and "A2-70 · High-volatility skip gate (module)"
//
// ## 4. Trade walkthrough
// Trigger: from 09:45 to 15:00 ET, while the gate is on and the day has not traded, rest a buy limit at the RTH open minus m × ATR20d and a sell limit at the open plus that band.
// Management: after a fill, the target is the RTH open and the disaster stop is 2m × ATR20d from the fill, floored at 14 ticks. Both are sent from the first closed bar in the trade.
// Exit: the target, the stop, or a market flatten at 15:55 ET (or five minutes before an early close when that is earlier).
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar. ATR20d and HV20 come from the prior completed daily bar.
// Order sent: Limit A264LE and A264SE. Target Limit A264LT / A264ST. Stop A264LS / A264SS. Flat is MarketNextBar A264LX / A264SX.
// Can fill: a limit can fill on a later bar when price trades through it. The stop and target are not working on the fill bar.
//
// ## 6. Position and re-entry
// Position: flat or one. Both limits rest; the first fill is the trade. MaxTradesPerDay is 1.
// Re-entry: not the same day. m_traded stays set after a fill, a roll, or a budget skip.
// Same session after a stop: no.
//
// ## 7. Instruments
// Typical: ES and NQ. This file is written for @ES.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES 1-minute, 24h Globex, dxFeed continuous unadjusted. Daily context is aggregated from Data1. There is no daily stream.
// Data2: Not applicable
// Further series: Not applicable
// Warmup: ATR20d and HV20 need their daily lengths (20). Until ATR20d is positive the day does not arm.
// Tests use data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: the gate is on; ATR20d is positive; the RTH open is known; the clock is inside 09:45–15:00 and before the flat; the day has not traded; entryAllowed allows it; the widened stop fits the 1% budget.
// Skip when: HV20 has turned the gate off; a roll day; the budget fails (the day is then marked traded).
//
// ## 10. Position sizing and risk
// Size: 1 contract.
// Risk limit: skipped when the disaster distance, floored at 14 ticks, exceeds 1% of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: Limit A264LE and Limit A264SE.
// Exit orders: Limit A264LT / A264ST at the RTH open; Stop A264LS / A264SS; MarketNextBar A264LX / A264SX.
// Fill assumption: limit on a trade-through. The fill bar itself is unprotected.
// Costs: the example assumes the ES research round trip of $17.00 ($2.25 commission and $6.25 slippage per side) set in MultiCharts, not in this file. Not yet verified
//
// ## 12. Trade management
// While open: the stop and the target are re-sent. The gate does not flatten an open trade.
//
// ## 13. Exits
// Hard stop: fill ± max(2 × m × ATR20d, 14 ticks). With m = 0.5 that structural distance is 1 × ATR20d.
// Target: the RTH open.
// Time exit: 15:55 ET, earlier on a CME early close.
// Invalidation: the gate blocks a new entry. It does not cancel a trade that is already open.
//
// ## 14. Regimes
// Suitable: a day whose prior HV20 is at or below the off threshold, or has fallen back under the re-enable threshold.
// Unsuitable: HV20 above thetaOff, until it falls below 0.8 × thetaOff. thetaOff is the example placeholder 0.30.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: M-DAILY (NeedsDaily, dataStream 0, aggregate Data1).
// Engine settings enabled: RollDetection = Series. DailyConfig.atrLength = 20. DailyConfig.hvLengths = { 20 }. MaxTradesPerDay = 1.
//
// ## 16. Pitfalls
// Lookahead: ATR and HV are taken from the prior completed day (PrevDate). Limits are priced from the closed bar and work forward.
// Repaint: Not applicable
// Survivorship: Not applicable
// Rolls: Series. A splice day does not arm. M-DAILY's ATR is reseeded at a roll and stays empty for atrLength sessions.
// Data quality: thetaOff is a placeholder. Do not read a result until the owner sets it. Not yet verified on a chart.
// Other: the fill bar is unprotected. m = 0.5 is the example value and is not the catalogue core.
//
// ## 17. Parameters
// Input: Band
// Purpose: A2-64 distance from the RTH open, in ATR20d
// Default: M100 — 1.0, the catalogue core. Owner 2026-11-12. The previous example value was 0.5. Not a fitted optimum.
// Units: ATR20d
// Range: M075 = 0.75, M100 = 1.0, M125 = 1.25. Any other value throws in StartCalc.
// Effect: a larger band rests the limits farther from the open and widens the 2m disaster stop
// Interactions: the disaster distance is 2 × Band × ATR20d, floored at 14 ticks
// Fixed or research-tuned: research-tuned. The three members are the catalogue sweep. The default is the core.
// Input: ThetaOff
// Purpose: A2-70 annualised HV20 level that blocks new entries
// Default: Pct50 — 0.50. Owner 2026-11-12. The previous value was the 0.30 placeholder. Not a fitted optimum.
// Units: annualised volatility (fraction)
// Range: Pct30 = 0.30, Pct40 = 0.40, Pct50 = 0.50, Pct70 = 0.70. Any other value throws in StartCalc.
// Effect: a higher threshold blocks fewer days. Re-enable is 0.8 × the chosen threshold.
// Interactions: an open trade is not flattened when the gate turns off
// Fixed or research-tuned: research-tuned. The four members are the catalogue sweep. The default is the book 50 percent.
//
// ## 18. Acceptance policy
// Policy: catalogue-default
// Predeclared before any result was read: yes, 2026-11-12. No result for this file has been read. The coded arm is the engines-manual example, not a fitted set.
// catalogue-default: judge this file by its catalogue kill, catalogue §2.3, and the A2 generic kill when the idea is A2-origin. The stricter stated rule decides. This header does not change the catalogue.
// big-winner-trend: Not applied
// When 1% of winning trades is not a whole number, remove K = max(1, ceil(0.01 × the number of winning trades)). If there are no winning trades, remove none. This is the programme overview's proposed rounding, adopted here before any run.
//
// ## 19. Validation
// Status: Not yet verified
// Evidence: Not yet verified
// Window used: a run, when one exists, uses data through 2024-12-31 only.
// Holdout (W-M and W-D Jan 2025–Oct 2026; W-T last 125 sessions) stays unviewed. Do not put those results here.
//
// ## 20. Edge monitoring
// A person reads this section. It does not place orders and it does not change parameters.
// Expectancy: Not yet verified
//   Formula: win_rate * avg_win - loss_rate * avg_loss, same cost model as section 11.
// Observations: Not yet verified
//   Catalogue M19 discusses sample error. A minimum count for this strategy is written here before incubation, or it stays Not yet verified.
// Drift: Not yet verified
//   Compare regime mix, fill versus system price, and costs with the tested profile. Numbers come from this strategy's own test, written down before incubation.
// Warning: Not yet verified
//   Warning means look. It is not an order.
// Pause: Not yet verified
//   Pause means stop new entries. Open positions keep the exits in section 13.
// Review: Not yet verified
//   Review means the owner looks before new entries resume.
// Uncertainty: no run yet; m and thetaOff are example values, not the catalogue core; the fill bar is unprotected; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: Not applicable. This arm does not call journalArm.
// logSignal tag: Not applicable. The gate line is a trace, not logSignal.
// Skip / flatten lines: "A2-64" plus "skip: stop" when the budget fails; "A2-70" plus "gate OFF" or "gate ON" when HV20 flips the gate.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class and its constructor. A _TestNN slot is this file with those two tokens renamed to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// Default change (owner 2026-11-12): Band is 1.0 (was 0.5) and ThetaOff is 0.50 (was 0.30). Both change which limits are sent.
// The prior-close reference (R-C) is not coded. That remains an owner question.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _A2_70_OpenFadeHvGate : _VolumeProfileFuturesBase
	{
		/// <summary>A2-64 band multiple of ATR20d. Catalogue sweep {0.75, 1.0, 1.25}. Default is the catalogue core 1.0 (owner 2026-11-12; was 0.5).</summary>
		public enum EBandMultiple { M075, M100, M125 }
		/// <summary>A2-70 annualised HV20 off-switch. Catalogue sweep {30, 40, 50, 70} percent. Default 50 percent (owner 2026-11-12; was the 30 percent placeholder).</summary>
		public enum EThetaOff { Pct30, Pct40, Pct50, Pct70 }

		[Input] public EBandMultiple Band { get; set; }
		[Input] public EThetaOff ThetaOff { get; set; }

		protected sealed class A264Config
		{
			public int contracts = 1;
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);
			public int atrLength = 20, hvLength = 20;
			public double reEnable = 0.8;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected A264Config Cfg { get; } = new A264Config();

		private IOrderPriced m_leLmt, m_seLmt, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private IOrderMarket m_lx, m_sx;
		private DateTime m_lastDaily = DateTime.MinValue, m_day = DateTime.MinValue;
		private bool m_gateOff;
		private double m_atr = double.NaN, m_hv = double.NaN, m_stop = double.NaN;
		private bool m_traded;
		private double m_r = double.NaN;

		public _A2_70_OpenFadeHvGate(object _ctx) : base(_ctx) { Band = EBandMultiple.M100; ThetaOff = EThetaOff.Pct50; }

		private double bandMultiple() { return Band == EBandMultiple.M075 ? 0.75 : Band == EBandMultiple.M125 ? 1.25 : 1.0; }
		private double thetaOff() { return ThetaOff == EThetaOff.Pct30 ? 0.30 : ThetaOff == EThetaOff.Pct40 ? 0.40 : ThetaOff == EThetaOff.Pct70 ? 0.70 : 0.50; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			MaxTradesPerDay = 1;
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "A264LE", EOrderAction.Buy));
			m_seLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "A264SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A264LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A264SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A264LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A264SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A264LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A264ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			if (!Enum.IsDefined(typeof(EBandMultiple), Band)) throw new ArgumentException("_A2_70_OpenFadeHvGate: Band is not a defined EBandMultiple");
			if (!Enum.IsDefined(typeof(EThetaOff), ThetaOff)) throw new ArgumentException("_A2_70_OpenFadeHvGate: ThetaOff is not a defined EThetaOff");
			NeedsDaily = true;                      // M-DAILY, dataStream 0 = aggregate Data1 per Globex day (roll-adjusted)
			DailyConfig.atrLength = Cfg.atrLength; DailyConfig.hvLengths = new[] { Cfg.hvLength };
			base.StartCalc();
			m_lastDaily = m_day = DateTime.MinValue; m_gateOff = false; m_atr = m_hv = m_stop = double.NaN; m_traded = false;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			stepDaily();
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_traded = false; }
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay, flat = flatAt(day);
			SessionStats s = Session;
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0)
			{
				m_traded = true;
				if (double.IsNaN(m_stop)) m_stop = StrategyInfo.AvgEntryPrice - Math.Sign(pos) * m_r;   // first bar in the trade
				if (tod >= flat && tod < new TimeSpan(17, 0, 0)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(s.rthOpen); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(s.rthOpen); }
				return;
			}
			if (rollOn(1)) m_traded = true;
			if (m_traded || s == null || s.rthOpen <= 0 || !(m_atr > 0) || m_gateOff) return;
			if (tod < Cfg.from || tod >= Cfg.to || tod >= flat) return;
			string why;
			if (!entryAllowed(out why)) return;
			double tick = tickSize(), m = bandMultiple(), band = m * m_atr;
			double r = Math.Max(2 * m * m_atr, Cfg.costFloorTicks * tick);   // disaster stop 2m x ATR, floored
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-64 {day:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); m_traded = true; return; }
			double buyAt = Math.Round((s.rthOpen - band) / tick) * tick, sellAt = Math.Round((s.rthOpen + band) / tick) * tick;
			// both limits stand; whichever fills first sets the side (MaxTradesPerDay = 1 blocks a second entry). The protective stop
			// is placed from the fill price on the first closed bar in the trade: a fill-bar spike is unprotected (pitfall).
			m_leLmt.Send(buyAt, Cfg.contracts); m_seLmt.Send(sellAt, Cfg.contracts);
			m_r = r; m_stop = double.NaN;
		}

		/// <summary>New completed day in M-DAILY: ATR20d and HV20 from the (roll-adjusted) daily context, and the A2-70 hysteresis gate.</summary>
		private void stepDaily()
		{
			_DailyContext d = DailyContext;
			if (d == null || !d.Valid || d.PrevDate == m_lastDaily) return;
			m_lastDaily = d.PrevDate;
			m_atr = d.Atr20d; m_hv = d.Hv(Cfg.hvLength);
			if (double.IsNaN(m_hv)) return;
			bool was = m_gateOff;
			double theta = thetaOff();
			if (!m_gateOff && m_hv > theta) m_gateOff = true;
			else if (m_gateOff && m_hv < Cfg.reEnable * theta) m_gateOff = false;
			if (was != m_gateOff && journalOn()) trace(ETraceLevel.Journal, $"A2-70 {d.PrevDate:yyyy-MM-dd} HV20 {m_hv:P1} gate {(m_gateOff ? "OFF" : "ON")}");
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }

		private TimeSpan flatAt(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan f = Cfg.flat;
			if (cme.RthCloseEt.HasValue && cme.RthCloseEt.Value - Cfg.halfDayExitBefore < f) f = cme.RthCloseEt.Value - Cfg.halfDayExitBefore;
			return f;
		}
	}
}
