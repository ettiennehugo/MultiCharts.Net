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

	static TickPrint TP(DateTime t, double p = 1, double sz = 1) { return new TickPrint { t = t, p = p, s = sz }; }

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


		// ---- legacy coverage: a bar before the first tick is never valid with zero prints ----
		var s6 = S(); s6.Enqueue(T0.AddMinutes(10).AddSeconds(5), 1, 1); s6.MarkHistoryComplete();
		Check(!s6.Aggregate(T0, T0.AddMinutes(1)).valid, "bar before the stream's first tick: invalid (was valid with 0 prints)");
		Check(s6.Aggregate(T0.AddMinutes(10), T0.AddMinutes(11)).valid, "bar holding the first tick: valid");
		// caller-decided coverage
		var s7 = S(); s7.Enqueue(T0.AddSeconds(5), 1, 1);
		Check(s7.Aggregate(T0, T0.AddMinutes(1), true).valid && s7.Last.prints == 1, "ready=true: valid without a later tick");
		Check(!s7.Aggregate(T0.AddMinutes(1), T0.AddMinutes(2), false).valid, "ready=false: invalid");
		Check(s7.Aggregate(T0.AddMinutes(2), T0.AddMinutes(3), true).valid && s7.Last.prints == 0, "ready=true, no tick in the bar: valid, 0 prints");

		// ---- new TickConfig fields ----
		var badc = new TickConfig { chunkWaitMs = -1, chunkMaxInFlightDays = 5, chunkSplitOnFail = -1, chunkTimeoutSec = -1, batchLines = -1 }.Validate();
		Check(badc != null && badc.Contains("chunkWaitMs=-1") && badc.Contains("chunkMaxInFlightDays=5") && badc.Contains("chunkSplitOnFail=-1")
			&& badc.Contains("chunkTimeoutSec=-1") && badc.Contains("batchLines=-1"), "Validate names the chunk settings");
		var defc = new TickConfig();
		Check(defc.Validate() == null && defc.chunkByDay && defc.chunkWaitMs == 120000 && defc.chunkMaxInFlightDays == 2 && defc.realTime, "chunk defaults");

		// ---- PlanDays: calendar days of bar closes, first bar - 1 day .. last bar, contiguous half-open windows ----
		// 1m bars Fri 2024-12-27 15:58..16:00, Mon 12-30 08:31..08:32, Tue 12-31 15:59..16:00 (bar zone); a duplicate is ignored
		var closes = new List<DateTime> {
			new DateTime(2024, 12, 27, 15, 58, 0), new DateTime(2024, 12, 27, 15, 59, 0), new DateTime(2024, 12, 27, 16, 0, 0),
			new DateTime(2024, 12, 30, 8, 31, 0), new DateTime(2024, 12, 30, 8, 32, 0), new DateTime(2024, 12, 30, 8, 32, 0),
			new DateTime(2024, 12, 31, 15, 59, 0), new DateTime(2024, 12, 31, 16, 0, 0) };
		DateTime firstFrom = closes[0].AddDays(-1);
		var days = TickChunkFeed.PlanDays(closes, firstFrom);
		Check(days.Count == 3, $"one window per bar date, weekend skipped ({days.Count})");
		Check(days[0].FromEt == firstFrom && days[0].ToEt == closes[2], "first window: (first bar - 1 day, last close of the date]");
		Check(days[1].FromEt == days[0].ToEt && days[2].FromEt == days[1].ToEt, "windows contiguous (no gap, no overlap)");
		Check(days[2].ToEt == new DateTime(2024, 12, 31, 16, 0, 0), "last window ends at the chart's last bar 2024-12-31 16:00 (not 00:00, not later)");
		Check(TickChunkFeed.PlanDays(new List<DateTime>(), firstFrom).Count == 0, "no bars: no window");

		// ---- the feed: issue, buffer, release in order, ready / wait logic ----
		var jf = new List<string>();
		var sf = S(c => { }, jf);
		var feed = new TickChunkFeed(sf, days, 2, 4, 1000, 3);
		Check(feed.From == firstFrom && feed.To == closes[7] && feed.Days == 3 && !feed.AllFinished, "feed window = plan");
		var issue = feed.Next(DateTime.UtcNow);
		Check(issue.Count == 2 && issue[0].ReqFrom == days[0].FromEt && issue[0].ReqTo == days[0].ToEt && issue[1].DayIndex == 1, "two days in flight (prefetch), window = plan");
		Check(feed.Next(DateTime.UtcNow).Count == 0, "no third day while two are in flight");
		int wid;
		Check(feed.Ready(closes[0], out wid) == false && wid == issue[0].Id, "bar of an in-flight day: not ready, wait on its chunk");
		Check(feed.Ready(closes[7].AddMinutes(1), out wid) == null, "after the plan: stream rule (null)");
		// day 2 completes first: held until day 1 is done (release in day order)
		feed.OnHistory(issue[1].Id, new List<TickPrint> { TP(closes[3].AddSeconds(-30)) }, true);
		Check(feed.Ready(closes[3], out wid) == false && wid == issue[1].Id, "day 2 done before day 1: not released yet, still a wait");
		// day 1 in two batches; one tick after the window end and one at the window start are excluded and counted
		feed.OnHistory(issue[0].Id, new List<TickPrint> { TP(firstFrom), TP(closes[0].AddSeconds(-20)), TP(closes[0].AddSeconds(-10)) }, false);
		Check(feed.Ready(closes[0], out wid) == false, "partial day: not ready");
		feed.OnHistory(issue[0].Id, new List<TickPrint> { TP(closes[2]), TP(closes[2].AddSeconds(1)) }, true);
		Check(feed.TakeWake() && !feed.TakeWake(), "a completed day wakes the pump once");
		Check(feed.Ready(closes[0], out wid) == true && wid == 0, "day 1 released: ready");
		Check(feed.Ready(closes[4], out wid) == true, "day 2 released after day 1: ready");
		var rr = sf.Aggregate(closes[0].AddMinutes(-1), closes[0], feed.Ready(closes[0], out wid));
		Check(rr.valid && rr.prints == 2, $"bar (15:57,15:58]: two ticks ({rr.prints})");
		rr = sf.Aggregate(closes[1], closes[2], feed.Ready(closes[2], out wid));
		Check(rr.valid && rr.prints == 1, "last bar of day 1: the tick at its close counts; the one after the window never reaches the stream");
		Check(jf.Any(x => x.StartsWith("TICK chunk done day(2024-12-27)") && x.Contains("ticks(3)") && x.Contains("batches(2)") && x.Contains("outOfWindow(2)")
			&& x.Contains("last(2024-12-27 16:00:00.000)")), "chunk done line: ticks, batches, first/last, outOfWindow");
		// day 3: empty response = missing data (not a quiet day), no wait
		var issue3 = feed.Next(DateTime.UtcNow);
		Check(issue3.Count == 1 && issue3[0].DayIndex == 2 && issue3[0].ReqTo == closes[7], "day 3 issued after a slot frees; ends at the last bar");
		Check(!feed.ShouldEnd(issue3[0].Id, false) && feed.ShouldEnd(issue[0].Id, false), "end finished requests only");
		feed.OnHistory(issue3[0].Id, new List<TickPrint>(), true);
		Check(feed.Ready(closes[7], out wid) == false && wid == 0, "empty day: invalid, no wait");
		Check(feed.AllFinished && feed.Pending() == 0, "all days finished");
		sf.DrainNotes();
		Check(jf.Count(x => x.StartsWith("TICK batch day(")) == 3 && jf.Count(x => x.StartsWith("TICK batch lines capped at 3")) == 1, "batch lines: first 3, then one cap line");
		string sum = feed.SummaryLine("complete");
		Check(sum.Contains("days(3) done(3) failed(0) pending(0) ticks(4)") && sum.Contains("last(2024-12-30 08:30:30.000)") && sum.Contains("planTo(2024-12-31 16:00:00)")
			&& sum.Contains("outOfWindow(2)"), "summary line: counts, last tick <= plan end");
		// stale callbacks (earlier pass ids) are ignored
		feed.OnHistory(5, new List<TickPrint> { TP(closes[7]) }, true);
		Check(feed.SummaryLine("x").Contains("ticks(4)"), "unknown chunk id ignored");

		// failure: split into sub-windows, Ready waits on the sub-chunk; RT day on a live chart stays open
		var sg = S();
		var f2 = new TickChunkFeed(sg, days.Take(1).ToList(), 1, 2, 2000, 0);
		var i2 = f2.Next(DateTime.UtcNow);
		f2.OnFail(i2[0].Id, "boom");
		var subs = f2.Next(DateTime.UtcNow);
		Check(subs.Count == 2 && subs[0].Sub == 1 && subs[1].ReqTo == days[0].ToEt, "failed day split in two sub-windows");
		Check(f2.Ready(closes[2], out wid) == false && wid == subs[1].Id, "wait follows the sub-window covering the bar");
		f2.OnHistory(subs[0].Id, new List<TickPrint> { TP(subs[0].ReqTo) }, true);
		f2.OnHistory(subs[1].Id, new List<TickPrint> { TP(closes[2]) }, true);
		Check(f2.Ready(closes[2], out wid) == true && f2.AllFinished, "sub-windows done: ready");
		Check(!f2.ShouldEnd(subs[1].Id, true) && f2.ShouldEnd(subs[1].Id, false), "a live chart keeps its done RT day open; a historical one ends it");
		f2.OnRealtime(subs[1].Id, TP(closes[2].AddMinutes(1)));
		Check(sg.Aggregate(closes[2], closes[2].AddMinutes(1), f2.Ready(closes[2].AddMinutes(1), out wid)).prints == 1, "RT tick after the released RT day reaches the stream");

		// timeout: an in-flight day silent past timeoutSec fails (and splits)
		var f3 = new TickChunkFeed(S(), days.Take(1).ToList(), 1, 4, 3000, 0);
		var i3 = f3.Next(DateTime.UtcNow);
		Check(f3.Timeouts(DateTime.UtcNow, 300).Count == 0, "no timeout before timeoutSec");
		var tl = f3.Timeouts(DateTime.UtcNow.AddSeconds(301), 300);
		Check(tl.Count == 1 && tl[0].Contains("next(split4)") && f3.Next(DateTime.UtcNow).Count == 4, "silent day timed out and split");

		// bounded wait: the loader thread completes the day while the calc thread polls (the P-RC7 pattern)
		var sw4 = S();
		var f4 = new TickChunkFeed(sw4, days.Take(1).ToList(), 1, 4, 4000, 0);
		var i4 = f4.Next(DateTime.UtcNow);
		var loader = Task.Run(() => { System.Threading.Thread.Sleep(150); f4.OnHistory(i4[0].Id, new List<TickPrint> { TP(closes[0].AddSeconds(-1)) }, false);
			System.Threading.Thread.Sleep(50); f4.OnHistory(i4[0].Id, new List<TickPrint> { TP(closes[2]) }, true); });
		var clock = System.Diagnostics.Stopwatch.StartNew(); bool? rdy = false; int wid4 = i4[0].Id;
		while (rdy == false && wid4 != 0 && clock.ElapsedMilliseconds < 5000) { System.Threading.Thread.Sleep(20); rdy = f4.Ready(closes[0], out wid4); }
		loader.Wait();
		Check(rdy == true && clock.ElapsedMilliseconds >= 150, $"wait ends when the day completes ({clock.ElapsedMilliseconds} ms)");
		Check(sw4.Aggregate(closes[0].AddMinutes(-1), closes[0], rdy).prints == 1, "waited bar gets its tick");

		// ---- CS-7 entry-blocked log: once per trading day and rule ----
		var dl = new DailyOnceLog(); var d1 = new DateTime(2024, 3, 13); int logged = 0;
		for (int i = 0; i < 300; i++) if (dl.ShouldLog(d1, "at/after FlatBy(15:55:00)")) logged++;
		Check(logged == 1 && dl.Suppressed == 299, "same rule, same day: one line");
		Check(dl.ShouldLog(d1, "DailyLossStopR(2) reached: -2.10 R") && !dl.ShouldLog(d1, "DailyLossStopR(2) reached: -2.40 R"), "rule key ignores changing numbers");
		Check(dl.ShouldLog(d1.AddDays(1), "at/after FlatBy(15:55:00)"), "new trading day: logged again");
		Check(DailyOnceLog.RuleOf("at/after FlatBy(15:55:00)") == "at/after FlatBy" && DailyOnceLog.RuleOf("plain") == "plain", "RuleOf");

		// Reset clears everything
		s.Reset(); Check(!s.HistoryComplete && !s.Last.valid, "Reset");

		Console.WriteLine($"TickTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
