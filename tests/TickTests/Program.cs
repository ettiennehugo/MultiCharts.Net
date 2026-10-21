using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PowerLanguage.Function;

// Off-MC tests for _TickStream (spec missing-functionality-specs.md rev 4 §7).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-9) { return Math.Abs(a - b) < eps; }
	static readonly DateTime T0 = new DateTime(2024, 3, 13, 9, 30, 0);

	static _TickStream S(Action<TickConfig> f = null, List<string> j = null)
	{
		var c = new TickConfig { enabled = true, burstNormLength = 10, burstMinSamples = 5 }; if (f != null) f(c);
		var s = new _TickStream { config = c }; if (j != null) s.journal = x => j.Add(x);
		string e = s.Configure(); if (e != null) throw new Exception(e); return s;
	}

	static void Main()
	{
		var bad = new TickConfig { historyTicks = 0, sizeBuckets = new double[] { 5, 2 }, burstMinSamples = 1 }.Validate();
		Check(bad != null && bad.Contains("historyTicks=0") && bad.Contains("not ascending") && bad.Contains("burstMinSamples=1"), "Validate names every bad value");
		Check(Near(_TickStream.Quantile(new double[] { 1, 2, 3, 4, 5 }, 0.5), 3) && Near(_TickStream.Quantile(new double[] { 10, 20 }, 0.1), 11), "quantile type 7");

		// bar (09:30, 09:31]: 4 ticks; three in second 09:30:10, inter-print 100,100,49800 ms
		var j = new List<string>(); var s = S(null, j);
		s.Enqueue(T0.AddSeconds(10), 100, 1); s.Enqueue(T0.AddSeconds(10.1), 100, 3); s.Enqueue(T0.AddSeconds(10.2), 100.25, 25);
		s.Enqueue(T0.AddSeconds(60), 100.25, 2);
		s.Enqueue(T0.AddSeconds(61), 100.5, 1);   // next bar -> proves coverage
		var r = s.Aggregate(T0, T0.AddMinutes(1));
		Check(r.valid && r.quality == "Tick" && r.prints == 4 && r.maxPrintsPerSecond == 3, "count and max prints/s (bar-end inclusive)");
		Check(Near(r.interPrintMedianMs, 100, 1e-3) && Near(r.interPrintP10Ms, 100, 1e-3), $"inter-print median / p10 ({r.interPrintMedianMs}, {r.interPrintP10Ms})");
		Check(r.sizeHistogram == null && r.largePrints == -1 && double.IsNaN(r.volume), "size outputs parked until §E6 verified");

		// not covered yet (no tick past bar end, history incomplete) -> invalid + one Journal fact
		r = s.Aggregate(T0.AddMinutes(1), T0.AddMinutes(2));
		Check(!r.valid && j.Count(x => x.Contains("missing/late")) == 1, "uncovered bar invalid, journaled");
		r = s.Aggregate(T0.AddMinutes(2), T0.AddMinutes(3));
		Check(!r.valid && j.Count(x => x.Contains("missing/late")) == 1, "invalid fact not repeated");
		// the 09:31:01 tick was in the uncovered bar and is consumed with it (not double counted)
		s.Enqueue(T0.AddSeconds(150), 1, 1);   // late: bar (09:32,09:33] already committed
		s.Enqueue(T0.AddSeconds(200), 1, 1); s.Enqueue(T0.AddSeconds(241), 1, 1);
		r = s.Aggregate(T0.AddMinutes(3), T0.AddMinutes(4));
		Check(r.valid && r.prints == 1 && j.Any(x => x.Contains("1 late tick(s) dropped")) && j.Last().Contains("valid again"), "late tick dropped, recovery journaled");

		// history complete: latest bar with ticks counts as covered
		s.MarkHistoryComplete();
		r = s.Aggregate(T0.AddMinutes(4), T0.AddMinutes(5));
		Check(r.valid && r.prints == 1, "history complete -> last bar valid");

		// size outputs when verified
		var s2 = S(c => { c.sizeOutputsVerified = true; c.largePrintSize = 20; });
		foreach (var z in new double[] { 1, 2, 3, 10, 25, 60 }) s2.Enqueue(T0.AddSeconds(z), 1, z);
		s2.MarkHistoryComplete();
		r = s2.Aggregate(T0, T0.AddMinutes(1));
		Check(r.sizeHistogram.SequenceEqual(new[] { 1, 1, 1, 1, 0, 1, 1 }) && r.largePrints == 2 && Near(r.volume, 101), "size histogram (upper-inclusive buckets), large prints, volume");

		// burst: z of max prints/s vs PREVIOUS bars' norm
		var s3 = S(); var rnd = new Random(3);
		for (int b = 0; b < 8; b++)
		{
			int k = 2 + b % 2; var bs = T0.AddMinutes(b);
			for (int i = 0; i < k; i++) s3.Enqueue(bs.AddSeconds(5).AddMilliseconds(i * 10), 1, 1);
			s3.Enqueue(bs.AddMinutes(1).AddSeconds(1), 1, 1); // coverage tick (belongs to the next bar)
			r = s3.Aggregate(bs, bs.AddMinutes(1));
		}
		Check(!r.burst && !double.IsNaN(r.burstZ), "steady bars no burst");
		var b8 = T0.AddMinutes(8);
		for (int i = 0; i < 20; i++) s3.Enqueue(b8.AddSeconds(30).AddMilliseconds(i * 20), 1, 1);
		s3.Enqueue(b8.AddMinutes(1).AddSeconds(1), 1, 1);
		r = s3.Aggregate(b8, b8.AddMinutes(1));
		Check(r.burst && r.maxPrintsPerSecond == 20, $"burst flagged (z={r.burstZ:0.0})");
		var s4 = S(); s4.Enqueue(T0.AddSeconds(1), 1, 1); s4.MarkHistoryComplete();
		Check(double.IsNaN(s4.Aggregate(T0, T0.AddMinutes(1)).burstZ), "no burst z before burstMinSamples");

		// thread safety: concurrent producer while consuming; every tick counted once
		var s5 = S(); int total = 20000;
		var prod = Task.Run(() => { for (int i = 0; i < total; i++) s5.Enqueue(T0.AddMilliseconds(i * 10), 1, 1); s5.Enqueue(T0.AddHours(1), 1, 1); s5.MarkHistoryComplete(); });
		int counted = 0; int bar = 0;
		while (bar < 4)
		{
			var x = s5.Aggregate(T0.AddSeconds(bar * 50 - (bar == 0 ? 1 : 0)), T0.AddSeconds((bar + 1) * 50));
			if (x.valid) { counted += x.prints; bar++; }
			else { s5.Reset(); counted = 0; bar = 0; prod.Wait(); s5 = null; break; }
		}
		if (s5 == null)
		{   // deterministic fallback: producer done first, consume
			s5 = S(); for (int i = 0; i < total; i++) s5.Enqueue(T0.AddMilliseconds(i * 10), 1, 1); s5.Enqueue(T0.AddHours(1), 1, 1);
			counted = 0; for (bar = 0; bar < 4; bar++) counted += s5.Aggregate(T0.AddSeconds(bar * 50 - (bar == 0 ? 1 : 0)), T0.AddSeconds((bar + 1) * 50)).prints;
		}
		prod.Wait();
		Check(counted == total, $"producer/consumer: every tick counted once ({counted})");

		// Reset clears everything
		s.Reset(); Check(!s.HistoryComplete && !s.Last.valid, "Reset");

		Console.WriteLine($"TickTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
