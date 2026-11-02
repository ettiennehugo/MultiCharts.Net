using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using PowerLanguage.Function;

// PR-D9a (f): run-time and log-size basis for the _DvpEngineTest 2016-2024 Journal run. Not part of the suite: it runs only with
// DVP_BENCH=1 (e.g. `DVP_BENCH=1 dotnet run -c Release --project tests/Dvp`). It feeds the real engine (default DvpConfig =
// winLen 240, refitEvery 1, projBars 60, SelfCalibrate PROBE, journalFits on) a synthetic 15m Globex-like series of 210,000 bars
// (Data1) and a separate 52,000-bar series (Data2), and times three variants: journal off; journal on into a counting sink; journal
// on with every line written the way _CustomSignal's LogFileSingleton does it (CSV prefix + one File.AppendAllText per line).
// MultiCharts' own per-bar work, the hosted PA steps and the chart are NOT in these numbers.
static partial class Program
{
	static void D9aBench()
	{
		int n1 = 210000, n2 = 52000;
		bool[] hd;
		RingBar[] s1 = Series(n1, 2016, Rolls(n1, 5980), null, out hd), s2 = Series(n2, 2017, Rolls(n2, 1495), null, out hd);
		Console.WriteLine("bench: Data1 " + n1 + " bars, Data2 " + n2 + " bars, default DvpConfig (SelfCalibrate PROBE, refitEvery 1, journalFits on)");
		bench("journal off", s1, s2, 0);
		bench("journal on, counting sink", s1, s2, 1);
		bench("journal on, File.AppendAllText per line", s1, s2, 2);
	}

	static int[] Rolls(int n, int every) { var r = new int[n / every]; for (int i = 0; i < r.Length; i++) r[i] = (i + 1) * every; return r; }

	static void bench(string what, RingBar[] s1, RingBar[] s2, int mode)
	{
		string path = Path.Combine(Path.GetTempPath(), "dvp_bench_" + Process.GetCurrentProcess().Id + ".csv");
		if (File.Exists(path)) File.Delete(path);
		long lines = 0, bytes = 0, fit = 0, deg = 0;
		var utf8 = new UTF8Encoding(false);
		Action<string> sink = null;
		if (mode > 0)
			sink = msg =>
			{
				lines++;
				if (msg.StartsWith("DVP FIT", StringComparison.Ordinal)) fit++; else if (msg.StartsWith("DVP DEGREE", StringComparison.Ordinal)) deg++;
				// _CustomSignal LogFileSingleton.WriteTrace layout: level,time zone,signal,symbol,runId (8 hex chars),message
				string line = "Journal,2020-06-15 10:30:00 Exchange,_DvpEngineTest,@ES,3f9c2a1b," + (msg.IndexOf(',') >= 0 ? "\"" + msg + "\"" : msg);
				bytes += utf8.GetByteCount(line) + Environment.NewLine.Length;
				if (mode == 2) File.AppendAllText(path, line + Environment.NewLine, utf8);
			};
		var c = new DvpConfig { enabled = true, journalFits = mode > 0, splitFlow = false, streams = new[] { 1, 2 } };
		var w = Stopwatch.StartNew();
		var e = new _DvpEngine(c, "bench", "ES", 0.25, true, true, sink, x => { });
		var r1 = new _BarRing(DvpStream.RingNeed(c)); var r2 = new _BarRing(DvpStream.RingNeed(c));
		// interleave like the chart: four 15m bars per 60m bar
		int j = 0;
		for (int i = 0; i < s1.Length; i++)
		{
			r1.Add(s1[i]); e.OnBar(1, r1, false);
			if ((i & 3) == 3 && j < s2.Length) { r2.Add(s2[j++]); e.OnBar(2, r2, false); }
		}
		while (j < s2.Length) { r2.Add(s2[j++]); e.OnBar(2, r2, false); }
		e.Finish();
		w.Stop();
		long fileBytes = mode == 2 && File.Exists(path) ? new FileInfo(path).Length : 0;
		if (File.Exists(path)) File.Delete(path);
		Console.WriteLine("bench " + what + ": " + w.ElapsedMilliseconds + " ms, lines " + lines + " (FIT " + fit + ", DEGREE " + deg + "), log bytes " + bytes
			+ (mode == 2 ? " (file " + fileBytes + ")" : ""));
	}
}
