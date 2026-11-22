// ## 1. Identity
// Catalogue ID: A2-14
// ID mapping: A2-14 -> _A2_14_WeeklyAvwapReclaim.Strategy.CS (class _A2_14_WeeklyAvwapReclaim).
// Name: A2-14 weekly AVWAP reclaim
// Header version: 1
// Rules version: catalogue text, coded 2026-11-15. There is no engines-manual example for this id.
// Study class: _A2_14_WeeklyAvwapReclaim
// File: sandbox/_A2_14_WeeklyAvwapReclaim.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md heading for A2-14.
// Taken from the source: the Rules line of that heading.
// Research assumption: ASSUMPTION: the rules are daily; Data1 is 1-minute, so each Globex trading date is aggregated into one daily bar (low = the day low, close = the last 1m close) and the daily rules run once, on the first bar of the next trading date. Held days count trading days. The week-open AVWAP is CurrentVwap1 with Vwap1Type Week. ATRd is DailyContext.Atr20d. The 20-day SMA and its slope are computed here from stored daily closes. A close back above after N closes below buys the next bar, which is the first minutes of the next session. Days-below sweep is the enum, default 2.
//
// ## 3. Hypothesis
// Mechanism: the catalogue entry. This file does not add a claim beyond that entry.
// Link: specs/trading-idea-catalogue.md, the A2-14 heading. Do not paste the entry.
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
// Market tag: both
// Typical: the catalogue market. Other roots are Not yet verified.
// Validated: Not yet verified
//
// ## 8. Data
// Chart: Data1 = 1-minute (owner rule). The daily swing builds its daily bars from Data1 by Globex trading date.
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
// Other: ASSUMPTION: the rules are daily; Data1 is 1-minute, so each Globex trading date is aggregated into one daily bar (low = the day low, close = the last 1m close) and the daily rules run once, on the first bar of the next trading date. Held days count trading days. The week-open AVWAP is CurrentVwap1 with Vwap1Type Week. ATRd is DailyContext.Atr20d. The 20-day SMA and its slope are computed here from stored daily closes. A close back above after N closes below buys the next bar, which is the first minutes of the next session. Days-below sweep is the enum, default 2.
//
// ## 17. Parameters
// Each [Input] below is one of this strategy's own parameters. TraceLevel stays on the base.
// Input: see the [Input] properties on the class
// Purpose: the catalogue sweep named in section 22
// Default: the value set in the constructor, which is the catalogue's first listed value
// Units: as the property name states
// Range: the enum members, checked with Enum.IsDefined in StartCalc
// Effect: selects the arm. It does not resize a position.
// Interactions: the other settings stay in the protected config
// Fixed or research-tuned: research-tuned where the catalogue names a sweep
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
// logSignal tag: A2-14
// Skip / flatten lines: the trace strings in the file.
// How to turn logging on: engines manual section 1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: rename the public class, its constructor, and any ArgumentException text that names the class, to _TestNN. Order names stay.
// ASSUMPTION: the rules are daily; Data1 is 1-minute, so each Globex trading date is aggregated into one daily bar (low = the day low, close = the last 1m close) and the daily rules run once, on the first bar of the next trading date. Held days count trading days. The week-open AVWAP is CurrentVwap1 with Vwap1Type Week. ATRd is DailyContext.Atr20d. The 20-day SMA and its slope are computed here from stored daily closes. A close back above after N closes below buys the next bar, which is the first minutes of the next session. Days-below sweep is the enum, default 2.
// IOG is disabled. GX exitPlan is not called. Exit orders are sent only from sendExits.


