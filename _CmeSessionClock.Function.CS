using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Spec VPF-7 (analyst-base-class-spec.md, VPF-11 in volume-profile-fix-proposals.md): the CME Globex session clock,
	/// extracted from _VolumeProfileFuturesBase into a composable helper so any strategy (a non-VP futures signal deriving
	/// _CustomSignal) can own one. Helper-file convention per the CS-0 probe (_SignalJournal.Function.CS: plain class in
	/// PowerLanguage.Function, class name = file name). No MultiCharts types: no Bars, no IInstrument, no study lifecycle.
	///
	/// It holds the session windows (US Eastern), the bar time zone, a one-entry per-bar-time cache and the holiday hooks.
	/// Hooks are delegates on the Globex trading date (ET). Defaults: with UseCmeHolidayTable (default true) the CME Globex
	/// equity-index calendar (_CmeHolidays.Rules); without it New Year / Christmas closures only (Sunday → Monday observed).
	/// No-RTH days default to NYSE holidays (_NyseHolidays.Rules) that are not full Globex closures.
	/// _VolumeProfileFuturesBase owns one (SessionClock) and points the hooks at its virtual hook methods, so its overrides
	/// keep working; its protected/public session API delegates here unchanged.
	///
	/// Usage from a non-VP strategy:
	///   m_clock = new _CmeSessionClock(resolveBarTimeZone(Bars.Info));   // StartCalc
	///   if (m_clock.State(Bars.Time[0]) == _CmeSessionClock.GlobexState.Open &amp;&amp; m_clock.InSession(Bars.Time[0], _CmeSessionClock.MarketSession.Rth)) ...
	/// Bar times are interpreted in BarTimeZone (null = this PC's zone); *Et methods take a time that is already US Eastern.
	/// </summary>
	public sealed class _CmeSessionClock
	{
		//types
		/// <summary>Flags indicating at what point of the day we are trading. Same values as _VolumeProfileFuturesBase.MarketSession.</summary>
		[Flags]
		public enum MarketSession
		{
			None = 0,
			Asia = 1 << 0,
			Europe = 1 << 1,
			NewYork = 1 << 2,
			Rth = 1 << 3,         // regular trading hours (session flag, RthStart → RthEnd)
			Premarket = 1 << 4,   // NewYorkStart → RthStart
			PostMarket = 1 << 5   // RthEnd → NewYorkEnd
		}

		/// <summary>General state of the futures market. Same values as _VolumeProfileFuturesBase.GlobexState.</summary>
		public enum GlobexState
		{
			Closed,   // weekend / holiday
			Halt,     // 17:00–18:00 ET weekdays, holiday halts
			Open
		}

		//constants (CME equity-index hours, ES/NQ; US Eastern)
		public static readonly TimeSpan DefaultAsiaStart = new TimeSpan(18, 0, 0);
		public static readonly TimeSpan DefaultAsiaEnd = new TimeSpan(3, 0, 0);
		public static readonly TimeSpan DefaultEuropeStart = new TimeSpan(3, 0, 0);
		public static readonly TimeSpan DefaultEuropeEnd = new TimeSpan(11, 30, 0);
		public static readonly TimeSpan DefaultNewYorkStart = new TimeSpan(8, 0, 0);
		public static readonly TimeSpan DefaultNewYorkEnd = new TimeSpan(17, 0, 0);
		public static readonly TimeSpan DefaultRthStart = new TimeSpan(9, 30, 0);
		public static readonly TimeSpan DefaultRthEnd = new TimeSpan(16, 15, 0);
		public static readonly TimeSpan DefaultRthValueEnd = new TimeSpan(16, 0, 0);
		public static readonly TimeSpan DefaultHaltStart = new TimeSpan(17, 0, 0);
		public static readonly TimeSpan DefaultHaltEnd = new TimeSpan(18, 0, 0);
		public static readonly TimeSpan DefaultGlobexOpen = new TimeSpan(18, 0, 0);
		public static readonly TimeSpan DefaultGlobexClose = new TimeSpan(17, 0, 0);

		private static readonly MarketSession[] s_singleSessions =
		{
			MarketSession.Asia, MarketSession.Europe, MarketSession.NewYork, MarketSession.Rth, MarketSession.Premarket, MarketSession.PostMarket
		};

		//properties
		// Session windows (US Eastern). Changing a window does not invalidate the per-bar cache (same as the futures base
		// before VPF-7); call Invalidate() if you change windows mid-run.
		public TimeSpan AsiaStart = DefaultAsiaStart, AsiaEnd = DefaultAsiaEnd;
		public TimeSpan EuropeStart = DefaultEuropeStart, EuropeEnd = DefaultEuropeEnd;
		public TimeSpan NewYorkStart = DefaultNewYorkStart, NewYorkEnd = DefaultNewYorkEnd;
		public TimeSpan RthStart = DefaultRthStart, RthEnd = DefaultRthEnd;
		/// <summary>End of RTH for value logic (D5, 16:00 ET); RthEnd (16:15) stays the session flag.</summary>
		public TimeSpan RthValueEnd = DefaultRthValueEnd;
		public TimeSpan HaltStart = DefaultHaltStart, HaltEnd = DefaultHaltEnd;
		public TimeSpan GlobexOpen = DefaultGlobexOpen, GlobexClose = DefaultGlobexClose;

		/// <summary>Default hooks only: true = CME Globex calendar (_CmeHolidays.Rules), false = New Year / Christmas closures only.</summary>
		public bool UseCmeHolidayTable = true;

		/// <summary>Hook: CME Globex fully closed on this Globex trading date. null = the default rule (see class summary).</summary>
		public Func<DateTime, bool> GlobexClosedDayHook;
		/// <summary>Hook: no cash (RTH) session on this Globex trading date. null = NYSE holidays that are not Globex closures.</summary>
		public Func<DateTime, bool> NoRthDayHook;
		/// <summary>Hook: early Globex halt (ET) for this Globex trading date, or null. null hook = the default rule.</summary>
		public Func<DateTime, TimeSpan?> HolidayHaltHook;
		/// <summary>Hook: early RTH close (ET) for this Globex trading date, or null. null hook = the default rule.</summary>
		public Func<DateTime, TimeSpan?> RthEarlyCloseHook;

		//attribute
		private readonly TimeZoneInfo m_est;
		private TimeZoneInfo m_barTz;
		private bool m_barIsEastern;
		private DateTime m_cacheBarTime;
		private DateTime m_cacheEt;
		private GlobexState m_cacheState;
		private MarketSession m_cacheSessions;
		private bool m_cacheValid;
		private int m_nyseYear = int.MinValue;
		private HashSet<DateTime> m_nyseDays;

		//interface methods
		/// <param name="barTimeZone">Zone the bar times are in (e.g. _CustomSignal.resolveBarTimeZone(Bars.Info)); null = this PC's zone.</param>
		public _CmeSessionClock(TimeZoneInfo barTimeZone = null)
		{
			try
			{
				m_est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
			}
			catch (TimeZoneNotFoundException)
			{
				m_est = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
			}
			BarTimeZone = barTimeZone;
		}

		/// <summary>US Eastern.</summary>
		public TimeZoneInfo Eastern { get { return m_est; } }

		/// <summary>Zone of the bar times (null = this PC's zone). Setting it clears the per-bar cache.</summary>
		public TimeZoneInfo BarTimeZone
		{
			get { return m_barTz; }
			set
			{
				m_barTz = value;
				m_barIsEastern = value != null && (value.Id == m_est.Id
					|| (value.BaseUtcOffset == m_est.BaseUtcOffset && value.HasSameRules(m_est)));
				m_cacheValid = false;
			}
		}

		/// <summary>Drop the one-entry per-bar cache (zone or windows changed).</summary>
		public void Invalidate() { m_cacheValid = false; }

		/// <summary>
		/// Bar time (in BarTimeZone) converted to US Eastern (DST-aware). A Utc-kind value is converted from UTC. Fall-back hour
		/// on a non-Eastern zone: an ambiguous wall time resolves as standard time; a spring-forward gap time moves forward 1h.
		/// </summary>
		public DateTime ToEastern(DateTime barTime)
		{
			if (barTime.Kind == DateTimeKind.Utc)
				return TimeZoneInfo.ConvertTimeFromUtc(barTime, m_est);

			DateTime t = DateTime.SpecifyKind(barTime, DateTimeKind.Unspecified);   // ConvertTime rejects Kind=Local with a non-Local source
			if (m_barIsEastern) return t;
			TimeZoneInfo src = m_barTz ?? TimeZoneInfo.Local;
			if (src.IsInvalidTime(t)) t = t.AddHours(1);
			return TimeZoneInfo.ConvertTime(t, src, m_est);
		}

		// ---- bar-time API (cached per bar time) ----
		/// <summary>US Eastern time of a bar time (cached).</summary>
		public DateTime EasternOf(DateTime barTime) { ensureBarCache(barTime); return m_cacheEt; }
		public GlobexState State(DateTime barTime) { ensureBarCache(barTime); return m_cacheState; }
		/// <summary>All sessions containing this bar time; overlaps are expected (10:05 ET → Europe | NewYork | Rth).</summary>
		public MarketSession Sessions(DateTime barTime) { ensureBarCache(barTime); return m_cacheSessions; }
		public bool InSession(DateTime barTime, MarketSession session) { return (Sessions(barTime) & session) == session; }
		public bool InAny(DateTime barTime, MarketSession sessions) { return (Sessions(barTime) & sessions) != MarketSession.None; }

		/// <summary>CME Globex trading date of a bar time: Sunday 18:30 ET belongs to Monday.</summary>
		public DateTime GlobexDate(DateTime barTime)
		{
			if (m_cacheValid && barTime == m_cacheBarTime) return GlobexDateEt(m_cacheEt);
			return GlobexDateEt(ToEastern(barTime));
		}

		// ---- Eastern-time cores: input is already ET, never converted ----
		public GlobexState StateEt(DateTime et)
		{
			var t = et.TimeOfDay;
			var d = et.DayOfWeek;

			// Holiday hooks, keyed on the Globex date so the evening before a closure is closed too.
			DateTime gx = GlobexDateEt(et);
			if (IsGlobexClosedDay(gx))
				return GlobexState.Closed;
			TimeSpan? holidayHalt = HolidayHaltEt(gx);
			if (holidayHalt.HasValue && et.Date == gx.Date && t >= holidayHalt.Value)
				return GlobexState.Halt;   // until the 18:00 reopen, which belongs to the next Globex date

			// Weekend: Fri after close through Sun before reopen
			if (d == DayOfWeek.Saturday)
				return GlobexState.Closed;
			if (d == DayOfWeek.Sunday && t < GlobexOpen)
				return GlobexState.Closed;
			if (d == DayOfWeek.Friday && t >= GlobexClose)
				return GlobexState.Closed;

			// Daily halt Mon–Thu 17:00–18:00 ET
			if (d != DayOfWeek.Sunday && d != DayOfWeek.Friday
					&& InWindow(t, HaltStart, HaltEnd, false))
				return GlobexState.Halt;

			return GlobexState.Open;
		}

		public MarketSession SessionsEt(DateTime et)
		{
			if (StateEt(et) != GlobexState.Open)
				return MarketSession.None;

			// Every single-flag session uses its TryWindow window, so Sessions and TrySessionBounds agree.
			var t = et.TimeOfDay;
			var s = MarketSession.None;
			for (int i = 0; i < s_singleSessions.Length; i++)
			{
				TimeSpan start, end;
				bool wraps;
				if (TryWindow(s_singleSessions[i], out start, out end, out wraps) && InWindow(t, start, end, wraps))
					s |= s_singleSessions[i];
			}

			// No cash session on NYSE holidays; early RTH close where configured.
			DateTime gx = GlobexDateEt(et);
			if (IsNoRthDay(gx))
				s &= ~(MarketSession.Rth | MarketSession.Premarket | MarketSession.PostMarket);
			else
			{
				TimeSpan? early = RthEarlyCloseEt(gx);
				if (early.HasValue && et.Date == gx.Date && t >= early.Value)
					s &= ~(MarketSession.Rth | MarketSession.PostMarket);
			}
			return s;
		}

		public DateTime GlobexDateEt(DateTime et)
		{
			if (et.TimeOfDay >= GlobexOpen)
				return et.Date.AddDays(1);
			return et.Date;
		}

		/// <summary>Single label. Priority: RTH > Premarket > PostMarket > NewYork > Europe > Asia.</summary>
		public MarketSession PrimarySessionEt(DateTime et)
		{
			MarketSession s = SessionsEt(et);
			if ((s & MarketSession.Rth) != 0) return MarketSession.Rth;
			if ((s & MarketSession.Premarket) != 0) return MarketSession.Premarket;
			if ((s & MarketSession.PostMarket) != 0) return MarketSession.PostMarket;
			if ((s & MarketSession.NewYork) != 0) return MarketSession.NewYork;
			if ((s & MarketSession.Europe) != 0) return MarketSession.Europe;
			if ((s & MarketSession.Asia) != 0) return MarketSession.Asia;
			return MarketSession.None;
		}

		/// <summary>Elapsed time since the session open on the Globex day of <paramref name="et"/>; TimeSpan.MinValue if not active.</summary>
		public TimeSpan TimeSinceOpenEt(DateTime et, MarketSession session)
		{
			DateTime open, close;
			if (!TrySessionBoundsEt(et, session, out open, out close)) return TimeSpan.MinValue;
			if (et < open || et >= close) return TimeSpan.MinValue;
			return et - open;
		}

		public int MinutesSinceOpenEt(DateTime et, MarketSession session)
		{
			TimeSpan elapsed = TimeSinceOpenEt(et, session);
			return elapsed == TimeSpan.MinValue ? -1 : (int)elapsed.TotalMinutes;
		}

		public TimeSpan TimeUntilCloseEt(DateTime et, MarketSession session)
		{
			DateTime open, close;
			if (!TrySessionBoundsEt(et, session, out open, out close)) return TimeSpan.MinValue;
			if (et < open || et >= close) return TimeSpan.MinValue;
			return close - et;
		}

		public int MinutesUntilCloseEt(DateTime et, MarketSession session)
		{
			TimeSpan left = TimeUntilCloseEt(et, session);
			return left == TimeSpan.MinValue ? -1 : (int)left.TotalMinutes;
		}

		public TimeSpan SessionLength(MarketSession session)
		{
			TimeSpan start, end;
			bool wraps;
			if (!TryWindow(session, out start, out end, out wraps)) return TimeSpan.Zero;
			if (!wraps) return end - start;
			return TimeSpan.FromDays(1) - start + end;
		}

		public bool IsOpeningRangeEt(DateTime et, MarketSession session, int minutes)
		{
			int elapsed = MinutesSinceOpenEt(et, session);
			return elapsed >= 0 && elapsed < minutes;
		}

		public bool IsRthAfterInitialBalanceEt(DateTime et, int ibMinutes)
		{
			return MinutesSinceOpenEt(et, MarketSession.Rth) >= ibMinutes;
		}

		/// <summary>
		/// Open/close (ET) of a single session on the Globex day that contains <paramref name="et"/>. Wrapping windows (Asia)
		/// open on the previous calendar date. RTH close honours RthEarlyCloseEt. False for None / combined flags.
		/// </summary>
		public bool TrySessionBoundsEt(DateTime et, MarketSession session, out DateTime open, out DateTime close)
		{
			open = DateTime.MinValue;
			close = DateTime.MinValue;
			TimeSpan start, end;
			bool wraps;
			if (!TryWindow(session, out start, out end, out wraps)) return false;

			DateTime gxDate = GlobexDateEt(et);
			DateTime gxStartCalendar = gxDate.AddDays(-1);
			if (wraps)
			{
				open = gxStartCalendar + start;
				close = gxDate + end;
				return true;
			}
			open = gxDate + start;
			close = gxDate + end;
			if (session == MarketSession.Rth)
			{
				TimeSpan? early = RthEarlyCloseEt(gxDate);
				if (early.HasValue && close > gxDate + early.Value) close = gxDate + early.Value;
			}
			return true;
		}

		/// <summary>Window of a single session flag (ET). False for None / combined flags.</summary>
		public bool TryWindow(MarketSession session, out TimeSpan start, out TimeSpan end, out bool wraps)
		{
			start = TimeSpan.Zero;
			end = TimeSpan.Zero;
			wraps = false;
			if (session == MarketSession.None || !IsSingleFlag(session)) return false;
			switch (session)
			{
				case MarketSession.Asia: start = AsiaStart; end = AsiaEnd; wraps = true; return true;
				case MarketSession.Europe: start = EuropeStart; end = EuropeEnd; return true;
				case MarketSession.NewYork: start = NewYorkStart; end = NewYorkEnd; return true;
				case MarketSession.Rth: start = RthStart; end = RthEnd; return true;
				case MarketSession.Premarket: start = NewYorkStart; end = RthStart; return true;
				case MarketSession.PostMarket: start = RthEnd; end = NewYorkEnd; return true;
				default: return false;
			}
		}

		/// <summary>RTH value-window end for a Globex date: the early RTH close when earlier, else RthValueEnd.</summary>
		public TimeSpan RthValueEndEt(DateTime globexDate)
		{
			TimeSpan? early = RthEarlyCloseEt(globexDate);
			return early.HasValue && early.Value < RthValueEnd ? early.Value : RthValueEnd;
		}

		/// <summary>The close-stamped bar at ET <paramref name="et"/> is in the RTH value window (RthStart, RthValueEndEt].</summary>
		public bool IsRthValueBarEt(DateTime et)
		{
			DateTime gx = GlobexDateEt(et);
			if (et.Date != gx.Date || IsGlobexClosedDay(gx) || IsNoRthDay(gx)) return false;
			TimeSpan t = et.TimeOfDay;
			return t > RthStart && t <= RthValueEndEt(gx);
		}

		// ---- holiday hooks (Globex trading date, ET) ----
		public bool IsGlobexClosedDay(DateTime globexDate)
		{
			return GlobexClosedDayHook != null ? GlobexClosedDayHook(globexDate) : DefaultGlobexClosedDay(globexDate, UseCmeHolidayTable);
		}

		public bool IsNoRthDay(DateTime globexDate)
		{
			if (NoRthDayHook != null) return NoRthDayHook(globexDate);
			return isNyseHoliday(globexDate.Date) && !IsGlobexClosedDay(globexDate);
		}

		public TimeSpan? HolidayHaltEt(DateTime globexDate)
		{
			if (HolidayHaltHook != null) return HolidayHaltHook(globexDate);
			return UseCmeHolidayTable ? _CmeHolidays.Rules.Get(globexDate).HaltEt : null;
		}

		public TimeSpan? RthEarlyCloseEt(DateTime globexDate)
		{
			if (RthEarlyCloseHook != null) return RthEarlyCloseHook(globexDate);
			return UseCmeHolidayTable ? _CmeHolidays.Rules.Get(globexDate).RthCloseEt : null;
		}

		/// <summary>
		/// Default closure rule. Table: _CmeHolidays.Rules (New Year, Christmas, Good Friday except abbreviated years,
		/// weekend-observed). No table: New Year and Christmas only, Sunday → observed Monday; a Saturday holiday closes nothing.
		/// </summary>
		public static bool DefaultGlobexClosedDay(DateTime globexDate, bool useCmeHolidayTable)
		{
			DateTime d = globexDate.Date;
			if (useCmeHolidayTable) return _CmeHolidays.Rules.Get(d).Closed;
			if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) return false;
			DateTime ny = new DateTime(d.Year, 1, 1), xmas = new DateTime(d.Year, 12, 25);
			if (ny.DayOfWeek != DayOfWeek.Saturday && d == _CmeHolidays.Rules.SundayToMonday(ny)) return true;
			if (xmas.DayOfWeek != DayOfWeek.Saturday && d == _CmeHolidays.Rules.SundayToMonday(xmas)) return true;
			return false;
		}

		/// <summary>Window test [start, end); wraps = the window crosses midnight (Asia 18:00 → 03:00).</summary>
		public static bool InWindow(TimeSpan t, TimeSpan start, TimeSpan end, bool wraps)
		{
			if (!wraps) return t >= start && t < end;
			return t >= start || t < end;
		}

		public static bool IsSingleFlag(MarketSession session)
		{
			int v = (int)session;
			return v != 0 && (v & (v - 1)) == 0;
		}

		//methods
		private void ensureBarCache(DateTime barTime)
		{
			if (m_cacheValid && barTime == m_cacheBarTime) return;
			m_cacheBarTime = barTime;
			m_cacheEt = ToEastern(barTime);
			m_cacheState = StateEt(m_cacheEt);
			m_cacheSessions = SessionsEt(m_cacheEt);
			m_cacheValid = true;
		}

		private bool isNyseHoliday(DateTime date)
		{
			if (date.Year != m_nyseYear)
			{
				m_nyseYear = date.Year;
				m_nyseDays = new HashSet<DateTime>(_NyseHolidays.Rules.Holidays(date.Year));
			}
			return m_nyseDays.Contains(date);
		}
	}
}
