using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PowerLanguage.Function;

// M-DVP PR-D7 (specs/m-dvp-spec.md rev 5.2 §12, DV-12): A2-33 hook. DvpA233.Sample (MC-free core) and an engine-driven replay of
// the host hook (first closed bar of each new tradingDayKey, t-1 snapshot, Rails(1) + Confluence(1) into a real _LevelStudy).
static partial class Program
{
	static LevelRef L(ELevelKind k, double p, bool cr = false, bool rw = false, double q = double.NaN)
	{ return new LevelRef { kind = k, price = p, tag = "T" + k + p, quality = q, crossesRoll = cr, isRollWindow = rw }; }

	static void A233Core()
	{
		var calls = new List<string>();
		Func<string, double, int, double, string, bool> add = (t, p, a, q, k) => { calls.Add(t + "|" + p + "|" + a + "|" + q + "|" + k); return true; };
		var src = new List<LevelRef> { L(ELevelKind.DvpPoc, 100, q: 2), L(ELevelKind.DvpVah, 101), L(ELevelKind.DvpVal, 99), L(ELevelKind.DvpHvn, 98),
			L(ELevelKind.DvpLvn, 97), L(ELevelKind.DvpSigma, 103), L(ELevelKind.DvpFib, 102), L(ELevelKind.DvpConf, 100.5, q: 3),
			L(ELevelKind.DvpSigma, double.NaN), L(ELevelKind.DvpPoc, 95, cr: true), L(ELevelKind.DvpVah, 96, rw: true) };
		int roll = 0, fib = 0;
		int n = DvpA233.Sample(src, add, ref roll, ref fib);
		Check(n == 7 && roll == 2 && fib == 1 && calls.Count == 7, "DV-12.1/12.2 Sample: 7 G0-d levels added, DvpFib not a G0-d type, crossesRoll/isRollWindow filtered (" + n + "," + roll + "," + fib + ")");
		Check(calls[0] == "DvpPoc|100|1|2|TDvpPoc100" && calls.Any(c => c.StartsWith("DvpConf|100.5|1|3|")), "DV-12.1 add(StudyType, price, ageSessions 1, quality, key = tag)");
		Check(new[] { "DvpPoc", "DvpVah", "DvpVal", "DvpHvn", "DvpLvn", "DvpSigma", "DvpConf" }.All(t => calls.Any(c => c.StartsWith(t + "|"))), "DV-12.1 all seven G0-d type strings");
		int n0 = DvpA233.Sample(src, (t, p, a, q, k) => false, ref roll, ref fib);
		Check(n0 == 0 && DvpA233.Sample(null, add, ref roll, ref fib) == 0, "Sample counts only levels add accepts; null-safe");
	}

