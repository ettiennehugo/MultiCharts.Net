using ATCenterProxy.interop;
using Microsoft.CodeAnalysis.CSharp;
using PowerLanguage.Function;
using System;
using System.Drawing;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Volume profile base class for futures trading. Provides common mechanisms that can be used in signals that trade
	/// volume profiles and VWAP in the futures market.
	/// 
	/// Chart Setup:
	/// 1. QuoteManager symbol timezone = Exchange (CME is Chicago).
	/// 2. Chart Format Instrument → Time Zone: pick Exchange or Eastern and stay there.
	/// 3. If the chart is Local and your PC is already Atlanta/Eastern, ToEastern is a no-op. If you ever run MC on a VPS in another zone, the conversion saves you.
	/// 
	/// General Notes:
	/// * Allow entries only when InSession(MarketSession.Rth) - high liquidity confirms moves.
	/// * Stand down in Asia unless you are explicitly studying overnight inventory.
	/// * Treat IsLondonNyOverlap() as a quality filter, not a requirement, until you have stats.
	/// * Reset developing value area on IsNewGlobexDay(), not at midnight.
	/// * Do not send orders when GetGlobexState() != Open.
	/// </summary>
	public class _VolumeProfileFuturesBase : _VolumeProfileBase
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

		//properties


		//attribute
		private TimeSpan m_asiaStart, m_asiaEnd, m_euStart, m_euEnd, m_nyStart, m_nyEnd;
		private TimeSpan m_rthStart, m_rthEnd, m_haltStart, m_haltEnd, m_globexOpen, m_globexClose;
		private TimeZoneInfo m_est;

		//interface methods
		public _VolumeProfileFuturesBase(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			m_asiaStart = TimeSpan.Parse(DEFAULT_ASIA_START);
			m_asiaEnd = TimeSpan.Parse(DEFAULT_ASIA_END);
			m_euStart = TimeSpan.Parse(DEFAULT_EUROPE_START);
			m_euEnd = TimeSpan.Parse(DEFAULT_EUROPE_END);
			m_nyStart = TimeSpan.Parse(DEFAULT_NEWYORK_START);
			m_nyEnd = TimeSpan.Parse(DEFAULT_NEWYORK_END);
			m_rthStart = TimeSpan.Parse(DEFAULT_RTH_START);
			m_rthEnd = TimeSpan.Parse(DEFAULT_RTH_END);
			m_haltStart = TimeSpan.Parse(DEFAULT_HALT_START);
			m_haltEnd = TimeSpan.Parse(DEFAULT_HALT_END);
			m_globexOpen = TimeSpan.Parse(DEFAULT_GLOBEX_OPEN);
			m_globexClose = TimeSpan.Parse(DEFAULT_GLOBEX_CLOSE);

			try
			{
				m_est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
			}
			catch (TimeZoneNotFoundException)
			{
				m_est = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
			}
		}

		//methods
		/// <summary>
		/// Bar time converted to US Eastern (DST-aware).
		/// </summary>
		protected DateTime ToEastern(DateTime barTime)
		{
			// Bars.Time is unspecified. Treat it as the chart TZ.
			// Safest default: chart set to Exchange or Eastern.
			if (barTime.Kind == DateTimeKind.Utc)
				return TimeZoneInfo.ConvertTimeFromUtc(barTime, m_est);

			return TimeZoneInfo.ConvertTime(barTime, TimeZoneInfo.Local, m_est);
		}

		protected GlobexState GetGlobexState()
		{
			return GetGlobexState(Bars.Time[0]);
		}

		protected GlobexState GetGlobexState(DateTime barTime)
		{
			var est = ToEastern(barTime);
			var t = est.TimeOfDay;
			var d = est.DayOfWeek;

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

		/// <summary>
		/// All regional sessions that contain this bar. Overlaps are expected.
		/// Example: 10:05 AM ET → Europe | NewYork | Rth
		/// </summary>
		protected MarketSession GetSessions()
		{
			return GetSessions(Bars.Time[0]);
		}

		protected MarketSession GetSessions(DateTime barTime)
		{
			if (GetGlobexState(barTime) != GlobexState.Open)
				return MarketSession.None;

			var t = ToEastern(barTime).TimeOfDay;
			var s = MarketSession.None;

			if (InWindow(t, m_asiaStart, m_asiaEnd, wraps: true)) s |= MarketSession.Asia;
			if (InWindow(t, m_euStart, m_euEnd, wraps: false)) s |= MarketSession.Europe;
			if (InWindow(t, m_nyStart, m_nyEnd, wraps: false)) s |= MarketSession.NewYork;
			if (InWindow(t, m_rthStart, m_rthEnd, wraps: false)) s |= MarketSession.Rth;

			var preEnd = m_rthStart;
			if (InWindow(t, m_nyStart, preEnd, wraps: false) && t < m_rthStart)
				s |= MarketSession.Premarket;

			if (t >= m_rthEnd && t < m_haltStart)
				s |= MarketSession.PostMarket;

			return s;
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
		protected DateTime GlobexDate(DateTime barTime)
		{
			var et = ToEastern(barTime);
			if (et.TimeOfDay >= m_globexOpen)
				return et.Date.AddDays(1);
			return et.Date;
		}

		protected bool IsNewGlobexDay()
		{
			if (Bars.CurrentBar < 2) return true;
			return GlobexDate(Bars.Time[0]) != GlobexDate(Bars.Time[1]);
		}

		/// <summary>
		/// Check whether a specific time it withint a given window.
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
		/// Single label when you must pick one. Priority: RTH > NewYork > Europe > Asia.
		/// Overlaps still exist; use GetSessions when the overlap matters.
		/// </summary>
		public MarketSession PrimarySession()
		{
			return PrimarySession(ToEastern(Bars.Time[0]));
		}

		public MarketSession PrimarySession(DateTime eastern)
		{
			MarketSession s = GetSessions(eastern);
			if ((s & MarketSession.Rth) != 0) return MarketSession.Rth;
			if ((s & MarketSession.NewYork) != 0) return MarketSession.NewYork;
			if ((s & MarketSession.Europe) != 0) return MarketSession.Europe;
			if ((s & MarketSession.Asia) != 0) return MarketSession.Asia;
			if ((s & MarketSession.Premarket) != 0) return MarketSession.Premarket;
			if ((s & MarketSession.PostMarket) != 0) return MarketSession.PostMarket;
			return MarketSession.None;
		}

		/// <summary>
		/// Elapsed time since this session's open for the Globex day that contains <paramref name="eastern"/>.
		/// Returns TimeSpan.MinValue if that session is not active.
		/// <paramref name="eastern"/>
		/// Time zone in est.
		/// </paramref>
		/// </summary>
		public TimeSpan TimeSinceOpen(MarketSession session)
		{
			return TimeSinceOpen(ToEastern(Bars.Time[0]), session);
		}

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
			return MinutesSinceOpen(ToEastern(Bars.Time[0]), session);
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
			return TimeUntilClose(ToEastern(Bars.Time[0]), session);
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
			return MinutesUntilClose(ToEastern(Bars.Time[0]), session);
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
			return IsOpeningRange(ToEastern(Bars.Time[0]), session, minutes);
		}

		public bool IsOpeningRange(DateTime eastern, MarketSession session, int minutes)
		{
			int elapsed = MinutesSinceOpen(eastern, session);
			return elapsed >= 0 && elapsed < minutes;
		}

		/// <summary>First N minutes of RTH.</summary>
		public bool IsRthAfterInitialBalance(int ibMinutes)
		{
			return IsRthAfterInitialBalance(ToEastern(Bars.Time[0]), ibMinutes);
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

			DateTime gxDate = GlobexDate(eastern);
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
			return true;
		}

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

		private static bool IsSingleFlag(MarketSession session)
		{
			int v = (int)session;
			return v != 0 && (v & (v - 1)) == 0;
		}
	}
}