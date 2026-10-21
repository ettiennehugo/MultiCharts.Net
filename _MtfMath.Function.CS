// ============================================================================================
// M-MTF — higher-timeframe access helpers (spec /workspace/specs/missing-functionality-specs.md rev 4 §3, §0.4).
// HTF bars come from extra chart series (BarsOfData(n)); no internal bar building. This file holds the MC-free rules so they
// are unit-tested (tests/MtfTests); _CustomSignal wraps them around BarsOfData (HtfClosed / HtfForming / htfMapped).
// ============================================================================================
using System;

namespace PowerLanguage.Function
{
	/// <summary>Pure M-MTF rules (VPB-8 closedBarsOnly).</summary>
	public static class _MtfMath
	{
		/// <summary>Bars-ago index of the ago-th CLOSED bar: the stream's bar 0 is closed only when its Status is Close.</summary>
		public static int ClosedIndex(bool barZeroClosed, int ago) { return barZeroClosed ? ago : ago + 1; }

		/// <summary>
		/// §3 rule 3: the HTF context for a Data1 bar at t is the latest CLOSED HTF bar with Time &lt;= t. Starting at the closed index
		/// 'first', walks back while timeAt(i) &gt; t (only possible if the HTF stream is stamped ahead of Data1, see Probe P2).
		/// Returns -1 when no such bar exists within maxBack.
		/// </summary>
		public static int MapIndex(Func<int, DateTime> timeAt, int first, DateTime t, int maxBack)
		{
			for (int i = first; i <= maxBack; i++) if (timeAt(i) <= t) return i;
			return -1;
		}

		/// <summary>§3 rule 6: symbol root used to check that HTF streams carry the Data1 instrument, e.g. "@ES" for "@ES" / "@ES#C" / "ESZ24".</summary>
		public static string SymbolRoot(string name)
		{
			if (string.IsNullOrEmpty(name)) return "";
			string s = name.Trim().ToUpperInvariant();
			int start = s.StartsWith("@") ? 1 : 0, i = start;
			while (i < s.Length && char.IsLetter(s[i])) i++;
			string letters = s.Substring(start, i - start);
			// outright futures (ESZ24, ESZ4): drop the month code when followed by digits
			if (i < s.Length && char.IsDigit(s[i]) && letters.Length >= 2 && "FGHJKMNQUVXZ".IndexOf(letters[letters.Length - 1]) >= 0) letters = letters.Substring(0, letters.Length - 1);
			return letters;
		}

		/// <summary>True when the stream is stale against the Data1 time: its last closed bar is older than maxStale.</summary>
		public static bool IsStale(DateTime streamTime, DateTime data1Time, TimeSpan maxStale) { return maxStale > TimeSpan.Zero && data1Time - streamTime > maxStale; }
	}
}
