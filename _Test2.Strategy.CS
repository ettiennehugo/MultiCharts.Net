using System;
using System.Collections.Generic;
using PowerLanguage.Function;
using System.Globalization;

namespace PowerLanguage.Strategy
{
	//public class _Ex01KaufmanThreeDay : _CustomSignal
	public class _Test2 : _CustomSignal
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

		public _Test2(object _ctx) : base(_ctx) { }

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
