using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using PowerLanguage.Function;

// Off-MC tests for _MtfMath (spec missing-functionality-specs.md rev 4 §3).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }

	// the trace writer's line: level,time zone,signal,symbol,runId,escapeCsv(message) (_CustomSignal LogFileSingleton.WriteTrace)
	static string LogLine(string level, string signal, string runId, string msg)
	{
		string m = msg.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + msg.Replace("\"", "\"\"") + "\"" : msg;
		return level + ",2024-07-09 10:00:00 Exchange," + signal + ",@ES," + runId + "," + m;
	}

	// evaluates the header one-liner's -Pattern ('...' + $_.Matches[0].Groups[1].Value + '...') with the runId in place of the match group
	static string PsConcat(string expr, string rid)
	{
		var sb = new System.Text.StringBuilder();
		foreach (Match m in Regex.Matches(expr, @"'([^']*)'|\$_\.Matches\[0\]\.Groups\[1\]\.Value"))
			sb.Append(m.Groups[1].Success ? m.Groups[1].Value : rid);
		return sb.ToString();
	}

	static void ProbeLog()
	{
		CultureInfo keep = Thread.CurrentThread.CurrentCulture;
		Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
		try
		{
			var t = new DateTime(2024, 7, 9, 10, 0, 0);
			string start = _MtfProbeLog.Start(false, 20, "@ES Minute 1", "@ES Minute 60", _MtfProbeLog.Num(5590.5), 1201, 21);
			string call = _MtfProbeLog.Call(1440, 1, t, "Close", 5600.25, 1440, t, "Close", 5590.5, 5603.75, 5588, 5600.25, 41234, 40, 0, t, 5600.25, 0, t, false, t);
			string callRt = _MtfProbeLog.Call(9, 3, t.AddMinutes(1), "Inside", 5601.5, 1441, t.AddHours(1), "Inside", 5600.5, 5601.5, 5600.25, 5601.5, 12, 41, 1, t, 5600.25, 1, t, true, t.AddMilliseconds(30153));
			string noMap = _MtfProbeLog.Call(2, 1, t, "Close", 1.5, 2, t, "Inside", 1, 2, 0.5, 1.5, 3, 1, 1, t, 1, -1, DateTime.MinValue, false, t);
			Check(start.IndexOf(',') < 0 && call.IndexOf(',') < 0 && callRt.IndexOf(',') < 0, "P2 lines are culture-invariant (no comma under de-DE), so the trace never quotes them");
			Check(call == "P2 call(1440) same(1) d1[2024-07-09 10:00:00 Close c=5600.25 cb=1440] d2[2024-07-09 10:00:00 Close o=5590.5 h=5603.75 l=5588 c=5600.25 v=41234 cb=40] closedIdx(0) closed[2024-07-09 10:00:00 c=5600.25] mapped(0 2024-07-09 10:00:00) realTime(False)", "call line text as in the run sheet");
			Check(start == "P2 start iog(False) maxBarsBack(20) d1(@ES Minute 1) d2(@ES Minute 60) d2.Close[MaxBarsBack]=5590.5 d1cb(1201) d2cb(21)", "start line text as in the run sheet");
			Match mc = Regex.Match(callRt, _MtfProbeLog.CallPattern), ms = Regex.Match(start, _MtfProbeLog.StartPattern), mn = Regex.Match(noMap, _MtfProbeLog.CallPattern);
			Check(mc.Success && mc.Groups[2].Value == "3" && mc.Groups[4].Value == "Inside" && mc.Groups[7].Value == "2024-07-09 11:00:00" && mc.Groups[15].Value == "1" && mc.Groups[16].Value == "2024-07-09 10:00:00" && mc.Groups[20].Value == "True" && mc.Groups[21].Value == "10:00:30.153", "CallPattern reads same(k), statuses, closed stamp, wall time");
			Check(ms.Success && ms.Groups[1].Value == "False" && ms.Groups[2].Value == "20" && ms.Groups[5].Value == "5590.5" && ms.Groups[7].Value == "21" && mn.Success && mn.Groups[18].Value == "-1" && mn.Groups[19].Value == "", "StartPattern; mapped(-1) has no stamp");

			string hdr = File.ReadAllText(Path.Combine("..", "..", "_MtfProbe.Strategy.CS"));
			var one = new string[4];
			foreach (Match m in Regex.Matches(hdr, @"^//\s+R([1-4])  (Get-ChildItem .*)$", RegexOptions.Multiline)) one[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) - 1] = m.Groups[2].Value.TrimEnd();
			Check(one[0] != null && one[1] != null && one[2] != null && one[3] != null, "header has the four extract one-liners R1..R4");
			for (int r = 0; r < 4; r++) if (one[r] == null) one[r] = "";
			var aliases = new[] { "%", "?", "gci", "sls", "select", "sort", "foreach", "where", "ls", "dir", "gc", "cat", "sc", "ft", "fl", "echo", "type", "cp", "mv", "rm", "ni", "iex" };
			bool noVar = true, noAlias = true, outName = true;
			for (int r = 0; r < 4; r++)
			{
				noVar &= one[r].Replace("$_", "").IndexOf('$') < 0;
				string code = Regex.Replace(one[r], "'[^']*'", "''");   // commands and parameters only, not string literals
				foreach (string tok in Regex.Split(code, @"[\s|{}();,]+")) if (Array.IndexOf(aliases, tok.ToLowerInvariant()) >= 0) noAlias = false;
				outName &= one[r].EndsWith("'P2_R" + (r + 1) + ".txt') -Encoding utf8 }", StringComparison.Ordinal) && one[r].IndexOf("-CaseSensitive -Encoding UTF8", StringComparison.Ordinal) > 0;
			}
			Check(noVar, "one-liners: no '$' other than '$_'");
			Check(noAlias, "one-liners: no aliases (%, ?, gci, sls, select, sort, foreach, where, ...)");
			Check(outName, "one-liners: R<n> writes Desktop P2_R<n>.txt, case-sensitive UTF8 read");
			const RegexOptions ps = RegexOptions.None;   // the one-liners pass -CaseSensitive
			Func<string, string> ridPat = l => Regex.Match(l, @"-Pattern '([^']*)'").Groups[1].Value;
			Func<string, string, string> runPat = (l, id) => PsConcat(Regex.Match(l, @"-Pattern (\('.*?'\)) -CaseSensitive").Groups[1].Value, id);
			bool ridSame = true;
			for (int r = 0; r < 4; r++) ridSame &= ridPat(one[r]) == _MtfProbeLog.ExtractRunIdPattern;
			Check(ridSame, "every one-liner's runId pattern equals _MtfProbeLog.ExtractRunIdPattern");
			string ridLine = _MtfProbeLog.ExtractRunIdPattern;
			string rid = "0a1b2c3d", other = "ffff0000";
			var lines = new[] { LogLine("Info", "_MtfProbe", other, start), LogLine("Info", "_MtfProbe", rid, "RUN x"), LogLine("Info", "_MtfProbe", rid, start), LogLine("Info", "_MtfProbe", rid, call), LogLine("Warning", "_MtfProbe", rid, "a, b") };
			string found = null;
			foreach (string l in lines) { Match m = Regex.Match(l, ridLine, ps); if (m.Success) found = m.Groups[1].Value; }
			Check(found == rid, "runId pattern picks the last P2 start's runId");
			Check(Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, "a, P2 start x"), ridLine, ps) == false && Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, start + ", x"), ridLine, ps), "runId pattern: P2 start at message start, quoted or not");
			string c1100 = _MtfProbeLog.Call(5, 1, t.AddHours(1), "Close", 1, 1, t.AddHours(1), "Close", 1, 1, 1, 1, 1, 1, 0, t.AddHours(1), 1, 0, t.AddHours(1), false, t);
			string c0945 = _MtfProbeLog.Call(5, 1, t.AddMinutes(-15), "Close", 1, 1, t, "Inside", 1, 1, 1, 1, 1, 1, 1, t.AddHours(-1), 1, 1, t.AddHours(-1), false, t);
			foreach (int r in new[] { 0, 2, 3 })
			{
				string pAll = runPat(one[r], rid);
				bool allOk = pAll.Length > 0;
				for (int i = 1; i < lines.Length; i++) allOk &= Regex.IsMatch(lines[i], pAll, ps);
				Check(allOk && !Regex.IsMatch(lines[0], pAll, ps) && !Regex.IsMatch(LogLine("Info", "_Other", other, call), pAll, ps) && Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, callRt), pAll, ps)
					&& Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, c1100), pAll, ps), "R" + (r + 1) + " extract: every line of the run, nothing from another run");
			}
			string pR2 = runPat(one[1], rid);
			Check(pR2.Length > 0 && Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, call), pR2, ps) && Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, c0945), pR2, ps) && Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, callRt), pR2, ps)
				&& !Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, c1100), pR2, ps) && Regex.IsMatch(lines[2], pR2, ps) && Regex.IsMatch(lines[4], pR2, ps) && !Regex.IsMatch(lines[0], pR2, ps), "R2 extract: calls 09:40-10:19 only, start/RUN/warnings kept");

			// HtfClosedIndex(2) is 0 or 1 (P2 closed rule, ClosedRule below), never -1; index 1 can still be unreadable on an early calc, so the probe logs closedIdx(-1)
			string unread = _MtfProbeLog.Call(1, 1, t, "Close", 1, 21, t.AddHours(1), "Inside", 1, 1, 1, 1, 1, 1, -1, DateTime.MinValue, 0, -1, DateTime.MinValue, false, t);
			Match mu = Regex.Match(unread, _MtfProbeLog.CallPattern);
			Check(_MtfMath.ClosedIndex(false, 0) == 1 && _MtfMath.ClosedIndex(true, 0) == 0 && unread.Contains("closedIdx(-1) closed[none] mapped(-1) realTime(False)") && mu.Success && mu.Groups[15].Value == "-1" && !mu.Groups[16].Success
				&& Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, unread), runPat(one[0], rid), ps), "unreadable closed bar: closedIdx(-1) closed[none], parsed and extracted");

			string sample = System.Environment.GetEnvironmentVariable("MTF_SAMPLE_LOG");
			if (!string.IsNullOrEmpty(sample))
				File.WriteAllLines(sample, new[] { lines[0], LogLine("Info", "_MtfProbe", other, call), lines[1], lines[2], lines[3], LogLine("Info", "_MtfProbe", rid, c0945), LogLine("Info", "_MtfProbe", rid, c1100), LogLine("Info", "_MtfProbe", rid, callRt), LogLine("Info", "_MtfProbe", rid, unread), lines[4], LogLine("Info", "_Other", rid + "x", call) });
		}
		finally { Thread.CurrentThread.CurrentCulture = keep; }
	}

	// Probe P2 closed rule. Status as the MC EBarState member names; the test maps them to the two typed booleans the host passes.
	enum St { None, Open, Inside, Close }
	struct Call { public DateTime d1; public St s1; public DateTime[] d2; public St s2; public string what; public bool mayStepBack;
		public Call(string what, DateTime d1, St s1, St s2, bool mayStepBack, params DateTime[] d2) { this.what = what; this.d1 = d1; this.s1 = s1; this.s2 = s2; this.d2 = d2; this.mayStepBack = mayStepBack; } }
	static int NewIdx(Call c) { return _MtfMath.ClosedIndex(c.s2 == St.Close, c.s2 == St.None, c.d2[0], c.d1, 0); }
	static int OldIdx(Call c) { return _MtfMath.ClosedIndex(c.s2 == St.Close, 0); }
	static int Mapped(Call c) { return _MtfMath.MapIndex(k => c.d2[k], NewIdx(c), c.d1, 2); }

	static void ClosedRule()
	{
		Func<int, int, DateTime> T = (h, m) => new DateTime(2026, 10, 30, h, m, 0);
		// d2 arrays: index 0 first. 60m bars end-stamped at :30 (Exchange CT).
		DateTime[] last1830 = { T(18, 30), T(17, 30), T(16, 30) }, forming1930 = { T(19, 30), T(18, 30), T(17, 30) };
		var hist = new Call("R1 history between marks: frozen, None", T(19, 5), St.Close, St.None, false, last1830);
		Check(NewIdx(hist) == 0 && d(hist) == T(18, 30) && OldIdx(hist) == 1, "history None, Time <= Data1 -> 0 (18:30; the old rule gave 1 = 17:30, one bar stale)");
		var r2 = new Call("R2 magnifier intrabar: frozen, None", T(19, 17), St.Inside, St.None, false, last1830);
		Check(NewIdx(r2) == 0 && Mapped(r2) == 0, "R2 (IOG + Bar Magnifier) intrabar: None frozen -> 0");
		var bnd = new Call("boundary Close/Close", T(19, 30), St.Close, St.Close, false, forming1930);
		Check(NewIdx(bnd) == 0 && d(bnd) == T(19, 30), "history boundary: d1 Close + d2 Close at 19:30 -> 0 (19:30, END stamp)");
		var r4f = new Call("R4 live IOG on, last minute forming", T(19, 30), St.Inside, St.Inside, false, forming1930);
		var r4o = new Call("R4 live IOG on, last minute open tick", T(19, 30), St.Open, St.Inside, false, forming1930);
		Check(NewIdx(r4f) == 1 && d(r4f) == T(18, 30) && NewIdx(r4o) == 1, "R4 d1 19:30 Open/Inside, d2 Time 19:30 Inside (Time equal) -> 1 (18:30, never the forming bar)");
		var r4c = new Call("R4 d1 19:30 Close", T(19, 30), St.Close, St.Close, false, forming1930);
		Check(NewIdx(r4c) == 0 && d(r4c) == T(19, 30), "R4 d1 19:30 Close, d2 Close final -> 0");
		var live = new Call("live IOG on between marks 63e2d28d", T(19, 12), St.Inside, St.None, false, last1830);
		Check(NewIdx(live) == 0 && d(live) == T(18, 30), "live between marks: None frozen -> 0");
		var k1 = new Call("R3 k=1", T(19, 30), St.Close, St.Inside, true, forming1930);
		var k2 = new Call("R3 k=2", T(19, 30), St.None, St.Close, false, forming1930);
		Check(NewIdx(k1) == 1 && d(k1) == T(18, 30) && NewIdx(k2) == 0 && d(k2) == T(19, 30), "R3 live IOG off: k=1 d1 Close / d2 Inside -> 1; k=2 d1 None / d2 Close -> 0");
		var rhmOffN = new Call("RHM off: forming stamped ahead, None", T(19, 5), St.Close, St.None, false, forming1930);
		var rhmOffI = new Call("RHM off: forming stamped ahead, Inside", T(19, 5), St.Close, St.Inside, false, forming1930);
		var rhmOffO = new Call("RHM off: forming stamped ahead, Open", T(18, 31), St.Close, St.Open, false, forming1930);
		Check(NewIdx(rhmOffN) == 1 && NewIdx(rhmOffI) == 1 && NewIdx(rhmOffO) == 1 && d(rhmOffN) == T(18, 30), "RHM off / live forming: d2 Time after Data1, Status None/Open/Inside -> 1");
		var futClose = new Call("Close with a future Time", T(19, 5), St.Close, St.Close, false, forming1930);
		Check(NewIdx(futClose) == 0 && _MtfMath.BarZeroClosed(true, false, T(19, 30), T(19, 5)) && Mapped(futClose) == 1 && d(futClose) == T(18, 30),
			"Status Close with d2 Time after Data1 -> ClosedIndex 0 (Close is closed whatever its Time); htfMapped still skips it (Time > Data1) -> 18:30");
		Check(_MtfMath.BarZeroClosed(false, true, T(19, 30), T(19, 30)) && !_MtfMath.BarZeroClosed(false, true, T(19, 30), T(19, 29)) && _MtfMath.ClosedIndex(false, true, T(18, 30), T(19, 0), 2) == 2
			&& _MtfMath.ClosedIndex(false, false, T(18, 30), T(19, 0), 2) == 3, "BarZeroClosed: None at equal Time closed, None ahead forming; ago adds");

		// replays: mapped HTF bar time over consecutive calls never goes back, except on a call flagged mayStepBack (R3 k=1), and then
		// by at most one bar. Never the forming bar (mapped Time <= Data1 Time and not bar 0 unless closed).
		var seqs = new[]
		{
			new[] { hist, new Call("19:29 Close", T(19, 29), St.Close, St.None, false, last1830), bnd, new Call("19:31 Close", T(19, 31), St.Close, St.None, false, forming1930) },
			new[] { live, new Call("19:29 Inside", T(19, 29), St.Inside, St.None, false, last1830), r4o, r4f, new Call("R4 19:30 Inside c=7582.5", T(19, 30), St.Inside, St.Inside, false, forming1930), r4c,
				new Call("19:31 Open", T(19, 31), St.Open, St.None, false, forming1930) },
			new[] { new Call("19:29 Close", T(19, 29), St.Close, St.None, false, last1830), k1, k2, new Call("19:31 Close", T(19, 31), St.Close, St.None, false, forming1930) },
			// R3 k=1 where Data2 bar 0 is still the 18:30 bar flagged Inside: maps one bar back for that one call
			new[] { new Call("19:29 Close", T(19, 29), St.Close, St.None, false, last1830), new Call("R3 k=1 (old bar 0)", T(19, 30), St.Close, St.Inside, true, last1830), k2 },
			new[] { new Call("18:59 Close", T(18, 59), St.Close, St.None, false, last1830), rhmOffN, rhmOffI, new Call("19:30 Close", T(19, 30), St.Close, St.Close, false, forming1930) },
		};
		bool mono = true, back1 = true, noForming = true, oldMono = true; int backs = 0;
		foreach (Call[] q in seqs)
		{
			DateTime prev = DateTime.MinValue, prevOld = DateTime.MinValue;
			foreach (Call c in q)
			{
				int i = Mapped(c); DateTime m = i >= 0 ? c.d2[i] : DateTime.MinValue;
				noForming &= i >= 0 && m <= c.d1 && (i > 0 || c.s2 == St.Close || c.s2 == St.None);
				if (m < prev) { backs++; if (!c.mayStepBack) mono = false; if (m < prev.AddHours(-1)) back1 = false; }
				else prev = m;
				int io = _MtfMath.MapIndex(k => c.d2[k], OldIdx(c), c.d1, 2); DateTime mo = io >= 0 ? c.d2[io] : DateTime.MinValue;
				if (mo < prevOld) oldMono = false; prevOld = mo;
			}
		}
		Check(mono && back1 && backs == 1, "replays: monotonic except the R3 k=1 call, which steps back at most one bar (" + backs + " step-back)");
		Check(noForming, "replays: never the forming bar, mapped Time <= Data1 Time");
		Check(!oldMono, "old rule (Close ? 0 : 1) is non-monotonic on the same replays (the P2 defect)");
		int stale = 0;
		for (int mnt = 1; mnt <= 60; mnt++)
		{
			DateTime t1 = T(18, 30).AddMinutes(mnt);
			var c = mnt == 60 ? new Call("", t1, St.Close, St.Close, false, forming1930) : new Call("", t1, St.Close, St.None, false, last1830);
			if (OldIdx(c) != NewIdx(c)) stale++;
		}
		Check(stale == 59, "the old rule maps one bar stale on 59 of 60 minutes (" + stale + ")");
	}
	static DateTime d(Call c) { int i = Mapped(c); return i >= 0 ? c.d2[i] : DateTime.MinValue; }

	static void Sessions()
	{
		Func<DayOfWeek, int, int, DayOfWeek, int, int, _MtfMath.Session> S = (sd, sh, sm, ed, eh, em) => new _MtfMath.Session(sd, new TimeSpan(sh, sm, 0), ed, new TimeSpan(eh, em, 0));
		var globex = new[] { S(DayOfWeek.Sunday, 17, 0, DayOfWeek.Monday, 16, 0), S(DayOfWeek.Monday, 17, 0, DayOfWeek.Tuesday, 16, 0) };
		var same = new[] { S(DayOfWeek.Sunday, 17, 0, DayOfWeek.Monday, 16, 0), S(DayOfWeek.Monday, 17, 0, DayOfWeek.Tuesday, 16, 0) };
		var rthEnd = new[] { S(DayOfWeek.Sunday, 17, 0, DayOfWeek.Monday, 15, 15), S(DayOfWeek.Monday, 17, 0, DayOfWeek.Tuesday, 16, 0) };
		var shortT = new[] { S(DayOfWeek.Sunday, 17, 0, DayOfWeek.Monday, 16, 0) };
		string m1 = _MtfMath.SessionMismatch(globex, rthEnd), m2 = _MtfMath.SessionMismatch(globex, shortT);
		var none = new _MtfMath.Session[0];
		Check(_MtfMath.SessionMismatch(globex, same) == null && _MtfMath.SessionUnavailable(globex, same) == null, "same templates -> available, no mismatch");
		Check(_MtfMath.SessionUnavailable(null, same) == "Data1 Sessions null" && _MtfMath.SessionUnavailable(globex, null, 3) == "Data3 Sessions null"
			&& _MtfMath.SessionUnavailable(none, none) == "Data1 Sessions empty, Data2 Sessions empty" && _MtfMath.SessionUnavailable(globex, none) == "Data2 Sessions empty"
			&& _MtfMath.SessionUnavailable(none, globex) == "Data1 Sessions empty", "null or empty Sessions list -> unavailable (two empty lists are not equal)");
		Check(_MtfMath.SessionMismatch(null, same) == null && _MtfMath.SessionMismatch(none, none) == null && _MtfMath.SessionMismatch(globex, none) == null && _MtfMath.SessionMismatch(none, globex) == null,
			"unavailable list -> no (misleading) mismatch report");
		string host = File.ReadAllText(Path.Combine("..", "..", "_CustomSignal.Strategy.CS")).Replace("\r\n", "\n");
		int iu = host.IndexOf("why = _MtfMath.SessionUnavailable(s1, s2, ds);", StringComparison.Ordinal), im = host.IndexOf("diff = why == null ? _MtfMath.SessionMismatch(s1, s2) : null;", StringComparison.Ordinal);
		Check(iu > 0 && im > iu && host.Contains("if (why != null)") && host.Contains("session check unavailable ({why})")
			&& host.Contains("catch (Exception e) { why = e.GetType().Name; diff = null; }"), "host: unavailable (null, empty or throw) logs the Warning before any mismatch check");
		Check(host.Contains("traceOn(ETraceLevel.Info))\n\t\t\t\ttrace(ETraceLevel.Info, \"MTF closed rule"), "host: the RHM-required line logs at Info");
		Check(m1 == "session 0: Data1 Sun 17:00-Mon 16:00 vs Sun 17:00-Mon 15:15", "different session end -> first difference named (" + m1 + ")");
		Check(m2 != null && m2.StartsWith("sessions 2 vs 1 ", StringComparison.Ordinal), "different session count -> warning (" + m2 + ")");
	}

	static void Main()
	{
		Check(_MtfMath.ClosedIndex(true, 0) == 0 && _MtfMath.ClosedIndex(false, 0) == 1 && _MtfMath.ClosedIndex(false, 3) == 4, "ClosedIndex: forming bar 0 is skipped");
		// HTF 60-min bars end-stamped 09:00, 10:00, 11:00 (index 0 = 11:00 forming)
		var t0 = new DateTime(2024, 3, 13, 11, 0, 0);
		Func<int, DateTime> times = i => t0.AddHours(-i);
		Check(_MtfMath.MapIndex(times, _MtfMath.ClosedIndex(false, 0), new DateTime(2024, 3, 13, 10, 7, 0), 3) == 1, "10:07 Data1 bar maps to the 10:00 closed HTF bar");
		Check(_MtfMath.MapIndex(times, 0, new DateTime(2024, 3, 13, 10, 0, 0), 3) == 1, "HTF bar stamped after Data1 time is skipped");
		Check(_MtfMath.MapIndex(times, 0, new DateTime(2024, 3, 13, 11, 0, 0), 3) == 0, "same-timestamp closed HTF bar is usable (end stamps)");
		Check(_MtfMath.MapIndex(times, 0, new DateTime(2024, 3, 13, 7, 0, 0), 2) == -1, "no bar within maxBack -> -1");
		Check(_MtfMath.SymbolRoot("@ES") == "ES" && _MtfMath.SymbolRoot("@es#C") == "ES" && _MtfMath.SymbolRoot("ESZ24") == "ES" && _MtfMath.SymbolRoot("ESZ4") == "ES", "SymbolRoot continuous / outright");
		Check(_MtfMath.SymbolRoot("@NQ") != _MtfMath.SymbolRoot("@ES") && _MtfMath.SymbolRoot("SPY") == "SPY" && _MtfMath.SymbolRoot("") == "", "SymbolRoot distinguishes markets, equities unchanged");
		Check(_MtfMath.SymbolRoot("@MES") == "MES" && _MtfMath.SymbolRoot("MESH25") == "MES", "micro root kept");
		Check(_MtfMath.IsStale(t0.AddMinutes(-3), t0, TimeSpan.FromMinutes(2)) && !_MtfMath.IsStale(t0.AddMinutes(-2), t0, TimeSpan.FromMinutes(2)) && !_MtfMath.IsStale(t0.AddDays(-1), t0, TimeSpan.Zero), "IsStale; maxStale 0 disables");
		ProbeLog();
		ClosedRule();
		Sessions();
		Console.WriteLine($"MtfTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
