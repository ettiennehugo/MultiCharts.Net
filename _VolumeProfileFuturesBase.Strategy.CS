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
	/// VPF-5 (Rev 2.5): order-flow helpers (Footprint, BarDelta, CumDelta, DiagonalImbalances, StackedImbalances, Absorption)
	/// use an APPROXIMATED aggressor side (MultiCharts .NET exposes none): Quote (Lee-Ready on DataLoader 1-tick Trade/Bid/Ask)
	/// → TickRule → BarUpDown → Unavailable, quality-tagged per bar. Off by default (OrderFlowEnabled = false).
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
			PriceActionConfig.ApplyPreset(EPriceActionPreset.Futures);   // spec §1.8: enabled stays false (still lazy until a derived strategy enables it)
			configureSessions();
			m_clock = null;
			clock();   // VPF-7: resolves US Eastern, as before
		}

		/// <summary>Phase 3 S5: ExtraPriceAction entries get the Futures preset unless they chose their own.</summary>
		protected override void presetExtraPriceAction(PriceActionConfig c) { c.ApplyPreset(EPriceActionPreset.Futures); }

		protected override void StartCalc()
		{
			base.StartCalc();
			clock().BarTimeZone = resolveBarTimeZone(Bars.Info);   // CS-6: zone resolution lives in _CustomSignal; VPF-7: clears the clock cache
			m_session = null;       // VPF-2
			m_sessionBar = -1;
			if (BarLadders.enabled) OrderFlowEnabled = true;   // Phase 3 VP-3: the per-bar ladder reads the flow engine
			startOrderFlow();       // VPF-5 (OrderFlowEnabled = false by default: nothing is computed)
			// (no trace here: trace() stamps Bars.Time[0], which is not readable before the first CalcBar)
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			updateSessionStats();   // VPF-2
			stepRollDetector();     // VPF-4 (detector in _CustomSignal since S4; idempotent per bar)
			updateOrderFlow();      // VPF-5 (no-op when OrderFlowEnabled = false)
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

		/// <summary>M-TOD: minutes since the 18:00 ET Globex open (bar end time in ET).</summary>
		protected override int todMinuteOfSession(DateTime barTime)
		{
			int m = (int)ToEastern(barTime).TimeOfDay.TotalMinutes - (int)m_globexOpen.TotalMinutes;
			return m < 0 ? m + 1440 : m;
		}

		/// <summary>M-TOD: holiday / early-close / no-RTH Globex dates are excluded from the norms.</summary>
		protected override bool todHalfDay(DateTime sessionDate)
		{
			return IsNoRthDay(sessionDate) || HolidayHaltEt(sessionDate) != null || RthEarlyCloseEt(sessionDate) != null;
		}

		/// <summary>M-TOD delta (Q1): the VPF-5 footprint of this closed Data1 bar. Brings the flow engine up to date first (it is
		/// idempotent per tick and per closed bar). BarUpDown / Unavailable are never used for signals: NaN.</summary>
		protected override double todDelta(out int tier)
		{
			tier = -1;
			if (m_flow == null) return double.NaN;
			updateOrderFlow();
			_OrderFlow.FootprintBar fb;
			lock (m_flowLock) fb = m_flow.Closed(0);
			if (fb == null || fb.BarNumber != Bars.FullSymbolData.Current) return double.NaN;
			if (fb.Source == _OrderFlow.FlowSource.BarUpDown || fb.Source == _OrderFlow.FlowSource.Unavailable) return double.NaN;
			tier = (int)fb.Quality;
			return fb.Delta;
		}

		/// <summary>M-AVWAP GapOpen: CME RTH value bars (close-stamped RTH rule, VPF-8).</summary>
		protected override bool isRthBar(DateTime barTime) { return IsRthValueBarEt(ToEastern(barTime)); }

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

		// VPF-4 settings (RollDetection, RollGapTicks, RollDates, RollBars) and the detector moved to _CustomSignal (Phase 3 S4);
		// tradingDayKey (GlobexDate here) gives the detector its trading day, so futures behaviour is unchanged.

		/// <summary>
		/// VPF-4: true when a detected roll falls after the profile's first bar and at or before the calc bar: the profile spans
		/// the splice, or its levels (e.g. a naked POC) are pre-roll prices being used after the roll. Data1 profiles only.
		/// </summary>
		protected override bool profileCrossesRoll(VpExtra e)
		{
			IList<int> rolls = RollBars;
			if (e == null || rolls.Count == 0 || e.dataStream > 1) return false;
			int cur = Bars.FullSymbolData.Current;
			for (int i = 0; i < rolls.Count; i++)
				if (rolls[i] > e.startBar && rolls[i] <= cur) return true;
			return false;
		}

		// ---- VPF-5 (spec Rev 2.5): order-flow helpers, ranked per-bar fallback chain, quality-tagged (off by default) ----

		/// <summary>VPF-5: where trade ticks come from. DataLoader (default): 1-tick Trade (+ Bid/Ask) streams, same path in history
		/// and real time. Iog: Data1 IOG tick calls (trades only, real time; history only with IogMagnifierOneTick). None: BarUpDown only.</summary>
		public enum EFlowTickSource { DataLoader = 0, Iog = 1, None = 2 }

		/// <summary>
		/// VPF-5 (Rev 2.5). Off by default; set true in a derived strategy's Create/StartCalc. Not an [Input].
		/// MultiCharts .NET exposes no trade aggressor side, so the buy/sell split is an APPROXIMATION: Quote (Lee-Ready on 1-tick
		/// Trade/Bid/Ask DataLoader streams, quote age &lt;= FlowCfg.MaxQuoteAgeMs) → TickRule → BarUpDown (Bars.UpTicks/DownTicks,
		/// FlowCfg.BuildVolumeOn = TradeVolume and Up + Down == Volume) → Unavailable. Every value carries a FlowQuality tag;
		/// cumulative values take the worst tag of their bars. Closed bars only. Bars.StatusLine bid/ask is never used.
		/// IOG history without a 1-tick Bar Magnifier is Unavailable. See _OrderFlow.Function.CS.
		/// </summary>
		protected bool OrderFlowEnabled { get; set; } = false;
		/// <summary>VPF-5: MaxQuoteAgeMs (1000), ResetMode (Session|Rth), AllowMixed (false), BuildVolumeOn (Unknown: set TradeVolume
		/// after checking the chart), QuotesExpected (true), ExcludeExtremesTickRule (true).</summary>
		protected _OrderFlow.FlowConfig FlowCfg { get; } = new _OrderFlow.FlowConfig();
		protected EFlowTickSource FlowTickSource { get; set; } = EFlowTickSource.DataLoader;
		/// <summary>VPF-5: the chart runs IOG with Bar Magnifier = 1 tick (cannot be read from code; stated by the strategy). Default false.</summary>
		protected bool IogMagnifierOneTick { get; set; } = false;

		private _OrderFlow.FlowEngine m_flow;
		private readonly object m_flowLock = new object();
		private bool m_flowLoaderStarted, m_flowLogged;
		private readonly List<IDataLoaderResult> m_flowLoads = new List<IDataLoaderResult>();
		private readonly bool[] m_flowHistoryDone = new bool[3];   // Trade, Bid, Ask
		private int m_flowClosedBar = -1, m_flowIogBar = -1;
		private double m_flowIogLastVol;
		private System.Reflection.MemberInfo m_barTime, m_barClose, m_barVolume;
		/// <summary>Spec S1, PROBE P-BA1: the ChartType member name for Volume Delta (the PG names it only in prose). Parsed at
		/// StartCalc of the level request; an unknown name logs one Warning and the McBidAsk tier stays empty.</summary>
		protected string McBidAskChartTypeName { get; set; } = "VolumeDelta";
		private int m_mcLoaded; private DateTime m_mcFirst = DateTime.MaxValue, m_mcLast = DateTime.MinValue;
		private bool m_mcHistoryDone, m_mcLoadedLogged;
		private DateTime m_mcSession = DateTime.MinValue; private int m_mcSessBars, m_mcSessAgree, m_mcSessCover, m_mcSessCmp; private bool m_mcSessFellLogged;

		/// <summary>VPF-5: the engine (null when OrderFlowEnabled is false). Lock-free reads are safe from CalcBar.</summary>
		protected _OrderFlow.FlowEngine Flow { get { return m_flow; } }

		private void startOrderFlow()
		{
			stopOrderFlowLoads();
			m_flow = null; m_flowLoaderStarted = false; m_flowLogged = false;
			m_mcLoaded = 0; m_mcFirst = DateTime.MaxValue; m_mcLast = DateTime.MinValue; m_mcHistoryDone = m_mcLoadedLogged = false;
			m_mcSession = DateTime.MinValue; m_mcSessBars = m_mcSessAgree = m_mcSessCover = m_mcSessCmp = 0; m_mcSessFellLogged = false;
			m_flowClosedBar = -1; m_flowIogBar = -1; m_flowIogLastVol = 0;
			for (int i = 0; i < m_flowHistoryDone.Length; i++) m_flowHistoryDone[i] = false;
			if (!OrderFlowEnabled) return;
			if (FlowTickSource != EFlowTickSource.DataLoader) FlowCfg.QuotesExpected = false;
			m_flow = new _OrderFlow.FlowEngine(tickSize(), FlowCfg);
		}

		protected override void StopCalc()
		{
			stopOrderFlowLoads();
			base.StopCalc();
		}

		private void stopOrderFlowLoads()
		{
			lock (m_flowLock)
			{
				foreach (IDataLoaderResult r in m_flowLoads) { try { DataLoader.EndLoadData(r); } catch { } }
				m_flowLoads.Clear();
			}
		}

		private void startFlowLoader()
		{
			m_flowLoaderStarted = true;
			int first = Bars.FullSymbolData.Current - 1;
			DateTime from = Bars.FullSymbolData.Time[first].AddDays(-1);   // covers the first bar's opening ticks
			var fields = FlowCfg.QuotesExpected
				? new[] { RequestQuoteField.Trade, RequestQuoteField.Bid, RequestQuoteField.Ask } : new[] { RequestQuoteField.Trade };
			for (int i = 0; i < fields.Length; i++)
			{
				InstrumentDataRequest req = Bars.Request;
				req.Resolution = new Resolution(EResolution.Tick, 1);
				req.QuoteField = fields[i];
				req.From = from;
				req.To = DateTime.Now.AddDays(1);
				req.Subscribe2RT = true;
				IDataLoaderResult r = DataLoader.BeginLoadData(req, onFlowData, i);
				lock (m_flowLock) m_flowLoads.Add(r);
			}
			if (FlowCfg.McBidAsk != _OrderFlow.EMcBidAsk.Off) startMcLevelLoader(from);
		}

		// Spec S1 ingestion: one more request, a copy of Bars.Request with the Volume Delta chart type. PROBES P-BA1..P-BA4 open,
		// so the chart type is parsed by name and Data2 / RTData2 / BarPriceLevels / PriceLevel members are read by reflection.
		private void startMcLevelLoader(DateTime from)
		{
			ChartType ct;
			try { ct = (ChartType)Enum.Parse(typeof(ChartType), McBidAskChartTypeName); }
			catch { if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, $"OF McBidAsk chartType({McBidAskChartTypeName}) unknown (PROBE P-BA1): tier disabled"); return; }
			InstrumentDataRequest req = Bars.Request;
			Resolution res = req.Resolution;
			res.ChartType = ct;
			req.Resolution = res;
			req.From = from;
			req.To = DateTime.Now.AddDays(1);
			req.Subscribe2RT = true;
			IDataLoaderResult r = DataLoader.BeginLoadData(req, onMcLevels, 3);
			lock (m_flowLock) m_flowLoads.Add(r);
		}

		private void onMcLevels(IDataLoaderResult result)
		{
			lock (m_flowLock)
			{
				if (m_flow == null) return;
				try
				{
					if (result.Event == DataLoadedEvent.History)
					{
						var data = reflectMember(result, "Data2") as System.Collections.IEnumerable;
						if (data != null) foreach (object bpl in data) mcSet(bpl);
						if (result.IsCompleted) m_mcHistoryDone = true;
					}
					else if (result.Event == DataLoadedEvent.RTNewBar)
					{
						object rt = reflectMember(result, "RTData2");
						if (rt != null) mcSet(rt);
					}
				}
				catch { /* member names unverified (P-BA2): bars fall through to Quote / TickRule and count as missing */ }
			}
		}

		private void mcSet(object bpl)
		{
			if (bpl == null) return;
			DateTime t = (DateTime)reflectMember(bpl, "Time");   // PROBE P-BA3: equals the Data1 bar stamp (unverified)
			var levels = reflectMember(bpl, "Levels") as System.Collections.IEnumerable;
			if (levels == null) return;
			var px = new List<double>(); var ask = new List<double>(); var bid = new List<double>();
			foreach (object l in levels)
			{
				px.Add(Convert.ToDouble(reflectMember(l, "Price")));
				ask.Add(Convert.ToDouble(reflectMember(l, "AskTradedValue")));   // PROBE P-BA2: names as VP ILevel (unverified)
				bid.Add(Convert.ToDouble(reflectMember(l, "BidTradedValue")));
			}
			m_flow.AddMcLevels(t, px.ToArray(), ask.ToArray(), bid.ToArray());
			m_mcLoaded++;
			if (t < m_mcFirst) m_mcFirst = t;
			if (t > m_mcLast) m_mcLast = t;
		}

		private static object reflectMember(object o, string name)
		{
			if (o == null) return null;
			var p = o.GetType().GetProperty(name);
			if (p != null) return p.GetValue(o, null);
			var f = o.GetType().GetField(name);
			return f != null ? f.GetValue(o) : null;
		}

		// S1 journal: loaded line once, T9 per session, tier fall-through once per session
		private void mcJournal(_OrderFlow.FootprintBar fp)
		{
			if (FlowCfg.McBidAsk == _OrderFlow.EMcBidAsk.Off || fp == null) return;
			if (!m_mcLoadedLogged && m_mcHistoryDone)
			{
				m_mcLoadedLogged = true;
				engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk loaded bars({m_mcLoaded}) first({m_mcFirst:yyyy-MM-dd HH:mm}) last({m_mcLast:yyyy-MM-dd HH:mm}) method({_OrderFlow.McBidAskMethod})");
			}
			if (fp.SessionKey != m_mcSession)
			{
				if (m_mcSessBars > 0)
					engineSink(ETraceLevel.Journal)?.Invoke($"OF T9 session({m_mcSession:yyyy-MM-dd}) signAgree({(m_mcSessCmp > 0 ? 100.0 * m_mcSessAgree / m_mcSessCmp : double.NaN):F1}) bars({m_mcSessBars}) mcCover({100.0 * m_mcSessCover / m_mcSessBars:F1})");
				m_mcSession = fp.SessionKey; m_mcSessBars = m_mcSessAgree = m_mcSessCover = m_mcSessCmp = 0; m_mcSessFellLogged = false;
			}
			m_mcSessBars++;
			if (!double.IsNaN(fp.McDelta))
			{
				m_mcSessCover++;
				if (!double.IsNaN(fp.ChainDelta)) { m_mcSessCmp++; if (Math.Sign(fp.McDelta) == Math.Sign(fp.ChainDelta)) m_mcSessAgree++; }
			}
			else if (fp.McMiss != null && !m_mcSessFellLogged && FlowCfg.McBidAsk == _OrderFlow.EMcBidAsk.Prefer)
			{
				m_mcSessFellLogged = true;
				engineSink(ETraceLevel.Journal)?.Invoke($"OF tier McBidAsk->{fp.Source} reason({fp.McMiss})");
			}
		}

		// DataLoader callback: another thread (PG §4.7.4).
		private void onFlowData(IDataLoaderResult result)
		{
			int stream = result.State is int ? (int)result.State : 0;
			lock (m_flowLock)
			{
				if (m_flow == null) return;
				if (result.Event == DataLoadedEvent.History)
				{
					if (result.Data != null) foreach (Bar b in result.Data) flowTick(stream, b);
					if (result.IsCompleted) m_flowHistoryDone[stream] = true;
				}
				else if (result.Event == DataLoadedEvent.RTNewBar && result.RTData.HasValue)
				{
					m_flowHistoryDone[stream] = true;
					flowTick(stream, result.RTData.Value);
				}
			}
		}

		private void flowTick(int stream, Bar b)
		{
			object o = b;   // Bar members read by reflection: their names are not in the Programming Guide (verify in the editor)
			DateTime t = (DateTime)barMember(o, ref m_barTime, "Time");
			double close = Convert.ToDouble(barMember(o, ref m_barClose, "Close"));
			if (stream == 0) m_flow.AddTrade(t, close, Convert.ToDouble(barMember(o, ref m_barVolume, "TotalVolume", "Volume", "Ticks")));
			else m_flow.AddQuote(t, stream == 1, close);
		}

		private static object barMember(object o, ref System.Reflection.MemberInfo cache, params string[] names)
		{
			if (cache == null)
				foreach (string n in names)
				{
					cache = (System.Reflection.MemberInfo)o.GetType().GetProperty(n) ?? o.GetType().GetField(n);
					if (cache != null) break;
				}
			if (cache is System.Reflection.PropertyInfo) return ((System.Reflection.PropertyInfo)cache).GetValue(o, null);
			if (cache is System.Reflection.FieldInfo) return ((System.Reflection.FieldInfo)cache).GetValue(o);
			throw new InvalidOperationException("VPF-5: Bar has none of " + string.Join("/", names));
		}

		private void updateOrderFlow()
		{
			if (m_flow == null) return;
			if (!m_flowLogged)
			{
				m_flowLogged = true;
				if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, $"VPF-5 order flow (APPROXIMATED aggressor side, Rev 2.5 chain) tickSource({FlowTickSource}) quotes({FlowCfg.QuotesExpected}) " +
					$"iog({Environment.IOGEnabled}) magnifier1Tick({IogMagnifierOneTick}) buildVolumeOn({FlowCfg.BuildVolumeOn}) maxQuoteAgeMs({FlowCfg.MaxQuoteAgeMs})");
			}
			if (FlowTickSource == EFlowTickSource.DataLoader && !m_flowLoaderStarted) startFlowLoader();

			bool realTime = Environment.IsRealTimeCalc;
			_OrderFlow.Regime regime;
			if (FlowTickSource == EFlowTickSource.DataLoader) regime = _OrderFlow.Regime.TickStream;
			else if (FlowTickSource == EFlowTickSource.Iog && Environment.IOGEnabled)
				regime = realTime ? _OrderFlow.Regime.IogRealTime : (IogMagnifierOneTick ? _OrderFlow.Regime.IogMagnifierOneTick : _OrderFlow.Regime.IogHistoryNoMagnifier);
			else regime = _OrderFlow.Regime.BarsOnly;

			int bar = Bars.FullSymbolData.Current;
			lock (m_flowLock)
			{
				if (regime == _OrderFlow.Regime.IogRealTime || regime == _OrderFlow.Regime.IogMagnifierOneTick)
				{
					if (bar != m_flowIogBar) { m_flowIogBar = bar; m_flowIogLastVol = 0; }
					double vol = Bars.VolumeValue;
					if (bar != m_flowClosedBar && vol > m_flowIogLastVol) m_flow.AddTrade(Bars.Time[0], Bars.CloseValue, vol - m_flowIogLastVol);
					m_flowIogLastVol = vol;
				}
				if (Bars.Status != EBarState.Close || bar == m_flowClosedBar) return;
				m_flowClosedBar = bar;
				// DataLoader history not delivered yet: the bar cannot be built from ticks (gap → BarUpDown or Unavailable)
				if (regime == _OrderFlow.Regime.TickStream && !m_flowHistoryDone[0]) regime = _OrderFlow.Regime.BarsOnly;
				DateTime et = ToEastern(Bars.Time[0]);
				DateTime gx = globexDateEt(et);
				DateTime reset = FlowCfg.ResetMode == _OrderFlow.ResetMode.Rth ? (IsRthValueBarEt(et) ? gx : gx.AddTicks(1)) : gx;
				_OrderFlow.FootprintBar closedFp = m_flow.CloseBar(new _OrderFlow.BarInput
				{
					BarNumber = bar, CloseTime = Bars.Time[0], SessionKey = gx, ResetKey = reset, Regime = regime,
					High = Bars.High[0], Low = Bars.Low[0], Close = Bars.Close[0], Volume = Bars.Volume[0], UpTicks = Bars.UpTicks[0], DownTicks = Bars.DownTicks[0]
				});
				mcJournal(closedFp);
			}
		}

		/// <summary>VPF-5: closed bars only. barsAgo 0 = the current bar once it has closed; an open bar returns Unavailable (Developing).</summary>
		private int flowAgo(int barsAgo)
		{
			if (m_flow == null || barsAgo < 0) return -1;
			int cur = Bars.FullSymbolData.Current;
			if (barsAgo == 0 && m_flowClosedBar != cur) return -1;
			return m_flowClosedBar == cur ? barsAgo : barsAgo - 1;
		}

		/// <summary>Phase 3 VP-3: footprint of Data1 bar `barNumber` (1-based) from the flow engine, or null.</summary>
		protected override _OrderFlow.FootprintBar footprintForLadder(int barNumber)
		{
			if (m_flow == null) return null;
			lock (m_flowLock) return m_flow.ByNumber(barNumber);
		}
		protected override bool ladderHasFlow { get { return m_flow != null; } }
		/// <summary>Phase 3 VP-1 coverage: the Globex clock is open in the slot (skips the daily halt, weekends, holidays and early closes).</summary>
		protected override bool sessionOpenAt(DateTime slotStart) { return GetGlobexState(slotStart) == GlobexState.Open; }

		/// <summary>VPF-5: footprint of a closed bar (null when unavailable). Check Source / Quality / HasLadder.</summary>
		public _OrderFlow.FootprintBar Footprint(int barsAgo)
		{
			int a = flowAgo(barsAgo);
			if (a < 0) return null;
			lock (m_flowLock) { _OrderFlow.FootprintBar fp = m_flow.Closed(a); return fp != null && fp.BarNumber == Bars.FullSymbolData.Current - barsAgo ? fp : null; }
		}

		/// <summary>VPF-5: approximated bar delta (Ask - Bid volume) with its source and quality.</summary>
		public _OrderFlow.FlowValue BarDelta(int barsAgo)
		{
			int a = flowAgo(barsAgo);
			if (a < 0) return _OrderFlow.FlowValue.Unavailable(barsAgo == 0);
			lock (m_flowLock) return m_flow.BarDelta(a);
		}

		/// <summary>VPF-5: cumulative delta since the session open (or RTH open, FlowCfg.ResetMode); worst quality of its bars.</summary>
		public _OrderFlow.FlowValue CumDelta(int barsAgo)
		{
			int a = flowAgo(barsAgo);
			if (a < 0) return _OrderFlow.FlowValue.Unavailable(barsAgo == 0);
			lock (m_flowLock) return m_flow.CumDelta(a);
		}

		/// <summary>VPF-5: diagonal imbalances (ratio floored at 3.0, minVol, TickRule: quality Low and extremes excluded).</summary>
		public IList<_OrderFlow.Imbalance> DiagonalImbalances(int barsAgo, double ratio = 3.0, double minVol = 10)
		{
			int a = flowAgo(barsAgo);
			if (a < 0) return new List<_OrderFlow.Imbalance>();
			lock (m_flowLock) return m_flow.DiagonalImbalances(a, ratio, minVol);
		}

		/// <summary>VPF-5: stacked imbalances (>= 3 consecutive same-side prices).</summary>
		public _OrderFlow.StackedResult StackedImbalances(int barsAgo, double ratio = 3.0, double minVol = 10, int minStack = 3)
		{
			int a = flowAgo(barsAgo);
			if (a < 0) return new _OrderFlow.StackedResult();
			lock (m_flowLock) return m_flow.StackedImbalances(a, ratio, minVol, minStack);
		}

		/// <summary>VPF-5: absorption (Quote quality or better only; Developing until the next bar closes). Value 1 / 0.</summary>
		public _OrderFlow.FlowValue Absorption(int barsAgo, _OrderFlow.Side side, int edgeTicks = 2, double volMult = 2.0, int holdTicks = 1)
		{
			int a = flowAgo(barsAgo);
			if (a < 0) return _OrderFlow.FlowValue.Unavailable(barsAgo == 0);
			lock (m_flowLock) return m_flow.Absorption(a, side, edgeTicks, volMult, holdTicks);
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