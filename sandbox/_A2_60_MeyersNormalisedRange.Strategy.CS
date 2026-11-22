// ## 1. Identity
// Catalogue ID: A2-60
// ID mapping: A2-60 -> _A2_60_MeyersNormalisedRange.Strategy.CS (class _A2_60_MeyersNormalisedRange).
// Name: A2-60 Meyers normalised historical range
// Header version: 1
// Rules version: catalogue specification under the A2-60 heading, rewritten 2026-11-15.
// Study class: _A2_60_MeyersNormalisedRange
// File: sandbox/_A2_60_MeyersNormalisedRange.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the A2-60 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: NHR uses n = 6 and a = 0.75. The buy is the highest NHR of the last 6 closed bars above 0.50 while the highest NLR of those bars stays below 1.05. ASSUMPTION: the short is the sign flip of that pair. The 1.5 ATR stop is the catalogue's added disaster stop, not a tested target. An opposite signal flats the position and does not reverse on the same bar, because GX does not reverse; the next closed bar may enter. Indicators reset at RthStart.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-60 heading. Do not paste the entry.
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
// Exit orders: sendExits. Disaster stop at 1.5 ATR, flat five minutes before the session value end, and a market exit when the opposite NHR signal prints. No fixed target.
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
// Other: ASSUMPTION: NHR uses n = 6 and a = 0.75. The buy is the highest NHR of the last 6 closed bars above 0.50 while the highest NLR of those bars stays below 1.05. ASSUMPTION: the short is the sign flip of that pair. The 1.5 ATR stop is the catalogue's added disaster stop, not a tested target. An opposite signal flats the position and does not reverse on the same bar, because GX does not reverse; the next closed bar may enter. Indicators reset at RthStart.
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
// logSignal tag: A2-60
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: NHR uses n = 6 and a = 0.75. The buy is the highest NHR of the last 6 closed bars above 0.50 while the highest NLR of those bars stays below 1.05. ASSUMPTION: the short is the sign flip of that pair. The 1.5 ATR stop is the catalogue's added disaster stop, not a tested target. An opposite signal flats the position and does not reverse on the same bar, because GX does not reverse; the next closed bar may enter. Indicators reset at RthStart.
// IOG is disabled.


using System;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_60_MeyersNormalisedRange : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_ls, m_ss;
		private DateTime m_day = DateTime.MinValue;
		private readonly double[] m_nhr = new double[6];
		private readonly double[] m_nlr = new double[6];
		private int m_fill, m_side;
		private double m_stop;
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_60_MeyersNormalisedRange(object _ctx) : base(_ctx) { }
		protected override void Create()
		{
			base.Create(); RollDetection = ERollDetection.Series;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A260LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A260SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A260LX", EOrderAction.Sell, OrderExit.FromAll));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A260SX", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_ls = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A260LS", EOrderAction.Sell, OrderExit.FromAll));
			m_ss = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A260SS", EOrderAction.BuyToCover, OrderExit.FromAll));
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
			if (day != m_day) { m_day = day; m_fill = 0; }
			int pos = StrategyInfo.MarketPosition;
			TimeSpan tod = sessionClock(Bars.Time[0]);
			if (pos != 0 && tod >= m_rthValueEnd.Subtract(TimeSpan.FromMinutes(5)) && tod < new TimeSpan(17, 0, 0))
			{
				if (pos > 0) m_lx.Send(); else m_sx.Send();
				return;
			}
			if (!IsRth() || rollOn(1) || Bars.CurrentBar < 8) { if (pos != 0) holdStop(pos); return; }
			double atr = averageTrueRange(Bars, 6);
			if (!(atr > 0)) return;
			double scale = Math.Pow(6, 0.75) * atr;
			int slot = m_fill % 6;
			m_nhr[slot] = (Bars.High[0] - Bars.Low[6]) / scale;
			m_nlr[slot] = (Bars.Low[0] - Bars.High[6]) / scale;
			m_fill++;
			if (m_fill < 6) { if (pos != 0) holdStop(pos); return; }
			double hrMax = m_nhr[0], lrMax = m_nlr[0];
			for (int i = 1; i < 6; i++) { hrMax = Math.Max(hrMax, m_nhr[i]); lrMax = Math.Max(lrMax, m_nlr[i]); }
			bool buy = hrMax > 0.50 && lrMax < 1.05;
			double lrMin = m_nlr[0], hrMin = m_nhr[0];
			for (int i = 1; i < 6; i++) { lrMin = Math.Min(lrMin, m_nlr[i]); hrMin = Math.Min(hrMin, m_nhr[i]); }
			bool sell = -lrMin > 0.50 && -hrMin < 1.05;
			if (pos > 0 && sell) { m_lx.Send(); return; }
			if (pos < 0 && buy) { m_sx.Send(); return; }
			if (pos != 0) { holdStop(pos); return; }
			if (!buy && !sell) return;
			double entry = Bars.Close[0];
			m_stop = buy ? entry - 1.5 * atr : entry + 1.5 * atr;
			m_stop = widen(buy, entry, m_stop);
			if (!budget(entry, m_stop)) return;
			m_side = buy ? 1 : -1;
			if (buy) { (entryOpen() ? m_le : null)?.Send(); m_ls.Send(m_stop); } else { (entryOpen() ? m_se : null)?.Send(); m_ss.Send(m_stop); }
		}
		private void holdStop(int pos) { if (pos > 0) m_ls.Send(m_stop); else m_ss.Send(m_stop); }
		private int floorTicks()
		{
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) return p.costFloorTicks; } catch (Exception) { }
			return 1;
		}
		private double widen(bool longSide, double entry, double stop)
		{
			double floor = floorTicks() * tickSize();
			if (longSide && entry - stop < floor) return entry - floor;
			if (!longSide && stop - entry < floor) return entry + floor;
			return stop;
		}
		private bool budget(double entry, double stop)
		{
			return Math.Abs(entry - stop) * Bars.Info.BigPointValue <= equityAvailable(0.01);
		}
	}
}
