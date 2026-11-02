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

	// evaluates a PowerShell concatenation of '...' literals and $vars, as written in the _MtfProbe header
	static string PsConcat(string expr, string rid, string win)
	{
		var sb = new System.Text.StringBuilder();
		foreach (Match m in Regex.Matches(expr, @"'([^']*)'|\$(\w+)"))
			sb.Append(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value == "rid" ? rid : m.Groups[2].Value == "win" ? win : "?");
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
			string ridLine = Regex.Match(hdr, @"^//\s+\$rid=.*-Pattern '([^']*)'", RegexOptions.Multiline).Groups[1].Value;
			string pLine = Regex.Match(hdr, @"^//\s+\$p=(.*)$", RegexOptions.Multiline).Groups[1].Value.TrimEnd();
			Check(ridLine == _MtfProbeLog.ExtractRunIdPattern && pLine.Length > 0, "header runId pattern equals _MtfProbeLog.ExtractRunIdPattern; $p line found");
			const RegexOptions ps = RegexOptions.IgnoreCase;   // Select-String is case-insensitive
			string rid = "0a1b2c3d", other = "ffff0000";
			var lines = new[] { LogLine("Info", "_MtfProbe", other, start), LogLine("Info", "_MtfProbe", rid, "RUN x"), LogLine("Info", "_MtfProbe", rid, start), LogLine("Info", "_MtfProbe", rid, call), LogLine("Warning", "_MtfProbe", rid, "a, b") };
			string found = null;
			foreach (string l in lines) { Match m = Regex.Match(l, ridLine, ps); if (m.Success) found = m.Groups[1].Value; }
			Check(found == rid, "runId pattern picks the last P2 start's runId");
			Check(Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, "a, P2 start x"), ridLine, ps) == false && Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, start + ", x"), ridLine, ps), "runId pattern: P2 start at message start, quoted or not");
			string pAll = PsConcat(pLine, rid, ""), pR2 = PsConcat(pLine, rid, "2024-07-09 (09:[45]|10:[01])");
			bool allOk = true;
			for (int i = 1; i < lines.Length; i++) allOk &= Regex.IsMatch(lines[i], pAll, ps);
			Check(allOk && !Regex.IsMatch(lines[0], pAll, ps) && !Regex.IsMatch(LogLine("Info", "_Other", other, call), pAll, ps) && Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, callRt), pAll, ps), "R1/R3/R4 extract: every line of the run, nothing from another run");
			string c1100 = _MtfProbeLog.Call(5, 1, t.AddHours(1), "Close", 1, 1, t.AddHours(1), "Close", 1, 1, 1, 1, 1, 1, 0, t.AddHours(1), 1, 0, t.AddHours(1), false, t);
			string c0945 = _MtfProbeLog.Call(5, 1, t.AddMinutes(-15), "Close", 1, 1, t, "Inside", 1, 1, 1, 1, 1, 1, 1, t.AddHours(-1), 1, 1, t.AddHours(-1), false, t);
			Check(Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, call), pR2, ps) && Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, c0945), pR2, ps) && Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, callRt), pR2, ps)
				&& !Regex.IsMatch(LogLine("Info", "_MtfProbe", rid, c1100), pR2, ps) && Regex.IsMatch(lines[2], pR2, ps) && Regex.IsMatch(lines[4], pR2, ps), "R2 extract: calls 09:40-10:19 only, start/RUN/warnings kept");
		}
		finally { Thread.CurrentThread.CurrentCulture = keep; }
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
		Console.WriteLine($"MtfTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
