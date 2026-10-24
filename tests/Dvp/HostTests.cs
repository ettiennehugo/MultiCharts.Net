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

	// DV-T16: rolls. Windows containing a splice are skipped, a window starting on the roll bar is clean, fits resume after n
	// post-roll bars, skipShare exact and diagnosticOnly at 0.30 +- one window, ATR ResetPrev on the roll bar.
	static void T16()
	{
		const int n = 240; int[] rolls = { 1800, 3600 };
		bool[] hd; RingBar[] s = Series(5000, 16, rolls, null, out hd);
		var rig = new Rig(Cfg(n), true, false);
		int wrongSkip = 0, wrongValid = 0, startOnRoll = 0, firstAfter = -1;
		var atr = new Gate0Atr(DvpConst.AtrLen); int atrBad = 0;
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], hd[i]);
			if (s[i].rollBar) atr.ResetPrev();
			atr.Add(s[i].h, s[i].l, s[i].c);
			DvpSnapshot x = rig.e.SnapshotRaw(1, rig.ring, 0);   // window ends at e = i (serves bar i+1)
			if (BitConverter.DoubleToInt64Bits(x.atr) != BitConverter.DoubleToInt64Bits(atr.Value)) atrBad++;
			if (i < n - 1) continue;
			bool cross = rolls.Any(R => R >= i - n + 2 && R <= i);
			if (x.crossesRoll != cross) wrongSkip++;
			if (x.valid == cross) wrongValid++;
			if (rolls.Any(R => i == R + n - 1) && x.valid) startOnRoll++;
			if (i > 1800 && firstAfter < 0 && x.valid) firstAfter = i;
			if (cross && (rig.e.Snapshot(1, rig.ring, 0) != null || rig.e.RailsOf(1, rig.ring, 0) != null)) wrongValid++;
		}
		Check(wrongSkip == 0 && wrongValid == 0, "DV-T16 crossesRoll iff a roll bar is at i in [1, n-1] of the window; skipped windows have no rails (" + wrongSkip + "/" + wrongValid + ")");
		Check(startOnRoll == 2, "DV-T16 a window starting on the roll bar is clean (" + startOnRoll + "/2)");
		Check(firstAfter == 1800 + n - 1, "DV-T16 fits resume once n post-roll bars have accrued (first at " + firstAfter + ")");
		DvpStream st = rig.S;
		Check(st.Skipped == 2 * (n - 1) && st.Eligible == s.Length - (n - 1) && st.SkipShare == (double)(2 * (n - 1)) / (s.Length - (n - 1)) && !st.DiagnosticOnly,
			"DV-T16 skipShare = 2(n-1)/eligible (" + st.Skipped + "/" + st.Eligible + ")");
		Check(atrBad == 0, "DV-T16 ATR(20) as of t-1 with ResetPrev on the roll bar (" + atrBad + " differ)");
		var noReset = new Gate0Atr(DvpConst.AtrLen); for (int i = 0; i <= 1800; i++) noReset.Add(s[i].h, s[i].l, s[i].c);
		var rs = new Rig(Cfg(n), true, false); for (int i = 0; i <= 1800; i++) rs.Feed(s[i], false);
		Check(rs.e.SnapshotRaw(1, rs.ring, 0).atr < noReset.Value, "DV-T16 ResetPrev drops the splice gap from the roll bar's true range");
		// diagnosticOnly at 0.30 +- one window: skipped = 478; eligible 1590 -> 0.3006 (diagnostic), 1600 -> 0.29875 (not)
		foreach (int E in new[] { 1590, 1600 })
		{
			bool[] h2; RingBar[] s2 = Series(E + n - 1, 30, new[] { 400, 1000 }, null, out h2);
			var r2 = new Rig(Cfg(n), true, false); for (int i = 0; i < s2.Length; i++) r2.Feed(s2[i], false);
			Check(r2.S.Eligible == E && r2.S.DiagnosticOnly == (E == 1590), "DV-T16 diagnosticOnly at skipShare " + r2.S.SkipShare.ToString("0.0000", CI) + " = " + r2.S.DiagnosticOnly);
		}
		var rr = new Rig(Cfg(n, 3), true, false); for (int i = 0; i < s.Length; i++) rr.Feed(s[i], false);
		Check(rr.S.Skipped > 0 && rr.S.Skipped <= 2 * (n - 1 + 2), "DV-T16 skip counting with refitEvery = 3 (" + rr.S.Skipped + ")");
	}

	// rollOn(1) -> RingBar.rollBar: the host's ringBarOf sets rollBar = (ago == 0 && rollOn(ds)) and rollOn(1) is
	// m_rollDet.LastRollBar == current bar. Simulated with the real _RollDetector on closed bars plus intrabar ticks.
	static void RollBarTest()
	{
		string host = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		Check(host.Contains("rollBar = ago == 0 && rollOn(ds)") && host.Contains("if (ds == 1) return m_rollDet.LastRollBar == Bars.FullSymbolData.Current;"),
			"rollOn(1) host wiring: ringBarOf sets rollBar from rollOn on the closed bar only");
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
		string host = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		int sd = host.IndexOf("private void startDvp()", StringComparison.Ordinal);
		string body = sd >= 0 ? host.Substring(sd, host.IndexOf("private void stepDvp()", sd, StringComparison.Ordinal) - sd) : "";
		int ret = body.IndexOf("if (!DvpConfig.enabled) return;", StringComparison.Ordinal);
		Check(ret > 0 && ret < body.IndexOf("requireRing(", StringComparison.Ordinal) && ret < body.IndexOf("new _DvpEngine(", StringComparison.Ordinal) && !body.Contains("PriceAction"),
			"DV-T22 disabled: startDvp returns before requireRing and the engine; no PA instance");
		int lst = body.IndexOf("new List<LevelRef>()", StringComparison.Ordinal);
		string noBody = System.Text.RegularExpressions.Regex.Replace(host.Replace(body, ""), @"//[^\n]*", "");
		Check(ret > 0 && lst > ret && !noBody.Contains("new List<LevelRef>") && noBody.Contains("private List<LevelRef> m_dvpRails, m_dvpRailPool;"),
			"DV-T22/DV-13.1 disabled: the DVP rail lists are allocated only in startDvp after the enabled check (no field initialiser)");
		Check(host.Contains("if (m_dvp != null) stepDvp();") && host.Contains("Dvp queried but DvpConfig.enabled is false") && host.Contains("startVpHelpers();   // Phase 3")
			&& host.IndexOf("startDvp();", StringComparison.Ordinal) > host.IndexOf("startVpHelpers();   // Phase 3", StringComparison.Ordinal),
			"DV-T22 one null check per CalcBar, one Warning on query; startDvp runs after startVpHelpers (which clears ring requests)");
		// eager allocation: the row arrays (DvpManifold) and every window buffer are built only under the _DvpEngine constructor
		var all = Directory.GetFiles(Root(), "*.CS").Select(File.ReadAllText).ToArray();
		int engNew = all.Sum(t => Count(t, "new _DvpEngine(")), manNew = all.Sum(t => Count(t, "new DvpManifold(")), strNew = all.Sum(t => Count(t, "new DvpStream("));
		string core = CoreSource();
		var statics = core.Split('\n').Where(l => l.Contains(" static ") && l.Contains(" new ") && !l.TrimStart().StartsWith("//") && !l.Contains("static string") && !l.Contains("static bool") && !l.Contains("static int") && !l.Contains("static void") && !l.Contains("static double") && !l.Contains("static ulong")).ToArray();
		Check(engNew == 1 && manNew == 1 && strNew == 1 && statics.Length == 1 && statics[0].Contains("Entry[] Table = new Entry[0]"),
			"eager-allocation check: one new _DvpEngine (startDvp), one new DvpManifold (DvpStream slot), one new DvpStream (engine ctor); no static buffers (" + engNew + "/" + manNew + "/" + strNew + "/" + statics.Length + ")");
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
			&& a.Count(l => l.StartsWith("DVP RUN", StringComparison.Ordinal)) == 1 && a[a.Count - 1].StartsWith("DVP RUN", StringComparison.Ordinal),
			"DV-11.3 one DVP CREATED, one DVP RUN per stream (Finish idempotent)");
		string run = a[a.Count - 1];
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

	// DV-T27: RollDetection Off -> one Warning, every stream diagnostic-only, skipShare NaN, nothing skipped.
	static void T27()
	{
		bool[] hd; RingBar[] s = Series(2000, 27, new[] { 1000 }, null, out hd);
		var rig = new Rig(Cfg(), false, true);
		for (int i = 0; i < s.Length; i++) rig.Feed(s[i], hd[i]);
		rig.e.Finish();
		string run = rig.journal.Last();
		DvpSnapshot x = rig.e.Snapshot(1, rig.ring, 0);
		Check(rig.warn.Count(w => w == "DVP: RollDetection Off — roll skip disabled") == 1 && rig.warn.Count == 2,   // + the DevCoverageFrozen degree warning
			"DV-T27 one RollDetection-Off Warning (" + string.Join(" | ", rig.warn) + ")");
		Check(run.Contains("skipShare(NaN)") && run.Contains("rollDetect(Off)") && run.Contains("diagnosticOnly(1)") && double.IsNaN(rig.S.SkipShare) && rig.S.DiagnosticOnly
			&& x != null && x.diagnosticOnly && rig.S.Skipped == 0 && !rig.journal.Any(l => l.Contains("crossesRoll(1)")),
			"DV-T27 diagnostic-only, skipShare NaN (not 0), no skip: " + run);
		Check(rig.journal[0].Contains("rollDetect(Off)") && rig.journal[0].Contains("d(Data1=1(calibrate,unfrozen))"), "DV-11.3 DVP CREATED: " + rig.journal[0]);
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

	// DV-R1: golden journal. 15m, 5,000 bars, two splices. The test regenerates the full series and every DVP FIT line
	// deterministically and checks their SHA-256 against fixtures/golden.sha256; the committed fixtures/golden_fit_sample.txt
	// (every 20th FIT line plus the lines on either side of each crossesRoll transition) must appear byte-identical, so a
	// mismatch shows readable lines. RAIL lines arrive in PR-D5. DVP_WRITE_GOLDEN=1 rewrites the sample and the hashes;
	// DVP_WRITE_FULL=<dir> also dumps the full series csv and FIT journal there for inspection (not committed).
	static void R1()
	{
		bool[] hd; RingBar[] s = Series(5000, 2026, new[] { 1700, 3400 }, new[] { 12, 40 }, out hd);
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
		var fit = JournalRun(s, hd, Cfg()).Where(l => l.StartsWith("DVP FIT", StringComparison.Ordinal)).ToList();
		string full = string.Join("\n", fit) + "\n";
		var sample = new StringBuilder();
		for (int i = 0; i < fit.Count; i++)
		{
			bool cr = fit[i].Contains("crossesRoll(1)");
			bool edge = (i > 0 && fit[i - 1].Contains("crossesRoll(1)") != cr) || (i + 1 < fit.Count && fit[i + 1].Contains("crossesRoll(1)") != cr);
			if (i % 20 == 0 || edge || i == fit.Count - 1) sample.Append(i.ToString(CI)).Append('\t').Append(fit[i]).Append('\n');
		}
		string hashes = "series " + Sha256(series) + "\nfit " + Sha256(full) + "\nfitLines " + fit.Count.ToString(CI) + "\n";
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
		Check(hashSame && sampleSame && fit.Count == 5000 - 239 && fit.Count(l => l.Contains("crossesRoll(1)")) == 2 * 239,
			"DV-R1 golden journal: " + fit.Count + " DVP FIT lines, full-run SHA-256 matches fixtures/golden.sha256 (" + hashSame + "), sample byte-identical (" + sampleSame + ")");
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
	// NaN on non-refit bars, when the previous window crosses a roll, or when it is no longer in the ring. Checked against an
	// independent DvpFit on the raw series.
	static void TDriftR4()
	{
		const int n = 120, r = 4;
		bool[] hd; RingBar[] s = Series(1500, 77, new[] { 900 }, null, out hd);
		var rig = new Rig(Cfg(n, r, false), true, false);
		var nf = new DvpFit(n); var pf = new DvpFit(n);
		double[] y = new double[n], v = new double[n];
		int finite = 0, nanRoll = 0, nanWarm = 0, bad = 0, nonRefitNonNaN = 0; double maxErr = 0;
		Func<DvpFit, int, bool> fitEnd = (f, endIdx) =>
		{
			if (endIdx - n + 1 < 0) return false;
			for (int k = 0; k < n; k++) { RingBar b = s[endIdx - n + 1 + k]; y[k] = (b.h + b.l) / 2; v[k] = b.v; }
			return f.Fit(y, v, n, 3, false);
		};
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], hd[i]);
			DvpSnapshot x = rig.e.SnapshotRaw(1, rig.ring, 0);
			if (x == null || !x.valid) continue;
			if (!x.refitBar) { if (!double.IsNaN(x.endpointDrift)) nonRefitNonNaN++; continue; }
			int e = x.anchorBar - 1, pe = e - r;           // series indices of the new and previous window ends
			bool prevCrosses = false;
			for (int k = Math.Max(1, pe - n + 2); k <= pe && k < s.Length; k++) if (s[k].rollBar) prevCrosses = true;
			if (pe - n + 1 < 0) { if (double.IsNaN(x.endpointDrift)) nanWarm++; else bad++; continue; }
			if (prevCrosses) { if (double.IsNaN(x.endpointDrift)) nanRoll++; else bad++; continue; }
			if (!fitEnd(nf, e) || !fitEnd(pf, pe)) { bad++; continue; }
			double want = (nf.Yhat(nf.U(n - 1 - r), 1) - pf.Yhat(1, 1)) / x.atr;
			double err = Math.Abs(want - x.endpointDrift);
			if (!(err <= 1e-9 * Math.Max(1, Math.Abs(want)))) bad++; else finite++;
			maxErr = Math.Max(maxErr, double.IsNaN(err) ? double.PositiveInfinity : err);
		}
		Console.WriteLine("  endpointDrift r=4: finite " + finite + ", NaN roll " + nanRoll + ", NaN out-of-ring " + nanWarm + ", max err " + maxErr.ToString("G3", CI));
		Check(bad == 0 && nonRefitNonNaN == 0 && finite > 200 && nanRoll > 0,
			"DV-3.8 endpointDrift r=4: " + finite + " refits match independent fit (max err " + maxErr.ToString("G3", CI) + "), " + nanRoll
			+ " NaN across roll, " + nanWarm + " NaN prev window out of range, 0 non-refit values (bad " + bad + ", nonRefit " + nonRefitNonNaN + ")");
	}

	static void D3Tests()
	{
		T14(); T15(); T16(); RollBarTest(); T16b(); T22(); ZeroAllocD3(); T25(); T27(); T28T29(); TDriftR4(); R1();
	}
}
