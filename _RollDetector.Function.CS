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
	//   Table:  the first bar of a trading day listed in dates.
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

		/// <summary>Bars (as passed to Step) on which a roll was detected, oldest first.</summary>
		public IList<int> RollBars { get { return m_rollBars.AsReadOnly(); } }
		public int LastRollBar { get { return m_rollBars.Count > 0 ? m_rollBars[m_rollBars.Count - 1] : -1; } }

		public void Reset() { m_rollBars.Clear(); m_lastDay = DateTime.MinValue; m_lastBar = -1; m_lastName = null; m_seriesLatch = DateTime.MinValue; LastOpen = LastPrevClose = LastGap = double.NaN; }

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
			bool newDay = tradingDay != m_lastDay;
			bool roll = m_lastName != null && name != m_lastName, bySeries = false;
			if (newDay && m_lastDay != DateTime.MinValue && bar > 1)
			{
				if (table && dates != null && dates.Contains(tradingDay.Date)) roll = true;
				DateTime expiry;
				bool inWindow = seriesWindow != null ? seriesWindow(tradingDay, out expiry) : SeriesWindow(tradingDay, out expiry);
				if (series && gapTicks > 0 && inWindow && expiry != m_seriesLatch
					&& Math.Abs(open - prevClose) >= gapTicks * tick - tick * 1e-6) { roll = true; bySeries = true; m_seriesLatch = expiry; }
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
	// </roll-detector>
}
