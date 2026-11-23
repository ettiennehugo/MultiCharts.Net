// ## 1. Identity
// Catalogue ID: A2-31
// ID mapping: A2-31 -> _A2_31_KlGate.Strategy.CS (class _A2_31_KlGate).
// Name: A2-31 KL acceptance gate
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15 after main 9107598.
// Study class: _A2_31_KlGate
// File: sandbox/_A2_31_KlGate.Strategy.CS
// Stage: sandbox
// Non-trading signal: KL-divergence gate on prior-day levels. It does not enter.
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-31.
// Taken from the source: the Rules or Analyst 2 specification under that heading.
// Research assumption: ASSUMPTION: This row is a session classifier, not an entry. At 12:00 it journals D_KL from _ProfileStats.KL between today's developing RTH bars and the prior RTH session. It does not send an order. Thresholds are the catalogue default 0.15 and 0.60.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-31 heading. Do not paste the entry.
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
// Market tag: futures-only
// Typical: ES or NQ as the catalogue says. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute.
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
// Base class: _VolumeProfileFuturesBase
// Engines enabled: those set in Create or StartCalc.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Off: this file does not set a roll mode (Off on futures too; rollOn never fires). Not migrated to Table (T-R2 review list).
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: This row is a session classifier, not an entry. At 12:00 it journals D_KL from _ProfileStats.KL between today's developing RTH bars and the prior RTH session. It does not send an order. Thresholds are the catalogue default 0.15 and 0.60.
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
// logSignal tag: A2-31
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: This row is a session classifier, not an entry. At 12:00 it journals D_KL from _ProfileStats.KL between today's developing RTH bars and the prior RTH session. It does not send an order. Thresholds are the catalogue default 0.15 and 0.60.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_31_KlGate : _VolumeProfileFuturesBase
	{
		private DateTime m_day = DateTime.MinValue;
		private bool m_logged;

		public _A2_31_KlGate(object _ctx) : base(_ctx) { }

		protected override void Create() { base.Create(); ProfileStats.enabled = true; }

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_logged = false; }
			if (m_logged || ToEastern(Bars.Time[0]).TimeOfDay < new TimeSpan(12, 0, 0)) return;
			m_logged = true;
			DateTime prev = DateTime.MinValue;
			for (int ago = 0; ago < Math.Min(2000, Bars.CurrentBar); ago++)
			{
				RingBar rb = ringBarOf(1, ago);
				if (rb.tradingDay < day && rb.tradingDay != DateTime.MinValue) { prev = rb.tradingDay; break; }
			}
			List<RingBar> today = SessionBars(day, true), prior = prev == DateTime.MinValue ? null : SessionBars(prev, true);
			double[] tp, tv, pp, pv;
			double kl = double.NaN;
			if (today != null && prior != null && _ProfileStats.Histogram(today, null, tickSize(), 2, out tp, out tv) && _ProfileStats.Histogram(prior, null, tickSize(), 2, out pp, out pv))
				kl = _ProfileStats.KL(tp, tv, pp, pv, tickSize(), 1e-6);
			string state = double.IsNaN(kl) ? "na" : kl < 0.15 ? "acceptance" : kl > 0.60 ? "migration" : "no-change";
			if (journalOn()) trace(ETraceLevel.Journal, "A2-31 " + day.ToString("yyyy-MM-dd") + " KL " + kl.ToString("F3") + " " + state);
		}
	}
}
