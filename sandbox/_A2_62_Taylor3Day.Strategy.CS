// ## 1. Identity
// Catalogue ID: A2-62
// ID mapping: A2-62 -> _A2_62_Taylor3Day.Strategy.CS (class _A2_62_Taylor3Day).
// Name: A2-62 Taylor three-day buying day
// Header version: 1
// Rules version: catalogue specification under the A2-62 heading, rewritten 2026-11-15.
// Study class: _A2_62_Taylor3Day
// File: sandbox/_A2_62_Taylor3Day.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, the A2-62 heading and its Analyst 2 specification.
// Taken from the source: the entry, the filters, the stop, the target and the time exit named there.
// Research assumption: ASSUMPTION: ASSUMPTION: a buying day is the session after the lowest RTH low of the prior 10 completed sessions, and then every third session. An uptrend is the 20-session mean of RTH closes above that mean from five sessions earlier. If the bar trades the prior low and the prior high on the same bar, the limit is not sent. The exit limit is the completed buying-day high, which is not known at the entry, so it is not an exitPlan target.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-62 heading. Do not paste the entry.
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
// Exit orders: sendExits. Stop is the prior RTH low minus 0.5 times DailyContext.Atr20d, widened to the cost floor. On the next session, a limit at the buying day's high, and a market exit at the session value end if that limit has not filled.
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
// Other: ASSUMPTION: ASSUMPTION: a buying day is the session after the lowest RTH low of the prior 10 completed sessions, and then every third session. An uptrend is the 20-session mean of RTH closes above that mean from five sessions earlier. If the bar trades the prior low and the prior high on the same bar, the limit is not sent. The exit limit is the completed buying-day high, which is not known at the entry, so it is not an exitPlan target.
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
// logSignal tag: A2-62
// Skip / flatten lines: the trace strings in this file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// Market tag: futures-only
// ASSUMPTION: ASSUMPTION: a buying day is the session after the lowest RTH low of the prior 10 completed sessions, and then every third session. An uptrend is the 20-session mean of RTH closes above that mean from five sessions earlier. If the bar trades the prior low and the prior high on the same bar, the limit is not sent. The exit limit is the completed buying-day high, which is not known at the entry, so it is not an exitPlan target.
// IOG is disabled.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;
namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_62_Taylor3Day : _VolumeProfileFuturesBase
	{
		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_ll, m_ls, m_lt;
		private readonly List<double> m_hi = new List<double>(), m_lo = new List<double>(), m_cl = new List<double>();
		private DateTime m_day = DateTime.MinValue, m_entryDay = DateTime.MinValue;
		private double m_runHi, m_runLo, m_runCl, m_stop, m_buyHigh;
		private int m_since = -1;
		private bool m_seen, m_done, m_buying;
		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_bookEntries; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_62_Taylor3Day(object _ctx) : base(_ctx) { }
		protected override void Create()
		{
			base.Create(); RollDetection = ERollDetection.Series;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A262LE", EOrderAction.Buy));
			m_ll = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A262LL", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A262LX", EOrderAction.Sell, OrderExit.FromAll));
			m_ls = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A262LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A262LT", EOrderAction.Sell, OrderExit.FromAll));
		}
		protected override void StartCalc() {
			_SandboxRules.ApplyPit(Bars.Info.Name, ref m_rthStart, ref m_rthEnd, ref m_rthValueEnd, Bars.Sessions != null && Bars.Sessions.Count > 0 ? _SandboxRules.DaytimeEnd(Bars.Sessions[Bars.Sessions.Count - 1].EndTime) : TimeSpan.Zero);

		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;
 NeedsDaily = true; DailyConfig.atrRestartOnRoll = false;   /* owner 2026-11-16: carry the 20-day ATR over the roll (PR #46) */ DailyConfig.atrLength = 20; m_hi.Clear(); m_lo.Clear(); m_cl.Clear(); m_seen = false; m_day = DateTime.MinValue; /* Coder 2026-11-22: a recalc reuses this object, so the day history is cleared */ DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc(); }
		// Coder 2026-11-22 (Analyst native retest of #152: entries filled after the flat-by cutoff): no entry order is sent on or after
		// the cutoff bar (session end minus 5 minutes, early-close aware) until 18:00 ET; an order sent on that bar would fill after the cutoff.
		private bool entryOpen() { return !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd)); }   // #146 date-aware cutoff
		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day)
			{
				if (m_seen) { m_hi.Add(m_runHi); m_lo.Add(m_runLo); m_cl.Add(m_runCl); }
				m_day = day; m_seen = false; m_done = false; m_filledSetup = false; m_restOn = false; m_runHi = double.MinValue; m_runLo = double.MaxValue; m_buying = classify();
			}
			int pos = StrategyInfo.MarketPosition;
			if (IsRth())
			{
				m_seen = true;
				m_runHi = Math.Max(m_runHi, Bars.High[0]); m_runLo = Math.Min(m_runLo, Bars.Low[0]); m_runCl = Bars.Close[0];
			}
			else if (pos == 0) return;
			if (sandboxBook()) return;   // before any position return, so Rest() sees the fill: one attempt per anchor per day
			if (pos > 0)
			{
				if (m_entryDay == DateTime.MinValue) m_entryDay = day;
				if (day == m_entryDay) m_buyHigh = m_runHi;
				m_ls.Send(m_stop);
				if (day != m_entryDay)
				{
					m_lt.Send(m_buyHigh);
					TimeSpan tod = sessionClock(Bars.Time[0]);
					if (_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd)) m_lx.Send();
				}
				return;
			}
			m_entryDay = DateTime.MinValue;
			if (m_done || !m_buying || rollOn(1) || m_lo.Count < 1) return;
			double priorLo = m_lo[m_lo.Count - 1], priorHi = m_hi[m_hi.Count - 1], priorCl = m_cl[m_cl.Count - 1];
			double atr = DailyContext.Atr20d;
			if (!(atr > 0)) return;
			TimeSpan clock = sessionClock(Bars.Time[0]);
			bool both = Bars.Low[0] <= priorLo && Bars.High[0] >= priorHi;
			if (!both && Bars.Low[0] <= priorLo && Session.rthHigh < priorHi)
			{
				m_stop = widen(priorLo, priorLo - 0.5 * atr);
				if (!budget(priorLo, m_stop)) { m_done = true; return; }
				m_restPx = priorLo; m_restBuy = true; m_restOn = true; m_restQty = 1; m_buyHigh = 0; (entryOpen() ? m_ll : null)?.Send(priorLo); return;
			}
			if (Session.rthOpen < priorCl && clock >= m_rthStart.Add(TimeSpan.FromMinutes(5)) && clock < m_rthStart.Add(TimeSpan.FromMinutes(6)))
			{
				double entry = Bars.Close[0];
				m_stop = widen(entry, priorLo - 0.5 * atr);
				if (!budget(entry, m_stop)) { m_done = true; return; }
				(entryOpen() ? m_le : null)?.Send(); m_ls.Send(m_stop);
			}
		}
		private bool classify()
		{
			int n = m_lo.Count;
			if (n < 20) return false;
			double a = 0, b = 0;
			for (int i = 0; i < 20; i++) { a += m_cl[n - 1 - i]; b += m_cl[n - 6 - i]; }
			if (!(a / 20.0 > b / 20.0)) { m_since = -1; return false; }
			int arg = n - 1;
			for (int i = n - 10; i < n; i++) if (m_lo[i] <= m_lo[arg]) arg = i;
			bool buy = arg == n - 1 || m_since == 3;
			m_since = buy ? 0 : (m_since < 0 ? -1 : m_since + 1);
			return buy;
		}
		private double widen(double entry, double stop)
		{
			int ticks = 1;
			try { InstrumentProfile p = InstrumentProfileOf(1); if (p != null && p.costFloorTicks > 0) ticks = p.costFloorTicks; } catch (Exception) { }
			double floor = ticks * tickSize();
			return entry - stop < floor ? entry - floor : stop;
		}
		private bool budget(double entry, double stop)
		{
			return Math.Abs(entry - stop) * Bars.Info.BigPointValue <= equityAvailable(0.01);
		}

		private DateTime m_bookDay = DateTime.MinValue;
		private bool sandboxBook()
		{
			DateTime bookDay = tradingDayKey(Bars.Time[0]);   // Analyst MC17 (A2-32/A2-35): one trade per anchor per day, so a fill on an earlier
			if (bookDay != m_bookDay) { m_bookDay = bookDay; m_filledSetup = false; m_bookEntries = 0; }   // trading day never blocks today (Rest returned Done forever)
			int anchor = 0;
			bool window = !gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd));
			ERest step = _SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday, ref m_bookEntries, ref m_prevPos, ref m_filledSetup, ref m_fillAnchor, anchor, ref m_restOn, window);
			if (step == ERest.Done) { m_done = true; return true; }
			if (step != ERest.Resend) return false;
			// Coder 2026-11-22: the buy limit at the prior low is resent every bar for the day (house rule 5); the branch was empty,
			// so the limit was cancelled after its first bar. Long only: there is no short resting order.
			if (m_restBuy)
			{
				(entryOpen() ? m_ll : null)?.Send(m_restPx, m_restQty);
			}
			return true;
		}

	}
}