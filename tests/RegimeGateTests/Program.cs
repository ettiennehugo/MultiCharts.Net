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
		AtrPriorOnly();
		HalfDaysAndDst();
		NoUsableDaysAndCounts();
		LabelIgnoresDayD();
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
		Check(line == "REGGATE 2020-03-10 arm(G) asOf(2020-03-09) label(2) raw(2) pFilt(0.5) fb(0) usable(1) allow(0) atrT(1) wouldBlock(1) why(Ok) signal(1) cfg(00112233aabbccdd) seed(20261022)", "REGGATE line: " + line);
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
		var bars = new List<Bar>(); double p = prevClose; var cme = Extracted.Cme.Rules.Get(d.date);
		DateTime end = d.date + (cme.RthCloseEt ?? cme.HaltEt ?? new TimeSpan(16, 0, 0));
		for (var t = d.date.AddHours(9).AddMinutes(35); t <= end; t = t.AddMinutes(5))
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
				if (core.IsLatchBar(b.close)) { var cme = Extracted.Cme.Rules.Get(b.close.Date); core.Latch(b.close, b.o, i > 0 ? days[i - 1].atr : double.NaN, i > 0 ? days[i - 1].date : DateTime.MinValue, dec[i].allow, cme.RthCloseEt, cme.NoRth); }   // M-DAILY Atr20d = through PrevDate
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
			var cme = Extracted.Cme.Rules.Get(days[i].date); if (cme.NoRth) continue;
			var exitClose = cme.RthCloseEt.HasValue ? cme.RthCloseEt.Value : new TimeSpan(16, 0, 0);   // the bar after the one closing at exit time
			double thr = bars[0].o + 0.5 * atr;
			for (int j = 1; j < bars.Count; j++)
			{
				var open = bars[j].close.AddMinutes(-5).TimeOfDay;
				if (open >= new TimeSpan(15, 30, 0) || open >= exitClose - new TimeSpan(0, 10, 0)) break;
				if (bars[j].h >= thr) { var x = bars.First(b => b.close.TimeOfDay == exitClose); trades.Add(new Trade { entry = Math.Max(bars[j].o, thr), entryTime = bars[j].close, exit = x.o, exitTime = x.close }); break; }
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
		Check(tU.All(x => x.entryTime.Date == x.exitTime.Date && (x.exitTime.TimeOfDay == new TimeSpan(16, 0, 0) || x.exitTime.TimeOfDay == new TimeSpan(13, 15, 0))), "one entry per day, flat by the 15:55 (half day 13:10) order");
		Check(tU.Any(x => x.exitTime.TimeOfDay == new TimeSpan(13, 15, 0)), "some half-day trades in the sample");
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
		// PR #46 review Q1: roll handling is hashed (an Off dev build cannot share a hash with a Table holdout build)
		Check(RegimeGate.Hash(rc2, gc, new A263Config { rollDetection = "Off" }) != u, "hash sensitive to RollDetection");
		Check(RegimeGate.Hash(rc2, gc, new A263Config { rollToYear = 2030 }) != u, "hash sensitive to the roll table years");
		Check(RegimeGate.Hash(rc2, gc, new A263Config { rollDatesDigest = "0000000000000000" }) != u, "hash sensitive to the roll-date digest");
		Check(RegimeGate.Hash(rc2, gc, new A263Config { tradingDay = "Calendar" }) != u, "hash sensitive to the trading-day key");
		Check(new A263Config().rollDatesDigest == "fdd63bd8f179a8d4" && new A263Config().rollDetection == "Table" && new A263Config().tradingDay == "Globex18ET",
			"A263Config roll defaults = the Table / Globex build (digest also pinned in EventTests)");
		// ruling 5: the hash is over the NAMED prereg field list, not field order or record length
		var fields = RegimeGate.CanonFields(rc2, gc, new A263Config());
		var extra = new Dictionary<string, string>(fields); extra["zz.appended"] = "1"; extra["host.futureField"] = "x"; extra["aa.first"] = "2";
		Check(RegimeGate.Hash(extra) == PinnedU, "hash pinned with extra fields appended");
		var rev = new Dictionary<string, string>(StringComparer.Ordinal); foreach (var k in fields.Keys.Reverse()) rev[k] = fields[k];
		Check(RegimeGate.Hash(rev) == PinnedU, "hash independent of field order");
		Check(RegimeGate.HashFields.All(fields.ContainsKey) && RegimeGate.HashFields.Distinct().Count() == RegimeGate.HashFields.Length, "every named field produced, no duplicates");
		var miss = new Dictionary<string, string>(fields); miss.Remove("rg.switchProb"); bool threw = false;
		try { RegimeGate.Hash(miss); } catch (ArgumentException) { threw = true; }
		Check(threw, "missing named field throws");
		Check(RegimeGate.HashFields.Contains("v") && RegimeGate.Version == "A263-RG-2" && RegimeGate.Canon(fields).Contains(";v=A263-RG-2;"), "version tag A263-RG-2 inside the hashed named-field record");
		var noV = new Dictionary<string, string>(fields); noV["v"] = "A263-RG-1"; Check(RegimeGate.Hash(noV) != PinnedU, "hash sensitive to the version tag");
		Check(RegimeGate.Canon(fields).StartsWith("gate.arm=U;gate.blocked=2;gate.seed=20261022;host.activeEnd=15:30:00;"), "canon sorted by name: " + RegimeGate.Canon(fields).Substring(0, 60));
	}
	// ---------------- ruling 1: ATR20d over completed prior sessions only ----------------
	static void AtrPriorOnly()
	{
		var core = new A263Core(new A263Config()); DateTime d = new DateTime(2024, 3, 12);
		core.Latch(d.AddHours(9).AddMinutes(35), 5000, 40, d.AddDays(-1), true);
		Check(core.HasSignal && core.Threshold == 5020 && !core.AtrRejected, "threshold O + 0.5 x ATR from the prior session");
		core.Latch(d.AddDays(1).AddHours(9).AddMinutes(35), 5000, 40, d.AddDays(1), true);
		Check(!core.HasSignal && core.AtrRejected, "ATR including today's session rejected (no entry)");
		core.Latch(d.AddDays(2).AddHours(9).AddMinutes(35), 5000, 40, DateTime.MinValue, true);
		Check(!core.HasSignal, "ATR with no as-of date rejected");
		core.Latch(d.AddDays(3).AddHours(9).AddMinutes(35), 5000, double.NaN, d.AddDays(2), true);
		Check(!core.HasSignal, "NaN ATR: no entry");
		core.Latch(d.AddDays(4).AddHours(9).AddMinutes(35), 5000, 40, d.AddDays(3), true);
		Check(core.Suffix().Contains(" atr20d(40) atrAsOf(2024-03-15) atrOk(1) thr(5020) exitAt(15:55) halfDay(0) noRth(0)"), "ATR journaled at the open: " + core.Suffix());
	}

	// ---------------- ruling 2: half days (CME calendar helper), no-RTH days, DST ----------------
	static void HalfDaysAndDst()
	{
		var cfg = new A263Config();
		foreach (var day in new[] { new DateTime(2024, 11, 29), new DateTime(2024, 12, 24), new DateTime(2024, 7, 3), new DateTime(2019, 7, 3) })
		{
			var cme = Extracted.Cme.Rules.Get(day);
			Check(cme.RthCloseEt == new TimeSpan(13, 15, 0) && !cme.NoRth, "CME early close 13:15 ET on " + day.ToString("yyyy-MM-dd"));
			var core = new A263Core(cfg);
			core.Latch(day.AddHours(9).AddMinutes(35), 5000, 40, day.AddDays(-1), true, cme.RthCloseEt, cme.NoRth);
			Check(core.ExitAt == new TimeSpan(13, 10, 0) && core.Suffix().Contains("exitAt(13:10) halfDay(1)"), "half-day exit at early close - 5m");
			Check(!core.Step(day.AddHours(13).AddMinutes(5), true).exit && core.Step(day.AddHours(13).AddMinutes(10), true).exit, "exit on the first bar closing >= 13:10");
			var c2 = new A263Core(cfg); c2.Latch(day.AddHours(9).AddMinutes(35), 5000, 40, day.AddDays(-1), true, cme.RthCloseEt, cme.NoRth);
			Check(c2.Step(day.AddHours(13).AddMinutes(5), false).sendStop && !c2.Step(day.AddHours(13).AddMinutes(10), false).sendStop, "no stop at/after the half-day exit time");
			var c1 = new A263Core(cfg); c1.Latch(day.AddHours(9).AddMinutes(31), 5000, 40, day.AddDays(-1), true, cme.RthCloseEt, cme.NoRth);
			Check(c1.Step(day.AddHours(13).AddMinutes(10), true).exit && !c1.Step(day.AddHours(13).AddMinutes(9), true).exit, "1m: exit on the 13:10 close bar");
		}
		var normal = Extracted.Cme.Rules.Get(new DateTime(2024, 11, 27));
		var cn = new A263Core(cfg); cn.Latch(new DateTime(2024, 11, 27, 9, 35, 0), 5000, 40, new DateTime(2024, 11, 26), true, normal.RthCloseEt, normal.NoRth);
		Check(cn.ExitAt == new TimeSpan(15, 55, 0) && !cn.Step(new DateTime(2024, 11, 27, 15, 50, 0), true).exit && cn.Step(new DateTime(2024, 11, 27, 15, 55, 0), true).exit, "normal day exit 15:55");
		var hol = Extracted.Cme.Rules.Get(new DateTime(2024, 7, 4));
		var ch = new A263Core(cfg); ch.Latch(new DateTime(2024, 7, 4, 9, 35, 0), 5000, 40, new DateTime(2024, 7, 3), true, hol.RthCloseEt, hol.NoRth);
		Check(hol.NoRth && !ch.HasSignal && ch.Suffix().Contains("noRth(1)"), "CME no-RTH day (Independence Day halt): no entry");
		foreach (var hd in new[] { new DateTime(2024, 11, 28), new DateTime(2024, 1, 15), new DateTime(2024, 5, 27) })
		{
			var hr = Extracted.Cme.Rules.Get(hd); var hc = new A263Core(cfg); hc.Latch(hd.AddHours(9).AddMinutes(35), 5000, 40, hd.AddDays(-1), true, hr.RthCloseEt, hr.NoRth);
			Check(hr.NoRth && !hc.HasSignal && !hc.Step(hd.AddHours(10), false).sendStop, "halt day no entry " + hd.ToString("yyyy-MM-dd"));
		}
		var half = Extracted.Cme.Rules.Get(new DateTime(2024, 11, 29)); var hcore = new A263Core(cfg);
		hcore.Latch(new DateTime(2024, 11, 29, 9, 35, 0), 5000, 40, new DateTime(2024, 11, 27), true, half.RthCloseEt, half.NoRth);
		Check(!half.NoRth && hcore.HasSignal && hcore.Step(new DateTime(2024, 11, 29, 10, 0, 0), false).sendStop && hcore.ExitAt == new TimeSpan(13, 10, 0), "half day (not a halt) still trades, exit 13:10");
		// DST: exchange wall-clock windows are the same on either side of the March/November switches
		foreach (var day in new[] { new DateTime(2024, 3, 8), new DateTime(2024, 3, 11), new DateTime(2024, 11, 1), new DateTime(2024, 11, 4) })
		{
			var c = new A263Core(cfg); Check(c.IsLatchBar(day.AddHours(9).AddMinutes(35)) && !c.IsLatchBar(day.AddHours(9).AddMinutes(30)), "DST: latch on the 09:35 close " + day.ToString("MM-dd"));
			c.Latch(day.AddHours(9).AddMinutes(35), 5000, 40, day.AddDays(-3), true);
			Check(c.Step(day.AddHours(15).AddMinutes(25), false).sendStop && !c.Step(day.AddHours(15).AddMinutes(30), false).sendStop && c.Step(day.AddHours(15).AddMinutes(55), true).exit, "DST: windows hold " + day.ToString("MM-dd"));
		}
	}

	// ---------------- ruling 3: no-usable-regime days trade in G and A; per-arm counts ----------------
	static void NoUsableDaysAndCounts()
	{
		var days = Market(1300, 13);
		foreach (var arm in new[] { ERegimeGateArm.U, ERegimeGateArm.G, ERegimeGateArm.A })
		{
			var d = Decisions(days, arm, new RegimeConfig()); var c = new RegimeGateCounts(); foreach (var x in d) c.Add(x);
			Check(c.sessions == days.Count && c.usable + c.warmup + c.invalid + c.fallback == c.sessions, arm + ": counts add up " + c.Suffix());
			Check(d.Where(x => !x.usable).All(x => x.allow && !x.wouldBlock), arm + ": every no-usable day is allowed");
			Check(c.warmup > 0, arm + ": warm-up days counted (" + c.warmup + ")");
			Check(arm == ERegimeGateArm.A ? c.fallback == 0 : c.fallback > 0, arm + ": ATR-fallback days counted separately (" + c.fallback + ")");
			Check(d.Count(x => x.reason == ERegimeGateReason.Fallback) == c.fallback && d.Where(x => x.reason == ERegimeGateReason.Fallback).All(x => x.fb && !x.usable && x.allow), arm + ": why(Fallback) days are fallback, allowed");
			Console.WriteLine("arm " + arm + c.Suffix());
		}
		DateTime t = new DateTime(2020, 3, 10);
		Check(RegimeGate.Decide(ERegimeGateArm.G, RegimeState.Neutral(3), t, -1).reason == ERegimeGateReason.Warmup, "neutral state = warm-up");
		Check(RegimeGate.Decide(ERegimeGateArm.G, St(true, 1, t.AddDays(-1), true), t, 1).reason == ERegimeGateReason.Fallback, "G fallback = Fallback");
		Check(RegimeGate.Decide(ERegimeGateArm.G, St(true, 1, t, false), t, 1).reason == ERegimeGateReason.Invalid, "AsOf == t = invalid label");
		Check(RegimeGate.Decide(ERegimeGateArm.A, St(true, 1, t.AddDays(-1), true), t, 1).reason == ERegimeGateReason.Ok, "A fallback = ok");
		var l = RegimeGate.Line(RegimeGate.Decide(ERegimeGateArm.G, St(true, 1, t.AddDays(-1), true), t, 1), true, "h", 1);
		Check(l.Contains(" why(Fallback) "), "REGGATE why(Fallback)");
		foreach (var arm in new[] { ERegimeGateArm.U, ERegimeGateArm.G, ERegimeGateArm.A })
		{
			var ob = RegimeGate.Decide(arm, St(true, 3, t.AddDays(-1), false), t, 1);
			Check(!ob.usable && !ob.wouldBlock && ob.allow && ob.reason == ERegimeGateReason.Invalid, arm + ": label >= Probs.Length = not usable (invalid)");
		}
		Check(RegimeGate.Decide(ERegimeGateArm.G, St(true, 1, t, true), t, 1).reason == ERegimeGateReason.Invalid, "fallback with AsOf == t = invalid (lookahead first)");
		// union check (ruling a): the G no-usable set = warm-up + invalid + fallback; A's is warm-up + invalid; the offline view drops the union
		var days2 = Market(1300, 13);
		var gD = Decisions(days2, ERegimeGateArm.G, new RegimeConfig()); var aD = Decisions(days2, ERegimeGateArm.A, new RegimeConfig());
		var union = new HashSet<DateTime>(gD.Where(x => !x.usable).Select(x => x.session).Concat(aD.Where(x => !x.usable).Select(x => x.session)));
		Check(union.Count == gD.Count(x => !x.usable) && aD.Where(x => !x.usable).All(x => union.Contains(x.session)), "A's no-usable days are a subset of G's (union = G's set): " + union.Count);
		var gDrop = new HashSet<DateTime>(gD.Select(x => x.session).Where(union.Contains)); var aDrop = new HashSet<DateTime>(aD.Select(x => x.session).Where(union.Contains));
		Check(gD.Select(x => x.session).SequenceEqual(aD.Select(x => x.session)) && gDrop.SetEquals(aDrop) && gDrop.SetEquals(union), "offline union drop removes the same day set from G and A: " + gDrop.Count);
		// ruling b: noRth counted; half days are not noRth
		foreach (var arm in new[] { ERegimeGateArm.U, ERegimeGateArm.G, ERegimeGateArm.A })
		{
			var cnt = new RegimeGateCounts(); var ok = RegimeGate.Decide(arm, St(true, 0, t.AddDays(-1), false), t, 0);
			foreach (var day in new[] { new DateTime(2024, 7, 3), new DateTime(2024, 7, 4), new DateTime(2024, 11, 28), new DateTime(2024, 11, 29), new DateTime(2024, 12, 24), new DateTime(2024, 1, 15) })
				cnt.Add(ok, Extracted.Cme.Rules.Get(day).NoRth);
			Check(cnt.noRth == 3 && cnt.sessions == 6, arm + ": noRth counted on halt days only (Jul 4, Thanksgiving, MLK), not half days: " + cnt.Suffix());
		}
		var cc = new RegimeGateCounts(); cc.Add(RegimeGate.Decide(ERegimeGateArm.G, RegimeState.Neutral(3), t, -1)); cc.Add(RegimeGate.Decide(ERegimeGateArm.G, St(true, 2, t.AddDays(-1), false), t, 2));
		Check(cc.Suffix() == " nSess(2) nUsable(1) nWarm(1) nInv(0) nFb(0) nBlock(1) nNoRth(0) nAtrNaN(0) nRollAtrNaN(0)", "counts suffix: " + cc.Suffix());
		var ca = new RegimeGateCounts(); ca.AddAtr(true, false); ca.AddAtr(true, true); ca.AddAtr(true, true); ca.AddAtr(false, true); ca.AddAtr(true, true, true);
		Check(ca.atrNaN == 3 && ca.rollAtrNaN == 2 && ca.Suffix().EndsWith(" nAtrNaN(3) nRollAtrNaN(2)"), "rulings (h): ATR NaN days counted, post-roll subset separate, no-RTH days excluded: " + ca.Suffix());
	}

	// ---------------- ruling 4: the label used on day D does not change when day D's bars are removed ----------------
	static void LabelIgnoresDayD()
	{
		var days = Market(1300, 17);
		foreach (var arm in new[] { ERegimeGateArm.G, ERegimeGateArm.A })
		{
			var full = Decisions(days, arm, new RegimeConfig()); bool ok = true; int checkedUsable = 0;
			foreach (int D in new[] { 300, 700, 1001, 1150, 1299 })
			{
				var cut = Decisions(days.Take(D).ToList(), arm, new RegimeConfig());   // day D and later never fed
				var rc = new RegimeConfig(); RegimeGate.ApplyArm(new RegimeGateConfig { arm = arm }, rc);
				var eng = new _RegimeEngine(); eng.config = rc; eng.Configure(); var terc = new AtrTercileTracker(rc.atrPctLookback, rc.K);
				for (int i = 0; i < D; i++) { var x = days[i]; eng.AddDay(x.date, x.close, x.hv20, x.hv63, x.high, x.low, x.atr, false); terc.AddDay(x.date, x.atr); }
				var dd = RegimeGate.Decide(arm, eng.State, days[D].date, terc.LastDate == days[D - 1].date ? terc.Tercile : -1);
				ok &= dd.label == full[D].label && dd.allow == full[D].allow && Same(dd.pFilt, full[D].pFilt) && dd.asOf == days[D - 1].date && dd.atrTercile == full[D].atrTercile && cut.Count == D;
				if (dd.usable) checkedUsable++;
			}
			Check(ok, arm + ": label on day D identical without day D's bars, AsOf = D-1");
			Check(checkedUsable >= 3, arm + ": checked on usable days (" + checkedUsable + ")");
		}
	}

	// re-pin only with a new trial (ruling 5: named-field hash). PR #46 review Q1: re-pinned for host.rollDetection/rollYears/rollDates/tradingDay
	// (was U=26b38655261cc0bf G=9c1da7b123ca9441 A=3d9814a5f264c353, the pre-roll Off builds; those builds are void, see specs/a2-63-rulings.md).
	const string PinnedU = "b442eb413f280db4", PinnedG = "bb2a59a5e74b3b4a", PinnedA = "8129399a3c7fffc2";
}
