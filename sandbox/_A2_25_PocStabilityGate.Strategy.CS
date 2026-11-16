// ## 1. Identity
// Catalogue ID: A2-25
// ID mapping: A2-25 -> _A2_25_PocStabilityGate.Strategy.CS (class _A2_25_PocStabilityGate).
// Name: A2-25 POC stability gate
// Header version: 1
// Rules version: catalogue entry as implemented by the engines-manual worked example. Ported 2026-11-15. The ten sandbox files from PR 124 are not edited.
// Study class: _A2_25_PocStabilityGate
// File: sandbox/_A2_25_PocStabilityGate.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-25. The code is the engines-manual worked example, renamed to the catalogue file stem.
// Taken from the source: the example's core arm, including its labelled assumptions.
// Research assumption: where the example labels an assumption, that label still applies. GX is not built, so the example's own exit orders remain. The control-arm exit is pending _GenericExit.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry and the example.
// Link: specs/trading-idea-catalogue.md, the A2-25 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the worked example's entry, on a closed bar.
// Management: the example's stop, if it has one, re-sent while the position is open. Size stays 1 contract where the example fixes size.
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
// Chart: Data1 = 1-minute.
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
// Size: 1 contract where the example fixes size. journalArm records the planned entry when the example calls it.
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
// Exit orders: this file sends none. Not applicable. The host, when this class extends one, sends its own from sendExits.
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
// Base class: _DV_02_FailedLevelFlip
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
// logSignal tag: A2-25
// Skip / flatten lines: the example's trace strings.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Slot copy: this class extends _DV_02_FailedLevelFlip. That file must be in the same compile or the slot does not build.
// The worked example class was _Ex35PocStabilityGate. The same file applies the A2-26 KDE flat/bimodal veto. There is no second A2-26 file.
// IOG is disabled. GX exitPlan is not called because _GenericExit is not in this build. The example's own exits remain.

// Manual v4 example 35 - A2-25 POC bootstrap-stability gate + A2-26 KDE bandwidth-sweep veto, as a module on the DV-02 host (Ex32).
// After the prior close, the prior RTH session's Data1 ring bars (SessionBars) feed _ProfileStats.BootstrapPoc (block 5, 500 draws,
// deterministic BootstrapSeed) and _ProfileStats.KdeSweep. Rules: KDE verdict FLAT or BIMODAL -> no trade (A2-26);
// stability >= 0.60 -> trade, stop distance = max(host bracket, ci90/2 + 1 tick) (A2-25); 0.25 <= stability < 0.60 -> the catalogue
// allows the trade only with the POC ZONE as the level; the DV-02 host is line-based, so this module skips that bucket (stated, not
// a catalogue rule); stability < 0.25 -> skip. Skip if the widened stop makes the target < 1R (the DV-02 bracket is symmetric, so the
// target is widened with it and stays 1R). Cut-offs 0.60/0.25 and block 5 are catalogue grid points.
// Features: F-PSTAT (ProfileStats.enabled -> ring, SessionBars, BootstrapPoc, BootstrapSeed, KdeSweep, EKdeVerdict), host hook.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_25_PocStabilityGate : _DV_02_FailedLevelFlip
	{
		protected double StabilityHigh { get; set; } = 0.60;
		protected double StabilityLow { get; set; } = 0.25;

		public _A2_25_PocStabilityGate(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			ProfileStats.enabled = true;                      // lazy: requires the Data1 ring (ProfileStats.ringBars) and range profiles
			ProfileStats.bootstrapBlock = 5; ProfileStats.bootstrapDraws = 500;
		}

		protected override bool pocGate(DateTime day, double poc, ref double bracket, out string why)
		{
			why = null;
			DateTime prev = priorTradingDay(day);
			List<RingBar> bars = prev == DateTime.MinValue ? null : SessionBars(prev, true);
			if (bars == null || bars.Count < 20) { why = "no prior RTH ring bars"; return false; }
			double tick = tickSize();
			KdeResult kde = _ProfileStats.KdeSweep(bars, tick, 2, poc, ProfileStats.kdeMedFloorTicks, ProfileStats.kdeFolds, ProfileStats.kdeFolding);
			if (kde.verdict == EKdeVerdict.Flat || kde.verdict == EKdeVerdict.Bimodal) { why = "KDE " + kde.verdict; return false; }
			int block = ProfileStats.bootstrapBlock, draws = ProfileStats.bootstrapDraws;
			BootstrapResult b = _ProfileStats.BootstrapPoc(bars, tick, 2, block, draws, _ProfileStats.BootstrapSeed(prev, block, draws));
			if (double.IsNaN(b.stability)) { why = "bootstrap n/a"; return false; }
			if (b.stability < StabilityHigh) { why = $"stability {b.stability:F2} < {StabilityHigh:F2} (zone bucket not handled by a line host)"; return false; }
			bracket = Math.Max(bracket, b.ci90Ticks / 2 * tick + tick);
			if (journalOn()) trace(ETraceLevel.Journal, $"A2-25 {day:yyyy-MM-dd} stability {b.stability:F2} ci90 {b.ci90Ticks:F0}t KDE {kde.verdict} bracket {bracket:F2}");
			return true;
		}

		private DateTime priorTradingDay(DateTime day)
		{
			int maxAgo = Math.Min(ProfileStats.ringBars, Bars.CurrentBar - 1);   // ringBarOf reads chart history: stay inside MaxBarsBack
			for (int ago = 0; ago < maxAgo; ago++)
			{
				RingBar rb = ringBarOf(1, ago);
				if (rb.tradingDay < day) return rb.tradingDay;
			}
			return DateTime.MinValue;
		}
	}
}
