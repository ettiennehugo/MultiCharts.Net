// ## 1. Identity
// Catalogue ID: OF-01
// ID mapping: OF-01 -> _OF_01_StackedImbalanceRetest.Strategy.CS (class _OF_01_StackedImbalanceRetest).
// Name: OF-01 stacked-imbalance origin, first retest
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _OF_01_StackedImbalanceRetest
// File: sandbox/_OF_01_StackedImbalanceRetest.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for OF-01. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the OF-01 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the worked example's entry, on a closed bar.
// Management: the example's stop, if it has one, re-sent while the position is open. Size stays 1 contract.
// Exit: the example's target, stop or time exit. Not the GX wrapper. GX is specified and not yet in the build.
// Exit orders are sent only from sendExits. Replacing that method with exitPlan is the GX move.
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
// Base class: _VolumeProfileFuturesBase
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
// catalogue-default: judge this file by its catalogue kill, catalogue section 2.3. The stricter stated rule decides. This header does not change the catalogue.
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
// logSignal tag: OF-01
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex38StackedImbalanceRetest.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 38 - OF-01 stacked-imbalance origin, first retest continuation (long side shown; short mirrors).
// Zone: a closed 5m bar with >= 3 stacked BUY imbalances (ratio 3:1, VPF-5 StackedImbalances) that closes in its upper 30%;
// zone = the lowest buy run [lo, hi]. Flow quality must be Exact or High (the catalogue warns TickRule imbalances are artefacts).
// Arming: price then moves >= 1 x ATR(14, 5m) above the zone top. Entry: first retest only - buy limit at the zone top
// (catalogue entry arm "limit"; the 1m HL arm is not coded). Stop: zone low - 1 tick, floored at 14 ticks. Target: 1R.
// Deviation: the catalogue's volume floor P70 is not computed; minVol = 10 contracts is a PLACEHOLDER. Zone expires at session end.
// Chart: Data1 @ES 5m, dxFeed continuous UNADJUSTED. Features: VPF order flow (OrderFlowEnabled, StackedImbalances, FlowQuality).
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _OF_01_StackedImbalanceRetest : _VolumeProfileFuturesBase
	{
		protected sealed class Of01Config
		{
			public int contracts = 1, minStack = 3, atrLength = 14, costFloorTicks = 14;
			public double ratio = 3.0, minVol = 10 /* PLACEHOLDER for P70 */, closeTopFrac = 0.30, awayAtr = 1.0, targetR = 1.0, riskBudgetFraction = 0.01;
			public TimeSpan flat = new TimeSpan(15, 55, 0);
		}
		protected Of01Config Cfg { get; } = new Of01Config();

		private IOrderPriced m_leLmt, m_lxStp, m_lxLmt;
		private IOrderMarket m_lx;
		private DateTime m_day = DateTime.MinValue;
		private bool m_zone, m_armed, m_used;
		private double m_zLo, m_zHi, m_stop, m_target;

		public _OF_01_StackedImbalanceRetest(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			OrderFlowEnabled = true;                          // lazy: footprints only exist when enabled
			FlatBy = Cfg.flat;
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "OF01LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "OF01LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "OF01LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "OF01LT", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc() { base.StartCalc(); m_day = DateTime.MinValue; }

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_zone = m_armed = m_used = false; }
			if (rollOn(1)) m_zone = false;
			int pos = StrategyInfo.MarketPosition;
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_used = true;
				if (mustFlatten()) { m_lx.Send(); return true; }
				m_lxStp.Send(m_stop); m_lxLmt.Send(m_target);
				return true;
			
			}
			if (pos != 0 && sendExits()) return;
			if (!IsRth()) return;
			double tick = tickSize(), h = Bars.High[0], l = Bars.Low[0], c = Bars.Close[0];
			double atr = averageTrueRange(Bars, Cfg.atrLength);
			if (m_zone && !m_used)
			{
				if (!m_armed && h >= m_zHi + Cfg.awayAtr * atr) m_armed = true;
				else if (m_armed && l <= m_zHi) { m_used = true; m_zone = false; return; }   // the retest happened without a fill: first retest only
				if (m_armed && !mustFlatten()) { m_leLmt.Send(m_zHi, Cfg.contracts); return; }
			}
			if (m_zone || m_used || h - l <= 0 || (c - l) / (h - l) < 1 - Cfg.closeTopFrac) return;
			_OrderFlow.StackedResult s = StackedImbalances(0, Cfg.ratio, Cfg.minVol, Cfg.minStack);
			if (!s.Found || s.Quality > _OrderFlow.FlowQuality.High) return;
			foreach (double[] run in s.Runs)
			{
				if (run[3] <= 0) continue;                      // buy runs first, low -> high: take the lowest
				double stop = run[0] - tick, entry = run[1];
				if (entry - stop < Cfg.costFloorTicks * tick) stop = entry - Cfg.costFloorTicks * tick;
				double r = entry - stop;
				if (Cfg.contracts * r * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction))
				{ if (journalOn()) trace(ETraceLevel.Journal, $"OF-01 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: risk budget"); return; }
				m_zLo = run[0]; m_zHi = run[1]; m_stop = stop; m_target = entry + Cfg.targetR * r; m_zone = true; m_armed = false;
				if (journalOn()) journalArm("OF01LE", entry, m_stop, m_target, null);
				return;
			}
		}
	}
}
