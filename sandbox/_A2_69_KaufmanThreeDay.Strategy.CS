// ## 1. Identity
// Catalogue ID: A2-69
// ID mapping: A2-69 → _A2_69_KaufmanThreeDay.Strategy.CS (class _A2_69_KaufmanThreeDay). KaufmanThreeDay shortens the catalogue title "Kaufman 3-day trade".
// Name: Kaufman three-day
// Header version: 1
// Rules version: catalogue v2.7 core arm, as implemented by docs/engines-manual.md §3.1 (example 1)
// Study class: _A2_69_KaufmanThreeDay
// File: sandbox/_A2_69_KaufmanThreeDay.Strategy.CS
// Stage: sandbox
// Owner approval: Not yet verified
//
// ## 2. Provenance
// Source: specs/trading-idea-catalogue.md, heading "A2-69 · Kaufman 3-day trade". The code is engines-manual example 1.
// Taken from the source: two lower RTH closes; long only; if the first RTH bar opens below the prior RTH close, buy at that bar's close for the next open; otherwise buy at the 15:59 bar. Exit at 15:59 on a later session. No stop in the core.
// Research assumption: RollDetection is Series and a roll clears the close history, so two contracts are never compared. The catalogue does not say this. A 1-minute "09:31" entry fills on the next bar's open.
//
// ## 3. Hypothesis
// Mechanism: two lower closes and a lower open are Kaufman's short-term pullback on an equity index. The book reports longs and weak shorts. This file trades the long core only.
// Link: specs/trading-idea-catalogue.md, heading "A2-69 · Kaufman 3-day trade: two down closes, buy a lower open, exit next close"
//
// ## 4. Trade walkthrough
// Trigger: on a Globex day that is not NoRth, if the last three completed RTH closes satisfy C1 < C2 < C3, buy at the first RTH bar when its open is below C1. Otherwise buy at the bar close-stamped 15:59 (one minute before an early close).
// Management: the position is held. Nothing is moved. There is no stop.
// Exit: a market flatten at 15:59 on a session after the entry day.
//
// ## 5. Timing
// Signal evaluated: closed bar only, after base.CalcBar.
// Order sent: MarketNextBar K3LE at the close of the 09:31 bar or the 15:59 bar. Exit is MarketNextBar K3LX.
// Can fill: the next bar after that close.
//
// ## 6. Position and re-entry
// Position: flat or one long. No scale-in, no short, no reverse.
// Re-entry: the setup flag is cleared when the order is sent. A new setup needs three closes again.
// Same session after a stop: Not applicable. This arm has no stop.
//
// ## 7. Instruments
// Typical: ES, NQ and RTY, daily signal with 1-minute fills. This file is written for @ES 1-minute.
// Validated: Not yet verified
//
// ## 8. Data
// Data1: @ES 1-minute, CME Globex, dxFeed continuous unadjusted. Eastern clock through the CME session adapter.
// Data2: Not applicable
// Further series: Not applicable
// Warmup: three completed RTH closes. NoRth days do not set up.
// Tests use data through 2024-12-31 only.
//
// ## 9. Entry and filters
// All of these must be true: flat; C1 < C2 < C3 on completed RTH closes; the day is not NoRth; either the first RTH bar opens below C1, or that test failed and the bar is the 15:59 close bar.
// Skip when: NoRth; fewer than three closes; a roll has cleared the history.
//
// ## 10. Position sizing and risk
// Size: 1 contract (A269Config.contracts).
// Risk limit: Not applicable. This arm has no stop and no risk-budget skip.
// Size is never shrunk to fit a stop: yes. Size is fixed at 1 contract.
//
// ## 11. Orders, execution, and costs
// Entry order: MarketNextBar K3LE, buy.
// Exit orders: MarketNextBar K3LX, sell.
// Fill assumption: next bar's open. No stop and no limit.
// Costs: the example assumes the ES research round trip of $17.00 ($2.25 commission and $6.25 slippage per side) set in MultiCharts, not in this file. Not yet verified
//
// ## 12. Trade management
// While open: Not applicable. No stop, target or trail is adjusted.
//
// ## 13. Exits
// Hard stop: Not applicable
// Target: Not applicable
// Time exit: 15:59 ET on a later session, or one minute before RthCloseEt on an early-close day. Market next bar.
// Invalidation: a roll clears the close history before a new setup. An open trade is not flattened by the roll.
//
// ## 14. Regimes
// Suitable: the book's long-only equity-index pullback. No trend filter is coded.
// Unsuitable: the catalogue says shorts lose on the equity index. This file does not sell short.
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: none. The CME session clock is constructed in the signal.
// Engine settings enabled: RollDetection = Series, set in StartCalc before base.StartCalc.
//
// ## 16. Pitfalls
// Lookahead: the setup uses completed RTH closes, and the order is sent on a closed bar for the next bar.
// Repaint: Not applicable
// Survivorship: Not applicable
// Rolls: Series. rollOn(1) clears the close list. The catalogue does not require this.
// Data quality: the lower-open test uses the RTH open against the prior RTH close, not the Globex close. Not yet verified on a chart.
// Other: bars stamped 18:00–23:59 ET belong to the next Globex day, so the close test also requires the clock to be before 17:00.
//
// ## 17. Parameters
// This file declares no strategy [Input]. TraceLevel stays the base [Input] on _CustomSignal.
// Fixed example settings stay in the protected config class. They are not a sweep.
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
// Predeclared before any result was read: yes, 2026-11-12. No result for this file has been read. The coded arm is the engines-manual example, not a fitted set.
// catalogue-default: judge this file by its catalogue kill, catalogue §2.3, and the A2 generic kill when the idea is A2-origin. The stricter stated rule decides. This header does not change the catalogue.
// big-winner-trend: Not applied
// When 1% of winning trades is not a whole number, remove K = max(1, ceil(0.01 × the number of winning trades)). If there are no winning trades, remove none. This is the programme overview's proposed rounding, adopted here before any run.
//
// ## 19. Validation
// Status: Not yet verified
// Evidence: Not yet verified
// Window used: a run, when one exists, uses data through 2024-12-31 only.
// Holdout (W-M and W-D Jan 2025–Oct 2026; W-T last 125 sessions) stays unviewed. Do not put those results here.
//
// ## 20. Edge monitoring
// A person reads this section. It does not place orders and it does not change parameters.
// Expectancy: Not yet verified
//   Formula: win_rate * avg_win - loss_rate * avg_loss, same cost model as section 11.
// Observations: Not yet verified
//   Catalogue M19 discusses sample error. A minimum count for this strategy is written here before incubation, or it stays Not yet verified.
// Drift: Not yet verified
//   Compare regime mix, fill versus system price, and costs with the tested profile. Numbers come from this strategy's own test, written down before incubation.
// Warning: Not yet verified
//   Warning means look. It is not an order.
// Pause: Not yet verified
//   Pause means stop new entries. Open positions keep the exits in section 13.
// Review: Not yet verified
//   Review means the owner looks before new entries resume.
// Uncertainty: no run yet; long core only; the roll-clear choice; the holdout still sealed.
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: K3LE
// logSignal tag: A2-69
// Skip / flatten lines: "A2-69 roll at" when the close history is cleared; "A2-69 {day} closes(...) setup(...)" once per Globex day.
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Slot copy: the study's code identifiers are the public class and its constructor. A _TestNN slot is this file with those two tokens renamed to _TestNN. Order names and journal tags stay.
// Convention: fixed example settings stay in the protected config class, the same shape as the engines-manual example. A multi-value choice this file exposes is a nested public enum on its own [Input]. Catalogue arms the example does not code are not added here.
// Uncoded catalogue arms (SMA filter, profit-take, spike, Friday-short, extra hold day) are an owner question, not this file.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;
using System.Globalization;

