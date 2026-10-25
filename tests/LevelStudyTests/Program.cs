using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for _LevelStudy (trading-idea-catalogue.md A2-33).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-9) { return Math.Abs(a - b) < eps; }
	static readonly DateTime D0 = new DateTime(2024, 3, 11);

	static LevelEvent E(string type, int no, bool ctl, double reaction, int age = 1, double q = double.NaN, string key = null, int day = 0, bool touched = true)
	{ return new LevelEvent { type = type, levelNo = no, control = ctl, touched = touched, reaction = touched ? reaction : double.NaN, asymmetry = 0, volAttraction = 1, age = age, quality = q, key = key ?? (type + no), day = D0.AddDays(day) }; }

	static void Main()
	{
		var cfg = new LevelStudyConfig();
		Check(cfg.Validate() == null && !cfg.enabled && cfg.controls == 20 && cfg.distLo == 0.9 && cfg.distHi == 1.1 && cfg.maxAge == 15, "defaults (off, 20 controls at 0.9-1.1x, ages 1-15)");
		var bad = new LevelStudyConfig { controls = 0, distLo = 1.2, reactionBars = 0 }.Validate();
		Check(bad != null && bad.Contains("controls=0") && bad.Contains("distLo") && bad.Contains("reactionBars=0"), "Validate names every bad value");

		// ---- recording ----
		var ls = new _LevelStudy { config = new LevelStudyConfig { enabled = true, reactionBars = 3, sigmaBars = 10 }, tickSize = 0.25 };
		Check(ls.Configure() == null, "configure");
		ls.BeginSession(D0);
		Check(!ls.AddLevel("POC", 100, 1), "no level before sigma exists (needs a prior session)");
		var rnd = new Random(1); double px = 100;
		for (int i = 0; i < 20; i++) { double c = px + (i % 2 == 0 ? 0.5 : -0.5); ls.Bar(px, Math.Max(px, c), Math.Min(px, c), c, 100); px = c; }
		ls.BeginSession(D0.AddDays(1));
		ls.Bar(100, 100.25, 99.75, 100, 100);   // reference close 100
		Check(ls.AddLevel("VAH", 102, 1, 0.7) && !ls.AddLevel("VAH", 102, 0) && !ls.AddLevel("VAH", 102, 16), "ages 1..15 only");
		var ev = ls.Events.Where(e => e.day == D0.AddDays(1)).ToList();
		var lv = ev.Single(e => !e.control); var ctl = ev.Where(e => e.control).ToList();
		Check(ctl.Count == 20 && ctl.All(c => c.price >= 100 + 2 * 0.9 - 0.125 && c.price <= 100 + 2 * 1.1 + 0.125 && Math.Abs(c.price / 0.25 - Math.Round(c.price / 0.25)) < 1e-9), "20 controls at 0.9-1.1x distance, tick-rounded");
		double sigma = lv.sigma; Check(Near(sigma, Math.Sqrt(10 * 0.25 / 9) * Math.Sqrt(3), 1e-9), $"sigma = stdev(1-bar changes, prior session) x sqrt(K) ({sigma})");
		ls.Bar(100, 101, 99.5, 101, 100);
		ls.Bar(101, 102.25, 100.75, 101.5, 300);   // touches 102 from below
		Check(lv.touched && lv.fromAbove == -1 && lv.touchBar == 3, "touch detection and approach side");
		ls.Bar(101.5, 101.75, 99, 99.5, 100);      // bounce down 3.0
		ls.Bar(99.5, 99.75, 98, 98.5, 100);
		ls.Bar(98.5, 99, 97, 97, 100);             // beyond K=3 bars: ignored
		Check(lv.barsSeen == 3 && Near(lv.reaction, 4.0 / sigma) && Near(lv.asymmetry, (4.0 - 0.25) / sigma), $"reaction max|P-level|/sigma over K bars, asymmetry ({lv.reaction * sigma}, {lv.asymmetry * sigma})");
		var sw = new StringWriter(); ls.Finish(); ls.WriteCsv(sw);
		Check(sw.ToString().Split('\n')[0].StartsWith("day,type,key,age") && sw.ToString().Contains("2024-03-12,VAH"), "CSV");
		Check(Near(lv.volAttraction, (300 + 100 + 100) / 3.0 / 100.0, 1e-9), $"volume attraction = mean vol after touch / session mean before ({lv.volAttraction})");
		var ls2 = new _LevelStudy { config = new LevelStudyConfig { enabled = true, from = D0.AddDays(5) } }; ls2.Configure();
		ls2.BeginSession(D0); for (int i = 0; i < 70; i++) ls2.Bar(100, 101, 99, 100 + i % 2, 1); ls2.BeginSession(D0.AddDays(1)); ls2.Bar(100, 101, 99, 100, 1);
		Check(!ls2.AddLevel("POC", 101, 1), "sessions outside the decision window are skipped");

		// ---- analysis ----
		var c2 = new LevelStudyConfig { maxAge = 4 };
		var list = new List<LevelEvent>(); var r = new Random(7); int no = 0;
		for (int i = 0; i < 60; i++)
		{
			int age = 1 + i % 4; no++;
			list.Add(E("REAL", no, false, 3.0 - 0.5 * (age - 1) + r.NextDouble() * 0.2, age, i)); for (int k = 0; k < 5; k++) list.Add(E("REAL", no, true, 1.0 + r.NextDouble() * 0.2, age, i));
			no++; double b = 1 + r.NextDouble(); list.Add(E("FAKE", no, false, b, age)); for (int k = 0; k < 5; k++) list.Add(E("FAKE", no, true, 1 + r.NextDouble(), age));
		}
		var res = _LevelStudy.Analyse(list, c2);
		var real = res.Single(x => x.type == "REAL"); var fake = res.Single(x => x.type == "FAKE");
		Check(real.edge > 1 && real.t > 3 && real.pHolm < 0.05, $"real level type has edge ({real.edge:0.00}, t {real.t:0.0})");
		Check(Math.Abs(fake.t) < 2 || fake.verdict.StartsWith("drop"), $"fake type dropped ({fake.t:0.00}: {fake.verdict})");
		Check(!double.IsNaN(real.halfLife) && real.halfLife > 2 && real.halfLife < 4 && real.verdict.StartsWith("keep up to age"), $"decaying edge -> half-life ({real.halfLife:0.00}) and age cap ({real.verdict})");
		Check(real.nByAge.All(n => n == 15), "edge by age buckets");

		// half-life helper, Holm, KM, Phi, verdicts
		Check(Near(_LevelStudy.HalfLife(new[] { 2.0, 1.5, 0.5 }, new[] { 1, 1, 1 }), 2.5) && double.IsNaN(_LevelStudy.HalfLife(new[] { 2.0, 1.9 }, new[] { 1, 1 })), "half-life interpolation");
		var h = new List<LevelTypeResult> { new LevelTypeResult { p = 0.01 }, new LevelTypeResult { p = 0.04 }, new LevelTypeResult { p = 0.03 } };
		_LevelStudy.Holm(h);
		Check(Near(h[0].pHolm, 0.03) && Near(h[2].pHolm, 0.06) && Near(h[1].pHolm, 0.06), "Holm step-down (monotone)");
		var km = _LevelStudy.KaplanMeier(new List<KeyValuePair<double, bool>> { new KeyValuePair<double, bool>(1, true), new KeyValuePair<double, bool>(2, false), new KeyValuePair<double, bool>(3, true), new KeyValuePair<double, bool>(3, true) }, 3);
		Check(Near(km[0], 0.75) && Near(km[1], 0.75) && Near(km[2], 0), "Kaplan-Meier with censoring");
		Check(Near(_LevelStudy.Phi(0), 0.5, 1e-7) && Near(_LevelStudy.Phi(1.96), 0.975, 1e-3), "normal CDF");
		var lifeEv = new List<LevelEvent> { E("X", 1, false, 2, 1, key: "k1"), E("X", 1, true, 1, 1, key: "k1"), E("X", 2, false, 0.5, 2, key: "k1", day: 1), E("X", 2, true, 1, 2, key: "k1", day: 1),
			E("X", 3, false, 2, 1, key: "k2"), E("X", 3, true, 1, 1, key: "k2") };
		var kx = _LevelStudy.Analyse(lifeEv, new LevelStudyConfig { maxAge = 3 }).Single();
		Check(Near(kx.kmSurvival[0], 1) && Near(kx.kmSurvival[1], 0) && kx.kmMedianAge == 2, $"level death at the first weak reaction ({string.Join(",", kx.kmSurvival)})");
		var tq = new LevelTypeResult { t = 1.5, edge = 0.1, tByTercile = new[] { 0.1, 0.5, 3.0 } };
		Check(_LevelStudy.Verdict(tq) == "trade top-quality tercile only", "top tercile only verdict");
		Check(_LevelStudy.Verdict(new LevelTypeResult()) == "insufficient data", "no data");
		Check(real.edgeByTercile.All(x => !double.IsNaN(x)), "terciles computed when quality given");
		Check(_LevelStudy.Summary(real).StartsWith("A2-33 REAL: levels(60)"), "summary line");

		DvpTypes();
		Console.WriteLine($"LevelStudyTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}

	// M-DVP PR-D7 (DV-12): the A2-33 harness takes the DVP G0-d type strings (LevelRef.StudyType of the appended ELevelKinds) as-is,
	// one result per type, keys = DVP tags, quality = families (terciles).
	static void DvpTypes()
	{
		var kinds = new[] { ELevelKind.DvpPoc, ELevelKind.DvpVah, ELevelKind.DvpVal, ELevelKind.DvpHvn, ELevelKind.DvpLvn, ELevelKind.DvpSigma, ELevelKind.DvpConf };
		string[] want = { "DvpPoc", "DvpVah", "DvpVal", "DvpHvn", "DvpLvn", "DvpSigma", "DvpConf" };
		Check(kinds.Select(k => new LevelRef { kind = k }.StudyType).SequenceEqual(want), "DVP StudyType strings DvpPoc/Vah/Val/Hvn/Lvn/Sigma/Conf");
		var ls = new _LevelStudy { config = new LevelStudyConfig { enabled = true, reactionBars = 3, sigmaBars = 10 }, tickSize = 0.25 };
		Check(ls.Configure() == null, "DVP: configure");
		double px = 4000; int added = 0;
		for (int d = 0; d < 6; d++)
		{
			ls.BeginSession(D0.AddDays(d));
			for (int i = 0; i < 12; i++)
			{
				double c = px + (i % 3 == 0 ? 1.0 : -0.5);
				ls.Bar(px, Math.Max(px, c) + 0.25, Math.Min(px, c) - 0.25, c, 100);
				px = c;
				if (i == 0 && d > 0)
					for (int k = 0; k < want.Length; k++)
						if (ls.AddLevel(want[k], px + (k - 3) * 1.25 + 0.5, 1, 1 + k % 3, "DVP:D1:" + D0.AddDays(d).ToString("yyyyMMdd") + ":" + want[k])) added++;
			}
		}
		List<LevelTypeResult> res = ls.Finish();
		var sw = new StringWriter(); ls.WriteCsv(sw); string csv = sw.ToString();
		Check(added == 5 * want.Length, "DVP: every type added once per session (" + added + ")");
		Check(want.All(t => res.Any(r => r.type == t && r.levels == 5)) && !res.Any(r => r.type == "DvpFib"), "DVP: one A2-33 result per G0-d type, 5 levels each, no DvpFib");
		Check(csv.Contains(",DvpConf,DVP:D1:") && csv.Contains(",DvpSigma,"), "DVP: CSV carries the DVP type and tag key");
	}
}
