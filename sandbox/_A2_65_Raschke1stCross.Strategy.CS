// ## 1. Identity
// Catalogue ID: A2-65
// ID mapping: A2-65 -> _A2_65_Raschke1stCross.Strategy.CS (class _A2_65_Raschke1stCross).
// Name: A2-65 Raschke first oscillator cross
// Header version: 1
// Rules version: catalogue specification under the A2-65 heading, rewritten 2026-11-15.
// Study class: _A2_65_Raschke1stCross
// File: sandbox/_A2_65_Raschke1stCross.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the A2-65 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: the oscillator is SMA(17) minus SMA(50) and its trend is the 5-bar mean of that oscillator. These are the lengths named in the catalogue heading. A long needs the oscillator to cross above its trend and the low to be above the prior low. The short is the mirror and is enabled. The exit is the cross back, which is not a GX target.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-65 heading. Do not paste the entry.
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
// Market tag: both
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
// Entry cutoff: no entry order is sent when its fill bar would end within NoEntryMinutesBeforeFlat (default 25) minutes of the date-aware flat-by cutoff, or after it (catalogue 2.1, early closes included); evening-session entries are allowed.
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
// Exit orders: sendExits. Market exit when the oscillator crosses back through its 5-bar mean. Disaster stop is 2.5 times the 14-bar ATR, widened to the cost floor. Flat at the last chart-session end when the chart has a session.
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
// Engine settings enabled: set in code. RollDetection = Table (the CME roll-date table from the Data1 profile, contract years 2000-2035, RollTableFromProfile; Off on a non-futures symbol). They are not strategy inputs.
//
// ## 16. Pitfalls
// Lookahead: orders from a closed bar.
// Repaint: Not applicable.
// Survivorship: Not applicable
// Rolls: RollDetection Table (CME roll-date table) when the base is the futures profile base.
// Data quality: Not yet verified on a chart.
// Other: ASSUMPTION: ASSUMPTION: the oscillator is SMA(17) minus SMA(50) and its trend is the 5-bar mean of that oscillator. These are the lengths named in the catalogue heading. A long needs the oscillator to cross above its trend and the low to be above the prior low. The short is the mirror and is enabled. The exit is the cross back, which is not a GX target.
//
// ## 17. Parameters
// Strategy inputs besides the inherited TraceLevel are listed below.
// Fixed or research-tuned: the catalogue core, not a sweep. The sweep stays in the catalogue.
//
// Input: NoEntryMinutesBeforeFlat
// Purpose: no new entry in the last N minutes before the date-aware flat-by cutoff (catalogue 2.1: no new entries after 15:30; flat by 15:55).
// Default: 25 minutes (owner 2026-11-22 22:50 CT).
// Units: minutes
// Range: 0 or more. 0 disables the window (the flat-by cutoff itself still blocks). A negative value acts as 0.
// Effect: an entry order is not sent when its fill bar (the next bar) would end at or after the cutoff minus N minutes. An early close moves it (ES 13:10 ET flat: none from 12:45 ET). Evening-session entries are not affected.
// Interactions: the flat-by cutoff (FlatBy, early-close aware).
// Fixed or research-tuned: fixed
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
// logSignal tag: A2-65
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: both
// ASSUMPTION: ASSUMPTION: the oscillator is SMA(17) minus SMA(50) and its trend is the 5-bar mean of that oscillator. These are the lengths named in the catalogue heading. A long needs the oscillator to cross above its trend and the low to be above the prior low. The short is the mirror and is enabled. The exit is the cross back, which is not a GX target.
// IOG is disabled.


