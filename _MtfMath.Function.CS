// ============================================================================================
// M-MTF — higher-timeframe access helpers (spec /workspace/specs/missing-functionality-specs.md rev 4 §3, §0.4).
// HTF bars come from extra chart series (BarsOfData(n)); no internal bar building. This file holds the MC-free rules so they
// are unit-tested (tests/MtfTests); _CustomSignal wraps them around BarsOfData (HtfClosed / HtfForming / htfMapped).
//
// CLOSED RULE (Probe P2, runs R1 8356ba60, R2 05c8e4d9, R3 c003d107, R4 aa78186a, live IOG-on 63e2d28d; @ES 1m + 60m, RHM on).
//   HTF bar 0 is CLOSED iff Status == Close, or Status == None and its Time <= the Data1 bar's Time. Open/Inside = forming -> use index 1.
//   - History (R1, R2) and live between marks: d2[0] is the last closed bar, OHLCV frozen, Status None, Time <= Data1 -> 0.
//   - Live IOG on (R4), the Data1 bar whose end equals Data2's end (d1 19:30 Open/Inside): d2[0] is the FORMING 19:30 bar,
//     Status Inside, c follows d1, v grows -> 1. At d1 19:30 Close, d2 is Close with final values -> 0.
//   - Live IOG off (R3, two calls in the same ms): k=1 d1 Close / d2 Inside (values already final) -> 1; k=2 d1 None / d2 Close -> 0.
//   - RHM off / live forming: d2[0] stamped after the Data1 time -> 1 (whatever its Status).
//   MONOTONIC: over consecutive calls the mapped HTF bar time never goes back, EXCEPT the R3 k=1 call (d1 Close, d2 Inside at the
//   shared end time), which may map one bar back for that one call. That is conservative: an older closed bar, never a forming one.
//   The old rule (Status == Close ? 0 : 1) mapped one bar stale for 59 of every 60 minutes and was non-monotonic at the boundary.
//   Realtime-History Matching (Format Signals > Signals tab > Properties > Backtesting > Realtime-History Matching) MUST be on;
//   MC .NET exposes no member to read it (not in the Programming Guide v1.0/v1.1), so it is a setup step, not a check.
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
		/// Probe P2 closed rule (header): HTF bar 0 is closed iff its Status is Close, or its Status is None (not updated on this calc:
		/// the frozen last closed bar) and its Time is not after the Data1 time. Open/Inside is the forming bar. The host passes typed
		/// EBarState compares (Status == EBarState.Close, Status == EBarState.None), so this file stays MC-free.
		/// </summary>
		public static bool BarZeroClosed(bool statusClose, bool statusNone, DateTime htfTime0, DateTime data1Time)
		{
			return statusClose || (statusNone && htfTime0 <= data1Time);
		}

		/// <summary>Bars-ago index of the ago-th CLOSED HTF bar under the Probe P2 rule (BarZeroClosed).</summary>
		public static int ClosedIndex(bool statusClose, bool statusNone, DateTime htfTime0, DateTime data1Time, int ago)
		{
			return ClosedIndex(BarZeroClosed(statusClose, statusNone, htfTime0, data1Time), ago);
		}

		/// <summary>One weekly session of a session template (MC SessionObject: StartDay/StartTime/EndDay/EndTime), MC-free.</summary>
		public struct Session
		{
			public DayOfWeek StartDay, EndDay;
			public TimeSpan StartTime, EndTime;
			public Session(DayOfWeek startDay, TimeSpan startTime, DayOfWeek endDay, TimeSpan endTime) { StartDay = startDay; StartTime = startTime; EndDay = endDay; EndTime = endTime; }
			public override string ToString()
			{
				return StartDay.ToString().Substring(0, 3) + " " + StartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture) + "-" + EndDay.ToString().Substring(0, 3) + " " + EndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
			}
		}

		/// <summary>
		/// MC-1439 setup check: null when the two session templates are the same (same count, and each session's start/end day and time
		/// equal, in order), else a short description of the first difference. With RHM on and different session ends, MC holds the
		/// strategy until Data2 prints. A null list counts as unknown and returns null (the host logs that separately).
		/// </summary>
		public static string SessionMismatch(Session[] data1, Session[] other)
		{
			if (data1 == null || other == null) return null;
			if (data1.Length != other.Length)
				return "sessions " + data1.Length.ToString(CultureInfo.InvariantCulture) + " vs " + other.Length.ToString(CultureInfo.InvariantCulture) + " (Data1 " + Join(data1) + "; other " + Join(other) + ")";
			for (int i = 0; i < data1.Length; i++)
			{
				Session a = data1[i], b = other[i];
				if (a.StartDay != b.StartDay || a.StartTime != b.StartTime || a.EndDay != b.EndDay || a.EndTime != b.EndTime)
					return "session " + i.ToString(CultureInfo.InvariantCulture) + ": Data1 " + a + " vs " + b;
			}
			return null;
		}

		private static string Join(Session[] s)
		{
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < s.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(s[i].ToString()); }
			return sb.ToString();
		}

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
