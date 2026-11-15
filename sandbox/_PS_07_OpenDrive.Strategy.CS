// ## 1. Identity
// Catalogue ID: PS-07
// ID mapping: PS-07 -> _PS_07_OpenDrive.Strategy.CS (class _PS_07_OpenDrive).
// Name: PS-07 open drive, continuation on the first higher low
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _PS_07_OpenDrive
// File: sandbox/_PS_07_OpenDrive.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for PS-07. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the PS-07 heading. Do not paste the entry.
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
// Market tag: futures-only
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
// logSignal tag: PS-07
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// The worked example class was _Ex19OpenDrive.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 19 - PS-07 open drive, continuation on the first higher low (core arm).
// Drive test (operationalised): the 09:30-09:45 range >= driveMult x the sum of the three same-slot TOD Range medians, and the
// 09:45 close in the outer third of that range in the drive direction. Entry: after the first PA pivot low labelled HL (long) / pivot
// high labelled LH (short) that confirms before 11:30, a stop order 1 tick beyond the confirming bar. Stop: the pivot -/+ 1 tick, floored.
// Exit: 11:30 time stop. One trade per day.
// Chart: Data1 = @ES 5m (24h Globex template), dxFeed continuous UNADJUSTED.
// Features: M-TOD (TodMetrics = Range, Median, Valid), M-PA (PriceActionConfig, NewPivotLow/High, LastLow/LastHigh, EPivotLabel), F-SESS.
using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _PS_07_OpenDrive : _VolumeProfileFuturesBase
	{
		protected sealed class Ps07Config
		{
			public int contracts = 1;
			public double driveMult = 0.6;                  // PLACEHOLDER threshold, not a tested value
			public TimeSpan driveEnd = new TimeSpan(9, 45, 0), timeStop = new TimeSpan(11, 30, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Ps07Config Cfg { get; } = new Ps07Config();

		private IOrderPriced m_leStp, m_seStp, m_lxStp, m_sxStp;
		private IOrderMarket m_lx, m_sx;
		private DateTime m_day = DateTime.MinValue;
		private double m_hi, m_lo, m_medSum, m_trigger = double.NaN, m_stop = double.NaN;
		private int m_slots, m_side;
		private bool m_done;

		public _PS_07_OpenDrive(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			TodMetrics = ETodMetric.Range; TodSessions = 20; TodMinSessions = 10; TodBucketMinutes = 5;
			PriceActionConfig.enabled = true;               // Futures preset applied by the base
			PriceActionConfig.dataStream = 1;
			m_leStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "PS07LE", EOrderAction.Buy));
			m_seStp = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "PS07SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS07LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "PS07SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS07LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PS07SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_day = DateTime.MinValue; m_side = 0; m_done = false; m_trigger = m_stop = double.NaN;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_hi = double.MinValue; m_lo = double.MaxValue; m_medSum = 0; m_slots = 0; m_side = 0; m_done = false; m_trigger = double.NaN; }
			if (rollOn(1)) m_done = true;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			int pos = StrategyInfo.MarketPosition;
			double tick = tickSize();
						// Exits. The only place this bar sends an exit order. A later GX exitPlan replaces this local function.
			bool sendExits()
			{
				m_done = true;                                        // one trade per day: never re-arm after this position
				if (tod >= Cfg.timeStop) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return true; }
				if (pos > 0) m_lxStp.Send(m_stop); else m_sxStp.Send(m_stop);
				return true;
			
			}
			if (pos != 0 && sendExits()) return;
			if (m_done || !IsRth()) return;
			if (tod > new TimeSpan(9, 30, 0) && tod <= Cfg.driveEnd)
			{
				m_hi = Math.Max(m_hi, Bars.High[0]); m_lo = Math.Min(m_lo, Bars.Low[0]);
				if (TodNormaliser != null && TodNormaliser.Valid(ETodMetric.Range)) { m_medSum += TodNormaliser.Median(ETodMetric.Range); m_slots++; }
				if (tod == Cfg.driveEnd) classifyDrive();
				return;
			}
			if (m_side == 0 || tod >= Cfg.timeStop) return;
			_PriceActionEngine pa = PriceActionEngine;
			if (pa == null || !pa.IsWarm) return;
			if (double.IsNaN(m_trigger))
			{
				_Pivot p = m_side > 0 ? (pa.NewPivotLow ? pa.LastLow(0) : null) : (pa.NewPivotHigh ? pa.LastHigh(0) : null);
				if (p == null || p.label != (m_side > 0 ? EPivotLabel.HL : EPivotLabel.LH)) return;
				m_trigger = m_side > 0 ? Bars.High[0] + tick : Bars.Low[0] - tick;
				m_stop = m_side > 0 ? p.price - tick : p.price + tick;
				if (m_side * (m_trigger - m_stop) < Cfg.costFloorTicks * tick) m_stop = m_trigger - m_side * Cfg.costFloorTicks * tick;
				double r = Math.Abs(m_trigger - m_stop);
				if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"PS-07 {day:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget"); m_done = true; return; }
				if (journalOn()) journalArm(m_side > 0 ? "PS07LE" : "PS07SE", m_trigger, m_stop, double.NaN, null);
			}
			if (m_side > 0) m_leStp.Send(m_trigger, Cfg.contracts); else m_seStp.Send(m_trigger, Cfg.contracts);
		}


		private void classifyDrive()
		{
			if (m_slots < 3) return;                                  // TOD history not warm for all three slots
			double range = m_hi - m_lo, c = Bars.Close[0];
			if (!(range > 0) || range < Cfg.driveMult * m_medSum) return;
			if (c >= m_hi - range / 3) m_side = +1; else if (c <= m_lo + range / 3) m_side = -1;
			if (m_side != 0 && journalOn()) trace(ETraceLevel.Journal, $"PS-07 {m_day:yyyy-MM-dd} drive {(m_side > 0 ? "up" : "down")} range {range:F2} vs sum-median {m_medSum:F2}");
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
	}
}
