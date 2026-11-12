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
// Input: Side
// Purpose: which side of the three-close pattern may trade
// Default: LongOnly — two lower closes, buy. This is the previous trade list.
// Units: Not applicable
// Range: LongOnly, Both, FridayShort. Any other value throws in StartCalc.
// Effect: Both adds the mirror short (two higher closes). FridayShort takes only that short, and only when the Globex date is Friday.
// Interactions: Trend applies to both sides
// Fixed or research-tuned: fixed default. The other members are the book arms.
// Input: Trend
// Purpose: prior RTH close versus an SMA of RTH closes
// Default: Off — no SMA filter. This is the previous trade list.
// Units: Not applicable
// Range: Off, Sma50, Sma120. A side is allowed only when the newest close is beyond that SMA. Fewer than N closes blocks the trade. Any other value throws in StartCalc.
// Effect: a filter, not a change to the three-close count
// Interactions: Side
// Fixed or research-tuned: fixed default. 50 and 120 are the catalogue arms.
// Input: Hold
// Purpose: how many later sessions the trade is held
// Default: NextClose — exit at 15:59 on the next session. This is the previous trade list.
// Units: sessions
// Range: NextClose, ExtraDay. Any other value throws in StartCalc.
// Effect: ExtraDay waits for the second later session's 15:59 bar
// Interactions: Not applicable
// Fixed or research-tuned: fixed default. ExtraDay is the book arm.
// Input: ProfitTake
// Purpose: intraday limit at the fill ± 2 × ATR20d
// Default: false — no target. This is the previous trade list.
// Units: Not applicable
// Range: false, true. True turns M-DAILY on and latches ATR20d at the entry.
// Effect: a limit is sent while the position is open
// Interactions: Spike shares the daily engine
// Fixed or research-tuned: fixed default. True is the book profit-take.
// Input: Spike
// Purpose: also enter a day whose close is at least 2.5 × ATR20d through the prior close and in the outer 25 percent of that day's range
// Default: false. This is the previous trade list.
// Units: Not applicable
// Range: false, true
// Effect: an extra entry at the 15:59 bar when the three-close setup did not fire
// Interactions: Side decides which spike is allowed. Trend still applies.
// Fixed or research-tuned: fixed default. True is the book spike rule.
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
// Side, Trend, Hold, ProfitTake and Spike are inputs. Every default is the previous long core. The SMA is an average of stored RTH closes, not DailyConfig.Sma200.
using System;
using System.Collections.Generic;
using PowerLanguage.Function;
using System.Globalization;

namespace PowerLanguage.Strategy
{
	public class _A2_69_KaufmanThreeDay : _CustomSignal
	{
		/// <summary>LongOnly is the current core. Both mirrors the pattern for shorts. FridayShort takes only the short pattern, and only on Friday.</summary>
		public enum ESide { LongOnly, Both, FridayShort }
		/// <summary>Off is the current core. Otherwise the prior RTH close must be on the trade's side of that SMA.</summary>
		public enum ETrend { Off, Sma50, Sma120 }
		/// <summary>NextClose exits the following session. ExtraDay waits one more session.</summary>
		public enum EHold { NextClose, ExtraDay }

		[Input] public ESide Side { get; set; }
		[Input] public ETrend Trend { get; set; }
		[Input] public EHold Hold { get; set; }
		[Input] public bool ProfitTake { get; set; }
		[Input] public bool Spike { get; set; }

		protected sealed class A269Config
		{
			public int contracts = 1;
			public TimeSpan rthOpen = new TimeSpan(9, 30, 0);
			public TimeSpan closeBar = new TimeSpan(15, 59, 0);   // ET close-stamp of the bar whose close sends the "15:59" order
		}
		protected A269Config Cfg { get; } = new A269Config();

