// **Features (direct).** F-ROLL (§5.7), F-CAL (§5.8), F-JRNL (§5.13), F-EVT (§5.14).
// **Catalogue rules (A2-69, core arm).** Setup: C(t−1) < C(t−2) < C(t−3), two lower RTH closes. Entry (long only): if the open O(t) < C(t−1),
//   buy at 09:31 (the first 1m bar's open); otherwise buy at 15:59 (MOC proxy). Exit at 15:59 on the following session. No stop in the core.
//   The SMA, profit-take, spike and Friday-short arms are not coded.
// **Chart.**
// - @ES 1m, dxFeed continuous unadjusted, CME Globex template. Inputs: only `TraceLevel`. Settings are in the protected `A269Config`.
//
// **Walk-through.**
// - The RTH close of each session is the close of its last RTH bar (`isRthBar`, through 16:00 ET or the early close), collected per Globex day. 
//   NoRth days never set up.
// - At the start of each Globex day the setup is tested on the last three completed RTH closes. At the close of the first RTH bar (close-stamped
//   09:31), if that bar's open is below C1, a market order goes in for the next bar's open.
// - Otherwise it waits for the bar close-stamped 15:59 (one minute before an early close) and sends a market order for the next bar.
// - The exit is sent the same way at 15:59 on any later session (normally the next one).
// - With TraceLevel Journal, `journalArm` records the planned entry and `logSignal` writes an `MCEvents` row for each entry.
//
// **Pitfalls.**
// - "Lower open" compares the RTH open with the prior **RTH** close, not the Globex close.
// - On an early-close day the "15:59" bar is one minute before `RthCloseEt`.
// - **Coding choice (labelled):** RollDetection is Series and `rollOn(1)` clears the close history, so the setup never compares closes from two contracts. The catalogue does not say this.
// - A 1m entry "at 09:31" is the next bar's open after the 09:31 bar closes. That is the closest MC can do with market-next-bar orders.

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
