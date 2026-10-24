using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PowerLanguage.Function;

// M-DVP PR-D6 (specs/m-dvp-spec.md rev 5.1 §16 item 8, DV-9): DV-T21 on the MC-free _DvpEngine driven the way _CustomSignal
// drives Data1 + Data2 (DvpHtf.ShouldStep / EndAgo, _RollPending for the same-root Data2 roll), plus P2 gate and HTF query helpers.
static partial class Program
{
	// 60m bars from 15m Data1: HTF bar k closes with Data1 bar 4k+3. Returns per-Data1-bar the forming/closed HTF state.
	sealed class HtfRig
	{
		public _DvpEngine e; public _BarRing r1, r2; public List<string> warn = new List<string>();
		public HtfRig(DvpConfig c, bool rollDetect)
		{
			List<string> w = warn;
			e = new _DvpEngine(c, "golden", "ES", 0.25, rollDetect, false, null, s => w.Add(s));
			r1 = new _BarRing(DvpStream.RingNeed(c)); r2 = new _BarRing(DvpStream.RingNeed(c));
		}
	}

	static readonly List<int> s_htfRollBars = new List<int>();

	static DvpConfig HtfCfg() { var c = Cfg(60); c.streams = new[] { 1, 2 }; return c; }

	static RingBar Agg(RingBar[] s, int k, int upto, int htfBarNo)   // HTF bar k from Data1 bars 4k..upto (upto < 4k+3: forming)
	{
		RingBar a = s[4 * k]; a.bar = htfBarNo; a.rollBar = false;
		for (int i = 4 * k + 1; i <= upto; i++) { a.h = Math.Max(a.h, s[i].h); a.l = Math.Min(a.l, s[i].l); a.c = s[i].c; a.v += s[i].v; a.up += s[i].up; a.down += s[i].down; a.time = s[i].time; }
		return a;
	}

	// Host replay: every Data1 bar is offered as an intrabar tick (closed=false) then closed; Data2 is offered on every Data1 close
	// with its own status (closed only on the 4th Data1 bar). Records the Data2 query a Data1 bar sees (intrabar and on close).
	static List<string> HtfReplay(RingBar[] s, bool[] hd, bool offerForming, int[] data1Rolls, out HtfRig rig, List<int> endBarOk)
	{
		rig = new HtfRig(HtfCfg(), true);
		var rp = new _RollPending(); var rollSet = new HashSet<int>(data1Rolls ?? new int[0]);
		var outp = new List<string>(); int last2 = int.MinValue;
		for (int i = 0; i < s.Length; i++)
		{
			int k = i / 4, cur2 = k + 1; bool close2 = i % 4 == 3;
			for (int pass = 0; pass < 2; pass++)
			{
				bool closed1 = pass == 1;
				if (closed1) { rig.r1.Add(s[i]); rig.e.OnBar(1, rig.r1, hd[i]); if (rollSet.Contains(i)) rp.Arm(s[i].time); }
				bool c2 = closed1 && close2;
				if (!offerForming && !c2) continue;   // truncated HTF series: the forming Data2 bar does not exist
				if (DvpHtf.ShouldStep(c2, last2, cur2))
				{
					RingBar hb = Agg(s, k, i, cur2); hb.rollBar = rp.Fire(cur2, hb.time);
					if (hb.rollBar) s_htfRollBars.Add(cur2);
					rig.r2.Add(hb); rig.e.OnBar(2, rig.r2, hd[i]); last2 = cur2;
				}
				int ago = DvpHtf.EndAgo(c2, rig.r2.Count, rig.r2.Count > 0 ? rig.r2[0].bar : -1, cur2);
				DvpSnapshot sn = rig.e.SnapshotRaw(2, rig.r2, ago);
				outp.Add(i + "/" + pass + " " + Sig(sn, rig.e.RailsOf(2, rig.r2, ago)));
				if (endBarOk != null && sn != null)   // DV-9.2: the read ends on the last closed HTF bar (as-of t-1 when Data2 just closed)
					endBarOk.Add(sn.endBar == (c2 ? cur2 - 1 : k) ? 1 : 0);
			}
		}
		return outp;
	}

