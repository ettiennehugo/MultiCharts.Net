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

		/// <summary>Phase 3 S5 (ruling 1): Futures preset per field; fields the entry changed stay its own; skipped after an explicit ApplyPreset.</summary>
		protected override void presetExtraPriceAction(PriceActionConfig c) { c.ApplyPresetDefaults(EPriceActionPreset.Futures); }

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
			drainFlowQueue();       // DataLoader-thread messages -> journal on the calc thread
			base.CalcBar();
			updateSessionStats();   // VPF-2
			stepRollDetector();     // VPF-4 (detector in _CustomSignal since S4; idempotent per bar)
			updateOrderFlow();      // VPF-5 (no-op when OrderFlowEnabled = false)
			if (m_flow == null) return;
			if (mcRecalcCheck("bar")) return;   // Recalc spec §4.3: ExecControl.Recalculate() was the last statement (PROBE P-RC4)
			flowIdleArm();                       // Recalc spec §4.3 step 5
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

		/// <summary>M-DVP (DV-7.7): isRthBar is a real RTH test on the futures base, so rthShare is journaled (NaN on the generic base).</summary>
		protected override bool rthKnown() { return true; }

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
		/// <summary>Spec S1: the MultiCharts chart type (Bars.Request.Resolution.ChartType) of the McBidAsk level request.
		/// Live Check 9a: the enum has no VolumeDelta member; FootPrint is the per-level Ask/Bid-traded chart type.</summary>
		protected ChartType McBidAskChartType { get; set; } = ChartType.FootPrint;
		private int m_mcLoaded; private DateTime m_mcFirst = DateTime.MaxValue, m_mcLast = DateTime.MinValue;
		private bool m_mcHistoryDone, m_mcLoadedLogged;
		private DateTime m_mcSession = DateTime.MinValue; private int m_mcSessBars, m_mcSessAgree, m_mcSessCover, m_mcSessCmp; private bool m_mcSessFellLogged;
		// Recalc spec §4.2: the McBidAsk cache and the recalc gate outlive StartCalc/StopCalc and so ExecControl.Recalculate();
		// only a signature change clears the cache. PROBE P-RC1: MC keeps this instance (and its fields) across Recalculate().
		private readonly _OrderFlow.McLevelCache m_mcCache = new _OrderFlow.McLevelCache();   // under m_flowLock
		private readonly _OrderFlow.McRecalcGate m_mcGate = new _OrderFlow.McRecalcGate();     // under m_flowLock
		private string m_mcSig;   // this pass's request signature (null: no McBidAsk request this pass)
		private bool m_mcCacheHit, m_mcCacheStoredLogged, m_idleArmed, m_idleTimeoutLogged, m_idleDoneLogged;
		private volatile bool m_mcRecalcDecided;      // once per pass (the callback fallback may set it)
		private volatile bool m_autoTradingSnap;      // Environment.IsAutoTradingMode, read on the calc thread for the callback fallback
		private DateTime m_idleSince;
		// Tick-chunk spec §5: one DataLoader request per Globex day and stream (null: legacy single request).
		private _OrderFlow.TickChunkScheduler m_chunks;                 // under m_flowLock
		private Dictionary<int, IDataLoaderResult> m_chunkLoads;        // under m_flowLock (lazy): chunk id -> its request, ended when finished
		private List<Bar>[] m_chunkRtBuf;                                // under m_flowLock (lazy): RT ticks before the RT chunk's history is released
		private RequestQuoteField[] m_chunkFields;                       // stream -> quote field
		private DateTime m_chunkRtTo;                                    // the RT chunk's request end (the legacy window end)
		private int m_chunkIdSeed = 1000;                                // ids >= 1000, increasing across passes: stale callbacks find no chunk
		private DateTime m_lastClosedEt = DateTime.MinValue;             // under m_flowLock: ET close of the last closed bar
		private DateTime m_waitGx = DateTime.MinValue;
		private bool m_chunkSummaryLogged;

		/// <summary>VPF-5: the engine (null when OrderFlowEnabled is false). Lock-free reads are safe from CalcBar.</summary>
		protected _OrderFlow.FlowEngine Flow { get { return m_flow; } }

		private void startOrderFlow()
		{
			stopOrderFlowLoads();
			// Recalc spec §4.2: new pass. m_mcCache and the gate's recalc count are deliberately NOT reset here or in StopCalc.
			lock (m_flowLock) m_mcGate.BeginPass();
			m_mcSig = null; m_mcCacheHit = m_mcCacheStoredLogged = m_mcRecalcDecided = false;
			m_idleArmed = m_idleTimeoutLogged = m_idleDoneLogged = false;
			lock (m_flowLock)
			{
				if (m_chunks != null) m_chunkIdSeed = m_chunks.NextId + 1000;
				m_chunks = null; m_chunkLoads = null; m_chunkRtBuf = null; m_lastClosedEt = DateTime.MinValue;
			}
			m_waitGx = DateTime.MinValue; m_chunkSummaryLogged = false;
			m_flow = null; m_flowLoaderStarted = false; m_flowLogged = false;
			m_mcLoaded = 0; m_mcFirst = DateTime.MaxValue; m_mcLast = DateTime.MinValue; m_mcHistoryDone = m_mcLoadedLogged = false; m_mcDiagLogged = null; m_mcCallbacks = 0; m_mcResult = null; m_mcStarted = false; m_mcHistBatches = m_mcHistNullBatches = m_mcHistBars = 0; m_mcHistLevels = 0;
			m_mcSession = DateTime.MinValue; m_mcSessBars = m_mcSessAgree = m_mcSessCover = m_mcSessCmp = 0; m_mcSessFellLogged = false;
			m_flowClosedBar = -1; m_flowIogBar = -1; m_flowIogLastVol = 0;
			for (int i = 0; i < m_flowHistoryDone.Length; i++) m_flowHistoryDone[i] = false;
			if (!OrderFlowEnabled) return;
			if (FlowTickSource != EFlowTickSource.DataLoader) FlowCfg.QuotesExpected = false;
			m_flow = new _OrderFlow.FlowEngine(tickSize(), FlowCfg) { journal = queueSink(ETraceLevel.Journal), warnOnce = queueSink(ETraceLevel.Warning) };
		}

		protected override void StopCalc()
		{
			if (m_mcStarted)
			{
				IDataLoaderResult mr = m_mcResult;
				// PG §4.7.4: IsCompleted is the only documented status member of IDataLoaderResult.
				drainFlowQueue();
				engineSink(ETraceLevel.Journal)?.Invoke(mcStopLine(mr, "stopCalc"));
			}
			drainFlowQueue();
			tickChunkSummary();
			stopOrderFlowLoads();
			drainFlowQueue();
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

		// Calc thread only.
		private string mcStopLine(IDataLoaderResult mr, string when)
		{
			// PG §4.7.4: IsCompleted is the only documented status member of IDataLoaderResult.
			return $"OF McBidAsk diag stop callbacks({System.Threading.Interlocked.CompareExchange(ref m_mcCallbacks, 0, 0)}) result({(mr == null ? "null" : "set")}) pending({(mr != null && !mr.IsCompleted ? 1 : 0)}) historyDone({m_mcHistoryDone}) when({when})";
		}

		private void startFlowLoader()
		{
			m_flowLoaderStarted = true;
			int first = Bars.FullSymbolData.Current - 1;
			DateTime firstBar = Bars.FullSymbolData.Time[first];
			// The last bar of the chart (PG: FullSymbolData.Count = bars on the chart; Time[k] is k bars back from Current; P-RC6 confirmed).
			DateTime lastBar = Bars.FullSymbolData.Time[Bars.FullSymbolData.Current - Bars.FullSymbolData.Count];
			DateTime from = firstBar.AddDays(-1);   // covers the first bar's opening ticks
			DateTime to = lastBar.AddDays(1);       // Recalc spec §4.5: clamp to the chart (was DateTime.Now + 1 day)
			var fields = FlowCfg.QuotesExpected
				? new[] { RequestQuoteField.Trade, RequestQuoteField.Bid, RequestQuoteField.Ask } : new[] { RequestQuoteField.Trade };
			if (FlowCfg.TickChunkByDay && planTickChunks(firstBar, lastBar, to, fields)) pumpTickChunks();
			else for (int i = 0; i < fields.Length; i++)
			{
				InstrumentDataRequest req = Bars.Request;
				req.Resolution = new Resolution(EResolution.Tick, 1);
				req.QuoteField = fields[i];
				setRequestWindow(ref req, from, to);
				req.Subscribe2RT = true;
				IDataLoaderResult r = DataLoader.BeginLoadData(req, onFlowData, i);
				lock (m_flowLock) m_flowLoads.Add(r);
			}
			if (FlowCfg.McBidAsk != _OrderFlow.EMcBidAsk.Off)
			{
				try { mcStartOrReplay(firstBar, lastBar, from, to); }
				catch (Exception ex) { engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk diag start exception({ex.GetType().FullName}) message({ex.Message})"); }
			}
		}

		// Recalc spec §4.5: the request window. API dump (P-RC5/P-RC8): InstrumentDataRequest.Range is a DataRequest struct field
		// (CreateFromTo sets RequestType FromTo); From/To are properties. Both carry the same window; TimeZone stays as copied from
		// Bars.Request (the zone the chart bars and the session clock use).
		private static void setRequestWindow(ref InstrumentDataRequest req, DateTime from, DateTime to)
		{
			req.Range = DataRequest.CreateFromTo(from, to);
			req.From = from;
			req.To = to;
		}

		// Calc thread only (journal text of a request window: the typed Range fields and the From/To properties).
		private static string requestWindowText(InstrumentDataRequest req)
		{
			DataRequest rg = req.Range;
			return $"range(type({rg.RequestType}) from({rg.From:yyyy-MM-dd HH:mm}) to({rg.To:yyyy-MM-dd HH:mm}) toAlwaysIsNow({rg.ToAlwaysIsNow})) " +
				$"from({req.From:yyyy-MM-dd HH:mm}) to({req.To:yyyy-MM-dd HH:mm}) zone({req.TimeZone})";
		}

		// Recalc spec §4.2: replay the cache when it holds this request's completed history (no new request; a tail request on a
		// live chart), otherwise clear it and request the full window. Calc thread only.
		private void mcStartOrReplay(DateTime firstBar, DateTime lastBar, DateTime from, DateTime to)
		{
			Resolution cr = Bars.Request.Resolution;
			string sig = $"{Bars.Info.Name}|{Bars.Info.DataFeed}|{cr.Type}|{cr.Size}|{McBidAskChartType}|{FlowCfg.McBidAsk}|{firstBar:yyyy-MM-dd HH:mm:ss}";
			m_mcSig = sig;
			bool live = Environment.IsRealTimeCalc || lastBar >= DateTime.Now.AddDays(-1);
			string miss; int sets; long levels; DateTime cFirst, cLast; int pass, recalcs;
			lock (m_flowLock)
			{
				miss = m_mcCache.MissReason(sig, firstBar, lastBar);
				if (miss == null)
				{
					m_mcCache.ReplayInto(m_flow);
					m_mcLoaded = m_mcCache.Sets; m_mcFirst = m_mcCache.First; m_mcLast = m_mcCache.Last;
					m_mcHistoryDone = true;
				}
				else
				{
					m_mcCache.Clear();
					m_mcCache.Signature = sig; m_mcCache.RequestFrom = from; m_mcCache.RequestTo = to;
				}
				sets = m_mcCache.Sets; levels = m_mcCache.Levels; cFirst = m_mcCache.First; cLast = m_mcCache.Last;
				pass = m_mcGate.Pass; recalcs = m_mcGate.CountFor(sig);
			}
			if (miss != null)
			{
				engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk cache miss reason({miss}) sig({sig})");
				startMcLevelLoader(from, to, "full");
				return;
			}
			m_mcCacheHit = true;
			m_mcRecalcDecided = true;   // levels are present from the first bar: nothing to recalculate in this pass
			DateTime tailFrom = sets > 0 ? cLast : from;
			engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk cache hit sets({sets}) levels({levels}) first({cFirst:yyyy-MM-dd HH:mm}) last({cLast:yyyy-MM-dd HH:mm}) pass({pass}) " +
				(live ? $"request(tail from({tailFrom:yyyy-MM-dd HH:mm}))" : "request(none)"));
			if (recalcs > 0) engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk recalc pass using cache pass({pass}) recalcCount({recalcs})");
			if (live) startMcLevelLoader(tailFrom, DateTime.Now.AddDays(1), "tail");
		}

		// Spec S1 ingestion: one more request, a copy of Bars.Request with ChartType McBidAskChartType (FootPrint). PROBES P-BA2..P-BA4 open;
		// Data2 / RTData2 / BarPriceLevels / PriceLevel are read through the typed API.
		private void startMcLevelLoader(DateTime from, DateTime to, string kind)
		{
			InstrumentDataRequest req = Bars.Request;
			Resolution res = req.Resolution;
			// MC forum t=52737 (MC support): Data2 stays null unless the FootPrint resolution is built with CreateFootPrint
			// (delta + footprint modes). Other chart types: set the enum directly.
			CumulativeDeltaChartType deltaType = CumulativeDeltaChartType.ASK_TRADE_BID_TRADE;
			FootPrintChartType fpType = FootPrintChartType.BID_ASK_VOL;
			bool fpPct = false;
			bool footPrint = McBidAskChartType == ChartType.FootPrint;
			if (footPrint)
				res = Resolution.CreateFootPrint(res.Type, res.Size, deltaType, fpType, fpPct);
			else
				res.ChartType = McBidAskChartType;
			req.Resolution = res;
			setRequestWindow(ref req, from, to);   // Recalc spec §4.5: last chart bar + 1 day (tail requests on a live chart: now + 1 day)
			req.Subscribe2RT = true;
			m_mcStarted = true;
			IDataLoaderResult r = DataLoader.BeginLoadData(req, onMcLevels, 3);
			m_mcResult = r;   // kept alive here and in m_flowLoads until EndLoadData
			lock (m_flowLock) m_flowLoads.Add(r);
			// Recalc spec §7.1: log what was built (the CreateFootPrint arguments are typed locals). API dump (P-RC9): after
			// CreateFootPrint the resolution reads ChartType=Regular, DeltaType=None, HaveDelta=True, so Regular is expected.
			string builder = footPrint
				? $"builder(CreateFootPrint delta({deltaType}) footprint({fpType}) pct({fpPct})) resChartType({res.ChartType}) haveDelta({res.HaveDelta})"
				: $"builder(ChartType) resChartType({res.ChartType}) haveDelta({res.HaveDelta})";
			// IsCompleted is the only documented status member (PG §4.7.4); no error/status field is documented, so none is logged.
			engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk diag request kind({kind}) resType({res.Type}) resSize({res.Size}) {builder} {requestWindowText(req)} " +
				$"subscribe2RT({req.Subscribe2RT}) quoteField({req.QuoteField}) beginLoadData({(r == null ? "null" : "result")}) isCompleted({(r == null ? "n/a" : r.IsCompleted.ToString())})");
		}

		// Recalc spec §4.3: the gate decision. Caller holds m_flowLock. Reads no MC member (the callback fallback calls it).
		private _OrderFlow.RecalcDecision mcRecalcDecision()
		{
			bool covers = m_mcCache.HistoryDone && m_mcGate.MissedBeforeDone > 0 && m_mcCache.Sets > 0
				&& m_mcCache.First <= m_mcGate.MissedLast && m_mcCache.Last >= m_mcGate.MissedFirst;
			return m_mcGate.Decide(m_mcHistoryDone, covers, m_autoTradingSnap, FlowCfg.McRecalcOnLoad, FlowCfg.McRecalcMax, m_mcSig);
		}

		// Caller holds m_flowLock. Reads no MC member.
		private string mcTriggeredLine(string from)
		{
			return $"OF McBidAsk recalc triggered from({from}) pass({m_mcGate.Pass}) missedBars({m_mcGate.MissedBeforeDone}) missedFirst({m_mcGate.MissedFirst:yyyy-MM-dd HH:mm}) " +
				$"missedLast({m_mcGate.MissedLast:yyyy-MM-dd HH:mm}) recalcCount({m_mcGate.CountFor(m_mcSig) + 1}/{FlowCfg.McRecalcMax})";
		}

		// Recalc spec §4.3 steps 3-4. Calc thread only (end of CalcBar, OnRecalcLastBarAfterEvent). Returns true when
		// ExecControl.Recalculate() was called and returned (PROBE P-RC4: it is expected to unwind instead).
		private bool mcRecalcCheck(string from)
		{
			if (m_mcSig == null) return false;
			bool done; _OrderFlow.RecalcDecision d = _OrderFlow.RecalcDecision.Wait;
			int sets = 0; long levels = 0; DateTime cFirst = DateTime.MinValue, cLast = DateTime.MinValue; string line = null;
			lock (m_flowLock)
			{
				done = m_mcHistoryDone;
				if (done && !m_mcCacheStoredLogged) { sets = m_mcCache.Sets; levels = m_mcCache.Levels; cFirst = m_mcCache.First; cLast = m_mcCache.Last; }
				if (done && !m_mcRecalcDecided)
				{
					d = mcRecalcDecision();
					m_mcRecalcDecided = true;
					if (d == _OrderFlow.RecalcDecision.Recalc) { line = mcTriggeredLine(from); m_mcGate.NoteRecalc(m_mcSig); }
				}
			}
			if (!done) return false;
			if (!m_mcCacheStoredLogged)
			{
				m_mcCacheStoredLogged = true;
				if (!m_mcCacheHit)
					engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk cache stored sets({sets}) levels({levels}) first({cFirst:yyyy-MM-dd HH:mm}) last({cLast:yyyy-MM-dd HH:mm}) sig({m_mcSig})");
			}
			if (line == null)
			{
				if (d != _OrderFlow.RecalcDecision.Wait)
					engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk recalc skipped reason({_OrderFlow.McRecalcGate.SkipReason(d)})");
				return false;
			}
			engineSink(ETraceLevel.Journal)?.Invoke(line);
			drainFlowQueue();
			ExecControl.Recalculate();   // Recalc spec §2: restarts from StartCalc; last statement, never inside a try/catch
			return true;
		}

		// Recalc spec §4.3 step 5: on the last chart bar, while loader work is pending, ask MC for OnRecalcLastBarAfterEvent.
		private void flowIdleArm()
		{
			if (m_idleArmed || !Bars.LastBarOnChart || Bars.Status != EBarState.Close || !idleWorkPending()) return;
			m_idleArmed = true;
			m_idleSince = DateTime.Now;
			engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk idle poll armed ms({FlowCfg.McIdlePollMs})");
			ExecControl.RecalcLastBarAfter(TimeSpan.FromMilliseconds(Math.Max(100, FlowCfg.McIdlePollMs)));
		}

		// Calc thread only.
		private bool idleWorkPending()
		{
			bool queued; lock (m_flowQueueLock) queued = m_flowQueue.Count > 0;
			bool mc; lock (m_flowLock) mc = m_mcSig != null && (!m_mcHistoryDone || !m_mcRecalcDecided || !m_mcCacheStoredLogged);
			return queued || mc || tickChunksPending() > 0;
		}

		// Tick-chunk spec §5.1: plan one chunk per Globex day (calc thread). false: nothing to chunk (legacy single request).
		private bool planTickChunks(DateTime firstBar, DateTime lastBar, DateTime rtTo, RequestQuoteField[] fields)
		{
			_CmeSessionClock ck = clock();
			DateTime gx0 = ck.GlobexDate(firstBar), gx1 = ck.GlobexDate(lastBar);
			var closed = new List<DateTime>();
			List<_OrderFlow.DayChunk> days = _OrderFlow.PlanGlobexDays(gx0, gx1, ck.IsGlobexClosedDay, FlowCfg.TickChunkBoundaryEt, closed);
			if (days.Count == 0)
			{
				engineSink(ETraceLevel.Journal)?.Invoke($"OF tick chunk plan days(0) first({gx0:yyyy-MM-dd}) last({gx1:yyyy-MM-dd}) -> single request");
				return false;
			}
			var streams = new int[fields.Length];
			for (int i = 0; i < streams.Length; i++) streams[i] = i;
			m_chunkFields = fields;
			m_chunkRtTo = rtTo;
			lock (m_flowLock)
				m_chunks = new _OrderFlow.TickChunkScheduler(days, streams, FlowCfg.TickChunkMaxInFlightDays, FlowCfg.TickChunkSplitOnFail, m_chunkIdSeed);
			bool skipLate = FlowCfg.TickChunkLate == _OrderFlow.ETickChunkLate.Skip && FlowCfg.TickChunkWaitMs <= 0;
			engineSink(ETraceLevel.Journal)?.Invoke($"OF tick chunk plan days({days.Count}) first({days[0].GlobexDate:yyyy-MM-dd}) last({days[days.Count - 1].GlobexDate:yyyy-MM-dd}) " +
				$"streams({streams.Length}) maxInFlightDays({m_chunks.MaxInFlightDays}) splitOnFail({m_chunks.SplitOnFail}) late({(skipLate ? "Skip" : "Load")}) " +
				$"waitMs({FlowCfg.TickChunkWaitMs}) boundaryEt({FlowCfg.TickChunkBoundaryEt:hh\\:mm}) closedDays({closed.Count})");
			if (traceOn(ETraceLevel.Info))
				foreach (DateTime d in closed) trace(ETraceLevel.Info, $"OF tick chunk skipped day({d:yyyy-MM-dd}) reason(closedDay)");
			return true;
		}

		// Tick-chunk spec §5.2: issue the next chunks, end finished requests (calc thread: CalcBar, idle event, wait loop).
		private void pumpTickChunks()
		{
			if (m_chunks == null || m_flow == null) return;
			var skipped = new List<_OrderFlow.TickChunk>();
			List<_OrderFlow.TickChunk> issue;
			bool skipLate = FlowCfg.TickChunkLate == _OrderFlow.ETickChunkLate.Skip && FlowCfg.TickChunkWaitMs <= 0;   // a wait loads late days
			lock (m_flowLock)
			{
				if (m_chunkLoads != null)
				{
					List<int> ended = null;
					foreach (KeyValuePair<int, IDataLoaderResult> kv in m_chunkLoads)
					{
						_OrderFlow.TickChunk c = m_chunks.Find(kv.Key);
						if (c == null || !c.Finished || c.Rt) continue;
						try { DataLoader.EndLoadData(kv.Value); } catch { }
						m_flowLoads.Remove(kv.Value);
						(ended ?? (ended = new List<int>())).Add(kv.Key);
					}
					if (ended != null) foreach (int id in ended) m_chunkLoads.Remove(id);
				}
				issue = m_chunks.Next(m_lastClosedEt, skipLate, skipped);
				foreach (_OrderFlow.TickChunk c in issue)
				{
					c.ReqFrom = clock().FromEastern(c.FromEt);
					c.ReqTo = c.Rt ? (m_chunkRtTo > clock().FromEastern(c.ToEt) ? m_chunkRtTo : clock().FromEastern(c.ToEt)) : clock().FromEastern(c.ToEt);
					c.IssuedAt = DateTime.Now;
				}
			}
			bool info = traceOn(ETraceLevel.Info);
			if (info)
				foreach (_OrderFlow.TickChunk c in skipped)
					trace(ETraceLevel.Info, $"OF tick chunk skipped day({c.Day:yyyy-MM-dd}) stream({c.Stream}) sub({c.Sub}) reason({c.SkipReason}) calcEt({m_lastClosedEt:yyyy-MM-dd HH:mm})");
			foreach (_OrderFlow.TickChunk c in issue)
			{
				InstrumentDataRequest req = Bars.Request;   // TimeZone (RequestTimeZone) kept as copied: the chart's zone, same as the clock's bar zone
				req.Resolution = new Resolution(EResolution.Tick, 1);
				req.QuoteField = m_chunkFields[c.Stream];
				setRequestWindow(ref req, c.ReqFrom, c.ReqTo);
				req.Subscribe2RT = c.Rt;
				if (info) trace(ETraceLevel.Info, $"OF tick chunk start day({c.Day:yyyy-MM-dd}) stream({c.Stream}) sub({c.Sub}) attempt({c.Attempt}) id({c.Id}) rt({c.Rt}) " +
					$"et({c.FromEt:yyyy-MM-dd HH:mm}..{c.ToEt:yyyy-MM-dd HH:mm}) {requestWindowText(req)}");
				IDataLoaderResult r = DataLoader.BeginLoadData(req, onFlowData, c.Id);   // state: the chunk id (boxed int)
				lock (m_flowLock)
				{
					m_flowLoads.Add(r);
					(m_chunkLoads ?? (m_chunkLoads = new Dictionary<int, IDataLoaderResult>()))[c.Id] = r;
				}
			}
		}

		private int tickChunksPending() { lock (m_flowLock) return m_chunks == null ? 0 : m_chunks.Pending(); }

		// Calc thread; once per pass (idle done or StopCalc).
		private void tickChunkSummary()
		{
			if (m_chunkSummaryLogged) return;
			int days, done, failed, skipped, pending; long ticks, late, backlog;
			lock (m_flowLock)
			{
				if (m_chunks == null) return;
				days = m_chunks.Days;
				m_chunks.Summary(out done, out failed, out skipped, out ticks, out late);
				pending = m_chunks.Pending();
				backlog = m_flow != null ? m_flow.BacklogDropped : 0;
			}
			m_chunkSummaryLogged = true;
			engineSink(ETraceLevel.Journal)?.Invoke($"OF tick chunk summary days({days}) done({done}) failed({failed}) skipped({skipped}) pending({pending}) ticks({ticks}) lateDropped({late}) backlogDropped({backlog})");
		}

		// Recalc spec §4.3: MC calls this on the calc thread after ExecControl.RecalcLastBarAfter (forum t=43339: it does not
		// recalculate by itself). PROBE P-RC2 (fires while history loads), P-RC3 (trace/Output usable here), P-RC4 (Recalculate here).
		protected override void OnRecalcLastBarAfterEvent()
		{
			base.OnRecalcLastBarAfterEvent();
			if (m_flow == null || !m_idleArmed) return;
			m_autoTradingSnap = Environment.IsAutoTradingMode;
			drainFlowQueue();
			pumpTickChunks();
			if (mcRecalcCheck("idle")) return;
			if (idleWorkPending())
			{
				if ((DateTime.Now - m_idleSince).TotalSeconds < FlowCfg.McIdleTimeoutSec)
					ExecControl.RecalcLastBarAfter(TimeSpan.FromMilliseconds(Math.Max(100, FlowCfg.McIdlePollMs)));
				else if (!m_idleTimeoutLogged)
				{
					m_idleTimeoutLogged = true;
					bool hd; lock (m_flowLock) hd = m_mcHistoryDone;
					engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk idle poll timeout sec({FlowCfg.McIdleTimeoutSec}) historyDone({hd}) chunksPending({tickChunksPending()})");
				}
				return;
			}
			if (m_idleDoneLogged) return;
			m_idleDoneLogged = true;
			if (m_mcStarted) engineSink(ETraceLevel.Journal)?.Invoke(mcStopLine(m_mcResult, "idle"));
			tickChunkSummary();
		}

		private int m_mcHistBatches, m_mcHistNullBatches, m_mcHistBars; private long m_mcHistLevels;   // cumulative history diagnostics
		private int m_mcCallbacks; private volatile IDataLoaderResult m_mcResult; private bool m_mcStarted;   // loader diagnostics
		private HashSet<string> m_mcDiagLogged;   // lazy: McBidAsk diagnostics already journaled (each once per run)

		// Queue one diagnostic line once per key (any thread, under m_flowLock). Drained to the journal on the calc thread.
		private void mcDiag(string key, string line)
		{
			if (m_mcDiagLogged == null) m_mcDiagLogged = new HashSet<string>();
			if (!m_mcDiagLogged.Add(key)) return;
			enqueueFlowMsg(ETraceLevel.Journal, line);
		}

		// DataLoader callbacks run on another thread (PG §4.7.4) and must not touch Bars, Output, trace() or engineSink
		// (MC: "Unaccessible property(method): Bars. Calculation thread only."). They queue plain strings; the calc thread drains.
		private readonly object m_flowQueueLock = new object();
		private List<KeyValuePair<ETraceLevel, string>> m_flowQueue = new List<KeyValuePair<ETraceLevel, string>>();

		private void enqueueFlowMsg(ETraceLevel level, string msg)
		{
			lock (m_flowQueueLock) m_flowQueue.Add(new KeyValuePair<ETraceLevel, string>(level, msg));
		}

		// Thread-safe sink for engine delegates: the level filter is evaluated here (calc thread); the returned delegate only queues.
		private Action<string> queueSink(ETraceLevel level)
		{
			if (!traceOn(level)) return null;
			return s => enqueueFlowMsg(level, s);
		}

		// Calc thread only (CalcBar start, StopCalc).
		private void drainFlowQueue()
		{
			List<KeyValuePair<ETraceLevel, string>> q;
			lock (m_flowQueueLock)
			{
				if (m_flowQueue.Count == 0) return;
				q = m_flowQueue; m_flowQueue = new List<KeyValuePair<ETraceLevel, string>>();
			}
			foreach (KeyValuePair<ETraceLevel, string> m in q) engineSink(m.Key)?.Invoke(m.Value);
		}

		// Typed PG §4.7.4 API: IDataLoaderResult.Data2 (BarPriceLevels[]) / RTData2 (BarPriceLevels?); BarPriceLevels.Levels
		// (PriceLevel[]) and .Time; PriceLevel.Price / UpVolume / DownVolume (MC forum t=52737). No string lookups.
		private void onMcLevels(IDataLoaderResult result)
		{
			bool recalcNow = false;
			try
			{
				System.Threading.Interlocked.Increment(ref m_mcCallbacks);
				lock (m_flowLock)
				{
					// Entry diagnostic before any null/m_flow check: typed members only (Event, IsCompleted, Data2, RTData2; PG §4.7.4 + API dump).
					// No error/status field is documented on IDataLoaderResult, so none is logged.
					try
					{
						if (result == null) mcDiag("entry:null", "OF McBidAsk diag entry result(null)");
						else
						{
							BarPriceLevels[] d2 = result.Data2; BarPriceLevels? rt2 = result.RTData2;
							mcDiag("entry:" + result.Event + ":" + result.IsCompleted + ":" + (d2 == null) + ":" + rt2.HasValue,
								$"OF McBidAsk diag entry event({result.Event}) isCompleted({result.IsCompleted}) data2({(d2 == null ? "null" : d2.Length.ToString())}) rtData2HasValue({rt2.HasValue}) flow({(m_flow == null ? "null" : "set")})");
						}
					}
					catch (Exception ex) { mcDiag("entryex:" + ex.GetType().Name, $"OF McBidAsk diag entry exception({ex.GetType().FullName}) message({ex.Message})"); }
					if (m_flow == null || result == null) return;
					bool completed = false;
					try
					{
						completed = result.IsCompleted;
						mcDiag("event:" + result.Event + ":" + completed, $"OF McBidAsk diag event({result.Event}) isCompleted({completed})");
						if (result.Event == DataLoadedEvent.History)
						{
							BarPriceLevels[] data = result.Data2;
							if (data == null) m_mcHistNullBatches++;
							else foreach (BarPriceLevels bpl in data) { m_mcHistBars++; m_mcHistLevels += bpl.Levels == null ? 0 : bpl.Levels.Length; mcSet(bpl); }
							m_mcHistBatches++;
							// Cumulative over all history batches, logged once at completion.
							if (completed) mcDiag("hist:done", $"OF McBidAsk diag history done batches({m_mcHistBatches}) nullData2Batches({m_mcHistNullBatches}) data2Bars({m_mcHistBars}) levels({m_mcHistLevels})");
						}
						else if (result.Event == DataLoadedEvent.RTNewBar)
						{
							BarPriceLevels? rt = result.RTData2;
							mcDiag("rt:" + rt.HasValue, $"OF McBidAsk diag rt rtData2({(rt.HasValue ? "bar levels(" + (rt.Value.Levels == null ? 0 : rt.Value.Levels.Length) + ")" : "none")})");
							if (rt.HasValue) mcSet(rt.Value);
						}
					}
					catch (Exception ex)
					{
						mcDiag("ex:" + ex.GetType().Name, $"OF McBidAsk diag exception({ex.GetType().FullName}) message({ex.Message})");
					}
					// History completion stands even when parsing failed: the loaded line then reports bars(0).
					if (completed && result.Event == DataLoadedEvent.History)
					{
						m_mcHistoryDone = true;
						m_mcCache.HistoryDone = true;   // Recalc spec §4.2: the cache now holds the request's complete history
						// Recalc spec §4.3 step 6 fallback (McRecalcFromCallback, off): PROBE P-RC2/P-RC3. Lines are queued; the
						// stored/triggered lines are written by the next calc-thread drain.
						if (FlowCfg.McRecalcFromCallback && m_mcSig != null && !m_mcRecalcDecided
							&& mcRecalcDecision() == _OrderFlow.RecalcDecision.Recalc)
						{
							m_mcRecalcDecided = true;
							enqueueFlowMsg(ETraceLevel.Journal, mcTriggeredLine("callback"));
							m_mcGate.NoteRecalc(m_mcSig);
							recalcNow = true;
						}
					}
				}
			}
			catch (Exception ex) { enqueueFlowMsg(ETraceLevel.Journal, $"OF McBidAsk callback exception({ex.GetType().FullName}) message({ex.Message})"); }
			if (recalcNow) ExecControl.Recalculate();   // fallback only (off by default); last statement, outside the try (spec §6)
		}

		private void mcSet(BarPriceLevels bpl)
		{
			PriceLevel[] levels = bpl.Levels;
			if (levels == null || levels.Length == 0) return;
			DateTime t = bpl.Time;   // PROBE P-BA3: equals the Data1 bar stamp (unverified)
			var px = new double[levels.Length]; var ask = new double[levels.Length]; var bid = new double[levels.Length];
			for (int k = 0; k < levels.Length; k++)
			{
				// CumulativeDeltaChartType.ASK_TRADE_BID_TRADE: Up = Ask traded, Down = Bid traded (P-BA2, unverified mapping)
				px[k] = levels[k].Price;
				ask[k] = levels[k].UpVolume;
				bid[k] = levels[k].DownVolume;
			}
			m_flow.AddMcLevels(t, px, ask, bid);
			m_mcCache.Add(t, px, ask, bid);   // Recalc spec §4.2 (caller holds m_flowLock)
			m_mcLoaded++;
			if (t < m_mcFirst) m_mcFirst = t;
			if (t > m_mcLast) m_mcLast = t;
		}

		// S1 journal: loaded line once, T9 per session, tier fall-through once per session
		private void mcJournal(_OrderFlow.FootprintBar fp)
		{
			if (FlowCfg.McBidAsk == _OrderFlow.EMcBidAsk.Off || fp == null) return;
			// Recalc spec §4.3: a bar that closed with no level set before the history completed (caller holds m_flowLock)
			if (fp.McMiss == "missing" && !m_mcHistoryDone) m_mcGate.NoteMissed(fp.CloseTime);
			if (!m_mcLoadedLogged && m_mcHistoryDone)
			{
				m_mcLoadedLogged = true;
				engineSink(ETraceLevel.Journal)?.Invoke($"OF McBidAsk loaded bars({m_mcLoaded}) first({m_mcFirst:yyyy-MM-dd HH:mm}) last({m_mcLast:yyyy-MM-dd HH:mm}) source({(m_mcCacheHit ? "cache" : "request")}) configChartType({McBidAskChartType}) method({_OrderFlow.McBidAskMethod}) mapping(UpVolume=AskTraded DownVolume=BidTraded UNVERIFIED)");
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
			try
			{
				int stream = result.State is int ? (int)result.State : 0;
				lock (m_flowLock)
				{
					if (m_flow == null) return;
					if (m_chunks != null) { onChunkData(stream, result); return; }
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
			catch (Exception ex) { enqueueFlowMsg(ETraceLevel.Journal, $"OF tick loader callback exception({ex.GetType().FullName}) message({ex.Message})"); }
		}

		// DataLoader callback (another thread), under m_flowLock: one chunk's ticks. Touches only m_chunks, m_flow (via flowTick),
		// the chunk buffers and m_flowHistoryDone; lines are queued.
		private void onChunkData(int id, IDataLoaderResult result)
		{
			_OrderFlow.TickChunk c = m_chunks.Find(id);
			if (c == null || c.Replaced) return;   // an earlier pass, or a chunk replaced by its sub-windows
			if (result.Event == DataLoadedEvent.History)
			{
				if (c.State != _OrderFlow.ChunkState.InFlight) return;
				var buf = c.Payload as List<Bar>;
				if (buf == null) c.Payload = buf = new List<Bar>();
				// memory guard (spec §5.2): a Skip-mode day that ended before the last closed bar can no longer be used
				bool late = !c.Rt && c.ToEt <= m_lastClosedEt && FlowCfg.TickChunkLate == _OrderFlow.ETickChunkLate.Skip && FlowCfg.TickChunkWaitMs <= 0;
				try
				{
					if (result.Data != null)
						foreach (Bar b in result.Data)
						{
							DateTime t = b.Time;
							if (t <= c.ReqFrom || (!c.Rt && t > c.ReqTo)) continue;   // (ReqFrom, ReqTo]: bars are close-stamped
							if (late) { c.LateDropped++; continue; }
							buf.Add(b);
						}
				}
				catch (Exception ex)
				{
					string outcome = m_chunks.Complete(c, false, 0, null);
					enqueueFlowMsg(ETraceLevel.Info, $"OF tick chunk failed day({c.Day:yyyy-MM-dd}) stream({c.Stream}) sub({c.Sub}) attempt({c.Attempt}) next({outcome}) exception({ex.GetType().FullName}) message({ex.Message})");
					releaseChunks();
					return;
				}
				if (!result.IsCompleted) return;
				m_chunks.Complete(c, true, buf.Count, buf);
				enqueueFlowMsg(ETraceLevel.Info, $"OF tick chunk done day({c.Day:yyyy-MM-dd}) stream({c.Stream}) sub({c.Sub}) ticks({buf.Count}) lateDropped({c.LateDropped}) " +
					$"sec({(DateTime.Now - c.IssuedAt).TotalSeconds:0.0})");
				releaseChunks();
			}
			else if (result.Event == DataLoadedEvent.RTNewBar && result.RTData.HasValue && c.Rt)
			{
				m_flowHistoryDone[c.Stream] = true;
				if (c.Released) { flowTick(c.Stream, result.RTData.Value); return; }
				if (m_chunkRtBuf == null) m_chunkRtBuf = new List<Bar>[3];
				(m_chunkRtBuf[c.Stream] ?? (m_chunkRtBuf[c.Stream] = new List<Bar>())).Add(result.RTData.Value);
			}
		}

		// Under m_flowLock (callback or calc thread): hand finished chunks to the engine in time order per stream (its queues are
		// time-ordered as delivered), then any RT ticks that waited for the RT chunk's history.
		private void releaseChunks()
		{
			foreach (_OrderFlow.TickChunk c in m_chunks.ReleasableInOrder())
			{
				var buf = c.Payload as List<Bar>;
				c.Payload = null;
				if (buf != null) foreach (Bar b in buf) flowTick(c.Stream, b);
				if (c.Rt)
				{
					m_flowHistoryDone[c.Stream] = true;
					List<Bar> rt = m_chunkRtBuf != null ? m_chunkRtBuf[c.Stream] : null;
					if (rt != null) { foreach (Bar b in rt) flowTick(c.Stream, b); m_chunkRtBuf[c.Stream] = null; }
				}
			}
		}

		// Typed PowerLanguage Bar members (no reflection), confirmed by the API dump of the MC install:
		// struct Bar { DateTime Time; double Open/High/Low/Close; float UpVolume/DownVolume/UnchangedVolume/TotalVolume; ... }.
		private void flowTick(int stream, Bar b)
		{
			DateTime t = b.Time;
			double close = b.Close;
			if (stream == 0) m_flow.AddTrade(t, close, (double)b.TotalVolume);   // Bar.TotalVolume is float: explicit widening to double
			else m_flow.AddQuote(t, stream == 1, close);
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
			if (!m_flowLoaderStarted && FlowCfg.McBidAsk != _OrderFlow.EMcBidAsk.Off)
				lock (m_flowLock) mcDiag("gate", $"OF McBidAsk diag gate tickSource({FlowTickSource}) mcBidAsk({FlowCfg.McBidAsk}) startsLoader({FlowTickSource == EFlowTickSource.DataLoader})");
			if (FlowTickSource == EFlowTickSource.DataLoader && !m_flowLoaderStarted) startFlowLoader();

			m_autoTradingSnap = Environment.IsAutoTradingMode;   // Recalc spec §4.3: read here for the callback fallback
			bool realTime = Environment.IsRealTimeCalc;
			_OrderFlow.Regime regime;
			if (FlowTickSource == EFlowTickSource.DataLoader) regime = _OrderFlow.Regime.TickStream;
			else if (FlowTickSource == EFlowTickSource.Iog && Environment.IOGEnabled)
				regime = realTime ? _OrderFlow.Regime.IogRealTime : (IogMagnifierOneTick ? _OrderFlow.Regime.IogMagnifierOneTick : _OrderFlow.Regime.IogHistoryNoMagnifier);
			else regime = _OrderFlow.Regime.BarsOnly;

			int bar = Bars.FullSymbolData.Current;
			if (m_chunks != null)
			{
				pumpTickChunks();
				if (FlowCfg.TickChunkWaitMs > 0 && regime == _OrderFlow.Regime.TickStream && Bars.Status == EBarState.Close) tickChunkWait();
			}
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
				DateTime et = ToEastern(Bars.Time[0]);
				// DataLoader history not delivered yet: the bar cannot be built from ticks (gap → BarUpDown or Unavailable).
				// Tick-chunk spec §5.3: chunked, the bar's day (every stream) must be loaded and released.
				if (regime == _OrderFlow.Regime.TickStream && (m_chunks != null ? !m_chunks.ReadyAt(et) : !m_flowHistoryDone[0])) regime = _OrderFlow.Regime.BarsOnly;
				m_lastClosedEt = et;
				DateTime gx = globexDateEt(et);
				DateTime reset = FlowCfg.ResetMode == _OrderFlow.ResetMode.Rth ? (IsRthValueBarEt(et) ? gx : gx.AddTicks(1)) : gx;
				_OrderFlow.FootprintBar closedFp = m_flow.CloseBar(new _OrderFlow.BarInput
				{
					BarNumber = bar, CloseTime = Bars.Time[0], OpenTime = barOpenTime(), SessionKey = gx, ResetKey = reset, Regime = regime,
					High = Bars.High[0], Low = Bars.Low[0], Close = Bars.Close[0], Volume = Bars.Volume[0], UpTicks = Bars.UpTicks[0], DownTicks = Bars.DownTicks[0]
				});
				mcJournal(closedFp);
			}
		}

		// Spec §7.3: the bar's open = the previous bar's close stamp; the first bar: close − duration (minute bars), else no bound.
		private DateTime barOpenTime()
		{
			if (Bars.CurrentBar > 1) return Bars.Time[1];
			Resolution r = Bars.Request.Resolution;
			return r.Type == EResolution.Minute ? Bars.Time[0] - r.Duration() : DateTime.MinValue;
		}

		// Tick-chunk spec §5.4 (TickChunkWaitMs > 0): at the first bar of each planned Globex day, wait (bounded) for that day's
		// chunks. Calc thread, outside m_flowLock. PROBE P-RC7: callbacks are delivered while the calc thread sleeps.
		private void tickChunkWait()
		{
			DateTime et = ToEastern(Bars.Time[0]);
			DateTime gx = globexDateEt(et);
			if (gx == m_waitGx) return;
			m_waitGx = gx;
			bool ready;
			lock (m_flowLock) ready = m_chunks.ReadyAt(et);
			if (ready || gx < m_chunks.FirstDay || gx > m_chunks.LastDay) return;
			var sw = System.Diagnostics.Stopwatch.StartNew();
			while (!ready && sw.ElapsedMilliseconds < FlowCfg.TickChunkWaitMs)
			{
				System.Threading.Thread.Sleep(50);
				pumpTickChunks();
				lock (m_flowLock) ready = m_chunks.ReadyAt(et);
			}
			if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, $"OF tick chunk wait day({gx:yyyy-MM-dd}) ms({sw.ElapsedMilliseconds}) ready({ready})");
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