namespace PowerLanguage.Strategy
{
	public class _A2_69_KaufmanThreeDay : _CustomSignal
	{
		// ---- protected config (no [Input]s besides TraceLevel) ----
		protected sealed class A269Config
		{
			public int contracts = 1;
			public TimeSpan rthOpen = new TimeSpan(9, 30, 0);
			public TimeSpan closeBar = new TimeSpan(15, 59, 0);   // ET close-stamp of the bar whose close sends the "15:59" order
		}
		protected A269Config Cfg { get; } = new A269Config();

		private IOrderMarket m_le, m_lx;
		private _CmeSessionClock m_clock;
		private readonly List<double> m_closes = new List<double>();   // 16:00 ET RTH closes C(t-1), C(t-2), C(t-3) ... newest first
		private DateTime m_day = DateTime.MinValue, m_entryDay = DateTime.MinValue;
		private double m_rthClose = double.NaN;   // last RTH value-window close of the current trading day
		private bool m_setup, m_openChecked, m_waitClose;

		public _A2_69_KaufmanThreeDay(object _ctx) : base(_ctx) { }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		// CME session adapter (section 1.6): Globex trading day + ET clock + RTH value window (09:30-16:00 ET, early close aware).
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "K3LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "K3LX", EOrderAction.Sell));
		}

