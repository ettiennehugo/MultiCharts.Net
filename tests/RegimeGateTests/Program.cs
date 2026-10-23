using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using PowerLanguage.Function;

// Off-MultiCharts tests for the A2-63 host core and the G-R regime gate (_RegimeGate, specs/regime-consumer-prereg.md rev 2).
static class Program
{
	static int pass, fail;
	static void Check(bool ok, string what) { if (ok) pass++; else { fail++; Console.WriteLine("FAIL: " + what); } }
	static double Gauss(_Rng r) { double u = Math.Max(r.NextDouble(), 1e-300), v = r.NextDouble(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }

	static RegimeState St(bool valid, int label, DateTime asOf, bool fb, params double[] p)
	{ return new RegimeState { Valid = valid, Label = label, RawArgmax = label, AsOf = asOf, UsingFallback = fb, K = 3, Probs = p.Length > 0 ? p : new[] { 0.2, 0.3, 0.5 } }; }

	static int Main()
	{
		GateDecisions();
		NoLookahead();
		HostTradesAndArms();
		HashStability();
		Console.WriteLine($"RegimeGateTests: {pass} passed, {fail} failed");
		return fail == 0 ? 0 : 1;
	}

	// ---------------- 1. gate decisions on synthetic labels ----------------
	static void GateDecisions()
	{
		DateTime t = new DateTime(2020, 3, 10), y = t.AddDays(-1);
		for (int L = 0; L < 3; L++)
		{
			var s = St(true, L, y, false);
			var u = RegimeGate.Decide(ERegimeGateArm.U, s, t, L); var g = RegimeGate.Decide(ERegimeGateArm.G, s, t, L);
			Check(u.allow, "U allows label " + L);
			Check(u.wouldBlock == (L == 2) && g.wouldBlock == (L == 2), "wouldBlock iff label 2, L=" + L);
			Check(g.allow == (L != 2), "G blocks only label 2, L=" + L);
			Check(g.usable && Math.Abs(g.pFilt - s.Probs[L]) < 1e-15, "pFilt = Probs[Label]");
			var fb = St(true, L, y, true);
			Check(!RegimeGate.Decide(ERegimeGateArm.G, fb, t, L).usable && RegimeGate.Decide(ERegimeGateArm.G, fb, t, L).allow, "G: fallback day not usable, allowed");
			var a = RegimeGate.Decide(ERegimeGateArm.A, fb, t, L);
			Check(a.usable && a.allow == (L != 2) && a.fb, "A: fallback tercile used, blocks tercile 2, L=" + L);
		}
		// post-hysteresis label, never RawArgmax
		var h = St(true, 1, y, false, 0.1, 0.4, 0.5); h.RawArgmax = 2;
		Check(RegimeGate.Decide(ERegimeGateArm.G, h, t, 2).allow, "G uses Label, not RawArgmax");
		h = St(true, 2, y, false, 0.1, 0.5, 0.4); h.RawArgmax = 1;
		Check(!RegimeGate.Decide(ERegimeGateArm.G, h, t, 0).allow, "G blocks Label 2 even if RawArgmax 1");
		// invalid / neutral
		Check(RegimeGate.Decide(ERegimeGateArm.G, RegimeState.Neutral(3), t, -1).allow && !RegimeGate.Decide(ERegimeGateArm.G, RegimeState.Neutral(3), t, -1).usable, "neutral: allowed, not usable");
		Check(RegimeGate.Decide(ERegimeGateArm.A, null, t, -1).allow, "null state allowed");
		Check(RegimeGate.Decide(ERegimeGateArm.G, St(false, 2, y, false), t, 2).allow, "invalid label 2 allowed");
		// line format
		var line = RegimeGate.Line(RegimeGate.Decide(ERegimeGateArm.G, St(true, 2, y, false), t, 1), true, "00112233aabbccdd", RegimeGate.PlaceboSeed);
		Check(line == "REGGATE 2020-03-10 arm(G) asOf(2020-03-09) label(2) raw(2) pFilt(0.5) fb(0) usable(1) allow(0) atrT(1) wouldBlock(1) signal(1) cfg(00112233aabbccdd) seed(20261022)", "REGGATE line: " + line);
		var tf = RegimeGate.TradeFields(RegimeGate.Decide(ERegimeGateArm.U, St(true, 0, y, false), t, 0), "h", 20261022);
		Check(tf["arm"] == "U" && tf["label"] == "0" && tf["cfg"] == "h" && tf["seed"] == "20261022" && tf.ContainsKey("pFilt"), "trade fields");
		// ApplyArm
		var r = new RegimeConfig(); var r0 = new RegimeConfig();
		RegimeGate.ApplyArm(new RegimeGateConfig { arm = ERegimeGateArm.A }, r);
		Check(r.enabled && !r.publish && r.fitSessions == 100000 && r.minSessions == 100000 && r.Validate() == null, "ApplyArm A");
		r = new RegimeConfig(); RegimeGate.ApplyArm(new RegimeGateConfig { arm = ERegimeGateArm.G }, r);
		Check(r.fitSessions == r0.fitSessions && r.minSessions == r0.minSessions && r.enabled && !r.publish, "ApplyArm G keeps defaults");
		Check(new RegimeGateConfig().arm == ERegimeGateArm.U && new RegimeGateConfig().placeboSeed == 20261022UL, "defaults U, seed 20261022");
	}

	// ---------------- 2. no-lookahead ----------------
	static void NoLookahead()
	{
		DateTime t = new DateTime(2020, 3, 10);
		Check(!RegimeGate.Decide(ERegimeGateArm.G, St(true, 2, t, false), t, 2).usable, "AsOf == t not usable");
		Check(RegimeGate.Decide(ERegimeGateArm.G, St(true, 2, t, false), t, 2).allow, "AsOf == t allowed");
		Check(!RegimeGate.Decide(ERegimeGateArm.A, St(true, 2, t.AddDays(1), true), t, 2).usable, "AsOf > t not usable");
		Check(!RegimeGate.Decide(ERegimeGateArm.G, St(true, 2, t.AddHours(-1).AddDays(1), false), t.AddHours(9.5), 2).usable, "intraday AsOf on t not usable");
		Check(RegimeGate.Decide(ERegimeGateArm.G, St(true, 2, t.AddDays(-3), false), t, 2).usable, "AsOf 3 days back (weekend) usable");

		// Engine-level: decisions for day t, taken from the state before day t is fed, do not change when day t and later are perturbed.
		var days = Market(1300, 7);
		var a = Decisions(days, ERegimeGateArm.G, new RegimeConfig());
		var pert = days.Select(d => d).ToList(); int cut = 1150; var rr = new _Rng(99);
		for (int i = cut; i < pert.Count; i++) { var d = pert[i]; pert[i] = new Day { date = d.date, close = d.close * (1 + 0.05 * Gauss(rr)), hv20 = d.hv20 * 3, hv63 = d.hv63 * 2, high = d.high * 1.1, low = d.low * 0.9, atr = d.atr * 4 }; }
		var b = Decisions(pert, ERegimeGateArm.G, new RegimeConfig());
		bool same = true; for (int i = 0; i <= cut; i++) same &= a[i].label == b[i].label && a[i].allow == b[i].allow && a[i].atrTercile == b[i].atrTercile && Same(a[i].pFilt, b[i].pFilt);
		Check(same, "decisions up to and including the perturbed day unchanged");
		bool diff = false; for (int i = cut + 1; i < a.Count; i++) diff |= a[i].atrTercile != b[i].atrTercile || a[i].label != b[i].label;
		Check(diff, "perturbation does reach later decisions (test is live)");
		Check(a.All(d => !d.usable || d.asOf < d.session), "every usable decision has AsOf < t");
		Check(a.Count(d => d.usable) > 100, "HMM usable on >100 days (" + a.Count(d => d.usable) + ")");
	}

	static bool Same(double x, double y) { return (double.IsNaN(x) && double.IsNaN(y)) || x == y; }

	// The host latch sequence: at session t the engine holds days < t (stepRegime feeds PrevDate); then day t is fed after the session.
	static List<RegimeGateDecision> Decisions(List<Day> days, ERegimeGateArm arm, RegimeConfig rc)
	{
		RegimeGate.ApplyArm(new RegimeGateConfig { arm = arm }, rc);
		var eng = new _RegimeEngine(); eng.config = rc; string err = eng.Configure(); if (err != null) throw new Exception(err);
		var terc = new AtrTercileTracker(rc.atrPctLookback, rc.K); var res = new List<RegimeGateDecision>();
		for (int i = 0; i < days.Count; i++)
		{
			res.Add(RegimeGate.Decide(arm, eng.State, days[i].date, i > 0 && terc.LastDate == days[i - 1].date ? terc.Tercile : -1));
			var d = days[i];
			eng.AddDay(d.date, d.close, d.hv20, d.hv63, d.high, d.low, d.atr, false);
			terc.AddDay(d.date, d.atr);
		}
		return res;
	}

	// ---------------- 3. host trades: U == ungated reference; G = U minus blocked days; A uses terciles ----------------
	sealed class Day { public DateTime date; public double close, hv20, hv63, high, low, atr; }
	struct Bar { public DateTime close; public double o, h, l, c; }
	struct Trade { public DateTime entryTime, exitTime; public double entry, exit; public override string ToString() { return entryTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " " + entry + "->" + exit; } }

	static List<Day> Market(int n, ulong seed)
	{
		var r = new _Rng(seed); var days = new List<Day>(); double c = 2000; int s = 0; var rets = new List<double>(); var rng = new List<double>();
		double[] vol = { 0.006, 0.012, 0.025 }; DateTime d = new DateTime(2012, 1, 2);
		for (int i = 0; i < n; i++)
		{
			while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
			if (r.NextDouble() > 0.98) s = (s + 1 + r.NextInt(2)) % 3;
			double ret = vol[s] * Gauss(r); c *= Math.Exp(ret); rets.Add(ret);
			double hl = c * vol[s] * (1 + Math.Abs(Gauss(r))); rng.Add(hl);
			days.Add(new Day { date = d, close = c, hv20 = Hv(rets, 20), hv63 = Hv(rets, 63), high = c + hl / 2, low = c - hl / 2, atr = rng.Count >= 20 ? rng.Skip(rng.Count - 20).Average() : double.NaN });
			d = d.AddDays(1);
		}
		return days;
	}
	static double Hv(List<double> r, int n) { if (r.Count < n) return double.NaN; var w = r.Skip(r.Count - n).ToArray(); double m = w.Average(); return Math.Sqrt(w.Sum(x => (x - m) * (x - m)) / (n - 1) * 252); }

	// 5m RTH bars 09:35..16:00 ET (close stamps) for each day, a random walk from the prior close
	static List<Bar> Intraday(Day d, double prevClose, _Rng r, double sd)
	{
		var bars = new List<Bar>(); double p = prevClose;
		for (var t = d.date.AddHours(9).AddMinutes(35); t <= d.date.AddHours(16); t = t.AddMinutes(5))
		{
			double o = p, c = o + sd * Gauss(r), h = Math.Max(o, c) + Math.Abs(sd * Gauss(r)) * 0.5, l = Math.Min(o, c) - Math.Abs(sd * Gauss(r)) * 0.5;
			bars.Add(new Bar { close = t, o = o, h = h, l = l, c = c }); p = c;
		}
		return bars;
	}

	// Drives A263Core like the MC host (orders sent on a bar's close work on the next bar; stop fills at max(open, stop)).
	static List<Trade> RunCore(List<Day> days, List<List<Bar>> intraday, List<RegimeGateDecision> dec)
	{
		var core = new A263Core(new A263Config()); var trades = new List<Trade>(); bool isLong = false; Trade cur = default(Trade);
		double pendStop = double.NaN; bool pendExit = false;
		for (int i = 0; i < days.Count; i++)
			foreach (var b in intraday[i])
			{
				if (pendExit) { cur.exit = b.o; cur.exitTime = b.close; trades.Add(cur); isLong = false; }
				else if (!double.IsNaN(pendStop) && b.h >= pendStop) { cur = new Trade { entry = Math.Max(b.o, pendStop), entryTime = b.close }; isLong = true; }
				pendStop = double.NaN; pendExit = false;
				if (core.IsLatchBar(b.close)) core.Latch(b.close, b.o, i > 0 ? days[i - 1].atr : double.NaN, dec[i].allow);   // M-DAILY Atr20d = through PrevDate
				var a = core.Step(b.close, isLong);
				if (a.exit) pendExit = true; else if (a.sendStop) pendStop = a.stop;
			}
		return trades;
	}

	// Independent reference of the catalogue rule (ungated): first fill bar = a bar opening in [09:35, 15:30) with high >= O + 0.5 x ATR,
	// exit at the open of the 16:00-close bar (the market order sent on the 15:55 close).
	static List<Trade> Reference(List<Day> days, List<List<Bar>> intraday)
	{
		var trades = new List<Trade>();
		for (int i = 1; i < days.Count; i++)
		{
			var bars = intraday[i]; double atr = days[i - 1].atr; if (double.IsNaN(atr)) continue;
			double thr = bars[0].o + 0.5 * atr;
			for (int j = 1; j < bars.Count; j++)
			{
				var open = bars[j].close.AddMinutes(-5).TimeOfDay;
				if (open >= new TimeSpan(15, 30, 0)) break;
				if (bars[j].h >= thr) { var x = bars.First(b => b.close.TimeOfDay == new TimeSpan(16, 0, 0)); trades.Add(new Trade { entry = Math.Max(bars[j].o, thr), entryTime = bars[j].close, exit = x.o, exitTime = x.close }); break; }
			}
		}
		return trades;
	}

	static void HostTradesAndArms()
	{
		var days = Market(1300, 11); var r = new _Rng(5); var intraday = new List<List<Bar>>();
		for (int i = 0; i < days.Count; i++) intraday.Add(Intraday(days[i], i > 0 ? days[i - 1].close : 2000, r, days[i].close * 0.0012));
		var dU = Decisions(days, ERegimeGateArm.U, new RegimeConfig());
		var dG = Decisions(days, ERegimeGateArm.G, new RegimeConfig());
		var dA = Decisions(days, ERegimeGateArm.A, new RegimeConfig());
		var tU = RunCore(days, intraday, dU); var tRef = Reference(days, intraday);
		Check(tU.Count > 100, "U trades > 100 (" + tU.Count + ")");
		Check(tU.Select(x => x.ToString()).SequenceEqual(tRef.Select(x => x.ToString())) && tU.Select(x => x.exit).SequenceEqual(tRef.Select(x => x.exit)), "Arm U == ungated host reference (" + tU.Count + " vs " + tRef.Count + ")");
		Check(dU.All(d => d.allow), "U never blocks");
		Check(tU.All(x => x.entryTime.Date == x.exitTime.Date && x.exitTime.TimeOfDay == new TimeSpan(16, 0, 0)), "one entry per day, flat by the 15:55 order");
		Check(tU.GroupBy(x => x.entryTime.Date).All(g => g.Count() == 1), "max one entry per day");

		var tG = RunCore(days, intraday, dG); var blockedG = new HashSet<DateTime>(dG.Where(d => d.wouldBlock).Select(d => d.session));
		Check(blockedG.Count > 10, "G blocks some days (" + blockedG.Count + ")");
		Check(tG.Select(x => x.ToString()).SequenceEqual(tU.Where(x => !blockedG.Contains(x.entryTime.Date)).Select(x => x.ToString())), "G trades == U trades minus wouldBlock days");
		Check(Enumerable.Range(0, days.Count).All(i => dU[i].wouldBlock == dG[i].wouldBlock && dU[i].label == dG[i].label), "U journals the same wouldBlock as G");

		var tA = RunCore(days, intraday, dA); var blockedA = new HashSet<DateTime>(dA.Where(d => d.wouldBlock).Select(d => d.session));
		Check(dA.All(d => !d.usable || d.fb), "A: every usable day is the fallback");
		Check(dA.All(d => !d.usable || d.label == d.atrTercile), "A: engine label == mirrored ATR tercile");
		Check(dA.Count(d => d.usable) > 900 && blockedA.Count > 100, "A usable/blocked counts (" + dA.Count(d => d.usable) + "/" + blockedA.Count + ")");
		Check(tA.Select(x => x.ToString()).SequenceEqual(tU.Where(x => !blockedA.Contains(x.entryTime.Date)).Select(x => x.ToString())), "A trades == U trades minus tercile-2 days");
		Check(Enumerable.Range(0, days.Count).All(i => dU[i].atrTercile == dA[i].atrTercile), "ATR tercile identical across arms");
	}

	// ---------------- 4. hash stability ----------------
	static void HashStability()
	{
		Func<ERegimeGateArm, string> h = arm => { var g = new RegimeGateConfig { arm = arm }; var rc = new RegimeConfig(); RegimeGate.ApplyArm(g, rc); return RegimeGate.Hash(rc, g, new A263Config()); };
		Check(RegimeGate.Fnv1a64("") == "cbf29ce484222325" && RegimeGate.Fnv1a64("a") == "af63dc4c8601ec8c", "FNV-1a 64 test vectors");
		string u = h(ERegimeGateArm.U), g2 = h(ERegimeGateArm.G), a = h(ERegimeGateArm.A);
		Console.WriteLine($"cfg hashes U={u} G={g2} A={a}");
		Check(u == h(ERegimeGateArm.U) && u.Length == 16, "hash repeatable");
		Check(u != g2 && g2 != a && u != a, "hash differs per arm");
		Check(u == PinnedU && g2 == PinnedG && a == PinnedA, "hash pinned (canon or defaults changed?)");
		var prev = Thread.CurrentThread.CurrentCulture;
		try { Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE"); Check(h(ERegimeGateArm.U) == u, "hash culture-invariant (de-DE)"); }
		finally { Thread.CurrentThread.CurrentCulture = prev; }
		var rc2 = new RegimeConfig(); var gc = new RegimeGateConfig(); RegimeGate.ApplyArm(gc, rc2); rc2.switchProb += 1e-9;
		Check(RegimeGate.Hash(rc2, gc, new A263Config()) != u, "hash sensitive to a RegimeConfig field");
		rc2 = new RegimeConfig(); RegimeGate.ApplyArm(gc, rc2);
		Check(RegimeGate.Hash(rc2, gc, new A263Config { k = 0.6 }) != u, "hash sensitive to host core");
		Check(RegimeGate.Hash(rc2, new RegimeGateConfig { placeboSeed = 1 }, new A263Config()) != u, "hash sensitive to placebo seed");
	}
	const string PinnedU = "5d3311a5c01cb357", PinnedG = "dff52021fedecef9", PinnedA = "e7bfcd1401bd19ab";   // re-pin only with a new trial
}
