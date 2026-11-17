using System;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Spec CS-0 (analyst-base-class-spec.md): compile probe for a plain (non-study) helper class in its own
	/// *.Function.CS file. It does not derive from FunctionSimple/FunctionSeries and has no MultiCharts lifecycle.
	/// If the PowerLanguage .NET Editor compiles this file (and _CustomSignal's nameof reference to it), the helper
	/// types of CS-1, CS-2, VPB-1, VPB-9 and VPF-7 can live in their own files; if not, they stay nested classes.
	/// Nothing calls this class at run time. CS-1 (per-trade journal) is expected to replace it with the real
	/// journal component.
	/// </summary>
	internal sealed class _SignalJournal
	{
		//constants
		public const string PROBE_ID = "CS-0";

		//interface methods
		public static string Describe()
		{
			return "_SignalJournal helper-file probe (" + PROBE_ID + ")";
		}
	}

	/// <summary>
	/// CS-7 log rate limit (MC-free; tests/TickTests): a repeating per-bar fact is written once per (trading day, rule). The rule is
	/// the text before the first '(' ("at/after FlatBy(15:55:00)" -> "at/after FlatBy"), so changing numbers in the reason do not
	/// re-open it. A new day clears the set. Returns true when the caller should write the line.
	/// </summary>
	internal sealed class DailyOnceLog
	{
		private DateTime m_day = DateTime.MinValue;
		private readonly System.Collections.Generic.HashSet<string> m_seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
		/// <summary>Lines held back since the last reset (diagnostics).</summary>
		public long Suppressed { get; private set; }

		public static string RuleOf(string reason)
		{
			if (reason == null) return "";
			int i = reason.IndexOf('(');
			return (i > 0 ? reason.Substring(0, i) : reason).Trim();
		}

		public bool ShouldLog(DateTime tradingDay, string reason)
		{
			if (tradingDay != m_day) { m_day = tradingDay; m_seen.Clear(); }
			if (m_seen.Add(RuleOf(reason))) return true;
			Suppressed++;
			return false;
		}

		public void Reset() { m_day = DateTime.MinValue; m_seen.Clear(); Suppressed = 0; }
	}
}
