using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using PowerLanguage.Function;

// PR-D9a (f): run-time and log-size basis for the _DvpEngineTest 2016-2024 Journal run. Not part of the suite: it runs only with
// DVP_BENCH=1 (e.g. `DVP_BENCH=1 dotnet run -c Release --project tests/Dvp`). It feeds the real engine (default DvpConfig =
// winLen 240, refitEvery 1, projBars 60, SelfCalibrate PROBE, journalFits on) a synthetic 15m Globex-like series of 210,000 bars
// (Data1) and a separate 52,000-bar series (Data2), and times: journal off; journal on (FIT/DEGREE, journalRails off) into a counting
// sink, and with every line written the way _CustomSignal's LogFileSingleton does it (CSV prefix + one File.AppendAllText per line);
// and the same two with the DVP RAIL step (journalRails on, the DvpConfig default the host runs at Journal: railStep below copies
// _CustomSignal.dvpRailStep without the PA lattice).
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
		bench("journal on + DVP RAIL step (journalRails, no PA lattice), counting sink", s1, s2, 3);
		bench("journal on + DVP RAIL step (journalRails, no PA lattice), File.AppendAllText per line", s1, s2, 4);
	}

	// _CustomSignal.dvpRailStep without the PA lattice (null, as in ConfluenceTests.RailJournalTest): t-1 snapshot, rails, confluence, Step.
	static void railStep(_DvpEngine e, int ds, _BarRing r, Action<string> sink)
	{
		DvpStream st = e.Stream(ds); if (st == null || st.RailJournal == null) return;
		DvpSnapshot x = e.Snapshot(ds, r, 1); DvpRails rl = x != null ? e.RailsOf(ds, r, 1) : null;
		DvpConfluence cf = rl != null && r.Count > 1 ? e.ConfluenceOf(ds, x, rl, null, r[1].c) : null;
		st.RailJournal.Step(st, r, x, rl, cf, false, st.SessionHalfDay, sink, e.RunId);
	}

	static int[] Rolls(int n, int every) { var r = new int[n / every]; for (int i = 0; i < r.Length; i++) r[i] = (i + 1) * every; return r; }

	static void bench(string what, RingBar[] s1, RingBar[] s2, int mode)
	{
		string path = Path.Combine(Path.GetTempPath(), "dvp_bench_" + Process.GetCurrentProcess().Id + ".csv");
		if (File.Exists(path)) File.Delete(path);
		long lines = 0, bytes = 0, fit = 0, deg = 0, rail = 0;
		var utf8 = new UTF8Encoding(false);
		Action<string> sink = null;
		if (mode > 0)
			sink = msg =>
			{
				lines++;
				if (msg.StartsWith("DVP FIT", StringComparison.Ordinal)) fit++; else if (msg.StartsWith("DVP DEGREE", StringComparison.Ordinal)) deg++;
				else if (msg.StartsWith("DVP RAIL", StringComparison.Ordinal)) rail++;
				// _CustomSignal LogFileSingleton.WriteTrace layout: level,time zone,signal,symbol,runId (8 hex chars),message
				string line = "Journal,2020-06-15 10:30:00 Exchange,_DvpEngineTest,@ES,3f9c2a1b," + (msg.IndexOf(',') >= 0 ? "\"" + msg + "\"" : msg);
				bytes += utf8.GetByteCount(line) + Environment.NewLine.Length;
				if (mode == 2 || mode == 4) File.AppendAllText(path, line + Environment.NewLine, utf8);
			};
		var c = new DvpConfig { enabled = true, journalFits = mode > 0, journalRails = mode >= 3, splitFlow = false, streams = new[] { 1, 2 } };
		var w = Stopwatch.StartNew();
		var e = new _DvpEngine(c, "bench", "ES", 0.25, true, true, sink, x => { });
		var r1 = new _BarRing(DvpStream.RingNeed(c)); var r2 = new _BarRing(DvpStream.RingNeed(c));
		// interleave like the chart: four 15m bars per 60m bar
		int j = 0;
		for (int i = 0; i < s1.Length; i++)
		{
			r1.Add(s1[i]); e.OnBar(1, r1, false); if (mode >= 3) railStep(e, 1, r1, sink);
			if ((i & 3) == 3 && j < s2.Length) { r2.Add(s2[j++]); e.OnBar(2, r2, false); if (mode >= 3) railStep(e, 2, r2, sink); }
		}
		while (j < s2.Length) { r2.Add(s2[j++]); e.OnBar(2, r2, false); if (mode >= 3) railStep(e, 2, r2, sink); }
		e.Finish();
		w.Stop();
		long fileBytes = mode == 2 && File.Exists(path) ? new FileInfo(path).Length : 0;
		if (File.Exists(path)) File.Delete(path);
		Console.WriteLine("bench " + what + ": " + w.ElapsedMilliseconds + " ms, lines " + lines + " (FIT " + fit + ", DEGREE " + deg + ", RAIL " + rail + "), log bytes " + bytes
			+ (mode == 2 ? " (file " + fileBytes + ")" : ""));
	}
}
