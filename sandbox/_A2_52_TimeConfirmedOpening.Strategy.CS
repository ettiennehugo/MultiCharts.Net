// ## 1. Identity
// Catalogue ID: A2-52
// ID mapping: A2-52 -> _A2_52_TimeConfirmedOpening.Strategy.CS (class _A2_52_TimeConfirmedOpening).
// Name: A2-52 time-confirmed opening range
// Header version: 1
// Rules version: catalogue specification under the A2-52 heading, rewritten 2026-11-15.
// Study class: _A2_52_TimeConfirmedOpening
// File: sandbox/_A2_52_TimeConfirmedOpening.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the A2-52 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: The opening range is the first 15 minutes after RthStart. A_up is ORH plus 0.5 times the range and A_dn is ORL minus 0.5 times the range. ASSUMPTION: eight consecutive 1-minute closes is the half-of-15 hold. The entry window ends at RthStart plus 2 hours. The pivot band is DPP plus or minus the absolute pivot differential of the prior RTH. A long requires the prior close at or above the band low and the current RTH low still above that band. The failure exit (flat when open profit is not positive 15 minutes after entry) is not a GX time stop, because a GX time stop exits a winner too.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-52 heading. Do not paste the entry.
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
// Exit orders: sendExits. Protective stop at the far side of the opening range, target one times the opening range, flat at the session value end, and a market exit when 15 closed bars have passed and the open profit is not positive.
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
// Other: ASSUMPTION: The opening range is the first 15 minutes after RthStart. A_up is ORH plus 0.5 times the range and A_dn is ORL minus 0.5 times the range. ASSUMPTION: eight consecutive 1-minute closes is the half-of-15 hold. The entry window ends at RthStart plus 2 hours. The pivot band is DPP plus or minus the absolute pivot differential of the prior RTH. A long requires the prior close at or above the band low and the current RTH low still above that band. The failure exit (flat when open profit is not positive 15 minutes after entry) is not a GX time stop, because a GX time stop exits a winner too.
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
// logSignal tag: A2-52
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: The opening range is the first 15 minutes after RthStart. A_up is ORH plus 0.5 times the range and A_dn is ORL minus 0.5 times the range. ASSUMPTION: eight consecutive 1-minute closes is the half-of-15 hold. The entry window ends at RthStart plus 2 hours. The pivot band is DPP plus or minus the absolute pivot differential of the prior RTH. A long requires the prior close at or above the band low and the current RTH low still above that band. The failure exit (flat when open profit is not positive 15 minutes after entry) is not a GX time stop, because a GX time stop exits a winner too.
// IOG is disabled.


using System;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_52_TimeConfirmedOpening : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_ls, m_ss, m_lt, m_st;
		private DateTime m_day = DateTime.MinValue;
		private double m_orh = double.NaN, m_orl = double.NaN, m_stop, m_target;
		private int m_hold, m_entryBar, m_side;
		private bool m_done;
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_52_TimeConfirmedOpening(object _ctx) : base(_ctx) { }
		protected override void Create()
		{
			base.Create(); RollDetection = ERollDetection.Series;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A252LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A252SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A252LX", EOrderAction.Sell, OrderExit.FromAll));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A252SX", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_ls = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A252LS", EOrderAction.Sell, OrderExit.FromAll));
			m_ss = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A252SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A252LT", EOrderAction.Sell, OrderExit.FromAll));
			m_st = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A252ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}
		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			base.StartCalc();
		}
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; m_hold = 0; m_orh = m_orl = double.NaN; }
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0) { sendExits(pos); return; }
			if (sandboxBook()) return;
			if (m_done || rollOn(1) || !IsRth()) return;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			TimeSpan orEnd = m_rthStart.Add(TimeSpan.FromMinutes(15));
			if (tod > m_rthStart && tod <= orEnd)
			{
				if (double.IsNaN(m_orh)) { m_orh = Bars.High[0]; m_orl = Bars.Low[0]; }
				else { m_orh = Math.Max(m_orh, Bars.High[0]); m_orl = Math.Min(m_orl, Bars.Low[0]); }
				return;
			}
			if (tod < orEnd || tod > m_rthStart.Add(TimeSpan.FromHours(2))) return;
			double w = m_orh - m_orl;
			if (!(w > 0)) return;
			SessionStats s = Session;
			double dpp = (s.priorRthHigh + s.priorRthLow + s.priorRthClose) / 3.0;
			double half = Math.Abs(dpp - (s.priorRthHigh + s.priorRthLow) / 2.0);
			bool longOk = s.priorRthClose >= dpp - half && !(s.rthLow > 0 && s.rthLow < dpp - half);
			bool shortOk = s.priorRthClose <= dpp + half && !(s.rthHigh > dpp + half);
			bool up = Bars.Close[0] >= m_orh + 0.5 * w;
			bool dn = Bars.Close[0] <= m_orl - 0.5 * w;
			if (up && longOk) m_hold = m_hold > 0 ? m_hold + 1 : 1;
			else if (dn && shortOk) m_hold = m_hold < 0 ? m_hold - 1 : -1;
			else m_hold = 0;
			if (Math.Abs(m_hold) < 8) return;
			bool buy = m_hold > 0;
			double entry = Bars.Close[0];
			m_stop = widen(buy, entry, buy ? m_orl - tickSize() : m_orh + tickSize());
			m_target = buy ? entry + w : entry - w;
			if (!budget(entry, m_stop)) { m_done = true; return; }
			m_side = buy ? 1 : -1; m_entryBar = Bars.CurrentBar;
			if (buy) { m_le.Send(); m_ls.Send(m_stop); } else { m_se.Send(); m_ss.Send(m_stop); }
		}
		private void sendExits(int pos)
		{
			TimeSpan tod = sessionClock(Bars.Time[0]);
			bool flat = _SandboxRules.PastFlat(tod, Bars.Info.Name, m_rthValueEnd);
			double openPts = (Bars.Close[0] - StrategyInfo.AvgEntryPrice) * pos;
			bool failed = Bars.CurrentBar - m_entryBar >= 15 && openPts <= 0;
			if (flat || failed) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }
			if (pos > 0) { m_ls.Send(m_stop); m_lt.Send(m_target); }
			else { m_ss.Send(m_stop); m_st.Send(m_target); }
		}
		private int floorTicks()
		{
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) return p.costFloorTicks; }
			catch (Exception) { }
			return 1;
		}
		private double widen(bool longSide, double entry, double stop)
		{
			double floor = floorTicks() * tickSize();
			if (longSide && entry - stop < floor) return entry - floor;
			if (!longSide && stop - entry < floor) return entry + floor;
			return stop;
		}
		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; }   // trading day never blocks today (Rest returned Done forever)
			bool window = !_SandboxRules.PastFlat(sessionClock(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd);
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, 0, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			return false;
		}
		private bool budget(double entry, double stop)
		{
			double risk = Math.Abs(entry - stop);
			return risk > 0 && risk * Bars.Info.BigPointValue <= equityAvailable(0.01);
		}
	}
}
