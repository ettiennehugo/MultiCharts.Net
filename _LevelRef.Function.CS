using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 §0.6 — the level record every VP helper emits, so the A2-33 harness (_LevelStudy) consumes levels without
	// adapters. _LevelStudy has no record type of its own (it takes type/price/age/quality/key), so LevelRef stays as specified;
	// StudyType / StudyKey map it onto _LevelStudy.AddLevel. Review M2 (A2-39c): isRollWindow = the source session is a roll
	// session or the session just before / after one, so tests can filter. Pure C# (tests/VpHelperTests).
	// ============================================================================================

	// <level-ref>
	public enum ELevelKind { Poc, Vah, Val, Hvn, Lvn, RangePoc, RangeVah, RangeVal, WeekPoc, WeekVah, WeekVal, FvgUpper, FvgLower,
	                         GmmMu, GmmEdge, GmmVoid, SigmaMu, VtPrice, BootstrapPocZoneLo, BootstrapPocZoneHi }

	public sealed class LevelRef
	{
		public ELevelKind kind; public double price, lo, hi;   // lo/hi = zone, both = price for a line level
		public DateTime createdTime;        // bar time from which it is usable (no lookahead)
		public DateTime sourceT0, sourceT1; // window that produced it
		public int dataStream; public string tag;  // e.g. "RP:20261021T0930-1130", "WK:2026W42", "FVG:U:202610211005"
		public double quality = double.NaN; // helper-specific (stability, pi_k, gap ticks); NaN if none
		public bool crossesRoll;
		public bool isRollWindow;           // review M2: source session is a roll session +-1 (A2-39c filter)

		public static LevelRef Line(ELevelKind kind, double price, DateTime created, DateTime t0, DateTime t1, int stream, string tag, double quality = double.NaN)
		{
			return new LevelRef { kind = kind, price = price, lo = price, hi = price, createdTime = created, sourceT0 = t0, sourceT1 = t1, dataStream = stream, tag = tag, quality = quality };
		}

		public static LevelRef Zone(ELevelKind kind, double price, double lo, double hi, DateTime created, DateTime t0, DateTime t1, int stream, string tag, double quality = double.NaN)
		{
			return new LevelRef { kind = kind, price = price, lo = lo, hi = hi, createdTime = created, sourceT0 = t0, sourceT1 = t1, dataStream = stream, tag = tag, quality = quality };
		}

		/// <summary>_LevelStudy.AddLevel type string.</summary>
		public string StudyType { get { return kind.ToString(); } }
		/// <summary>_LevelStudy.AddLevel key (stable identity across sessions).</summary>
		public string StudyKey { get { return (tag ?? kind.ToString()) + "@" + price.ToString("R", CultureInfo.InvariantCulture); } }

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "{0} {1} [{2}-{3}] tag({4}) created({5:yyyy-MM-dd HH:mm:ss}) q({6}) roll({7}) rollWin({8})",
				kind, price, lo, hi, tag, createdTime, quality, crossesRoll ? 1 : 0, isRollWindow ? 1 : 0);
		}
	}

	/// <summary>
	/// Review M2 / A2-39(c): roll session +-1. Feed every trading day once (in order) with whether a roll happened on it.
	/// IsRollWindow(day) is true for a roll day and for the trading day just before and just after it. The "day after"
	/// is known only once that day is seen, so a record stamped earlier must be re-read (RefreshFlag) when filtering.
	/// </summary>
	public sealed class _RollWindow
	{
		private readonly List<DateTime> m_days = new List<DateTime>();
		private readonly HashSet<DateTime> m_rolls = new HashSet<DateTime>();
		private readonly Dictionary<DateTime, int> m_index = new Dictionary<DateTime, int>();

		/// <summary>Register a trading day (idempotent) and mark it as a roll day when roll is true (a roll seen on any bar of the day counts).</summary>
		public void Observe(DateTime tradingDay, bool roll)
		{
			DateTime d = tradingDay.Date;
			if (!m_index.ContainsKey(d))
			{
				if (m_days.Count > 0 && d < m_days[m_days.Count - 1]) return;   // out of order: ignore
				m_index[d] = m_days.Count; m_days.Add(d);
			}
			if (roll) m_rolls.Add(d);
		}

		public bool IsRollDay(DateTime tradingDay) { return m_rolls.Contains(tradingDay.Date); }

		/// <summary>Roll day, or the observed trading day immediately before / after a roll day. Unknown days: false.</summary>
		public bool IsRollWindow(DateTime tradingDay)
		{
			int i;
			if (!m_index.TryGetValue(tradingDay.Date, out i)) return false;
			if (m_rolls.Contains(m_days[i])) return true;
			if (i > 0 && m_rolls.Contains(m_days[i - 1])) return true;
			if (i + 1 < m_days.Count && m_rolls.Contains(m_days[i + 1])) return true;
			return false;
		}

		public int Days { get { return m_days.Count; } }
	}
	// </level-ref>
}