		protected override void StartCalc()
		{
			m_clock = null;
			RollDetection = ERollDetection.Series;   // unadjusted series: detect splices
			base.StartCalc();
			m_closes.Clear(); m_rthClose = double.NaN; m_day = m_entryDay = DateTime.MinValue;
			m_setup = m_openChecked = m_waitClose = false;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime et = Clock.ToEastern(Bars.Time[0]);
			DateTime day = tradingDayKey(Bars.Time[0]);
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan closeBar = cme.RthCloseEt.HasValue ? cme.RthCloseEt.Value - TimeSpan.FromMinutes(1) : Cfg.closeBar;   // half-day

			if (rollOn(1))
			{
				m_closes.Clear(); m_rthClose = double.NaN;   // the series jumps by the roll gap: restart the 3-close count (journaled below)
				if (journalOn()) trace(ETraceLevel.Journal, $"A2-69 roll at {Bars.Time[0]:yyyy-MM-dd HH:mm}: close history cleared");
			}

			// 1. new trading day: push the completed session's RTH close as C(t-1), then evaluate the setup once
			if (day != m_day)
			{
				m_day = day; m_openChecked = false; m_waitClose = false;
				if (!double.IsNaN(m_rthClose)) { m_closes.Insert(0, m_rthClose); if (m_closes.Count > 3) m_closes.RemoveAt(3); }
				m_rthClose = double.NaN;
				m_setup = !cme.NoRth && m_closes.Count == 3 && m_closes[0] < m_closes[1] && m_closes[1] < m_closes[2];
				if (journalOn())
					trace(ETraceLevel.Journal, $"A2-69 {day:yyyy-MM-dd} closes({string.Join(",", m_closes)}) setup({m_setup}) noRth({cme.NoRth}) halfDay({cme.RthCloseEt.HasValue})");
			}

			bool rth = isRthBar(Bars.Time[0]);
			if (rth) m_rthClose = Bars.Close[0];   // ends as the 16:00 (or early-close) bar close

			// ET 18:00-23:59 bars belong to the NEXT trading day, so "after 15:59" must also be "before 17:00"
			bool atClose = et.TimeOfDay >= closeBar && et.TimeOfDay < new TimeSpan(17, 0, 0);

			int pos = StrategyInfo.MarketPosition;

			// 2. exit: on the session after the entry day, at the 15:59 bar
			if (pos > 0 && day > m_entryDay && atClose) { m_lx.Send(); return; }
			if (pos != 0 || !m_setup) return;

			// 3. first RTH bar (close-stamped 09:31): O(t) is that bar's open; lower open -> buy the next bar's open
			if (!m_openChecked && rth)
			{
				m_openChecked = true;
				double o = Bars.Open[0];
				if (o < m_closes[0]) { arm(o, "open"); return; }
				m_waitClose = true;
			}
			// 4. otherwise buy at the 15:59 bar (MOC proxy)
			if (m_waitClose && atClose) { m_waitClose = false; arm(Bars.Close[0], "close"); }
		}

		private void arm(double planned, string how)
		{
			m_setup = false; m_entryDay = m_day;
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "c1", m_closes[0].ToString() }, { "how", how } };
				journalArm("K3LE", planned, double.NaN, double.NaN, ctx);
				logSignal("A2-69", +1, planned, double.NaN, ctx, true);
			}
			m_le.Send(Cfg.contracts);
		}
	}
}
