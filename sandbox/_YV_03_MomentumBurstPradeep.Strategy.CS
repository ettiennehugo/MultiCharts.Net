// ## 1. Identity
// Catalogue ID: YV-03
// ID mapping: YV-03 -> _YV_03_MomentumBurstPradeep.Strategy.CS (class _YV_03_MomentumBurstPradeep).
// Name: YV-03 four-percent momentum burst
// Header version: 1
// Rules version: catalogue specification under the YV-03 heading, rewritten 2026-11-15.
// Study class: _YV_03_MomentumBurstPradeep
// File: sandbox/_YV_03_MomentumBurstPradeep.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the YV-03 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: the market-cap scanner is not available, so the chart is the candidate. A prior session whose absolute return is under 2 percent, then a session that is up at least 4 percent from the prior close within 15 minutes of the session open, buys. The stop is the session low, and it is pulled in to half the day's range when that low is farther than 3 percent. The hold is 4 sessions. A close that gives back half the open gain exits.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the YV-03 heading. Do not paste the entry.
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
// Market tag: stock-only
// Typical: the chart instrument. No symbol is hard-coded.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute.
// Data1: the chart. Tests use data through 2024-12-31 only.
// Data2: Not applicable unless CalcBar reads it.
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
// Exit orders: sendExits. Stop at the session low or the 3 percent cap. A close that gives back half the gain, or the fourth session, exits at the market.
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
// Base class: _VolumeProfileFuturesBase
// Engines enabled: set in Create.
// Engine settings enabled: set in code. They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Series when the base is the futures profile base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: ASSUMPTION: the market-cap scanner is not available, so the chart is the candidate. A prior session whose absolute return is under 2 percent, then a session that is up at least 4 percent from the prior close within 15 minutes of the session open, buys. The stop is the session low, and it is pulled in to half the day's range when that low is farther than 3 percent. The hold is 4 sessions. A close that gives back half the open gain exits.
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
// logSignal tag: YV-03
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: stock-only
// ASSUMPTION: ASSUMPTION: the market-cap scanner is not available, so the chart is the candidate. A prior session whose absolute return is under 2 percent, then a session that is up at least 4 percent from the prior close within 15 minutes of the session open, buys. The stop is the session low, and it is pulled in to half the day's range when that low is farther than 3 percent. The hold is 4 sessions. A close that gives back half the open gain exits.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _YV_03_MomentumBurstPradeep : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_lx; private IOrderPriced m_ls;
		private double m_stop, m_gain; private int m_held; private bool m_done; private DateTime m_day = DateTime.MinValue, m_entry = DateTime.MinValue;
		private readonly List<double> m_cl = new List<double>();
		public _YV_03_MomentumBurstPradeep(object _ctx) : base(_ctx) { }
		protected override void Create() { base.Create(); m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "YV03LE", EOrderAction.Buy)); m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "YV03LX", EOrderAction.Sell, OrderExit.FromAll)); m_ls = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "YV03LS", EOrderAction.Sell, OrderExit.FromAll)); }
		protected override void CalcBar()
		{
			base.CalcBar(); if (Bars.Status != EBarState.Close) return;
			DateTime day = Bars.Time[0].Date;
			int pos = StrategyInfo.MarketPosition;
			if (pos > 0) { m_ls.Send(m_stop); if (day != m_entry) m_held++; if (m_held >= 4 || Bars.Close[0] < Bars.Open[0] - 0.5 * m_gain) m_lx.Send(); return; }
			if (day != m_day)
			{
				if (Session.priorRthClose > 0 && (m_cl.Count == 0 || m_cl[m_cl.Count - 1] != Session.priorRthClose)) m_cl.Add(Session.priorRthClose);
				m_day = day; m_done = false; m_held = 0;
			}
			if (m_done || !IsRth() || !(Session.priorRthClose > 0)) return;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (tod > m_rthStart.Add(TimeSpan.FromMinutes(15))) return;
			double priorMove = 1;
			if (m_cl.Count >= 2 && m_cl[m_cl.Count - 2] > 0) priorMove = Math.Abs(m_cl[m_cl.Count - 1] - m_cl[m_cl.Count - 2]) / m_cl[m_cl.Count - 2];
			if (priorMove >= 0.02) return;
			double up = (Bars.Close[0] - Session.priorRthClose) / Session.priorRthClose;
			if (up < 0.04 || up > 0.06) return;
			double entry = Bars.Close[0];
			double stop = Session.rthLow;
			if (entry - stop > 0.03 * entry) stop = entry - 0.5 * (Session.rthHigh - Session.rthLow);
			stop = widen(true, entry, stop);
			if (!budget(entry, stop)) { m_done = true; return; }
			m_stop = stop; m_gain = Bars.Close[0] - Session.priorRthClose; m_entry = day;
			m_le.Send(); m_done = true;
		}

		private double widen(bool buy, double entry, double stop)
		{
			int ticks = 1;
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) ticks = p.costFloorTicks; } catch (Exception) { }
			double floor = ticks * tickSize();
			if (buy && entry - stop < floor) return entry - floor;
			if (!buy && stop - entry < floor) return entry + floor;
			return stop;
		}
		private int m_timeStop, m_flatOverride;
		private bool m_atrTrail;
		private bool budget(double a, double b) { double r = Math.Abs(a - b); return r > 0 && r * Bars.Info.BigPointValue <= equityAvailable(0.01); }
		private void arm(bool buy, double entry, double stop, double target, string name, int holdDays)
		{
			GxConfig.InitialStop = EInitialStop.EntryProvided;
			bool has = Math.Abs(target - entry) > tickSize();
			GxConfig.TargetMode = has ? ETargetMode.SingleTarget : ETargetMode.None;
			GxConfig.FinalTargetUnit = ETriggerUnit.Points;
			GxConfig.FinalTargetValue = has ? Math.Abs(target - entry) : 0;
			GxConfig.BreakEven = EBreakEven.Off; GxConfig.TrailMode = m_atrTrail ? ETrailMode.Atr : ETrailMode.None;
			GxConfig.SessionExit = holdDays > 0 ? ESessionExit.HoldOvernight : ESessionExit.FlatBy;
			GxConfig.MaxHoldDays = holdDays;
			GxConfig.FlatByHHMM = _SandboxRules.FlatBy(m_flatOverride, Bars.Info.Exchange);
			GxConfig.TimeStopMinutes = m_timeStop; GxConfig.SingleLotMode = ESingleLotMode.ExitAtT1;
			ExitMarket mkt = new ExitMarket(); mkt.Tick = tickSize();
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { }
			mkt.EntryStop = stop; mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0];
			mkt.BarKind = EGxBarKind.Minute; mkt.BarMinutes = 1;
			exitPlan(buy, entry, stop, 1, mkt, name);
		}
	}
}
