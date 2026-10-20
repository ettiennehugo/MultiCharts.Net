using ATCenterProxy.interop;
using PowerLanguage.Function;
using System;
using System.Collections.Generic;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Volume profile base class for futures trading. Provides common mechanisms that can be used in signals that trade
	/// volume profiles and VWAP in the futures market.
	/// 
	/// Chart Setup:
	/// 1. The bar time zone is read from Bars.Info.TimeZone (Format Instrument → Settings → Time Zone): Local = this PC's
	///    zone, GMT = UTC, Exchange = the zone of Bars.Info.Exchange (see exchangeTimeZoneId; unknown exchanges fail in StartCalc).
	/// 2. All session logic runs in US Eastern; ToEastern converts each bar time once (cached per bar).
	/// 3. Use the "CME Globex" session template (or equivalent) so the VP base's session VWAP reset agrees with
	///    the Globex trading day that tradingDayKey/GlobexDate use (18:00 → 17:00 ET).
	/// 
	/// General Notes:
	/// * Allow entries only when InSession(MarketSession.Rth) - high liquidity confirms moves.
	/// * Stand down in Asia unless you are explicitly studying overnight inventory.
	/// * Treat IsLondonNyOverlap() as a quality filter, not a requirement, until you have stats.
	/// * Reset developing value area on IsNewGlobexDay(), not at midnight.
	/// * Do not send orders when GetGlobexState() != Open.
	/// </summary>
	public abstract class _VolumeProfileFuturesBase : _VolumeProfileBase
	{
		//constants
		public const string DEFAULT_ASIA_START = "18:00"; // EST Asia session start
		public const string DEFAULT_ASIA_END = "03:00";   // EST Asia session end
		public const string DEFAULT_EUROPE_START = "03:00"; // EST Europe session start
		public const string DEFAULT_EUROPE_END = "11:30"; // EST Europe sessions end
		public const string DEFAULT_NEWYORK_START = "08:00";  // EST New-York session start
		public const string DEFAULT_NEWYORK_END = "17:00";  // EST New York session end
		public const string DEFAULT_RTH_START = "09:30";  // EST Regular Trading Hours (high volume) session start
		public const string DEFAULT_RTH_END = "16:15";  // EST Regular Trading Hours (high volume) session end
		public const string DEFAULT_HALT_START = "17:00"; // EST Daily maintenance halt session start
		public const string DEFAULT_HALT_END = "18:00"; // EST Daily maintenance halt session end
		public const string DEFAULT_GLOBEX_OPEN = "18:00";  // EST Sunday open after weekend
		public const string DEFAULT_GLOBEX_CLOSE = "17:00"; // EST Friday close for weekend

		// Same defaults as TimeSpans (VPF-13: no culture-sensitive TimeSpan.Parse per instance). Keep both in sync.
		public static readonly TimeSpan DefaultAsiaStart = new TimeSpan(18, 0, 0);
		public static readonly TimeSpan DefaultAsiaEnd = new TimeSpan(3, 0, 0);
		public static readonly TimeSpan DefaultEuropeStart = new TimeSpan(3, 0, 0);
		public static readonly TimeSpan DefaultEuropeEnd = new TimeSpan(11, 30, 0);
		public static readonly TimeSpan DefaultNewYorkStart = new TimeSpan(8, 0, 0);
		public static readonly TimeSpan DefaultNewYorkEnd = new TimeSpan(17, 0, 0);
		public static readonly TimeSpan DefaultRthStart = new TimeSpan(9, 30, 0);
		public static readonly TimeSpan DefaultRthEnd = new TimeSpan(16, 15, 0);
		public static readonly TimeSpan DefaultHaltStart = new TimeSpan(17, 0, 0);
		public static readonly TimeSpan DefaultHaltEnd = new TimeSpan(18, 0, 0);
		public static readonly TimeSpan DefaultGlobexOpen = new TimeSpan(18, 0, 0);
		public static readonly TimeSpan DefaultGlobexClose = new TimeSpan(17, 0, 0);

		//types
		/// <summary>
		/// Flags indicating at what point of the day we are trading.
		/// </summary>
		[Flags]
		public enum MarketSession
		{
			None = 0, // God help you, we don't know
			Asia = 1 << 0,    // Asia session
			Europe = 1 << 1,  // Europe session
			NewYork = 1 << 2, // New-York session
			Rth = 1 << 3,       // regular trading hours, high volume session
			Premarket = 1 << 4, // 08:00–09:30 ET before the regular trading hours
			PostMarket = 1 << 5 // 16:15–17:00 ET
		}

		/// <summary>
		/// General state of the futures market.
		/// </summary>
		public enum GlobexState
		{
			Closed,   // weekend
			Halt,     // 17:00–18:00 ET weekdays
			Open			// open - see MarketSession to determine exact session state
		}

		//attribute
		// Session windows in US Eastern; set by configureSessions (override per product, VPF-05).
		protected TimeSpan m_asiaStart, m_asiaEnd, m_euStart, m_euEnd, m_nyStart, m_nyEnd;
		protected TimeSpan m_rthStart, m_rthEnd, m_haltStart, m_haltEnd, m_globexOpen, m_globexClose;
		private TimeZoneInfo m_est;
		private TimeZoneInfo m_chartTz;     // resolved from Bars.Info in StartCalc (VPF-01)
		private bool m_chartIsEastern;      // chart zone has Eastern's rules: no conversion needed
		private DateTime m_cacheBarTime;    // VPF-10 one-entry cache, keyed on bar time
		private DateTime m_cacheEt;
		private GlobexState m_cacheState;
		private MarketSession m_cacheSessions;
		private bool m_cacheValid;

		//interface methods
		public _VolumeProfileFuturesBase(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			configureSessions();

			try
			{
				m_est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
			}
			catch (TimeZoneNotFoundException)
			{
				m_est = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
			}
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_chartTz = resolveChartTz(Bars.Info);
			m_chartIsEastern = m_chartTz.Id == m_est.Id
				|| (m_chartTz.BaseUtcOffset == m_est.BaseUtcOffset && m_chartTz.HasSameRules(m_est));
			m_cacheValid = false;   // zone or session windows may have changed
			m_session = null;       // VPF-2
			m_sessionBar = -1;
			// (no trace here: trace() stamps Bars.Time[0], which is not readable before the first CalcBar)
		}

		/// <summary>
		/// VPF-01. Zone of the bar timestamps from the instrument settings (PR #1 review: no input parameter).
		/// </summary>
		protected override void CalcBar()
		{
			base.CalcBar();
			updateSessionStats();   // VPF-2
		}

		private TimeZoneInfo resolveChartTz(IInstrumentSettings info)
		{
			switch (info.TimeZone)
			{
				case RequestTimeZone.Local:
					return TimeZoneInfo.Local;
				case RequestTimeZone.GMT:
					return TimeZoneInfo.Utc;
				case RequestTimeZone.Exchange:
					string id = exchangeTimeZoneId(info.Exchange);
					if (id == null)
						throw new Exception("Exchange '" + info.Exchange + "' has no known time zone: set the chart to Local time or override exchangeTimeZoneId.");
					return findTz(id);
				default:
					return TimeZoneInfo.Local;
			}
		}

		/// <summary>
		/// Windows time zone id of an exchange's Exchange-time bars, or null when unknown. Matching is on the exchange name as
		/// the data feed reports it (case-insensitive, substring). Override to add feeds/exchanges.
		/// </summary>
		protected virtual string exchangeTimeZoneId(string exchange)
		{
			if (string.IsNullOrWhiteSpace(exchange)) return null;
			string e = exchange.ToUpperInvariant();
			// Chicago: CME Group's CME/CBOT (incl. GLOBEX, ECBOT, CBT) exchange time
			if (e.Contains("CME") || e.Contains("CBOT") || e.Contains("CBT") || e.Contains("GLOBEX") || e.Contains("CBOE")) return "Central Standard Time";
			// New York: NYMEX/COMEX and the US equity exchanges
			if (e.Contains("NYMEX") || e.Contains("COMEX") || e.Contains("NYSE") || e.Contains("NASDAQ") || e.Contains("ARCA")) return "Eastern Standard Time";
			return null;
		}

		private static TimeZoneInfo findTz(string windowsId)
		{
			try { return TimeZoneInfo.FindSystemTimeZoneById(windowsId); }
			catch (TimeZoneNotFoundException) { }
			if (windowsId == "Central Standard Time") return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
			return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
		}

		//methods
		/// <summary>
		/// Set the session windows (US Eastern). The defaults are the CME equity-index hours (ES/NQ); other products
		/// (CL, GC, ZN, 6E, ...) have different RTH hours: override, call base first and change only what differs, e.g.
		/// protected override void configureSessions() { base.configureSessions(); m_rthStart = new TimeSpan(9, 0, 0); m_rthEnd = new TimeSpan(14, 30, 0); }
		/// Called from Create (the derived object is fully constructed by then).
		/// </summary>
		protected virtual void configureSessions()
		{
			m_asiaStart = DefaultAsiaStart;
			m_asiaEnd = DefaultAsiaEnd;
			m_euStart = DefaultEuropeStart;
			m_euEnd = DefaultEuropeEnd;
			m_nyStart = DefaultNewYorkStart;
			m_nyEnd = DefaultNewYorkEnd;
			m_rthStart = DefaultRthStart;
			m_rthEnd = DefaultRthEnd;
			m_haltStart = DefaultHaltStart;
			m_haltEnd = DefaultHaltEnd;
			m_globexOpen = DefaultGlobexOpen;
			m_globexClose = DefaultGlobexClose;
		}

		/// <summary>
		/// Bar time (bar time zone from Bars.Info, see resolveChartTz) converted to US Eastern (DST-aware). VPF-01/03.
		/// Pass bar times only; never pass a value that is already Eastern (use the *Et cores for those, VPF-02).
		/// </summary>
		protected DateTime ToEastern(DateTime barTime)
		{
			if (barTime.Kind == DateTimeKind.Utc)
				return TimeZoneInfo.ConvertTimeFromUtc(barTime, m_est);

			// Bars.Time carries no zone: interpret it in the configured chart zone.
			DateTime t = DateTime.SpecifyKind(barTime, DateTimeKind.Unspecified);   // ConvertTime rejects Kind=Local with a non-Local source
			if (m_chartIsEastern) return t;
			TimeZoneInfo src = m_chartTz ?? TimeZoneInfo.Local;                     // before StartCalc: legacy behaviour (PC local)
			if (src.IsInvalidTime(t)) t = t.AddHours(1);                            // VPF-03: spring-forward gap, ConvertTime would throw
			return TimeZoneInfo.ConvertTime(t, src, m_est);
		}

		// ---- Eastern-time cores (VPF-02): input is already ET, never convert here ----
		private GlobexState globexStateEt(DateTime et)
		{
			var t = et.TimeOfDay;
			var d = et.DayOfWeek;

			// VPF-04: CME holiday hooks, keyed on the Globex date so the evening before a closure is closed too.
			DateTime gx = globexDateEt(et);
			if (IsGlobexClosedDay(gx))
				return GlobexState.Closed;
			TimeSpan? holidayHalt = HolidayHaltEt(gx);
			if (holidayHalt.HasValue && et.Date == gx.Date && t >= holidayHalt.Value)
				return GlobexState.Halt;   // until the 18:00 reopen, which belongs to the next Globex date

			// Weekend: Fri after close through Sun before reopen
			if (d == DayOfWeek.Saturday)
				return GlobexState.Closed;
			if (d == DayOfWeek.Sunday && t < m_globexOpen)
				return GlobexState.Closed;
			if (d == DayOfWeek.Friday && t >= m_globexClose)
				return GlobexState.Closed;

			// Daily halt Mon–Thu 17:00–18:00 ET
			if (d != DayOfWeek.Sunday && d != DayOfWeek.Friday
					&& InWindow(t, m_haltStart, m_haltEnd, wraps: false))
				return GlobexState.Halt;

			// Friday 17:00 is already Closed above
			return GlobexState.Open;
		}

		private static readonly MarketSession[] s_singleSessions =
		{
			MarketSession.Asia, MarketSession.Europe, MarketSession.NewYork, MarketSession.Rth, MarketSession.Premarket, MarketSession.PostMarket
		};

		private MarketSession sessionsEt(DateTime et)
		{
			if (globexStateEt(et) != GlobexState.Open)
				return MarketSession.None;

			// VPF-08: every single-flag session uses its TryWindow window, so GetSessions and TrySessionBounds agree.
			var t = et.TimeOfDay;
			var s = MarketSession.None;
			for (int i = 0; i < s_singleSessions.Length; i++)
			{
				TimeSpan start, end;
				bool wraps;
				if (TryWindow(s_singleSessions[i], out start, out end, out wraps) && InWindow(t, start, end, wraps))
					s |= s_singleSessions[i];
			}

			// VPF-04: no cash session on NYSE holidays; early RTH close where configured.
			DateTime gx = globexDateEt(et);
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

		private DateTime globexDateEt(DateTime et)
		{
			if (et.TimeOfDay >= m_globexOpen)
				return et.Date.AddDays(1);
			return et.Date;
		}

		// ---- One-entry per-bar cache (VPF-10): IOG ticks and repeated InSession calls share one conversion ----
		private void ensureBarCache(DateTime barTime)
		{
			if (m_cacheValid && barTime == m_cacheBarTime) return;
			m_cacheBarTime = barTime;
			m_cacheEt = ToEastern(barTime);
			m_cacheState = globexStateEt(m_cacheEt);
			m_cacheSessions = sessionsEt(m_cacheEt);
			m_cacheValid = true;
		}

		/// <summary>Current bar time in US Eastern (cached per bar time).</summary>
		protected DateTime EasternNow()
		{
			ensureBarCache(Bars.Time[0]);
			return m_cacheEt;
		}

		protected GlobexState GetGlobexState()
		{
			return GetGlobexState(Bars.Time[0]);
		}

		/// <param name="barTime">Bar time in the chart time zone (not Eastern).</param>
		protected GlobexState GetGlobexState(DateTime barTime)
		{
			ensureBarCache(barTime);
			return m_cacheState;
		}

		/// <summary>
		/// All regional sessions that contain this bar. Overlaps are expected.
		/// Example: 10:05 AM ET → Europe | NewYork | Rth
		/// </summary>
		protected MarketSession GetSessions()
		{
			return GetSessions(Bars.Time[0]);
		}

		/// <param name="barTime">Bar time in the chart time zone (not Eastern).</param>
		protected MarketSession GetSessions(DateTime barTime)
		{
			ensureBarCache(barTime);
			return m_cacheSessions;
		}

		protected bool InSession(MarketSession session)
		{
			return (GetSessions() & session) == session;
		}

		protected bool InAny(MarketSession sessions)
		{
			return (GetSessions() & sessions) != MarketSession.None;
		}

		protected bool IsRth() { return InSession(MarketSession.Rth); }
		protected bool IsAsia() { return InSession(MarketSession.Asia); }
		protected bool IsEurope() { return InSession(MarketSession.Europe); }
		protected bool IsNewYork() { return InSession(MarketSession.NewYork); }

		protected bool IsLondonNyOverlap()
		{
			var s = GetSessions();
			return (s & MarketSession.Europe) != 0
					&& (s & MarketSession.NewYork) != 0;
		}

		/// <summary>
		/// CME Globex trading date: a bar at Sunday 18:30 ET belongs to Monday.
		/// Use this to reset daily VP / IB / "one trade per day" state.
		/// </summary>
		/// <param name="barTime">Bar time in the chart time zone (not Eastern).</param>
		protected DateTime GlobexDate(DateTime barTime)
		{
			if (m_cacheValid && barTime == m_cacheBarTime) return globexDateEt(m_cacheEt);
			return globexDateEt(ToEastern(barTime));
		}

		/// <summary>
		/// True on the first bar of a Globex trading day (VPF-06: only the chart's very first bar has no predecessor;
		/// Bars.CurrentBar is MaxBarsBack-relative, so the old CurrentBar &lt; 2 test fired on the first calculated bar).
		/// </summary>
		protected bool IsNewGlobexDay()
		{
			if (Bars.FullSymbolData.Current <= 1) return true;
			return GlobexDate(Bars.Time[0]) != GlobexDate(Bars.Time[1]);
		}

		/// <summary>
		/// Check whether a specific time is within a given window [start, end).
		/// </summary>
		/// <param name="wraps">
		/// True for windows that cross midnight (Asia 18:00→03:00).
		/// </param>
		private static bool InWindow(TimeSpan t, TimeSpan start, TimeSpan end, bool wraps)
		{
			if (!wraps)
				return t >= start && t < end;

			// e.g. 18:00 → 03:00
			return t >= start || t < end;
		}

		/// <summary>
		/// Single label when you must pick one. Priority: RTH > Premarket > PostMarket > NewYork > Europe > Asia
		/// (VPF-07: Premarket/PostMarket are subsets of NewYork and were unreachable). With the default windows NewYork
		/// is only returned on a no-RTH holiday session. Overlaps still exist; use GetSessions when the overlap matters.
		/// </summary>
		public MarketSession PrimarySession()
		{
			return PrimarySession(EasternNow());
		}

		/// <param name="eastern">Time in US Eastern (already converted; see ToEastern).</param>
		public MarketSession PrimarySession(DateTime eastern)
		{
			MarketSession s = sessionsEt(eastern);   // VPF-02: no second conversion
			if ((s & MarketSession.Rth) != 0) return MarketSession.Rth;
			if ((s & MarketSession.Premarket) != 0) return MarketSession.Premarket;
			if ((s & MarketSession.PostMarket) != 0) return MarketSession.PostMarket;
			if ((s & MarketSession.NewYork) != 0) return MarketSession.NewYork;
			if ((s & MarketSession.Europe) != 0) return MarketSession.Europe;
			if ((s & MarketSession.Asia) != 0) return MarketSession.Asia;
			return MarketSession.None;
		}

		/// <summary>As <see cref="TimeSinceOpen(DateTime, MarketSession)"/> for the current bar.</summary>
		public TimeSpan TimeSinceOpen(MarketSession session)
		{
			return TimeSinceOpen(EasternNow(), session);
		}

		/// <summary>
		/// Elapsed time since this session's open for the Globex day that contains <paramref name="eastern"/>.
		/// Returns TimeSpan.MinValue if that session is not active.
		/// </summary>
		/// <param name="eastern">Time in US Eastern (already converted; see ToEastern).</param>
		/// <param name="session">A single session flag.</param>
		public TimeSpan TimeSinceOpen(DateTime eastern, MarketSession session)
		{
			DateTime open;
			DateTime close;
			if (!TrySessionBounds(eastern, session, out open, out close))
				return TimeSpan.MinValue;
			if (eastern < open || eastern >= close)
				return TimeSpan.MinValue;
			return eastern - open;
		}

		public int MinutesSinceOpen(MarketSession session)
		{
			return MinutesSinceOpen(EasternNow(), session);
		}

		public int MinutesSinceOpen(DateTime eastern, MarketSession session)
		{
			TimeSpan elapsed = TimeSinceOpen(eastern, session);
			if (elapsed == TimeSpan.MinValue)
				return -1;
			return (int)elapsed.TotalMinutes;
		}

		public TimeSpan TimeUntilClose(MarketSession session)
		{
			return TimeUntilClose(EasternNow(), session);
		}

		public TimeSpan TimeUntilClose(DateTime eastern, MarketSession session)
		{
			DateTime open;
			DateTime close;
			if (!TrySessionBounds(eastern, session, out open, out close))
				return TimeSpan.MinValue;
			if (eastern < open || eastern >= close)
				return TimeSpan.MinValue;
			return close - eastern;
		}

		public int MinutesUntilClose(MarketSession session)
		{
			return MinutesUntilClose(EasternNow(), session);
		}

		public int MinutesUntilClose(DateTime eastern, MarketSession session)
		{
			TimeSpan left = TimeUntilClose(eastern, session);
			if (left == TimeSpan.MinValue)
				return -1;
			return (int)left.TotalMinutes;
		}

		public TimeSpan SessionLength(MarketSession session)
		{
			TimeSpan start;
			TimeSpan end;
			bool wraps;
			if (!TryWindow(session, out start, out end, out wraps))
				return TimeSpan.Zero;

			if (!wraps)
				return end - start;
			return TimeSpan.FromDays(1) - start + end;
		}

		/// <summary>
		/// True while we are inside the first <paramref name="minutes"/> of an active session.
		/// Use this to refuse a failed-auction entry before acceptance has had time to exist.
		/// </summary>
		public bool IsOpeningRange(MarketSession session, int minutes)
		{
			return IsOpeningRange(EasternNow(), session, minutes);
		}

		public bool IsOpeningRange(DateTime eastern, MarketSession session, int minutes)
		{
			int elapsed = MinutesSinceOpen(eastern, session);
			return elapsed >= 0 && elapsed < minutes;
		}

		/// <summary>True once at least <paramref name="ibMinutes"/> minutes of RTH have elapsed (after the initial balance). False outside RTH.</summary>
		public bool IsRthAfterInitialBalance(int ibMinutes)
		{
			return IsRthAfterInitialBalance(EasternNow(), ibMinutes);
		}

		public bool IsRthAfterInitialBalance(DateTime eastern, int ibMinutes)
		{
			int elapsed = MinutesSinceOpen(eastern, MarketSession.Rth);
			return elapsed >= ibMinutes;
		}

		/// <summary>
		/// Open/close DateTime for the requested session on the Globex day that contains <paramref name="eastern"/>.
		/// Returns false for Halt/None or unknown flags.
		/// </summary>
		public bool TrySessionBounds(
				DateTime eastern,
				MarketSession session,
				out DateTime open,
				out DateTime close)
		{
			open = DateTime.MinValue;
			close = DateTime.MinValue;

			TimeSpan start;
			TimeSpan end;
			bool wraps;
			if (!TryWindow(session, out start, out end, out wraps))
				return false;

			DateTime gxDate = globexDateEt(eastern);   // VPF-02: input is already Eastern
			// GlobexDate is the *calendar date the session is labeled*.
			// Open at 18:00 lives on gxDate - 1.
			DateTime gxStartCalendar = gxDate.AddDays(-1);

			if (wraps)
			{
				open = gxStartCalendar + start;
				close = gxDate + end;
				return true;
			}

			// Non-wrapping windows live on the calendar date of the Globex day,
			// except Sunday evening pieces which only apply to wrapping Asia/Globex.
			open = gxDate + start;
			close = gxDate + end;
			if (session == MarketSession.Rth)
			{
				TimeSpan? early = RthEarlyCloseEt(gxDate);   // VPF-04: MinutesUntilClose(Rth) honours an early close
				if (early.HasValue && close > gxDate + early.Value) close = gxDate + early.Value;
			}
			return true;
		}

		/// <summary>
		/// Trading-day key for a bar. Equities: calendar date. Futures override with GlobexDate.
		/// Used in _CustomSignal.CalcBar to determine when to start recalculating the intraday values.
		/// </summary>
		protected override DateTime tradingDayKey(DateTime barTime) { return GlobexDate(barTime); }

		private bool TryWindow(
				MarketSession session,
				out TimeSpan start,
				out TimeSpan end,
				out bool wraps)
		{
			start = TimeSpan.Zero;
			end = TimeSpan.Zero;
			wraps = false;

			// Single-flag only. Combined flags are not a window.
			if (session == MarketSession.None || !IsSingleFlag(session))
				return false;

			switch (session)
			{
				case MarketSession.Asia:
					start = m_asiaStart;
					end = m_asiaEnd;
					wraps = true;
					return true;
				case MarketSession.Europe:
					start = m_euStart;
					end = m_euEnd;
					return true;
				case MarketSession.NewYork:
					start = m_nyStart;
					end = m_nyEnd;
					return true;
				case MarketSession.Rth:
					start = m_rthStart;
					end = m_rthEnd;
					return true;
				case MarketSession.Premarket:
					start = m_nyStart;	// NY-session before regular trading hours
					end = m_rthStart;
					return true;
				case MarketSession.PostMarket:
					start = m_rthEnd;		// NY-session after regular trading hours
					end = m_nyEnd;
					return true;
				default:
					return false;
			}
		}

		// ---- VPF-2: session statistics (RTH / ETH / IB) per Globex day ----

		/// <summary>
		/// VPF-2. Statistics of one Globex trading day, updated from closed Data1 bars only (no look-ahead; an intrabar tick
		/// never changes them). Bars are close-stamped: a bar is RTH when its ET close time is in (RthStart, RthValueEnd]
		/// (value window, D5: 16:00, or the early RTH close), ETH (overnight) when it closes at or before RthStart, and
		/// the IB is the RTH bars closing in (RthStart, RthStart + IbMinutes]. 0 means "not seen yet".
		/// </summary>
		public sealed class SessionStats
		{
			public DateTime globexDate;
			public double globexHigh, globexLow, globexVolume;
			public double ethHigh, ethLow, ethVolume;          // overnight: Globex open to the RTH open
			public double rthOpen, rthHigh, rthLow, rthClose, rthVolume;
			public double ibHigh, ibLow;
			public bool ibComplete;                           // true from the first closed bar after the IB end
			public bool rthComplete;                          // true from the first closed bar after the RTH value end
			public double priorRthHigh, priorRthLow, priorRthClose;
			public double priorGlobexHigh, priorGlobexLow;
			public double IbRange { get { return ibComplete ? ibHigh - ibLow : 0; } }
			/// <summary>RTH high above the IB high in IB ranges (0 until the IB is complete).</summary>
			public double IbExtensionUp { get { double r = IbRange; return r > 0 && rthHigh > ibHigh ? (rthHigh - ibHigh) / r : 0; } }
			/// <summary>IB low minus the RTH low in IB ranges (0 until the IB is complete).</summary>
			public double IbExtensionDown { get { double r = IbRange; return r > 0 && rthLow > 0 && rthLow < ibLow ? (ibLow - rthLow) / r : 0; } }
			/// <summary>RTH open minus the prior RTH close (0 when either is unknown).</summary>
			public double Gap { get { return rthOpen > 0 && priorRthClose > 0 ? rthOpen - priorRthClose : 0; } }
		}

		/// <summary>VPF-2. Initial balance length in minutes from RthStart. >= 1. Default 60. Not an [Input].</summary>
		protected int IbMinutes { get; set; } = 60;
		/// <summary>VPF-2 (D5). End of RTH for value logic (profiles, IB/RTH stats): 16:00 ET. m_rthEnd (16:15) stays the session flag.</summary>
		protected TimeSpan m_rthValueEnd = DefaultRthValueEnd;
		public static readonly TimeSpan DefaultRthValueEnd = new TimeSpan(16, 0, 0);

		private SessionStats m_session;
		private int m_sessionBar = -1;

		/// <summary>VPF-2. Statistics of the Globex day of the last closed Data1 bar (null before the first closed bar).</summary>
		public SessionStats Session { get { return m_session; } }

		/// <summary>RTH value-window end for a Globex date: the early RTH close when there is one, else m_rthValueEnd.</summary>
		protected TimeSpan RthValueEndEt(DateTime globexDate)
		{
			TimeSpan? early = RthEarlyCloseEt(globexDate);
			return early.HasValue && early.Value < m_rthValueEnd ? early.Value : m_rthValueEnd;
		}

		/// <summary>VPF-2/3: the close-stamped bar at ET time <paramref name="et"/> belongs to the RTH value window.</summary>
		protected bool IsRthValueBarEt(DateTime et)
		{
			DateTime gx = globexDateEt(et);
			if (et.Date != gx.Date || IsGlobexClosedDay(gx) || IsNoRthDay(gx)) return false;
			TimeSpan t = et.TimeOfDay;
			return t > m_rthStart && t <= RthValueEndEt(gx);
		}

		private void updateSessionStats()
		{
			if (Bars.Status != EBarState.Close) return;
			int bar = Bars.FullSymbolData.Current;
			if (bar == m_sessionBar) return;
			m_sessionBar = bar;
			if (IbMinutes < 1) throw new ArgumentException("IbMinutes must be >= 1.");

			DateTime et = ToEastern(Bars.Time[0]);
			DateTime gx = globexDateEt(et);
			if (m_session == null || m_session.globexDate != gx)
			{
				SessionStats prev = m_session;
				m_session = new SessionStats { globexDate = gx };
				if (prev != null)
				{
					bool prevHadRth = prev.rthClose > 0;
					m_session.priorRthHigh = prevHadRth ? prev.rthHigh : prev.priorRthHigh;
					m_session.priorRthLow = prevHadRth ? prev.rthLow : prev.priorRthLow;
					m_session.priorRthClose = prevHadRth ? prev.rthClose : prev.priorRthClose;
					m_session.priorGlobexHigh = prev.globexHigh;
					m_session.priorGlobexLow = prev.globexLow;
				}
			}
			SessionStats s = m_session;
			double h = Bars.High[0], l = Bars.Low[0], c = Bars.Close[0], o = Bars.Open[0], v = Bars.Volume[0];
			if (s.globexHigh == 0 || h > s.globexHigh) s.globexHigh = h;
			if (s.globexLow == 0 || l < s.globexLow) s.globexLow = l;
			s.globexVolume += v;

			TimeSpan t = et.TimeOfDay;
			bool sameCalendarDay = et.Date == gx.Date;
			bool rth = IsRthValueBarEt(et);
			TimeSpan ibEnd = m_rthStart + TimeSpan.FromMinutes(IbMinutes);
			if (rth)
			{
				if (s.rthOpen == 0) s.rthOpen = o;
				if (s.rthHigh == 0 || h > s.rthHigh) s.rthHigh = h;
				if (s.rthLow == 0 || l < s.rthLow) s.rthLow = l;
				s.rthClose = c;
				s.rthVolume += v;
				if (t <= ibEnd)
				{
					if (s.ibHigh == 0 || h > s.ibHigh) s.ibHigh = h;
					if (s.ibLow == 0 || l < s.ibLow) s.ibLow = l;
				}
			}
			else if (!sameCalendarDay || t <= m_rthStart)
			{
				if (s.ethHigh == 0 || h > s.ethHigh) s.ethHigh = h;
				if (s.ethLow == 0 || l < s.ethLow) s.ethLow = l;
				s.ethVolume += v;
			}
			if (sameCalendarDay && t >= ibEnd && s.ibHigh > 0) s.ibComplete = true;
			if (sameCalendarDay && t >= RthValueEndEt(gx) && s.rthClose > 0) s.rthComplete = true;
		}

		// ---- VPF-3: RTH-only and Globex-day Own profiles (VPB-1 hooks) ----

		/// <summary>VPF-3: window of the VPB-1 Own profile on a futures chart.</summary>
		public enum EOwnProfileWindow { MultiChartsProfile = 0, GlobexDay = 1, Rth = 2 }

		/// <summary>
		/// VPF-3. MultiChartsProfile (default, VPB behaviour: the MC profile boundaries), GlobexDay (18:00 → 17:00 ET) or Rth
		/// (Globex day, RTH value-window bars only, 09:30 → 16:00 ET). Set in Create; needs DevelopingSource = Own. Not an [Input].
		/// </summary>
		protected EOwnProfileWindow OwnProfileWindow { get; set; } = EOwnProfileWindow.MultiChartsProfile;

		protected override bool isNewProfileWindow(int stream, int barIndex, DateTime barTime, DateTime prevBarTime)
		{
			if (OwnProfileWindow == EOwnProfileWindow.MultiChartsProfile)
				return base.isNewProfileWindow(stream, barIndex, barTime, prevBarTime);
			if (prevBarTime == DateTime.MinValue) return true;
			return GlobexDate(barTime) != GlobexDate(prevBarTime);
		}

		protected override bool includeBarInProfile(DateTime barTime)
		{
			if (OwnProfileWindow != EOwnProfileWindow.Rth) return base.includeBarInProfile(barTime);
			return IsRthValueBarEt(ToEastern(barTime));
		}

		/// <summary>VPF-3: developing RTH Own profile (OwnProfileWindow = Rth and DevelopingSource = Own), else null.</summary>
		public VpExtra RthProfile()
		{
			return OwnProfileWindow == EOwnProfileWindow.Rth && DevelopingSource == EDevelopingSource.Own ? DevelopingExtra(DevProfileDataStream) : null;
		}

		/// <summary>VPF-3: developing Globex-day Own profile (OwnProfileWindow = GlobexDay and DevelopingSource = Own), else null.</summary>
		public VpExtra GlobexProfile()
		{
			return OwnProfileWindow == EOwnProfileWindow.GlobexDay && DevelopingSource == EDevelopingSource.Own ? DevelopingExtra(DevProfileDataStream) : null;
		}

		/// <summary>VPF-3: last completed RTH Own profile, or null.</summary>
		public VpExtra PriorRthProfile()
		{
			if (OwnProfileWindow != EOwnProfileWindow.Rth) return null;
			List<VpExtra> done = OwnCompletedProfiles(1);
			return done.Count > 0 ? done[0] : null;
		}

		// ---- VPF-04 / VPF-6: CME holiday hooks (Globex trading date, ET). ----

		/// <summary>
		/// Spec VPF-6: when true (default) the holiday hooks below use the CME Globex equity-index calendar
		/// (_CmeHolidays.Rules: closures incl. Good Friday, 13:00 ET holiday halts, 13:15 ET early closes, abbreviated
		/// sessions). Set false in a derived strategy's Create/StartCalc for the pre-table behaviour (New Year / Christmas
		/// closures only, with the Sunday → Monday observance fix). Products with other holiday hours (CL, GC, ZN, ...)
		/// should override the hooks. Not an [Input].
		/// </summary>
		protected bool UseCmeHolidayTable { get; set; } = true;

		/// <summary>
		/// CME Globex fully closed for this Globex trading date (the evening session that would open the day before belongs
		/// to the closed date and is closed too). With UseCmeHolidayTable: New Year, Christmas and Good Friday (except
		/// abbreviated years), weekend-observed. Without: New Year and Christmas only, Sunday → observed Monday (VPF-6 bug
		/// fix: Sun 25 Dec 2022 → closed Mon 26 Dec; Sun 1 Jan 2023 → Mon 2 Jan); a Saturday holiday closes nothing.
		/// </summary>
		protected virtual bool IsGlobexClosedDay(DateTime globexDate)
		{
			DateTime d = globexDate.Date;
			if (UseCmeHolidayTable) return _CmeHolidays.Rules.Get(d).Closed;
			if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) return false;
			DateTime ny = new DateTime(d.Year, 1, 1), xmas = new DateTime(d.Year, 12, 25);
			if (ny.DayOfWeek != DayOfWeek.Saturday && d == _CmeHolidays.Rules.SundayToMonday(ny)) return true;
			if (xmas.DayOfWeek != DayOfWeek.Saturday && d == _CmeHolidays.Rules.SundayToMonday(xmas)) return true;
			return false;
		}

		/// <summary>
		/// No cash (RTH) session on this Globex trading date: NYSE holidays (Holidays, the _NyseHolidays calendar) that are not
		/// full Globex closures. Rth/Premarket/PostMarket are masked off; Globex itself stays Open (see HolidayHaltEt).
		/// </summary>
		protected virtual bool IsNoRthDay(DateTime globexDate)
		{
			return Holidays != null && Holidays.isHoliday(globexDate.Date) && !IsGlobexClosedDay(globexDate);
		}

		/// <summary>
		/// Early Globex halt (ET) for this Globex trading date, or null. With UseCmeHolidayTable: 13:00 ET on holiday
		/// sessions, 13:15 ET on early-close days, 09:15 / 09:30 ET on abbreviated Good Friday / mourning sessions.
		/// Without the table: null (pre-VPF-6 behaviour).
		/// </summary>
		protected virtual TimeSpan? HolidayHaltEt(DateTime globexDate)
		{
			return UseCmeHolidayTable ? _CmeHolidays.Rules.Get(globexDate).HaltEt : null;
		}

		/// <summary>
		/// Early RTH close (ET) for this Globex trading date, or null. With UseCmeHolidayTable: 13:15 ET on the day after
		/// Thanksgiving and on Dec 24 / Jul 3 when they fall Mon-Thu. Without the table: null.
		/// </summary>
		protected virtual TimeSpan? RthEarlyCloseEt(DateTime globexDate)
		{
			return UseCmeHolidayTable ? _CmeHolidays.Rules.Get(globexDate).RthCloseEt : null;
		}

		/// <summary>VPF-6: tradingDayCount on a futures chart skips full CME Globex closures (not NYSE-only holidays).</summary>
		protected override bool isTradingHoliday(DateTime date)
		{
			return _CmeHolidays.Rules.Get(date).Closed;
		}

		private static bool IsSingleFlag(MarketSession session)
		{
			int v = (int)session;
			return v != 0 && (v & (v - 1)) == 0;
		}
	}
}