using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage;
using PowerLanguage.Function;

// Off-MC tests for _PriceActionEngine (spec missing-functionality-specs.md §1.7). Synthetic fixtures (no dxFeed data on the box).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-9) { return Math.Abs(a - b) < eps; }
	static readonly DateTime T0 = new DateTime(2024, 3, 13, 9, 30, 0);

	static PriceActionConfig Cfg(Action<PriceActionConfig> f = null) { var c = new PriceActionConfig { enabled = true }; if (f != null) f(c); return c; }
	static _PriceActionEngine Eng(PriceActionConfig c, List<string> journal = null, List<string> warns = null)
	{
		var e = new _PriceActionEngine { config = c };
		if (journal != null) e.journal = s => journal.Add(s);
		if (warns != null) e.warnOnce = s => warns.Add(s);
		string err = e.Configure(); if (err != null) throw new Exception(err);
		return e;
	}
	static void Feed(_PriceActionEngine e, int n, double o, double h, double l, double c, double v = 100) { e.Add(n, T0.AddMinutes(n), o, h, l, c, v); }
	// bar around a mid price with a fixed half-range
	static void Mid(_PriceActionEngine e, int n, double mid, double half = 1, double v = 100) { Feed(e, n, mid, mid + half, mid - half, mid, v); }

	static List<double[]> RandomBars(int count, int seed, double start = 4000)
	{
		var r = new Random(seed); var list = new List<double[]>(); double p = start;
		for (int i = 0; i < count; i++)
		{
			double o = p, c = o + (r.NextDouble() - 0.5) * 8; double h = Math.Max(o, c) + r.NextDouble() * 3, l = Math.Min(o, c) - r.NextDouble() * 3;
			o = Math.Round(o * 4) / 4; c = Math.Round(c * 4) / 4; h = Math.Max(Math.Round(h * 4) / 4, Math.Max(o, c)); l = Math.Min(Math.Round(l * 4) / 4, Math.Min(o, c));
			list.Add(new[] { o, h, l, c, 50 + r.Next(500) }); p = c;
		}
		return list;
	}

	static void Main()
	{
		Validation(); Presets(); Warmup(); IndicatorsMatchNaive(); FlatTops(); LabelsAndEqual(); Breakouts(); Bos(); OutOfOrder();
		ResetEqualsFresh(); Roll(); MonotonicAndImmutable(); CandleRules(); ChangedXor(); StepClosedOnly(); ZigZag();
		ExtraCanon(); ExtraIndependent(); ExtraSharing(); ExtraRollOnData2();
		Console.WriteLine($"PriceActionTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}

	// [M4] every bad value reported by name; disabled config never validated
	static void Validation()
	{
		Check(new PriceActionConfig().Validate() == null, "M4 disabled config validates");
		var c = Cfg(k => { k.layers = 0; k.strength = 0; k.history = 1; k.factorClose = 2; k.dataStream = 0; });
		string err = c.Validate();
		Check(err != null && err.Contains("layers=0") && err.Contains("strength=0") && err.Contains("history=1") && err.Contains("factorClose=2") && err.Contains("dataStream=0"), "M4 all bad values named: " + err);
		Check(Cfg(k => { k.layers = 2; k.minBreakoutLayer = 2; }).Validate().Contains("minBreakoutLayer"), "M4 minBreakoutLayer < layers");
		var e = new _PriceActionEngine { config = Cfg(k => k.layers = 0) };
		Check(e.Configure() != null && !e.Configured, "M4 Configure returns error, engine stays unconfigured");
		Check(e.LastHigh() == null && e.Structure() == EStructure.Unknown && double.IsNaN(e.Atr), "neutral values before Configure");
	}

	// rev 4 presets
	static void Presets()
	{
		var d = new PriceActionConfig();
		Check(d.tieRule == EPivotTieRule.Strict && d.volumeIncludesCurrent && d.preset == EPriceActionPreset.Library && !d.enabled, "default = Library parity, disabled");
		var f = PriceActionConfig.Futures();
		Check(f.tieRule == EPivotTieRule.LeftInclusive && !f.volumeIncludesCurrent && f.preset == EPriceActionPreset.Futures && !f.enabled, "Futures() preset, still disabled");
		Check(f.layers == d.layers && f.strength == d.strength && f.atrLength == d.atrLength, "preset touches only its own fields");
		var o = new PriceActionConfig(); o.ApplyPreset(EPriceActionPreset.Futures); o.tieRule = EPivotTieRule.Strict;
		Check(o.tieRule == EPivotTieRule.Strict && !o.volumeIncludesCurrent, "per-field override after the preset sticks");
		o.ApplyPreset(EPriceActionPreset.Library);
		Check(o.tieRule == EPivotTieRule.Strict && o.volumeIncludesCurrent, "ApplyPreset(Library) restores parity");
	}

	// [L5] ATR NaN until warm
	static void Warmup()
	{
		var e = Eng(Cfg(k => { k.atrLength = 5; k.volumeLength = 5; }));
		bool allNaN = true;
		for (int i = 0; i < 4; i++) { Mid(e, i, 100 + i); allNaN &= double.IsNaN(e.Atr) && !e.IsWarm && e.Candle == ECandle.None; }
		Check(allNaN, "L5 first atrLength-1 bars: Atr NaN, not warm, no candle flags");
		Mid(e, 4, 104);
		Check(e.IsWarm && !double.IsNaN(e.Atr), "L5 warm after atrLength bars");
	}

	// [M1] running sums == naive recompute; volume norm with and without the current bar
	static void IndicatorsMatchNaive()
	{
		foreach (bool inc in new[] { true, false })
		{
			var e = Eng(Cfg(k => { k.atrLength = 14; k.volumeLength = 10; k.volumeIncludesCurrent = inc; }));
			var bars = RandomBars(400, 7); bool ok = true; double worst = 0;
			for (int i = 0; i < bars.Count; i++)
			{
				var b = bars[i]; e.Add(i, T0.AddMinutes(i), b[0], b[1], b[2], b[3], b[4]);
				if (i < 20) continue;
				double trSum = 0;
				for (int j = i - 13; j <= i; j++) { double pc = bars[j - 1][3]; trSum += Math.Max(bars[j][1], pc) - Math.Min(bars[j][2], pc); }
				int hi = inc ? i : i - 1; var vs = Enumerable.Range(hi - 9, 10).Select(j => bars[j][4]).ToArray();
				double mean = vs.Average(), sd = Math.Sqrt(vs.Select(x => (x - mean) * (x - mean)).Average());
				worst = Math.Max(worst, Math.Abs(e.Atr - trSum / 14));
				ok &= Near(e.Atr, trSum / 14, 1e-9) && Near(e.VolumeMean, mean, 1e-7) && Near(e.VolumeSigma, sd, 1e-6);
			}
			Check(ok, $"M1 ATR/volume equal a naive recompute (volumeIncludesCurrent={inc}, worst ATR diff {worst:E1})");
		}
	}

	static _PriceActionEngine WarmFlat(PriceActionConfig c, int n, List<string> j = null)
	{
		var e = Eng(c, j);
		for (int i = 0; i < n; i++) Mid(e, i, 100 + (i % 2) * 0.5);   // gentle zig to warm up
		return e;
	}

	// [M6] flat tops: Strict -> no pivot, LeftInclusive -> pivot on the right bar of the flat
	static void FlatTops()
	{
		foreach (var rule in new[] { EPivotTieRule.Strict, EPivotTieRule.LeftInclusive })
		{
			var e = Eng(Cfg(k => { k.atrLength = 2; k.volumeLength = 2; k.tieRule = rule; }));
			Feed(e, 0, 9, 9.5, 8.5, 9); Feed(e, 1, 9, 10, 8.75, 9.5);
			int before = e.PivotCount(0, true);
			Feed(e, 2, 9.5, 12, 9, 11); Feed(e, 3, 11, 12, 10.5, 11.5); Feed(e, 4, 11.5, 11.75, 10, 10.25); Feed(e, 5, 10.25, 10.5, 9.5, 9.75);
			int added = e.PivotCount(0, true) - before;
			if (rule == EPivotTieRule.Strict) Check(added == 0, "M6 Strict: equal adjacent highs never pivot");
			else Check(added == 1 && e.LastHigh().price == 12 && e.LastHigh().extremeBar == 3 && e.LastHigh().confirmBar == 4, "M6 LeftInclusive: flat top pivots on its right bar (mid >= left && mid > right)");
		}
	}

	// [M5][H5] always labelled; equal high -> LH + isEqual on the NEW pivot, earlier pivot untouched
	static void LabelsAndEqual()
	{
		var j = new List<string>();
		var e = WarmFlat(Cfg(k => { k.atrLength = 4; k.volumeLength = 4; }), 6, j);
		int n = 6;
		double[] highs = { 101, 105, 101, 101.5, 101, 105, 101, 99, 103, 99 };
		foreach (var h in highs) Feed(e, n++, h - 1, h, h - 2, h - 1);
		var ph = Enumerable.Range(0, e.PivotCount(0, true)).Select(a => e.High(0, a)).Reverse().ToList();
		var p105 = ph.Where(p => p.price == 105).ToList();
		Check(p105.Count == 2, "two 105 pivot highs found (" + p105.Count + ")");
		if (p105.Count == 2)
		{
			Check(p105[1].label == EPivotLabel.HH && p105[1].isEqual && p105[1].equalToSeq == p105[0].seq, "H5 equal high (vs 101.5 in between): label kept HH, isEqual, equalToSeq = first.seq");
			Check(!p105[0].isEqual && p105[0].equalToSeq == -1, "H5 earlier pivot not modified");
		}
		Check(ph.Skip(1).All(p => p.label != EPivotLabel.None), "M5 every pivot after the first is labelled");
		// adjacent equal highs (tie) -> LH + isEqual
		var e2 = WarmFlat(Cfg(k => { k.atrLength = 4; k.volumeLength = 4; }), 6); n = 6;
		foreach (var h in new double[] { 101, 105, 101, 105, 101 }) Feed(e2, n++, h - 1, h, h - 2, h - 1);
		Check(e2.LastHigh().price == 105 && e2.LastHigh().label == EPivotLabel.LH && e2.LastHigh().isEqual && e2.High(0, 1).label != EPivotLabel.None, "M5 tie -> LH and isEqual; layer-0 labels present");
		Check(e.DoubleTop() == ph.Last().isEqual, "DoubleTop() reflects newest pivot");
		Check(j.Any(s => s.Contains("EQ#")), "journal carries the EQ match");
	}

	// [H6] breakout: strong bar AND close beyond an unbroken pivot of layer >= minBreakoutLayer; each pivot breaks once
	static void Breakouts()
	{
		var j = new List<string>();
		var e = WarmFlat(Cfg(k => { k.atrLength = 4; k.volumeLength = 4; k.layers = 1; k.minBreakoutLayer = 0; }), 6, j);
		int n = 6;
		Feed(e, n++, 100, 101, 99, 100); Feed(e, n++, 100, 103, 99.5, 102); Feed(e, n++, 102, 102.5, 100, 100.5);   // pivot high 103
		Check(e.LastHigh() != null && e.LastHigh().price == 103, "pivot high 103 formed");
		Feed(e, n++, 100.5, 102.75, 100.5, 102.75);   // strong-ish but close below pivot
		Check(!e.Has(ECandle.BreakoutUp), "H6 no close beyond pivot -> no breakout");
		Feed(e, n++, 102.5, 106, 102.5, 106);         // strong bull, close > 103
		Check(e.Has(ECandle.Strong) && e.Has(ECandle.BreakoutUp), "H6 strong bull closing beyond unbroken pivot -> BreakoutUp");
		Feed(e, n++, 106, 110, 106, 110);             // strong again, same pivot (no new pivot high yet)
		Check(!e.Has(ECandle.BreakoutUp), "H6 same pivot cannot break twice");

		var e2 = WarmFlat(Cfg(k => { k.atrLength = 4; k.volumeLength = 4; k.layers = 1; k.minBreakoutLayer = 0; }), 6);
		n = 6; Feed(e2, n++, 100, 101, 99, 100); Feed(e2, n++, 100, 103, 99.5, 102); Feed(e2, n++, 102, 102.5, 100, 100.5);
		Feed(e2, n++, 101, 104, 100, 103.5);          // closes beyond, not strong
		Check(!e2.Has(ECandle.Strong) && !e2.Has(ECandle.BreakoutUp), "H6 breakoutNeedsStrong: weak close beyond -> no breakout");
		Check(e2.BosUp, "BOS still fires on a non-strong close beyond the swing high");

		// layers = 2 default (minBreakoutLayer 1): a strong bar beyond a layer-0 pivot only is not a breakout
		var e3 = WarmFlat(Cfg(k => { k.atrLength = 4; k.volumeLength = 4; }), 6);
		n = 6; Feed(e3, n++, 100, 101, 99, 100); Feed(e3, n++, 100, 103, 99.5, 102); Feed(e3, n++, 102, 102.5, 100, 100.5);
		Feed(e3, n++, 100.5, 106, 100.5, 106);
		Check(e3.Has(ECandle.Strong) && !e3.Has(ECandle.BreakoutUp) && e3.PivotCount(1, true) == 0, "H6 layers=2: strong bar without a layer-1 pivot -> no breakout (PriceAction flagged every strong bar)");
	}

	// layer-0 BOS / CHoCH
	static void Bos()
	{
		var e = WarmFlat(Cfg(k => { k.atrLength = 4; k.volumeLength = 4; }), 6);
		int n = 6;
		Feed(e, n++, 100, 101, 99, 100); Feed(e, n++, 100, 103, 99.5, 102); Feed(e, n++, 102, 102.5, 100, 100.5);
		Feed(e, n++, 100.5, 103.5, 100, 103.25);
		Check(e.BosUp && !e.BosDown, "BOS up on close beyond the last swing high");
		Feed(e, n++, 103.25, 104, 103, 103.75);
		Check(!e.BosUp, "BOS fires once per swing");
	}

	// [H9] keyed by bar number: equal times both processed; duplicates ignored; out-of-order ignored + warned once
	static void OutOfOrder()
	{
		var w = new List<string>();
		var e = Eng(Cfg(k => { k.atrLength = 2; k.volumeLength = 2; }), null, w);
		Check(e.Add(1, T0, 1, 2, 0, 1, 10) && e.Add(2, T0, 1, 2, 0, 1, 10), "H9 same timestamp, different bar numbers -> both processed");
		Check(!e.Add(2, T0, 1, 2, 0, 1, 10), "duplicate bar number ignored silently");
		Check(!e.Add(1, T0, 1, 2, 0, 1, 10) && !e.Add(0, T0, 1, 2, 0, 1, 10) && w.Count == 1 && w[0].Contains("out-of-order"), "H9 decreasing bar number ignored, warned once");
	}

	static List<string> Run(_PriceActionEngine e, List<double[]> bars, int from, int to, int numberBase = 0)
	{
		var j = new List<string>(); e.journal = s => j.Add(s);
		for (int i = from; i < to; i++) { var b = bars[i]; e.Add(numberBase + i, T0.AddMinutes(i), b[0], b[1], b[2], b[3], b[4]); }
		return j;
	}

	// [H7] Reset mid-run == fresh engine on the remaining bars (catches stale seq / broken-pivot markers)
	static void ResetEqualsFresh()
	{
		var bars = RandomBars(1200, 11);
		Func<PriceActionConfig> c = () => Cfg(k => { k.layers = 3; k.minBreakoutLayer = 0; k.breakoutNeedsStrong = false; });
		var a = Eng(c()); Run(a, bars, 0, 600); a.Reset();
		var ja = Run(a, bars, 600, 1200);
		var b = Eng(c()); var jb = Run(b, bars, 600, 1200);
		Check(ja.Count > 50 && ja.SequenceEqual(jb), $"H7 Reset == fresh engine ({ja.Count} vs {jb.Count} journal lines)");
		Check(ja.Any(s => s.Contains("Breakout")) && ja.Any(s => s.Contains("BOS")), "H7 fixture exercises breakouts and BOS");
	}

	// [M3] roll: indicators and pivots cleared, splice TR skipped, seq continues, journal "roll reset"
	static void Roll()
	{
		var j = new List<string>();
		var e = Eng(Cfg(k => { k.atrLength = 10; k.volumeLength = 10; }), j);
		var pre = RandomBars(200, 3, 4000); var post = RandomBars(60, 4, 4060);
		for (int i = 0; i < 200; i++) e.Add(i, T0.AddMinutes(i), pre[i][0], pre[i][1], pre[i][2], pre[i][3], pre[i][4]);
		long seqBefore = e.LastHigh().seq; DateTime rollT = T0.AddMinutes(200);
		e.OnRoll(rollT);
		Check(e.LastHigh() == null && e.Structure() == EStructure.Unknown && !e.IsWarm, "M3 pivots/structure/warm-up cleared at roll");
		for (int i = 0; i < 60; i++) e.Add(200 + i, T0.AddMinutes(200 + i), post[i][0], post[i][1], post[i][2], post[i][3], post[i][4]);
		double tr = 0; for (int i = 50; i < 60; i++) { double pc = post[i - 1][3]; tr += Math.Max(post[i][1], pc) - Math.Min(post[i][2], pc); }
		Check(Near(e.Atr, tr / 10), "M3 ATR after roll uses the new contract only");
		var e2 = Eng(Cfg(k => { k.atrLength = 10; k.volumeLength = 10; }));
		e2.Add(0, rollT, post[0][0], post[0][1], post[0][2], post[0][3], post[0][4]);
		e.Reset(); // reuse: compare first-bar TR via a fresh engine fed only the splice bar
		Check(e2.Configured, "fresh engine configured");
		var all = new List<_Pivot>(); var e3 = Eng(Cfg(k => { k.atrLength = 10; k.volumeLength = 10; }), j);
		for (int i = 0; i < 200; i++) e3.Add(i, T0.AddMinutes(i), pre[i][0], pre[i][1], pre[i][2], pre[i][3], pre[i][4]);
		long s0 = e3.LastHigh().seq; e3.OnRoll(rollT);
		for (int i = 0; i < 60; i++) { e3.Add(200 + i, T0.AddMinutes(200 + i), post[i][0], post[i][1], post[i][2], post[i][3], post[i][4]); if (e3.NewPivotHigh) all.Add(e3.LastHigh()); if (e3.NewPivotLow) all.Add(e3.LastLow()); }
		Check(all.Count > 0 && all.All(p => p.extremeTime >= rollT), "M3 no pivot spans the splice");
		Check(all.Where(p => p.isHigh && p.layer == 0).All(p => p.seq > s0), "M3 seq keeps increasing across the roll");
		Check(j.Any(s => s.Contains("roll reset at " + rollT.ToString("yyyy-MM-dd HH:mm:ss"))), "M3 journal 'roll reset' with bar time");
		Check(seqBefore > 0, "fixture formed pivots before the roll");
	}

	// [H2][H4] 2000 bars: bar numbers and seq strictly increasing; pivot fields never change (snapshot compare)
	static void MonotonicAndImmutable()
	{
		var bars = RandomBars(2000, 21);
		var e = Eng(Cfg(k => k.layers = 3));
		var seen = new List<Tuple<_Pivot, string>>();
		Func<_Pivot, string> snap = p => $"{p.isHigh}{p.layer}{p.seq}{p.price}{p.extremeTime:o}{p.extremeBar}{p.confirmTime:o}{p.confirmBar}{p.label}{p.isEqual}{p.equalToSeq}{p.legSizeAtr}";
		var lastSeq = new Dictionary<string, long>(); bool mono = true;
		for (int i = 0; i < bars.Count; i++)
		{
			var b = bars[i]; e.Add(i, T0.AddMinutes(i), b[0], b[1], b[2], b[3], b[4]);
			for (int L = 0; L < 3; L++) foreach (bool hi in new[] { true, false })
			{
				var p = hi ? e.LastHigh(L) : e.LastLow(L); if (p == null) continue;
				string key = L + "" + hi; long prev;
				if (lastSeq.TryGetValue(key, out prev)) { if (p.seq < prev) mono = false; if (p.seq == prev) continue; }
				lastSeq[key] = p.seq; seen.Add(Tuple.Create(p, snap(p)));
				mono &= p.confirmBar == i && p.extremeBar <= i;
			}
		}
		Check(mono && seen.Count > 100, $"H2 seq strictly increasing, confirmBar = calc bar ({seen.Count} pivots)");
		Check(seen.All(t => snap(t.Item1) == t.Item2), "H4 pivot fields never change after creation");
		Check(lastSeq.ContainsKey("2True"), "layer 2 pivots formed");
	}

	static _PaBar B(double o, double h, double l, double c) { return new _PaBar { o = o, h = h, l = l, c = c }; }

	// [M7] corrected reversal rules, BodyAtHigh/Low naming, engulfing on range
	static void CandleRules()
	{
		var k = new PriceActionConfig(); double atr = 2;   // near = 0.2, eps = 0.1
		var pBear = B(12, 12.5, 9.5, 10);
		Check((_PriceActionEngine.ClassifyCandle(B(10, 11.1, 10, 11), pBear, atr, k) & ECandle.BullReversal) == 0, "M7 zero-tail bull: rule 2 fails -> no BullReversal (rule 1 only)");
		Check((_PriceActionEngine.ClassifyCandle(B(10, 11.1, 9.4, 11), pBear, atr, k) & ECandle.BullReversal) != 0, "M7 rules 1+2 -> BullReversal");
		var pBull = B(10, 12.5, 9.5, 12);
		Check((_PriceActionEngine.ClassifyCandle(B(12, 12.05, 9.4, 9.4), pBull, atr, k) & ECandle.BearReversal) != 0, "M7 bear rules 1+5 -> BearReversal");
		Check((_PriceActionEngine.ClassifyCandle(B(11.5, 12.05, 9.4, 9.4), pBull, atr, k) & ECandle.BearReversal) == 0, "M7 bear open below prior close - near: rule 1 fails");
		Check((_PriceActionEngine.ClassifyCandle(B(12, 12.05, 11.85, 11.9), pBull, atr, k) & ECandle.BearReversal) == 0, "M7 bear close within near of prior close is not 'well below'");
		var f = _PriceActionEngine.ClassifyCandle(B(10.9, 11, 9, 10.95), B(10, 11.5, 9.5, 10.5), atr, k);
		Check((f & ECandle.BodyAtHigh) != 0 && (f & ECandle.BodyAtLow) == 0, "M7 hammer = BodyAtHigh");
		f = _PriceActionEngine.ClassifyCandle(B(9.1, 11, 9, 9.05), B(10, 11.5, 9.5, 10.5), atr, k);
		Check((f & ECandle.BodyAtLow) != 0 && (f & ECandle.BodyAtHigh) == 0, "M7 shooting star = BodyAtLow");
		Check((_PriceActionEngine.ClassifyCandle(B(9, 12.5, 8.9, 12.2), B(11, 12, 10, 10.5), atr, k) & ECandle.Engulfing) != 0, "engulfing = body engulfs prior range");
		Check((_PriceActionEngine.ClassifyCandle(B(10.6, 12.5, 8.9, 11.9), B(11, 12, 10, 10.5), atr, k) & ECandle.Engulfing) == 0, "body inside prior range -> not engulfing");
		Check((_PriceActionEngine.ClassifyCandle(B(10, 10.2, 9.8, 10.05), B(10, 10.5, 9.5, 10), atr, k) & (ECandle.Doji | ECandle.Inside)) == (ECandle.Doji | ECandle.Inside), "doji + inside");
	}

	// [L4] Changed = XOR of consecutive flags
	static void ChangedXor()
	{
		var e = WarmFlat(Cfg(k => { k.atrLength = 4; k.volumeLength = 4; }), 6);
		var prev = e.Candle; Feed(e, 6, 100, 104, 100, 104); 
		Check(e.Changed == (e.Candle ^ prev), "L4 Changed == flags XOR previous flags");
	}

	sealed class S<T> : ISeries<T> { public T v; public T this[int i] { get { return v; } } }
	sealed class Inst : IInstrument
	{
		public S<double> o = new S<double>(), h = new S<double>(), l = new S<double>(), c = new S<double>(), vol = new S<double>(); public S<DateTime> t = new S<DateTime>();
		public ISeries<double> Open { get { return o; } } public ISeries<double> High { get { return h; } } public ISeries<double> Low { get { return l; } }
		public ISeries<double> Close { get { return c; } } public ISeries<double> Volume { get { return vol; } } public ISeries<DateTime> Time { get { return t; } }
		public EBarState Status { get; set; } public int CurrentBar { get; set; }
		public void Set(int n, double hi, EBarState st) { CurrentBar = n; t.v = T0.AddMinutes(n); o.v = 100; h.v = hi; l.v = 99; c.v = 100; vol.v = 10; Status = st; }
	}

	// [H1][H3] Step commits on Status == Close only; a tick that makes then un-makes a high leaves no trace
	static void StepClosedOnly()
	{
		var a = Eng(Cfg(k => { k.atrLength = 2; k.volumeLength = 2; })); var b = Eng(Cfg(k => { k.atrLength = 2; k.volumeLength = 2; }));
		var ia = new Inst(); var ib = new Inst();
		double[] closedHighs = { 101, 101.5, 101, 102, 101, 103, 101.5, 101 };
		for (int n = 0; n < closedHighs.Length; n++)
		{
			ia.Set(n, 120, EBarState.Inside); a.Step(ia); ia.Set(n, 90, EBarState.Open); a.Step(ia);   // intrabar noise
			ia.Set(n, closedHighs[n], EBarState.Close); a.Step(ia); a.Step(ia);                         // close (twice: dedupe)
			ib.Set(n, closedHighs[n], EBarState.Close); b.Step(ib);
		}
		Check(a.PivotCount(0, true) == b.PivotCount(0, true) && a.PivotCount(0, true) > 0 && a.LastHigh().price == b.LastHigh().price && a.LastHigh().price != 120, "H1/H3 tick replay == closed-bar replay");
	}

	static void ZigZag()
	{
		var e = Eng(Cfg(k => { k.pivotMode = EPivotMode.AtrZigZag; k.atrLength = 3; k.volumeLength = 3; k.atrMultiple = 2; }));
		int n = 0; for (int i = 0; i < 4; i++) Mid(e, n++, 100);
		for (int i = 0; i < 6; i++) Mid(e, n++, 100 + 2 * i);   // up 10
		for (int i = 0; i < 6; i++) Mid(e, n++, 110 - 2 * i);   // down 10
		for (int i = 0; i < 6; i++) Mid(e, n++, 100 + 2 * i);
		Check(e.PivotCount(0, true) >= 1 && e.PivotCount(0, false) >= 1 && e.LastHigh().price == 111, "AtrZigZag alternates high/low pivots");
	}

	// ---------------------------------------------------------------- Phase 3 S5: a second PRICEACTION instance
	static void ExtraCanon()
	{
		var a = Cfg(); var b = Cfg(k => { k.role = "D2"; k.preset = EPriceActionPreset.Futures; });
		Check(a.Canon() == b.Canon(), "S5 Canon ignores role / preset label");
		Check(a.Canon() != Cfg(k => k.strength = 2).Canon(), "S5 Canon sees strength");
		Check(a.Canon() != Cfg(k => k.dataStream = 2).Canon(), "S5 Canon sees dataStream");
		Check(a.Canon() != Cfg(k => k.factorDoji = 0.1000001).Canon(), "S5 Canon round-trips doubles");
		Check(!new PriceActionConfig().presetApplied && PriceActionConfig.Futures().presetApplied, "S5 presetApplied set by ApplyPreset only");
	}

	// Two instances on the same bars with different settings never touch each other's state.
	static void ExtraIndependent()
	{
		var bars = RandomBars(400, 7);
		var main = Eng(Cfg()); var extra = Eng(Cfg(k => { k.strength = 3; k.role = "D2"; }));
		var solo = Eng(Cfg());
		bool same = true; int extraPivots = 0, mainPivots = 0;
		for (int i = 0; i < bars.Count; i++)
		{
			var b = bars[i];
			main.Add(i, T0.AddMinutes(i), b[0], b[1], b[2], b[3], b[4]);
			extra.Add(i, T0.AddMinutes(i), b[0], b[1], b[2], b[3], b[4]);
			solo.Add(i, T0.AddMinutes(i), b[0], b[1], b[2], b[3], b[4]);
			if (main.NewPivotHigh) mainPivots++; if (extra.NewPivotHigh) extraPivots++;
			same &= main.NewPivotHigh == solo.NewPivotHigh && main.NewPivotLow == solo.NewPivotLow && main.Candle == solo.Candle && main.Structure() == solo.Structure();
		}
		Check(same, "S5 main output identical with and without an extra instance");
		Check(extraPivots > 0 && extraPivots < mainPivots, $"S5 extra (strength 3) runs its own pivots ({extraPivots} < {mainPivots})");
		Check(extra.LastBar == bars.Count - 1, "S5 LastBar exposes the committed bar");
	}

	// Host model of _CustomSignal.findSharedPa: (dataStream, Canon()) -> one reference.
	static void ExtraSharing()
	{
		var pool = new List<_PriceActionEngine>();
		Func<PriceActionConfig, _PriceActionEngine> get = c =>
		{
			foreach (var e in pool) if (e.config.dataStream == c.dataStream && e.config.Canon() == c.Canon()) return e;
			var n = Eng(c); pool.Add(n); return n;
		};
		var x = get(Cfg(k => { k.dataStream = 2; k.role = "A"; }));
		var y = get(Cfg(k => { k.dataStream = 2; k.role = "B"; }));
		var z = get(Cfg(k => { k.dataStream = 2; k.role = "C"; k.strength = 2; }));
		var w = get(Cfg(k => { k.dataStream = 3; k.role = "D"; }));
		Check(ReferenceEquals(x, y), "S5 equal (ds, Canon) share one reference");
		Check(!ReferenceEquals(x, z) && !ReferenceEquals(x, w) && pool.Count == 3, "S5 different settings or stream get their own instance");
		Mid(x, 1, 100); Mid(y, 1, 100);   // second step of the same bar through the other role is a no-op
		Check(x.LastBar == 1, "S5 shared instance dedupes the same closed bar");
	}

	// Data2 instance resets through the same-root pending flag, once, on its own first bar at/after the Data1 roll.
	static void ExtraRollOnData2()
	{
		var j = new List<string>();
		var e = Eng(Cfg(k => { k.dataStream = 2; k.role = "D2"; }), j);
		var pend = new _RollPending();
		int resets = 0;
		for (int i = 0; i < 30; i++)
		{
			DateTime t = T0.AddMinutes(5 * i);
			if (i == 20) pend.Arm(t.AddMinutes(-2));   // Data1 rolled between Data2 bars 19 and 20
			for (int tick = 0; tick < 2; tick++)          // the host can see the same closed bar twice (stepExtraPa order)
			{
				if (e.LastBar != i && pend.Fire(i, t)) { e.OnRoll(t); resets++; }
				e.Add(i, t, 100 + i, 101 + i, 99 + i, 100 + i, 100);
			}
		}
		Check(resets == 1 && j.Count(s => s.Contains("roll reset")) == 1, $"S5 Data2 OnRoll fires exactly once via the pending flag ({resets})");
		Check(!pend.Armed, "S5 pending flag cleared after firing");
		Check(e.IsWarm == false || e.LastHigh() == null || e.LastHigh().confirmBar >= 20, "S5 no pre-roll pivot survives the reset");
	}
}
