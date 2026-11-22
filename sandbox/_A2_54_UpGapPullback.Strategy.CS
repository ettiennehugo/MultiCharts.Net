// ## 1. Identity
// Catalogue ID: A2-54
// ID mapping: A2-54 -> _A2_54_UpGapPullback.Strategy.CS (class _A2_54_UpGapPullback).
// Name: A2-54 gap pullback to the prior close
// Header version: 1
// Rules version: catalogue specification under the A2-54 heading, rewritten 2026-11-15.
// Study class: _A2_54_UpGapPullback
// File: sandbox/_A2_54_UpGapPullback.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the A2-54 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: the gap threshold is 0.4 percent, the middle of the catalogue set 0.2, 0.4 and 0.6. An up gap sells a stop at the prior RTH close minus one tick; a down gap buys a stop at the prior close plus one tick. The stop is live from RthStart plus 15 minutes until RthValueEnd minus 30 minutes. The protective stop is the session extreme at the fill plus one tick, and it is no tighter than one 14-bar ATR. The exit is the next RTH open, which is not a GX FlatBy because FlatBy is the same session.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-54 heading. Do not paste the entry.
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
// Exit orders: sendExits. Protective stop while the position is open, and a market order on the first RTH bar of the next session. No same-session target.
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
// Other: ASSUMPTION: ASSUMPTION: the gap threshold is 0.4 percent, the middle of the catalogue set 0.2, 0.4 and 0.6. An up gap sells a stop at the prior RTH close minus one tick; a down gap buys a stop at the prior close plus one tick. The stop is live from RthStart plus 15 minutes until RthValueEnd minus 30 minutes. The protective stop is the session extreme at the fill plus one tick, and it is no tighter than one 14-bar ATR. The exit is the next RTH open, which is not a GX FlatBy because FlatBy is the same session.
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
// logSignal tag: A2-54
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: ASSUMPTION: the gap threshold is 0.4 percent, the middle of the catalogue set 0.2, 0.4 and 0.6. An up gap sells a stop at the prior RTH close minus one tick; a down gap buys a stop at the prior close plus one tick. The stop is live from RthStart plus 15 minutes until RthValueEnd minus 30 minutes. The protective stop is the session extreme at the fill plus one tick, and it is no tighter than one 14-bar ATR. The exit is the next RTH open, which is not a GX FlatBy because FlatBy is the same session.
// IOG is disabled.


using System;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_54_UpGapPullback : _VolumeProfileFuturesBase
	{
		private IOrderPriced m_le, m_se, m_ls, m_ss;
		private IOrderMarket m_lx, m_sx;
		private DateTime m_day = DateTime.MinValue, m_entryDay = DateTime.MinValue;
		private double m_stop; private int m_side; private bool m_armed;
		private bool m_done;
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_54_UpGapPullback(object _ctx) : base(_ctx) { }
		protected override void Create()
		{
			base.Create(); RollDetection = ERollDetection.Series;
			m_le = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A254LE", EOrderAction.Buy));
			m_se = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A254SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A254LX", EOrderAction.Sell, OrderExit.FromAll));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A254SX", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_ls = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A254LS", EOrderAction.Sell, OrderExit.FromAll));
			m_ss = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A254SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}
		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
		}
		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		private bool entryOpen() { return !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd); }
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_armed = false; m_done = false; m_filledSetup = false; m_restOn = false; }
			if (sandboxBook()) return;   // Coder 2026-11-22: the entry stop is resent every bar of its window (house rule 5); before any position return so Rest() sees the fill
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0) { if (m_entryDay == DateTime.MinValue) m_entryDay = day; sendExits(pos, day); return; }
			m_entryDay = DateTime.MinValue;
			if (m_armed || m_done || rollOn(1) || !IsRth()) return;
			SessionStats s = Session;
			if (!(s.priorRthClose > 0) || !(s.rthOpen > 0)) return;
			double gap = (s.rthOpen - s.priorRthClose) / s.priorRthClose;
			if (Math.Abs(gap) < 0.004) return;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (tod < m_rthStart.Add(TimeSpan.FromMinutes(15)) || tod > m_rthValueEnd.Subtract(TimeSpan.FromMinutes(30))) return;
			double atr = averageTrueRange(Bars, 14);
			if (!(atr > 0)) return;
			bool up = gap > 0;
			m_side = up ? -1 : 1;
			m_stop = up ? s.rthHigh + tickSize() : s.rthLow - tickSize();
			double floor = Math.Max(atr, floorTicks() * tickSize());
			if (up && m_stop - s.priorRthClose < floor) m_stop = s.priorRthClose + floor;
			if (!up && s.priorRthClose - m_stop < floor) m_stop = s.priorRthClose - floor;
			// The protective stop sent with the entry is the exit for that side (the short's buy-to-cover stop on an up gap; it was the
			// long's sell stop before, which protected nothing).
			m_restPx = up ? s.priorRthClose - tickSize() : s.priorRthClose + tickSize(); m_restBuy = !up; m_restOn = true; m_restQty = 1;
			if (up) { (entryOpen() ? m_se : null)?.Send(m_restPx, m_restQty); m_ss.Send(m_stop); } else { (entryOpen() ? m_le : null)?.Send(m_restPx, m_restQty); m_ls.Send(m_stop); }
			m_armed = true;
		}
		private void sendExits(int pos, DateTime day)
		{
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (day != m_entryDay && IsRth() && tod <= m_rthStart.Add(TimeSpan.FromMinutes(1)))
			{
				if (pos > 0) m_lx.Send(); else m_sx.Send();
				return;
			}
			if (pos > 0) m_ls.Send(m_stop); else m_ss.Send(m_stop);
		}
		private int floorTicks()
		{
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) return p.costFloorTicks; } catch (Exception) { }
			return 1;
		}

		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; m_bookEntries = 0; }   // trading day never blocks today (Rest returned Done forever)
			int anchor = 0;
			bool window = !_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd)
				&& IsRth() && sessionClock(Bars.Time[0]) <= m_rthValueEnd.Subtract(TimeSpan.FromMinutes(30));   // the stop is live until RthValueEnd - 30 min
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, anchor, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			if (m_restBuy)
			{
				(entryOpen() ? m_le : null)?.Send(m_restPx, m_restQty); m_ls.Send(m_stop);
			}
			else
			{
				(entryOpen() ? m_se : null)?.Send(m_restPx, m_restQty); m_ss.Send(m_stop);
			}
			return true;
		}

	}
}