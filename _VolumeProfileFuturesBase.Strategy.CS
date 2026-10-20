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
	/// 4. VPF-4: continuous futures come from the datafeed. Use non-adjusted data for volume-profile levels near a roll
	///    splice (back-adjusted prices move every level before the roll), and set RollDetection to flag profiles / naked
	///    POCs / composites that span a splice (crossesRoll).
	/// 5. Bar times are close times: VPF-2 session statistics and RTH Own profiles count a bar as RTH when its ET close
	///    time is in (RthStart, RTH value end], value end 16:00 ET (D5). InSession(Rth) keeps the 09:30-16:15 session flag.
	///
	/// VPF-8 notes:
	/// * _CustomSignal.Intraday* (High/Low/Open/Close/Volume and times) are Globex-day values on futures charts
	///   (18:00 → 17:00 ET, tradingDayKey = GlobexDate), not RTH values; use Session (VPF-2) for RTH / IB / overnight.
	/// * ToEastern on a Local-zone chart: in the autumn fall-back hour a wall-clock bar time occurs twice and is ambiguous;
	///   TimeZoneInfo.ConvertTime resolves it as standard time, so one hour of bars can map 1h off. Exchange or GMT bar
	///   time avoids this. A spring-forward gap time is moved forward one hour.
	/// * Accessibility: session queries used by derived strategies (GetSessions, InSession, IsRth, GlobexDate, ...) are
	///   protected; the pure clock helpers (PrimarySession, TimeSinceOpen, MinutesSinceOpen, TimeUntilClose, SessionLength,
	///   IsOpeningRange, TrySessionBounds) are public. Left as is to avoid breaking callers; new APIs follow the same split.
	/// 
	/// VPF-7: the session logic (windows, bar zone, per-bar cache, holiday hooks) lives in _CmeSessionClock
	/// (_CmeSessionClock.Function.CS); every session method here is a one-line delegate to SessionClock. A futures strategy
	/// that needs no volume profiles can derive from _CustomSignal and own a _CmeSessionClock (see _CmeSessionClockTest).
	///
	/// VPF-5: order-flow helpers (BarDelta, AnchoredCvd, DeltaZ, Imbalances, StackedImbalances, UnfinishedAuctionAt,
	/// AbsorptionSignal) use an APPROXIMATED aggressor side - MultiCharts .NET exposes no aggressor side. Tick rule on
	/// intrabar ticks (needs IOG), Bars.UpTicks/DownTicks per bar otherwise. Off by default (OrderFlowMode = Off).
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
		// VPF-7: Eastern zone, bar zone, the VPF-10 per-bar cache and the holiday hooks live in the session clock.
		private _CmeSessionClock m_clock;

		//interface methods
		public _VolumeProfileFuturesBase(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			configureSessions();
			m_clock = null;
			clock();   // VPF-7: resolves US Eastern, as before
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			clock().BarTimeZone = resolveBarTimeZone(Bars.Info);   // CS-6: zone resolution lives in _CustomSignal; VPF-7: clears the clock cache
			m_session = null;       // VPF-2
			m_sessionBar = -1;
			m_rollBars.Clear(); m_rollLastDay = DateTime.MinValue; m_rollLastBar = -1; m_rollLastName = null;   // VPF-4
			startOrderFlow();       // VPF-5 (Off by default: nothing is computed)
			// (no trace here: trace() stamps Bars.Time[0], which is not readable before the first CalcBar)
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			updateSessionStats();   // VPF-2
			detectRoll();           // VPF-4
			updateOrderFlow();      // VPF-5 (no-op when OrderFlowMode = Off)
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
		/// VPF-7: the CME session clock behind every session method of this class (windows, bar zone, per-bar cache, holiday
		/// hooks). The m_* session windows are copied into it on every access, so derived code that changes them keeps working;
		/// its hooks call this class's virtual IsGlobexClosedDay / IsNoRthDay / HolidayHaltEt / RthEarlyCloseEt.
		/// A non-VP strategy can own its own _CmeSessionClock instead of deriving from this class.
		/// </summary>
		protected _CmeSessionClock SessionClock { get { return clock(); } }

		private _CmeSessionClock clock()
		{
			_CmeSessionClock c = m_clock;
			if (c == null)
			{
				c = new _CmeSessionClock();
				c.GlobexClosedDayHook = IsGlobexClosedDay;   // virtual: derived overrides are honoured
				c.NoRthDayHook = IsNoRthDay;
				c.HolidayHaltHook = HolidayHaltEt;
				c.RthEarlyCloseHook = RthEarlyCloseEt;
				m_clock = c;
			}
			c.AsiaStart = m_asiaStart; c.AsiaEnd = m_asiaEnd;
			c.EuropeStart = m_euStart; c.EuropeEnd = m_euEnd;
			c.NewYorkStart = m_nyStart; c.NewYorkEnd = m_nyEnd;
			c.RthStart = m_rthStart; c.RthEnd = m_rthEnd; c.RthValueEnd = m_rthValueEnd;
			c.HaltStart = m_haltStart; c.HaltEnd = m_haltEnd;
			c.GlobexOpen = m_globexOpen; c.GlobexClose = m_globexClose;
			c.UseCmeHolidayTable = UseCmeHolidayTable;
			return c;
		}

		/// <summary>
		/// Bar time (bar time zone from Bars.Info, see _CustomSignal.resolveBarTimeZone) converted to US Eastern (DST-aware). VPF-01/03.
		/// Pass bar times only; never pass a value that is already Eastern (use the *Et cores for those, VPF-02).
		/// Before StartCalc the bar zone is this PC's zone (legacy behaviour).
		/// </summary>
		protected DateTime ToEastern(DateTime barTime) { return clock().ToEastern(barTime); }

		// ---- Eastern-time cores (VPF-02): input is already ET, never convert here (VPF-7: in _CmeSessionClock) ----
		private GlobexState globexStateEt(DateTime et) { return (GlobexState)clock().StateEt(et); }
		private MarketSession sessionsEt(DateTime et) { return (MarketSession)clock().SessionsEt(et); }
		private DateTime globexDateEt(DateTime et) { return clock().GlobexDateEt(et); }

		/// <summary>Current bar time in US Eastern (cached per bar time).</summary>
		protected DateTime EasternNow() { return clock().EasternOf(Bars.Time[0]); }

		protected GlobexState GetGlobexState() { return GetGlobexState(Bars.Time[0]); }

		/// <param name="barTime">Bar time in the chart time zone (not Eastern).</param>
		protected GlobexState GetGlobexState(DateTime barTime) { return (GlobexState)clock().State(barTime); }

		/// <summary>
		/// All regional sessions that contain this bar. Overlaps are expected.
		/// Example: 10:05 AM ET → Europe | NewYork | Rth
		/// </summary>
		protected MarketSession GetSessions() { return GetSessions(Bars.Time[0]); }

		/// <param name="barTime">Bar time in the chart time zone (not Eastern).</param>
		protected MarketSession GetSessions(DateTime barTime) { return (MarketSession)clock().Sessions(barTime); }

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
		protected DateTime GlobexDate(DateTime barTime) { return clock().GlobexDate(barTime); }

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
		/// Single label when you must pick one. Priority: RTH > Premarket > PostMarket > NewYork > Europe > Asia
		/// (VPF-07: Premarket/PostMarket are subsets of NewYork and were unreachable). With the default windows NewYork
		/// is only returned on a no-RTH holiday session. Overlaps still exist; use GetSessions when the overlap matters.
		/// </summary>
		public MarketSession PrimarySession()
		{
			return PrimarySession(EasternNow());
		}

		/// <param name="eastern">Time in US Eastern (already converted; see ToEastern).</param>
		public MarketSession PrimarySession(DateTime eastern) { return (MarketSession)clock().PrimarySessionEt(eastern); }   // VPF-02: no second conversion

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
		public TimeSpan TimeSinceOpen(DateTime eastern, MarketSession session) { return clock().TimeSinceOpenEt(eastern, toClock(session)); }

		public int MinutesSinceOpen(MarketSession session)
		{
			return MinutesSinceOpen(EasternNow(), session);
		}

		public int MinutesSinceOpen(DateTime eastern, MarketSession session) { return clock().MinutesSinceOpenEt(eastern, toClock(session)); }

		public TimeSpan TimeUntilClose(MarketSession session)
		{
			return TimeUntilClose(EasternNow(), session);
		}

		public TimeSpan TimeUntilClose(DateTime eastern, MarketSession session) { return clock().TimeUntilCloseEt(eastern, toClock(session)); }

		public int MinutesUntilClose(MarketSession session)
		{
			return MinutesUntilClose(EasternNow(), session);
		}

		public int MinutesUntilClose(DateTime eastern, MarketSession session) { return clock().MinutesUntilCloseEt(eastern, toClock(session)); }

		public TimeSpan SessionLength(MarketSession session) { return clock().SessionLength(toClock(session)); }

		/// <summary>
		/// True while we are inside the first <paramref name="minutes"/> of an active session.
		/// Use this to refuse a failed-auction entry before acceptance has had time to exist.
		/// </summary>
		public bool IsOpeningRange(MarketSession session, int minutes)
		{
			return IsOpeningRange(EasternNow(), session, minutes);
		}

		public bool IsOpeningRange(DateTime eastern, MarketSession session, int minutes) { return clock().IsOpeningRangeEt(eastern, toClock(session), minutes); }

		/// <summary>True once at least <paramref name="ibMinutes"/> minutes of RTH have elapsed (after the initial balance). False outside RTH.</summary>
		public bool IsRthAfterInitialBalance(int ibMinutes)
		{
			return IsRthAfterInitialBalance(EasternNow(), ibMinutes);
		}

		public bool IsRthAfterInitialBalance(DateTime eastern, int ibMinutes) { return clock().IsRthAfterInitialBalanceEt(eastern, ibMinutes); }

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
			return clock().TrySessionBoundsEt(eastern, toClock(session), out open, out close);
		}

		private static _CmeSessionClock.MarketSession toClock(MarketSession session) { return (_CmeSessionClock.MarketSession)session; }

		/// <summary>
		/// Trading-day key for a bar. Equities: calendar date. Futures override with GlobexDate.
		/// Used in _CustomSignal.CalcBar to determine when to start recalculating the intraday values, so on futures the
		/// Intraday* values cover the whole Globex day (VPF-8), and CS-7 daily counters roll at 18:00 ET.
		/// </summary>
		protected override DateTime tradingDayKey(DateTime barTime) { return GlobexDate(barTime); }

		/// <summary>CS-7: entry windows / FlatBy compare against US Eastern time on futures charts.</summary>
		protected override TimeSpan sessionClock(DateTime barTime) { return ToEastern(barTime).TimeOfDay; }

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
		protected TimeSpan RthValueEndEt(DateTime globexDate) { return clock().RthValueEndEt(globexDate); }

		/// <summary>VPF-2/3: the close-stamped bar at ET time <paramref name="et"/> belongs to the RTH value window.</summary>
		protected bool IsRthValueBarEt(DateTime et) { return clock().IsRthValueBarEt(et); }

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

		/// <summary>VPB-6 (Rev 2.4, D5): with OwnProfileWindow = Rth, value migration uses completed Own RTH (09:30-16:00) profiles.</summary>
		protected override List<VpExtra> migrationProfiles(int stream, int count)
		{
			if (OwnProfileWindow == EOwnProfileWindow.Rth && DevelopingSource == EDevelopingSource.Own && stream == DevProfileDataStream)
			{
				List<VpExtra> own = OwnCompletedProfiles(count);
				own.RemoveAll(e => e.isPartial);
				return own;
			}
			return base.migrationProfiles(stream, count);
		}

		/// <summary>VPF-3: last completed RTH Own profile, or null.</summary>
		public VpExtra PriorRthProfile()
		{
			if (OwnProfileWindow != EOwnProfileWindow.Rth) return null;
			List<VpExtra> done = OwnCompletedProfiles(1);
			return done.Count > 0 ? done[0] : null;
		}

		// ---- VPF-4: roll-splice flag for datafeed continuous futures (Q3) ----

		/// <summary>VPF-4: how rolls are found. Off (default) = never flag.</summary>
		public enum ERollDetection { Off = 0, Series = 1, Table = 2, SeriesOrTable = 3 }

		/// <summary>VPF-4: roll detection mode. Not an [Input].</summary>
		protected ERollDetection RollDetection { get; set; } = ERollDetection.Off;
		/// <summary>
		/// VPF-4 Series: a Globex-day open gapping >= RollGapTicks from the prior close inside the quarterly roll window
		/// (days 1-21 of Mar/Jun/Sep/Dec, the run-up to the third-Friday expiry), or a change of Bars.Info.Name.
		/// </summary>
		protected int RollGapTicks { get; set; } = 8;
		/// <summary>VPF-4 Table: Globex trading dates on which the continuous series switched contract (set by the strategy).</summary>
		protected HashSet<DateTime> RollDates { get; } = new HashSet<DateTime>();

		private readonly List<int> m_rollBars = new List<int>();   // Data1 bar (Status Bar #) of the first bar of each roll day
		private DateTime m_rollLastDay = DateTime.MinValue;
		private int m_rollLastBar = -1;
		private string m_rollLastName;

		/// <summary>VPF-4: Data1 bars (1-based) on which a roll was detected so far.</summary>
		public IList<int> RollBars { get { return m_rollBars.AsReadOnly(); } }

		private void detectRoll()
		{
			if (RollDetection == ERollDetection.Off || Bars.Status != EBarState.Close) return;
			int bar = Bars.FullSymbolData.Current;
			if (bar == m_rollLastBar) return;
			m_rollLastBar = bar;
			string name = Bars.Info.Name;
			DateTime gx = GlobexDate(Bars.Time[0]);
			bool newDay = gx != m_rollLastDay;
			bool roll = m_rollLastName != null && name != m_rollLastName;
			if (newDay && m_rollLastDay != DateTime.MinValue && bar > 1)
			{
				bool series = RollDetection == ERollDetection.Series || RollDetection == ERollDetection.SeriesOrTable;
				bool table = RollDetection == ERollDetection.Table || RollDetection == ERollDetection.SeriesOrTable;
				if (table && RollDates.Contains(gx.Date)) roll = true;
				if (series && gx.Month % 3 == 0 && gx.Day <= 21 && RollGapTicks > 0
					&& Math.Abs(Bars.Open[0] - Bars.Close[1]) >= RollGapTicks * tickSize() - tickSize() * 1e-6) roll = true;
			}
			if (roll && (m_rollBars.Count == 0 || m_rollBars[m_rollBars.Count - 1] != bar))
			{
				m_rollBars.Add(bar);
				if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, $"VPF-4 roll splice detected globexDate({gx:yyyy-MM-dd}) bar({bar}) name({name})");
			}
			m_rollLastName = name;
			m_rollLastDay = gx;
		}

		/// <summary>
		/// VPF-4: true when a detected roll falls after the profile's first bar and at or before the calc bar: the profile spans
		/// the splice, or its levels (e.g. a naked POC) are pre-roll prices being used after the roll. Data1 profiles only.
		/// </summary>
		protected override bool profileCrossesRoll(VpExtra e)
		{
			if (e == null || m_rollBars.Count == 0 || e.dataStream > 1) return false;
			int cur = Bars.FullSymbolData.Current;
			for (int i = 0; i < m_rollBars.Count; i++)
				if (m_rollBars[i] > e.startBar && m_rollBars[i] <= cur) return true;
			return false;
		}

		// ---- VPF-5: order-flow helpers on an APPROXIMATED aggressor side (off by default) ----

		/// <summary>VPF-5: Off (default) = nothing is computed and every helper returns NaN / empty / false / Unknown.</summary>
		public enum EOrderFlowMode { Off = 0, Approximate = 1 }

		/// <summary>VPF-5: where the cumulative delta (AnchoredCvd) restarts. Manual = only AnchorCvd().</summary>
		public enum ECvdAnchor { GlobexDay = 0, RthOpen = 1, Manual = 2 }

		/// <summary>
		/// VPF-5. MultiCharts .NET does not expose the trade aggressor side (also not with dxFeed), so buy/sell volume is an
		/// APPROXIMATION (see _OrderFlow): with Intrabar Order Generation each Data1 tick calc is classified by the tick rule
		/// (uptick = buy, downtick = sell, zero tick inherits the previous side; volume = increase of Bars.Volume[0] since
		/// the previous calc); a bar with no intrabar calc (IOG off, historical bars) falls back to Bars.UpTicks /
		/// Bars.DownTicks (bar delta only, no per-price footprint). Default Off: no behaviour change. Not an [Input].
		/// </summary>
		protected EOrderFlowMode OrderFlowMode { get; set; } = EOrderFlowMode.Off;
		/// <summary>VPF-5: diagonal imbalance ratio, Ask(p) >= ratio x Bid(p - 1 tick). Default 3.0.</summary>
		protected double ImbalanceRatio { get; set; } = 3.0;
		/// <summary>VPF-5: an imbalance row needs at least this approximated volume. Default 1.</summary>
		protected double ImbalanceMinVolume { get; set; } = 1;
		/// <summary>VPF-5: consecutive imbalance rows for a stack. Default 3.</summary>
		protected int StackedMinCount { get; set; } = 3;
		/// <summary>VPF-5 absorption hypothesis: bar volume percentile rank vs the prior AbsorptionLookback bars. Default 0.9.</summary>
		protected double AbsorptionMinVolumeRank { get; set; } = 0.9;
		protected int AbsorptionLookback { get; set; } = 20;
		/// <summary>VPF-5 absorption hypothesis: max close-to-close progress in the aggressor's direction, ticks. Default 1.</summary>
		protected int AbsorptionMaxProgressTicks { get; set; } = 1;
		/// <summary>VPF-5 absorption hypothesis: the bar's max-volume row within this many ticks of the extreme. Default 2.</summary>
		protected int AbsorptionExtremeTicks { get; set; } = 2;
		/// <summary>VPF-5: prior sessions in the same-minute delta z. Default 20.</summary>
		protected int DeltaZSessions { get; set; } = 20;
		protected ECvdAnchor CvdAnchor { get; set; } = ECvdAnchor.GlobexDay;
		/// <summary>VPF-5: closed bars whose footprint is kept. Default 500.</summary>
		protected int FootprintHistory { get; set; } = 500;

		private _OrderFlow.Source m_ofCapability = _OrderFlow.Source.None;
		private bool m_ofLogged, m_ofWarned;
		private _OrderFlow.TickRule m_ofRule;
		private _OrderFlow.Footprint m_ofCur;
		private int m_ofBar = -1, m_ofCalcs, m_ofClosedBar = -1;
		private double m_ofLastVol;
		private readonly List<_OrderFlow.Footprint> m_ofClosed = new List<_OrderFlow.Footprint>();   // oldest first
		private readonly List<double> m_ofVolumes = new List<double>();
		private _OrderFlow.DeltaHistory m_ofHistory;
		private DateTime m_ofLastGx = DateTime.MinValue;
		private bool m_ofRthAnchored;

		/// <summary>
		/// VPF-5: None when OrderFlowMode is Off; TickRule when IOG is on (intrabar ticks are seen; bars without intrabar
		/// calcs still use UpDownTicks); UpDownTicks otherwise. Set in StartCalc, logged once at Info.
		/// </summary>
		public _OrderFlow.Source OrderFlowCapability { get { return m_ofCapability; } }

		private void startOrderFlow()
		{
			m_ofCapability = _OrderFlow.Source.None;
			m_ofLogged = m_ofWarned = false;
			m_ofCur = null; m_ofBar = -1; m_ofCalcs = 0; m_ofClosedBar = -1; m_ofLastVol = 0;
			m_ofClosed.Clear(); m_ofVolumes.Clear();
			m_ofLastGx = DateTime.MinValue; m_ofRthAnchored = false;
			if (OrderFlowMode == EOrderFlowMode.Off) return;
			m_ofRule = new _OrderFlow.TickRule(tickSize());
			m_ofHistory = new _OrderFlow.DeltaHistory(Math.Max(FootprintHistory, 1000));
			m_ofCapability = Environment.IOGEnabled ? _OrderFlow.Source.TickRule : _OrderFlow.Source.UpDownTicks;
		}

		private void updateOrderFlow()
		{
			if (m_ofCapability == _OrderFlow.Source.None) return;
			if (!m_ofLogged)
			{
				m_ofLogged = true;
				if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, $"VPF-5 order flow APPROXIMATED aggressor side: source({m_ofCapability}) " +
					"(tick rule on intrabar ticks, Bars.UpTicks/DownTicks fallback; MultiCharts exposes no aggressor side)");
			}
			int bar = Bars.FullSymbolData.Current;
			if (bar != m_ofBar)
			{
				m_ofBar = bar; m_ofCalcs = 0; m_ofLastVol = 0;
				m_ofCur = new _OrderFlow.Footprint(tickSize()) { Bar = bar, Source = _OrderFlow.Source.TickRule, Partial = Bars.Status != EBarState.Open };
			}
			if (bar == m_ofClosedBar) return;   // a repeated Close calc of the same bar
			double vol = Bars.Volume[0];
			double add = vol - m_ofLastVol;
			m_ofLastVol = vol;
			double price = Bars.Close[0];
			m_ofCur.Add(price, add > 0 ? add : 0, m_ofRule.Classify(price));
			m_ofCalcs++;
			if (Bars.Status != EBarState.Close) return;

			// bar closed
			m_ofClosedBar = bar;
			_OrderFlow.Footprint fp = m_ofCur;
			fp.Open = Bars.Open[0]; fp.High = Bars.High[0]; fp.Low = Bars.Low[0]; fp.Close = Bars.Close[0];
			if (m_ofCalcs < 2) { fp.SetBarTotals(Bars.UpTicks[0], Bars.DownTicks[0]); fp.Partial = false; }   // no intrabar ticks seen
			m_ofClosed.Add(fp);
			if (m_ofClosed.Count > Math.Max(1, FootprintHistory)) m_ofClosed.RemoveAt(0);
			m_ofVolumes.Add(vol);
			if (m_ofVolumes.Count > Math.Max(1, AbsorptionLookback) + 1) m_ofVolumes.RemoveAt(0);

			DateTime et = ToEastern(Bars.Time[0]);
			DateTime gx = globexDateEt(et);
			if (gx != m_ofLastGx) { m_ofLastGx = gx; m_ofRthAnchored = false; if (CvdAnchor == ECvdAnchor.GlobexDay) m_ofHistory.Anchor(bar); }
			if (CvdAnchor == ECvdAnchor.RthOpen && !m_ofRthAnchored && IsRthValueBarEt(et)) { m_ofRthAnchored = true; m_ofHistory.Anchor(bar); }
			m_ofHistory.Add(bar, fp.Delta, gx, (int)et.TimeOfDay.TotalMinutes);
		}

		private bool orderFlowOn()
		{
			if (m_ofCapability != _OrderFlow.Source.None) return true;
			if (!m_ofWarned) { m_ofWarned = true; if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, "VPF-5 order-flow helper called with OrderFlowMode=Off: NaN / empty"); }
			return false;
		}

		/// <summary>VPF-5: approximated footprint, barsAgo 0 = the current bar (developing under IOG), else a closed bar; null if unavailable.</summary>
		public _OrderFlow.Footprint OrderFlowFootprint(int barsAgo = 0)
		{
			if (!orderFlowOn() || barsAgo < 0) return null;
			if (barsAgo == 0 && m_ofCur != null && m_ofBar == Bars.FullSymbolData.Current) return m_ofCur;
			int closedAgo = m_ofClosedBar == Bars.FullSymbolData.Current ? barsAgo : barsAgo - 1;
			int i = m_ofClosed.Count - 1 - closedAgo;
			return i >= 0 && i < m_ofClosed.Count ? m_ofClosed[i] : null;
		}

		/// <summary>VPF-5: approximated bar delta (buy - sell volume), NaN if unavailable.</summary>
		public double BarDelta(int barsAgo = 0)
		{
			_OrderFlow.Footprint fp = OrderFlowFootprint(barsAgo);
			return fp != null ? fp.Delta : double.NaN;
		}

		/// <summary>VPF-5: approximated cumulative delta of closed bars since the CvdAnchor (Globex day / RTH open / AnchorCvd), NaN if unavailable.</summary>
		public double AnchoredCvd() { return orderFlowOn() ? m_ofHistory.AnchoredCvd : double.NaN; }

		/// <summary>VPF-5: restart the anchored cumulative delta at the current bar (named-bar anchor).</summary>
		public void AnchorCvd() { if (orderFlowOn()) m_ofHistory.Anchor(Bars.FullSymbolData.Current); }

		/// <summary>VPF-5: approximated cumulative delta of the closed bars fromBar..current (1-based bar numbers).</summary>
		public double CvdFrom(int fromBar) { return orderFlowOn() ? m_ofHistory.Cvd(fromBar, Bars.FullSymbolData.Current) : double.NaN; }

		/// <summary>VPF-5: z of the last closed bar's delta vs the same ET clock minute of the prior DeltaZSessions sessions (prior sessions only).</summary>
		public double DeltaZ()
		{
			if (!orderFlowOn() || m_ofClosed.Count == 0) return double.NaN;
			_OrderFlow.Footprint fp = m_ofClosed[m_ofClosed.Count - 1];
			DateTime et = ToEastern(Bars.Time[Math.Max(0, Bars.FullSymbolData.Current - fp.Bar)]);
			return m_ofHistory.SameMinuteZ((int)et.TimeOfDay.TotalMinutes, globexDateEt(et), fp.Delta, DeltaZSessions);
		}

		/// <summary>VPF-5: diagonal imbalance prices (ImbalanceRatio, ImbalanceMinVolume). Empty without a tick-rule footprint.</summary>
		public List<double> Imbalances(_OrderFlow.Side side, int barsAgo = 0)
		{
			return _OrderFlow.Imbalances(OrderFlowFootprint(barsAgo), side, ImbalanceRatio, ImbalanceMinVolume);
		}

		/// <summary>VPF-5: stacked imbalances {low, high, count} with at least StackedMinCount rows (default 3).</summary>
		public List<double[]> StackedImbalances(_OrderFlow.Side side, int barsAgo = 0)
		{
			return _OrderFlow.StackedImbalances(OrderFlowFootprint(barsAgo), side, ImbalanceRatio, ImbalanceMinVolume, StackedMinCount);
		}

		/// <summary>VPF-5: unfinished auction at the bar's high / low (approximated buyers and sellers both traded there).</summary>
		public bool UnfinishedAuctionAt(bool high, int barsAgo = 1)
		{
			return _OrderFlow.UnfinishedAuction(OrderFlowFootprint(barsAgo), high);
		}

		/// <summary>VPF-5: absorption HYPOTHESIS on the last closed bar (see _OrderFlow.Absorption). Buy = selling absorbed at the low.</summary>
		public _OrderFlow.Side AbsorptionSignal()
		{
			if (!orderFlowOn() || m_ofClosed.Count < 2 || m_ofVolumes.Count < 2) return _OrderFlow.Side.Unknown;
			_OrderFlow.Footprint fp = m_ofClosed[m_ofClosed.Count - 1];
			_OrderFlow.Footprint prev = m_ofClosed[m_ofClosed.Count - 2];
			if (prev.Bar != fp.Bar - 1) return _OrderFlow.Side.Unknown;
			double cur = m_ofVolumes[m_ofVolumes.Count - 1];
			double rank = _OrderFlow.PercentRank(m_ofVolumes.GetRange(0, m_ofVolumes.Count - 1), cur);
			return _OrderFlow.Absorption(fp, prev.Close, rank, AbsorptionMinVolumeRank, AbsorptionMaxProgressTicks, AbsorptionExtremeTicks);
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
			return _CmeSessionClock.DefaultGlobexClosedDay(globexDate, UseCmeHolidayTable);   // VPF-7: same rule, shared with the clock
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
	}
}