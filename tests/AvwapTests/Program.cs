using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for _AvwapManager (spec missing-functionality-specs.md rev 4 §2, §0.3).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-9) { return Math.Abs(a - b) < eps; }
	static readonly DateTime D0 = new DateTime(2024, 3, 4, 9, 30, 0);   // Monday

	static _AvwapManager Mgr(EAvwapAnchor ev, Action<AvwapLimits> f = null, List<string> j = null, List<string> w = null)
	{
		var l = new AvwapLimits(); if (f != null) f(l);
		var m = new _AvwapManager { limits = l, events = ev, tickSize = 0.25 };
		if (j != null) m.journal = s => j.Add(s);
		if (w != null) m.warnOnce = s => w.Add(s);
		string err = m.Configure(); if (err != null) throw new Exception(err);
		return m;
	}
	static AvwapBar Bar(int n, DateTime t, double o, double h, double l, double c, double v, bool rth = true) { return new AvwapBar { bar = n, time = t, o = o, h = h, l = l, c = c, v = v, sessionDate = t.Date, rth = rth }; }
	static double Tp(AvwapBar b) { return (b.h + b.l + b.c) / 3; }

	static void Main()
	{
		Calendar(); Validation(); SwingBackfill(); BackfillOutOfRing(); ReplaceEvictExpire(); Roll(); Gap(); Climax(); PeriodOpens(); TouchSlopeBands(); Breakout(); Placebos();
		Console.WriteLine($"AvwapTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}

	static void Calendar()
	{
		long k(EAvwapPeriod p, int y, int m, int d) => _AvwapCalendar.PeriodKey(p, new DateTime(y, m, d));
		Check(k(EAvwapPeriod.Week, 2024, 3, 4) == k(EAvwapPeriod.Week, 2024, 3, 10) && k(EAvwapPeriod.Week, 2024, 3, 10) != k(EAvwapPeriod.Week, 2024, 3, 11), "Week key: Monday-start (Sun 03-10 in week of Mon 03-04)");
		Check(k(EAvwapPeriod.Week, 2024, 12, 30) == k(EAvwapPeriod.Week, 2025, 1, 3), "Week key spans the year end");
		Check(k(EAvwapPeriod.Month, 2024, 3, 31) + 1 == k(EAvwapPeriod.Month, 2024, 4, 1) && k(EAvwapPeriod.Month, 2024, 12, 1) + 1 == k(EAvwapPeriod.Month, 2025, 1, 1), "Month key monotonic");
		Check(k(EAvwapPeriod.Quarter, 2024, 3, 31) + 1 == k(EAvwapPeriod.Quarter, 2024, 4, 1) && k(EAvwapPeriod.Quarter, 2024, 4, 1) == k(EAvwapPeriod.Quarter, 2024, 6, 30), "Quarter key");
	}

	static void Validation()
	{
		var l = new AvwapLimits { maxAnchors = 0, slopeBars = 0, bandSigmas = new double[] { 1, -1 } };
		string e = l.Validate();
		Check(e != null && e.Contains("maxAnchors=0") && e.Contains("slopeBars=0") && e.Contains("bandSigmas contains -1"), "Validate names every bad value");
		Check(new AvwapLimits().Validate() == null, "defaults valid");
		var m = new _AvwapManager { tickSize = 0 }; Check(m.Configure() != null, "tickSize 0 rejected");
	}

	// SwingLow: anchored at the extreme bar, PV back-filled to the confirming bar, usable from confirmation
	static void SwingBackfill()
	{
		var m = Mgr(EAvwapAnchor.SwingLow, l => l.backfillBars = 3);
		var bars = new List<AvwapBar>();
		double[] lows = { 100, 99, 97, 98, 99.5, 101 };
		for (int i = 0; i < lows.Length; i++) bars.Add(Bar(i, D0.AddMinutes(i), lows[i] + 1, lows[i] + 2, lows[i], lows[i] + 1.5, 100 + 10 * i));
		for (int i = 0; i < 4; i++) m.Add(bars[i], new AvwapPaEvents());
		m.Add(bars[3], new AvwapPaEvents());   // duplicate ignored
		// bar 3 confirms the swing low at bar 2 (strength 1)
		var ev = new AvwapPaEvents { swingLow = true, swingLowBar = 2, swingLowTime = bars[2].time };
		var b3 = bars[3]; b3.bar = 4; b3.time = D0.AddMinutes(4); bars[4] = new AvwapBar { bar = 4, time = b3.time, o = bars[4].o, h = bars[4].h, l = bars[4].l, c = bars[4].c, v = bars[4].v, sessionDate = D0.Date, rth = true };
		m.Add(bars[4], ev);
		var a = m.Latest(EAvwapAnchor.SwingLow);
		double pv = 0, vv = 0; foreach (int i in new[] { 2, 3, 4 }) { pv += Tp(bars[i]) * bars[i].v; vv += bars[i].v; }
		Check(a != null && a.AnchorBar == 2 && a.AnchorTime == bars[2].time && a.UsableFrom == bars[4].time && Near(a.Value, pv / vv), "SwingLow anchored at extreme, back-filled VWAP exact, usable from confirmation");
		Check(a.Direction == +1 && a.AgeBars == 2 && a.Tag == "AV:SwingLow:" + bars[2].time.ToString("yyyyMMddHHmm"), "direction / age / journal tag");
		m.Add(bars[5], new AvwapPaEvents());
		pv += Tp(bars[5]) * bars[5].v; vv += bars[5].v;
		Check(Near(a.Value, pv / vv), "anchor keeps accumulating");
	}

	static void BackfillOutOfRing()
	{
		var w = new List<string>();
		var m = Mgr(EAvwapAnchor.SwingHigh, l => l.backfillBars = 2, null, w);
		for (int i = 0; i < 10; i++) m.Add(Bar(i, D0.AddMinutes(i), 100, 101, 99, 100, 10), new AvwapPaEvents());
		m.Add(Bar(10, D0.AddMinutes(10), 100, 101, 99, 100, 10), new AvwapPaEvents { swingHigh = true, swingHighBar = 5, swingHighTime = D0.AddMinutes(5) });
		Check(m.Latest(EAvwapAnchor.SwingHigh) == null && w.Count == 1 && w[0].Contains("backfillBars"), "extreme older than the ring: no anchor, warn once");
	}

	static void ReplaceEvictExpire()
	{
		var j = new List<string>();
		// replaceSameType: only the newest WeekOpen... use VolumeClimax via a z override to create anchors on demand
		double z = 0; var m = Mgr(EAvwapAnchor.VolumeClimax, l => { l.maxAnchors = 3; l.replaceSameType = true; l.maxAgeSessions = 2; }, j);
		m.volumeZ = () => z;
		int n = 0; DateTime t = D0;
		Action<double> add = zz => { z = zz; m.Add(Bar(n, t.AddMinutes(n), 100, 101, 99, 100, 10), new AvwapPaEvents()); n++; };
		add(4); add(0); add(5);
		Check(m.Anchors.Count == 1 && m.Anchors[0].AnchorBar == 2 && j.Any(s => s.Contains("expired (replaced)")), "replaceSameType keeps the newest of a type");
		var m2 = Mgr(EAvwapAnchor.VolumeClimax, l => { l.maxAnchors = 3; l.replaceSameType = false; l.maxAgeSessions = 2; }, j);
		m2.volumeZ = () => z; n = 0;
		Action<double, int> add2 = (zz, day) => { z = zz; m2.Add(Bar(n, D0.AddDays(day).AddMinutes(n), 100, 101, 99, 100, 10), new AvwapPaEvents()); n++; };
		add2(4, 0); add2(4, 0); add2(4, 0); add2(4, 0);
		Check(m2.Anchors.Count == 3 && m2.Anchors[0].AnchorBar == 1, "maxAnchors evicts the oldest of the same type");
		add2(0, 1); add2(0, 2);
		Check(m2.Anchors.Count == 3 && m2.Anchors.All(a => a.AgeSessions == 2), "age counted in sessions");
		add2(0, 3);
		Check(m2.Anchors.Count == 0 && j.Any(s => s.Contains("expired (age)")), "expire after maxAgeSessions");
	}

	static void Roll()
	{
		var j = new List<string>(); double z = 4;
		var m = Mgr(EAvwapAnchor.VolumeClimax, l => l.replaceSameType = false, j); m.volumeZ = () => z;
		for (int i = 0; i < 3; i++) m.Add(Bar(i, D0.AddMinutes(i), 100, 101, 99, 100, 10), new AvwapPaEvents());
		var held = m.Anchors.ToList();
		m.OnRoll(D0.AddMinutes(3));
		Check(m.Anchors.Count == 0 && held.All(a => a.Expired && a.ExpireReason == "roll") && j.Count(s => s.Contains("invalidated (roll)")) == 3, "§0.3 roll invalidates every live anchor (journaled)");
	}

	static void Gap()
	{
		// fallback: 3 x SMA-TR(20); bars TR = 2 -> threshold 6
		foreach (double gap in new[] { 5.0, 7.0 })
		{
			var m = Mgr(EAvwapAnchor.GapOpen);
			int n = 0;
			for (int i = 0; i < 25; i++) m.Add(Bar(n++, D0.AddMinutes(i), 100, 101, 99, 100, 10), new AvwapPaEvents());
			m.Add(Bar(n++, D0.AddHours(8), 100, 101, 99, 100, 10, false), new AvwapPaEvents());   // ETH bar same session: no RTH open
			var d1 = D0.AddDays(1);
			m.Add(Bar(n++, d1.AddHours(-3), 100 + gap, 101 + gap, 99 + gap, 100 + gap, 10, false), new AvwapPaEvents());   // overnight
			Check(m.Latest(EAvwapAnchor.GapOpen) == null, "no GapOpen on an ETH bar");
			m.Add(Bar(n++, d1, 100 + gap, 101 + gap, 99 + gap, 100 + gap, 10), new AvwapPaEvents());
			var a = m.Latest(EAvwapAnchor.GapOpen);
			Check(gap > 6 ? (a != null && a.Direction == 1) : a == null, $"GapOpen fallback 3 x SMA-TR: gap {gap} vs 6");
		}
		var md = Mgr(EAvwapAnchor.GapOpen); md.dailyAtr = () => 8;   // 0.5 x 8 = 4
		md.Add(Bar(0, D0, 100, 101, 99, 100, 10), new AvwapPaEvents());
		md.Add(Bar(1, D0.AddDays(1), 95, 96, 94, 95, 10), new AvwapPaEvents());
		Check(md.Latest(EAvwapAnchor.GapOpen) != null && md.Latest(EAvwapAnchor.GapOpen).Direction == -1, "GapOpen with daily ATR: |gap| 5 >= 0.5 x 8, direction down");
	}

	static void Climax()
	{
		var m = Mgr(EAvwapAnchor.VolumeClimax);
		var r = new Random(5); int n = 0;
		for (int i = 0; i < 20; i++) m.Add(Bar(n++, D0.AddMinutes(n), 100, 101, 99, 100, 100 + r.Next(-10, 10)), new AvwapPaEvents());
		Check(m.Anchors.Count == 0, "no climax on normal volume");
		m.Add(Bar(n++, D0.AddMinutes(n), 100, 103, 99, 102.5, 400), new AvwapPaEvents());
		Check(m.Latest(EAvwapAnchor.VolumeClimax) != null && m.Latest(EAvwapAnchor.VolumeClimax).Direction == 1, "climax by fallback 20-bar z (prior bars)");
		var m2 = Mgr(EAvwapAnchor.VolumeClimax); m2.volumeZ = () => 2.9;
		for (int i = 0; i < 30; i++) m2.Add(Bar(i, D0.AddMinutes(i), 100, 101, 99, 100, i == 25 ? 10000 : 100), new AvwapPaEvents());
		Check(m2.Anchors.Count == 0, "M-TOD z override (2.9 < 3) replaces the fallback");
	}

	static void PeriodOpens()
	{
		var m = Mgr(EAvwapAnchor.WeekOpen | EAvwapAnchor.MonthOpen | EAvwapAnchor.QuarterOpen, l => l.maxAgeSessions = 100);
		int n = 0;
		foreach (var d in new[] { new DateTime(2024, 3, 27), new DateTime(2024, 3, 28), new DateTime(2024, 3, 29), new DateTime(2024, 4, 1), new DateTime(2024, 4, 2) })
			m.Add(Bar(n++, d.AddHours(9.5), 100, 101, 99, 100, 10), new AvwapPaEvents());
		Check(m.Latest(EAvwapAnchor.WeekOpen) != null && m.Latest(EAvwapAnchor.WeekOpen).AnchorTime.Date == new DateTime(2024, 4, 1), "WeekOpen on Monday 04-01, none on the first bar fed");
		Check(m.Latest(EAvwapAnchor.MonthOpen).AnchorTime.Date == new DateTime(2024, 4, 1) && m.Latest(EAvwapAnchor.QuarterOpen).AnchorTime.Date == new DateTime(2024, 4, 1), "MonthOpen / QuarterOpen on 04-01");
		Check(m.Anchors.Count == 3, "one anchor per period type");
	}

	static void TouchSlopeBands()
	{
		var m = Mgr(EAvwapAnchor.VolumeClimax, l => { l.slopeBars = 5; }); double z = 4; m.volumeZ = () => z;
		m.Add(Bar(0, D0, 100, 100.5, 99.5, 100, 10), new AvwapPaEvents()); z = 0;
		var a = m.Latest(EAvwapAnchor.VolumeClimax);
		for (int i = 1; i <= 10; i++) m.Add(Bar(i, D0.AddMinutes(i), 100 + i, 100.5 + i, 99.5 + i, 100 + i, 10), new AvwapPaEvents());
		// equal volume: VWAP after bar k = mean of tp 100..100+k = 100 + k/2 -> slope 0.5/bar = 2 ticks/bar
		Check(Near(a.Value, 105) && Near(a.SlopeTicks, 2), $"slope over slopeBars in ticks/bar ({a.SlopeTicks})");
		double sd = Math.Sqrt(Enumerable.Range(0, 11).Select(k => (double)(k - 5) * (k - 5)).Average());
		Check(Near(a.Sigma, sd, 1e-6) && Near(a.Band(2), 105 + 2 * sd, 1e-6) && Near(a.Band(-1), 105 - sd, 1e-6), "sigma and bands");
		Check(a.TouchCount == 0 && !a.Touched, "no touch while price runs away from the anchor");
		m.Add(Bar(11, D0.AddMinutes(11), 106, 106, 104.5, 105, 10), new AvwapPaEvents());
		Check(a.TouchCount == 1 && a.Touched, $"touch counted when the bar spans the prior value ({a.TouchCount})");
	}

	static void Breakout()
	{
		var m = Mgr(EAvwapAnchor.Breakout);
		m.Add(Bar(0, D0, 100, 101, 99, 100, 10), new AvwapPaEvents { breakoutDown = true });
		Check(m.Latest(EAvwapAnchor.Breakout) != null && m.Latest(EAvwapAnchor.Breakout).Direction == -1, "Breakout anchor from PRICEACTION BOS/breakout flag");
		var none = Mgr(EAvwapAnchor.SwingHigh);
		none.Add(Bar(0, D0, 100, 101, 99, 100, 10), new AvwapPaEvents { breakoutDown = true });
		Check(none.Anchors.Count == 0, "events not in AvwapEvents are ignored");
	}

	// Spec S3: P1 / P2 placebo twins
	static void Placebos()
	{
		var off = Mgr(EAvwapAnchor.VolumeClimax); off.volumeZ = () => 4;
		off.Add(Bar(0, D0, 100, 101, 99, 100, 10), new AvwapPaEvents());
		Check(double.IsNaN(off.Anchors[0].P1) && double.IsNaN(off.Anchors[0].P2) && !off.Anchors[0].p2Degraded, "S3 default off: P1/P2 NaN");
		Check(new AvwapLimits { p2MinPool = 0 }.Validate().Contains("p2MinPool=0"), "S3 p2MinPool validated");

		// P1: unweighted mean TP, a V = 0 bar counts
		var j = new List<string>();
		var m = Mgr(EAvwapAnchor.VolumeClimax, l => { l.placebos = EAvwapPlacebo.P1 | EAvwapPlacebo.P2; l.replaceSameType = false; l.maxAnchors = 10; }, j);
		double z = 4; m.volumeZ = () => z;
		var pool = new List<double>(); for (int k = 0; k < 12; k++) pool.Add(50);   // constant pool -> P2 weights equal -> P2 == P1
		m.volumePool = slot => pool;
		var bs = new[] { Bar(0, D0, 100, 102, 99, 101, 10), Bar(1, D0.AddMinutes(1), 101, 104, 100, 103, 0), Bar(2, D0.AddMinutes(2), 103, 106, 102, 105, 500) };
		m.Add(bs[0], new AvwapPaEvents()); z = double.NaN; m.Add(bs[1], new AvwapPaEvents()); m.Add(bs[2], new AvwapPaEvents());
		var a = m.Anchors[0];
		double p1 = (Tp(bs[0]) + Tp(bs[1]) + Tp(bs[2])) / 3;
		Check(Near(a.P1, p1) && !Near(a.P1, a.Value, 1e-3), "S3 P1 = mean TP incl. a V=0 bar (differs from the VWAP)");
		Check(Near(a.P2, p1) && !a.p2Degraded, "S3 P2 with a constant slot pool equals P1");
		Check(a.TagFor(EAvwapPlacebo.P1) == a.Tag + ":P1" && a.TagFor(EAvwapPlacebo.P2) == a.Tag + ":P2", "S3 placebo trade tags");
		Check(j.Any(s => s.Contains("created") && s.Contains("placebos(P1,P2)") && s.Contains("p2seed")), "S3 create journal names the twins and the seed");

		// P2 deterministic draw, seeded per (anchor id, bar time)
		var pv = new List<double>(); for (int k = 0; k < 15; k++) pv.Add(10 + 7 * k);
		Func<_AvwapManager> mk = () => { var x = Mgr(EAvwapAnchor.VolumeClimax, l => l.placebos = EAvwapPlacebo.P2); x.volumeZ = () => 4; x.volumePool = s2 => pv; return x; };
		var m1 = mk(); var m2 = mk();
		foreach (var b in bs) { m1.Add(b, new AvwapPaEvents()); m2.Add(b, new AvwapPaEvents()); }
		var a1 = m1.Anchors[0];
		double sw = 0, swp = 0;
		foreach (var b in new[] { bs[2] }) { double v = pv[new _Rng(_Rng.Seed(a1.Id, b.time.Ticks, 2)).NextInt(pv.Count)]; sw += v; swp += v * Tp(b); }
		Check(Near(a1.P2, m2.Anchors[0].P2) && Near(a1.P2, swp / sw), "S3 P2 reproducible and equals the hand-drawn seeded value");

		// small pool -> own volume, flagged and journaled once
		var j2 = new List<string>();
		var m3 = Mgr(EAvwapAnchor.VolumeClimax, l => l.placebos = EAvwapPlacebo.P2, j2); m3.volumeZ = () => 4; m3.volumePool = s2 => new double[] { 1, 2, 3 };
		m3.Add(bs[0], new AvwapPaEvents()); m3.volumeZ = () => double.NaN; m3.Add(bs[2], new AvwapPaEvents());
		Check(m3.Anchors[0].p2Degraded && Near(m3.Anchors[0].P2, m3.Anchors[0].Value) && j2.Count(s => s.Contains("p2Degraded")) == 1, "S3 pool < p2MinPool -> own volume, p2Degraded journaled once");

		// back-filled swing twins equal a forward build (ring carries TP, time and the pool each bar saw)
		var mb = Mgr(EAvwapAnchor.SwingLow, l => { l.backfillBars = 3; l.placebos = EAvwapPlacebo.P1 | EAvwapPlacebo.P2; });
		int seen = 0; mb.volumePool = s2 => { seen++; var r = new List<double>(); for (int k = 0; k < 12; k++) r.Add(seen * 10 + k); return r; };
		var sb = new List<AvwapBar>(); double[] lows = { 100, 99, 97, 98, 99.5 };
		for (int i = 0; i < lows.Length; i++) sb.Add(Bar(i, D0.AddMinutes(i), lows[i] + 1, lows[i] + 2, lows[i], lows[i] + 1.5, 100 + 10 * i));
		for (int i = 0; i < 4; i++) mb.Add(sb[i], new AvwapPaEvents());
		mb.Add(sb[4], new AvwapPaEvents { swingLow = true, swingLowBar = 2, swingLowTime = sb[2].time });
		var sa = mb.Latest(EAvwapAnchor.SwingLow);
		double q = 0, qp = 0, t1 = 0;
		for (int i = 2; i <= 4; i++)
		{
			var poolI = new List<double>(); for (int k = 0; k < 12; k++) poolI.Add((i + 1) * 10 + k);   // the pool bar i saw
			double v = poolI[new _Rng(_Rng.Seed(sa.Id, sb[i].time.Ticks, 2)).NextInt(12)];
			q += v; qp += v * Tp(sb[i]); t1 += Tp(sb[i]);
		}
		Check(Near(sa.P2, qp / q) && Near(sa.P1, t1 / 3), "S3 back-filled swing P1/P2 equal the forward build (per-bar pool kept in the ring)");

		// roll clears the twins with the anchors
		m.OnRoll(D0.AddMinutes(5));
		Check(m.Anchors.Count == 0 && a.Expired, "S3 roll clears placebo twins with their anchors");
	}
}