using System;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_65_Raschke1stCross : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_ls, m_ss;
		private double m_stop; private bool m_done; private DateTime m_day = DateTime.MinValue;
		[Input] public ESandboxSide Side { get; set; }
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		// Owner 2026-11-22 22:50 CT: minutes before the flat-by cutoff with no new entries (header section 17). Default 25; 0 = off.
		[Input] public int NoEntryMinutesBeforeFlat { get; set; }
		public _A2_65_Raschke1stCross(object _ctx) : base(_ctx) { NoEntryMinutesBeforeFlat = 25; Side = ESandboxSide.Both; }
		protected override void Create()
		{
			base.Create(); RollDetection = ERollDetection.Table; RollTableFromProfile = true;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A265LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A265SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A265LX", EOrderAction.Sell, OrderExit.FromAll));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A265SX", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_ls = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A265LS", EOrderAction.Sell, OrderExit.FromAll));
			m_ss = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A265SS", EOrderAction.BuyToCover, OrderExit.FromAll));
		}
		/// <summary>Analyst MC17 run: Data1 lookback past MaxBarsBack 49 comes from the engine ring (see _SandboxRules.RingAgo).</summary>
		private RingBar lbBar(int ago) { return _SandboxRules.RingAgo(Ring(1), ago); }

		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);
			DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); requireRing(1, 2000, "A2-65 lookback");   /* Analyst MC17: deep Data1 lookback without raising MaxBarsBack 49 */
		}
		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		// Coder 2026-11-22 (owner 22:50 CT): catalogue 2.1 "no new entries after 15:30" as the NoEntryMinutesBeforeFlat input (default 25; 0 = off).
		private bool entryOpen() { return !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat); }   // #146 date-aware cutoff
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close || Bars.CurrentBar < 60) return;
			if (sandboxBook()) return;
			DateTime day = Bars.Time[0].Date;
			if (day != m_day) { m_day = day; m_done = false; m_filledSetup = false; m_restOn = false; }
			int pos = StrategyInfo.MarketPosition;
			double o0 = osc(0), o1 = osc(1), t0 = trend(0), t1 = trend(1);
			bool crossUp = o1 <= t1 && o0 > t0;
			bool crossDn = o1 >= t1 && o0 < t0;
			if (pos > 0 && (crossDn || sessionFlat())) { m_lx.Send(); return; }
			if (pos < 0 && (crossUp || sessionFlat())) { m_sx.Send(); return; }
			if (pos > 0) { m_ls.Send(m_stop); return; }
			if (pos < 0) { m_ss.Send(m_stop); return; }
			if (m_done) return;
			bool buy = crossUp && Bars.Low[0] > Bars.Low[1];
			bool sell = crossDn && Bars.High[0] < Bars.High[1];
			if (!buy && !sell) return;
			double atr = averageTrueRange(Bars, 14);
			if (!(atr > 0)) return;
			double entry = Bars.Close[0];
			m_stop = widen(buy, entry, buy ? entry - 2.5 * atr : entry + 2.5 * atr);
			if (Math.Abs(entry - m_stop) * Bars.Info.BigPointValue > equityAvailable(0.01)) return;
			if (buy) { (entryOpen() ? m_le : null)?.Send(); m_ls.Send(m_stop); } else { (entryOpen() ? m_se : null)?.Send(); m_ss.Send(m_stop); }
		}
		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; m_bookEntries = 0; }   // trading day never blocks today (Rest returned Done forever)
			bool window = !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, 0, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			return false;
		}
		private double sma(int n, int ago) { double s = 0; for (int i = 0; i < n; i++) s += lbBar(ago + i).c; return s / n; }
		private double osc(int ago) { return sma(17, ago) - sma(50, ago); }
		private double trend(int ago) { double s = 0; for (int i = 0; i < 5; i++) s += osc(ago + i); return s / 5.0; }
		private bool sessionFlat()
		{
			if (Bars.Sessions == null || Bars.Sessions.Count == 0) return false;
			TimeSpan t = Bars.Time[0].TimeOfDay;
			TimeSpan end = Bars.Sessions[Bars.Sessions.Count - 1].EndTime;
			return t >= end.Subtract(TimeSpan.FromMinutes(5)) && t < end.Add(TimeSpan.FromMinutes(1));
		}
		private double widen(bool longSide, double entry, double stop)
		{
			int ticks = 1;
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) ticks = p.costFloorTicks; } catch (Exception) { }
			double floor = ticks * tickSize();
			if (longSide && entry - stop < floor) return entry - floor;
			if (!longSide && stop - entry < floor) return entry + floor;
			return stop;
		}
	}
}