using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _A2_14_WeeklyAvwapReclaim : _VolumeProfileFuturesBase
	{
		public enum EDaysBelow { Two, Three }
		[Input] public EDaysBelow DaysBelow { get; set; }

		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_lxStp, m_lxLmt;
		private readonly List<double> m_closes = new List<double>();
		private int m_below, m_held;
		private double m_lowBelow, m_stop, m_target, m_entry;
		private bool m_ready, m_hasDay, m_dRoll, m_exitSent;
		private DateTime m_day = DateTime.MinValue;
		private double m_dLow, m_dClose, m_dVwap = double.NaN;

		private double m_restPx; private bool m_restBuy, m_restOn, m_filledSetup; private int m_prevPos, m_fillAnchor, m_restQty = 1;
		public _A2_14_WeeklyAvwapReclaim(object _ctx) : base(_ctx) { DaysBelow = EDaysBelow.Two; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A214LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A214LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A214LS", EOrderAction.Sell, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A214LT", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
		if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;

			if (!Enum.IsDefined(typeof(EDaysBelow), DaysBelow)) throw new ArgumentException("_A2_14_WeeklyAvwapReclaim: DaysBelow is not a defined EDaysBelow");
			NeedsDaily = true; DailyConfig.atrRestartOnRoll = false;   /* owner 2026-11-16: carry the 20-day ATR over the roll (PR #46) */
			Vwap1Type = EVwapType.Week; Vwap1DataStream = 1;
			/* Coder 2026-11-22: a recalc reuses this object, so the history from the previous pass is cleared (no lookahead from a reused list) */ m_closes.Clear(); m_below = 0; m_held = 0; m_ready = m_hasDay = m_dRoll = m_exitSent = false; m_day = DateTime.MinValue; DvpConfig.journalFits = DvpConfig.journalRails = traceOn(ETraceLevel.Verbose);   /* Coder 2026-11-22 (Analyst #152 retest: MT-07 wrote 1.42M DVP FIT/RAIL lines): the per-bar DVP journals are Verbose, not Journal */ base.StartCalc();
		}

		protected override void CalcBar()
		{   // Coder 2026-11-22: Data1 is 1-minute (owner rule), so the daily swing builds its own daily bar per Globex trading date
			// (low = the day's low, close = the last 1m close of the trading day) and runs the daily rules once, on the first bar of the
			// next trading date. Held days, the 20-day SMA and the closes-below count are counted in days, not 1-minute bars.
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			int pos = StrategyInfo.MarketPosition;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day)
			{
				if (m_hasDay) dailyClose(pos);
				m_day = day; m_hasDay = true; m_dLow = Bars.Low[0]; m_dRoll = false;
			}
			else m_dLow = Math.Min(m_dLow, Bars.Low[0]);
			m_dClose = Bars.Close[0]; m_dVwap = CurrentVwap1; if (rollOn(1)) m_dRoll = true;
			if (StrategyInfo.MarketPosition > 0 && !m_exitSent) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); }   // resting stop and target work every bar
		}

		private void dailyClose(int pos)
		{
			m_exitSent = false;
			// Analyst MC17 (2024-01-15 MLK): a CME holiday session halts at 12:00 CT and its trades carry the next day's trade date, so
			// its daily bar is not a settled daily close. It is not counted as a close, a held day or an entry signal; the resting
			// stop and target stay working.
			if (IsNoRthDay(m_day))
			{
				if (journalOn()) trace(ETraceLevel.Journal, $"A2-14 {m_day:yyyy-MM-dd} holiday session ({_CmeHolidays.Rules.Get(m_day).Name}), not a daily close");
				return;
			}
			double vwap = m_dVwap, close = m_dClose;
			double atr = DailyContext != null ? DailyContext.Atr20d : double.NaN;
			if (pos > 0)
			{
				m_held++;   // one per completed trading day
				bool back = !double.IsNaN(vwap) && close < vwap;
				if (m_held >= 7 || back) { m_lx.Send(); m_exitSent = true; if (journalOn()) trace(ETraceLevel.Journal, $"A2-14 {m_day:yyyy-MM-dd} exit: " + (back ? "daily close below the weekly AVWAP" : "7 trading days held")); return; }
			}
			m_closes.Insert(0, close); if (m_closes.Count > 21) m_closes.RemoveAt(21);
			if (pos != 0 || m_dRoll || double.IsNaN(vwap) || !(atr > 0)) return;
			bool below = close < vwap;
			if (below) { m_below++; m_lowBelow = m_below == 1 ? m_dLow : Math.Min(m_lowBelow, m_dLow); m_ready = m_below >= (DaysBelow == EDaysBelow.Three ? 3 : 2); return; }
			if (!m_ready) { m_below = 0; return; }
			if (!slopeUp()) { m_below = 0; m_ready = false; return; }
			double tick = tickSize();
			double stop = m_lowBelow - 0.1 * atr;
			double floor = 14 * tick;
			if (close - stop < floor) stop = close - floor;
			double risk = close - stop;
			if (!(risk > 0) || risk * Bars.Info.BigPointValue > equityAvailable(0.01))
			{ if (journalOn()) trace(ETraceLevel.Journal, "A2-14 skip: risk budget"); m_below = 0; m_ready = false; return; }
			m_stop = stop; m_target = close + 2 * risk; m_held = 0; m_below = 0; m_ready = false;
			m_le.Send(); m_lxStp.Send(m_stop);
			if (journalOn()) journalArm("A214LE", close, m_stop, m_target, null);
		}

		private bool slopeUp()
		{
			if (m_closes.Count < 21) return false;
			double a = 0, b = 0;
			for (int i = 0; i < 20; i++) { a += m_closes[i]; b += m_closes[i + 1]; }
			return a / 20.0 > b / 20.0;
		}
	}
}
