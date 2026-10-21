using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for _TodNormaliser (spec missing-functionality-specs.md rev 4 §4).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-9) { return Math.Abs(a - b) < eps; }
	static readonly DateTime D0 = new DateTime(2024, 1, 2);

	static _TodNormaliser Tod(ETodMetric m, int sessions = 20, int min = 10, int bucket = 1, List<string> j = null)
	{
		var t = new _TodNormaliser { metrics = m, sessions = sessions, minSessions = min, bucketMinutes = bucket };
		if (j != null) t.journal = s => j.Add(s);
		string e = t.Configure(); if (e != null) throw new Exception(e);
		return t;
	}

	static int s_bar;
	// one session of 3 one-minute bars at minutes 1..3 with volumes v1..v3
	static void Session(_TodNormaliser t, int day, double[] vols, bool half = false, double[] deltas = null, int tier = 1)
	{
		for (int i = 0; i < vols.Length; i++)
			t.Add(s_bar++, new TodSample { sessionDate = D0.AddDays(day), minuteOfSession = i + 1, halfDay = half, volume = vols[i], delta = deltas == null ? double.NaN : deltas[i], range = 1, trueRange = 1, deltaTier = tier });
	}

	static void Main()
	{
		Validation(); NormFromPriorSessionsOnly(); HalfDayExcluded(); RingWindow(); Buckets(); DeltaTier(); Splice(); Cumulative();
		Console.WriteLine($"TodTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}

	static void Validation()
	{
		var t = new _TodNormaliser { metrics = ETodMetric.None, sessions = 1, minSessions = 5, bucketMinutes = 0 };
		string e = t.Configure();
		Check(e != null && e.Contains("metrics=None") && e.Contains("sessions=1") && e.Contains("minSessions=5") && e.Contains("bucketMinutes=0") && !t.Configured, "Validate names every bad value");
		Check(double.IsNaN(t.Z(ETodMetric.Volume)) && !t.Valid(ETodMetric.Volume), "neutral outputs when unconfigured");
	}

	static void NormFromPriorSessionsOnly()
	{
		s_bar = 0; var t = Tod(ETodMetric.Volume, 20, 3);
		var minute2 = new[] { 100.0, 110, 90, 100 };
		for (int d = 0; d < 3; d++) Session(t, d, new[] { 50, minute2[d], 70 });
		// session 3: minute 1 has only 3 prior sessions -> valid
		Session(t, 3, new[] { 50.0 });
		t.Add(s_bar++, new TodSample { sessionDate = D0.AddDays(3), minuteOfSession = 2, volume = 120 });
		double mean = 100, sd = Math.Sqrt((0 + 100 + 100) / 3.0);
		Check(t.Valid(ETodMetric.Volume) && Near(t.Z(ETodMetric.Volume), (120 - mean) / sd), $"z vs same minute of prior completed sessions ({t.Z(ETodMetric.Volume)})");
		Check(Near(t.Median(ETodMetric.Volume), 100) && Near(t.Pct(ETodMetric.Volume), 1.0), "median and percentile rank");
		Check(t.Sessions(ETodMetric.Volume) == 3, "current session not in the norm");
		var t2 = Tod(ETodMetric.Volume, 20, 4); s_bar = 0;
		for (int d = 0; d < 3; d++) Session(t2, d, new[] { 50.0, 100, 70 });
		Session(t2, 3, new[] { 50.0 });
		Check(!t2.Valid(ETodMetric.Volume) && double.IsNaN(t2.Z(ETodMetric.Volume)), "valid=false until minSessions completed sessions");
	}

	static void HalfDayExcluded()
	{
		s_bar = 0; var t = Tod(ETodMetric.Volume, 20, 2);
		Session(t, 0, new[] { 10.0, 10, 10 }); Session(t, 1, new[] { 1000.0, 1000, 1000 }, half: true); Session(t, 2, new[] { 20.0, 20, 20 });
		Session(t, 3, new[] { 15.0 });
		Check(t.Sessions(ETodMetric.Volume) == 2 && Near(t.Median(ETodMetric.Volume), 15) && Near(t.Z(ETodMetric.Volume), 0), "half-day session excluded from the norm");
	}

	static void RingWindow()
	{
		s_bar = 0; var t = Tod(ETodMetric.Volume, 3, 2);
		Session(t, 0, new[] { 1000.0 }); Session(t, 1, new[] { 10.0 }); Session(t, 2, new[] { 20.0 }); Session(t, 3, new[] { 30.0 });
		Session(t, 4, new[] { 20.0 });
		Check(Near(t.Median(ETodMetric.Volume), 20) && Near(t.Z(ETodMetric.Volume), 0) && t.Sessions(ETodMetric.Volume) == 3, "only the last `sessions` sessions are kept");
		t.Add(s_bar - 1, new TodSample { sessionDate = D0.AddDays(4), minuteOfSession = 1, volume = 999 });
		Check(Near(t.Z(ETodMetric.Volume), 0), "duplicate bar number ignored");
	}

	static void Buckets()
	{
		s_bar = 0; var t = Tod(ETodMetric.Volume, 20, 2, bucket: 5);
		for (int d = 0; d < 2; d++) for (int m = 5; m < 10; m++) t.Add(s_bar++, new TodSample { sessionDate = D0.AddDays(d), minuteOfSession = m, volume = 10 + d });
		t.Add(s_bar++, new TodSample { sessionDate = D0.AddDays(2), minuteOfSession = 7, volume = 52.5 });
		// bucket 1 (minutes 5..9) sums 50 and 55 -> mean 52.5
		Check(Near(t.Median(ETodMetric.Volume), 52.5) && Near(t.Z(ETodMetric.Volume), 0), "bars finer than the bucket are summed per bucket");
	}

	static void DeltaTier()
	{
		var j = new List<string>(); s_bar = 0; var t = Tod(ETodMetric.Delta, 20, 2, 1, j);
		Session(t, 0, new[] { 1.0 }, deltas: new[] { 5.0 }, tier: 1); Session(t, 1, new[] { 1.0 }, deltas: new[] { 7.0 }, tier: 1);
		Session(t, 2, new[] { 1.0 }, deltas: new[] { 6.0 }, tier: 1);
		Check(t.Valid(ETodMetric.Delta) && Near(t.Z(ETodMetric.Delta), 0), "delta z within one FlowQuality tier");
		Session(t, 3, new[] { 1.0 }, deltas: new[] { 6.0 }, tier: 2);
		Check(!t.Valid(ETodMetric.Delta) && t.Sessions(ETodMetric.Delta) == 0 && j.Any(s => s.Contains("delta tier change 1 -> 2")), "tier change resets the delta norm (journaled)");
		Session(t, 4, new[] { 1.0 }, deltas: new[] { double.NaN }, tier: 1);
		Check(t.Sessions(ETodMetric.Delta) == 1 && !j.Any(s => s.Contains("2 -> 1")), "NaN delta (BarUpDown/Unavailable) is no sample and no tier change");
	}

	static void Splice()
	{
		s_bar = 0; var t = Tod(ETodMetric.Range | ETodMetric.Volume, 20, 2);
		for (int d = 0; d < 2; d++) t.Add(s_bar++, new TodSample { sessionDate = D0.AddDays(d), minuteOfSession = 1, volume = 10 + d, range = 2 + d });
		t.Add(s_bar++, new TodSample { sessionDate = D0.AddDays(2), minuteOfSession = 1, volume = 10.5, range = 50, splice = true });
		Check(!t.Valid(ETodMetric.Range) && t.Valid(ETodMetric.Volume), "roll splice bar: Range sample dropped, Volume kept");
	}

	static void Cumulative()
	{
		s_bar = 0; var t = Tod(ETodMetric.Volume, 20, 2);
		Session(t, 0, new[] { 10.0, 10, 10 }); Session(t, 1, new[] { 20.0, 20, 20 });
		Session(t, 2, new[] { 15.0, 15, 30 });
		// cum at minute 3: prior 30 and 60 (mean 45, sd 15); today 60 -> 1
		Check(Near(t.ZCum(ETodMetric.Volume), 1) && Near(t.Z(ETodMetric.Volume), 3), $"ZCum session-to-date vs same-minute cumulative norm ({t.ZCum(ETodMetric.Volume)}, z {t.Z(ETodMetric.Volume)})");
	}
}
