// ## 1. Identity
// Catalogue ID: A2-57
// ID mapping: A2-57 -> _A2_57_TwoTimeframeTurn.Strategy.CS (class _A2_57_TwoTimeframeTurn).
// Name: A2-57 60m turn then the first lower-timeframe oversold
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _A2_57_TwoTimeframeTurn
// File: sandbox/_A2_57_TwoTimeframeTurn.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-57. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the A2-57 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the worked example's entry, on a closed bar.
// Management: the example's stop, if it has one, re-sent while the position is open. Size stays 1 contract.
// Exit: the example's target, stop or time exit. Not the GX wrapper. GX is specified and not yet in the build.
//
// ## 5. Timing
// Signal evaluated: closed bar only. CalcBar returns unless Bars.Status is EBarState.Close, after base.CalcBar.
// Order sent: the example's order type (market next bar, stop or limit).
// Can fill: the next bar after the signal bar, or a later bar for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: the example's position rule, flat or one unless the example says otherwise.
// Re-entry: as the example codes it.
// Same session after a stop: as the example codes it.
//
// ## 7. Instruments
// Typical: @ES, the example's chart. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: the example's chart, dxFeed continuous unadjusted. Tests use data through 2024-12-31 only.
// Data2: the example's second series, or Not applicable when the example has none.
// Further series: Not applicable unless the example names one.
// Warmup: the example's indicator warmup.
//
// ## 9. Entry and filters
// All of these must be true: the example's entry conditions, and the 1 percent risk-budget check when the example has one.
// Skip when: the example journals a skip, a roll day when the example skips rolls, or the risk budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract where the example fixes size. journalArm records the planned entry.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity, when the example codes that check.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: the example's entry order name.
// Exit orders: the example's exit order names.
// Fill assumption: next bar for a market order. A stop or limit rests until filled. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number. Not yet verified
//
// ## 12. Trade management
// While open: the example re-sends its stop or target. Nothing else is added.
//
// ## 13. Exits
// Hard stop: the example's stop, or Not applicable when the example has none.
// Target: the example's target, or Not applicable.
// Time exit: the example's flat time, or Not applicable.
// Invalidation: the example's skip rules.
//
// ## 14. Regimes
// Suitable: the example's session window.
// Unsuitable: outside that window, and where the example stands aside.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: those the example turns on in Create or StartCalc.
// Engine settings enabled: RollDetection and the other protected settings the example sets. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders are sent from a closed bar for a later bar. HTF reads use HtfClosedBar when the example does.
// Repaint: Not applicable when the example uses closed bars only.
// Survivorship: Not applicable
// Rolls: as the example sets RollDetection.
// Data quality: Not yet verified on a chart.
// Other: this is a port of the worked example, not a new rule set. ASSUMPTION: the example's labelled choices stand until an owner changes them.
//
// ## 17. Parameters
// This file declares no strategy [Input] besides the inherited TraceLevel.
// Input: none
// Purpose: Not applicable
// Default: Not applicable
// Units: Not applicable
// Range: Not applicable
// Effect: Not applicable
// Interactions: Not applicable
// Fixed or research-tuned: Not applicable
//
// ## 18. Acceptance policy
// Policy: catalogue-default
// Predeclared before any result was read: yes, 2026-11-15. No result for this file has been read.
// catalogue-default: judge this file by its catalogue kill, catalogue section 2.3 and the A2 generic kill. The stricter stated rule decides. This header does not change the catalogue.
// big-winner-trend: Not applied
// When 1 percent of winning trades is not a whole number, remove K = max(1, ceil(0.01 times the number of winning trades)). If there are no winning trades, remove none.
//
// ## 19. Validation
// Status: Not yet verified
// Evidence: Not yet verified
// Window used: a run, when one exists, uses data through 2024-12-31 only.
// Holdout (W-M and W-D Jan 2025-Oct 2026; W-T last 125 sessions) stays unviewed. Do not put those results here.
//
// ## 20. Edge monitoring
// A person reads this section. It does not place orders and it does not change parameters.
// Expectancy: Not yet verified
//   Formula: win_rate * avg_win - loss_rate * avg_loss, same cost model as section 11.
// Observations: Not yet verified
// Drift: Not yet verified
// Warning: Not yet verified
// Pause: Not yet verified
// Review: Not yet verified
// Uncertainty: no run yet; the example's assumptions; GX is not in the build; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the example's entry order name, when it calls journalArm.
// logSignal tag: A2-57
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex05TwoTimeframeTurn.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 5 - A2-57 60m EMA(13) slope up-turn + FIRST 12m %K(3) <= 20, buy stop above the signal bar (long arm, base config).
// Chart: Data1 = @ES 1m, Data2 = @ES 12m, Data3 = @ES 60m (same symbol and 24h Globex template), dxFeed continuous UNADJUSTED.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_57_TwoTimeframeTurn : _CustomSignal
	{
		protected sealed class A257Config
		{
			public int contracts = 1;
			public int emaLength = 13, stochLength = 3, validBars = 2;
			public double osLevel = 20;
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);   // early-close days: flat at RthCloseEt - 5 min
			public int costFloorTicks = 14;  // catalogue 2.1: ES cost floor; stop distance = max(structural, floor), size never shrunk
			public double riskBudgetFraction = 0.01;  // floor-rule risk budget: Ettienne, 2026-10-23: 1% of equity (PF-04's 0.5% is not the default here)
			public double trailKeep = 0.5;   // after +1R: protect 50% of the peak open profit
		}
		protected A257Config Cfg { get; } = new A257Config();

		private IOrderPriced m_leStp, m_lxStp;
		private IOrderMarket m_lx;
		private _CmeSessionClock m_clock;
		private DateTime m_last12 = DateTime.MinValue, m_last60 = DateTime.MinValue;
		private double m_ema = double.NaN, m_slope = double.NaN;
		private int m_ema60Count;
		private bool m_up, m_osTaken;          // 60m state; the first OS after this up-turn was already seen
		private double m_trigger = double.NaN, m_planStop = double.NaN; private int m_validLeft;
		private double m_stop = double.NaN, m_entry = double.NaN, m_risk = double.NaN, m_peak = double.NaN;
		private bool m_slopeDown;

		/// <summary>Catalogue 2.1 / App F #9 (resolved): true when Cfg.contracts at this stop distance risk more than the per-trade budget.</summary>
		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
		public _A2_57_TwoTimeframeTurn(object _ctx) : base(_ctx) { }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;   // unadjusted series: rollOn(1) below needs detection on (default is Off)
			m_leStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "A257LE", EOrderAction.Buy));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A257LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A257LX", EOrderAction.Sell));
		}

		protected override void StartCalc()
		{
			HtfStreams = new[] { 2, 3 };
			m_clock = null;
			base.StartCalc();
			m_last12 = m_last60 = DateTime.MinValue; m_ema = m_slope = double.NaN; m_ema60Count = 0;
			m_up = m_osTaken = m_slopeDown = false; m_trigger = m_planStop = double.NaN; m_validLeft = 0;
			m_stop = m_entry = m_risk = m_peak = double.NaN;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			TimeSpan tod = Clock.ToEastern(Bars.Time[0]).TimeOfDay;
			TimeSpan flat = flatAt(tradingDayKey(Bars.Time[0]));
			bool rthWindow = tod >= Cfg.from && tod < Cfg.to && tod < flat;

			if (rollOn(1))
			{
				m_trigger = double.NaN; m_validLeft = 0;   // never carry a level across the splice
				// re-seed the 60m EMA from the new contract: no slope turn counts until 3 x emaLength new closed 60m bars
				m_ema = m_slope = double.NaN; m_ema60Count = 0; m_up = false;
				if (journalOn()) trace(ETraceLevel.Journal, $"A2-57 roll {Bars.Time[0]:yyyy-MM-dd HH:mm}: 60m EMA re-seeded");
			}
			step60();
			bool new12 = step12(rthWindow);

			int pos = StrategyInfo.MarketPosition;
			if (pos > 0) { manage(tod, flat); return; }
			m_stop = m_entry = m_risk = m_peak = double.NaN;
			if (new12 && m_validLeft > 0) m_validLeft--;                    // "valid for the next 2 12m bars"
			if (m_validLeft > 0 && rthWindow && m_up && !double.IsNaN(m_trigger)) m_leStp.Send(m_trigger, Cfg.contracts);
		}

		/// <summary>New CLOSED 60m bar: update EMA(13) and the slope state.</summary>
		private void step60()
		{
			DateTime t; double o, h, l, c, v;
			HtfClosedBar(3, 0, out t, out o, out h, out l, out c, out v);
			if (t == m_last60) return;
			m_last60 = t;
			double prevEma = m_ema, prevSlope = m_slope, a = 2.0 / (Cfg.emaLength + 1);
			m_ema = double.IsNaN(m_ema) ? c : m_ema + a * (c - m_ema);
			m_ema60Count++;
			if (double.IsNaN(prevEma) || m_ema60Count < 3 * Cfg.emaLength) return;   // EMA warm-up (3 x length: an assumption)
			m_slope = m_ema - prevEma;
			bool turnUp = !double.IsNaN(prevSlope) && prevSlope <= 0 && m_slope > 0;
			if (turnUp) { m_up = true; m_osTaken = false; if (journalOn()) trace(ETraceLevel.Journal, $"A2-57 60m up-turn {t:yyyy-MM-dd HH:mm} ema({m_ema:F2})"); }
			if (m_slope <= 0) { m_up = false; m_slopeDown = true; m_trigger = double.NaN; m_validLeft = 0; } else m_slopeDown = false;
		}

		/// <summary>New CLOSED 12m bar: the first %K(3) &lt;= 20 after the up-turn arms the buy stop. Returns true on a new 12m bar.</summary>
		private bool step12(bool rthWindow)
		{
			DateTime t; double o, h, l, c, v;
			HtfClosedBar(2, 0, out t, out o, out h, out l, out c, out v);
			if (t == m_last12) return false;
			m_last12 = t;
			if (!m_up || m_osTaken || BarsOfData(2).CurrentBar <= Cfg.stochLength + 1) return true;
			double hh = double.MinValue, ll = double.MaxValue, prevLow = double.NaN;
			for (int k = 0; k < Cfg.stochLength; k++)
			{
				DateTime tk; double ok, hk, lk, ck, vk;
				HtfClosedBar(2, k, out tk, out ok, out hk, out lk, out ck, out vk);
				hh = Math.Max(hh, hk); ll = Math.Min(ll, lk);
				if (k == 1) prevLow = lk;
			}
			double pctK = hh > ll ? 100.0 * (c - ll) / (hh - ll) : 50.0;
			if (pctK > Cfg.osLevel) return true;
			m_osTaken = true;                       // FIRST oversold only: later ones are ignored (the key rule)
			if (!rthWindow) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-57 first OS {t:HH:mm} outside 09:45-15:00: turn forfeited"); return true; }
			double tick = tickSize();
			double trigger = h + tick, planStop = Math.Min(l, double.IsNaN(prevLow) ? l : prevLow) - tick;
			if (trigger - planStop < Cfg.costFloorTicks * tick) planStop = trigger - Cfg.costFloorTicks * tick;   // catalogue 2.1: widen to the cost floor
			if (overRiskBudget(trigger - planStop))   // widened stop breaches the per-trade risk budget: skipped and logged, not resized (this up-turn is used up)
			{
				if (journalOn()) trace(ETraceLevel.Journal, $"A2-57 {t:yyyy-MM-dd HH:mm} skip: stop {(trigger - planStop) / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}");
				return true;
			}
			m_trigger = trigger; m_planStop = planStop;
			m_validLeft = Cfg.validBars + 1;        // decremented on this bar's new12 pass, leaving 2 full 12m bars
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "pctK", pctK.ToString("F1") }, { "ema60", m_ema.ToString("F2") } };
				journalArm("A257LE", m_trigger, m_planStop, double.NaN, ctx);
				logSignal("A2-57", +1, m_trigger, m_planStop, ctx, true);
			}
			return true;
		}

		private void manage(TimeSpan tod, TimeSpan flat)
		{
			if (double.IsNaN(m_entry))
			{
				m_entry = StrategyInfo.AvgEntryPrice; m_stop = m_planStop; m_risk = m_entry - m_stop; m_peak = m_entry;
				m_trigger = double.NaN; m_validLeft = 0;   // one trade per up-turn
			}
			if ((tod >= flat && tod < new TimeSpan(17, 0, 0)) || m_slopeDown) { m_lx.Send(); return; }
			m_peak = Math.Max(m_peak, Bars.High[0]);
			if (m_risk > 0 && m_peak - m_entry >= m_risk)
			{
				double trail = m_entry + Cfg.trailKeep * (m_peak - m_entry);   // >= break-even once +1R is reached
				m_stop = Math.Max(m_stop, Math.Max(m_entry, trail));
			}
			m_lxStp.Send(m_stop);
		}

		/// <summary>Flat time for a Globex day: Cfg.flat, or on a CME early-close day RthCloseEt - halfDayExitBefore if earlier
		/// (_CmeHolidays.Rules.Get(day).RthCloseEt; same rule as A263Config.halfDayExitBefore).</summary>
		private TimeSpan flatAt(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan f = Cfg.flat;
			if (cme.RthCloseEt.HasValue && cme.RthCloseEt.Value - Cfg.halfDayExitBefore < f) f = cme.RthCloseEt.Value - Cfg.halfDayExitBefore;
			return f;
		}
	}
}
