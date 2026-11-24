using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 S4 — the VPF-4 roll detector, extracted from _VolumeProfileFuturesBase so _CustomSignal (any base) can detect
	// rolls. Logic unchanged: a roll is
	//   Series: the first bar of a new trading day whose open gaps >= gapTicks x tick from the prior close, inside the roll
	//           window of the next quarterly contract (SeriesWindow: Monday..Friday of the week holding the customary roll
	//           date), AT MOST ONCE per contract (the first qualifying gap wins), or a change in the instrument name;
	//           (fix-i: was days 1-21 of Mar/Jun/Sep/Dec, every qualifying gap. An 8-tick ES threshold is below an ordinary
	//           weekend gap, so every Sunday open of the window passed: 72 detections for ~33 rolls on 1-min @ES 2016-2024.)
	//   Table:  the first bar of a trading day listed in dates (T-R1: the day-start gap is exposed for the confirmation journal).
	// The name change is checked on every mode (as before). Step is idempotent per bar number. Pure C# (tests/RollTests).
	// ============================================================================================

	// <roll-detector>
	public sealed class _RollDetector
	{
		public bool series;                 // Series or SeriesOrTable
		public bool table;                  // Table or SeriesOrTable
		public int gapTicks = 8;
		public ICollection<DateTime> dates; // Table: trading dates on which the continuous series switched contract

		/// <summary>M5: Series window. Null keeps the ES quarterly <see cref="SeriesWindow"/> (RollTests, and a host with no profile
		/// bound). The host sets it from the Data1 profile's contract months / expiry / roll (<c>InstrumentRules.SeriesWindow</c>).</summary>
		public delegate bool SeriesWindowFn(DateTime tradingDay, out DateTime expiry);
		public SeriesWindowFn seriesWindow;

		private readonly List<int> m_rollBars = new List<int>();
		private DateTime m_lastDay = DateTime.MinValue;
		private int m_lastBar = -1;
		private string m_lastName;
		private DateTime m_seriesLatch = DateTime.MinValue;   // third Friday of the contract whose Series splice was already taken

		/// <summary>PR #51 review: open, prior close and |open - prior close| (points) of the bar of the last recorded roll when
		/// Series took it (NaN when the roll came only from a Table date or a name change).</summary>
		public double LastOpen = double.NaN, LastPrevClose = double.NaN, LastGap = double.NaN;

		// Coder 2026-11-23 (Analyst MES run: no 2024-06 splice where ES had one): a Series window that closes without a splice is
		// reported once (MissedExpiry set on the first day after the window), with the largest first-bar gap of the last ten
		// trading-day starts (the window and the days before it), so the data can be checked. Detection itself is unchanged.
		public DateTime MissedExpiry = DateTime.MinValue, MissedGapDay = DateTime.MinValue;
		public double MissedGap = double.NaN;
		private DateTime m_openWindow = DateTime.MinValue;
		private readonly List<KeyValuePair<DateTime, double>> m_dayGaps = new List<KeyValuePair<DateTime, double>>();

		/// <summary>T-R1 (m-instr §7.1 items 2-4, Confirmed by Ettienne 2026-11-22): facts of the trading-day start seen by the last
		/// Step call. DayStart is true only on the first bar of a new trading day (not the chart's first day); DayGap =
		/// |open - prior close| in points; DayInTable = Table is on and the day is in dates (that bar is a Table roll). The host
		/// journals TableSpliceLine on a Table roll bar and OffTableWarning on any other day start. Detection is unchanged.</summary>
		public bool DayStart, DayInTable;
		public DateTime DayStartDay = DateTime.MinValue;
		public double DayGap = double.NaN;

		/// <summary>Bars (as passed to Step) on which a roll was detected, oldest first.</summary>
		public IList<int> RollBars { get { return m_rollBars.AsReadOnly(); } }
		public int LastRollBar { get { return m_rollBars.Count > 0 ? m_rollBars[m_rollBars.Count - 1] : -1; } }

		public void Reset() { m_rollBars.Clear(); m_lastDay = DateTime.MinValue; m_lastBar = -1; m_lastName = null; m_seriesLatch = DateTime.MinValue; LastOpen = LastPrevClose = LastGap = double.NaN; DayStart = DayInTable = false; DayStartDay = DateTime.MinValue; DayGap = double.NaN; MissedExpiry = MissedGapDay = m_openWindow = DateTime.MinValue; MissedGap = double.NaN; m_dayGaps.Clear(); }

		/// <summary>Same cut-over as EventRules.RollRuleMondayFrom (this file must stay MC- and EventRules-free for tests/RollTests).</summary>
		public static readonly DateTime RollRuleMondayFrom = new DateTime(2022, 4, 1);

		/// <summary>Third Friday of month m.</summary>
		public static DateTime ThirdFriday(int y, int m)
		{
			DateTime d = new DateTime(y, m, 1);
			while (d.DayOfWeek != DayOfWeek.Friday) d = d.AddDays(1);
			return d.AddDays(14);
		}

		/// <summary>Series candidate window for the quarterly contract expiring next on/after tradingDay: [Monday of the week holding
		/// the customary roll date (EventRules.RollDate: Monday before the third Friday from 2022-04-01, else the Thursday eight days
		/// before it), Friday of that week (PR #51 B1; 2022+ = the third Friday)]. tradingDay is a Globex trading day (Sunday 18:00 ET open = Monday). Returns false outside it;
		/// expiry = that contract's third Friday (the per-contract latch key).</summary>
		public static bool SeriesWindow(DateTime tradingDay, out DateTime expiry)
		{
			DateTime d = tradingDay.Date;
			int m = ((d.Month - 1) / 3 + 1) * 3, y = d.Year;
			expiry = ThirdFriday(y, m);
			if (d > expiry) { m += 3; if (m > 12) { m = 3; y++; } expiry = ThirdFriday(y, m); }
			DateTime rule = expiry >= RollRuleMondayFrom ? expiry.AddDays(-4) : expiry.AddDays(-8);
			DateTime from = rule.AddDays(-(((int)rule.DayOfWeek + 6) % 7));   // Monday of the rule's week
			// PR #51 B1: end on the Friday of the rule week (2022+: = expiry). Pre-2022 the window used to run on into expiry week and
			// took a weekend gap there when the rule week had no splice gap (U18/U19/Z19/U21 a week late): no roll beats a wrong one.
			return d >= from && d <= from.AddDays(4);
		}

		/// <summary>PR #46 review: digest of a roll-date set for the config hash (RegimeGate host.rollDates). FNV-1a 64 over the UTF-8
		/// of the distinct dates sorted ascending, each as "yyyy-MM-dd;"; 16 lower-case hex digits. Order- and duplicate-insensitive.</summary>
		public static string DatesDigest(IEnumerable<DateTime> dates)
		{
			var l = new List<DateTime>(); if (dates != null) foreach (var d in dates) if (!l.Contains(d.Date)) l.Add(d.Date);
			l.Sort();
			var sb = new System.Text.StringBuilder();
			foreach (var d in l) sb.Append(d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
			ulong h = 14695981039346656037UL;
			unchecked { foreach (byte b in System.Text.Encoding.UTF8.GetBytes(sb.ToString())) { h ^= b; h *= 1099511628211UL; } }   // FNV-1a wraps mod 2^64 by design
			return h.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
		}

		/// <summary>T-R1: g > k x A. False while A is NaN (ATR warm-up) or not positive.</summary>
		public static bool GapConfirmed(double gap, double atr, double k)
		{
			return !double.IsNaN(atr) && atr > 0 && !double.IsNaN(gap) && gap > k * atr;
		}

		static string num(double v, string fmt) { return double.IsNaN(v) ? "NaN" : v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }

		/// <summary>Owner ruling 2026-11-23 11:30 CT: the Journal line on every Table roll bar (the table-date reopen). The roll EVENT is
		/// here (rollOn); the price offset is placed later by <see cref="RollSpliceLocator"/> and journaled by SpliceLine. reopenGapPts =
		/// |open - prior close| of the reopen bar; gapAtr = that / A (NaN during the ATR(20) warm-up). Replaces the T-R1 confirmed(k) line:
		/// k = 1.0 missed most real splices (Analyst T-R4, fabf1bd: roll gaps 0.49-1.35 x A, never confirmed on NQ / MNQ).</summary>
		public static string TableRollLine(DateTime tradingDay, double gap, double atr, double minAtr)
		{
			bool warm = !double.IsNaN(atr) && atr > 0;
			return "ROLL table roll(" + tradingDay.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ") reopenGapPts(" + num(gap, "0.00")
				+ ") atr20(" + (warm ? num(atr, "0.00") : "NaN") + ") gapAtr(" + (warm ? num(gap / atr, "0.000") : "NaN") + ") minAtr(" + num(minAtr, "0.0##")
				+ ") offset(search: first bar to the end of the next session with |open - prior close| >= minAtr x atr20; else fallback)";
		}

		/// <summary>The Journal line on the bar where the roll price offset is applied (located splice or fallback).</summary>
		public static string SpliceLine(RollSpliceLocator r, double tick)
		{
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			bool warm = !double.IsNaN(r.Atr) && r.Atr > 0;
			string via = RollSpliceLocator.ViaName(r.Via);
			return "ROLL table splice(" + r.Table.ToString("yyyy-MM-dd", inv) + ") at(" + r.BarTime.ToString("yyyy-MM-dd HH:mm", inv) + ") via(" + via + ") session(+" + r.Session.ToString(inv)
				+ ") gapBar(" + (r.GapTime == DateTime.MinValue ? "-" : r.GapTime.ToString("yyyy-MM-dd HH:mm", inv)) + ") gapPts(" + (double.IsNaN(r.Gap) ? "NaN" : r.Gap.ToString("+0.00;-0.00;0.00", inv))
				+ ") gapTicks(" + (double.IsNaN(r.Gap) || !(tick > 0) ? "NaN" : (r.Gap / tick).ToString("+0;-0;0", inv)) + ") atr20(" + (warm ? num(r.Atr, "0.00") : "NaN")
				+ ") gapAtr(" + (warm && !double.IsNaN(r.Gap) ? num(Math.Abs(r.Gap) / r.Atr, "0.000") : "NaN") + ") minAtr(" + num(r.minAtr, "0.0##") + ") found(" + (r.Found ? "1" : "0") + ")"
				+ (r.Via == RollSpliceLocator.EVia.IntrabarBody ? " test(close - prevClose) openGapPts(" + (double.IsNaN(r.OpenGap) ? "NaN" : r.OpenGap.ToString("+0.00;-0.00;0.00", inv)) + ") mixedBar(1)" : "");
		}

		/// <summary>Owner ruling 2026-11-23 13:31 CT: the Journal line when a late splice fires the engine resets a second time.</summary>
		public static string ResetAtSpliceLine(RollSpliceLocator r)
		{
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			string via = RollSpliceLocator.ViaName(r.Via);
			return "ROLL table reset-at-splice(" + r.Table.ToString("yyyy-MM-dd", inv) + ") at(" + r.BarTime.ToString("yyyy-MM-dd HH:mm", inv) + ") via(" + via + ") session(+" + r.Session.ToString(inv)
				+ ") rollEvent(" + (r.ArmTime == DateTime.MinValue ? "-" : r.ArmTime.ToString("yyyy-MM-dd HH:mm", inv)) + ") engines(AVWAP VWAP PRICEACTION PROFILE)";
		}

		/// <summary>T-R1 (m-instr §7.1 item 3): the Warning text for a day start that is not a Table date and gaps more than k x A,
		/// else null. Null during the ATR warm-up (the check is skipped). Never a roll.</summary>
		public static string OffTableWarning(DateTime tradingDay, double gap, double atr, double k)
		{
			if (!GapConfirmed(gap, atr, k)) return null;
			return "ROLL off-table gap(" + tradingDay.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ") gapPts(" + num(gap, "0.00")
				+ ") atr20(" + num(atr, "0.00") + ") gapAtr(" + num(gap / atr, "0.000") + ") k(" + num(k, "0.0##") + ")";
		}

		/// <summary>Educator review R1: Series tests only the first bar of each trading-day key. On an INTRADAY chart keyed by
		/// calendar date that bar is the one after midnight, so the 18:00 ET Globex open gap is never tested (Series is
		/// effectively blind). Daily and longer bars are fine. Returns the warning text, or null when there is nothing to warn.
		/// intraday = the caller's typed EResolution test (_CustomSignal.isIntradayResolution; no enum names as strings here);
		/// resolutionLabel is display text only; calendarKeyed = tradingDayKey is the bar's calendar date.</summary>
		public static string SeriesKeyWarning(bool series, bool intraday, string resolutionLabel, bool calendarKeyed)
		{
			// fix-i: _CustomSignal keys Series by the Globex trading day on futures charts itself and passes calendarKeyed = false
			if (!series || !calendarKeyed) return null;
			if (!intraday) return null;
			return "RollDetection Series on an intraday chart (" + resolutionLabel + ") keyed by calendar date: Series only tests the first bar after midnight "
				+ "and misses the Globex-open roll gap; use RollDetection = Table (RollDates) or key days by the Globex trading day "
				+ "(override tradingDayKey with _CmeSessionClock.GlobexTradingDayEt: ET >= 18:00 belongs to the next date)";
		}

		/// <summary>Feed one CLOSED bar. Returns true when this call recorded a roll on `bar` (false on a repeat call for the same bar).
		/// prevClose is the prior bar's close (ignored on bar 1).</summary>
		public bool Step(int bar, DateTime tradingDay, double open, double prevClose, string name, double tick)
		{
			if (bar == m_lastBar) return false;
			m_lastBar = bar;
			DayStart = DayInTable = false;
			bool newDay = tradingDay != m_lastDay;
			bool roll = m_lastName != null && name != m_lastName, bySeries = false;
			if (newDay && m_lastDay != DateTime.MinValue && bar > 1)
			{
				DayStart = true; DayStartDay = tradingDay.Date; DayGap = Math.Abs(open - prevClose);
				if (table && dates != null && dates.Contains(tradingDay.Date)) { roll = true; DayInTable = true; }
				DateTime expiry;
				bool inWindow = seriesWindow != null ? seriesWindow(tradingDay, out expiry) : SeriesWindow(tradingDay, out expiry);
				if (series && gapTicks > 0 && inWindow && expiry != m_seriesLatch
					&& Math.Abs(open - prevClose) >= gapTicks * tick - tick * 1e-6) { roll = true; bySeries = true; m_seriesLatch = expiry; }
				if (series && gapTicks > 0)
				{
					m_dayGaps.Add(new KeyValuePair<DateTime, double>(tradingDay.Date, Math.Abs(open - prevClose)));
					if (m_dayGaps.Count > 10) m_dayGaps.RemoveAt(0);
					if (!inWindow && m_openWindow != DateTime.MinValue && m_openWindow != m_seriesLatch)
					{
						MissedExpiry = m_openWindow; MissedGap = double.NaN; MissedGapDay = DateTime.MinValue;
						for (int i = 0; i < m_dayGaps.Count - 1; i++)   // not today's (after the window)
							if (double.IsNaN(MissedGap) || m_dayGaps[i].Value > MissedGap) { MissedGap = m_dayGaps[i].Value; MissedGapDay = m_dayGaps[i].Key; }
					}
					m_openWindow = inWindow ? expiry : DateTime.MinValue;
				}
			}
			bool added = false;
			if (roll && (m_rollBars.Count == 0 || m_rollBars[m_rollBars.Count - 1] != bar)) {
				m_rollBars.Add(bar); added = true;
				if (bySeries) { LastOpen = open; LastPrevClose = prevClose; LastGap = Math.Abs(open - prevClose); } else LastOpen = LastPrevClose = LastGap = double.NaN;
			}
			m_lastName = name;
			m_lastDay = tradingDay;
			return added;
		}
	}

	/// <summary>Phase 3 S4: pending roll for a Data n stream (same root as Data1). Arm at each Data1 roll with its bar time;
	/// Fire reports true on the first closed bar of the stream with time >= that time (and stays true on repeat calls for that bar).</summary>
	public sealed class _RollPending
	{
		private DateTime m_armed = DateTime.MaxValue;
		private int m_firedBar = int.MinValue;
		public bool Armed { get { return m_armed != DateTime.MaxValue; } }
		public void Arm(DateTime data1RollTime) { if (data1RollTime < m_armed) m_armed = data1RollTime; }
		public bool Fire(int bar, DateTime time)
		{
			if (bar == m_firedBar) return true;
			if (!Armed || time < m_armed) return false;
			m_armed = DateTime.MaxValue; m_firedBar = bar;
			return true;
		}
		public void Reset() { m_armed = DateTime.MaxValue; m_firedBar = int.MinValue; }
	}

	/// <summary>
	/// M9. Series gap in ticks = round-half-away-from-zero(C × Wilder ATR(20) / tick). The true range is
	/// _DailyContext's: max(high, previous close) − min(low, previous close). The seed is the average of the first
	/// 20 true ranges; after that one Wilder update per completed day. The first completed day has no previous close
	/// and contributes no true range. Splice days are not skipped and the seed does not restart (the series-b fit).
	/// C = 8 × 0.25 / A, where A is the higher of the two central ATR values on RollTests series b (index n/2 of the
	/// sorted sample). The host keeps RollGapTicks (8) unless <see cref="Apply"/> is set. No study sets it.
	/// </summary>
	public sealed class RollGapCalibrator
	{
		public const int AtrLength = 20;
		public const int InUseTicks = 8;
		/// <summary>8 × 0.25 / the series-b median ATR. Locked by RollTests after the fit; not a rounded 0.893.</summary>
		public const double C = 0.8933716847476064;

		/// <summary>M9 opt-in. Default false: the calibrated tick count is not written onto a detector.</summary>
		public bool Apply;

		private DateTime m_day = DateTime.MinValue;
		private double m_high, m_low, m_close, m_prevClose, m_seed, m_atr = double.NaN;
		private bool m_open, m_havePrev;
		private int m_trSeen;

		public int TrCount { get { return m_trSeen; } }
		public double Atr { get { return m_trSeen >= AtrLength ? m_atr : double.NaN; } }

		public void Reset()
		{
			m_day = DateTime.MinValue; m_high = m_low = m_close = m_prevClose = m_seed = 0; m_atr = double.NaN;
			m_open = m_havePrev = false; m_trSeen = 0;
		}

		/// <summary>One closed bar of a trading day. High and low are that bar's high and low (series b passes max(open, close) and min(open, close)).</summary>
		public void AddBar(DateTime tradingDay, double high, double low, double close)
		{
			if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close) || double.IsInfinity(high) || double.IsInfinity(low) || double.IsInfinity(close)) return;
			DateTime d = tradingDay.Date;
			if (!m_open) { m_day = d; m_high = high; m_low = low; m_close = close; m_open = true; return; }
			if (d == m_day)
			{
				if (high > m_high) m_high = high;
				if (low < m_low) m_low = low;
				m_close = close;
				return;
			}
			if (d < m_day) return;
			Commit();
			m_day = d; m_high = high; m_low = low; m_close = close; m_open = true;
		}

		/// <summary>Close the day still open (end of a series). The host does not call this: the next day's first bar commits the previous day, and the log fires then.</summary>
		public void Flush() { if (m_open) { Commit(); m_open = false; } }

		/// <summary>Calibrated tick count, or −1 when ATR or tick is not usable.</summary>
		public static int Ticks(double atr, double tick)
		{
			if (!(atr > 0) || !(tick > 0) || double.IsInfinity(atr) || double.IsInfinity(tick)) return -1;
			double x = C * atr / tick;
			if (double.IsNaN(x) || double.IsInfinity(x) || x <= 0 || x >= int.MaxValue) return -1;
			int n = (int)Math.Round(x, 0, MidpointRounding.AwayFromZero);
			return n > 0 ? n : -1;
		}

		private void Commit()
		{
			double close = m_close, high = m_high, low = m_low;
			if (!m_havePrev) { m_prevClose = close; m_havePrev = true; return; }
			double tr = Math.Max(high, m_prevClose) - Math.Min(low, m_prevClose);
			m_prevClose = close;
			m_trSeen++;
			if (m_trSeen <= AtrLength) { m_seed += tr; if (m_trSeen == AtrLength) m_atr = m_seed / AtrLength; }
			else m_atr = (m_atr * (AtrLength - 1) + tr) / AtrLength;
		}
	}
	/// <summary>
	/// Owner ruling 2026-11-23 11:30 CT (#155, after Analyst's T-R4 run on fabf1bd): in Table mode the roll price offset is applied on
	/// the FIRST bar, from the table-date reopen on, whose gap g = open - prior close satisfies |g| >= minAtr x A, where A is the carried
	/// Globex-day ATR(20) (RollGapCalibrator.Atr, the T-R1 / T-R4 A). Reopen and in-session gaps both count. If no bar qualifies by
	/// the end of the next session, the offset is applied on the first bar after it (the earliest causal "end of the next session"),
	/// measured as the largest |g| seen in the window (signed): Coder's choice, because it is the splice whenever the spread is real but
	/// small (pre-2022 carry, warm-up), and a spread model needs rates / dividends the host does not have.
	/// Causal: one bar at a time, never a later bar. Arm on the table reopen bar BEFORE stepping that bar. Step is idempotent per bar.
	/// While A is NaN (warm-up) no bar qualifies, so the window ends in the fallback.
	/// Owner ruling 2026-11-23 18:46 CT: on intraday Data1 slower than 1 minute (bodyCheck) a bar whose open gap does not qualify is
	/// also tested on its close - prevClose (same minAtr x A): the switch happened inside the bar (Via IntrabarBody, a mixed bar).
	/// 1-minute and faster Data1, tick / volume / range bars and Day-or-slower bars keep the open-gap test only.
	/// </summary>
	public sealed class RollSpliceLocator
	{
		public const double DefaultMinAtr = 0.4;
		/// <summary>IntrabarBody (owner ruling 2026-11-23 18:46 CT): Data1 slower than 1 minute only; the switch happened INSIDE the bar
		/// (|open - prevClose| below the threshold, |close - prevClose| at or above it). The bar mixes both contracts.</summary>
		public enum EVia { None = 0, Reopen = 1, Intrabar = 2, Fallback = 3, IntrabarBody = 4 }

		/// <summary>Journal name of a via: reopen / intrabar / fallback / intrabar-body / none.</summary>
		public static string ViaName(EVia v)
		{
			switch (v)
			{
				case EVia.Reopen: return "reopen";
				case EVia.Intrabar: return "intrabar";
				case EVia.Fallback: return "fallback";
				case EVia.IntrabarBody: return "intrabar-body";
				default: return "none";
			}
		}

		/// <summary>Owner ruling 2026-11-23 18:46 CT: also test the mid-bar jump close - prevClose when the open gap does not qualify.
		/// The host sets it only for intraday Data1 slower than 1 minute (Minute size &gt; 1, Hour); never for 1-minute or faster,
		/// tick / volume / range bars, or Day and slower bars. Default false = the open-gap test only.</summary>
		public bool bodyCheck;

		/// <summary>Qualifying gap in multiples of A (inclusive).</summary>
		public double minAtr = DefaultMinAtr;

		private bool m_armed;
		private DateTime m_curDay = DateTime.MinValue;
		private int m_sessions, m_lastBar = -1, m_bestSession;
		private double m_bestGap = double.NaN, m_bestAtr = double.NaN;
		private DateTime m_bestTime = DateTime.MinValue;

		/// <summary>Result of the last Step that returned true (Via != None only on that bar).</summary>
		public EVia Via;
		public DateTime Table = DateTime.MinValue, BarTime = DateTime.MinValue, GapTime = DateTime.MinValue;
		public double Gap = double.NaN, Atr = double.NaN;
		/// <summary>IntrabarBody only: the bar's open gap (open - prevClose), which did not qualify; NaN otherwise.</summary>
		public double OpenGap = double.NaN;
		/// <summary>Session of the offset bar relative to the table session: 0 = table session, 1 = next, 2 = fallback on the session after.</summary>
		public int Session;
		/// <summary>True when a bar qualified (|g| >= minAtr x A); false for the fallback. Replaces the T-R1 k = 1.0 confirmed flag.</summary>
		public bool Found;
		public bool Armed { get { return m_armed; } }
		/// <summary>Bar time of the table reopen that armed the search (the roll event), DateTime.MinValue if not given.</summary>
		public DateTime ArmTime = DateTime.MinValue;
		/// <summary>Owner ruling 2026-11-23 13:31 CT: the AVWAP / VWAP / PRICEACTION / profile resets fire again on the offset bar when it
		/// is NOT the table reopen bar itself (intrabar, a later session's reopen, or the fallback). An aligned reopen splice (Reopen,
		/// session 0) is the roll bar: no second reset. Valid on the bar Step returned true.</summary>
		public bool ResetsAgain { get { return Via != EVia.None && !(Via == EVia.Reopen && Session == 0); } }

		public void Reset()
		{
			m_armed = false; m_curDay = DateTime.MinValue; m_sessions = 0; m_lastBar = -1; m_bestSession = 0; m_bestGap = m_bestAtr = double.NaN; m_bestTime = DateTime.MinValue;
			Via = EVia.None; Table = BarTime = GapTime = ArmTime = DateTime.MinValue; Gap = Atr = OpenGap = double.NaN; Session = 0; Found = false;
		}

		/// <summary>Open the search on the table reopen bar (call before Step for that bar). A window still open is replaced.</summary>
		public void Arm(DateTime tableDay, DateTime armTime = default(DateTime))
		{
			ArmTime = armTime;
			m_armed = true; Table = tableDay.Date; m_curDay = tableDay.Date; m_sessions = 0;
			m_bestGap = m_bestAtr = double.NaN; m_bestTime = DateTime.MinValue; m_bestSession = 0;
		}

		/// <summary>True when the roll price offset applies on this bar; Via / Gap / Atr / Session / Found describe it. prevClose = the
		/// prior bar's close; atr = A on this bar; dayStart = this is the first bar of its trading day.</summary>
		public bool Step(int bar, DateTime tradingDay, DateTime barTime, double open, double prevClose, double atr, bool dayStart)
		{
			return Step(bar, tradingDay, barTime, open, prevClose, atr, dayStart, double.NaN);
		}

		/// <summary>Step with the bar's close: with bodyCheck, a bar whose open gap does not qualify but whose close - prevClose does
		/// is the splice (Via IntrabarBody, Gap = close - prevClose = the net jump from the last old-contract close).</summary>
		public bool Step(int bar, DateTime tradingDay, DateTime barTime, double open, double prevClose, double atr, bool dayStart, double close)
		{
			if (bar == m_lastBar) return false;
			m_lastBar = bar;
			Via = EVia.None;
			if (!m_armed) return false;
			DateTime d = tradingDay.Date;
			if (d != m_curDay) { m_curDay = d; if (d > Table) m_sessions++; }
			if (m_sessions >= 2)
			{
				m_armed = false; Via = EVia.Fallback; Found = false; Session = 2; BarTime = barTime;
				Gap = double.IsNaN(m_bestGap) ? 0 : m_bestGap; GapTime = m_bestTime; Atr = m_bestAtr; OpenGap = double.NaN;
				return true;
			}
			double g = double.IsNaN(open) || double.IsNaN(prevClose) ? double.NaN : open - prevClose;
			if (double.IsNaN(g)) return false;
			if (Qualifies(g, atr, minAtr))
			{
				m_armed = false; Via = dayStart ? EVia.Reopen : EVia.Intrabar; Found = true; Session = m_sessions;
				Gap = g; Atr = atr; BarTime = GapTime = barTime; OpenGap = double.NaN;
				return true;
			}
			if (bodyCheck && !double.IsNaN(close))
			{
				double gb = close - prevClose;
				if (Qualifies(gb, atr, minAtr))
				{
					m_armed = false; Via = EVia.IntrabarBody; Found = true; Session = m_sessions;
					Gap = gb; OpenGap = g; Atr = atr; BarTime = GapTime = barTime;
					return true;
				}
			}
			if (double.IsNaN(m_bestGap) || Math.Abs(g) > Math.Abs(m_bestGap)) { m_bestGap = g; m_bestTime = barTime; m_bestAtr = atr; m_bestSession = m_sessions; }
			return false;
		}

		/// <summary>|g| >= minAtr x A (inclusive, 1e-9 relative tolerance); false while A is NaN or not positive.</summary>
		public static bool Qualifies(double g, double atr, double minAtr)
		{
			return !double.IsNaN(g) && !double.IsNaN(atr) && atr > 0 && Math.Abs(g) >= minAtr * atr * (1 - 1e-9);
		}
	}

	/// <summary>
	/// T-R4 evidence (specs/sandbox-roll-floor-migration.md T-R4; owner ruling 2026-11-23: run it before #155 merges). Diagnostic
	/// only: never a roll, never an order, no effect on detection. For each Table date it collects the trading days around it
	/// (<see cref="Sessions"/> before and after, counted in sessions the chart actually has) and, per day, the session-start gap
	/// (open of the day's first bar minus the prior close: the Globex reopen) and the largest bar-to-bar gap inside the day (open
	/// minus prior close of every later bar, with its bar time). Each is printed in points, ticks and multiples of the day's
	/// Globex ATR(20) (the T-R1 A). The day's candidate is the larger of the two absolute gaps. When the window is complete it
	/// prints one line per day and a verdict:
	///   aligned                    the largest candidate is on the table date and is >= StandoutRatio x the median of the others;
	///   data splice on yyyy-MM-dd  the largest candidate is on another day of the window and stands out by the same test;
	///   no visible splice          no day stands out (the ratio is below StandoutRatio).
	/// Pure C# (tests/RollTests). The host feeds one closed Data1 bar per AddBar call and calls Finish on the last bar.
	/// </summary>
	public sealed class RollEvidence
	{
		/// <summary>Sessions before and after the table date (the ruling's +-5).</summary>
		public const int Sessions = 5;
		/// <summary>Diagnostic threshold for "stands out": largest candidate / median of the other days' candidates.</summary>
		public const double StandoutRatio = 2.0;

		public ICollection<DateTime> dates;
		public double tick = 0.25;
		public double k = 1.0;

		public sealed class Day
		{
			public DateTime day, startTime = DateTime.MinValue, barTime = DateTime.MinValue;
			public double startGap = double.NaN, barGap = double.NaN, atr = double.NaN;
			/// <summary>Larger of |startGap| and |barGap| (NaN when neither is known).</summary>
			public double Candidate
			{
				get
				{
					double a = double.IsNaN(startGap) ? double.NaN : Math.Abs(startGap), b = double.IsNaN(barGap) ? double.NaN : Math.Abs(barGap);
					if (double.IsNaN(a)) return b;
					if (double.IsNaN(b)) return a;
					return Math.Max(a, b);
				}
			}
			/// <summary>True when the candidate is the session-start gap (ties go to the start).</summary>
			public bool AtStart { get { return !double.IsNaN(startGap) && (double.IsNaN(barGap) || Math.Abs(startGap) >= Math.Abs(barGap)); } }
		}

		sealed class Pending { public DateTime table; public List<Day> before = new List<Day>(); public Day target; public List<Day> after = new List<Day>(); }

		private readonly List<Day> m_recent = new List<Day>();   // the last Sessions completed days
		private readonly List<Pending> m_pending = new List<Pending>();
		private Day m_cur;
		private int m_aligned, m_offDate, m_none, m_incomplete;

		/// <summary>Verdict counts so far: aligned, data splice on another date, no visible splice, incomplete windows (Finish).</summary>
		public int Aligned { get { return m_aligned; } }
		public int OffDate { get { return m_offDate; } }
		public int NoVisible { get { return m_none; } }
		public int Incomplete { get { return m_incomplete; } }

		public void Reset() { m_recent.Clear(); m_pending.Clear(); m_cur = null; m_aligned = m_offDate = m_none = m_incomplete = 0; }

		/// <summary>One closed bar. tradingDay = the detector's day key; prevClose = the prior bar's close (NaN on the first bar);
		/// atr = the T-R1 A on this bar (ATR(20) of the completed days before it, NaN during warm-up). Lines are appended to output.</summary>
		public void AddBar(DateTime tradingDay, DateTime barTime, double open, double prevClose, double atr, List<string> output)
		{
			DateTime d = tradingDay.Date;
			double g = double.IsNaN(prevClose) || double.IsNaN(open) ? double.NaN : open - prevClose;
			if (m_cur == null || d != m_cur.day)
			{
				if (m_cur != null) complete(m_cur, output);
				m_cur = new Day { day = d, startTime = barTime, startGap = m_cur == null ? double.NaN : g, atr = atr };
				if (dates != null && dates.Contains(d))
				{
					var p = new Pending { table = d, target = m_cur };
					p.before.AddRange(m_recent);
					m_pending.Add(p);
				}
				return;
			}
			if (!double.IsNaN(g) && (double.IsNaN(m_cur.barGap) || Math.Abs(g) > Math.Abs(m_cur.barGap))) { m_cur.barGap = g; m_cur.barTime = barTime; }
		}

		/// <summary>End of data: completes the open day and reports every window still pending (marked incomplete), then a summary line.</summary>
		public void Finish(List<string> output)
		{
			if (m_cur != null) { complete(m_cur, output); m_cur = null; }
			foreach (Pending p in m_pending) { m_incomplete++; report(p, output, true); }
			m_pending.Clear();
			output.Add(string.Format(Inv, "ROLL T-R4 summary aligned({0}) dataSpliceOtherDay({1}) noVisibleSplice({2}) incomplete({3}) sessions(+-{4}) standout({5:0.0#}) k({6:0.0##})",
				m_aligned, m_offDate, m_none, m_incomplete, Sessions, StandoutRatio, k));
		}

		private void complete(Day day, List<string> output)
		{
			for (int i = 0; i < m_pending.Count; i++)
			{
				Pending p = m_pending[i];
				if (day != p.target && day.day > p.table) p.after.Add(day);
				if (p.after.Count >= Sessions) { report(p, output, false); m_pending.RemoveAt(i); i--; }
			}
			m_recent.Add(day);
			if (m_recent.Count > Sessions) m_recent.RemoveAt(0);
		}

		static readonly IFormatProvider Inv = System.Globalization.CultureInfo.InvariantCulture;
		static string D(DateTime d) { return d.ToString("yyyy-MM-dd", Inv); }
		static string T(DateTime t) { return t == DateTime.MinValue ? "-" : t.ToString("yyyy-MM-dd HH:mm", Inv); }

		/// <summary>"pts +12.25 ticks +49 atr 0.183" (signed points and ticks; atr = |gap| / A, NaN during warm-up).</summary>
		public string Gap(double g, double atr)
		{
			if (double.IsNaN(g)) return "pts NaN ticks NaN atr NaN";
			string ticks = tick > 0 ? (g / tick).ToString("+0;-0;0", Inv) : "NaN";
			string a = !double.IsNaN(atr) && atr > 0 ? (Math.Abs(g) / atr).ToString("0.000", Inv) : "NaN";
			return "pts " + g.ToString("+0.00;-0.00;0.00", Inv) + " ticks " + ticks + " atr " + a;
		}

		/// <summary>Median of the values (NaN for none).</summary>
		public static double Median(List<double> v)
		{
			if (v == null || v.Count == 0) return double.NaN;
			var s = new List<double>(v); s.Sort();
			int n = s.Count;
			return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2.0;
		}

		/// <summary>The verdict for a window: index of the largest-candidate day (-1 when none is known) and the standout ratio.</summary>
		public static string Verdict(List<Day> window, Day target, out int largest, out double ratio)
		{
			largest = -1; ratio = double.NaN;
			for (int i = 0; i < window.Count; i++)
			{
				double c = window[i].Candidate;
				if (double.IsNaN(c)) continue;
				if (largest < 0 || c > window[largest].Candidate) largest = i;
			}
			if (largest < 0) return "no visible splice";
			var others = new List<double>();
			for (int i = 0; i < window.Count; i++) if (i != largest && !double.IsNaN(window[i].Candidate)) others.Add(window[i].Candidate);
			double med = Median(others), top = window[largest].Candidate;
			ratio = double.IsNaN(med) ? double.NaN : med > 0 ? top / med : (top > 0 ? double.PositiveInfinity : double.NaN);
			if (!(ratio >= StandoutRatio)) return "no visible splice";
			return window[largest] == target ? "aligned" : "data splice on " + D(window[largest].day);
		}

		private void report(Pending p, List<string> output, bool incomplete)
		{
			var w = new List<Day>(p.before); w.Add(p.target); w.AddRange(p.after);
			int largest; double ratio;
			string verdict = Verdict(w, p.target, out largest, out ratio);
			int ti = p.before.Count;
			for (int i = 0; i < w.Count; i++)
			{
				Day d = w[i];
				output.Add("ROLL T-R4 table(" + D(p.table) + ") day(" + D(d.day) + ") rel(" + (i - ti).ToString("+0;-0;0", Inv) + ") start(" + T(d.startTime) + " " + Gap(d.startGap, d.atr)
					+ ") maxBar(" + T(d.barTime) + " " + Gap(d.barGap, d.atr) + ") atr20(" + (double.IsNaN(d.atr) ? "NaN" : d.atr.ToString("0.00", Inv)) + ") largest(" + (i == largest ? "1" : "0") + ")");
			}
			Day t = p.target;
			string where = largest < 0 ? "-" : w[largest].AtStart ? "start " + T(w[largest].startTime) : "bar " + T(w[largest].barTime);
			string conf = double.IsNaN(t.atr) || !(t.atr > 0) ? "-" : (_RollDetector.GapConfirmed(double.IsNaN(t.startGap) ? double.NaN : Math.Abs(t.startGap), t.atr, k) ? "1" : "0");
			if (!incomplete) { if (verdict == "aligned") m_aligned++; else if (verdict.StartsWith("data splice", StringComparison.Ordinal)) m_offDate++; else m_none++; }
			output.Add("ROLL T-R4 verdict table(" + D(p.table) + ") " + verdict + (incomplete ? " (incomplete window)" : "")
				+ " | largest(" + (largest < 0 ? "-" : D(w[largest].day)) + " " + where + " " + (largest < 0 ? "" : Gap(w[largest].AtStart ? w[largest].startGap : w[largest].barGap, w[largest].atr))
				+ ") ratio(" + (double.IsNaN(ratio) ? "NaN" : double.IsInfinity(ratio) ? "inf" : ratio.ToString("0.00", Inv)) + ") tableStart(" + Gap(t.startGap, t.atr) + ") confirmed(" + conf
				+ ") sessions(" + p.before.Count + " before " + p.after.Count + " after)");
		}
	}

	// </roll-detector>
}
