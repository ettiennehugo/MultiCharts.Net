// ## 1. Identity
// Catalogue ID: A2-38
// ID mapping: A2-38 -> _A2_38_MetaorderDrift.Strategy.CS (class _A2_38_MetaorderDrift).
// Name: A2-38 metaorder drift completion
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15 after main 9107598.
// Study class: _A2_38_MetaorderDrift
// File: sandbox/_A2_38_MetaorderDrift.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-38.
// Taken from the source: the Rules or Analyst 2 specification under that heading.
// Research assumption: ASSUMPTION: Trade-size entropy is not computed in _TickStream, and the catalogue says the row is not yet buildable. The file journals that and does not invent a detector.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-38 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: one contract.
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: market next bar when this file trades.
// Can fill: the next bar. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: flat or one.
// Re-entry: one attempt per session where the file trades.
// Same session after a stop: no new entry that session.
//
// ## 7. Instruments
// Typical: ES or NQ as the catalogue says. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: 1-minute unless section 22 says daily. Tests use data through 2024-12-31 only.
// Data2: named in section 22, or Not applicable.
// Further series: Not applicable
// Warmup: the statistic named in the rule.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue setup, and the 1 percent budget when an order is sent.
// Skip when: the statistic is not ready, or the budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract where this file trades. journalArm is written by exitPlan when GX is used.
// Risk limit: skip when 1 contract times the stop distance times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: the order name in Create, when this file trades.
// Exit orders: see section 13.
// Fill assumption: next bar for a market order. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number.
//
// ## 12. Trade management
// While open: exitBar, or sendExits, as section 13 says.
//
// ## 13. Exits
// Exit orders: this file sends none. GX is not used because the catalogue row is not an entry.
// Hard stop: the catalogue stop when this file trades.
// Target: the catalogue target when GX or sendExits names one.
// Time exit: the catalogue flat time.
// Invalidation: the catalogue skip.
//
// ## 14. Regimes
// Suitable: the catalogue window.
// Unsuitable: outside that window.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: those set in Create or StartCalc.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series where this file trades.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: Trade-size entropy is not computed in _TickStream, and the catalogue says the row is not yet buildable. The file journals that and does not invent a detector.
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name, when an entry is sent.
// logSignal tag: A2-38
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: Trade-size entropy is not computed in _TickStream, and the catalogue says the row is not yet buildable. The file journals that and does not invent a detector.
// IOG is disabled.

using System;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_38_MetaorderDrift : _CustomSignal
	{
		private DateTime m_day = DateTime.MinValue;
		public _A2_38_MetaorderDrift(object _ctx) : base(_ctx) { }
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = Bars.Time[0].Date;
			if (day == m_day) return;
			m_day = day;
			if (journalOn()) trace(ETraceLevel.Journal, "A2-38 " + day.ToString("yyyy-MM-dd") + " Trade-size entropy is not computed in _TickStream, and the catalogue says the ro");
		}
	}
}