	// Host replay: Data1 rig on the synthetic Globex series (sessions cross midnight; one half-day; a roll). On each closed bar, the
	// LevelStudy steps (BeginSession on a new tradingDayKey, then Bar) and the hook fires once per new tradingDayKey with the t-1
	// rails + confluence. Checks one sampling per session (half-day included), types, roll-window filtering, frozen price.
	static void A233Replay()
	{
		const int roll = 1800; bool[] hd; RingBar[] s = Series(3000, 33, new[] { roll }, new[] { 10 }, out hd);
		var rig = new Rig(Cfg(240), true, false);
		var ls = new _LevelStudy { config = new LevelStudyConfig { enabled = true, reactionBars = 3, sigmaBars = 10 }, tickSize = 0.25 };
		Check(ls.Configure() == null, "A2-33 replay: configure");
		var rw = new _RollWindow();
		var rails = new List<LevelRef>(); var pool = new List<LevelRef>();
		DateTime lsDay = DateTime.MinValue, hookDay = DateTime.MinValue;
		int sessions = 0, sampled = 0, hookCalls = 0, halfSampled = 0, added = 0, rollSkip = 0, fibSkip = 0, priceBad = 0, createdEval = 0, keyBad = 0;
		var types = new HashSet<string>();
		Func<string, double, int, double, string, bool> add = (t, p, ag, q, k) => { bool ok = ls.AddLevel(t, p, ag, q, k); if (ok) types.Add(t); return ok; };
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], hd[i]);
			DateTime day = s[i].tradingDay;
			if (day != lsDay) { lsDay = day; ls.BeginSession(day); sessions++; }
			rw.Observe(day, s[i].rollBar);
			ls.Bar(s[i].o, s[i].h, s[i].l, s[i].c, s[i].v);
			if (day == hookDay) continue;
			hookDay = day; hookCalls++;
			DvpSnapshot sn = rig.e.Snapshot(1, rig.ring, 1); DvpRails r = sn != null ? rig.e.RailsOf(1, rig.ring, 1) : null;
			if (r == null) continue;
			rails.Clear(); r.Publish(rails, pool, 1, sn.tPrev, sn.t0, rw.IsRollWindow(rig.ring[1].tradingDay));
			foreach (var x in rails) { bool hit = false; for (int j = 0; j < r.Count; j++) if (r.Items[j].price == x.price) hit = true; if (!double.IsNaN(x.price) && !hit) priceBad++; }
			int ev0 = ls.Events.Count;
			int before = added; added += DvpA233.Sample(rails, add, ref rollSkip, ref fibSkip);
			for (int j = ev0; j < ls.Events.Count; j++) { var ev = ls.Events[j]; if (ev.touched || ev.barsSeen != 0) createdEval++; if (ev.day.ToString("yyyy-MM-dd") != DvpCustomSignalLineDay(day)) keyBad++; }
			if (added > before) { sampled++; if (hd[i]) halfSampled++; }
		}
		Check(hookCalls == sessions && sampled > sessions / 2 && halfSampled == 1, "DV-12.1 / DV-T29 one A2-33 sampling per tradingDayKey (" + hookCalls + "/" + sessions + "), half-day sampled once (" + halfSampled + ")");
		Check(rollSkip > 0 && types.All(t => t != "DvpFib") && types.Contains("DvpPoc") && types.Contains("DvpSigma"), "DV-12.2 roll-window levels not added (" + rollSkip + "), types " + string.Join(",", types.OrderBy(t => t)));
		Check(priceBad == 0, "DV-12.1 price = the t-1 snapshot rail price (frozen at creation)");
		Check(createdEval == 0 && ls.Events.All(ev => ev.touchBar != 1), "DV-12.1 (e) a DVP-fed level is never evaluated (touch / outcome) on its creation bar");
		Check(keyBad == 0, "DV-12 (a) journal tradingDayKey == LevelStudy CSV day for every DVP-fed level");
		List<LevelTypeResult> res = ls.Finish();
		Check(res.Count > 0 && res.All(x => x.type.StartsWith("Dvp")), "A2-33 Finish: per-type results for DVP types (" + res.Count + ")");

		string host = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		int a = host.IndexOf("if (m_levelStudy != null) stepLevelStudy();", StringComparison.Ordinal), b = host.IndexOf("if (m_levelStudy != null && m_dvp != null) dvpLevelStudyStep();", StringComparison.Ordinal);
		Check(a > 0 && b > a && b - a < 120, "DV-12.1 host: the hook runs right after stepLevelStudy (BeginSession first), only with LevelStudy and DVP on");
		int h = host.IndexOf("private void dvpLevelStudyStep()", StringComparison.Ordinal);
		string body = host.Substring(h, host.IndexOf("\n\t\t}\n", h, StringComparison.Ordinal) - h);
		Check(body.Contains("if (Bars.Status != EBarState.Close || Bars.CurrentBar == m_dvpLsBar) return;") && body.Contains("DateTime day = tradingDayKey(Bars.Time[0]);")
			&& body.Contains("if (day == m_dvpLsDay) return;") && body.Contains("DvpA233.Sample(dvpRails(1), m_dvpLsAdd") && body.Contains("DvpA233.Sample(dvpConfluence(1), m_dvpLsAdd")
			&& body.Contains("todHalfDay(day)") && body.Contains("if (m_dvpLsAdd == null) m_dvpLsAdd = levelStudyAdd;"), "DV-12.1 host: first closed bar of a new tradingDayKey, Rails(1) + Confluence(1), halfDay flag, lazy delegate");
		Check(host.Contains("m_dvpLsDay = DateTime.MinValue; m_dvpLsBar = -1;"), "DV-12 host state reset in startDvp");
	}

	static string DvpCustomSignalLineDay(DateTime d) { string l = DvpA233.Line("r", d, false, 0, 0, 0, 0); int i = l.IndexOf("tradingDayKey(") + 14; return l.Substring(i, 10); }

	// (e) forced: the creation bar straddles the level price; the level must stay untouched until the next bar.
	static void A233CreationBar()
	{
		var ls = new _LevelStudy { config = new LevelStudyConfig { enabled = true, reactionBars = 3, sigmaBars = 2, controls = 2 }, tickSize = 0.25 };
		var d0 = new DateTime(2024, 1, 2); ls.BeginSession(d0);
		for (int k = 0; k < 5; k++) ls.Bar(100, 101 + k % 2, 99, 100 + k % 2, 10);
		var d1 = d0.AddDays(1); ls.BeginSession(d1);
		ls.Bar(100, 102, 98, 100, 10);   // creation bar: fed first (stepLevelStudy), range covers 100.5
		bool ok = ls.AddLevel("DvpPoc", 100.5, 1, double.NaN, "DvpPoc@t");
		var lv = ls.Events.First(e => !e.control && e.key == "DvpPoc@t");
		Check(ok && !lv.touched && lv.barsSeen == 0, "DV-12.1 (e) creation-bar range covers the level: not touched on it");
		ls.Bar(100, 101, 100, 100.75, 10);
		Check(lv.touched && lv.touchBar == 2 && lv.refPrice == 100, "DV-12.1 (e) first evaluation on the next bar; reference = creation-bar close (" + lv.touchBar + ", " + lv.refPrice + ")");
		string line = DvpA233.Line("rid", d1, true, 1, 0, 0, 0);
		Check(line.Contains("tradingDayKey(2024-01-03)") && line.Contains("halfDay(1)"), "DV-12 (a) journal line carries tradingDayKey in the CSV day format");
		string host = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		Check(host.Contains("if (m_levelStudyLastBar != Bars.CurrentBar) return;"), "DV-12.1 (e) host guard: hook only after the bar was fed to the study");
	}

	static void D7Tests() { A233Core(); A233Replay(); A233CreationBar(); }
}
