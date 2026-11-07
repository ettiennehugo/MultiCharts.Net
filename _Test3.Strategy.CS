using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	//public class _Ex02EuropeanOpenDrift : _CustomSignal
	public class _Test03 : _CustomSignal
	{
		/// <summary>Sweep (catalogue): 1 = 01:30-04:30, 2 = 02:00-03:00, 3 = 1 + event filter, 4 = 2 + event filter.</summary>
		[Input] public int Variant { get; set; }

		protected sealed class Ts01Config
		{
			public int contracts = 1;
			public double disasterAtr = 2.0;
			public int atrLength = 14;       // ATR60m length is not in the catalogue: an assumption of this example
			public int costFloorTicks = 14;  // catalogue 2.1: ES cost floor; stop distance = max(structural, floor), size never shrunk
			public double riskBudgetFraction = 0.01;  // floor-rule risk budget: Ettienne, 2026-10-23: 1% of equity
		}
		protected Ts01Config Cfg { get; } = new Ts01Config();

		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_lxStp;
		private _CmeSessionClock m_clock;
		private TimeSpan m_start, m_end;
		private bool m_filter;
		private DateTime m_doneDay = DateTime.MinValue;
		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
		private double m_stop = double.NaN;

		public _Test03(object _ctx) : base(_ctx) { Variant = 1; }

		private _CmeSessionClock Clock { get { if (m_clock == null) m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info)); return m_clock; } }
		protected override DateTime tradingDayKey(DateTime t) { return Clock.GlobexDate(t); }
		protected override TimeSpan sessionClock(DateTime t) { return Clock.ToEastern(t).TimeOfDay; }
		protected override bool isRthBar(DateTime t) { return Clock.IsRthValueBarEt(Clock.ToEastern(t)); }

		protected override void Create()
		{
			base.Create();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TS01LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "TS01LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TS01DS", EOrderAction.Sell, OrderExit.FromAll));
			// M-EVENT: created only when enabled (StartCalc). T1 only; coverage holes of lower tiers never block (EV-4.6b).
			EventConfig.minTier = EEventTier.T1;
			EventConfig.classes = EEventClassMask.Macro;
		}

		protected override void StartCalc()
		{
			if (Variant < 1 || Variant > 4) throw new ArgumentException("_Ex02EuropeanOpenDrift: Variant must be 1-4");
			bool narrow = Variant == 2 || Variant == 4;
			m_start = narrow ? new TimeSpan(2, 0, 0) : new TimeSpan(1, 30, 0);
			m_end = narrow ? new TimeSpan(3, 0, 0) : new TimeSpan(4, 30, 0);
			m_filter = Variant >= 3;
			EventConfig.enabled = m_filter;   // set before base.StartCalc: the engine is built there
			HtfStreams = new[] { 2 };         // M-MTF: StartCalc fails loudly if Data2 is missing or another root
			m_clock = null;
			base.StartCalc();
			m_doneDay = DateTime.MinValue; m_stop = double.NaN;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime et = Clock.ToEastern(Bars.Time[0]);
			DateTime day = tradingDayKey(Bars.Time[0]);   // the 01:30 ET bar belongs to the Globex day that opened 18:00 ET the evening before
			TimeSpan tod = et.TimeOfDay;
			int pos = StrategyInfo.MarketPosition;

			if (pos > 0)
			{
				if (tod >= m_end && tod < new TimeSpan(17, 0, 0)) { m_lx.Send(); return; }
				if (!double.IsNaN(m_stop)) m_lxStp.Send(m_stop);   // re-send every bar (MC cancels unsent orders)
				return;
			}
			m_stop = double.NaN;
			if (day == m_doneDay) return;
			// the entry order is sent on the close of the FIRST closed bar stamped at or after m_start - 1 min (normally that
			// bar, so it fills at the m_start bar's open); a missing bar delays the entry, it does not skip the night
			TimeSpan sendFrom = m_start - TimeSpan.FromMinutes(1);
			if (tod >= m_end && tod < new TimeSpan(17, 0, 0))
			{
				m_doneDay = day;   // the window passed with no closed bar in it
				if (journalOn()) trace(ETraceLevel.Journal, $"TS-01 {day:yyyy-MM-dd} skip: no bar in {sendFrom:hh\\:mm}-{m_end:hh\\:mm} ET");
				return;
			}
			if (tod < sendFrom || tod >= m_end) return;
			m_doneDay = day;

			string skip = skipReason(day);
			double atr = atrClosed(2, Cfg.atrLength);
			if (skip == null && !(atr > 0)) skip = "ATR60m warm-up";
			if (skip != null) { if (journalOn()) trace(ETraceLevel.Journal, $"TS-01 {day:yyyy-MM-dd} skip: {skip}"); return; }

			double planned = Bars.Close[0];
			m_stop = planned - Cfg.disasterAtr * atr;   // planned from the signal close; the fill is the next bar's open
			double tick = tickSize();
			if (planned - m_stop < Cfg.costFloorTicks * tick) m_stop = planned - Cfg.costFloorTicks * tick;   // max(structural, 14 ticks)
			if (overRiskBudget(planned - m_stop))   // widened stop breaches the 1% budget: skip and log, never resize
			{
				if (journalOn()) trace(ETraceLevel.Journal, $"TS-01 {day:yyyy-MM-dd} skip: stop {(planned - m_stop) / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}");
				m_stop = double.NaN; return;
			}
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "variant", Variant.ToString() }, { "atr60", atr.ToString("F2") }, { "sendBar", tod.ToString(@"hh\:mm") } };
				journalArm("TS01LE", planned, m_stop, double.NaN, ctx);
				logSignal("TS-01", +1, planned, m_stop, ctx, true);
			}
			m_le.Send(Cfg.contracts);
			m_lxStp.Send(m_stop);   // also protects the entry bar
		}

		private string skipReason(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			if (cme.Closed || cme.NoRth || cme.HaltEt.HasValue) return "CME holiday/halt " + cme.Name;
			if (!m_filter) return null;
			_EventCalendar ev = EventCalendar;
			if (ev == null || ev.Disabled) return "event filter on but M-EVENT disabled";   // fail closed
			EEventKind hole;
			if (ev.CoverageBlocks(day, EEventClassMask.Macro, EEventTier.T1, out hole)) return "coverage Unknown " + hole;
			foreach (EventRec r in ev.EventsOn(day, EEventClassMask.Macro))
				if (r.tier == EEventTier.T1 && r.etTime.TimeOfDay == EventRules.T0830) return "T1 08:30 " + r.KindName;
			return null;
		}

		/// <summary>Simple-average true range over the last n CLOSED bars of stream ds (needs n + 1 closed bars).</summary>
		private double atrClosed(int ds, int n)
		{
			if (BarsOfData(ds).CurrentBar <= n + 1) return double.NaN;
			double sum = 0;
			for (int k = 0; k < n; k++)
			{
				DateTime t; double o, h, l, c, v, pc;
				HtfClosedBar(ds, k + 1, out t, out o, out h, out l, out pc, out v);
				HtfClosedBar(ds, k, out t, out o, out h, out l, out c, out v);
				sum += Math.Max(h - l, Math.Max(Math.Abs(h - pc), Math.Abs(l - pc)));
			}
			return sum / n;
		}
	}
}