	static void T21()
	{
		bool[] hd; RingBar[] s = Series(4 * 400, 21, null, null, out hd);
		HtfRig a, b; var endOk = new List<int>();
		List<string> full = HtfReplay(s, hd, true, null, out a, endOk);
		List<string> trunc = HtfReplay(s, hd, false, null, out b, null);
		// map: truncated rows exist only on Data2 closes; compare those, and check intrabar/forming reads equal the previous close's read
		var byKey = full.ToDictionary(x => x.Substring(0, x.IndexOf(' ')), x => x.Substring(x.IndexOf(' ') + 1));
		int bad = 0, valid = 0;
		foreach (string t in trunc) { string key = t.Substring(0, t.IndexOf(' ')); if (byKey[key] != t.Substring(t.IndexOf(' ') + 1)) bad++; if (t.Contains("valid=True")) valid++; }
		Check(bad == 0 && valid > trunc.Count / 2, "DV-T21 forming Data2 bar never changes a Data1 snapshot: " + trunc.Count + " closes, " + valid + " valid, " + bad + " differ from the truncated HTF series");
		Check(endOk.Count > 0 && endOk.All(x => x == 1), "DV-T21 Data1 read maps to the last closed HTF bar (" + endOk.Count(x => x == 0) + " of " + endOk.Count + " wrong)");
		Check(a.r2.Count > 0 && a.r2[0].bar == 400 && a.e.Stream(2).FitsComputed > 0, "DV-T21 Data2 stepped once per closed HTF bar (" + a.r2[0].bar + ")");
		// mutation: if the host added the forming bar, Data1 reads would change (the test above is sensitive)
		var m = new HtfRig(HtfCfg(), true); int lastM = int.MinValue; string sigOk = null, sigBad = null;
		for (int i = 0; i < 4 * 200 + 2; i++) { m.r1.Add(s[i]); m.e.OnBar(1, m.r1, hd[i]); if (i % 4 == 3) { m.r2.Add(Agg(s, i / 4, i, i / 4 + 1)); m.e.OnBar(2, m.r2, hd[i]); lastM = i / 4 + 1; } }
		sigOk = Sig(m.e.SnapshotRaw(2, m.r2, 0), null);
		m.r2.Add(Agg(s, 200, 4 * 200 + 1, 201)); m.e.OnBar(2, m.r2, false); sigBad = Sig(m.e.SnapshotRaw(2, m.r2, 0), null);
		Check(sigOk.Contains("valid=True") && sigOk != sigBad && !DvpHtf.ShouldStep(false, lastM, 201), "DV-T21 mutation: stepping a forming Data2 bar would change the read; ShouldStep refuses it");

		// same-root Data2 roll: the Data1 roll arms _RollPending; the first closed Data2 bar at/after it fires (rollBar), windows skip
		bool[] hd2; RingBar[] s2 = Series(4 * 400, 22, new[] { 4 * 200 + 1 }, null, out hd2);
		HtfRig rr; s_htfRollBars.Clear(); HtfReplay(s2, hd2, true, new[] { 4 * 200 + 1 }, out rr, null);
		int rollBars = s_htfRollBars.Count, rollAt = rollBars > 0 ? s_htfRollBars[0] : -1;
		DvpStream st2 = rr.e.Stream(2);
		Check(rollBars == 1 && rollAt == 201 && st2.Skipped > 0, "DV-T21 same-root Data2 roll detected: rollBar on Data2 bar " + rollAt + " (" + rollBars + "), " + st2.Skipped + " roll-crossing windows skipped");

		// different root rejected; same root (any month, any case) accepted
		Check(DvpHtf.RootError(2, "ESZ24", "ES", "ESZ24", "ES") == null && DvpHtf.RootError(3, "ESZ24", "ES", "@es", "es") == null
			&& DvpHtf.RootError(2, "ESZ24", "ES", "NQZ24", "NQ") != null && DvpHtf.RootError(1, "ESZ24", "ES", "x", "NQ") == null, "DV-T21 different root on Data2/3 rejected");
		string host = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		Check(host.Contains("string rerr = DvpHtf.RootError(ds,") && host.Contains("if (rerr != null) throw new ArgumentException(rerr);")
			&& host.Contains("requireDataStream(\"DvpConfig.streams\", ds);") && !host.Contains("HTF streams arrive in PR-D6"), "DV-T21 host: startDvp hosts Data2/3 with the root check");
		Check(host.Contains("return ring == null ? 0 : DvpHtf.EndAgo("), "DV-9.2 host: dvpEndAgo uses DvpHtf.EndAgo");

		// P2 gate
		Check(!DvpConst.P2_PASSED && a.e.Tradeable(1) && !a.e.Tradeable(2) && !a.e.Tradeable(2) && !a.e.Tradeable(3)
			&& a.warn.Count(w => w == "DVP Data2 used before Probe P2") == 1, "DV-T21 Tradeable(2) false while P2_PASSED is false, one Warning (" + a.warn.Count(w => w.Contains("Probe P2")) + ")");
		Check(HtfCfg().Canon().Contains(";P2_PASSED=0}") && CreatedP2(), "DV-9.3 / DV-11.3 P2 state in Canon() and DVP CREATED");

		// HtfNextRailDistAtr core
		var rails = new DvpRails(2); SetRails(rails, new[] { 100.0, 104.0, 110.0, double.NaN });
		var lat = new DvpLattice(1); lat.Price[0] = 102.0; lat.Price[1] = 96.0; lat.Count = 2;
		Check(DvpHtf.NextRailDistAtr(rails, null, 101, 1, 2) == 1.5 && DvpHtf.NextRailDistAtr(rails, lat, 101, 1, 2) == 0.5
			&& DvpHtf.NextRailDistAtr(rails, lat, 101, -1, 2) == 0.5 && DvpHtf.NextRailDistAtr(rails, lat, 100, -1, 2) == 2.0
			&& double.IsNaN(DvpHtf.NextRailDistAtr(rails, lat, 110, 1, 2)) && double.IsNaN(DvpHtf.NextRailDistAtr(rails, lat, 101, 0, 2))
			&& double.IsNaN(DvpHtf.NextRailDistAtr(rails, lat, 101, 1, double.NaN)), "DV-9.4 NextRailDistAtr: nearest strictly beyond C(t-1), model + lattice, NaN when none");
		Check(host.Contains("protected bool dvpHtfAccepted(int ds, LevelRef rl, int dir, int N, bool countTrigger, double deltaAtr)")
			&& host.Contains("protected double dvpHtfNextRailDistAtr(int ds, int dir)") && host.Contains("protected bool dvpTradeable(int ds)"), "DV-10 host API: dvpTradeable / dvpHtfAccepted / dvpHtfNextRailDistAtr");
	}

	static bool CreatedP2()
	{
		var j = new List<string>();
		new _DvpEngine(HtfCfg(), "golden", "ES", 0.25, true, false, x => j.Add(x), x => { });
		return j.Count > 0 && j[0].Contains(" p2(notRun) ");
	}

	static void SetRails(DvpRails r, double[] prices)
	{
		for (int i = 0; i < prices.Length; i++) r.Items[i] = new DvpRail { kind = ELevelKind.DvpVah, z = i, price = prices[i] };
		typeof(DvpRails).GetProperty("Count").SetValue(r, prices.Length);
	}

	static void D6Tests() { T21(); }
}
