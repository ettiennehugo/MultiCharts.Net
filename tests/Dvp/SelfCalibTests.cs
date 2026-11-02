using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PowerLanguage.Function;

// M-DVP PR-D9 (specs/m-dvp-spec.md rev 6, DV-3.10): DV-T30..T38 for the online degree self-calibration, on the MC-free
// _DvpEngine / DvpStream driven the way _CustomSignal.stepDvp drives it (ring.Add, then OnBar on the closed bar).
// Small configuration (winLen 30, projBars 10, calibStep 5, calibLen 40, calibMinScored 20) on a Globex-like series of
// 9 bars per weekday session (Sunday-evening open, tradingDay = the next date), whose curvature regime changes every half
// month, so the best degree flips mid-month.
static partial class Program
{
	static DvpConfig SCfg(int refitEvery = 1, bool journalFits = false)
	{
		var c = Cfg(30, refitEvery, journalFits);
		c.degMode = EDvpDegMode.SelfCalibrate; c.projBars = 10; c.calibStep = 5; c.calibLen = 40; c.calibMinScored = 20;
		return c;
	}

	// Weekday sessions only (tradingDay Mon..Fri): the Monday session opens Sunday 18:00. Bars every 150 minutes, 9 per session
	// (the last at 16:30 on tradingDay). skipTd drops whole sessions. bar numbers start at 1 and are contiguous.
	static RingBar[] CalSeries(int calendarDays, int seed, int[] rolls, Func<DateTime, bool> skipTd, DateTime? firstSession = null)
	{
		var rng = new Random(seed);
		Func<double> g = () => { double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble(); return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2); };
		var list = new List<RingBar>(); var rollSet = new HashSet<int>(rolls ?? new int[0]);
		double p = 4000, prevC = 4000, ph = 0;
		DateTime sess = firstSession ?? new DateTime(2024, 1, 1, 18, 0, 0);
		for (int d = 0; d < calendarDays; d++, sess = sess.AddDays(1))
		{
			DateTime td = sess.AddHours(6).Date;
			if (td.DayOfWeek == DayOfWeek.Saturday || td.DayOfWeek == DayOfWeek.Sunday) continue;
			if (skipTd != null && skipTd(td)) continue;
			int regime = (td.Month * 2 + (td.Day >= 15 ? 1 : 0)) % 3;
			double period = regime == 0 ? 10 : regime == 1 ? 45 : 400, amp = regime == 0 ? 6 : regime == 1 ? 4 : 1.5;
			for (int k = 0; k < 9; k++)
			{
				int i = list.Count;
				ph += 2 * Math.PI / period;
				p += amp * Math.Sin(ph) + 1.2 * g();
				bool roll = rollSet.Contains(i);
				if (roll) { p += 35; prevC += 35; }
				double o = Math.Round(prevC * 4) / 4, c = Math.Round(p * 4) / 4;
				double h = Math.Max(o, c) + Math.Round(Math.Abs(g()) * 4) / 4, l = Math.Min(o, c) - Math.Round(Math.Abs(g()) * 4) / 4;
				double v = Math.Round(500 + 400 * Math.Abs(g())), up = Math.Round(v * 0.5);
				list.Add(new RingBar { bar = i + 1, time = sess.AddMinutes(150 * (k + 1)), tradingDay = td, o = o, h = h, l = l, c = c, v = v, up = up, down = v - up, rth = false, rollBar = roll });
				prevC = c;
			}
		}
		return list.ToArray();
	}

	sealed class CalTrace
	{
		public readonly List<int> deg = new List<int>();
		public readonly List<bool> warm = new List<bool>(), tradeable = new List<bool>(), recheck = new List<bool>();
		public readonly List<long> sampled = new List<long>(), scored = new List<long>(), gridSkipped = new List<long>(), rollDropped = new List<long>(), added = new List<long>();
		public readonly List<int> count = new List<int>(), pendingBefore = new List<int>();
		public Rig rig;
	}

	static CalTrace RunCal(RingBar[] s, DvpConfig c, bool rollDetect = true, bool journal = false, int upto = -1, Action<Rig, int> each = null)
	{
		var t = new CalTrace(); var rig = new Rig(c, rollDetect, journal); t.rig = rig;
		int end = upto < 0 ? s.Length - 1 : upto;
		long rc = 0;
		for (int i = 0; i <= end; i++)
		{
			DvpCalib cal = rig.S.Calib;
			t.pendingBefore.Add(cal == null ? 0 : cal.Pending);
			rig.Feed(s[i], false);
			DvpStream st = rig.S; cal = st.Calib; DvpSelfCalib sc = st.Self;
			t.deg.Add(st.CurrentDegree); t.warm.Add(st.Warming); t.tradeable.Add(rig.e.Tradeable(1));
			long r = sc == null ? 0 : sc.Rechecks; t.recheck.Add(r != rc); rc = r;
			t.sampled.Add(cal == null ? 0 : cal.Sampled); t.scored.Add(cal == null ? 0 : cal.Scored); t.gridSkipped.Add(cal == null ? 0 : cal.GridSkipped);
			t.rollDropped.Add(cal == null ? 0 : cal.RollDropped); t.added.Add(sc == null ? 0 : sc.Added); t.count.Add(sc == null ? 0 : sc.Count);
			if (each != null) each(rig, i);
		}
		return t;
	}

	static string Path0(CalTrace t, int upto)
	{
		var sb = new StringBuilder();
		for (int i = 0; i <= upto && i < t.deg.Count; i++) sb.Append(t.deg[i]).Append(t.warm[i] ? 'w' : '.');
		return sb.ToString();
	}

	// DV-T30 no look-ahead.
	static void T30()
	{
		RingBar[] s = CalSeries(200, 30, null, null);
		DvpConfig c = SCfg();
		CalTrace t = RunCal(s, c);
		int H = c.projBars, n = c.winLen, step = c.calibStep;
		// (a) the window sampled at s is scored on bar s + H, never earlier; samples only on grid positions
		var sampledAt = new List<int>(); bool gridOk = true, timingOk = true;
		for (int i = 0; i < s.Length; i++)
		{
			long prev = i == 0 ? 0 : t.sampled[i - 1];
			if (t.sampled[i] != prev) { sampledAt.Add(i); if (i % step != 0 || i < n - 1 || t.sampled[i] - prev != 1) gridOk = false; }
			int due = sampledAt.Count(x => x + H <= i);
			if (t.scored[i] != due || t.added[i] != due) timingOk = false;
		}
		Check(gridOk && timingOk && sampledAt.Count > 100 && t.rig.S.Calib.RollDropped == 0 && t.rig.S.Calib.BadDropped == 0,
			"DV-T30a a window sampled at s is scored on bar s + H exactly (never earlier), samples only on grid positions (" + sampledAt.Count + " samples)");
		// (b) truncation at 200 random t
		var rng = new Random(300); int[] ts = Pick(rng, 0, s.Length - 1, 200); int bad = 0;
		string full = Path0(t, s.Length - 1);
		foreach (int tt in ts)
		{
			CalTrace u = RunCal(s, c, true, false, tt);
			if (Path0(u, tt) != full.Substring(0, 2 * (tt + 1))) bad++;
		}
		bool switched = t.rig.S.Self.Changes >= 1 && !t.rig.S.Warming;
		Check(bad == 0 && ts.Length == 200 && switched, "DV-T30b truncated runs at 200 random t give the full-series degree path up to t (" + bad + " differ, changes " + t.rig.S.Self.Changes + ")");
		// (c) permuting bars > t leaves the degree in effect at every bar <= t unchanged
		int badP = 0, differs = 0;
		foreach (int tt in Pick(new Random(301), 50, s.Length - 50, 20))
		{
			var p = (RingBar[])s.Clone();
			var tail = Enumerable.Range(tt + 1, s.Length - tt - 1).ToArray(); var perm = tail.OrderBy(_ => rng.Next()).ToArray();
			for (int k = 0; k < tail.Length; k++)
			{
				RingBar src = s[perm[k]], dst = s[tail[k]];
				p[tail[k]] = new RingBar { bar = dst.bar, time = dst.time, tradingDay = dst.tradingDay, o = src.o, h = src.h, l = src.l, c = src.c, v = src.v, up = src.up, down = src.down, rth = dst.rth, rollBar = dst.rollBar };
			}
			CalTrace u = RunCal(p, c);
			string pp = Path0(u, s.Length - 1);
			if (pp.Substring(0, 2 * (tt + 1)) != full.Substring(0, 2 * (tt + 1))) badP++;
			if (pp != full) differs++;
		}
		Check(badP == 0, "DV-T30c permuting bars > t leaves the degree path up to t unchanged (20 cuts, " + differs + " paths differ after t)");
		// (d) an entry whose horizon is incomplete at the end of the data is never scored
		int last = s.Length - 1, open = sampledAt.Count(x => x + H > last);
		DvpCalib cal = t.rig.S.Calib;
		Check(open > 0 && cal.Pending == open && cal.Scored == cal.Sampled - open && t.rig.S.Self.Added == cal.Scored,
			"DV-T30d " + open + " horizons incomplete at the end stay pending and are never scored");
	}

	// DV-T31 warm-up.
	static void T31()
	{
		RingBar[] s = CalSeries(200, 31, null, null);
		DvpConfig c = SCfg();
		CalTrace t = RunCal(s, c);
		DvpSelfCalib sc = t.rig.S.Self;
		int we = (int)sc.WarmEndPos, p20 = t.count.FindIndex(x => x >= c.calibMinScored);
		bool before = true, after = true;
		for (int i = 0; i < s.Length; i++)
		{
			if (i < we) { if (t.deg[i] != 1 || !t.warm[i] || t.tradeable[i] || sc.DegreeAt(i) != -1 && sc.DegreeAt(i) != 1) before = false; }
			else if (t.warm[i] || !t.tradeable[i]) after = false;
		}
		int firstRe = -1; for (int i = p20; i < s.Length; i++) if (t.recheck[i]) { firstRe = i; break; }
		Check(we > 0 && before && after, "DV-T31 before warm-up ends (bar " + we + "): d = 1, warming, Tradeable(1) false; after: not warming, tradeable");
		Check(p20 >= 0 && p20 < we && we == firstRe && !t.recheck.Skip(p20).Take(we - p20).Any(x => x) && s[p20].tradingDay.Month == s[we - 1].tradingDay.Month,
			"DV-T31 reaching calibMinScored mid-month (bar " + p20 + ") changes nothing until the next recheck (bar " + we + ")");
		var warns = t.rig.warn.Where(w => w.StartsWith("DVP DEGREE warming", StringComparison.Ordinal)).ToList();
		Check(warns.Count == 1 && warns[0] == "DVP DEGREE warming stream(Data1) winLen(30) minScored(20): d = 1, not tradeable until a monthly recheck with n >= 20",
			"DV-T31 exactly one DVP DEGREE warming Warning per calibrator (" + warns.Count + ")");
		// too short to warm up: 28 calendar days (January only), enough scored windows but no recheck
		RingBar[] sh = CalSeries(28, 31, null, null);
		CalTrace u = RunCal(sh, c, true, true);
		u.rig.e.Finish();
		string run = u.rig.journal.FirstOrDefault(l => l.StartsWith("DVP RUN", StringComparison.Ordinal)) ?? "";
		Check(u.rig.S.Self.Count >= c.calibMinScored && u.rig.S.Self.Rechecks == 0 && u.deg.All(d => d == 1) && u.warm.All(w => w) && run.Contains(",warmEnd=NaN,dFinal=1}"),
			"DV-T31 a series too short to warm up stays on d = 1 with warmEnd=NaN in DVP RUN (" + run.Substring(Math.Max(0, run.IndexOf("calib{", StringComparison.Ordinal))) + ")");
	}

	// DV-T32 monthly-only switching.
	static void T32()
	{
		RingBar[] s = CalSeries(200, 32, null, null);
		DvpConfig c = SCfg(); c.calibMarginAbs = 0; c.calibMarginRel = 0;
		CalTrace t = RunCal(s, c);
		int changesOff = 0, changes = 0, monthTurns = 0; bool reOk = true;
		for (int i = 1; i < s.Length; i++)
		{
			if (t.deg[i] != t.deg[i - 1]) { changes++; if (!t.recheck[i]) changesOff++; }
			bool turn = s[i].tradingDay.Year * 12 + s[i].tradingDay.Month != s[i - 1].tradingDay.Year * 12 + s[i - 1].tradingDay.Month;
			if (turn) monthTurns++;
			if (turn != t.recheck[i]) reOk = false;
		}
		// the best degree (argmin over the ring) flips between rechecks: candidates differ across rechecks and mid-month
		var cands = new HashSet<int>(); var sc = t.rig.S.Self;
		Check(changes >= 1 && changesOff == 0 && reOk && !t.recheck[0] && monthTurns == sc.Rechecks,
			"DV-T32 d changes only on the first closed bar of a new tradingDay month (" + changes + " changes, " + sc.Rechecks + " rechecks, " + changesOff + " off-recheck)");
		// mid-month flip of the best degree: recompute the ring argmin on every bar from a parallel calibrator copy
		int flipsMid = 0; int lastBest = -1;
		var rig = new Rig(c, true, false);
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], false);
			var x = rig.S.Self; var sco = new double[DvpFit.MaxDeg + 1];
			for (int d = 1; d <= DvpFit.MaxDeg; d++) sco[d] = x.Score(d);
			int b = x.Count > 0 ? DvpSelfCalib.Candidate(sco, x.DCap) : -1;
			if (b > 0) cands.Add(b);
			if (lastBest > 0 && b > 0 && b != lastBest && !t.recheck[i]) flipsMid++;
			if (b > 0) lastBest = b;
		}
		Check(flipsMid >= 1 && cands.Count >= 2, "DV-T32 the ring's best degree flips mid-month " + flipsMid + " times without a switch (" + cands.Count + " distinct candidates)");
		// Globex Sunday-evening session that opens a new month is the recheck bar: 2024-03-31 (Sunday) 18:00 opens tradingDay 2024-04-01
		int apr = Array.FindIndex(s, b => b.tradingDay == new DateTime(2024, 4, 1));
		Check(apr > 0 && t.recheck[apr] && s[apr].time.DayOfWeek == DayOfWeek.Sunday && s[apr].time.Month == 3 && !t.recheck[apr + 1],
			"DV-T32 the Sunday-evening Globex bar " + s[apr].time.ToString("yyyy-MM-dd HH:mm", CI) + " (tradingDay 2024-04-01) is the April recheck bar");
		// a month with no bars rechecks once, on the next bar
		RingBar[] sk = CalSeries(200, 32, null, td => td.Month == 3);
		CalTrace u = RunCal(sk, c);
		int firstApr = Array.FindIndex(sk, b => b.tradingDay.Month == 4);
		int reFebApr = 0; for (int i = Array.FindIndex(sk, b => b.tradingDay.Month == 2) + 1; i <= firstApr; i++) if (u.recheck[i]) reFebApr++;
		Check(firstApr > 0 && u.recheck[firstApr] && reFebApr == 1 && sk[firstApr - 1].tradingDay.Month == 2,
			"DV-T32 a month with no bars (March) rechecks once, on the next bar (" + sk[firstApr].time.ToString("yyyy-MM-dd HH:mm", CI) + ")");
		// the stream's first bar is never a recheck, even when it opens a month
		int feb = Array.FindIndex(s, b => b.tradingDay.Month == 2);
		var fromFeb = s.Skip(feb).ToArray();
		CalTrace v = RunCal(fromFeb, c);
		Check(!v.recheck[0] && v.rig.S.Self.Rechecks == monthTurns - 1 && !t.recheck[0], "DV-T32 the first bar is never a recheck (a stream starting on the first February bar)");
	}

	static DvpCoef Win(double[] covIn, double[] fwd, int top, int H)
	{
		var q = new DvpCoef(true); q.top = top;
		for (int k = 1; k <= top; k++) { q.CovInK[k] = covIn[k]; q.HitsK[k] = (int)Math.Round(fwd[k] * H); }
		return q;
	}

	// a calibrator with len windows of the given per-degree scores (covIn = fwd + score, fwd = 0.5), then one recheck
	static DvpSelfCalib Cal(double[] score, double mAbs, double mRel, DvpSelfCalib reuse = null, int month = 1)
	{
		const int H = 100;
		DvpSelfCalib sc = reuse ?? new DvpSelfCalib(20, 1, mAbs, mRel, 3, 16);
		var fwd = new[] { 0, 0.5, 0.5, 0.5 }; var ci = new double[4];
		for (int k = 1; k <= 3; k++) ci[k] = 0.5 + score[k];
		for (int i = 0; i < 20; i++) sc.Add(Win(ci, fwd, 3, H), H);
		sc.Step(new DateTime(2024, month, 10), 2 * month, new DateTime(2024, month, 10));
		sc.Step(new DateTime(2024, month + 1, 1), 2 * month + 1, new DateTime(2024, month + 1, 1));
		return sc;
	}

	// DV-T33 hysteresis.
	static void T33()
	{
		var abs = new[] { double.NaN, 0.1, 0.1 - 0.0099, double.NaN }; var abs2 = new[] { double.NaN, 0.1, 0.1 - 0.0101, double.NaN };
		var rel = new[] { double.NaN, 0.8, 0.8 - 0.0159, double.NaN }; var rel2 = new[] { double.NaN, 0.8, 0.8 - 0.0161, double.NaN };
		Check(!DvpSelfCalib.ShouldSwitch(1, 2, abs, 0.01, 0.02) && DvpSelfCalib.ShouldSwitch(1, 2, abs2, 0.01, 0.02),
			"DV-T33 absolute margin binds (score(c) 0.1: 0.0099 gap stays, 0.0101 switches)");
		Check(!DvpSelfCalib.ShouldSwitch(1, 2, rel, 0.01, 0.02) && DvpSelfCalib.ShouldSwitch(1, 2, rel2, 0.01, 0.02),
			"DV-T33 relative margin binds (score(c) 0.8: margin 0.016; 0.0159 stays, 0.0161 switches)");
		DvpSelfCalib a = Cal(new[] { 0, 0.1, 0.1 - 0.0099, 0.3 }, 0.01, 0.02), b = Cal(new[] { 0, 0.1, 0.1 - 0.0101, 0.3 }, 0.01, 0.02);
		DvpSelfCalib r = Cal(new[] { 0, 0.4, 0.4 - 0.0079, 0.6 }, 0.01, 0.02), r2 = Cal(new[] { 0, 0.4, 0.4 - 0.0081, 0.6 }, 0.0, 0.02);
		DvpSelfCalib r3 = Cal(new[] { 0, 0.4, 0.4 - 0.0099, 0.6 }, 0.01, 0.02);
		Check(a.Current == 1 && a.LastCandidate == 2 && !a.Warming && b.Current == 2 && b.Changes == 1 && r.Current == 1 && r2.Current == 2 && r3.Current == 1,
			"DV-T33 recheck through DvpSelfCalib: just below the margin keeps d, just above switches (abs and rel cases)");
		Check(!DvpSelfCalib.ShouldSwitch(1, 2, new[] { 0, double.NaN, 0.0, 0 }, 0, 0) && !DvpSelfCalib.ShouldSwitch(2, 2, abs2, 0, 0) && !DvpSelfCalib.ShouldSwitch(1, 0, abs2, 0, 0),
			"DV-T33 a NaN incumbent score keeps d; b == c or no candidate never switches");
	}

	// DV-T34 ties.
	static void T34()
	{
		Check(DvpSelfCalib.Candidate(new[] { double.NaN, 0.05, 0.05, 0.05 }, 3) == 1 && DvpSelfCalib.Candidate(new[] { double.NaN, 0.07, 0.05, 0.05 }, 3) == 2
			&& DvpSelfCalib.Candidate(new[] { double.NaN, double.NaN, 0.05, 0.04 }, 3) == 3 && DvpSelfCalib.Candidate(new[] { double.NaN, double.NaN, double.NaN, double.NaN }, 3) == 0
			&& DvpSelfCalib.Candidate(new[] { double.NaN, 0.07, 0.05, 0.01 }, 2) == 2,
			"DV-T34 exact equal scores: the candidate is the lower d (NaN never chosen; scan limited to dCap)");
		DvpSelfCalib w = Cal(new[] { 0, 0.05, 0.05, 0.05 }, 0, 0);
		Check(w.Current == 1 && !w.Warming && w.LastCandidate == 1 && w.Changes == 0, "DV-T34 leaving warm-up with tied scores keeps d = 1 (margins 0)");
		DvpSelfCalib x = Cal(new[] { 0, 0.2, 0.2, 0.05 }, 0.01, 0.02);
		bool was3 = x.Current == 3;
		Cal(new[] { 0, 0.2, 0.05, 0.05 }, 0.01, 0.02, x, 3);
		Check(was3 && x.Current == 3 && x.LastCandidate == 2 && x.Rechecks == 3 && !x.LastChanged, "DV-T34 an incumbent d = 3 tied by d = 2 stays at 3 (candidate 2, margin not met)");
	}

	// DV-T35 roll-crossing horizons.
	static void T35()
	{
		RingBar[] s = CalSeries(120, 35, new[] { 403, 611 }, null);
		DvpConfig c = SCfg();
		CalTrace t = RunCal(s, c);
		int n = c.winLen, step = c.calibStep; bool dropOk = true, gridOk = true; int dropsWithPending = 0, skipped = 0, wantSkipped = 0;
		for (int i = 0; i < s.Length; i++)
		{
			long dRoll = t.rollDropped[i] - (i == 0 ? 0 : t.rollDropped[i - 1]);
			long dAdded = t.added[i] - (i == 0 ? 0 : t.added[i - 1]);
			if (s[i].rollBar) { if (dRoll != t.pendingBefore[i] || dAdded != 0) dropOk = false; if (dRoll > 0) dropsWithPending++; }
			else if (dRoll != 0) dropOk = false;
			long dS = t.sampled[i] - (i == 0 ? 0 : t.sampled[i - 1]), dG = t.gridSkipped[i] - (i == 0 ? 0 : t.gridSkipped[i - 1]);
			bool grid = i % step == 0 && i >= n - 1;
			bool crosses = false; for (int k = i - n + 2; k <= i; k++) if (k >= 0 && s[k].rollBar) crosses = true;
			if (!grid) { if (dS != 0 || dG != 0) gridOk = false; continue; }
			if (crosses) { wantSkipped++; if (dG != 1 || dS != 0) gridOk = false; else skipped++; }
			else if (dS != 1 || dG != 0) gridOk = false;
		}
		DvpCalib cal = t.rig.S.Calib;
		Check(dropOk && dropsWithPending == 2 && cal.RollDropped == t.pendingBefore[403] + t.pendingBefore[611] && cal.Scored == t.rig.S.Self.Added,
			"DV-T35 a splice inside a pending horizon drops it whole (rollDropped " + cal.RollDropped + ", none reach the ring)");
		Check(gridOk && skipped == wantSkipped && skipped >= 2 && cal.GridSkipped == skipped,
			"DV-T35 grid positions whose window crosses a roll are skipped (gridSkipped " + skipped + "), the grid stays on pos mod calibStep = 0");
		CalTrace u = RunCal(s, c, false);
		Check(u.rig.S.Calib.RollDropped == 0 && u.rig.S.Calib.GridSkipped == 0 && u.rig.S.Calib.Sampled == Enumerable.Range(n - 1, s.Length - n + 1).Count(i => i % step == 0),
			"DV-T35 under rollMode None (RollDetection off) nothing is dropped or skipped");
	}

	// DV-T36 recalc reproducibility.
	static void T36()
	{
		RingBar[] s = CalSeries(200, 36, null, null);
		DvpConfig c = SCfg(1, true);
		CalTrace a = RunCal(s, c, true, true), b = RunCal(s, c, true, true);
		var da = a.rig.journal.Where(l => l.StartsWith("DVP DEGREE", StringComparison.Ordinal)).ToList();
		var db = b.rig.journal.Where(l => l.StartsWith("DVP DEGREE", StringComparison.Ordinal)).ToList();
		string pa = Path0(a, s.Length - 1);
		Check(da.Count > 0 && da.SequenceEqual(db) && pa == Path0(b, s.Length - 1) && a.rig.S.Self.Changes >= 1,
			"DV-T36 two runs give byte-identical DVP DEGREE lines (" + da.Count + ") and degree paths (" + a.rig.S.Self.Changes + " changes)");
		// journal on / off / random queries: identical degree paths and snapshots (incl. endpointDrift)
		var qOn = new List<string>(); var qOff = new List<string>(); var qRnd = new Dictionary<int, string>(); var rng = new Random(360);
		CalTrace on = RunCal(s, c, true, true, -1, (r, i) => qOn.Add(r.Q(0)));
		CalTrace off = RunCal(s, SCfg(1, false), true, false, -1, (r, i) => qOff.Add(r.Q(0)));
		CalTrace rnd = RunCal(s, SCfg(1, false), true, false, -1, (r, i) =>
		{
			if (rng.NextDouble() < 0.3) r.Q(rng.Next(0, 3));
			if (i % 7 == 0) qRnd[i] = r.Q(0);
		});
		bool snapsOk = qOn.SequenceEqual(qOff) && qRnd.All(kv => kv.Value == qOn[kv.Key]);
		Check(Path0(on, s.Length - 1) == pa && Path0(off, s.Length - 1) == pa && Path0(rnd, s.Length - 1) == pa && snapsOk,
			"DV-T36 journal on, journal off and random queries give identical degree paths and snapshots (E10)");
		CalTrace r4 = RunCal(s, SCfg(4, false));
		Check(Path0(r4, s.Length - 1) == pa, "DV-T36 refitEvery 1 and 4 give the identical degree path");
		// endpointDrift at the new degree, degChanged = 1 once per switch
		foreach (int r in new[] { 1, 4 })
		{
			int n = 30; var rig = new Rig(SCfg(r, false), true, false);
			var nf = new DvpFit(n); var pf = new DvpFit(n); double[] y = new double[n], v = new double[n];
			Func<DvpFit, int, bool> fitEnd = (f, endIdx) =>
			{
				if (endIdx - n + 1 < 0) return false;
				for (int k = 0; k < n; k++) { RingBar x = s[endIdx - n + 1 + k]; y[k] = (x.h + x.l) / 2; v[k] = x.v; }
				return f.Fit(y, v, n, 3, false);
			};
			int changed = 0, driftBad = 0, driftAfterSwitch = 0, checkedN = 0;
			for (int i = 0; i < s.Length; i++)
			{
				rig.Feed(s[i], false);
				DvpSnapshot x = rig.e.SnapshotRaw(1, rig.ring, 0);
				if (x == null || !x.valid || !x.refitBar) continue;
				if (x.degChanged) changed++;
				int e = x.anchorBar - 1, pe = e - r;
				if (pe - n + 1 < 0) continue;
				if (!fitEnd(nf, e) || !fitEnd(pf, pe)) { driftBad++; continue; }
				double want = (nf.Yhat(nf.U(n - 1 - r), x.deg) - pf.Yhat(1, x.deg)) / x.atr;
				if (!(Math.Abs(want - x.endpointDrift) <= 1e-9 * Math.Max(1, Math.Abs(want)))) driftBad++;
				else { checkedN++; if (x.degChanged) driftAfterSwitch++; }
			}
			long ch = rig.S.Self.Changes;
			Check(ch >= 1 && changed == ch && driftBad == 0 && driftAfterSwitch == ch && checkedN > 100,
				"DV-T36 refitEvery " + r + ": endpointDrift equals both fits at the current degree (" + checkedN + " refits, " + driftAfterSwitch + " right after a switch), degChanged=1 once per switch (" + changed + "/" + ch + ")");
		}
	}

	// DV-T37 journal formats.
	static void T37()
	{
		RingBar[] s = CalSeries(200, 37, null, null);
		DvpConfig c = SCfg();
		var infos = new List<string>();
		var rig = new Rig(c, true, true); rig.e.info = x => infos.Add(x);
		for (int i = 0; i < s.Length; i++) rig.Feed(s[i], false);
		DvpSelfCalib sc = rig.S.Self;
		var sel = rig.journal.Where(l => l.StartsWith("DVP DEGREE sel", StringComparison.Ordinal)).ToList();
		var re = new Regex(@"^DVP DEGREE sel stream\(Data1\) winLen\(30\) from\(([1-3])\) to\(([1-3])\) scores\[([^\]]*)\] n\((\d+)\)$");
		bool fmt = true;
		foreach (string l in sel)
		{
			Match m = re.Match(l);
			if (!m.Success) { fmt = false; continue; }
			string[] parts = m.Groups[3].Value.Split(',');
			if (parts.Length != sc.DCap) fmt = false;
			foreach (string p in parts) { double x; if (p != "NaN" && (!double.TryParse(p, NumberStyles.Float, CI, out x) || x.ToString("R", CI) != p)) fmt = false; }
		}
		Check(sel.Count == sc.Rechecks && sel.Count >= 5 && fmt && sc.DCap == 3, "DV-T37 one DVP DEGREE sel line per recheck at Journal (" + sel.Count + "), dCap scores in invariant R");
		var chg = new Regex(@"^DVP DEGREE change stream\(Data1\) winLen\(30\) from\(([1-3])\) to\(([1-3])\) n\((\d+)\)$");
		Check(infos.Count == sc.Changes && sc.Changes >= 1 && infos.All(l => chg.IsMatch(l) && chg.Match(l).Groups[1].Value != chg.Match(l).Groups[2].Value)
			&& !rig.journal.Any(l => l.StartsWith("DVP DEGREE change", StringComparison.Ordinal)) && infos.Count < sel.Count,
			"DV-T37 DVP DEGREE change at Info only on a change (" + infos.Count + " of " + sel.Count + " rechecks)");
		// NaN when the ring has no window for d: the first recheck comes 2 sessions after the start, before any window is scored
		RingBar[] e = CalSeries(10, 37, null, null, new DateTime(2024, 1, 29, 18, 0, 0));
		CalTrace t = RunCal(e, c, true, true);
		string first = t.rig.journal.FirstOrDefault(l => l.StartsWith("DVP DEGREE sel", StringComparison.Ordinal));
		Check(first == "DVP DEGREE sel stream(Data1) winLen(30) from(1) to(1) scores[NaN,NaN,NaN] n(0)", "DV-T37 empty ring scores print NaN (" + first + ")");
		Check(t.rig.warn.Count(w => w == rig.S.WarmingLine()) == 1, "DV-T37 the warming Warning text is the WarmingLine");
		// null sinks: no string is formatted (zero allocations across rechecks and switches)
		var eng = new _DvpEngine(c, "x", "ES", 0.25, true, false, null, null);
		var ring = new _BarRing(DvpStream.RingNeed(c));
		int warmBars = 300;
		for (int i = 0; i < warmBars; i++) { ring.Add(s[i]); eng.OnBar(1, ring, false); }
		long r0 = eng.Stream(1).Self.Rechecks, c0 = eng.Stream(1).Self.Changes;
		long b0 = GC.GetAllocatedBytesForCurrentThread();
		for (int i = warmBars; i < s.Length; i++) { ring.Add(s[i]); eng.OnBar(1, ring, false); }
		long b1 = GC.GetAllocatedBytesForCurrentThread();
		long dr = eng.Stream(1).Self.Rechecks - r0, dc = eng.Stream(1).Self.Changes - c0;
		Check(b1 - b0 == 0 && dr >= 3 && dc >= 1, "DV-T37 null sinks: zero allocations over " + dr + " rechecks and " + dc + " switches (" + (b1 - b0) + " bytes)");
		// host: the DVP sinks are engineSink lambdas onto trace(), whose writers use one File.AppendAllText per line
		string host = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		Check(host.Contains("m_dvp.info = engineSink(ETraceLevel.Info);") && host.Contains("File.AppendAllText(m_logFilePath, line + System.Environment.NewLine, s_utf8NoBom);")
			, "DV-T37 host: DVP DEGREE change goes to the Info sink; the log writes one File.AppendAllText per line");
	}

	// DV-T38 online vs offline parity, and the DV-8.2 degree-change trigger suppression.
	static void T38()
	{
		RingBar[] s = CalSeries(150, 38, new[] { 500 }, null);
		DvpConfig c = SCfg(); c.calibLen = 2000; c.calibMinScored = 20;
		CalTrace t = RunCal(s, c, true, true);
		DvpCalib cal = t.rig.S.Calib; DvpSelfCalib sc = t.rig.S.Self; DvpCalib.Bucket p = cal.Pooled();
		bool same = cal.Scored <= c.calibLen && sc.Count == cal.Scored; double maxErr = 0;
		for (int d = 1; d <= sc.DCap; d++)
		{
			double ci, cf; sc.Means(d, out ci, out cf);
			if (p.n[d] == 0) { same = false; continue; }
			maxErr = Math.Max(maxErr, Math.Max(Math.Abs(ci - p.covIn[d] / p.n[d]), Math.Abs(cf - p.covFwd[d] / p.n[d])));
		}
		Check(same && maxErr <= 1e-12 && cal.Scored > 100, "DV-T38 ring means equal the DVP CAL pooled means of the same run (" + cal.Scored + " windows, max err " + maxErr.ToString("G3", CI) + ")");
		// the DVP CAL table of a SelfCalibrate run equals the Calibrate run's over the same bars (all before HOLDOUT_FROM)
		DvpConfig k = SCfg(); k.degMode = EDvpDegMode.Calibrate;
		Func<DvpConfig, List<string>> calLines = cfg => { var r = RunCal(s, cfg, true, true).rig; r.e.Finish(); return r.journal.Where(l => l.StartsWith("DVP CAL", StringComparison.Ordinal)).ToList(); };
		var la = calLines(SCfg()); var lb = calLines(k);
		Check(s[s.Length - 1].time < DvpConst.HoldoutFrom && la.Count > 4 && la.SequenceEqual(lb) && la.Last().Contains("rollDropped(") && !la.Last().Contains("rollDropped(0)"),
			"DV-T38 the DVP CAL table from SelfCalibrate equals the Calibrate table over the same bars before HOLDOUT_FROM (" + la.Count + " lines)");
		// DV-8.2: a trigger on a degree-change bar is suppressed; a run already in progress continues
		double[] cl = { 103, 102, 101.5, 99, 99 }, rp = cl.Select(_ => 100.0).ToArray(), at = cl.Select(_ => 10.0).ToArray();
		bool[] g2 = { false, false, true, false, false }, g0 = { true, false, false, false, false };
		Check(DvpAccept.Triggered(cl, rp, at, null, 5, 2, 1) && !DvpAccept.Triggered(cl, rp, at, g2, 5, 2, 1)
			&& DvpAccept.Accepted(cl, rp, at, g0, 5, 1, 2, true, 0) && !DvpAccept.Accepted(cl, rp, at, g2, 5, 1, 2, true, 0)
			&& DvpAccept.Accepted(cl, rp, at, null, 5, 1, 2, true, 0) == DvpAccept.Accepted(cl, rp, at, 5, 1, 2, true, 0),
			"DV-T38 / DV-8.2 a trigger on a degree-change bar is suppressed; a run triggered earlier continues past one");
		// G marks exactly the bar after each switch (bar k's own snapshot ends at k-1, so the switch bar's window shows on bar +1)
		DvpConfig gc = SCfg(); gc.calibMarginAbs = 0; gc.calibMarginRel = 0;
		RingBar[] s2 = CalSeries(200, 32, null, null);
		var rig = new Rig(gc, true, false); int gBars = 0, gWrong = 0; var switchBars = new HashSet<int>(); int prevDeg = 1;
		for (int i = 0; i < s2.Length; i++)
		{
			rig.Feed(s2[i], false);
			if (rig.S.CurrentDegree != prevDeg) switchBars.Add(i);
			prevDeg = rig.S.CurrentDegree;
			DvpRailHistory h = rig.e.HistoryOf(1, rig.ring, ELevelKind.DvpPoc, 0);
			if (h == null || h.Len < 2) continue;
			if (h.G[0]) { gBars++; if (!switchBars.Contains(i - 1)) gWrong++; }
			else if (switchBars.Contains(i - 1) && h.D[0] > 0 && h.D[1] > 0) gWrong++;
		}
		Check(switchBars.Count >= 1 && gBars == switchBars.Count && gWrong == 0, "DV-T38 rail history marks the bar after each switch as a degree-change bar (" + gBars + "/" + switchBars.Count + ")");
	}

	static void D9Tests() { T30(); T31(); T32(); T33(); T34(); T35(); T36(); T37(); T38(); }
}
