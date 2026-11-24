using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PowerLanguage.Function;

// M-DVP PR-D3 (specs/m-dvp-spec.md §15): DV-T14..T16, T16b, T22, T25, T27..T29, DV-R1 and the rollOn(1) rollBar test, on the
// MC-free _DvpEngine / DvpStream driven the way _CustomSignal.stepDvp drives it (ring.Add, then OnBar on the closed bar).
static partial class Program
{
	static readonly CultureInfo CI = CultureInfo.InvariantCulture;

	static DvpConfig Cfg(int winLen = 240, int refitEvery = 1, bool journalFits = true)
	{
		return new DvpConfig { enabled = true, winLen = winLen, refitEvery = refitEvery, journalFits = journalFits, splitFlow = false };
	}

	// Synthetic 15m Globex-like series: 92-bar sessions from 18:00 to 17:00 (crossing midnight; tradingDay = the next date),
	// half-day sessions are 52 bars; a roll adds a +35 point splice and sets rollBar on that bar only.
	static RingBar[] Series(int count, int seed, int[] rolls, int[] halfDaySessions, out bool[] halfDay)
	{
		var rng = new Random(seed);
		Func<double> g = () => { double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble(); return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2); };
		var bars = new RingBar[count]; halfDay = new bool[count];
		var rollSet = new HashSet<int>(rolls ?? new int[0]); var half = new HashSet<int>(halfDaySessions ?? new int[0]);
		double p = 4000, drift = 0, prevC = 4000;
		DateTime sessStart = new DateTime(2024, 1, 1, 18, 0, 0);
		int sess = 0, inSess = 0;
		for (int i = 0; i < count; i++)
		{
			if (inSess == (half.Contains(sess) ? 52 : 92)) { sess++; inSess = 0; sessStart = sessStart.AddDays(1); }
			DateTime t = sessStart.AddMinutes(15 * (inSess + 1)), td = sessStart.AddHours(6).Date;
			drift = 0.995 * drift + 0.03 * g(); p += drift + 1.5 * g();
			bool roll = rollSet.Contains(i);
			if (roll) { p += 35; prevC += 35; }
			double o = Math.Round(prevC * 4) / 4, c = Math.Round(p * 4) / 4;
			double h = Math.Max(o, c) + Math.Round(Math.Abs(g()) * 4) / 4, l = Math.Min(o, c) - Math.Round(Math.Abs(g()) * 4) / 4;
			double v = Math.Round(500 + 400 * Math.Abs(g())), up = Math.Round(v * (0.5 + 0.05 * g()));
			int mod = t.Hour * 60 + t.Minute;
			bars[i] = new RingBar { bar = i + 1, time = t, tradingDay = td, o = o, h = h, l = l, c = c, v = v, up = up, down = v - up, rth = mod > 570 && mod <= 960, rollBar = roll };
			halfDay[i] = half.Contains(sess);
			prevC = c; inSess++;
		}
		return bars;
	}

	sealed class Rig
	{
		public _DvpEngine e; public _BarRing ring; public List<string> journal; public List<string> warn = new List<string>();
		public Rig(DvpConfig c, bool rollDetect, bool journalOn, bool rthKnown = false, string runId = "golden")
		{
			journal = journalOn ? new List<string>() : null;
			List<string> w = warn, j = journal;
			e = new _DvpEngine(c, runId, "ES", 0.25, rollDetect, rthKnown, journalOn ? (Action<string>)(s => j.Add(s)) : null, s => w.Add(s));
			ring = new _BarRing(DvpStream.RingNeed(c));
		}
		public void Feed(RingBar b, bool half) { ring.Add(b); e.OnBar(1, ring, half); }
		public DvpStream S { get { return e.Stream(1); } }
		public string Q(int endAgo) { return Sig(e.SnapshotRaw(1, ring, endAgo), e.RailsOf(1, ring, endAgo)); }
	}

	static string Sig(DvpSnapshot s, DvpRails r)
	{
		if (s == null) return "null";
		var sb = new StringBuilder();
		foreach (var f in typeof(DvpSnapshot).GetFields())
		{
			object v = f.GetValue(s);
			sb.Append(f.Name).Append('=').Append(v is double ? ((double)v).ToString("R", CI) : v is DateTime ? ((DateTime)v).Ticks.ToString(CI) : Convert.ToString(v, CI)).Append(' ');
		}
		if (r != null) for (int i = 0; i < r.Count; i++) sb.Append(r.Items[i].kind).Append(':').Append(r.Items[i].z.ToString("R", CI)).Append('@').Append(r.Items[i].price.ToString("R", CI)).Append(' ');
		return sb.ToString();
	}

	static int[] Pick(Random rng, int lo, int hi, int k)
	{
		var set = new SortedSet<int>(); while (set.Count < Math.Min(k, hi - lo)) set.Add(rng.Next(lo, hi)); return set.ToArray();
	}

	// DV-T14: a snapshot at bar t computed on the series truncated at t-1 equals the full-series one, 500 random t (r = 1) + 100 (r = 4).
	static void T14()
	{
		bool[] hd; RingBar[] s = Series(5000, 7, new[] { 1800, 3600 }, new[] { 20 }, out hd);
		foreach (int r in new[] { 1, 4 })
		{
			var rng = new Random(14 + r);
			int[] ts = Pick(rng, 2, s.Length, r == 1 ? 500 : 100);
			var want = new Dictionary<int, string>(); var tset = new HashSet<int>(ts);
			var full = new Rig(Cfg(240, r), true, false);
			for (int i = 0; i < s.Length; i++) { full.Feed(s[i], hd[i]); if (tset.Contains(i)) want[i] = full.Q(1); }
			int bad = 0, valid = 0;
			foreach (int t in ts)
			{
				var tr = new Rig(Cfg(240, r), true, false);
				for (int i = 0; i < t; i++) tr.Feed(s[i], hd[i]);
				string got = tr.Q(0);
				if (got != want[t]) bad++;
				if (got.Contains("valid=True")) valid++;
			}
			Check(bad == 0 && valid > ts.Length / 2, "DV-T14 truncation audit r=" + r + ": " + ts.Length + " t, " + valid + " with a fit, " + bad + " mismatches");
		}
	}

	// DV-T15: permuting bars > t-1 (values; bar numbers/times kept) leaves bar t's snapshot and rails unchanged.
	static void T15()
	{
		bool[] hd; RingBar[] s = Series(3000, 15, new[] { 1500 }, null, out hd);
		var rng = new Random(15);
		int[] ts = Pick(rng, 300, s.Length - 10, 40);
		int bad = 0;
		foreach (int t in ts)
		{
			var a = new Rig(Cfg(), true, false); var b = new Rig(Cfg(), true, false);
			var p = (RingBar[])s.Clone();
			int[] idx = Enumerable.Range(t, s.Length - t).ToArray();
			for (int i = idx.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); int x = idx[i]; idx[i] = idx[j]; idx[j] = x; }
			for (int k = 0; k < idx.Length; k++)
			{
				RingBar src = s[idx[k]]; RingBar d = p[t + k];
				d.o = src.o; d.h = src.h; d.l = src.l; d.c = src.c; d.v = src.v; d.up = src.up; d.down = src.down; d.rollBar = src.rollBar; d.rth = src.rth;
				p[t + k] = d;
			}
			for (int i = 0; i <= t; i++) { a.Feed(s[i], hd[i]); b.Feed(p[i], hd[i]); }
			if (a.Q(1) != b.Q(1)) bad++;
		}
		Check(bad == 0, "DV-T15 shuffled future: " + ts.Length + " t, " + bad + " differ");
	}

	// DV-T16: rolls (DV-7.1 as amended by Ettienne 2026-11-23 14:09 CT). A window with a roll bar at i in [1, n-1] is truncated to
	// start on that roll bar and fitted once RollMin = 60 post-roll bars have accrued (n = bars since the roll, growing to winLen);
	// with fewer it is skipped. A window starting on the roll bar is clean. skipShare exact and diagnosticOnly at 0.30 +- one
	// window, ATR ResetPrev on the roll bar.
	static void T16()
	{
		const int n = 240; int[] rolls = { 1800, 3600 };
		bool[] hd; RingBar[] s = Series(5000, 16, rolls, null, out hd);
		var rig = new Rig(Cfg(n), true, false);
		int wrongSkip = 0, wrongValid = 0, startOnRoll = 0, firstAfter = -1, wrongN = 0, trunc = 0; int rm = DvpConst.RollMinBars;
		var atr = new Gate0Atr(DvpConst.AtrLen); int atrBad = 0;
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], hd[i]);
			if (s[i].rollBar) atr.ResetPrev();
			atr.Add(s[i].h, s[i].l, s[i].c);
			DvpSnapshot x = rig.e.SnapshotRaw(1, rig.ring, 0);   // window ends at e = i (serves bar i+1)
			if (BitConverter.DoubleToInt64Bits(x.atr) != BitConverter.DoubleToInt64Bits(atr.Value)) atrBad++;
			if (i < n - 1) continue;
			int[] inWin = rolls.Where(R => R >= i - n + 2 && R <= i).ToArray();
			int since = inWin.Length == 0 ? -1 : i - inWin.Max() + 1;
			bool cross = since >= 0 && since < rm;
			if (x.crossesRoll != cross) wrongSkip++;
			if (x.valid == cross) wrongValid++;
			if (x.valid && (x.n != (since >= 0 ? since : n) || x.rollTruncated != (since >= 0))) wrongN++;
			if (x.valid && x.rollTruncated) trunc++;
			if (rolls.Any(R => i == R + n - 1) && x.valid && x.n == n && !x.rollTruncated) startOnRoll++;
			if (i > 1800 && firstAfter < 0 && x.valid) firstAfter = i;
			if (cross && (rig.e.Snapshot(1, rig.ring, 0) != null || rig.e.RailsOf(1, rig.ring, 0) != null)) wrongValid++;
		}
		Check(wrongSkip == 0 && wrongValid == 0, "DV-T16 skipped (crossesRoll) iff the newest roll bar in [1, n-1] has < 60 post-roll bars; skipped windows have no rails (" + wrongSkip + "/" + wrongValid + ")");
		Check(wrongN == 0 && trunc == 2 * (n - rm), "DV-T16 truncated windows: n = bars since the roll bar (60..239), winEff flag set; " + trunc + " truncated, " + wrongN + " wrong");
		Check(startOnRoll == 2, "DV-T16 a window starting on the roll bar is clean and full (" + startOnRoll + "/2)");
		Check(firstAfter == 1800 + rm - 1, "DV-T16 fits resume once RollMinBars = 60 post-roll bars have accrued (first at " + firstAfter + ")");
		DvpStream st = rig.S;
		Check(st.Skipped == 2 * (rm - 1) && st.Truncated == 2 * (n - rm) && st.Eligible == s.Length - (n - 1) && st.SkipShare == (double)(2 * (rm - 1)) / (s.Length - (n - 1)) && !st.DiagnosticOnly,
			"DV-T16 skipShare = 2(60-1)/eligible, truncated = 2(n-60) (" + st.Skipped + "/" + st.Truncated + "/" + st.Eligible + ")");
		Check(atrBad == 0, "DV-T16 ATR(20) as of t-1 with ResetPrev on the roll bar (" + atrBad + " differ)");
		var noReset = new Gate0Atr(DvpConst.AtrLen); for (int i = 0; i <= 1800; i++) noReset.Add(s[i].h, s[i].l, s[i].c);
		var rs = new Rig(Cfg(n), true, false); for (int i = 0; i <= 1800; i++) rs.Feed(s[i], false);
		Check(rs.e.SnapshotRaw(1, rs.ring, 0).atr < noReset.Value, "DV-T16 ResetPrev drops the splice gap from the roll bar's true range");
		// diagnosticOnly at 0.30 +- one window: skipped = 2 x 59 = 118; eligible 393 -> 0.3003 (diagnostic), 394 -> 0.2995 (not)
		foreach (int E in new[] { 393, 394 })
		{
			bool[] h2; RingBar[] s2 = Series(E + n - 1, 30, new[] { 300, 500 }, null, out h2);
			var r2 = new Rig(Cfg(n), true, false); for (int i = 0; i < s2.Length; i++) r2.Feed(s2[i], false);
			Check(r2.S.Eligible == E && r2.S.Skipped == 118 && r2.S.DiagnosticOnly == (E == 393), "DV-T16 diagnosticOnly at skipShare " + r2.S.SkipShare.ToString("0.0000", CI) + " = " + r2.S.DiagnosticOnly);
		}
		var rr = new Rig(Cfg(n, 3), true, false); for (int i = 0; i < s.Length; i++) rr.Feed(s[i], false);
		Check(rr.S.Skipped > 0 && rr.S.Skipped <= 2 * (rm - 1 + 2) && rr.S.Truncated > 0, "DV-T16 skip counting with refitEvery = 3 (" + rr.S.Skipped + " skipped, " + rr.S.Truncated + " truncated)");
	}

	// rollOn(1) -> RingBar.rollBar: the host's ringBarOf sets rollBar = (ago == 0 && rollOn(ds)) and rollOn(1) is
	// m_rollDet.LastRollBar == current bar. Simulated with the real _RollDetector on closed bars plus intrabar ticks.
	static void RollBarTest()
	{
		string host = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		Check(host.Contains("rollBar = ago == 0 && rollResetOn(ds)") && host.Contains("if (ds == 1) return m_rollDet.LastRollBar == Bars.FullSymbolData.Current;"),
			"rollOn(1) host wiring: ringBarOf sets rollBar from rollResetOn (roll + late splice, ruling 2026-11-23 13:31 CT) on the closed bar only");
		var det = new _RollDetector { series = false, table = false };
		var ring = new _BarRing(64); var rollBars = new List<int>();
		int barNo = 0; double prev = double.NaN;
		for (int i = 0; i < 300; i++)
		{
			string name = i < 150 ? "ESH24" : "ESM24";
			DateTime day = new DateTime(2024, 3, 1).AddDays(i / 92);
			for (int tick = 0; tick < 3; tick++)   // intrabar updates: the host neither steps the detector nor adds to the ring
			{
				bool closed = tick == 2;
				if (!closed) continue;
				barNo++;
				det.Step(barNo, day, 4000 + i, prev, name, 0.25);
				bool rollOn1 = det.LastRollBar == barNo;
				ring.Add(new RingBar { bar = barNo, time = day.AddMinutes(i), tradingDay = day, h = 1, l = 0, c = 0.5, v = 1, rollBar = rollOn1 });
				if (ring[0].rollBar) rollBars.Add(barNo);
				prev = 4000 + i;
			}
		}
		Check(rollBars.Count == 1 && rollBars[0] == 151, "rollOn(1) sets rollBar only on the closed first bar of the new contract (" + string.Join(",", rollBars) + ")");
	}

	// DV-T16b: journal-on and journal-off runs, and random query patterns, give identical snapshots, endpointDrift and skipShare.
	// DV-7.1 truncation (Ettienne 2026-11-23 14:09 CT): constants, a late switch (13:31 CT re-reset: roll flags on the reopen AND the
	// switch bar) counts the 60 bars from the switch, the truncated fit equals an independent fit on the post-roll bars (degree cap
	// Cap(n), sigma bit-identical), winLen < 60 waits winLen, the journal shows DVP ROLL warmup / resume, winEff(n) and truncated().
	static void T16Trunc()
	{
		Check(DvpConst.RollMinBars == 60 && Cfg().Canon().Contains(";rollMinBars=60;"), "DV-1.10 RollMinBars = 60 is a constant hashed in Canon()");
		const int n = 240; int reopen = 1800, sw = 1830; int rm = DvpConst.RollMinBars;
		bool[] hd; RingBar[] s = Series(2400, 41, new[] { reopen, sw }, null, out hd);
		var rig = new Rig(Cfg(n), true, true);
		int firstValid = -1, badFit = 0, capBad = 0; var f = new DvpFit(n); double[] y = new double[n], v = new double[n];
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], hd[i]);
			if (i <= reopen) continue;
			DvpSnapshot x = rig.e.SnapshotRaw(1, rig.ring, 0);
			if (x == null || !x.valid) continue;
			if (firstValid < 0) firstValid = i;
			if (i < sw + n - 1)
			{
				int m = i - sw + 1;
				for (int k = 0; k < m; k++) { RingBar b = s[sw + k]; y[k] = (b.h + b.l) / 2; v[k] = b.v; }
				f.Fit(y, v, m, 3, false); f.Choose(x.deg, false);
				if (x.n != m || !x.rollTruncated || x.sigma != f.SigmaD) badFit++;
				if (x.dCap > DvpDegree.Cap(m, 3) || x.deg > x.dCap) capBad++;
			}
		}
		Check(firstValid == sw + rm - 1, "DV-7.1 late switch: the 60 post-roll bars count from the switch bar (first fit at " + firstValid + ", want " + (sw + rm - 1) + ")");
		Check(badFit == 0 && capBad == 0, "DV-7.1 truncated fits = an independent fit on the post-switch bars only (n, sigma bit-identical; dCap = Cap(n)) (" + badFit + "/" + capBad + ")");
		var j = rig.journal;
		string warm = j.FirstOrDefault(l => l.StartsWith("DVP ROLL warmup", StringComparison.Ordinal)), res = j.FirstOrDefault(l => l.StartsWith("DVP ROLL resume", StringComparison.Ordinal));
		Check(j.Count(l => l.StartsWith("DVP ROLL warmup", StringComparison.Ordinal)) == 2 && warm != null && warm.Contains("winLen(240) rollMinBars(60)")
			&& j.Count(l => l.StartsWith("DVP ROLL resume", StringComparison.Ordinal)) == 1 && res != null && res.EndsWith("winEff(60) winLen(240)"),
			"DV-11.1 journal: DVP ROLL warmup on each roll bar, one DVP ROLL resume with winEff(60): " + res);
		Check(j.Any(l => l.StartsWith("DVP FIT", StringComparison.Ordinal) && l.Contains("crossesRoll(0) winEff(60)")) && !j.Any(l => l.Contains("crossesRoll(1) winEff(")),
			"DV-11.1 FIT lines: winEff(n) on truncated fits only");
		rig.e.Finish();
		string run = rig.journal.LastOrDefault(l => l.StartsWith("DVP RUN", StringComparison.Ordinal)) ?? "";
		Check(run.Contains(" skipped(" + (sw - (reopen + 1) + 1 + rm - 1) + ") truncated(" + (n - rm) + ") "), "DV-11.3 DVP RUN skipped()/truncated(): " + run);
		var rq = new Rig(Cfg(n, 1, false), true, true); for (int i = 0; i < s.Length; i++) rq.Feed(s[i], hd[i]);
		Check(rq.journal.Count(l => l.StartsWith("DVP ROLL warmup", StringComparison.Ordinal)) == 2 && rq.journal.Count(l => l.StartsWith("DVP ROLL resume", StringComparison.Ordinal)) == 1
			&& !rq.journal.Any(l => l.StartsWith("DVP FIT", StringComparison.Ordinal)), "DV-11.1 DVP ROLL warmup / resume lines do not need journalFits (sandbox: FIT lines are Verbose)");
		// winLen 40 < 60: RollMin = winLen, so the first fit after the roll is the clean window starting on the roll bar
		bool[] h2; RingBar[] s2 = Series(400, 5, new[] { 200 }, null, out h2);
		var r2 = new Rig(Cfg(40), true, false); int fv = -1;
		for (int i = 0; i < s2.Length; i++) { r2.Feed(s2[i], false); var x = r2.e.SnapshotRaw(1, r2.ring, 0); if (i > 200 && fv < 0 && x != null && x.valid) fv = i; }
		Check(r2.S.RollMin == 40 && fv == 200 + 40 - 1, "DV-7.1 winLen 40 < RollMinBars: RollMin = winLen, fits resume at the clean window (" + fv + ")");
	}

	// Owner ruling 2026-11-23 18:46 CT: an intrabar-body splice (Data1 slower than 1 minute) puts BOTH contracts in the splice bar. The host
	// flags it RingBar.rollMixed; DVP keeps it out of every post-roll window (also at i = 0), counts the 60 bars from the NEXT bar, and
	// keeps its range out of the ATR. rollMixed = false (every 1-minute chart) is unchanged (T16 / T16Trunc / R1 golden).
	static void T16Mixed()
	{
		const int n = 240; int reopen = 1800, sw = 1830; int rm = DvpConst.RollMinBars;
		bool[] hd; RingBar[] s = Series(2400, 41, new[] { reopen, sw }, null, out hd);
		var mx = (RingBar[])s.Clone(); RingBar mb = mx[sw]; mb.rollMixed = true; mb.h += 70.5; mx[sw] = mb;   // the 70.5-pt mixed bar
		var rig = new Rig(Cfg(n), true, true);
		var atr = new Gate0Atr(DvpConst.AtrLen);
		int firstValid = -1, badFit = 0, hasMixed = 0, fullAt = -1, iZero = -1, atrBad = 0; var f = new DvpFit(n); double[] y = new double[n], v = new double[n];
		for (int i = 0; i < mx.Length; i++)
		{
			rig.Feed(mx[i], hd[i]);
			if (mx[i].rollBar) atr.ResetPrev();
			if (!mx[i].rollMixed) atr.Add(mx[i].h, mx[i].l, mx[i].c);
			DvpSnapshot x = rig.e.SnapshotRaw(1, rig.ring, 0);
			if (i >= n && BitConverter.DoubleToInt64Bits(x.atr) != BitConverter.DoubleToInt64Bits(atr.Value)) atrBad++;
			if (i <= reopen || x == null || !x.valid) continue;
			if (firstValid < 0) firstValid = i;
			if (i - x.n + 1 <= sw) hasMixed++;   // the window [i-n+1, i] holds the mixed bar
			if (x.rollTruncated)
			{
				int m = i - sw;   // bars after the mixed bar
				for (int k = 0; k < m; k++) { RingBar b = mx[sw + 1 + k]; y[k] = (b.h + b.l) / 2; v[k] = b.v; }
				f.Fit(y, v, m, 3, false); f.Choose(x.deg, false);
				if (x.n != m || x.sigma != f.SigmaD) badFit++;
			}
			if (i == sw + n - 1) iZero = x.rollTruncated && x.n == n - 1 ? 1 : 0;
			if (fullAt < 0 && !x.rollTruncated && x.n == n) fullAt = i;
		}
		Check(firstValid == sw + rm, "18:46 mixed splice bar: the 60 post-roll bars count from the bar AFTER it (first fit at " + firstValid + ", want " + (sw + rm) + "; clean splice " + (sw + rm - 1) + ")");
		Check(hasMixed == 0 && badFit == 0, "18:46 no fitted window contains the mixed bar; truncated fit = an independent fit on the bars after it (" + hasMixed + "/" + badFit + ")");
		Check(iZero == 1 && fullAt == sw + n, "18:46 the mixed bar at i = 0 still cuts (n = 239); the first full window starts on the next bar (" + iZero + ", " + fullAt + ")");
		Check(atrBad == 0, "18:46 ATR(20): the mixed bar's range is left out and the next bar starts fresh (" + atrBad + " differ)");
		string warm = rig.journal.LastOrDefault(l => l.StartsWith("DVP ROLL warmup", StringComparison.Ordinal)) ?? "";
		string res = rig.journal.FirstOrDefault(l => l.StartsWith("DVP ROLL resume", StringComparison.Ordinal)) ?? "";
		Check(warm.Contains("mixedBar(1): the roll bar holds both contracts and is excluded") && rig.journal.Count(l => l.Contains("mixedBar(1)")) == 1 && res.EndsWith("winEff(60) winLen(240)") && res.Contains(mx[sw + rm].time.ToString("yyyy-MM-dd HH:mm:ss", CI)),
			"18:46 journal: warmup line with mixedBar(1) on the mixed bar only, resume with winEff(60) one bar later: " + res);
		rig.e.Finish();
		string run = rig.journal.LastOrDefault(l => l.StartsWith("DVP RUN", StringComparison.Ordinal)) ?? "";
		Check(run.Contains(" skipped(" + (sw - (reopen + 1) + 1 + rm) + ") truncated(" + (n - rm) + ") "), "18:46 DVP RUN: one more skipped window than a clean splice, truncated unchanged (the i = 0 window is cut too): " + run);
		// rollMixed on a bar that is not a roll bar is ignored (the host sets it only with rollBar)
		var ns = (RingBar[])s.Clone(); RingBar nb = ns[1000]; nb.rollMixed = true; ns[1000] = nb;
		var a1 = new Rig(Cfg(n), true, false); var a2 = new Rig(Cfg(n), true, false); int diff = 0;
		for (int i = 0; i < s.Length; i++) { a1.Feed(s[i], hd[i]); a2.Feed(ns[i], hd[i]); if (i >= n && a1.Q(1) != a2.Q(1)) diff++; }
		Check(diff == 0, "18:46 rollMixed without rollBar changes nothing (" + diff + ")");
	}

	static void T16b()
	{
		bool[] hd; RingBar[] s = Series(4000, 161, new[] { 1700, 3300 }, new[] { 10 }, out hd);
		foreach (int r in new[] { 1, 3 })
		{
			var rng = new Random(160 + r);
			var test = new HashSet<int>(Pick(rng, 300, s.Length, 100));
			var A = new Rig(Cfg(240, r), true, true); var B = new Rig(Cfg(240, r), true, false); var C = new Rig(Cfg(240, r), true, false);
			int bad = 0, drifts = 0;
			for (int i = 0; i < s.Length; i++)
			{
				if (rng.NextDouble() < 0.2) B.Q(0);   // intrabar query before the close (ring newest = i-1)
				A.Feed(s[i], hd[i]); B.Feed(s[i], hd[i]); C.Feed(s[i], hd[i]);
				A.Q(1);
				if (rng.NextDouble() < 0.3) B.Q(rng.Next(2));
				if (test.Contains(i))
				{
					string a = A.Q(1), b = B.Q(1), c = C.Q(1), a0 = A.Q(0), b0 = B.Q(0), c0 = C.Q(0);
					if (a != b || a != c || a0 != b0 || a0 != c0) bad++;
					DvpSnapshot x = A.e.SnapshotRaw(1, A.ring, 0);
					if (x.refitBar && !double.IsNaN(x.endpointDrift)) drifts++;
				}
			}
			Check(bad == 0 && drifts > 0, "DV-T16b path independence r=" + r + ": 100 bars, " + bad + " differ (" + drifts + " refit bars with endpointDrift)");
			Check(A.S.Skipped == B.S.Skipped && A.S.Eligible == C.S.Eligible && A.S.SkipShare.Equals(C.S.SkipShare), "DV-T16b skipShare path-independent r=" + r);
			Check(C.S.FitsComputed < A.S.FitsComputed, "DV-T16b journal-off run fitted only on query (" + C.S.FitsComputed + " vs " + A.S.FitsComputed + ")");
		}
		// between refits the frozen fit is re-evaluated at u = UAhead(1 + bars since the anchor) (DV-3.7)
		var R = new Rig(Cfg(240, 3), true, false); bool ok = true; int seen = 0;
		for (int i = 0; i < 1200; i++)
		{
			R.Feed(s[i], hd[i]);
			DvpSnapshot x = R.e.Snapshot(1, R.ring, 0);
			if (x == null) continue;
			DvpFit f = R.S.FitFor(x);
			ok &= x.uT == f.UAhead(1 + x.sinceRefit) && x.yhatT == f.Yhat(x.uT) && x.refitBar == (s[i].bar % 3 == 0) && (x.refitBar || double.IsNaN(x.endpointDrift));
			seen++;
		}
		Check(ok && seen > 900, "DV-3.7 refitEvery = 3: frozen coefficients at u_t, endpointDrift only on refit bars");
	}

	// DV-T22 laziness + the eager-allocation check (D3 brief): disabled -> no engine / ring / PA; enabled, no query, journal off -> 0 fits.
	static void T22()
	{
		bool[] hd; RingBar[] s = Series(3000, 22, new[] { 1500 }, null, out hd);
		var rig = new Rig(Cfg(), true, false);
		for (int i = 0; i < s.Length; i++) rig.Feed(s[i], hd[i]);
		Check(rig.S.FitsComputed == 0 && rig.S.Eligible > 0, "DV-T22 enabled, no query, journal off: fit counter 0 (" + rig.S.FitsComputed + ")");
		Check(rig.S.CalibFits == rig.S.Calib.Sampled && rig.S.CalibFits > 0, "DV-T22 rev 6: the calibration fit counter equals the sampled grid windows (" + rig.S.CalibFits + "), separate from the snapshot counter");
		string core0 = CoreSource();
		int cs0 = core0.IndexOf("void calibStep(", StringComparison.Ordinal), ce0 = core0.IndexOf("public DvpSelfCalib Self", StringComparison.Ordinal);
		string cbody = cs0 > 0 && ce0 > cs0 ? core0.Substring(cs0, ce0 - cs0) : "";
		Check(cbody.Length > 0 && !cbody.Contains("man") && !cbody.Contains("Rails") && !cbody.Contains("Lattice") && !cbody.Contains("Pivots") && cbody.Contains("loadAndFit(m_calFit"),
			"DV-T22 rev 6: the calibration step builds no manifold, rail or lattice (fit only)");
		string host = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		int sd = host.IndexOf("private void startDvp()", StringComparison.Ordinal);
		string body = sd >= 0 ? host.Substring(sd, host.IndexOf("private void stepDvp()", sd, StringComparison.Ordinal) - sd) : "";
		int ret = body.IndexOf("if (!DvpConfig.enabled) return;", StringComparison.Ordinal);
		Check(ret > 0 && ret < body.IndexOf("requireRing(", StringComparison.Ordinal) && ret < body.IndexOf("new _DvpEngine(", StringComparison.Ordinal) && ret < body.IndexOf("_PriceActionEngine", StringComparison.Ordinal),
			"DV-T22 disabled: startDvp returns before requireRing, the engine and any PA instance");
		int lst = body.IndexOf("new List<LevelRef>()", StringComparison.Ordinal);
		string noBody = System.Text.RegularExpressions.Regex.Replace(host.Replace(body, ""), @"//[^\n]*", "");
		Check(ret > 0 && lst > ret && !noBody.Contains("new List<LevelRef>") && noBody.Contains("private List<LevelRef> m_dvpRails, m_dvpRailPool;"),
			"DV-T22/DV-13.1 disabled: the DVP rail lists are allocated only in startDvp after the enabled check (no field initialiser)");
		Check(host.Contains("if (m_dvp != null) stepDvp();") && host.Contains("Dvp queried but DvpConfig.enabled is false") && host.Contains("startVpHelpers();   // Phase 3")
			&& host.IndexOf("startDvp();", StringComparison.Ordinal) > host.IndexOf("startVpHelpers();   // Phase 3", StringComparison.Ordinal),
			"DV-T22 one null check per CalcBar, one Warning on query; startDvp runs after startVpHelpers (which clears ring requests)");
		// eager allocation: the row arrays (DvpManifold) and every window buffer are built only under the _DvpEngine constructor
		var all = Directory.GetFiles(Root(), "*.CS").Where(f => Path.GetFileName(f) != "_Gate0.Function.CS").Select(File.ReadAllText).ToArray();
		// PR-D8: the Gate0 KD-02 canary is the only other host; its one engine is built in kd02Init (Configure, KD02 only)
		string g0 = File.ReadAllText(Path.Combine(Root(), "_Gate0.Function.CS"));
		int g0New = g0.IndexOf("new _DvpEngine(", StringComparison.Ordinal), g0Init = g0.IndexOf("private string kd02Init()", StringComparison.Ordinal);
		Check(Count(g0, "new _DvpEngine(") == 1 && Count(g0, "new DvpStream(") == 0 && g0Init > 0 && g0New > g0Init && g0New < g0.IndexOf("private void kd02Feed(", StringComparison.Ordinal)
			&& g0.Contains("Cv == EGate0Canary.KD02)") && g0.Contains("if (m_kd != null) kd02Feed(b);"),
			"PR-D8 eager allocation: Gate0 allocates its KD-02 _DvpEngine only in kd02Init (KD02 canary), fed only when non-null");
		int engNew = all.Sum(t => Count(t, "new _DvpEngine(")), manNew = all.Sum(t => Count(t, "new DvpManifold(")), strNew = all.Sum(t => Count(t, "new DvpStream("));
		int latNew = all.Sum(t => Count(t, "new DvpLattice(")), pivNew = all.Sum(t => Count(t, "new DvpPivots("));
		string core = CoreSource();
		var statics = core.Split('\n').Where(l => l.Contains(" static ") && l.Contains(" new ") && !l.TrimStart().StartsWith("//") && !l.Contains("static string") && !l.Contains("static readonly DateTime") && !l.Contains("static bool") && !l.Contains("static int") && !l.Contains("static void") && !l.Contains("static double") && !l.Contains("static ulong")).ToArray();
		Check(engNew == 1 && manNew == 1 && strNew == 1 && latNew == 1 && pivNew == 1 && statics.Length == 0,
			"eager-allocation check: one new _DvpEngine (startDvp), one new DvpManifold (DvpStream slot), one new DvpStream (engine ctor), one new DvpLattice + DvpPivots (DvpStream ctor, useFib); no static buffers, the frozen-degree table is gone (" + engNew + "/" + manNew + "/" + strNew + "/" + latNew + "/" + pivNew + "/" + statics.Length + ")");
	}

	// DV-13.1/13.2: no allocation per closed bar (journal off) and none per query after warm-up (snapshot + rails, no LevelRef publish).
	static void ZeroAllocD3()
	{
		bool[] hd; RingBar[] s = Series(2000, 13, null, null, out hd);
		var rig = new Rig(Cfg(240, 2), true, false);
		for (int i = 0; i < 600; i++) { rig.Feed(s[i], hd[i]); rig.e.Snapshot(1, rig.ring, i % 2); }
		double sink = 0;
		long b0 = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 600; i < s.Length; i++)
		{
			rig.ring.Add(s[i]); rig.e.OnBar(1, rig.ring, hd[i]);
			DvpSnapshot x = rig.e.Snapshot(1, rig.ring, 1); DvpRails r = rig.e.RailsOf(1, rig.ring, 1);
			if (x != null) sink += x.sigma + r.RailPrice(ELevelKind.DvpPoc, 0) + (x.refitBar ? x.endpointDrift : 0);
		}
		long b1 = GC.GetAllocatedBytesForCurrentThread();
		Check(b1 - b0 == 0, "DV-13 zero allocations per closed bar + query over 1400 bars (" + (b1 - b0) + " bytes)");
		if (double.IsNaN(sink)) Console.WriteLine("sink NaN");
	}

	static List<string> JournalRun(RingBar[] s, bool[] hd, DvpConfig c, bool rollDetect = true, bool rthKnown = false)
	{
		var rig = new Rig(c, rollDetect, true, rthKnown);
		for (int i = 0; i < s.Length; i++) rig.Feed(s[i], hd[i]);
		rig.e.Finish(); rig.e.Finish();
		return rig.journal;
	}

	// DV-T25: running twice gives identical journals (CREATED, FIT, RUN).
	static void T25()
	{
		bool[] hd; RingBar[] s = Series(2500, 25, new[] { 1200 }, new[] { 5 }, out hd);
		var a = JournalRun(s, hd, Cfg()); var b = JournalRun(s, hd, Cfg());
		Check(a.Count > 2000 && a.SequenceEqual(b), "DV-T25 recalc: identical journals (" + a.Count + " lines)");
		Check(a[0].StartsWith("DVP CREATED", StringComparison.Ordinal) && a.Count(l => l.StartsWith("DVP CREATED", StringComparison.Ordinal)) == 1
			&& a.Count(l => l.StartsWith("DVP RUN", StringComparison.Ordinal)) == 1 && a.Skip(a.FindIndex(l => l.StartsWith("DVP RUN", StringComparison.Ordinal)) + 1).All(l => l.StartsWith("DVP CAL", StringComparison.Ordinal)),
			"DV-11.3 one DVP CREATED, one DVP RUN per stream (Finish idempotent); only PR-D8 DVP CAL lines follow it");
		string run = a.Last(l => l.StartsWith("DVP RUN", StringComparison.Ordinal));
		Check(run.Contains("rollDetect(On)") && run.Contains("diagnosticOnly(0)") && !run.Contains("meanCovIn(NaN)") && !run.Contains("meanCovFwd(NaN)") && run.Contains("fits(") ,
			"DV-11.3 DVP RUN fields: " + run);
		var fd = System.Text.RegularExpressions.Regex.Match(run, @"fwdDropped\((\d+)\)");
		Check(fd.Success && int.Parse(fd.Groups[1].Value, CI) > 0 && int.Parse(fd.Groups[1].Value, CI) <= 60,
			"DV-11.3 DVP RUN fwdDropped: cov_fwd horizons pending at the one roll are counted (" + (fd.Success ? fd.Groups[1].Value : "missing") + ", <= projBars)");
		string fit = a.First(l => l.StartsWith("DVP FIT", StringComparison.Ordinal) && l.Contains("crossesRoll(0)"));
		string[] keys = { "runId(", "configHash(", "stream(Data1)", "barTime(", " n(240)", " deg(", " bic(", "manRowsEff(", "rthShare(NaN)", "crossesRoll(0)", "sigma(", "sigmaAtr(", " r2(", " dw(", "skew(",
			"yhatEnd(", "slopeEnd(", "slope1(", "endpointDrift(", "pocZ(", "vaLoZ(", "vaHiZ(", "entropy(", "concentration(", "concentrationOcc(", "tailVol(", "inSampleCov2(", "lowR2(", "coef{" };
		Check(keys.All(k => fit.Contains(k)), "DV-11.1 DVP FIT carries every field");
		string skip = a.First(l => l.StartsWith("DVP FIT", StringComparison.Ordinal) && l.Contains("crossesRoll(1)"));
		Check(skip.Contains("sigma(NaN)") && skip.Contains("coef{NaN}"), "DV-11.1 a skipped window writes crossesRoll(1) and NaNs");
		var rth = JournalRun(s, hd, Cfg(), true, true);
		string f2 = rth.First(l => l.StartsWith("DVP FIT", StringComparison.Ordinal) && l.Contains("crossesRoll(0)"));
		Check(!f2.Contains("rthShare(NaN)"), "DV-7.7 rthShare journaled on the futures base (rthKnown), NaN on the generic base");
		var cal = new DvpConfig { enabled = true, degMode = EDvpDegMode.Calibrate };
		Check(JournalRun(s.Take(600).ToArray(), hd, cal).Any(l => l.Contains(" deg(1)") && l.Contains("calibrate(sigma=")), "DV-3.6 Calibrate journals every d <= dCap, reported d = 1");
	}

	// PR-D8 DV-3.6: the Calibrate coverage journal (DvpCalib)
	static void CalibTests()
	{
		bool[] hd; RingBar[] s = Series(2500, 25, new[] { 1200 }, null, out hd);
		// lazy: Fixed1 never calibrates -> no DvpCalib, no CAL line, no calibration fits
		var f1 = new Rig(new DvpConfig { enabled = true, winLen = 240, journalFits = true, splitFlow = false, degMode = EDvpDegMode.Fixed1 }, true, true);
		for (int i = 0; i < s.Length; i++) f1.Feed(s[i], hd[i]);
		f1.e.Finish();
		Check(f1.S.Calib == null && !f1.journal.Any(l => l.StartsWith("DVP CAL", StringComparison.Ordinal)), "DvpCalib lazy: Fixed1 allocates none and journals no DVP CAL");
		// Calibrate, journal off: nothing journaled, so nothing calibrates either; SelfCalibrate always has its calibrator (DV-0.1 exception)
		Func<DvpConfig> calc = () => { var x = Cfg(); x.degMode = EDvpDegMode.Calibrate; return x; };
		var off = new Rig(calc(), true, false);
		for (int i = 0; i < s.Length; i++) off.Feed(s[i], hd[i]);
		Check(off.S.Calib == null && off.S.CalibFits == 0, "DvpCalib lazy: Calibrate with journal off allocates none");
		var soff = new Rig(Cfg(), true, false);
		for (int i = 0; i < s.Length; i++) soff.Feed(s[i], hd[i]);
		Check(soff.S.Calib != null && soff.S.Self != null && soff.S.Calib.Years.Count == 0 && soff.S.CalibFits == soff.S.Calib.Sampled && soff.S.Calib.Sampled > 0,
			"DvpCalib SelfCalibrate journal off: the calibrator runs on its grid (" + soff.S.CalibFits + " fits), no per-year buckets");
		// Calibrate: samples on the fixed grid (calibStep = H = projBars), drops the horizon across the roll
		var rg = new Rig(calc(), true, true);
		for (int i = 0; i < s.Length; i++) rg.Feed(s[i], hd[i]);
		rg.e.Finish();
		DvpCalib c = rg.S.Calib;
		var cal = rg.journal.Where(l => l.StartsWith("DVP CAL", StringComparison.Ordinal)).ToList();
		Check(c != null && c.H == 60 && c.Step == 60 && c.Sampled > 0 && c.RollDropped >= 1 && c.HoldoutSkipped == 0 && c.Years.Keys.All(y => y == 2024),
			"DvpCalib: sampled " + (c == null ? "-" : c.Sampled + " rollDropped " + c.RollDropped + " H " + c.H));
		string sum = cal.LastOrDefault() ?? "";
		Check(cal.Count >= 3 && sum.StartsWith("DVP CAL runId(golden) stream(Data1) summary H(60) step(60) sampled(" + c.Sampled.ToString(CI) + ") rollDropped(" + c.RollDropped.ToString(CI)
				+ ") badDropped(0) gridSkipped(" + c.GridSkipped.ToString(CI) + ") holdoutSkipped(0) dStar(", StringComparison.Ordinal)
			&& cal.Any(l => l.Contains(" year(2024) d(1) windows(")) && cal.Any(l => l.Contains(" year(all) d(1) windows(") && l.Contains(" covIn(") && l.Contains(" covFwd(") && l.Contains(" covFwdFlat(") && l.Contains(" score(")),
			"DvpCalib DVP CAL lines (per year, pooled, summary): " + sum);
		var p = c.Pooled(); int ds = DvpCalib.DStar(p);
		Check(ds >= 1 && ds <= DvpFit.MaxDeg && p.n[ds] > 0 && Enumerable.Range(1, DvpFit.MaxDeg).Where(k => p.n[k] > 0).All(k => p.Score(k) >= p.Score(ds)) && sum.EndsWith("dStar(" + ds.ToString(CI) + ")", StringComparison.Ordinal),
			"DvpCalib dStar = argmin pooled score (" + ds + ")");
		bool cov = Enumerable.Range(1, DvpFit.MaxDeg).Where(k => p.n[k] > 0).All(k => p.covIn[k] / p.n[k] >= 0 && p.covIn[k] / p.n[k] <= 1 && p.covFwd[k] / p.n[k] >= 0 && p.covFwd[k] / p.n[k] <= 1 && p.covFwdFlat[k] / p.n[k] >= 0 && p.covFwdFlat[k] / p.n[k] <= 1);
		Check(cov, "DvpCalib coverages are shares in [0, 1]");
		// DStar ties go to the lower degree; nothing completed -> 0
		var b = new DvpCalib.Bucket(); Check(DvpCalib.DStar(b) == 0, "DvpCalib DStar of an empty bucket = 0");
		b.n[1] = b.n[2] = b.n[3] = 2; b.covIn[1] = b.covIn[2] = b.covIn[3] = 1.9; b.covFwd[1] = 1.5; b.covFwd[2] = 1.7; b.covFwd[3] = 1.7;
		Check(DvpCalib.DStar(b) == 2, "DvpCalib DStar: lowest score, ties to the lower d (2 over 3)");
		// holdout (Calibrate only, DV-3.10.9): a series entirely in 2025+ samples nothing; one straddling 2025-01-01 keeps only 2024 windows
		Func<int, RingBar[]> shift = days => s.Select(x => { var y = x; y.time = x.time.AddDays(days); y.tradingDay = x.tradingDay.AddDays(days); return y; }).ToArray();
		var h1 = new Rig(calc(), true, true); var s1 = shift(366);
		for (int i = 0; i < s1.Length; i++) h1.Feed(s1[i], hd[i]);
		Check(h1.S.Calib != null && h1.S.Calib.Sampled == 0 && h1.S.Calib.HoldoutSkipped > 0 && h1.S.Calib.Years.Count == 0, "DvpCalib holdout: nothing sampled on/after DvpConst.HoldoutFrom");
		var h2 = new Rig(calc(), true, true); var s2 = shift(350);
		for (int i = 0; i < s2.Length; i++) h2.Feed(s2[i], hd[i]);
		DvpCalib c2 = h2.S.Calib;
		Check(c2 != null && c2.Sampled > 0 && c2.HoldoutSkipped > 0 && c2.Years.Keys.All(y => y < 2025), "DvpCalib holdout straddle: only pre-2025 windows, horizons reaching 2025 dropped (" + (c2 == null ? "-" : c2.Sampled + "/" + c2.HoldoutSkipped) + ")");
		var h3 = new Rig(Cfg(), true, true);
		for (int i = 0; i < s1.Length; i++) h3.Feed(s1[i], hd[i]);
		Check(h3.S.Calib.Sampled > 0 && h3.S.Calib.HoldoutSkipped == 0 && h3.S.Calib.Scored > 0, "DV-3.10.9 SelfCalibrate ignores HOLDOUT_FROM (" + h3.S.Calib.Sampled + " sampled in 2025)");
		// determinism: identical CAL lines on a second run
		var rg2 = new Rig(calc(), true, true);
		for (int i = 0; i < s.Length; i++) rg2.Feed(s[i], hd[i]);
		rg2.e.Finish();
		Check(rg2.journal.Where(l => l.StartsWith("DVP CAL", StringComparison.Ordinal)).SequenceEqual(cal), "DvpCalib deterministic CAL lines");
	}

	// DV-T27: RollDetection Off -> one Warning, every stream diagnostic-only, skipShare NaN, nothing skipped.
	static void T27()
	{
		bool[] hd; RingBar[] s = Series(2000, 27, new[] { 1000 }, null, out hd);
		var rig = new Rig(Cfg(), false, true);
		for (int i = 0; i < s.Length; i++) rig.Feed(s[i], hd[i]);
		rig.e.Finish();
		string run = rig.journal.Last(l => l.StartsWith("DVP RUN", StringComparison.Ordinal));
		DvpSnapshot x = rig.e.Snapshot(1, rig.ring, 0);
		Check(rig.warn.Count(w => w == "DVP: RollDetection Off — roll skip disabled") == 1 && rig.warn.Count == 2,   // + the DVP DEGREE warming Warning (SelfCalibrate, DV-3.10.4)
			"DV-T27 one RollDetection-Off Warning (" + string.Join(" | ", rig.warn) + ")");
		Check(run.Contains("skipShare(NaN)") && run.Contains("rollDetect(Off)") && run.Contains("diagnosticOnly(1)") && double.IsNaN(rig.S.SkipShare) && rig.S.DiagnosticOnly
			&& x != null && x.diagnosticOnly && rig.S.Skipped == 0 && !rig.journal.Any(l => l.Contains("crossesRoll(1)")),
			"DV-T27 diagnostic-only, skipShare NaN (not 0), no skip: " + run);
		Check(rig.journal[0].Contains("rollDetect(Off)") && rig.journal[0].Contains("d(Data1=1(warming))"), "DV-11.3 DVP CREATED: " + rig.journal[0]);
	}

	// DV-T28 / T29: Globex sessions crossing midnight are one session; a half-day is one session flagged halfDay and its bars stay in fit windows.
	static void T28T29()
	{
		bool[] hd; RingBar[] s = Series(1200, 28, null, new[] { 4 }, out hd);
		var rig = new Rig(Cfg(), true, false);
		int newSess = 0, midnightNew = 0, halfStarts = 0; var days = new HashSet<DateTime>();
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], hd[i]);
			days.Add(s[i].tradingDay);
			if (rig.S.NewSession) { newSess++; if (s[i].time.Hour == 0 && s[i].time.Minute == 15) midnightNew++; if (rig.S.SessionHalfDay) halfStarts++; }
		}
		Check(newSess == days.Count && midnightNew == 0 && rig.S.Sessions == days.Count && s.Any(b => b.time.Hour == 0),
			"DV-T28 24h Globex session crossing midnight is one session (" + newSess + " boundaries, " + days.Count + " trading days)");
		Check(halfStarts == 1, "DV-T29 a half-day session gives one boundary with halfDay=1 (" + halfStarts + ")");
		int hStart = Array.IndexOf(hd, true), hEnd = Array.LastIndexOf(hd, true);
		var r2 = new Rig(Cfg(), true, false);
		for (int i = 0; i <= hEnd + 50; i++) r2.Feed(s[i], hd[i]);
		DvpSnapshot x = r2.e.Snapshot(1, r2.ring, 0);
		var f = new DvpFit(240); var y = new double[240]; var v = new double[240];
		for (int i = 0; i < 240; i++) { RingBar b = s[hEnd + 50 - 239 + i]; y[i] = (b.h + b.l) * 0.5; v[i] = b.v; }
		f.Fit(y, v, 240, 3, false); f.Choose(1, false);
		Check(x != null && x.n == 240 && hEnd + 50 - 239 < hStart && x.sigma == f.SigmaD, "DV-T29 half-day bars stay in the fit window (n = 240, sigma bit-identical)");
	}

	// DV-R1: golden journal. 15m, 10,000 bars (rev 6, PR-D9: long enough under the default SelfCalibrate config to leave warm-up
	// and contain a later recheck), two splices. The test regenerates the full series and every DVP FIT and DVP DEGREE line
	// deterministically and checks their SHA-256 against fixtures/golden.sha256; the committed fixtures/golden_fit_sample.txt
	// (every 20th FIT line, the lines on either side of each skipped / truncated / full transition at a roll, and every DVP DEGREE line) must appear
	// byte-identical, so a mismatch shows readable lines. DVP_WRITE_GOLDEN=1 rewrites the sample and the hashes;
	// DVP_WRITE_FULL=<dir> also dumps the full series csv and FIT journal there for inspection (not committed).
	static void R1()
	{
		bool[] hd; RingBar[] s = Series(10000, 2026, new[] { 1700, 3400 }, new[] { 12, 40 }, out hd);
		string dir = Path.Combine(Root(), "tests", "Dvp", "fixtures");
		string sampleP = Path.Combine(dir, "golden_fit_sample.txt"), hashP = Path.Combine(dir, "golden.sha256");
		var sb = new StringBuilder("bar,time,tradingDay,o,h,l,c,v,up,down,rth,rollBar,halfDay\n");
		for (int i = 0; i < s.Length; i++)
		{
			RingBar b = s[i];
			sb.Append(b.bar.ToString(CI)).Append(',').Append(b.time.ToString("yyyy-MM-dd HH:mm", CI)).Append(',').Append(b.tradingDay.ToString("yyyy-MM-dd", CI)).Append(',')
				.Append(b.o.ToString("R", CI)).Append(',').Append(b.h.ToString("R", CI)).Append(',').Append(b.l.ToString("R", CI)).Append(',').Append(b.c.ToString("R", CI)).Append(',')
				.Append(b.v.ToString("R", CI)).Append(',').Append(b.up.ToString("R", CI)).Append(',').Append(b.down.ToString("R", CI)).Append(',')
				.Append(b.rth ? 1 : 0).Append(',').Append(b.rollBar ? 1 : 0).Append(',').Append(hd[i] ? 1 : 0).Append('\n');
		}
		string series = sb.ToString();
		List<string> all = JournalRun(s, hd, Cfg());
		var fit = all.Where(l => l.StartsWith("DVP FIT", StringComparison.Ordinal)).ToList();
		var deg = all.Where(l => l.StartsWith("DVP DEGREE", StringComparison.Ordinal)).ToList();
		string run = all.FirstOrDefault(l => l.StartsWith("DVP RUN", StringComparison.Ordinal)) ?? "";
		string full = string.Join("\n", fit) + "\n", degFull = string.Join("\n", deg) + "\n";
		var sample = new StringBuilder();
		for (int i = 0; i < fit.Count; i++)
		{
			Func<string, int> kind = l => l.Contains("crossesRoll(1)") ? 2 : l.Contains(" winEff(") ? 1 : 0;   // skipped / truncated at a roll (DV-7.1, 14:09 CT) / full
			int cr = kind(fit[i]);
			bool edge = (i > 0 && kind(fit[i - 1]) != cr) || (i + 1 < fit.Count && kind(fit[i + 1]) != cr);
			if (i % 20 == 0 || edge || i == fit.Count - 1) sample.Append(i.ToString(CI)).Append('\t').Append(fit[i]).Append('\n');
		}
		for (int i = 0; i < deg.Count; i++) sample.Append("deg").Append(i.ToString(CI)).Append('\t').Append(deg[i]).Append('\n');
		string hashes = "series " + Sha256(series) + "\nfit " + Sha256(full) + "\nfitLines " + fit.Count.ToString(CI) + "\ndegree " + Sha256(degFull)
			+ "\ndegreeLines " + deg.Count.ToString(CI) + "\n";
		if (Environment.GetEnvironmentVariable("DVP_WRITE_GOLDEN") == "1") { File.WriteAllText(sampleP, sample.ToString()); File.WriteAllText(hashP, hashes); Console.WriteLine("wrote golden sample + hashes"); }
		string dump = Environment.GetEnvironmentVariable("DVP_WRITE_FULL");
		if (!string.IsNullOrEmpty(dump)) { File.WriteAllText(Path.Combine(dump, "golden_series.csv"), series); File.WriteAllText(Path.Combine(dump, "golden_fit.txt"), full); }
		bool hashSame = File.Exists(hashP) && File.ReadAllText(hashP) == hashes;
		bool sampleSame = File.Exists(sampleP) && File.ReadAllText(sampleP) == sample.ToString();
		if (!sampleSame && File.Exists(sampleP))
		{
			string[] want = File.ReadAllLines(sampleP), got = sample.ToString().TrimEnd('\n').Split('\n');
			for (int i = 0; i < Math.Min(want.Length, got.Length); i++) if (want[i] != got[i]) { Console.WriteLine("  R1 first sample diff:\n   want " + want[i] + "\n   got  " + got[i]); break; }
		}
		int warmIdx = run.IndexOf(",warmEnd=", StringComparison.Ordinal);
		bool warmed = warmIdx > 0 && !run.Contains(",warmEnd=NaN,");
		// rechecks after the one that ended warm-up: sel lines after the first whose n reached calibMinScored
		int firstWarm = deg.FindIndex(l => l.StartsWith("DVP DEGREE sel", StringComparison.Ordinal) && int.Parse(l.Substring(l.LastIndexOf("n(", StringComparison.Ordinal) + 2).TrimEnd(')'), CI) >= Cfg().calibMinScored);
		int later = firstWarm < 0 ? 0 : deg.Skip(firstWarm + 1).Count(l => l.StartsWith("DVP DEGREE sel", StringComparison.Ordinal));
		Check(hashSame && sampleSame && fit.Count == 10000 - 239 && fit.Count(l => l.Contains("crossesRoll(1)")) == 2 * (DvpConst.RollMinBars - 1)
			&& fit.Count(l => l.Contains(" winEff(")) == 2 * (240 - DvpConst.RollMinBars) && warmed && later >= 1
			&& fit.Any(l => l.Contains("warming(0)")) && fit.Any(l => l.Contains("warming(1)")),
			"DV-R1 golden journal: " + fit.Count + " DVP FIT lines and " + deg.Count + " DVP DEGREE lines, full-run SHA-256 matches fixtures/golden.sha256 (" + hashSame
			+ "), sample byte-identical (" + sampleSame + "), leaves warm-up (" + warmed + ") with " + later + " later recheck(s)");
	}

	static string Sha256(string text)
	{
		using (var h = System.Security.Cryptography.SHA256.Create())
		{
			byte[] d = h.ComputeHash(Encoding.UTF8.GetBytes(text)); var sb = new StringBuilder();
			foreach (byte x in d) sb.Append(x.ToString("x2", CI));
			return sb.ToString();
		}
	}

	// DV-3.8 endpointDrift (accepted reading, Analyst 2 review of #36), refitEvery = 4: on a refit bar whose window ends at E,
	// drift = (newFit.Yhat(bar E-r) - prevFit.Yhat(bar E-r)) / ATR(t-1), where prevFit is the refit window ending at E-r.
	// NaN on non-refit bars, when the previous window is skipped at a roll (fewer than RollMin post-roll bars), or when it is no
	// longer in the ring. DV-7.1 (Ettienne 2026-11-23 14:09 CT): windows after a roll are truncated at the roll bar, so both fits
	// may be truncated (each to its own bars since the roll). Checked against an independent DvpFit on the raw series.
	static void TDriftR4()
	{
		const int n = 120, r = 4;
		bool[] hd; RingBar[] s = Series(1500, 77, new[] { 900 }, null, out hd);
		var rig = new Rig(Cfg(n, r, false), true, false);
		var nf = new DvpFit(n); var pf = new DvpFit(n);
		double[] y = new double[n], v = new double[n];
		int finite = 0, nanRoll = 0, nanWarm = 0, bad = 0, nonRefitNonNaN = 0, truncFinite = 0; double maxErr = 0;
		int rollMin = Math.Min(DvpConst.RollMinBars, n);
		Func<int, int> winOf = endIdx =>   // DV-7.1: window length ending at endIdx (0 = skipped at a roll)
		{
			for (int k = endIdx; k >= Math.Max(1, endIdx - n + 2); k--) if (s[k].rollBar) { int ne = endIdx - k + 1; return ne >= rollMin ? ne : 0; }
			return n;
		};
		Func<DvpFit, int, bool> fitEnd = (f, endIdx) =>
		{
			int m = winOf(endIdx);
			if (m == 0 || endIdx - m + 1 < 0) return false;
			for (int k = 0; k < m; k++) { RingBar b = s[endIdx - m + 1 + k]; y[k] = (b.h + b.l) / 2; v[k] = b.v; }
			return f.Fit(y, v, m, 3, false);
		};
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], hd[i]);
			DvpSnapshot x = rig.e.SnapshotRaw(1, rig.ring, 0);
			if (x == null || !x.valid) continue;
			if (!x.refitBar) { if (!double.IsNaN(x.endpointDrift)) nonRefitNonNaN++; continue; }
			int e = x.anchorBar - 1, pe = e - r;           // series indices of the new and previous window ends
			bool prevCrosses = pe >= 0 && winOf(pe) == 0;
			if (pe - n + 1 < 0) { if (double.IsNaN(x.endpointDrift)) nanWarm++; else bad++; continue; }
			if (prevCrosses) { if (double.IsNaN(x.endpointDrift)) nanRoll++; else bad++; continue; }
			if (!fitEnd(nf, e) || !fitEnd(pf, pe)) { bad++; continue; }
			double want = (nf.Yhat(nf.U(nf.n - 1 - r), 1) - pf.Yhat(1, 1)) / x.atr;
			double err = Math.Abs(want - x.endpointDrift);
			if (!(err <= 1e-9 * Math.Max(1, Math.Abs(want)))) bad++; else { finite++; if (nf.n < n) truncFinite++; }
			maxErr = Math.Max(maxErr, double.IsNaN(err) ? double.PositiveInfinity : err);
		}
		Console.WriteLine("  endpointDrift r=4: finite " + finite + ", NaN roll " + nanRoll + ", NaN out-of-ring " + nanWarm + ", max err " + maxErr.ToString("G3", CI));
		Check(bad == 0 && nonRefitNonNaN == 0 && finite > 200 && nanRoll > 0 && truncFinite > 0,
			"DV-3.8 endpointDrift r=4: " + finite + " refits match independent fit (" + truncFinite + " truncated at the roll; max err " + maxErr.ToString("G3", CI) + "), " + nanRoll
			+ " NaN across roll, " + nanWarm + " NaN prev window out of range, 0 non-refit values (bad " + bad + ", nonRefit " + nonRefitNonNaN + ")");
	}

	static void D3Tests()
	{
		T14(); T15(); T16(); T16Trunc(); T16Mixed(); RollBarTest(); T16b(); T22(); ZeroAllocD3(); T25(); CalibTests(); T27(); T28T29(); TDriftR4(); R1();
	}
}
