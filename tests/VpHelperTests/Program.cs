using System;
using System.Collections.Generic;
using PowerLanguage.Function;

// Phase 3 VP helper unit tests (phase3-specs.md VP-1..VP-6, §0.2/§0.4/§0.6, with the Analyst 2 review corrections).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double tol = 1e-9) { return Math.Abs(a - b) <= tol; }
	static readonly DateTime D0 = new DateTime(2026, 10, 19);   // a Monday

	static RingBar B(int bar, DateTime t, double o, double h, double l, double c, double v, DateTime? day = null, bool rth = true, bool roll = false)
	{
		return new RingBar { bar = bar, time = t, tradingDay = (day ?? t.Date), o = o, h = h, l = l, c = c, v = v, rth = rth, rollBar = roll };
	}

	static int Main()
	{
		Rng(); Ring(); RollWin(); RangeProfile(); Weekly(); Ladder(); Stats(); Fvgs(); Bootstrap(); Kde(); Vt(); VpConf(); VpDevWeight();
		VwapRollReset();
		Console.WriteLine($"VpHelperTests: {s_pass} passed, {s_fail} failed");
		return s_fail == 0 ? 0 : 1;
	}

	static void Rng()
	{
		var r = new _Rng(0);
		Check(r.Next() == 0xE220A8397B1DCDAFUL && r.Next() == 0x6E789E6AA1B965F4UL && r.Next() == 0x06C45D188009454FUL && r.Next() == 0xF88BB8A8724C81ECUL, "SplitMix64 seed-0 vectors");
		var a = new _Rng(_Rng.Seed(25, D0.Ticks, 5, 500)); var b = new _Rng(_Rng.Seed(25, D0.Ticks, 5, 500));
		bool same = true, inRange = true; var hits = new int[7];
		for (int i = 0; i < 7000; i++) { int x = a.NextInt(7); same &= x == b.NextInt(7); inRange &= x >= 0 && x < 7; if (x >= 0 && x < 7) hits[x]++; }
		Check(same && inRange, "NextInt deterministic and in range");
		bool spread = true; foreach (int h in hits) spread &= h > 800 && h < 1200;
		Check(spread, "NextInt roughly uniform");
		Check(_Rng.Seed(1, 2) != _Rng.Seed(2, 1), "Seed order-sensitive");
		double d = new _Rng(7).NextDouble(); Check(d >= 0 && d < 1, "NextDouble in [0,1)");
	}

	static void Ring()
	{
		var ring = new _BarRing(3);
		for (int i = 1; i <= 5; i++) ring.Add(B(i, D0.AddHours(10).AddMinutes(i), 1, 1, 1, 1, 1));
		Check(ring.Count == 3 && ring[0].bar == 5 && ring.At(0).bar == 3 && ring.Dropped, "ring wraps, newest first / chronological");
		Check(!ring.Add(B(5, D0.AddHours(10).AddMinutes(5), 1, 1, 1, 1, 1)), "ring ignores a repeated bar (recalc idempotence)");
		Check(ring.IndexAtOrBefore(D0.AddHours(10).AddMinutes(4).AddSeconds(30)) == 1 && ring.IndexAtOrBefore(D0) == -1, "IndexAtOrBefore");
		Check(ring.FirstIndexAtOrAfter(D0.AddHours(10).AddMinutes(4)) == 1 && ring.FirstIndexAtOrAfter(D0.AddHours(10).AddMinutes(4), true) == 2, "FirstIndexAtOrAfter incl/excl");
		Check(!ring.Covers(D0.AddHours(10).AddMinutes(2)) && ring.Covers(D0.AddHours(10).AddMinutes(3)), "Covers");
	}

	static void RollWin()
	{
		var w = new _RollWindow();
		for (int i = 0; i < 6; i++) w.Observe(D0.AddDays(i), i == 3);
		Check(!w.IsRollWindow(D0.AddDays(1)) && w.IsRollWindow(D0.AddDays(2)) && w.IsRollWindow(D0.AddDays(3)) && w.IsRollWindow(D0.AddDays(4)) && !w.IsRollWindow(D0.AddDays(5)),
			"roll session +-1 window (review M2)");
		var lr = LevelRef.Line(ELevelKind.RangePoc, 100.25, D0, D0, D0, 1, "RP:x");
		Check(lr.StudyType == "RangePoc" && lr.lo == lr.hi && double.IsNaN(lr.quality) && !lr.isRollWindow, "LevelRef line");
	}

	// ---------------- VP-1 ----------------
	static void RangeProfile()
	{
		var t = D0.AddHours(10);
		var ring = new _BarRing(100);
		ring.Add(B(1, t.AddMinutes(1), 100, 100.5, 100, 100.5, 300));
		ring.Add(B(2, t.AddMinutes(2), 100.25, 100.25, 100.25, 100.25, 50));
		ring.Add(B(3, t.AddMinutes(3), 100.75, 101, 100.75, 101, 20));
		var cfg = new RangeProfileConfig { enabled = true };
		RangeHistogram h = RangeProfileBuilder.Build(ring, t, t.AddMinutes(3), 0.25, cfg, null);
		Check(h != null && h.Rows == 5 && Near(h.totalVol, 370) && Near(h.prices[_ProfileMath.PocIndex(h.vols)], 100.25) && Near(h.vols[1], 150), "VP-1 T1 uniform POC 100.25 (150), total 370, 5 rows");
		Check(h.bars == 3 && h.approximate && !h.isPartial && Near(h.high, 101) && Near(h.low, 100) && Near(h.open, 100) && Near(h.close, 101) && h.bid == null, "VP-1 OHLC / flags");
		var close = RangeProfileBuilder.Build(ring, t, t.AddMinutes(3), 0.25, new RangeProfileConfig { distribution = 1 }, null);
		Check(close.Rows == 4 && Near(close.vols[1], 300) && Near(close.vols[0], 50) && Near(close.vols[3], 20), "VP-1 T2 close mode");
		Check(RangeProfileBuilder.Build(ring, t.AddMinutes(1).AddSeconds(1), t.AddMinutes(3), 0.25, cfg, null).bars == 2, "VP-1 t0 inclusive at bar stamp");
		Check(RangeProfileBuilder.Build(ring, t.AddMinutes(1), t.AddMinutes(3), 0.25, cfg, null, true).bars == 2, "VP-1 t0Exclusive (A2-30 window)");
		Check(RangeProfileBuilder.Build(ring, t.AddMinutes(5), t.AddMinutes(9), 0.25, cfg, null) == null, "VP-1 empty window -> null");

		// T3 rolls
		var rr = new _BarRing(10);
		for (int i = 1; i <= 6; i++) rr.Add(B(i, t.AddMinutes(i), 100, 101, 100, 100.5, 10, null, true, i == 4));
		Check(RangeProfileBuilder.Build(rr, t, t.AddMinutes(6), 0.25, cfg, null) == null, "VP-1 T3 Reject with a roll inside");
		var tr = RangeProfileBuilder.Build(rr, t, t.AddMinutes(6), 0.25, new RangeProfileConfig { rollPolicy = ERangeRollPolicy.Truncate }, null);
		Check(tr != null && tr.bars == 3 && tr.startBar == 4 && tr.isPartial, "VP-1 T3 Truncate starts at the roll bar");
		Check(RangeProfileBuilder.Build(rr, t.AddMinutes(4), t.AddMinutes(6), 0.25, cfg, null) != null, "VP-1 roll on the first bar is fine");

		// T4 coverage: 390 RTH slots (09:30-16:00 open), 5 bars removed
		DateTime rth0 = D0.AddHours(9).AddMinutes(30);
		Func<DateTime, bool> open = s => s >= rth0 && s < D0.AddHours(16);
		int exp = RangeProfileBuilder.ExpectedSlots(rth0.AddMinutes(1), D0.AddHours(16), TimeSpan.FromMinutes(1), open);
		var cr = new _BarRing(500); int n = 0;
		for (int i = 1; i <= 390; i++) if (i % 70 != 0) cr.Add(B(++n, rth0.AddMinutes(i), 100, 100.25, 100, 100, 5));
		var st = RangeProfileBuilder.Stats(RangeProfileBuilder.Build(cr, rth0, D0.AddHours(16), 0.25, cfg, null), exp);
		Check(exp == 390 && st.bars == 385 && Near(st.coverage, 385.0 / 390), $"VP-1 T4 coverage 385/390 (got {st.bars}/{exp})");
		Check(RangeProfileBuilder.ExpectedSlots(D0.AddHours(13), D0.AddHours(14), TimeSpan.FromMinutes(1), s => s < D0.AddHours(13).AddMinutes(30)) == 31, "VP-1 expected slots honour an early close (13:30)");

		// T5 handle incremental == one-shot after 500 seeded random bars
		var rng = new _Rng(_Rng.Seed(5)); var big = new _BarRing(600);
		var hd = new RangeProfileHandle(1, rth0, null, 0.25, cfg);
		double px = 4000;
		for (int i = 1; i <= 500; i++)
		{
			double o = px, c = px + (rng.NextInt(9) - 4) * 0.25, hi = Math.Max(o, c) + rng.NextInt(4) * 0.25, lo = Math.Min(o, c) - rng.NextInt(4) * 0.25;
			var b = B(i, rth0.AddMinutes(i), o, hi, lo, c, 1 + rng.NextInt(500)); px = c;
			big.Add(b); hd.Step(b, null);
			if (i == 250) { var mid = hd.Profile; Check(mid.bars == 250, "VP-1 handle lazily rebuilt mid-way"); }
		}
		var one = RangeProfileBuilder.Build(big, rth0, rth0.AddMinutes(500), 0.25, cfg, null);
		var inc = hd.Profile;
		bool eq = one.Rows == inc.Rows && one.bars == inc.bars;
		for (int i = 0; eq && i < one.Rows; i++) eq = one.prices[i] == inc.prices[i] && one.vols[i] == inc.vols[i];
		Check(eq, "VP-1 T5 handle incremental == one-shot (exact)");
		var hb = new RangeProfileHandle(1, rth0.AddMinutes(100), null, 0.25, cfg); hb.Backfill(big, null);
		Check(hb.Profile.bars == 401, "VP-1 handle backfill from ring");
		var hr = new RangeProfileHandle(1, rth0, "x", 0.25, cfg);
		hr.Step(B(1, rth0.AddMinutes(1), 1, 1, 1, 1, 1), null);
		Check(hr.Step(B(2, rth0.AddMinutes(2), 1, 1, 1, 1, 1, null, true, true), null) && hr.IsClosed && hr.CrossesRoll && hr.Bars == 1, "VP-1 handle closes at a roll with crossesRoll");

		// partial when the ring dropped bars at/after t0
		var small = new _BarRing(10);
		for (int i = 1; i <= 20; i++) small.Add(B(i, rth0.AddMinutes(i), 100, 100, 100, 100, 1));
		Check(RangeProfileBuilder.Build(small, rth0, rth0.AddMinutes(20), 0.25, cfg, null).isPartial, "VP-1 t0 older than the ring -> isPartial");

		// T6 ladder override; review H2: ignored unless useTickLadder (default false)
		var lad = new LadderBar { bar = 2, tick = 0.25, source = _OrderFlow.FlowSource.TickRule, quality = _OrderFlow.FlowQuality.Medium,
			rows = new long[] { 401, 402 }, bid = new double[] { 10, 0 }, ask = new double[] { 30, 10 }, total = new double[] { 40, 10 } };
		Func<RingBar, LadderBar> lf = b => b.bar == 2 ? lad : null;
		Check(!new RangeProfileConfig().useTickLadder, "VP-1 useTickLadder defaults to false (review H2)");
		var noLad = RangeProfileBuilder.Build(ring, t, t.AddMinutes(3), 0.25, cfg, lf);
		Check(Near(noLad.vols[1], 150) && noLad.approximate, "VP-1 ladder ignored by default");
		var withLad = RangeProfileBuilder.Build(ring, t, t.AddMinutes(3), 0.25, new RangeProfileConfig { useTickLadder = true }, lf);
		Check(Near(withLad.vols[0], 100) && Near(withLad.vols[1], 140) && Near(withLad.vols[2], 110) && Near(withLad.totalVol, 370) && withLad.ladderBars == 1 && withLad.approximate && withLad.bid == null,
			"VP-1 T6 ladder bar overrides the uniform distribution");
		var allLad = new _BarRing(5); allLad.Add(B(2, t, 100.25, 100.5, 100.25, 100.5, 50));
		var al = RangeProfileBuilder.Build(allLad, t, t, 0.25, new RangeProfileConfig { useTickLadder = true }, lf);
		Check(!al.approximate && al.bid != null && Near(al.ask[0], 30) && Near(al.bid[0], 10), "VP-1 all-ladder window is exact with bid/ask");
	}

	// ---------------- VP-2 ----------------
	static Dictionary<long, double[]> Rows(params double[] kv)
	{
		var d = new Dictionary<long, double[]>();
		for (int i = 0; i < kv.Length; i += 2) d[(long)kv[i]] = new[] { kv[i + 1], double.NaN, double.NaN };
		return d;
	}

	static void Weekly()
	{
		var w = new _WeeklyProfiles(new WeeklyProfileConfig { enabled = true }, 0.25);
		for (int i = 0; i < 5; i++) w.AddSession(D0.AddDays(i), Rows(400 + i, 10, 500, 5), false, false, 100, 101, 99, 100, 390);
		w.AddSession(D0.AddDays(7), Rows(400, 1), false, false, 100, 101, 99, 100, 390);
		WeekProfile wk = w.Week(1);
		double sum = 0; foreach (var kv in wk.rows) sum += kv.Value[0];
		Check(w.CompletedWeeks == 1 && wk.Sessions == 5 && Near(sum, 75) && Near(wk.rows[500][0], 25) && !wk.isPartial && !wk.bidAskValid, "VP-2 T1 weekly sum = row sum of 5 sessions");
		double[] p, tot, ask, bid;
		Check(wk.Dense(out p, out tot, out ask, out bid) && p.Length == 101 && ask == null, "VP-2 dense week");
		Check(w.Week(0) != null && w.Week(0).Sessions == 1 && w.WeekKey(0) == D0.AddDays(7), "VP-2 developing week");

		var h = new _WeeklyProfiles(new WeeklyProfileConfig(), 0.25);
		h.AddSession(new DateTime(2026, 11, 23), Rows(1, 1), false, false, 1, 1, 1, 1, 1);
		h.AddSession(new DateTime(2026, 11, 24), Rows(1, 1), false, false, 1, 1, 1, 1, 1);
		h.AddSession(new DateTime(2026, 11, 30), Rows(1, 1), false, false, 1, 1, 1, 1, 1);
		Check(h.Week(1).isPartial && h.Week(1).Sessions == 2, "VP-2 T2 holiday week with 2 sessions is partial");

		Check(_WeeklyProfiles.WeekKeyOf(new DateTime(2026, 12, 28)) == _WeeklyProfiles.WeekKeyOf(new DateTime(2027, 1, 1)) && _WeeklyProfiles.WeekKeyOf(new DateTime(2027, 1, 1)) == new DateTime(2026, 12, 28),
			"VP-2 T3 year-boundary week key");
		Check(_WeeklyProfiles.WeekKeyOf(new DateTime(2026, 10, 25)) == D0, "VP-2 Sunday maps to the Monday-start week (Sunday evening belongs to Monday's Globex date upstream)");

		var r = new _WeeklyProfiles(new WeeklyProfileConfig(), 0.25);
		r.AddSession(D0, Rows(1, 1), false, false, 1, 1, 1, 1, 1);
		r.AddSession(D0.AddDays(1), Rows(1, 1), false, false, 1, 1, 1, 1, 1);
		r.AddSession(D0.AddDays(2), Rows(2, 1), true, false, 1, 1, 1, 1, 1);
		r.AddSession(D0.AddDays(3), Rows(2, 1), false, false, 1, 1, 1, 1, 1);
		Check(r.CompletedWeeks == 1 && r.Week(1).crossesRoll && r.Week(1).Sessions == 2 && r.Week(0).isPartial && r.Week(0).Sessions == 2 && r.Week(0).key == D0, "VP-2 T4 mid-week roll splits the week");

		var c = new _WeeklyProfiles(new WeeklyProfileConfig(), 0.25);
		for (int k = 0; k < 4; k++) for (int i = 0; i < 5; i++) c.AddSession(D0.AddDays(7 * k + i), Rows(100 + k, 1, 200, 2), false, false, 1, 1, 1, 1, 1);
		c.Flush();
		WeekProfile comp = c.Composite(3);
		Check(comp != null && comp.Sessions == 15 && Near(comp.rows[200][0], 30) && Near(comp.rows[101][0], 5) && !comp.rows.ContainsKey(100), "VP-2 T5 3-week composite = row-wise sum");
		Check(c.Composite(5) == null, "VP-2 composite null with too few weeks");
		var cr = new _WeeklyProfiles(new WeeklyProfileConfig(), 0.25);
		for (int k = 0; k < 4; k++) for (int i = 0; i < 5; i++) cr.AddSession(D0.AddDays(7 * k + i), Rows(1, 1), k == 2 && i == 0, false, 1, 1, 1, 1, 1);
		cr.Flush();
		Check(cr.Composite(3) == null && cr.Composite(2) != null, "VP-2 T5 composite null across a roll (week-start roll)");
		var cm = new _WeeklyProfiles(new WeeklyProfileConfig(), 0.25);
		for (int k = 0; k < 3; k++) for (int i = 0; i < 5; i++) cm.AddSession(D0.AddDays(7 * k + i), Rows(1, 1), k == 1 && i == 2, false, 1, 1, 1, 1, 1);
		cm.Flush();
		Check(cm.CompletedWeeks == 4 && cm.Composite(3) == null && cm.Composite(2) != null && cm.Composite(2).isPartial && !cm.Composite(1).isPartial, "VP-2 composite null across a mid-week roll; post-roll part is partial");
		// ruling 5: a roll on Monday starts a FULL week; the composite still refuses to span it
		var mo = new _WeeklyProfiles(new WeeklyProfileConfig(), 0.25);
		for (int k = 0; k < 3; k++) for (int i = 0; i < 5; i++) mo.AddSession(D0.AddDays(7 * k + i), Rows(1, 1), k == 1 && i == 0, false, 1, 1, 1, 1, 1);
		mo.Flush();
		Check(mo.CompletedWeeks == 3 && !mo.Week(2).isPartial && !mo.Week(2).crossesRoll && mo.Week(2).Sessions == 5 && !mo.Week(3).crossesRoll && !mo.Week(3).isPartial,
			"VP-2 ruling 5: Monday roll -> full week, prior week untouched");
		Check(mo.Composite(2) != null && !mo.Composite(2).isPartial && mo.Composite(3) == null, "VP-2 ruling 5: composite refuses to span a Monday roll");
		Check(cm.Week(2).isPartial && cm.Week(2).Sessions == 3 && cm.Week(3).crossesRoll && cm.Week(3).Sessions == 2, "VP-2 ruling 5: mid-week roll -> pre-roll crossesRoll, post-roll segment partial");
		cm.RefreshRollWindow(d => d == D0.AddDays(9));
		Check(cm.Week(2).sessions[0].isRollWindow && cm.Week(2).IsRollWindow, "VP-2 session record carries isRollWindow (review M2)");
	}

	// ---------------- VP-3 ----------------
	static _OrderFlow.FootprintBar Fp(_OrderFlow.FlowSource src, double tick, params double[] pbs)   // price, bid(sell), ask(buy) triples
	{
		var fp = new _OrderFlow.FootprintBar { Tick = tick, Source = src, Quality = src == _OrderFlow.FlowSource.Quote ? _OrderFlow.FlowQuality.High : _OrderFlow.FlowQuality.Medium };
		for (int i = 0; i < pbs.Length; i += 3)
		{
			if (pbs[i + 1] > 0) fp.add(pbs[i], pbs[i + 1], _OrderFlow.Side.Sell);
			if (pbs[i + 2] > 0) fp.add(pbs[i], pbs[i + 2], _OrderFlow.Side.Buy);
		}
		return fp;
	}

	static void Ladder()
	{
		var rb = B(1, D0.AddHours(10), 100, 100.25, 100, 100.25, 13);
		var lb = _BarLadderStore.Build(rb, Fp(_OrderFlow.FlowSource.TickRule, 0.25, 100.00, 6, 2, 100.25, 0, 5), 0.25);
		Check(lb.HasBidAsk && lb.rows.Length == 2 && Near(lb.bid[0], 6) && Near(lb.ask[0], 2) && Near(lb.bid[1], 0) && Near(lb.ask[1], 5) && Near(lb.Buy, 7) && Near(lb.Sell, 6) && Near(lb.Delta, 1),
			"VP-3 T1 tick-rule ladder: Buy 7, Sell 6, delta +1");
		var q = _BarLadderStore.Build(rb, Fp(_OrderFlow.FlowSource.Quote, 0.25, 100.00, 1, 3, 100.25, 0, 2), 0.25);
		Check(q.source == _OrderFlow.FlowSource.Quote && Near(q.Delta, 4) && q.quality == _OrderFlow.FlowQuality.High, "VP-3 T2 Lee-Ready (Quote) delta +4");

		var parts = new List<LadderBar>(); var rbs = new List<RingBar>();
		for (int i = 0; i < 30; i++)
		{
			var b = B(i + 1, D0.AddHours(10).AddMinutes(i + 1), 100, 100.25, 100, 100, 3);
			rbs.Add(b);
			parts.Add(_BarLadderStore.Build(b, Fp(i == 7 ? _OrderFlow.FlowSource.TickRule : _OrderFlow.FlowSource.Quote, 0.25, 100.00, 1, 0, 100.25, 0, 2), 0.25));
		}
		var htf = _BarLadderStore.Merge(parts, rbs, 30, 1, D0.AddHours(10).AddMinutes(30), 0.25);
		Check(Near(htf.Volume, 90) && Near(htf.bid[0], 30) && Near(htf.ask[1], 60) && Near(htf.Delta, 30) && htf.source == _OrderFlow.FlowSource.TickRule && htf.quality == _OrderFlow.FlowQuality.Medium && Near(htf.coverage, 1),
			"VP-3 T3 HTF merge of 30 bars = sum, worst source/quality");
		var miss = _BarLadderStore.Merge(parts.GetRange(0, 29), rbs.GetRange(0, 29), 30, 1, D0.AddHours(10).AddMinutes(30), 0.25);
		Check(miss.coverage < 1 && miss.source == _OrderFlow.FlowSource.BarUpDown && double.IsNaN(miss.bid[0]) && Near(miss.Volume, 87) && double.IsNaN(miss.Delta), "VP-3 T4 missing minute -> totals-only");
		var bud = _BarLadderStore.BarUpDown(B(1, D0, 100, 101, 100, 100.5, 50), 0.25);
		double s = 0; foreach (double x in bud.total) s += x;
		Check(bud.source == _OrderFlow.FlowSource.BarUpDown && double.IsNaN(bud.bid[0]) && double.IsNaN(bud.ask[4]) && Near(s, 50) && bud.rows.Length == 5 && !bud.HasBidAsk, "VP-3 T5 BarUpDown NaN bid/ask, sum total = V");
		Check(_BarLadderStore.Build(rb, null, 0.25).source == _OrderFlow.FlowSource.BarUpDown, "VP-3 no footprint -> BarUpDown");
		var store = new _BarLadderStore(new BarLadderConfig { keepBars = 2 }, 0.25);
		store.Get(B(1, D0, 1, 1, 1, 1, 1), null); store.Get(B(2, D0, 1, 1, 1, 1, 1), null); store.Get(B(3, D0, 1, 1, 1, 1, 1), null);
		LadderBar tmp;
		Check(store.Count == 2 && !store.TryGet(1, out tmp) && store.TryGet(3, out tmp), "VP-3 store keeps keepBars");
		var rollPart = _BarLadderStore.Merge(new List<LadderBar> { _BarLadderStore.BarUpDown(B(1, D0, 1, 1, 1, 1, 1, null, true, true), 0.25) }, null, 1, 1, D0, 0.25);
		Check(rollPart.crossesRoll, "VP-3 HTF bar containing the splice is crossesRoll");
	}

	// ---------------- VP-4 ----------------
	static void Stats()
	{
		var u = _ProfileStats.Entropy(new double[] { 5, 5, 5, 5 }, EEntropyRows.DenseRange, 10);
		Check(Near(u.Hnorm, 1) && Near(u.Neff, 4), "VP-4 T1 uniform 4 rows: H_norm 1, n_eff 4");
		var one = _ProfileStats.Entropy(new double[] { 9 }, EEntropyRows.DenseRange, 10);
		Check(Near(one.H, 0) && Near(one.Neff, 1) && double.IsNaN(one.Hnorm), "VP-4 T2 one row: H 0, n_eff 1, H_norm NaN");
		var mm = _ProfileStats.Entropy(new double[] { 1, 0, 1, 1 }, EEntropyRows.DenseRange, 10);
		Check(Near(mm.Hmm - mm.H, 0.1) && mm.m == 3 && mm.n == 4, "VP-4 T3 MM m=3 N=10 adds 0.1");
		var noclip = _ProfileStats.Entropy(new double[] { 1, 1, 1, 1 }, EEntropyRows.DenseRange, 30);
		Check(noclip.HnormMm > 1 && Near(noclip.HnormMm, (Math.Log(4) + 3.0 / 60) / Math.Log(4)), "VP-4 H_norm,MM not clipped at 1 (review H3)");
		var traded = _ProfileStats.Entropy(new double[] { 1, 0, 0, 1 }, EEntropyRows.TradedRows, 10);
		Check(traded.n == 2 && Near(traded.Hnorm, 1), "VP-4 TradedRows option");
		Check(Near(_ProfileStats.MmN(EMmN.Bars, 30, double.NaN, 1e6), 30) && double.IsNaN(_ProfileStats.MmN(EMmN.Trades, 30, double.NaN, 1e6)), "VP-4 MM N modes");

		double[] pp = { 100, 100.25 };
		Check(Near(_ProfileStats.KL(pp, new double[] { 3, 7 }, pp, new double[] { 3, 7 }, 0.25, 1e-6), 0), "VP-4 T4 KL(P||P) = 0");
		Check(Near(_ProfileStats.KL(pp, new double[] { 0.5, 0.5 }, pp, new double[] { 0.9, 0.1 }, 0.25, 1e-6), 0.5108, 1e-4), "VP-4 T4 KL([.5,.5]||[.9,.1]) = 0.5108");
		double klOut = _ProfileStats.KL(new double[] { 101 }, new double[] { 1 }, pp, new double[] { 1, 1 }, 0.25, 1e-6);
		double klk = _ProfileStats.KLKde(new double[] { 101 }, new double[] { 1 }, pp, new double[] { 1, 1 }, 0.25, 0.5, 1e-6);
		Check(klOut > 10 && klk > 0 && klk < klOut, "VP-4 KL on KDE densities (review M4) is finite and smaller off-range");
		Check(Near(_ProfileStats.KLKde(pp, new double[] { 3, 7 }, pp, new double[] { 3, 7 }, 0.25, 0.5, 1e-6), 0), "VP-4 KLKde(P||P) = 0");

		var pk = new List<RingBar> { B(1, D0, 1, Math.Exp(0.02), 1, 1, 1), B(2, D0, 1, Math.Exp(0.04), 1, 1, 1) };
		Check(Near(_ProfileStats.ParkinsonPerBar(pk), 0.018992, 1e-6), "VP-4 T5 Parkinson sigma 0.018992");
		pk.Add(B(3, D0, 1, Math.Exp(0.5), 1, 1, 1, null, true, true));
		Check(Near(_ProfileStats.ParkinsonPerBar(pk), 0.018992, 1e-6), "VP-4 Parkinson excludes the splice bar");
		Check(double.IsNaN(_ProfileStats.ParkinsonSessionPrice(pk, 4000, 60)) && Near(_ProfileStats.ParkinsonSessionPrice(pk, 4000, 2), 4000 * 0.0189922 * Math.Sqrt(2), 1e-2),
			"VP-4 session-scale Parkinson; < minBars -> NaN (dropped from pool)");
		Check(Near(_ProfileStats.EwmaForecast(new List<double> { 1, double.NaN, 2 }, 10), 2.0 / 11 * 2 + 9.0 / 11 * 1) && double.IsNaN(_ProfileStats.EwmaForecast(new List<double>(), 10)), "VP-4 EWMA sigma-hat span 10");

		// T6 percentile with ties / min fill
		var tk = new _EntropyTracker(10, 0.8, new[] { 660 });
		for (int i = 0; i < 7; i++) tk.Push(660, D0.AddDays(i), i < 4 ? 0.5 : 0.7);
		Check(double.IsNaN(tk.Percentile(660, 0.5)), "VP-4 T6 percentile NaN below min fill");
		tk.Push(660, D0.AddDays(7), 0.9);
		Check(Near(tk.Percentile(660, 0.5), 100.0 * (0 + 0.5 * 4) / 8) && Near(tk.Percentile(660, 0.7), 100.0 * (4 + 0.5 * 3) / 8), "VP-4 T6 mid-rank with ties");
		Check(!tk.Push(660, D0.AddDays(7), 1) && !tk.Push(661, D0.AddDays(9), 1) && tk.Count(660) == 8, "VP-4 one value per slot per day; only configured slots (review L1)");

		// T7 A2-30 window boundary exclusive at t - 30
		var ring = new _BarRing(100); DateTime t = D0.AddHours(11);
		for (int i = 0; i <= 30; i++) ring.Add(B(i + 1, t.AddMinutes(-30 + i), 100, 100 + (i == 0 ? 5 : 0.25), 100, 100, 10));
		var win = RangeProfileBuilder.Build(ring, t.AddMinutes(-30), t, 0.25, new RangeProfileConfig(), null, true);
		Check(win.bars == 30 && win.Rows == 2, "VP-4 T7 A2-30 window (t-30, t] excludes the bar at t-30");

		// AggregateBars: (t-5, t] end-stamped, never across a trading-day change
		var m1 = new List<RingBar>();
		for (int i = 1; i <= 12; i++) m1.Add(B(i, D0.AddHours(10).AddMinutes(i), 100 + i, 101 + i, 99 + i, 100.5 + i, 1, D0));
		m1.Add(B(13, D0.AddHours(10).AddMinutes(13), 1, 2, 0.5, 1.5, 1, D0.AddDays(1)));
		var a5 = _ProfileStats.AggregateBars(m1, TimeSpan.FromMinutes(5));
		Check(a5.Count == 4 && a5[0].time == D0.AddHours(10).AddMinutes(5) && Near(a5[0].v, 5) && Near(a5[0].o, 101) && Near(a5[0].c, 105.5) && Near(a5[0].h, 106) && Near(a5[0].l, 100)
			&& a5[2].time == D0.AddHours(10).AddMinutes(15) && Near(a5[2].v, 2) && Near(a5[3].v, 1) && a5[3].tradingDay == D0.AddDays(1), "VP-4 AggregateBars clock-aligned, end-stamped, session-truncated");
		var atrBars = new List<RingBar>(); for (int i = 0; i < 3; i++) atrBars.Add(B(i, D0, 10, 12, 10, 11, 1));
		Check(Near(_ProfileStats.WilderAtr(atrBars, 2), (2 + 2) / 2.0 * 0.5 + 2 * 0.5) && double.IsNaN(_ProfileStats.WilderAtr(atrBars, 5)), "VP-4 Wilder ATR");
	}

	// ---------------- VP-5 ----------------
	static List<Fvg> Feed(_FvgDetector d, params RingBar[] bars) { var n = new List<Fvg>(); foreach (var b in bars) { d.Add(b); if (d.NewThisBar != null) n.Add(d.NewThisBar); } return n; }

	static void Fvgs()
	{
		DateTime t = D0.AddHours(10); var res = TimeSpan.FromMinutes(1);
		var d = new _FvgDetector(new FvgConfig { enabled = true }, 0.25, res);
		var nb = Feed(d, B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1), B(3, t.AddMinutes(3), 102.5, 104, 101.5, 103.5, 1));
		Check(nb.Count == 1 && nb[0].dir == 1 && Near(nb[0].lo, 101) && Near(nb[0].hi, 101.5) && Near(nb[0].start, 101) && nb[0].createdTime == t.AddMinutes(3), "VP-5 T1 bullish zone exact");
		var e = new _FvgDetector(new FvgConfig(), 0.25, res);
		var nbe = Feed(e, B(1, t.AddMinutes(1), 100, 100.5, 99, 99.5, 1), B(2, t.AddMinutes(2), 99, 99.5, 97, 97.5, 1), B(3, t.AddMinutes(3), 97.5, 98, 96, 96.5, 1));
		Check(nbe.Count == 1 && nbe[0].dir == -1 && Near(nbe[0].lo, 98) && Near(nbe[0].hi, 99) && Near(nbe[0].start, 99), "VP-5 T1 bearish zone exact");
		Check(Feed(new _FvgDetector(new FvgConfig(), 0.25, res), B(1, t.AddMinutes(1), 100, 101, 99.5, 101, 1), B(2, t.AddMinutes(2), 101, 102, 101, 102, 1), B(3, t.AddMinutes(3), 101, 102, 101, 102, 1)).Count == 0, "VP-5 T2 equal edges -> none");
		Check(Feed(new _FvgDetector(new FvgConfig { minTicks = 3 }, 0.25, res), B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1), B(3, t.AddMinutes(3), 102.5, 104, 101.5, 103.5, 1)).Count == 0,
			"VP-5 T3 minTicks filter");
		var atrD = new _FvgDetector(new FvgConfig { minAtrFrac = 0.1, atrLen = 3 }, 0.25, res);
		Check(Feed(atrD, B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1), B(3, t.AddMinutes(3), 102.5, 104, 101.5, 103.5, 1)).Count == 1, "VP-5 T3 ATR filter passes (0.5 >= 0.1 ATR)");
		var atrD2 = new _FvgDetector(new FvgConfig { minAtrFrac = 0.5, atrLen = 3 }, 0.25, res);
		Check(Feed(atrD2, B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1), B(3, t.AddMinutes(3), 102.5, 104, 101.5, 103.5, 1)).Count == 0, "VP-5 T3 ATR filter rejects");
		Check(Feed(new _FvgDetector(new FvgConfig(), 0.25, res), B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1, D0), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1, D0.AddDays(1)), B(3, t.AddMinutes(3), 102.5, 104, 101.5, 103.5, 1, D0.AddDays(1))).Count == 0,
			"VP-5 T4 session boundary -> none");
		Check(Feed(new _FvgDetector(new FvgConfig(), 0.25, res), B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1), B(3, t.AddMinutes(5), 102.5, 104, 101.5, 103.5, 1)).Count == 0,
			"VP-5 T4 time gap > 2.5 bars -> none");

		var fc = new _FvgDetector(new FvgConfig(), 0.25, res);
		Feed(fc, B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1), B(3, t.AddMinutes(3), 102.5, 104, 101.5, 103.5, 1));
		Fvg g = fc.Live[0];
		fc.Add(B(4, t.AddMinutes(4), 103, 103, 101.25, 102, 1));
		Check(g.touched && g.firstTouchTime == t.AddMinutes(4) && Near(g.fillPct, 0.5) && !g.filled, "VP-5 touch and fillPct");
		fc.Add(B(5, t.AddMinutes(5), 102, 102, 100.5, 101.25, 1));
		Check(!g.filled && Near(g.fillPct, 1), "VP-5 T5 Close mode: trading through is not a fill");
		fc.Add(B(6, t.AddMinutes(6), 101, 101.25, 100.5, 100.75, 1));
		Check(g.filled && fc.Live.Count == 0, "VP-5 T5 Close below lo fills");
		var ft = new _FvgDetector(new FvgConfig { fillOn = EFvgFillOn.Trade }, 0.25, res);
		Feed(ft, B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1), B(3, t.AddMinutes(3), 102.5, 104, 101.5, 103.5, 1));
		Fvg gt = ft.Live[0]; ft.Add(B(4, t.AddMinutes(4), 102, 102, 100.5, 101.25, 1));
		Check(gt.filled, "VP-5 T5 Trade mode: low below lo fills");

		var fr = new _FvgDetector(new FvgConfig(), 0.25, res);
		Feed(fr, B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1), B(3, t.AddMinutes(3), 102.5, 104, 101.5, 103.5, 1));
		Fvg gr = fr.Live[0]; fr.Add(B(4, t.AddMinutes(4), 110, 111, 110, 110.5, 1, null, true, true));
		Check(gr.invalid && fr.Live.Count == 0, "VP-5 T6 roll invalidates");

		var fm = new _FvgDetector(new FvgConfig { maxLive = 2 }, 0.25, res);
		double px = 100;
		for (int i = 0; i < 12; i++) { fm.Add(B(i + 1, t.AddMinutes(i + 1), px, px + 1, px, px + 1, 1)); px += 2; }
		Check(fm.Live.Count == 2 && fm.Live[1].createdTime == t.AddMinutes(12), "VP-5 T7 maxLive eviction (oldest out)");

		var fx = new _FvgDetector(new FvgConfig { maxAgeSessions = 1 }, 0.25, res);
		Feed(fx, B(1, t.AddMinutes(1), 100, 101, 99.5, 100.75, 1), B(2, t.AddMinutes(2), 101, 103, 100.75, 102.75, 1), B(3, t.AddMinutes(3), 102.5, 104, 101.5, 103.5, 1));
		Fvg gx = fx.Live[0];
		fx.Add(B(4, t.AddDays(1), 110, 111, 110, 110.5, 1, D0.AddDays(1)));
		Check(!gx.expired, "VP-5 age 1 session not yet expired");
		fx.Add(B(5, t.AddDays(2), 110, 111, 110, 110.5, 1, D0.AddDays(2)));
		Check(gx.expired && fx.Live.Count == 0, "VP-5 expiry after maxAgeSessions");
		var lr = fc.ToLevel(g, true);
		Check(lr.kind == ELevelKind.FvgLower && Near(lr.price, 101) && Near(lr.hi, 101.5) && lr.tag.StartsWith("FVG:U:"), "VP-5 ToLevel");
		Check(fm.Nearest(px + 50, 1) == fm.Live[1], "VP-5 Nearest");
	}

	// ---------------- VP-6 ----------------
	static List<RingBar> RandomSession(ulong seed, int n)
	{
		var rng = new _Rng(seed); var res = new List<RingBar>(); double px = 4000;
		for (int i = 0; i < n; i++)
		{
			double o = px, c = px + (rng.NextInt(9) - 4) * 0.25, hi = Math.Max(o, c) + rng.NextInt(3) * 0.25, lo = Math.Min(o, c) - rng.NextInt(3) * 0.25;
			res.Add(B(i + 1, D0.AddHours(9.5).AddMinutes(i + 1), o, hi, lo, c, 50 + rng.NextInt(400))); px = c;
		}
		return res;
	}

	static void Bootstrap()
	{
		var flat = new List<RingBar>(); for (int i = 0; i < 40; i++) flat.Add(B(i + 1, D0.AddMinutes(i), 100, 100, 100, 100, 10));
		var b1 = _ProfileStats.BootstrapPoc(flat, 0.25, 2, 5, 500, _ProfileStats.BootstrapSeed(D0, 5, 500));
		Check(Near(b1.stability, 1) && Near(b1.ci90Ticks, 0) && Near(b1.medianPoc, 100), "VP-6 bootstrap one-row: stability 1, ci90 0");
		var sess = RandomSession(11, 390);
		var x1 = _ProfileStats.BootstrapPoc(sess, 0.25, 2, 5, 200, 42);
		var x2 = _ProfileStats.BootstrapPoc(sess, 0.25, 2, 5, 200, 42);
		var x3 = _ProfileStats.BootstrapPoc(sess, 0.25, 2, 5, 200, 43);
		bool same = true, diff = false;
		for (int i = 0; i < 200; i++) { same &= x1.pocs[i] == x2.pocs[i]; diff |= x1.pocs[i] != x3.pocs[i]; }
		Check(same && diff && x1.ci90Ticks >= 0 && x1.stability > 0 && x1.stability <= 1 && x1.zoneLo <= x1.medianPoc && x1.zoneHi >= x1.medianPoc, "VP-6 bootstrap: same seed identical, different seed differs");
		Check(Near(_ProfileStats.Percentile(new double[] { 1, 2, 3, 4 }, 95), 3.85) && Near(_ProfileStats.Percentile(new double[] { 1, 2, 3, 4 }, 50), 2.5), "VP-6 numpy linear percentile");
		Check(_ProfileStats.BootstrapLevels(x1, D0, D0, D0, 1, "BOOT").Count == 2, "VP-6 bootstrap LevelRefs");
	}

	static List<RingBar> Gaussian(double mu, double sd, double mass, double tick, ref int bar)
	{
		var res = new List<RingBar>();
		for (int k = -40; k <= 40; k++)
		{
			double p = mu + k * tick, z = k * tick / sd, v = mass * Math.Exp(-0.5 * z * z);
			if (v < 1e-3) continue;
			res.Add(B(++bar, D0.AddMinutes(bar), p, p, p, p, v));
		}
		return res;
	}

	static void Kde()
	{
		int bar = 0;
		var g = Gaussian(100, 1.5, 100, 0.25, ref bar);
		var r = _ProfileStats.KdeSweep(g, 0.25, 1);
		Check(r.verdict == EKdeVerdict.Stable && Near(r.cvPoc, 100) && r.h.Length == 30 && r.cvH.Length == 25, $"VP-6 KDE single Gaussian -> STABLE (got {r.verdict})");
		Check(Near(r.cvH[0], 0.15 * r.sigma) && Near(r.cvH[24], 1.2 * r.sigma) && Near(r.cvH[12], r.sigma * Math.Sqrt(0.15 * 1.2), 1e-9), "VP-6 CV grid 0.15..1.2 sigma, 25 log-spaced (review H1)");
		Check(r.hCv > r.cvH[0], $"VP-6 bar-fold CV does not collapse to the smallest h (h_cv/sigma = {r.hCv / r.sigma:0.###})");
		bar = 0;
		// broad heavy cluster vs a narrow taller one: the POC jumps from the narrow peak to the broad one as h grows
		var two = Gaussian(100, 2, 100, 0.25, ref bar); two.AddRange(Gaussian(110, 0.5, 150, 0.25, ref bar));
		var rb = _ProfileStats.KdeSweep(two, 0.25, 1);
		Check(rb.verdict == EKdeVerdict.Bimodal, $"VP-6 KDE two separated clusters -> BIMODAL (got {rb.verdict}, maxJ {rb.maxJ}, medJ {rb.medJ})");
		var lit = _ProfileStats.KdeSweep(g, 0.25, 1, double.NaN, 0);
		Check(lit.verdict == EKdeVerdict.Stable && !lit.disagree, "VP-6 literal Keller arm (floor 0) on a stable Gaussian");
		Check(!_ProfileStats.KdeSweep(g, 0.25, 1, 100.75).disagree && _ProfileStats.KdeSweep(g, 0.25, 1, 101.25).disagree, "VP-6 disagreement flag boundary (4 vs 5 ticks)");
		var single = _ProfileStats.KdeSweep(new List<RingBar> { B(1, D0, 1, 1, 1, 1, 5) }, 0.25, 1);
		Check(single.verdict == EKdeVerdict.Stable, "VP-6 single row -> STABLE");
		var inter = _ProfileStats.KdeSweep(g, 0.25, 1, double.NaN, 1, 5, ECvFolding.Interleaved);
		Check(r.folding == ECvFolding.Contiguous && !double.IsNaN(r.HcvContiguous) && !double.IsNaN(r.HcvInterleaved) && r.HcvContiguous == r.hCv
			&& inter.HcvInterleaved == inter.hCv && inter.HcvContiguous == r.hCv && inter.HcvInterleaved == r.HcvInterleaved, "VP-6 ruling 1: h_cv under both fold schemes");
		Check(r.Journal().Contains("hcvC(") && r.Journal().Contains("hcvI(") && r.Journal().StartsWith("PSTAT KDE STABLE"), "VP-6 ruling 1: KDE journal carries both h_cv");
		var prior = new _PriorDayValue(); prior.Set(D0, 0.5); prior.Set(D0.AddDays(1), 0.9);
		Check(Near(prior.Before(D0.AddDays(1)), 0.5) && double.IsNaN(prior.Before(D0)) && Near(prior.Before(D0.AddDays(3)), 0.9), "VP-4 ruling 2: prior-day h_cv, no lookahead");
		double[] pq = { 100, 100.25 };
		Check(Near(_ProfileStats.KLKdePrior(pq, new double[] { 1, 3 }, pq, new double[] { 3, 1 }, 0.25, 0.5, 1e-6), _ProfileStats.KLKde(pq, new double[] { 1, 3 }, pq, new double[] { 3, 1 }, 0.25, 0.5, 1e-6))
			&& double.IsNaN(_ProfileStats.KLKdePrior(pq, new double[] { 1, 3 }, pq, new double[] { 3, 1 }, 0.25, double.NaN, 1e-6)), "VP-4 ruling 2: one h for both densities; NaN without a prior h_cv");
		Check(!double.IsNaN(inter.hCv), "VP-6 interleaved folding option");
	}

	static void Vt()
	{
		var bars = new List<RingBar> {
			B(1, D0.AddMinutes(1), 100, 100, 100, 100, 90), B(2, D0.AddMinutes(2), 100, 102, 100, 101, 30),
			B(3, D0.AddMinutes(3), 101, 102, 101, 102, 20), B(4, D0.AddMinutes(4), 105, 105, 105, 105, 6) };
		var vt = _ProfileStats.VtPrices(bars, 1.0, 2, 4.0);
		Check(Near(vt.volPoc, 100) && Near(vt.tpoPoc, 100) && vt.prices.Count == 2 && Near(vt.prices[0], 102) && Near(vt.ratios[0], 10) && Near(vt.prices[1], 105) && Near(vt.ratios[1], 6),
			"VP-6 V/T hand example");
		Check(_ProfileStats.VtPrices(bars, 1.0, 2, double.NaN).prices.Count == 0, "VP-6 V/T needs ATR30");
		Check(_ProfileStats.VtLevels(vt, D0, D0, D0, 1, "VT")[0].kind == ELevelKind.VtPrice, "VP-6 V/T LevelRefs");
		// M-DVP PR-D0a (DV-10.3): DVP kinds appended after BootstrapPocZoneHi; existing ordinals unchanged.
		Check((int)ELevelKind.Poc == 0 && (int)ELevelKind.FvgLower == 12 && (int)ELevelKind.BootstrapPocZoneHi == 19, "D0a existing ELevelKind ordinals unchanged");
		Check((int)ELevelKind.DvpPoc == 20 && (int)ELevelKind.DvpVah == 21 && (int)ELevelKind.DvpVal == 22 && (int)ELevelKind.DvpHvn == 23
			&& (int)ELevelKind.DvpLvn == 24 && (int)ELevelKind.DvpSigma == 25 && (int)ELevelKind.DvpFib == 26 && (int)ELevelKind.DvpConf == 27
			&& Enum.GetValues(typeof(ELevelKind)).Length == 28, "D0a DVP kinds appended in order 20..27");
		Check(LevelRef.Line(ELevelKind.DvpVah, 100, D0, D0, D0, 1, "DVP1:x").StudyType == "DvpVah" && LevelRef.Line(ELevelKind.DvpConf, 100, D0, D0, D0, 1, null).StudyType == "DvpConf", "D0a DVP StudyType strings for A2-33");
	}

	// PR #47 (PR F scope): VpConfluence rules (extracted from _VpConfluenceEngine <vpconf-rules>) plus source checks on the engine/base.
	static void VpConf()
	{
		const double tol = 1.0, atr = 400;          // 4 ticks x 0.25; 3% x 400 = 12 > 4 x tol = 4
		double f = VpConfluenceRules.DefaultSplitAtrFraction;
		Check(f == 0.03 && VpConfluenceRules.MinSplitCount == 3 && VpConfluenceRules.SplitTolMultiple == 4.0, "split constants: 3% Atr20d, >= 3 anchors, 4 x tol");
		// anchors chained within tol (they join), widths 0.02 x ATR (8) and 0.04 x ATR (16)
		var narrow = new List<double> { 100, 101, 102, 103, 104, 105, 106, 107, 108 };
		var wide = new List<double> { 100, 101, 102, 103, 104, 105, 106, 107, 108.5, 109, 110, 111, 112, 113, 114, 115, 116 };
		Check(VpConfluenceRules.SplitAt(narrow, 0, narrow.Count, tol, atr, f) == -1, "width 0.02 x Atr20d: one zone");
		int sp = VpConfluenceRules.SplitAt(wide, 0, wide.Count, tol, atr, f);
		Check(sp == 8 && VpConfluenceRules.SplitAt(wide, 0, sp, tol, atr, f) == -1 && VpConfluenceRules.SplitAt(wide, sp, wide.Count, tol, atr, f) == -1,
			"width 0.04 x Atr20d: two zones, split at the largest gap (107 | 108.5) [" + sp + "]");
		Check(VpConfluenceRules.SplitAt(wide, 0, wide.Count, tol, atr, f) > 0 && 1.5 <= tol * 1.5, "no gap > tol veto: a largest gap of 1.5 (<= 2 x tol) still splits");
		Check(VpConfluenceRules.SplitAt(narrow, 0, narrow.Count, tol, double.NaN, f) > 0 && VpConfluenceRules.SplitWidth(tol, double.NaN, f) == 4.0,
			"Atr20d NaN: falls back to 4 x tol (width 8 > 4 splits)");
		Check(VpConfluenceRules.SplitWidth(tol, 50, f) == 4.0 && VpConfluenceRules.SplitWidth(tol, 0, f) == 4.0 && VpConfluenceRules.SplitWidth(tol, double.PositiveInfinity, f) == 4.0,
			"limit = max(4 x tol, 3% x Atr20d); bad ATR -> 4 x tol");
		Check(VpConfluenceRules.SplitAt(new List<double> { 100, 120 }, 0, 2, tol, double.NaN, f) == -1, "fewer than 3 anchors never split");
		Check(VpConfluenceRules.SplitAt(new List<double> { 100, 101, 102, 103, 104, 105, 109, 110 }, 0, 8, tol, double.NaN, f) == 6, "split index = first anchor above the largest gap");
		// V5: independent sources are distinct (stream, profile window) ids, not survivors
		Check(VpConfluenceRules.DistinctCount(new[] { "ds1:s10", "ds1:s10" }) == 1, "two survivors from the same profile/stream (acceptance + rejection) count 1 source");
		Check(VpConfluenceRules.DistinctCount(new[] { "ds1:s10", "ds2:s10", "ds1:s50", "", null }) == 3, "distinct sources across streams / windows; empty ids ignored");
		// duplicate enabled streams are rejected at StartCalc
		string e = VpConfluenceRules.DuplicateStreamError(1, 1, -1);
		Check(e == "ConfluenceDataStream2 = ConfluenceDataStream1 (1): each confluence slot needs its own stream", "duplicate stream error text: " + e);
		Check(VpConfluenceRules.DuplicateStreamError(2, 3, 2) != null && VpConfluenceRules.DuplicateStreamError(1, -1, -1) == null && VpConfluenceRules.DuplicateStreamError(1, 0, 0) == null
			&& VpConfluenceRules.DuplicateStreamError(1, 2, 3) == null, "only enabled (>= 1) duplicates are rejected");
		// ---- behaviour (review round 2): the rules the engine now delegates to ----
		// Partition: realistic ES Atr20d ~ 60 (3% = 1.8 < 4 x tol = 4, the floor binds)
		var z = new List<int[]>(); var sp2 = new List<int[]>();
		var es = new List<double> { 5000, 5001, 5002, 5003, 5004, 5005 };            // width 5 > 4: split
		VpConfluenceRules.Partition(es, 0, es.Count, tol, 60, f, z, sp2);
		Check(z.Count == 2 && sp2.Count == 1 && z[0][0] == 0 && z[z.Count - 1][1] == es.Count && z[0][1] == z[1][0], "ES Atr20d 60: width 5 > max(4, 1.8) splits into 2 zones");
		z.Clear(); VpConfluenceRules.Partition(new List<double> { 5000, 5001.5, 5003 }, 0, 3, tol, 60, f, z, null);
		Check(z.Count == 1, "ES Atr20d 60: width 3 <= 4 stays one zone");
		Check(Near(VpConfluenceRules.SplitWidth(0.25, 60, f), 1.8) , "ES Atr20d 60 with tol 0.25 (1 tick): limit = 3% x 60 = 1.8");
		z.Clear(); VpConfluenceRules.Partition(new List<double> { 5000, 5001, 5002 }, 0, 3, 0.25, 60, f, z, null);
		Check(z.Count == 2, "ES Atr20d 60, tol 0.25: 3 anchors over 2 pts > 1.8 split");
		z.Clear(); VpConfluenceRules.Partition(new List<double> { 5000, 5010 }, 0, 2, 0.25, 60, f, z, null);
		Check(z.Count == 1, ">= 3-anchor condition: 2 anchors 10 pts apart are never split");
		z.Clear(); VpConfluenceRules.Partition(wide, 0, wide.Count, tol, double.NaN, f, z, null);
		bool cover = z.Count > 2 && z[0][0] == 0 && z[z.Count - 1][1] == wide.Count; for (int i = 1; i < z.Count; i++) cover &= z[i][0] == z[i - 1][1];
		Check(cover, "Partition ranges tile the cluster in order (" + z.Count + " zones, NaN ATR)");
		// ZoneSourceCounts (V5) over the engine's survivor interface
		int ns, np;
		Func<string, string, bool, double, IVpConfSurvivor> S = (svid, p, ind, c) => new TSurv { Anchor = new TAnc { SourceVolumeId = svid, ProfileId = p }, Independent = ind, Credit = c };
		VpConfluenceRules.ZoneSourceCounts(new[] { S("ds1:s10", "ds1:s10", true, 1), S("ds1:s10", "ds1:s10", true, 1), S("", "ds1:s50", true, 1), S("ds2:s10", "ds2:s10", true, 1) }, VpConfluenceRules.DefaultDevSourceThreshold, out ns, out np);
		Check(ns == 3 && np == 3, "ZoneSourceCounts: two labels of one source count once; empty source id -> own profile [" + ns + "," + np + "]");
		VpConfluenceRules.ZoneSourceCounts(new[] { S("ds1:s10", "ds1:s10", true, 1), S("ds1:s10", "ds1:s30", true, 0.5) }, VpConfluenceRules.DefaultDevSourceThreshold, out ns, out np);
		Check(ns == 1 && np == 2, "ZoneSourceCounts: developing child remapped to its parent's source adds no source [" + ns + "," + np + "]");
		VpConfluenceRules.ZoneSourceCounts(new[] { S("a", "a", true, 1), S("b", "b", false, 1), S("c", "c", true, 0) }, VpConfluenceRules.DefaultDevSourceThreshold, out ns, out np);
		Check(ns == 1 && np == 1, "ZoneSourceCounts: non-independent and zero-credit survivors excluded");
		Check(VpConfluenceRules.SourceId("", "p") == "p" && VpConfluenceRules.SourceId(null, "p") == "p" && VpConfluenceRules.SourceId("s", "p") == "s", "SourceId: source else profile");
		// DevelopingHits over the cluster: every developing non-overlay anchor in [lo, hi) is listed with its own survivor's credit
		var an = new[] { new TAnc { ProfileId = "d0", Developing = true }, new TAnc { ProfileId = "c1" }, new TAnc { ProfileId = "o2", Developing = true, Overlay = true },
			new TAnc { ProfileId = "d3", Developing = true }, new TAnc { ProfileId = "d4", Developing = true }, new TAnc { ProfileId = "d5", Developing = true } };
		var sv = new IVpConfSurvivor[] { new TSurv { Anchor = an[0], Credit = 0.5, Independent = true }, new TSurv { Anchor = an[1], Credit = 1 }, new TSurv { Anchor = an[2], Credit = 0.3 }, new TSurv { Anchor = an[4], Credit = 0 } };
		List<string> dh = null;
		double ds = VpConfluenceRules.DevelopingHits(an, 0, 5, sv, (x, own, c) => x.ProfileId + ":" + (own != null ? "own" : "none") + ":" + c, ref dh);
		Check(dh != null && string.Join(",", dh) == "d0:own:0.5,d3:none:0,d4:none:0" && Near(ds, 0.5),
			"DevelopingHits: developing non-overlay anchors in range listed; absorbed/refused carry 0; score sums credit [" + (dh == null ? "null" : string.Join(",", dh)) + "]");
		List<string> dh2 = null;
		Check(VpConfluenceRules.DevelopingHits(an, 1, 3, sv, (x, own, c) => x.ProfileId, ref dh2) == 0 && dh2 == null, "DevelopingHits: none (and no list allocated) without developing anchors");
		// EmitZones: zones emitted in order, splits reported, and the host's Atr20d (provider) reaches the split
		var em = new List<string>(); var spl = new List<string>(); int calls = 0;
		var es2 = new List<double> { 5000, 5000.5, 5001, 5001.5, 5002, 5002.5, 5003 };     // 3 pts wide, tol 0.25: 4 x tol = 1
		int nz = VpConfluenceRules.EmitZones(es2, 0, es2.Count, 0.25, () => { calls++; return 60.0; }, f, (lo2, hi2) => em.Add(lo2 + "-" + hi2), (lo2, hi2, at, hatr) => spl.Add(at + "@" + hatr));
		bool tile = em.Count == nz && nz >= 2 && em[0].StartsWith("0-") && em[em.Count - 1].EndsWith("-" + es2.Count);
		Check(tile && spl.Count == nz - 1 && spl[0].EndsWith("@60") && calls == 1, "EmitZones: ES host Atr20d 60 (limit 1.8) splits 3 pts; zones in order, splits journaled with the host ATR [" + string.Join(" ", em) + "]");
		em.Clear(); spl.Clear();
		VpConfluenceRules.EmitZones(es2, 0, es2.Count, 0.25, () => 400.0, f, (lo2, hi2) => em.Add(lo2 + "-" + hi2), (lo2, hi2, at, hatr) => spl.Add(at + "@" + hatr));
		Check(em.Count == 1 && em[0] == "0-" + es2.Count && spl.Count == 0, "EmitZones: host Atr20d 400 (limit 12) keeps one zone: the host ATR, not 4 x tol, decides");
		em.Clear();
		VpConfluenceRules.EmitZones(es2, 0, es2.Count, 0.25, () => double.NaN, f, (lo2, hi2) => em.Add(lo2 + "-" + hi2), null);
		Check(em.Count >= 3, "EmitZones: NaN host ATR falls back to 4 x tol (1 pt)");
		// HostAtr20d: the host's daily Atr20d passes through only when M-DAILY is on
		Check(VpConfluenceRules.HostAtr20d(true, () => 60) == 60 && double.IsNaN(VpConfluenceRules.HostAtr20d(false, () => 60)) && double.IsNaN(VpConfluenceRules.HostAtr20d(true, null)), "HostAtr20d: daily Atr20d when NeedsDaily, else NaN");
		// ValidateStreams throws; NeedsAtr20d
		bool threw = false; try { VpConfluenceRules.ValidateStreams(2, 3, 2); } catch (ArgumentException ex) { threw = ex.Message.StartsWith("ConfluenceDataStream3 = ConfluenceDataStream1 (2)"); }
		Check(threw, "ValidateStreams throws ArgumentException on a duplicate enabled stream");
		threw = false; try { VpConfluenceRules.ValidateStreams(1, -1, -1); VpConfluenceRules.ValidateStreams(1, 0, 0); } catch (ArgumentException) { threw = true; }
		Check(!threw, "ValidateStreams accepts distinct / disabled slots");
		Check(VpConfluenceRules.NeedsAtr20d(true, 0.03, 1, -1, -1) && !VpConfluenceRules.NeedsAtr20d(true, 0, 1, 2, 3) && !VpConfluenceRules.NeedsAtr20d(true, 0.03, -1, 0, -1), "NeedsAtr20d: only with an ATR fraction and an enabled slot");
		Check(!VpConfluenceRules.NeedsAtr20d(false, 0.03, 1, 2, 3), "NeedsAtr20d: no M-DAILY for a host that does not use confluence zones (default config)");
		// source pins on comment-stripped source: each glue line appears exactly once (commented-out / duplicated lines fail)
		string dir = AppContext.BaseDirectory; while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir, "_VpConfluenceEngine.Function.CS"))) dir = System.IO.Path.GetDirectoryName(dir);
		if (dir == null) { Check(false, "repo root with _VpConfluenceEngine.Function.CS not found"); return; }
		Func<string, string> strip = p => System.Text.RegularExpressions.Regex.Replace(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, p)), @"//[^\n]*", "");
		string eng = strip("_VpConfluenceEngine.Function.CS"), bas = strip("_VolumeProfileBase.Strategy.CS");
		Func<string, string, int> count = (hay, needle) => { int n = 0, i = 0; while ((i = hay.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; } return n; };
		Action<string, string, string> pin1 = (hay, needle, what) => Check(count(hay, needle) == 1, what + " (exactly once, uncommented)");
		pin1(eng, "VpConfluenceRules.EmitZones(m_prices, lo, hi, tol, m_owner.confluenceAtr20d, atrFraction, (zlo, zhi) => addZone(cluster, zlo, zhi, tol), (a, b, split, atr) => journalSplit(a, b, split, tol, atrFraction, atr));", "engine emits zones and feeds the host Atr20d through EmitZones");
		pin1(eng, "VpConfluenceRules.ZoneSourceCounts(survivors, m_devSourceThreshold, out nSources, out nProfiles);", "engine counts sources through ZoneSourceCounts");
		pin1(eng, "zone.independentSources = nSources;", "engine sets independentSources only from ZoneSourceCounts");
		pin1(eng, "zone.developingScore = VpConfluenceRules.DevelopingHits(cluster, loIdx, hiIdx, survivors, developingHit, ref zone.developingHits);", "engine lists developing hits through DevelopingHits");
		Check(!eng.Contains("m_atr20d") && !eng.Contains("svIds") && !eng.Contains("m_zones") && !eng.Contains("gap <= tol") && eng.Contains("VPCONF split"),
			"no engine-side glue left (no m_atr20d / svIds / m_zones / own gap test); splits journaled");
		pin1(bas, "return VpConfluenceRules.HostAtr20d(NeedsDaily, () => DailyContext == null ? double.NaN : DailyContext.Atr20d);", "base feeds the daily Atr20d through HostAtr20d");
		pin1(bas, "VpConfluenceRules.ValidateStreams(ConfluenceDataStream1, ConfluenceDataStream2, ConfluenceDataStream3);", "base StartCalc calls ValidateStreams");
		pin1(bas, "if (VpConfluenceRules.NeedsAtr20d(UsesConfluenceZones, ConfluenceSplitAtrFraction, ConfluenceDataStream1, ConfluenceDataStream2, ConfluenceDataStream3)) NeedsDaily = true;", "base enables M-DAILY only for confluence users");
		pin1(bas, "protected bool UsesConfluenceZones { get; set; } = false;", "UsesConfluenceZones defaults off");
		Check(count(bas, "NeedsDaily = true") == 1, "no other path turns on M-DAILY in the VP base");
		Func<string, string, string> body = (hay, sig) => { int i = hay.IndexOf(sig, StringComparison.Ordinal); if (i < 0) return null; int o = hay.IndexOf('{', i), d = 0, j = o; for (; j < hay.Length; j++) { if (hay[j] == '{') d++; else if (hay[j] == '}' && --d == 0) break; } return System.Text.RegularExpressions.Regex.Replace(hay.Substring(o + 1, j - o - 1), @"\s+", " ").Trim(); };
		Check(body(bas, "internal double confluenceAtr20d()") == "return VpConfluenceRules.HostAtr20d(NeedsDaily, () => DailyContext == null ? double.NaN : DailyContext.Atr20d);", "confluenceAtr20d body is exactly the HostAtr20d feed");
		Check(count(eng, "zone.independentSources =") == 1, "independentSources assigned once in the engine (no override after ZoneSourceCounts)");
		Check(count(eng, "addZone(cluster") == 1, "zones are added only through the EmitZones callback");
		Check(strip("_DeltaDivRevFuturesLELX.Strategy.CS").Contains("UsesConfluenceZones = true;") && strip("_VolumeProfileTest1.Strategy.CS").Contains("UsesConfluenceZones = TraceConfluence;"), "confluence-zone users declare UsesConfluenceZones");
		Check(!eng.Contains("FALLBACK_TIMEFRAME_WEIGHT_STEP") && !bas.Contains("FALLBACK_TIMEFRAME_WEIGHT_STEP"), "fallback timeframe weight removed");
		int c0 = bas.IndexOf("protected override void Create()"), c1 = bas.IndexOf("protected override void Destroy()");
		string create = c0 >= 0 && c1 > c0 ? bas.Substring(c0, c1 - c0) : "";
		Check(create.Length > 0 && !create.Contains("new _VpConfluenceEngine") && !create.Contains("new _VpVwapEngine") && !create.Contains("new Dictionary<int, StreamPack>"),
			"Create allocates no stream packs, VWAP or confluence engines (lazy)");
	}
	// ---- vp-confluence-developing-weight-spec.md (rev 2, approved 2026-10-31): completeness-weighted developing profiles ----
	const double REFINEMENT = 0.25;   // _VolumeProfileBase.DEFAULT_CONFLUENCE_REFINEMENT (pinned below)

	// main @ 3f9e9be ZoneSourceCounts, verbatim (the Off-mode reference for test 13).
	static void LegacyZoneSourceCounts(IReadOnlyList<IVpConfSurvivor> survivors, out int independentSources, out int profileCount)
	{
		var src = new List<string>(); var prof = new List<string>();
		if (survivors != null)
			for (int i = 0; i < survivors.Count; i++)
			{
				IVpConfSurvivor s = survivors[i];
				if (!(s.Credit > 0) || !s.Independent) continue;
				src.Add(VpConfluenceRules.SourceId(s.Anchor.SourceVolumeId, s.Anchor.ProfileId));
				prof.Add(s.Anchor.ProfileId);
			}
		independentSources = VpConfluenceRules.DistinctCount(src);
		profileCount = VpConfluenceRules.DistinctCount(prof);
	}

	// Mirror of the engine's addZone scoring after applyNested: weight loop (skipped when Off), acc/rej/ctx sums, ZoneSourceCounts,
	// DevelopingHits. legacy = main's path (no weight loop, legacy counts). Serialised with "R" so equal strings = identical doubles.
	static string ZoneOut(List<TSurv> svIn, IVpConfAnchor[] cluster, EDevWeighting mode, double threshold, bool legacy)
	{
		var sv = new List<TSurv>();
		foreach (var x in svIn) sv.Add(new TSurv { Anchor = x.Anchor, Independent = x.Independent, Credit = x.Credit, Completeness = x.Completeness, Overlay = x.Overlay, Rejection = x.Rejection });
		var raw = new double[sv.Count];
		for (int i = 0; i < sv.Count; i++) raw[i] = sv[i].Credit;
		if (!legacy && mode != EDevWeighting.Off)
			foreach (var s in sv) if (s.Anchor.Developing && !s.Anchor.Overlay) s.Credit *= s.Completeness;
		double acc = 0, rej = 0, ctx = 0; var hits = new System.Text.StringBuilder();
		for (int i = 0; i < sv.Count; i++)
		{
			var s = sv[i]; if (s.Credit <= 0) continue;
			if (s.Overlay) ctx += s.Credit; else if (s.Rejection) rej += s.Credit; else acc += s.Credit;
			hits.Append(s.Anchor.ProfileId).Append('=').Append(s.Credit.ToString("R")).Append(';');
		}
		int ns, np; var ro = sv.ConvertAll(x => (IVpConfSurvivor)x);
		if (legacy) LegacyZoneSourceCounts(ro, out ns, out np); else VpConfluenceRules.ZoneSourceCounts(ro, mode == EDevWeighting.Off ? 0 : threshold, out ns, out np);
		List<string> dh = null;
		double dscore = VpConfluenceRules.DevelopingHits(cluster, 0, cluster.Length, ro, (a, own, c) => a.ProfileId + ":" + c.ToString("R"), ref dh);
		return "acc=" + acc.ToString("R") + " rej=" + rej.ToString("R") + " ctx=" + ctx.ToString("R") + " raw=" + (acc + rej).ToString("R") + " src=" + ns + " prof=" + np
			+ " dev=" + dscore.ToString("R") + " devHits=" + (dh == null ? "null" : string.Join(",", dh)) + " hits=" + hits;
	}

	static void VpDevWeight()
	{
		const double st = VpConfluenceRules.DefaultDevStartFraction, mn = VpConfluenceRules.DefaultDevMinWeight, nr = VpConfluenceRules.DefaultDevNoRefWeight;
		Func<double, double, EDevRamp, double, double, double, double> W = (el, ex, rp, s, m, n) => VpConfluenceRules.Weigh(el, ex, rp, s, m, n).w;
		// 14. defaults
		Check(VpConfluenceRules.DefaultDevWeighting == EDevWeighting.Time && VpConfluenceRules.DefaultDevRamp == EDevRamp.Linear && st == 0.10 && mn == 0.10 && nr == 0.50
			&& VpConfluenceRules.DefaultDevSourceThreshold == 0.67 && VpConfluenceRules.MaxDevSourceThreshold == 1.01, "DW14 defaults: Time, Linear, start 0.10, min 0.10, noRef 0.50, threshold 0.67");
		// 2. linear / sqrt
		Check(Near(W(40, 80, EDevRamp.Linear, st, mn, nr), 0.5), "DW2 linear 40/80 = 0.5");
		Check(Near(W(40, 80, EDevRamp.Sqrt, st, mn, nr), 0.7071067811865476), "DW2 sqrt 40/80 = 0.7071");
		// 3. below start: weight 0 (listed in developingHits with credit 0: see DW12)
		var b3 = VpConfluenceRules.Weigh(7, 80, EDevRamp.Linear, st, mn, nr);
		Check(b3.w == 0 && b3.belowStart && !b3.noRef, "DW3 raw 0.0875 < start 0.10: w 0");
		// 4. floor
		Check(Near(W(12, 100, EDevRamp.Linear, st, 0.25, nr), 0.25), "DW4 floor: raw 0.12, min 0.25 -> 0.25");
		// 5. clamp
		var b5 = VpConfluenceRules.Weigh(120, 80, EDevRamp.Linear, st, mn, nr);
		Check(b5.w == 1 && b5.raw == 1 && W(120, 80, EDevRamp.Sqrt, st, mn, nr) == 1, "DW5 raw > 1 clamps to 1");
		// 6. no reference
		var b6 = VpConfluenceRules.Weigh(40, double.NaN, EDevRamp.Linear, st, mn, nr);
		Check(b6.noRef && b6.w == 0.5 && W(40, 0, EDevRamp.Linear, st, mn, 0.3) == 0.3, "DW6 no reference -> noRef weight");
		Check(VpConfluenceRules.Weigh(EDevWeighting.Time, 40, VpConfluenceRules.Median(new List<double>()), 0, 0, EDevRamp.Linear, st, mn, nr).w == 0.5, "DW6 empty reference set -> median NaN -> 0.50");
		// 7. median ignores one half day; partial excluded
		Check(VpConfluenceRules.Median(new List<double> { 81, 40, 82 }) == 81, "DW7 median of {81, 40 (half day), 82} = 81");
		Check(VpConfluenceRules.Median(new List<double> { 80, 82 }) == 81 && VpConfluenceRules.Median(new List<double> { 5 }) == 5, "DW7 even count: mean of the middle two");
		var unsorted = new List<double> { 3, 1, 2 }; VpConfluenceRules.Median(unsorted);
		Check(unsorted[0] == 3 && unsorted[1] == 1, "DW7 Median does not reorder its input");
		Check(VpConfluenceRules.IsReference(true, false, 10) && !VpConfluenceRules.IsReference(true, true, 10) && !VpConfluenceRules.IsReference(false, false, 10)
			&& !VpConfluenceRules.IsReference(true, false, 0), "DW7 reference = completed, not partial, startBar >= 1");
		// 8. volume / either
		Check(Near(VpConfluenceRules.Weigh(EDevWeighting.Volume, 70, 80, 300, 1000, EDevRamp.Linear, st, mn, nr).w, 0.3), "DW8 volume 300/1000 = 0.3");
		Check(Near(VpConfluenceRules.Weigh(EDevWeighting.Either, 70, 80, 300, 1000, EDevRamp.Linear, st, mn, nr).w, 0.875)
			&& Near(VpConfluenceRules.Weigh(EDevWeighting.Either, 20, 80, 600, 1000, EDevRamp.Linear, st, mn, nr).w, 0.6), "DW8 Either = max(time, volume)");
		Check(VpConfluenceRules.Weigh(EDevWeighting.Off, 1, 80, 1, 1000, EDevRamp.Linear, st, mn, nr).w == 1, "DW Off: w 1");
		Check(VpConfluenceRules.Weigh(EDevWeighting.Volume, 70, 80, double.NaN, 1000, EDevRamp.Linear, st, mn, nr).noRef, "DW8 volume basis without a snapshot total -> noRef");
		// 9. weekly references weekly only: per-stream medians
		double sessMed = VpConfluenceRules.Median(new List<double> { 81, 80, 79 }), weekMed = VpConfluenceRules.Median(new List<double> { 405, 400, 410 });
		Check(Near(W(40, sessMed, EDevRamp.Linear, st, mn, nr), 0.5) && Near(W(101, weekMed, EDevRamp.Linear, st, mn, nr), 101.0 / 405), "DW9 session vs weekly stream: each against its own median");
		// mutation resistance: asymmetric values (30/80, sqrt, min 0.2)
		double ma = W(30, 80, EDevRamp.Sqrt, st, 0.2, nr);
		Check(Near(ma, Math.Sqrt(0.375)), "DWm sqrt(30/80) = 0.6124 [" + ma + "]");
		Check(!Near(ma, 0.375) && !Near(ma, 1) && !Near(ma, 0.2) && !Near(ma, Math.Sqrt(80.0 / 30)), "DWm not linear / clamp / min / swapped");
		Check(Near(W(30, 80, EDevRamp.Linear, st, 0.2, nr), 0.375) && Near(W(15, 80, EDevRamp.Linear, st, 0.2, nr), 0.2), "DWm linear 0.375; 0.1875 floors to 0.2");
		Check(W(10, 100, EDevRamp.Linear, st, 0.05, nr) > 0 && W(10, 100, EDevRamp.Linear, st, 0.05, nr) == 0.1, "DWm boundary raw == start credits (> 0)");
		Check(W(9.999, 100, EDevRamp.Linear, st, 0.05, nr) == 0, "DWm just below start: 0");
		// setters: range checks throw
		Func<Action, bool> throws = a => { try { a(); return false; } catch (ArgumentException) { return true; } };
		Check(throws(() => VpConfluenceRules.RequireRange("x", -0.01, 0, 1)) && throws(() => VpConfluenceRules.RequireRange("x", 1.0001, 0, 1)) && throws(() => VpConfluenceRules.RequireRange("x", double.NaN, 0, 1))
			&& throws(() => VpConfluenceRules.RequireRange("x", 1.02, 0, 1.01)) && !throws(() => VpConfluenceRules.RequireRange("x", 1.01, 0, 1.01)) && !throws(() => VpConfluenceRules.RequireRange("x", 0, 0, 1)),
			"DW setter range check: NaN / out of range throw, bounds accepted");
		// 11. independentSources threshold
		int ns, np;
		Func<double, double, int> srcAt = (w, th) => { VpConfluenceRules.ZoneSourceCounts(new IVpConfSurvivor[] { new TSurv { Anchor = new TAnc { SourceVolumeId = "c", ProfileId = "c" }, Independent = true, Credit = 1 },
			new TSurv { Anchor = new TAnc { SourceVolumeId = "d", ProfileId = "d", Developing = true }, Independent = true, Credit = w, Completeness = w } }, th, out ns, out np); return ns; };
		Check(srcAt(0.66, 0.67) == 1 && srcAt(0.67, 0.67) == 2 && srcAt(0.80, 0.67) == 2, "DW11 threshold 0.67: w 0.66 no, 0.67 counts (boundary), 0.80 counts once");
		Check(srcAt(1.0, 1.01) == 1 && srcAt(0.6, 0.5) == 2 && srcAt(0.4, 0.5) == 1, "DW11 threshold 1.01 never; 0.5: w 0.6 yes, 0.4 no");
		VpConfluenceRules.ZoneSourceCounts(new IVpConfSurvivor[] { new TSurv { Anchor = new TAnc { SourceVolumeId = "d", ProfileId = "d", Developing = true }, Independent = true, Credit = 0.3, Completeness = 0.3 } }, 0.67, out ns, out np);
		Check(ns == 0 && np == 0, "DW11 profileCount follows the same rule");
		VpConfluenceRules.ZoneSourceCounts(new IVpConfSurvivor[] { new TSurv { Anchor = new TAnc { SourceVolumeId = "c", ProfileId = "c" }, Independent = true, Credit = 1, Completeness = 1 } }, 1.01, out ns, out np);
		Check(ns == 1, "DW11 completed survivors ignore the threshold");
		// fixture: completed c1 (POC 1.2), completed c2 rejection (0.8), developing own-source d3 (w 0.4), developing remapped d4 -> c1 (w 0.4), overlay v (0.5)
		var c1 = new TAnc { SourceVolumeId = "c1", ProfileId = "c1" }; var c2 = new TAnc { SourceVolumeId = "c2", ProfileId = "c2" };
		var d3 = new TAnc { SourceVolumeId = "d3", ProfileId = "d3", Developing = true }; var d4 = new TAnc { SourceVolumeId = "c1", ProfileId = "d4", Developing = true };
		var ov = new TAnc { SourceVolumeId = "overlay:Vwap1", ProfileId = "overlay:Vwap1", Overlay = true };
		var cluster = new IVpConfAnchor[] { c1, d3, c2, d4, ov };
		Func<double, List<TSurv>> fx = w => new List<TSurv> {
			new TSurv { Anchor = c1, Independent = true, Credit = 1.2 }, new TSurv { Anchor = c2, Independent = true, Credit = 0.8, Rejection = true },
			new TSurv { Anchor = d3, Independent = true, Credit = 0.9, Completeness = w }, new TSurv { Anchor = d4, Independent = true, Credit = 0.7, Completeness = w },
			new TSurv { Anchor = ov, Independent = false, Credit = 0.5, Overlay = true } };
		// 13. Off: identical to main on the fixture (all fields, exact doubles), at any threshold incl. 1.01
		string main = ZoneOut(fx(1), cluster, EDevWeighting.Off, 0.67, true);
		Check(ZoneOut(fx(1), cluster, EDevWeighting.Off, 0.67, false) == main && ZoneOut(fx(1), cluster, EDevWeighting.Off, 1.01, false) == main, "DW13 Off: zone output identical to main [" + main + "]");
		// 1. completed survivors unchanged by weighting; overlays unchanged (context score)
		string on = ZoneOut(fx(0.4), cluster, EDevWeighting.Time, 0.67, false);
		Check(on.Contains("ctx=0.5 ") && on.Contains("c1=1.2;") && on.Contains("c2=0.8;") && on.Contains("rej=0.8 ") && main.Contains("ctx=0.5 "), "DW1 completed credits and overlay context unchanged [" + on + "]");
		Check(on.Contains("d3=" + (0.9 * 0.4).ToString("R") + ";") && on.Contains("d4=" + (0.7 * 0.4).ToString("R") + ";"), "DW credit' = credit x w");
		Check(on.Contains(" src=2 ") && main.Contains(" src=3 "), "DW11 fixture: w 0.4 own-source developing dropped from independentSources (3 -> 2)");
		Check(ZoneOut(fx(1), cluster, EDevWeighting.Time, 0.67, false) == main, "DW1 Time with every w = 1 equals Off / main");
		// 12. developingScore = sum of weighted credits; null with no developing anchor
		Check(on.Contains(" dev=" + (0.9 * 0.4 + 0.7 * 0.4).ToString("R") + " ") && on.Contains("devHits=d3:" + (0.9 * 0.4).ToString("R") + ",d4:" + (0.7 * 0.4).ToString("R") + " "), "DW12 developingScore = weighted sum [" + on + "]");
		string zeroW = ZoneOut(fx(0), cluster, EDevWeighting.Time, 0.67, false);
		Check(zeroW.Contains("devHits=d3:0,d4:0 ") && zeroW.Contains(" dev=0 ") && !zeroW.Contains("d3=") , "DW3 w 0: hit listed in developingHits with credit 0, not scored");
		Check(ZoneOut(new List<TSurv> { new TSurv { Anchor = c1, Independent = true, Credit = 1.2 } }, new IVpConfAnchor[] { c1 }, EDevWeighting.Time, 0.67, false).Contains("devHits=null"), "DW12 no developing anchor: developingHits null");
		// 10. nested developing child: credit = 0.25 x parent x w (weight applied after applyNested)
		double parent = 1.6, wn = 0.457;
		var nest = new List<TSurv> { new TSurv { Anchor = c1, Independent = true, Credit = parent }, new TSurv { Anchor = d3, Independent = false, Credit = REFINEMENT * parent, Completeness = wn } };
		string nz = ZoneOut(nest, new IVpConfAnchor[] { c1, d3 }, EDevWeighting.Time, 0.67, false);
		Check(nz.Contains("d3=" + (REFINEMENT * parent * wn).ToString("R") + ";") && nz.Contains(" src=1 "), "DW10 nested developing child = 0.25 x parent x w [" + nz + "]");
		// journal line (spec §8)
		var j = VpConfluenceRules.Weigh(EDevWeighting.Time, 37, 81, 0, 0, EDevRamp.Linear, st, mn, nr);
		string jl = VpConfluenceRules.DevWeightLine(2, "ds2:s1234", EDevWeighting.Time, j, EDevRamp.Linear, st, 3, 0.67);
		Check(jl == "VPCONF devweight ds(2) profile(ds2:s1234) basis(time) elapsed(37) expected(81) raw(0.457) ramp(linear) w(0.457) refN(3) source(no)", "DW journal line: " + jl);
		string jn = VpConfluenceRules.DevWeightLine(2, "ds2:s9", EDevWeighting.Time, VpConfluenceRules.Weigh(5, double.NaN, EDevRamp.Linear, st, mn, nr), EDevRamp.Linear, st, 0, 0.67);
		string js = VpConfluenceRules.DevWeightLine(2, "ds2:s9", EDevWeighting.Time, VpConfluenceRules.Weigh(5, 81, EDevRamp.Linear, st, mn, nr), EDevRamp.Linear, st, 3, 0.67);
		string jy = VpConfluenceRules.DevWeightLine(1, "ds1:s9", EDevWeighting.Volume, VpConfluenceRules.Weigh(700, 1000, EDevRamp.Linear, st, mn, nr), EDevRamp.Linear, st, 3, 0.67);
		Check(jn.Contains("basis(noref)") && jn.Contains("w(0.500)") && js.Contains("w(0) start(0.10)") && jy.Contains("basis(volume)") && jy.Contains("source(yes)"), "DW journal: noref / below start / source yes [" + jn + " | " + js + " | " + jy + "]");
		// ---- source pins (comment-stripped) ----
		string dir = AppContext.BaseDirectory; while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir, "_VpConfluenceEngine.Function.CS"))) dir = System.IO.Path.GetDirectoryName(dir);
		if (dir == null) { Check(false, "DW repo root not found"); return; }
		Func<string, string> strip = p => System.Text.RegularExpressions.Regex.Replace(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, p)), @"//[^\n]*", "");
		string eng = strip("_VpConfluenceEngine.Function.CS"), bas = strip("_VolumeProfileBase.Strategy.CS");
		Func<string, string, int> count = (hay, needle) => { int n = 0, i = 0; while ((i = hay.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; } return n; };
		Check(count(eng, "s.credit *= s.anchor.completeness;") == 1 && eng.IndexOf("applyNested(survivors);", StringComparison.Ordinal) >= 0
			&& eng.IndexOf("s.credit *= s.anchor.completeness;", StringComparison.Ordinal) > eng.IndexOf("applyNested(survivors);", StringComparison.Ordinal)
			&& count(eng, "applyNested(survivors);") == 1, "DW pin: weight applied exactly once, after applyNested");
		Check(count(eng, "if (m_devMode != EDevWeighting.Off)") == 1 && count(eng, "if (!s.anchor.developing || s.anchor.overlay) continue;") == 1, "DW pin: weight loop skipped when Off; developing non-overlay only");
		Check(count(eng, "VpConfluenceRules.ZoneSourceCounts(survivors, m_devSourceThreshold, out nSources, out nProfiles);") == 1
			&& count(eng, "m_devSourceThreshold = m_devMode == EDevWeighting.Off ? 0 : m_owner.ConfluenceDevSourceThreshold;") == 1, "DW pin: ZoneSourceCounts gets the config threshold (0 when Off)");
		Check(count(eng, "bool weigh = m_devMode != EDevWeighting.Off && hasDeveloping(extras);") == 1 && count(eng, "if (weigh) referenceMedians(") == 1
			&& count(eng, "if (m_refBars == null) { m_refBars = new List<double>(8); m_refVols = new List<double>(8); }") == 1, "DW pin: medians / scratch only when weighting is on and a developing profile exists");
		Check(count(eng, "if (anchors[i].developing && !anchors[i].overlay) anchors[i].completeness = d.w;") == 1 && count(eng, "public double completeness = 1;") == 1, "DW pin: completeness default 1, set only on developing non-overlay anchors");
		Check(count(eng, "VpConfluenceRules.IsReference(e.pocComplete, e.isPartial, e.startBar)") == 1, "DW pin: reference set filter");
		Check(count(bas, "public const double DEFAULT_CONFLUENCE_REFINEMENT = 0.25;") == 1, "DW pin: REFINEMENT 0.25");
		foreach (var prop in new[] { "protected internal EDevWeighting ConfluenceDevWeighting { get; set; } = VpConfluenceRules.DefaultDevWeighting;",
			"protected internal EDevRamp ConfluenceDevRamp { get; set; } = VpConfluenceRules.DefaultDevRamp;",
			"set { m_confluenceDevStartFraction = VpConfluenceRules.RequireRange(\"ConfluenceDevStartFraction\", value, 0, 1); }",
			"set { m_confluenceDevMinWeight = VpConfluenceRules.RequireRange(\"ConfluenceDevMinWeight\", value, 0, 1); }",
			"set { m_confluenceDevNoRefWeight = VpConfluenceRules.RequireRange(\"ConfluenceDevNoRefWeight\", value, 0, 1); }",
			"set { m_confluenceDevSourceThreshold = VpConfluenceRules.RequireRange(\"ConfluenceDevSourceThreshold\", value, 0, VpConfluenceRules.MaxDevSourceThreshold); }" })
			Check(count(bas, prop) == 1, "DW pin config: " + prop);
		Check(!System.Text.RegularExpressions.Regex.IsMatch(bas, @"\[Input\][^;{]*ConfluenceDev"), "DW: no [Input] for the dev-weight config");
		Check(count(strip("_VolumeProfileTest1.Strategy.CS"), "kinds.Append(\"/w=\");") == 1 && count(strip("_VolumeProfileTest1.Strategy.CS"), "if (z.hits[h].developing)") == 1, "DW pin: Test1 zone trace shows w= on developing hits");
		Check(count(eng, "rawCredit = double.IsNaN(s.rawCredit) ? s.credit : s.rawCredit") == 1 && count(eng, "completeness = s.anchor.completeness,") == 1, "DW pin: hits carry completeness and rawCredit");
	}
	// PR I: opt-in Week/Month VWAP reset at a roll (VwapConfig.resetPeriodSlotsOnRoll, default false = unchanged)
	static void VwapRollReset()
	{
		Check(!new VwapConfig().resetPeriodSlotsOnRoll, "PR I: VwapConfig.resetPeriodSlotsOnRoll defaults to false");
		// wiring pins (comment-stripped source): config passed through, default off and not hard-wired, reset only on closed bars
		string dir = AppContext.BaseDirectory; while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir, "_VpVwapEngine.Function.CS"))) dir = System.IO.Path.GetDirectoryName(dir);
		if (dir == null) { Check(false, "PR I: repo root with _VpVwapEngine.Function.CS not found"); return; }
		Func<string, string> strip = q => System.Text.RegularExpressions.Regex.Replace(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, q)), @"//[^\n]*", "");
		string veng = strip("_VpVwapEngine.Function.CS"), vbas = strip("_VolumeProfileBase.Strategy.CS");
		Func<string, string, int> count = (hay, needle) => { int n = 0, i = 0; while ((i = hay.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; } return n; };
		Check(count(vbas, "eng.resetPeriodOnRoll = VwapConfig.resetPeriodSlotsOnRoll;") == 1 && count(vbas, "resetPeriodOnRoll =") == 1, "PR I: configureVwap passes the config flag through (exactly once, no other assignment)");
		Check(count(veng, "public bool resetPeriodSlotsOnRoll = false;") == 1 && count(veng, "public bool resetPeriodOnRoll;") == 1 && count(veng, "resetPeriodOnRoll =") == 0, "PR I: default off, not hard-wired in the engine");
		Check(count(vbas, "protected VwapConfig VwapConfig { get; } = new VwapConfig();") == 1 && count(vbas, "resetPeriodSlotsOnRoll = true") == 0, "PR I: base uses the default VwapConfig (no hard-wired true)");
		Check(count(veng, "if (rollSlot && bars.Status == EBarState.Close &&") == 1, "PR I: roll reset only on closed bars");
		Check(count(veng, "bool rollSlot = type == EVwapType.RollDate || (resetPeriodOnRoll && (type == EVwapType.Week || type == EVwapType.Month));") == 1, "PR I: roll slots = RollDate, or Week/Month with the flag");
		Check(count(vbas, "if (VwapConfig.resetPeriodSlotsOnRoll && RollDetection == ERollDetection.Off)") == 1, "PR I: warns when the flag is on but RollDetection is Off");
		Check(count(veng, "if (parent.TraceLevel >= ETraceLevel.Journal) parent.trace(ETraceLevel.Journal, $\"VWAP roll reset {label} type({type}) Data{datastream} ") == 1
			&& count(veng, "else if (parent.TraceLevel >= ETraceLevel.Journal)") == 0 && count(veng, "VWAP at roll") == 0 && count(veng, "Resetting RollDate VWAP") == 0,
			"PR I: each roll reset logged exactly once, at Journal (also at Debug), with label and stream");
		Check(count(vbas, "trace(ETraceLevel.Warning, \"VWAP resetPeriodSlotsOnRoll is on but RollDetection is Off") == 1, "PR I: the Off warning is at ETraceLevel.Warning");
		// default off: only RollDate resets at a roll (unchanged behaviour)
		Check(VwapRollRules.ResetsAtRoll(true, false, false, true), "PR I: RollDate resets at a roll (flag off)");
		Check(!VwapRollRules.ResetsAtRoll(false, true, false, true), "PR I: Week/Month do not reset at a roll when off");
		// opt-in
		Check(VwapRollRules.ResetsAtRoll(false, true, true, true), "PR I: Week/Month reset at a roll when on");
		Check(!VwapRollRules.ResetsAtRoll(false, false, true, true), "PR I: other slots (Session/Length/Quarter/FromDate) never reset at a roll");
		// no roll -> never
		Check(!VwapRollRules.ResetsAtRoll(true, false, true, false) && !VwapRollRules.ResetsAtRoll(false, true, true, false), "PR I: no rollOn, no reset");
		// accumulator walk: Week VWAP over 3 bars, roll on bar 2 (closing roll bar stays, prior sums dropped)
		double[] px = { 100, 110, 120 }, vol = { 1, 1, 1 }; bool[] roll = { false, true, false };
		foreach (bool on in new[] { false, true })
		{
			double pv = 0, v = 0;
			for (int i = 0; i < 3; ++i)
			{
				if (VwapRollRules.ResetsAtRoll(false, true, on, roll[i])) { pv = 0; v = 0; }
				pv += px[i] * vol[i]; v += vol[i];
			}
			double vw = pv / v;
			Check(on ? Math.Abs(vw - 115) < 1e-9 : Math.Abs(vw - 110) < 1e-9, "PR I: Week VWAP walk " + (on ? "on -> 115 (roll bar + after)" : "off -> 110 (spans the roll)"));
		}
	}
}
class TAnc : IVpConfAnchor { public string SourceVolumeId { get; set; } public string ProfileId { get; set; } public bool Developing { get; set; } public bool Overlay { get; set; } }
class TSurv : IVpConfSurvivor { public IVpConfAnchor Anchor { get; set; } public bool Independent { get; set; } public double Credit { get; set; } public double Completeness { get; set; } = 1; public bool Overlay; public bool Rejection; }