		private IOrderMarket m_le, m_se, m_lx, m_sx;
		private IOrderPriced m_lt, m_st;
		private _CmeSessionClock m_clock;
		private readonly List<double> m_closes = new List<double>();   // 16:00 ET RTH closes, newest first. The core uses the first three.
		private DateTime m_day = DateTime.MinValue, m_entryDay = DateTime.MinValue;
		private double m_rthClose = double.NaN, m_rthHigh = double.NaN, m_rthLow = double.NaN, m_atrAtEntry = double.NaN;
		private bool m_longSetup, m_shortSetup, m_openChecked, m_waitLong, m_waitShort;
		private int m_sessionsAfter;

		public _A2_69_KaufmanThreeDay(object _ctx) : base(_ctx)
		{
			Side = ESide.LongOnly; Trend = ETrend.Off; Hold = EHold.NextClose; ProfitTake = false; Spike = false;
		}

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		// CME session adapter (section 1.6): Globex trading day + ET clock + RTH value window (09:30-16:00 ET, early close aware).
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "K3LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "K3SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "K3LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "K3SX", EOrderAction.BuyToCover));
			m_lt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "K3LT", EOrderAction.Sell, OrderExit.FromAll));
			m_st = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "K3ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			if (!Enum.IsDefined(typeof(ESide), Side)) throw new ArgumentException("_A2_69_KaufmanThreeDay: Side is not a defined ESide");
			if (!Enum.IsDefined(typeof(ETrend), Trend)) throw new ArgumentException("_A2_69_KaufmanThreeDay: Trend is not a defined ETrend");
			if (!Enum.IsDefined(typeof(EHold), Hold)) throw new ArgumentException("_A2_69_KaufmanThreeDay: Hold is not a defined EHold");
			if (ProfitTake || Spike) NeedsDaily = true;
			m_clock = null;
			RollDetection = ERollDetection.Series;   // unadjusted series: detect splices
			base.StartCalc();
			m_closes.Clear(); m_rthClose = m_rthHigh = m_rthLow = m_atrAtEntry = double.NaN; m_day = m_entryDay = DateTime.MinValue;
			m_longSetup = m_shortSetup = m_openChecked = m_waitLong = m_waitShort = false; m_sessionsAfter = 0;
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
				m_closes.Clear(); m_rthClose = m_rthHigh = m_rthLow = double.NaN;
				if (journalOn()) trace(ETraceLevel.Journal, $"A2-69 roll at {Bars.Time[0]:yyyy-MM-dd HH:mm}: close history cleared");
			}

			int pos = StrategyInfo.MarketPosition;
			if (day != m_day)
			{
				bool wasIn = pos != 0;
				m_day = day; m_openChecked = false; m_waitLong = m_waitShort = false;
				if (!double.IsNaN(m_rthClose)) { m_closes.Insert(0, m_rthClose); if (m_closes.Count > 120) m_closes.RemoveAt(120); }
				m_rthClose = m_rthHigh = m_rthLow = double.NaN;
				bool down = m_closes.Count > 2 && m_closes[0] < m_closes[1] && m_closes[1] < m_closes[2];
				bool up = m_closes.Count > 2 && m_closes[0] > m_closes[1] && m_closes[1] > m_closes[2];
				m_longSetup = !cme.NoRth && down && trendOk(+1) && Side != ESide.FridayShort;
				m_shortSetup = !cme.NoRth && up && trendOk(-1) && Side != ESide.LongOnly && (Side != ESide.FridayShort || day.DayOfWeek == DayOfWeek.Friday);
				if (wasIn && m_entryDay != DateTime.MinValue && day > m_entryDay) m_sessionsAfter++;
				if (journalOn())
					trace(ETraceLevel.Journal, $"A2-69 {day:yyyy-MM-dd} closes({string.Join(",", m_closes)}) long({m_longSetup}) short({m_shortSetup}) noRth({cme.NoRth}) halfDay({cme.RthCloseEt.HasValue})");
			}

			bool rth = isRthBar(Bars.Time[0]);
			if (rth)
			{
				m_rthClose = Bars.Close[0];
				m_rthHigh = double.IsNaN(m_rthHigh) ? Bars.High[0] : Math.Max(m_rthHigh, Bars.High[0]);
				m_rthLow = double.IsNaN(m_rthLow) ? Bars.Low[0] : Math.Min(m_rthLow, Bars.Low[0]);
			}

			bool atClose = et.TimeOfDay >= closeBar && et.TimeOfDay < new TimeSpan(17, 0, 0);
			pos = StrategyInfo.MarketPosition;
			if (ProfitTake && pos != 0 && m_atrAtEntry > 0)
			{
				double tgt = StrategyInfo.AvgEntryPrice + Math.Sign(pos) * 2.0 * m_atrAtEntry;
				if (pos > 0) m_lt.Send(tgt); else m_st.Send(tgt);
			}
			int need = Hold == EHold.ExtraDay ? 2 : 1;
			if (pos != 0 && m_sessionsAfter >= need && atClose) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }
			if (pos != 0) return;

			if (!m_openChecked && rth && (m_longSetup || m_shortSetup))
			{
				m_openChecked = true;
				double o = Bars.Open[0];
				if (m_longSetup && o < m_closes[0]) { arm(+1, o, "open"); return; }
				if (m_shortSetup && o > m_closes[0]) { arm(-1, o, "open"); return; }
				m_waitLong = m_longSetup && !(o < m_closes[0]);
				m_waitShort = m_shortSetup && !(o > m_closes[0]);
			}
			if (atClose && m_waitLong) { m_waitLong = false; arm(+1, Bars.Close[0], "close"); return; }
			if (atClose && m_waitShort) { m_waitShort = false; arm(-1, Bars.Close[0], "close"); return; }
			if (Spike && atClose && !m_longSetup && !m_shortSetup && m_closes.Count > 0 && m_atrAtEntryReady()) trySpike();
		}

		private bool m_atrAtEntryReady()
		{
			_DailyContext d = DailyContext;
			return d != null && d.Valid && d.Atr20d > 0;
		}

		private void trySpike()
		{
			double atr = DailyContext.Atr20d, rng = m_rthHigh - m_rthLow;
			if (!(atr > 0) || !(rng > 0)) return;
			bool longSpike = m_rthClose <= m_closes[0] - 2.5 * atr && (m_rthClose - m_rthLow) / rng <= 0.25;
			bool shortSpike = m_rthClose >= m_closes[0] + 2.5 * atr && (m_rthHigh - m_rthClose) / rng <= 0.25;
			bool friday = m_day.DayOfWeek == DayOfWeek.Friday;
			if (longSpike && Side != ESide.FridayShort && trendOk(+1)) arm(+1, m_rthClose, "spike");
			else if (shortSpike && Side != ESide.LongOnly && (Side != ESide.FridayShort || friday) && trendOk(-1)) arm(-1, m_rthClose, "spike");
		}

		private bool trendOk(int side)
		{
			if (Trend == ETrend.Off) return true;
			int n = Trend == ETrend.Sma50 ? 50 : 120;
			if (m_closes.Count < n) return false;
			double s = 0;
			for (int i = 0; i < n; i++) s += m_closes[i];
			double sma = s / n;
			return side > 0 ? m_closes[0] > sma : m_closes[0] < sma;
		}

		private void arm(int side, double planned, string how)
		{
			m_longSetup = false; m_shortSetup = false; m_entryDay = m_day; m_sessionsAfter = 0;
			m_atrAtEntry = (ProfitTake || Spike) && DailyContext != null ? DailyContext.Atr20d : double.NaN;
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "c1", m_closes[0].ToString() }, { "how", how }, { "side", Side.ToString() } };
				journalArm(side > 0 ? "K3LE" : "K3SE", planned, double.NaN, double.NaN, ctx);
				logSignal("A2-69", side, planned, double.NaN, ctx, true);
			}
			if (side > 0) m_le.Send(Cfg.contracts); else m_se.Send(Cfg.contracts);
		}
	}
}
