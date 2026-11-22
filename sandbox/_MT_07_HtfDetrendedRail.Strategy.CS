// ## 1. Identity
// Catalogue ID: MT-07
// ID mapping: MT-07 -> _MT_07_HtfDetrendedRail.Strategy.CS (class _MT_07_HtfDetrendedRail).
// Name: MT-07 higher-timeframe rail gate
// Header version: 1
// Rules version: catalogue specification under the MT-07 heading, rewritten 2026-11-15.
// Study class: _MT_07_HtfDetrendedRail
// File: sandbox/_MT_07_HtfDetrendedRail.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the MT-07 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: This file is PS-25. It sends that entry only after two 60-minute closes (closed Data2 60-minute bars) have held beyond the detrended VAH.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the MT-07 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: see CalcBar. Closed bar only.
// Management: one contract when this file trades. Size is never cut to fit the stop.
// Exit: see section 13.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: the order type named in the catalogue (market next bar, stop, or limit).
// Can fill: the next bar, or later for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: flat or one, unless the catalogue says a reverse, which this file does not open inside GX.
// Re-entry: one attempt per session unless the catalogue says otherwise.
// Same session after a stop: no new entry that session unless the catalogue says otherwise.
//
// ## 7. Instruments
// Market tag: futures-only
// Typical: the chart instrument. No symbol is hard-coded.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute. Data2 = 60-minute.
// Data1: the chart. Tests use data through 2024-12-31 only.
// Data2: the same instrument on 60-minute bars (owner rule: Data2 = native timeframe). CalcBar reads closed Data2 bars only, through HtfClosedBar.
// Further series: Not applicable
// Warmup: the average or profile the rule names.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue trigger and the 1 percent budget when an order is sent.
// Skip when: rollOn on a futures profile idea, the profile or average is not ready, or the budget fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract when this file trades.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes. The floor is InstrumentProfile.costFloorTicks times tickSize().
//
// ## 11. Orders, execution, and costs
// Entry order: named in Create when this file trades.
// Exit orders: see section 13.
// Fill assumption: next bar for a market order. A stop or limit rests. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number.
//
// ## 12. Trade management
// While open: exitBar when section 13 says GX, otherwise sendExits.
//
// ## 13. Exits
// No exit orders. This file does not open a position.
// Hard stop: the catalogue stop, widened to the instrument cost floor.
// Target: the catalogue target when it is a price known at the signal.
// Time exit: the catalogue clock, taken from the session API when the rule says the session end.
// Invalidation: any extra exit named in the assumption.
//
// ## 14. Regimes
// Suitable: the session the code consults.
// Unsuitable: outside that session, and a roll day when the rule skips rolls.
//
// ## 15. Dependencies
// Base class: _PS_25_AcceptBeyondRail
// Engines enabled: set in Create.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series when the base is the futures profile base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: This file is PS-25. It sends that entry only after two 60-minute closes (closed Data2 60-minute bars) have held beyond the detrended VAH.
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
// Fixed or research-tuned: the catalogue core, not a sweep. The sweep stays in the catalogue.
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
// journalArm name: the entry order name when an entry is sent.
// logSignal tag: MT-07
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: This file is PS-25. It sends that entry only after two 60-minute closes (closed Data2 60-minute bars) have held beyond the detrended VAH.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _MT_07_HtfDetrendedRail : _PS_25_AcceptBeyondRail
	{
		private int m_htf;
		// Coder 2026-11-22: Data2 (the native timeframe) read through the M-MTF closed-bar rule. newNativeBar() is true once per new
		// closed Data2 bar; nativeBar(ago) is the ago-th closed Data2 bar; nativeAtr(n) is the mean true range of the last n closed bars.
		private DateTime m_nativeTime = DateTime.MinValue;
		private bool nativeReady(int ago) { return IsExist(2) && BarsOfData(2).CurrentBar > ago + 2; }
		private bool newNativeBar()
		{
			if (!nativeReady(0)) return false;
			DateTime t; double o, h, l, c, v; HtfClosedBar(2, 0, out t, out o, out h, out l, out c, out v);
			if (t == m_nativeTime) return false;
			m_nativeTime = t; return true;
		}
		private void nativeBar(int ago, out double o, out double h, out double l, out double c, out double v) { DateTime t; HtfClosedBar(2, ago, out t, out o, out h, out l, out c, out v); }
		private double nativeAtr(int n)
		{
			if (!nativeReady(n)) return double.NaN;
			double sum = 0, o, h, l, c, v, po, ph, pl, pc, pv;
			for (int k = 0; k < n; k++)
			{
				nativeBar(k, out o, out h, out l, out c, out v); nativeBar(k + 1, out po, out ph, out pl, out pc, out pv);
				sum += Math.Max(h, pc) - Math.Min(l, pc);
			}
			return sum / n;
		}
		public _MT_07_HtfDetrendedRail(object _ctx) : base(_ctx) { }
		protected override void StartCalc()
		{
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			m_htf = 0; m_nativeTime = DateTime.MinValue;   // a recalc starts the 60-minute count again
			base.StartCalc();
		}

		protected override void CalcBar()
		{
			if (Bars.Status == EBarState.Close)
			{
				if (!IsExist(2)) { if (journalOn()) trace(ETraceLevel.Error, "Data2 missing"); return; }
				// Coder 2026-11-22: the 60-minute close is the last closed Data2 60-minute bar (it was one 1-minute close at HH:00).
				if (newNativeBar())
				{
					double o, h, l, c, v; nativeBar(0, out o, out h, out l, out c, out v);
					double vah = dvpRailPrice(1, ELevelKind.DvpVah, 0);
					if (!double.IsNaN(vah) && c > vah) m_htf++;
					else m_htf = 0;
				}
			}
			base.CalcBar();
		}
		protected override bool htfAccepted() { return m_htf >= 2; }
	}
}
