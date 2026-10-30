using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 S4 — the VPF-4 roll detector, extracted from _VolumeProfileFuturesBase so _CustomSignal (any base) can detect
	// rolls. Logic unchanged: a roll is
	//   Series: the first bar of a new trading day whose open gaps >= gapTicks x tick from the prior close, on days 1-21 of
	//           Mar/Jun/Sep/Dec, or a change in the instrument name (Bars.Info.Name);
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

		private readonly List<int> m_rollBars = new List<int>();
		private DateTime m_lastDay = DateTime.MinValue;
		private int m_lastBar = -1;
		private string m_lastName;

		/// <summary>Bars (as passed to Step) on which a roll was detected, oldest first.</summary>
		public IList<int> RollBars { get { return m_rollBars.AsReadOnly(); } }
		public int LastRollBar { get { return m_rollBars.Count > 0 ? m_rollBars[m_rollBars.Count - 1] : -1; } }

		public void Reset() { m_rollBars.Clear(); m_lastDay = DateTime.MinValue; m_lastBar = -1; m_lastName = null; }

		/// <summary>Educator review R1: Series tests only the first bar of each trading-day key. On an INTRADAY chart keyed by
		/// calendar date that bar is the one after midnight, so the 18:00 ET Globex open gap is never tested (Series is
		/// effectively blind). Daily and longer bars are fine. Returns the warning text, or null when there is nothing to warn.
		/// resolutionType = EResolution.ToString(); calendarKeyed = tradingDayKey is the bar's calendar date.</summary>
		/// <summary>PR #46 review: digest of a roll-date set for the config hash (RegimeGate host.rollDates). FNV-1a 64 over the UTF-8
		/// of the distinct dates sorted ascending, each as "yyyy-MM-dd;"; 16 lower-case hex digits. Order- and duplicate-insensitive.</summary>
		public static string DatesDigest(IEnumerable<DateTime> dates)
		{
			var l = new List<DateTime>(); if (dates != null) foreach (var d in dates) if (!l.Contains(d.Date)) l.Add(d.Date);
			l.Sort();
			var sb = new System.Text.StringBuilder();
			foreach (var d in l) sb.Append(d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
			ulong h = 14695981039346656037UL;
			foreach (byte b in System.Text.Encoding.UTF8.GetBytes(sb.ToString())) { h ^= b; h *= 1099511628211UL; }
			return h.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
		}

		public static string SeriesKeyWarning(bool series, string resolutionType, bool calendarKeyed)
		{
			if (!series || !calendarKeyed) return null;
			switch (resolutionType ?? "")
			{
				case "Day": case "Week": case "Month": case "Quarter": case "Year": return null;
			}
			return "RollDetection Series on an intraday chart (" + resolutionType + ") keyed by calendar date: Series only tests the first bar after midnight "
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
			bool roll = m_lastName != null && name != m_lastName;
			if (newDay && m_lastDay != DateTime.MinValue && bar > 1)
			{
				if (table && dates != null && dates.Contains(tradingDay.Date)) roll = true;
				if (series && tradingDay.Month % 3 == 0 && tradingDay.Day <= 21 && gapTicks > 0
					&& Math.Abs(open - prevClose) >= gapTicks * tick - tick * 1e-6) roll = true;
			}
			bool added = false;
			if (roll && (m_rollBars.Count == 0 || m_rollBars[m_rollBars.Count - 1] != bar)) { m_rollBars.Add(bar); added = true; }
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
	// </roll-detector>
}
