// ## 1. Identity
// Catalogue ID: A2-09
// ID mapping: A2-09 -> _A2_09_NoiseBandLong.Strategy.CS (class _A2_09_NoiseBandLong).
// Name: A2-09 noise-band long-only
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15. There is no engines-manual example for this id.
// Study class: _A2_09_NoiseBandLong
// File: sandbox/_A2_09_NoiseBandLong.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-09.
// Taken from the source: the Rules line of that heading.
// Research assumption: ASSUMPTION: Chart is 1-minute. The move from the open is a fraction, |close-rthOpen|/rthOpen, averaged over the same clock minute across the last 14 sessions. A gap day uses max(rthOpen, prior RTH close) as the band base. sVWAP is CurrentVwap1 with Vwap1Type Session. Check times are bar-end minutes of :00 and :30. Re-entry is allowed. There is no protective stop in the source; the exit is the band check and the 15:59 flat.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-09 heading. Do not paste the entry.
//
// ## 4. Trade walkthrough
// Trigger: the catalogue rule, on a closed bar.
// Management: sendExits re-sends the stop and the target while the position is open. Size stays 1 contract.
// Exit: the catalogue stop, target or time exit, sent only from sendExits. Not the GX wrapper.
//
// ## 5. Timing
// Signal evaluated: closed bar only. CalcBar returns unless Bars.Status is EBarState.Close, after base.CalcBar.
// Order sent: market next bar, or a stop, as the rule states.
// Can fill: the next bar after the signal bar, or a later bar for a resting stop or limit. IOG is disabled.
//
// ## 6. Position and re-entry
// Position: one contract, long or short as the rule states.
// Re-entry: as the catalogue states. Where it is silent, one attempt per session.
// Same session after a stop: no new entry that session, except where the catalogue allows re-entry.
//
// ## 7. Instruments
// Market tag: futures-only
// Typical: the catalogue market. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute.
// Data1: the chart the header names. Tests use data through 2024-12-31 only.
// Data2: Not applicable. Higher-timeframe bars are built from Data1.
// Further series: Not applicable
// Warmup: the ATR or average named in the rule.
//
// ## 9. Entry and filters
// All of these must be true: the catalogue setup and the 1 percent risk-budget check.
// Skip when: the budget fails, a required average is not ready, or the rule's filter fails.
//
// ## 10. Position sizing and risk
// Size: 1 contract. journalArm records the planned entry.
// Risk limit: skip when 1 contract times the widened stop times BigPointValue exceeds 1 percent of equity.
// Size is never shrunk to fit a stop: yes.
//
// ## 11. Orders, execution, and costs
// Entry order: the order name in Create.
// Exit orders: sent only from sendExits.
// Fill assumption: next bar for a market order. A stop or limit rests until filled. IOG is off.
// Costs: MultiCharts commission and slippage. This file does not set a fee or a slippage number. Not yet verified
//
// ## 12. Trade management
// While open: sendExits re-sends the stop and the target. Nothing else is added.
//
// ## 13. Exits
// Exit orders are sent only from sendExits. Replacing that method with exitPlan is the GX move.
// Hard stop: the catalogue stop, widened to 14 ticks when the rule places a stop.
// Target: the catalogue target, or Not applicable.
// Time exit: the catalogue flat time, via FlatBy and mustFlatten inside sendExits.
// Invalidation: the catalogue skip rules.
//
// ## 14. Regimes
// Suitable: the catalogue session window.
// Unsuitable: outside that window.
//
// ## 15. Dependencies
// Base class: _VolumeProfileFuturesBase
// Engines enabled: those set in Create or StartCalc.
// Engine settings enabled: RollDetection and the other protected settings set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders are sent from a closed bar for a later bar.
// Repaint: Not applicable. Closed bars only.
// Survivorship: Not applicable
// Rolls: RollDetection Series, unless the file says otherwise.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: Chart is 1-minute. The move from the open is a fraction, |close-rthOpen|/rthOpen, averaged over the same clock minute across the last 14 sessions. A gap day uses max(rthOpen, prior RTH close) as the band base. sVWAP is CurrentVwap1 with Vwap1Type Session. Check times are bar-end minutes of :00 and :30. Re-entry is allowed. There is no protective stop in the source; the exit is the band check and the 15:59 flat.
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
// Uncertainty: no run yet; the ASSUMPTION in section 16; GX is not on this branch; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: the entry order name.
// logSignal tag: A2-09
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: Chart is 1-minute. The move from the open is a fraction, |close-rthOpen|/rthOpen, averaged over the same clock minute across the last 14 sessions. A gap day uses max(rthOpen, prior RTH close) as the band base. sVWAP is CurrentVwap1 with Vwap1Type Session. Check times are bar-end minutes of :00 and :30. Re-entry is allowed. There is no protective stop in the source; the exit is the band check and the 15:59 flat.
// IOG is disabled. GX exitPlan is not called. Exit orders are sent only from sendExits.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_09_NoiseBandLong : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_lx;
		private readonly Dictionary<int, List<double>> m_hist = new Dictionary<int, List<double>>();
		private int m_lastSlot = -1;

		public _A2_09_NoiseBandLong(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			FlatBy = new TimeSpan(15, 59, 0);
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A209LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A209LX", EOrderAction.Sell));
		}

		protected override void StartCalc()
		{
			Vwap1Type = EVwapType.Session; Vwap1DataStream = 1;
			base.StartCalc();
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			bool check = tod.Seconds == 0 && tod.Minutes % 30 == 0 && tod >= new TimeSpan(9, 30, 0) && tod < new TimeSpan(16, 0, 0);
			int pos = StrategyInfo.MarketPosition;
			SessionStats s = Session;
			double vwap = CurrentVwap1;
			double upper = double.NaN;
			if (check && s != null && s.rthOpen > 0)
			{
				int slot = (int)tod.TotalMinutes;
				double frac = Math.Abs(Bars.Close[0] - s.rthOpen) / s.rthOpen;
				List<double> hist;
				if (!m_hist.TryGetValue(slot, out hist)) { hist = new List<double>(); m_hist[slot] = hist; }
				if (hist.Count >= 14)
				{
					double mean = 0; for (int i = hist.Count - 14; i < hist.Count; i++) mean += hist[i]; mean /= 14.0;
					bool gap = s.priorRthClose > 0 && Math.Abs(s.rthOpen - s.priorRthClose) > 0;
					double basis = gap ? Math.Max(s.rthOpen, s.priorRthClose) : s.rthOpen;
					upper = basis * (1.0 + mean);
				}
				hist.Add(frac); if (hist.Count > 14) hist.RemoveAt(0);
			}
			bool sendExits()
			{
				if (mustFlatten()) { m_lx.Send(); return true; }
				if (check && !double.IsNaN(upper) && !double.IsNaN(vwap) && Bars.Close[0] < Math.Max(upper, vwap)) { m_lx.Send(); return true; }
				return false;
			}
			if (pos > 0 && sendExits()) return;
			if (pos != 0 || !check || double.IsNaN(upper) || rollOn(1)) return;
			if (!(Bars.Close[0] > upper)) return;
			m_le.Send();
			if (journalOn()) journalArm("A209LE", Bars.Close[0], double.NaN, double.NaN, null);
		}
	}
}
