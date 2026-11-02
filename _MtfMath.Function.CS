// ============================================================================================
// M-MTF — higher-timeframe access helpers (spec /workspace/specs/missing-functionality-specs.md rev 4 §3, §0.4).
// HTF bars come from extra chart series (BarsOfData(n)); no internal bar building. This file holds the MC-free rules so they
// are unit-tested (tests/MtfTests); _CustomSignal wraps them around BarsOfData (HtfClosed / HtfForming / htfMapped).
// ============================================================================================
using System;
using System.Globalization;

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

	/// <summary>Probe P2 log lines for _MtfProbe. MC-free and culture-invariant (no commas, so the trace never quotes them) so
	/// tests/MtfTests pins the exact text the run sheet's Select-String extracts and PASS rules read.</summary>
	public static class _MtfProbeLog
	{
		public const string ExtractRunIdPattern = @"^Info,[^,]*,_MtfProbe,[^,]*,([0-9a-f]{8}),""?P2 start ";
		public const string CallPattern = @"P2 call\((\d+)\) same\((\d+)\) d1\[(\S+ \S+) (\w+) c=(\S+) cb=(\d+)\] d2\[(\S+ \S+) (\w+) o=(\S+) h=(\S+) l=(\S+) c=(\S+) v=(\S+) cb=(\d+)\] closedIdx\((-?\d+)\) closed\[(?:(\S+ \S+) c=(\S+)|none)\] mapped\((-?\d+) ?([^)]*)\) realTime\((True|False)(?: wall=([0-9:.]+))?\)";
		public const string StartPattern = @"P2 start iog\((True|False)\) maxBarsBack\((\d+)\) d1\(([^)]*)\) d2\(([^)]*)\) d2\.Close\[MaxBarsBack\]=(\S+) d1cb\((\d+)\) d2cb\((\d+)\)";

		public static string Num(double v) { return v.ToString("R", CultureInfo.InvariantCulture); }
		public static string Stamp(DateTime t) { return t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); }

		public static string Start(bool iog, int maxBarsBack, string d1, string d2, string deep, int d1cb, int d2cb)
		{
			return "P2 start iog(" + (iog ? "True" : "False") + ") maxBarsBack(" + maxBarsBack.ToString(CultureInfo.InvariantCulture) + ") d1(" + d1 + ") d2(" + d2 +
				") d2.Close[MaxBarsBack]=" + deep + " d1cb(" + d1cb.ToString(CultureInfo.InvariantCulture) + ") d2cb(" + d2cb.ToString(CultureInfo.InvariantCulture) + ")";
		}

		public static string Call(int call, int same, DateTime t1, string st1, double c1, int cb1, DateTime t2, string st2, double o2, double h2, double l2, double c2, double v2, int cb2,
			int closedIdx, DateTime closedTime, double closedClose, int mapped, DateTime mappedTime, bool realTime, DateTime wall)
		{
			CultureInfo ic = CultureInfo.InvariantCulture;
			return "P2 call(" + call.ToString(ic) + ") same(" + same.ToString(ic) + ") d1[" + Stamp(t1) + " " + st1 + " c=" + Num(c1) + " cb=" + cb1.ToString(ic) + "] d2[" +
				Stamp(t2) + " " + st2 + " o=" + Num(o2) + " h=" + Num(h2) + " l=" + Num(l2) + " c=" + Num(c2) + " v=" + Num(v2) + " cb=" + cb2.ToString(ic) + "] closedIdx(" +
				closedIdx.ToString(ic) + ") closed[" + (closedIdx >= 0 ? Stamp(closedTime) + " c=" + Num(closedClose) : "none") + "] mapped(" + mapped.ToString(ic) + (mapped >= 0 ? " " + Stamp(mappedTime) : "") +
				") realTime(" + (realTime ? "True wall=" + wall.ToString("HH:mm:ss.fff", ic) : "False") + ")";
		}
	}
}
