using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PowerLanguage.Function;

// M-DVP PR-D5 (specs/m-dvp-spec.md rev 5 DV-6, DV-8, DV-11.2, RG-10): DV-T18a-e confluence, DV-T19/T19b acceptance, DV-T24 RG-10
// sign/units, rail history path independence, the DVP RAIL journal, zero allocation and host source scans.
static partial class Program
{
	static DvpConfluence Conf(params object[] q)   // (kind, price, range) triples
	{
		var c = new DvpConfluence(64);
		for (int i = 0; i < q.Length; i += 3) c.Add((ELevelKind)q[i], Convert.ToDouble(q[i + 1], CI), (int)q[i + 2]);
		return c;
	}

	static void T18()
	{
		const ELevelKind Poc = ELevelKind.DvpPoc, Vah = ELevelKind.DvpVah, Hvn = ELevelKind.DvpHvn, Sig = ELevelKind.DvpSigma, Fib = ELevelKind.DvpFib;
		// T18a: three families within 0.30 ATR (ATR 10 -> tol 3) qualify; two families do not.
		var c = Conf(Poc, 100.0, -1, Sig, 101.0, -1, Fib, 102.0, 0, Sig, 150.0, -1, Fib, 151.0, 1);
		c.Cluster(10, 100, 0.30);
		Check(c.ClusterCount == 2 && c.QualifyingCount == 1 && c.ClusterFamilies[0] == 3 && c.ClusterSize[0] == 3 && c.ClusterRanges[0] == 1 && c.ClusterFamilies[1] == 2 && !c.Qualifies(1),
			"DV-T18a 3 families qualify (families = 3), 2 families do not");
		Check(c.CandFamilies(0) == 3 && c.CandFamilies(3) == 2 && DvpConfluence.Family(ELevelKind.DvpConf) < 0 && !c.Add(ELevelKind.DvpConf, 100, -1),
			"DV-T18a rail score = its cluster's families; DvpConf (and the cone) are not candidates");
		// T18b: seed order F, then K, then |P - C|, then lowest price; the cluster price is the seed's.
		c = Conf(Poc, 100.0, -1, Sig, 102.9, -1, Fib, 105.8, 0);   // 102.9 reaches both (F=3), the ends reach 2 families
		c.Cluster(10, 100, 0.30);
		Check(c.ClusterPrice[0] == 102.9 && c.ClusterFamilies[0] == 3, "DV-T18b max-F seed wins over closer-to-close candidates (seed price 102.9)");
		c = Conf(Poc, 100.0, -1, Hvn, 100.5, -1, Sig, 101.0, -1, Sig, 103.8, -1);   // F tie (2): 101 has K=3 (100,100.5,101) vs ...
		c.Cluster(10, 0, 0.30);
		Check(c.ClusterPrice[0] == 101.0 || c.ClusterPrice[0] == 100.5, "DV-T18b K tiebreak");
		c = Conf(Poc, 100.0, -1, Sig, 100.0, -1, Poc, 200.0, -1, Sig, 200.0, -1);
		c.Cluster(10, 199, 0.30);
		Check(c.ClusterPrice[0] == 200.0, "DV-T18b |P - C(t-1)| tiebreak");
		c = Conf(Poc, 200.0, -1, Sig, 200.0, -1, Poc, 100.0, -1, Sig, 100.0, -1);
		c.Cluster(10, 150, 0.30);
		Check(c.ClusterPrice[0] == 100.0, "DV-T18b lowest-price tiebreak (order-independent)");
		// T18c: report weights never change families or the cluster set; score077 = manifold max weight + sigma + lattice.
		c = Conf(Poc, 100.0, -1, Vah, 100.5, -1, Sig, 101.0, -1, Fib, 101.5, 2);
		c.Cluster(10, 100, 0.30); string a = string.Join(",", Enumerable.Range(0, c.CandCount).Select(i => c.CandCluster[i] + ":" + c.CandFamilies(i)));
		double s077 = c.ClusterScore077[0];
		c.Cluster(10, 100, 0.30, 5, 9); string b = string.Join(",", Enumerable.Range(0, c.CandCount).Select(i => c.CandCluster[i] + ":" + c.CandFamilies(i)));
		Check(a == b && Math.Abs(s077 - (DvpConst.ReportWeightVa + 2)) < 1e-12 && Math.Abs(c.ClusterScore077[0] - 11) < 1e-12, "DV-T18c weights are report-only (families/clusters unchanged; score077 " + s077 + ")");
		c = Conf(Poc, 100.0, -1, Sig, 101.0, -1);
		c.Cluster(10, 100, 0.30);
		Check(Math.Abs(c.ClusterScore077[0] - (DvpConst.ReportWeightPoc + 1)) < 1e-12, "DV-T18c POC-only cluster score077 = ReportWeightPoc + 1");
		string core = CoreSource();
		var lit = Regex.Matches(Regex.Replace(core, @"//[^\n]*", ""), @"(?<![\d.])0?\.(7|8|80)(?![\d])");
		Check(lit.Count == 0, "DV-T18c no 0.7/0.8/0.80 literal in dvp-core (" + lit.Count + ")");
		// S4/N2: every rail occupies a candidate slot in publish order; a priceless rail is a non-candidate slot
		var rr = new DvpConfluence(8);
		rr.Add(Poc, 100.0, -1); rr.Add(ELevelKind.DvpHvn, double.NaN, -1);
		Check(rr.CandCount == 1, "DvpConfluence.Add skips priceless candidates (low-level)");
		// R2 (spec rev 5.1 DV-6.3/6.5 S4): Load-level slot alignment with a priceless HVN between POC and sigma
		var rl = new DvpRails(1);
		rl.Items[0] = new DvpRail { kind = Poc, z = 0, price = 100.0 };
		rl.Items[1] = new DvpRail { kind = Hvn, z = 0.5, price = double.NaN };
		rl.Items[2] = new DvpRail { kind = Sig, z = 1.0, price = 110.0 };
		typeof(DvpRails).GetProperty("Count").SetValue(rl, 3);
		var lt = new DvpLattice(1); lt.Price[0] = 110.5; lt.F[0] = 0.5; lt.Range[0] = 0; lt.Count = 1;
		var cl = new DvpConfluence(16); cl.Load(rl, lt); cl.Cluster(10, 100, 0.30);
		Check(cl.RailCount == 3 && cl.CandFam[1] == -1 && cl.CandCluster[1] == -1 && cl.CandFamilies(2) == 2 && cl.CandCluster[2] >= 0 && cl.CandCluster[2] == cl.CandCluster[3] && cl.CandFamilies(0) == 1,
			"S4 Load: [POC, HVN(NaN), sigma]+fib -> RailCount 3, slot 1 non-candidate unclustered, CandFamilies(2) = sigma+fib (" + cl.RailCount + "/" + cl.CandFam[1] + "/" + cl.CandCluster[1] + "/" + cl.CandFamilies(2) + ")");
		// T18d: FamiliesAt counts families and ranges without clustering.
		c = Conf(Poc, 100.0, -1, Sig, 102.0, -1, Fib, 101.0, 0, Fib, 101.2, 3, Fib, 120.0, 1);
		c.Cluster(10, 100, 0.30); int f, r;
		c.FamiliesAt(101, 0.30, out f, out r);
		int f2, r2; c.FamiliesAt(120, 0.10, out f2, out r2);
		Check(f == 3 && r == 2 && f2 == 1 && r2 == 1, "DV-T18d FamiliesAt (3 families, 2 ranges; 1/1)");
		// T18e: the tolerance boundary is inclusive at exactly 0.30 ATR.
		c = Conf(Poc, 100.0, -1, Sig, 103.0, -1, Fib, 97.0, 0);
		c.Cluster(10, 100, 0.30);
		var d = Conf(Poc, 100.0, -1, Sig, 103.0001, -1, Fib, 97.0, 0);
		d.Cluster(10, 100, 0.30);
		Check(c.ClusterCount == 1 && c.ClusterFamilies[0] == 3 && d.ClusterFamilies[0] == 2, "DV-T18e boundary inclusive at 0.30 ATR");
	}

	static void T19()
	{
		// newest first; rail flat at 100, ATR 10 -> delta 1.
		Func<double[], double[]> P = cl => cl.Select(_ => 100.0).ToArray();
		Func<double[], double[]> A = cl => cl.Select(_ => 10.0).ToArray();
		double[] c = { 103, 102, 101.5, 99, 99 };   // trigger at k=2 (101.5 > 101, prior 99 <= 100), then 2 closes beyond
		var p = P(c); var a = A(c);
		Check(DvpAccept.Triggered(c, p, a, 5, 2, 1) && !DvpAccept.Triggered(c, p, a, 5, 1, 1), "DV-T19 trigger: beyond +0.10 ATR, prior bar not beyond (no delta)");
		Check(DvpAccept.Accepted(c, p, a, 5, 1, 2, true, 0) && !DvpAccept.Accepted(c, p, a, 5, 1, 2, false, 0) && DvpAccept.Accepted(c, p, a, 5, 1, 3, false, 0),
			"DV-T19 Accepted countTrigger true (trigger + 2) vs false (3 in total)");
		Check(!DvpAccept.Accepted(c, p, a, 5, 1, 2, true, 0.2), "DV-T19 deltaAtr applies to every bar of the run (101.5 not > 102)");
		Check(DvpAccept.RunBeyond(c, p, a, 5, 1, 0) == 3 && DvpAccept.RunBeyond(c, p, a, 5, 1, 0.25) == 1, "DV-T19 RunBeyond from the newest bar");
		double[] t = { 98.5, 102, 99 }; var tp = P(t); var ta = A(t);
		Check(DvpAccept.BackInside(t, tp, ta, 3, 1) && !DvpAccept.BackInside(c, p, a, 5, 1), "DV-T19 BackInside since the trigger");
		double[] q = { 99.5, 102, 99 };
		Check(!DvpAccept.BackInside(q, P(q), A(q), 3, 1), "DV-T19 BackInside needs > 0.10 ATR inside");
		double[] dn = { 97, 98, 98.5, 101 };
		Check(DvpAccept.Accepted(dn, P(dn), A(dn), 4, -1, 2, true, 0) && !DvpAccept.Accepted(dn, P(dn), A(dn), 4, 1, 2, true, 0), "DV-T19 down direction mirrors");
		bool threw = false; try { DvpAccept.Accepted(c, p, a, 5, 1, 7, true, 0); } catch (ArgumentOutOfRangeException) { threw = true; }
		bool threw0 = false; try { DvpAccept.Accepted(c, p, a, 5, 1, 0, true, 0); } catch (ArgumentOutOfRangeException) { threw0 = true; }
		Check(threw && threw0, "DV-8.1 assert N in 1..6");
		double[] nn = { 103, 102, 101.5, 99, 99 }; var np = P(nn); np[1] = double.NaN;
		Check(!DvpAccept.Accepted(nn, np, A(nn), 5, 1, 2, true, 0) && DvpAccept.RunBeyond(nn, np, A(nn), 5, 1, 0) == 1, "DV-T19 a missing own rail breaks the run");
	}

	static void T19b()
	{
		// own-rail: the rail rises with the bars; vs a fixed rail at today's price the decision differs.
		double[] c = { 106, 104, 102.5, 99 }, own = { 105.5, 103.5, 101, 100 }, a = { 10, 10, 10, 10 };
		double[] fixedP = { 105.5, 105.5, 105.5, 105.5 };
		Check(DvpAccept.Accepted(c, own, a, 4, 1, 2, true, 0) && !DvpAccept.Accepted(c, fixedP, a, 4, 1, 2, true, 0), "DV-T19b each bar against its OWN rail, not today's");
		double[] own2 = { 105.5, 104.5, 101, 100 };   // bar 1 closes at its own rail: not beyond (strict)
		Check(!DvpAccept.Accepted(c, own2, a, 4, 1, 2, true, 0) && DvpAccept.RunBeyond(c, own2, a, 4, 1, 0) == 1, "DV-T19b strictly beyond its own rail");
		double[] ten = { 10, 10, 10, 10, 10, 10, 10, 10 };
		// (i)/(ii) trigger + 1 further close: countTrigger=true N=2 not accepted; countTrigger=false N=2 accepted (KD-06, deltaAtr = delta)
		double[] t1 = { 102, 102, 99 }, f1 = { 100, 100, 100 };
		Check(DvpAccept.Triggered(t1, f1, ten, 3, 1, 1) && !DvpAccept.Accepted(t1, f1, ten, 3, 1, 2, true, 0), "DV-T19b (i) countTrigger=true, N=2, trigger + 1 further close: not accepted");
		Check(DvpAccept.Accepted(t1, f1, ten, 3, 1, 2, false, DvpAccept.Delta), "DV-T19b (ii) countTrigger=false, N=2, trigger + 1 further close: accepted (deltaAtr = 0.10)");
		// (iii) N=5 true: trigger + 5 further closes = 6 snapshots plus the pre-trigger bar (Len 7); Len 6 is not enough
		double[] t5 = { 105, 105, 105, 105, 105, 102, 99 }, f5 = { 100, 100, 100, 100, 100, 100, 100 };
		Check(DvpAccept.Accepted(t5, f5, ten, 7, 1, 5, true, 0) && !DvpAccept.Accepted(t5, f5, ten, 6, 1, 5, true, 0) && !DvpAccept.Accepted(t5, f5, ten, 7, 1, 6, true, 0),
			"DV-T19b (iii) countTrigger=true, N=5 needs 6 snapshots + the pre-trigger bar (Len 7 accepted, Len 6 not)");
		// (iv) 2nd further close beyond the OLD (trigger-bar) rail but not its own: not accepted
		double[] t4 = { 103, 104, 102, 99 }, own4 = { 104, 101, 100, 100 }, old4 = { 100, 100, 100, 100 };
		Check(DvpAccept.Accepted(t4, old4, ten, 4, 1, 2, true, 0) && !DvpAccept.Accepted(t4, own4, ten, 4, 1, 2, true, 0),
			"DV-T19b (iv) 2nd further close beyond the old rail but not its own: not accepted");
		// (v) a close beyond the old rail but not its own rail: no trigger
		double[] tv = { 102, 99 }, ownv = { 102.5, 100 }, oldv = { 100, 100 };
		Check(DvpAccept.Triggered(tv, oldv, ten, 2, 0, 1) && !DvpAccept.Triggered(tv, ownv, ten, 2, 0, 1), "DV-T19b (v) close beyond the old rail but not its own: no trigger");
	}

	static RingBar[] Ramp(int n, double slope, int seed)
	{
		bool[] hd; var s = Series(n, seed, null, null, out hd);
		double c0 = s[0].c;
		for (int i = 0; i < n; i++)
		{
			double off = slope * i - (s[i].c - c0) * 0.9;   // mostly remove the random walk, keep noise
			s[i].o += off; s[i].h += off; s[i].l += off; s[i].c += off;
		}
		return s;
	}

	static void T24()
	{
		foreach (double sl in new[] { 0.5, -0.5 })
		{
			var s = Ramp(900, sl, 24);
			var rig = new Rig(Cfg(), true, false);
			for (int i = 0; i < s.Length; i++) rig.Feed(s[i], false);
			DvpSnapshot x = rig.e.Snapshot(1, rig.ring, 1);
			DvpRails rails = rig.e.RailsOf(1, rig.ring, 1);
			Check(x != null && x.valid && Math.Sign(x.slope1SigmaPerBar) == Math.Sign(sl) && Math.Sign(x.slope1) == Math.Sign(sl), "DV-T24 slope1SigmaPerBar sign on a " + (sl > 0 ? "rising" : "falling") + " ramp (" + (x != null ? x.slope1SigmaPerBar : double.NaN) + ")");
			double z0 = rig.S.History.ZClose(rig.ring, 0);
			double want = (rig.ring[0].c - rails.YhatT) / rails.Sigma;
			Check(Math.Abs(z0 - want) < 1e-9, "DV-T24 Z history k=0 = (C - yhat_T) / sigma of the bar's own snapshot (endAgo 1)");
			DvpRails r3 = rig.e.RailsOf(1, rig.ring, 4);
			Check(Math.Abs(rig.S.History.ZClose(rig.ring, 3) - (rig.ring[3].c - r3.YhatT) / r3.Sigma) < 1e-9, "DV-T24 Z history k=3 uses snapshot endAgo 4");
			// units: a close exactly one sigma above yhat_T has z = 1
			Check(Math.Abs(rails.Z(rails.YhatT + rails.Sigma) - 1) < 1e-9, "DV-T24 Z units: one sigma = 1");
		}
	}

	static void HistoryPathIndependence()
	{
		bool[] hd; var s = Series(1200, 31, new[] { 700 }, null, out hd);
		var eager = new Rig(Cfg(), true, false); var lazy = new Rig(Cfg(), true, false);
		bool same = true, own = true; int checks = 0;
		for (int i = 0; i < s.Length; i++)
		{
			eager.Feed(s[i], hd[i]); lazy.Feed(s[i], hd[i]);
			if (i < 300) continue;
			eager.e.HistoryOf(1, eager.ring, ELevelKind.DvpPoc, 0);
			if (i % 97 != 0) continue;
			var h1 = eager.e.HistoryOf(1, eager.ring, ELevelKind.DvpVah, 0); var c1 = h1.P.ToArray(); var a1 = h1.A.ToArray();
			var h2 = lazy.e.HistoryOf(1, lazy.ring, ELevelKind.DvpVah, 0);
			for (int k = 0; k < DvpRailHistory.Depth; k++)
			{
				if (!(c1[k].Equals(h2.P[k]) && a1[k].Equals(h2.A[k]))) same = false;
				DvpRails rk = eager.e.RailsOf(1, eager.ring, k + 1); DvpSnapshot sk = eager.e.Snapshot(1, eager.ring, k + 1);
				double want = rk != null ? rk.RailPrice(ELevelKind.DvpVah, 0) : double.NaN;
				if (!want.Equals(c1[k]) || (sk != null && !sk.atr.Equals(a1[k]))) own = false;   // no published snapshot (skipped window): rail NaN
				checks++;
			}
		}
		Check(same && checks > 0, "DV-8.1 rail history is path independent (queried every bar vs on demand)");
		Check(own, "DV-8.1 bar k's own rail = snapshot endAgo k+1 (" + checks + " checks)");
		// history fill does not evict the host's query snapshot cache
		var rig = new Rig(Cfg(), true, false);
		for (int i = 0; i < 400; i++) rig.Feed(s[i], hd[i]);
		DvpSnapshot q = rig.e.Snapshot(1, rig.ring, 1); string before = Sig(q, rig.e.RailsOf(1, rig.ring, 1));
		rig.e.HistoryOf(1, rig.ring, ELevelKind.DvpPoc, 0);
		Check(Sig(q, rig.e.RailsOf(1, rig.ring, 1)) == before && ReferenceEquals(q, rig.e.Snapshot(1, rig.ring, 1)), "DV-8.1 history fill uses its own slot (query snapshot not evicted)");
	}

	static void RailJournalTest()
	{
		bool[] hd; var s = Series(1500, 41, new[] { 900 }, new[] { 6 }, out hd);
		var rig = new Rig(Cfg(), true, true);
		int events = 0, logs = 0;
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], hd[i]);
			DvpStream st = rig.S;
			DvpSnapshot x = rig.e.Snapshot(1, rig.ring, 1); DvpRails r = x != null ? rig.e.RailsOf(1, rig.ring, 1) : null;
			DvpConfluence cf = r != null ? rig.e.ConfluenceOf(1, x, r, null, rig.ring[1].c) : null;
			int n = st.RailJournal.Step(st, rig.ring, x, r, cf, false, st.SessionHalfDay, rig.journal.Add, rig.e.RunId);
			events += n;
			if (st.RailJournal.Step(st, rig.ring, x, r, cf, false, false, rig.journal.Add, rig.e.RunId) != 0) logs++;   // same bar twice: no-op
		}
		int beforeFinish = rig.journal.Count(l => l.StartsWith("DVP RAIL"));
		rig.e.Finish();
		var rail = rig.journal.Where(l => l.StartsWith("DVP RAIL")).ToList();
		Check(events > 0 && logs == 0 && rail.Count == events && rig.S.RailJournal.Dropped == 0, "DV-11.2 one DVP RAIL line per interaction (" + events + "), same bar stepped twice is a no-op");
		Check(rail.Take(beforeFinish).All(l => l.Contains("complete(1)") && l.Contains("bars(40)")) && rail.Skip(beforeFinish).All(l => l.Contains("complete(0)")) && rail.Count > beforeFinish,
			"DV-11.2 written when the 40-bar clock completes; Finish flushes the rest with complete(0)");
		string[] fields = { "railKind(", "z(", "price(", "families(", "nRanges(", "score(", "score077(", "distAtr(", "closedThrough(", "heldBars(", "accepted(N2=", "verdict41v0(", "isRollWindow(", "halfDay(", "mfe40(", "mae40(" };
		Check(rail.All(l => fields.All(l.Contains)), "DV-11.2 DVP RAIL fields present");
		var v = rail.Where(l => l.Contains("complete(1)") && l.Contains("event(trigger)")).Select(l => Regex.Match(l, @"verdict41v0\((\w+)\)").Groups[1].Value).Distinct().ToList();
		Check(v.All(x => x == "accepted" || x == "held" || x == "failed") && v.Contains("failed"), "DV-11.2 verdict41 v0 in {accepted, held, failed} [GAP stand-in] (" + string.Join("/", v) + ")");
		Check(rail.Where(l => l.Contains("event(accept)")).All(l => l.Contains("verdict41v0(NaN)")) && rail.Any(l => l.Contains("event(accept)")) && rail.Any(l => l.Contains("event(trigger)")),
			"DV-11.2 verdict41 v0 [GAP]: accept events journal NaN; triggers are journalled");
		var trig = rail.Where(l => l.Contains("event(trigger)") && l.Contains("complete(1)")).ToList();
		Check(trig.Where(l => l.Contains("verdict41v0(accepted)")).All(l => l.Contains("accepted(N2=0"))
			&& trig.Count(l => l.Contains("verdict41v0(accepted)")) > 0 && trig.Count(l => l.Contains("verdict41v0(accepted)")) <= rail.Count(l => l.Contains("event(accept)") && l.Contains("accepted(N2=1")),
			"DV-11.2 verdict41 v0 accepted only via an N=2 completion (accepted triggers <= N2 accept events)");
		// R1 (Analyst 1 re-review, spec rev 5.1 DV-8.4 S3): exact 1:1 for stable-identity rails; also guards e1 (own-trigger only)
		var bi = new Dictionary<DateTime, int>(); for (int i = 0; i < s.Length; i++) bi[s[i].time] = i;
		Func<string, string, string> fv = (l, k) => Regex.Match(l, Regex.Escape(k) + @"\(([^)]*)\)").Groups[1].Value;
		Func<string, bool> uk = l => { string k = fv(l, "railKind"); return k == "DvpPoc" || k == "DvpVah" || k == "DvpVal" || k == "DvpSigma"; };
		Func<string, int, string> key = (l, d) => fv(l, "railKind") + "|" + fv(l, "dir") + "|" + (fv(l, "railKind") == "DvpSigma" ? fv(l, "z") : "") + "|"
			+ (bi[DateTime.ParseExact(fv(l, "barTime"), "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)] - d);
		var kt = rail.Where(l => uk(l) && l.Contains("event(trigger)") && l.Contains("verdict41v0(accepted)")).Select(l => key(l, 0)).ToList();
		var ka = rail.Where(l => uk(l) && l.Contains("event(accept)") && l.Contains("accepted(N2=1")).Select(l => key(l, 2)).ToList();
		Check(kt.Count > 0 && kt.Count == ka.Count && kt.Distinct().Count() == kt.Count && new HashSet<string>(kt).SetEquals(ka),
			"DV-11.2 verdict41 v0 accepted <-> N=2 accept event 2 bars later, 1:1 for unique/sigma rails (" + kt.Count + " / " + ka.Count + ")");
		var off = new Rig(new DvpConfig { enabled = true, journalRails = false, splitFlow = false }, true, true);
		Check(off.S.RailJournal == null && rig.S.RailJournal != null, "N1 DvpRailJournal allocated only when journalRails is on");
	}

	static void ZeroAllocD5()
	{
		bool[] hd; RingBar[] s = Series(1600, 15, null, null, out hd);
		var rig = new Rig(Cfg(), true, false);
		double sink = 0;
		Action<int> q = i =>
		{
			DvpSnapshot x = rig.e.Snapshot(1, rig.ring, 1); DvpRails r = rig.e.RailsOf(1, rig.ring, 1);
			if (x == null) return;
			DvpConfluence cf = rig.e.ConfluenceOf(1, x, r, rig.e.LatticeOf(1, rig.ring, 1), rig.ring[1].c);
			int f, n; cf.FamiliesAt(x.yhatT, 0.30, out f, out n);
			DvpRailHistory h = rig.e.HistoryOf(1, rig.ring, ELevelKind.DvpPoc, 0);
			sink += f + cf.QualifyingCount + DvpAccept.RunBeyond(h.C, h.P, h.A, h.Len, 1, 0) + (DvpAccept.Accepted(h.C, h.P, h.A, h.Len, 1, 2, true, 0) ? 1 : 0) + h.ZClose(rig.ring, 2);
		};
		for (int i = 0; i < 600; i++) { rig.Feed(s[i], hd[i]); q(i); }
		long b0 = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 600; i < s.Length; i++) { rig.Feed(s[i], hd[i]); q(i); }
		long b1 = GC.GetAllocatedBytesForCurrentThread();
		Check(b1 - b0 == 0, "DV-13 zero allocations per bar for confluence + history + acceptance queries (" + (b1 - b0) + " bytes)");
		if (double.IsNaN(sink) && sink > 0) Console.WriteLine("sink");
	}

	static void D5Host()
	{
		string h = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		int pa = h.IndexOf("if (m_paHosted != null) stepExtraPa();", StringComparison.Ordinal), rs = h.IndexOf("if (m_dvp != null) dvpRailStep();", StringComparison.Ordinal);
		Check(pa > 0 && rs > pa && rs - pa < 200, "DV-11.2 dvpRailStep runs right after stepExtraPa (lattice pivots current)");
		int b = h.IndexOf("private void dvpRailStep()", StringComparison.Ordinal);
		string body = h.Substring(b, h.IndexOf("\n\t\t}\n", b, StringComparison.Ordinal) - b);
		Check(body.Contains("if (!DvpConfig.journalRails || m_dvp.journal == null) return;") && body.Contains("logSignal(") && body.Contains(", false);"),
			"DV-11.2 dvpRailStep only with journalRails + Journal; logSignal taken:false");
		Check(Count(h, "dvpLattice(") == 2 && Count(h, "dvpConfOf(") == 5, "N2 the lattice is built only through dvpConfOf");
	}

	static void NodeSwap()
	{
		// Analyst 1 T2: HVN/LVN nearest-z match is bounded (|dz| <= 0.25); a tracked node absent from a snapshot is NaN, never a far node.
		var a = new DvpRail[3]; var b = new DvpRail[3]; var c = new DvpRail[3];
		a[0].kind = ELevelKind.DvpPoc; a[1].kind = ELevelKind.DvpHvn; a[1].z = 0.50; a[1].price = 100; a[2].kind = ELevelKind.DvpHvn; a[2].z = 1.20; a[2].price = 110;
		b[0].kind = ELevelKind.DvpPoc; b[1].kind = ELevelKind.DvpHvn; b[1].z = 1.18; b[1].price = 111; b[2].kind = ELevelKind.DvpLvn; b[2].z = 0.55; b[2].price = 101;   // 0.50 HVN disappeared
		c[0].kind = ELevelKind.DvpPoc; c[1].kind = ELevelKind.DvpHvn; c[1].z = 0.62; c[1].price = 101; c[2].kind = ELevelKind.DvpHvn; c[2].z = 1.21; c[2].price = 110;   // a node (re)appears near 0.50
		Check(DvpRailHistory.Match(a, 3, ELevelKind.DvpHvn, 0.50) == 1 && DvpRailHistory.Match(b, 3, ELevelKind.DvpHvn, 0.50) == -1 && DvpRailHistory.Match(c, 3, ELevelKind.DvpHvn, 0.50) == 1,
			"DV-8.1 node disappears (far same-kind node 0.68 z away is NOT matched) then reappears within 0.25 z");
		Check(DvpRailHistory.Match(b, 3, ELevelKind.DvpHvn, 1.20) == 1 && DvpRailHistory.Match(c, 3, ELevelKind.DvpHvn, 1.20) == 2 && DvpRailHistory.Match(b, 3, ELevelKind.DvpLvn, 0.50) == 2,
			"DV-8.1 nodes matched by z, not position; never across kinds");
		Check(DvpRailHistory.Match(a, 3, ELevelKind.DvpHvn, 0.75) == 1 && DvpRailHistory.Match(a, 3, ELevelKind.DvpHvn, 0.751) == -1 && DvpRailHistory.Match(a, 3, ELevelKind.DvpSigma, 1.0) == -1,
			"DV-8.1 bound inclusive at |dz| = 0.25");
		// absent node in the pre-trigger snapshot -> NaN own rail -> no false trigger
		double[] cl = { 102, 99 }, p = { 100, double.NaN }, at = { 10, 10 };
		Check(!DvpAccept.Triggered(cl, p, at, 2, 0, 1), "DV-8.1 absent node (NaN) in the pre-trigger snapshot: no trigger");
	}

	static void QueryOrder()
	{
		// Analyst 2 (ATR history fix): identical results whichever order the queries run, asserted for both orders explicitly.
		bool[] hd; var s = Series(900, 57, null, null, out hd);
		var x = new Rig(Cfg(), true, false); var y = new Rig(Cfg(), true, false); var z = new Rig(Cfg(), true, false); var w = new Rig(Cfg(), true, false);
		bool eq = true, atrOk = true; int n = 0;
		for (int i = 0; i < s.Length; i++)
		{
			x.Feed(s[i], hd[i]); y.Feed(s[i], hd[i]); z.Feed(s[i], hd[i]); w.Feed(s[i], hd[i]);
			if (i < 300 || i % 53 != 0) continue;
			// order A: snapshots endAgo 1..8 first, then the history; order B: the history first, then snapshots 8..1
			var qa = new string[9]; for (int k = 1; k <= 8; k++) qa[k] = x.Q(k);
			var ha = x.e.HistoryOf(1, x.ring, ELevelKind.DvpVal, 0); string sa = string.Join(",", ha.P.Concat(ha.A).Concat(ha.C).Select(v => v.ToString("R", CI)));
			var hb = y.e.HistoryOf(1, y.ring, ELevelKind.DvpVal, 0); string sb = string.Join(",", hb.P.Concat(hb.A).Concat(hb.C).Select(v => v.ToString("R", CI)));
			for (int k = 8; k >= 1; k--) if (y.Q(k) != qa[k]) eq = false;
			if (sa != sb) eq = false;
			// order C: snapshots endAgo 8 -> 1, then the history; order D: Snapshot(endAgo 6..8) before HistoryOf
			for (int k = 8; k >= 1; k--) z.e.Snapshot(1, z.ring, k);
			var hz = z.e.HistoryOf(1, z.ring, ELevelKind.DvpVal, 0); string sz = string.Join(",", hz.P.Concat(hz.A).Concat(hz.C).Select(v => v.ToString("R", CI)));
			for (int k = 6; k <= 8; k++) w.e.Snapshot(1, w.ring, k);
			var hw = w.e.HistoryOf(1, w.ring, ELevelKind.DvpVal, 0); string sw = string.Join(",", hw.P.Concat(hw.A).Concat(hw.C).Select(v => v.ToString("R", CI)));
			if (sz != sa || sw != sa) eq = false;
			for (int k = 0; k < 8; k++) if (double.IsNaN(ha.A[k])) atrOk = false;
			n++;
		}
		Check(eq && n > 0, "DV-8.1 query orders A (snapshots 1..8, history) == B (history, snapshots 8..1) == C (snapshots 8..1, history) == D (Snapshot 6..8, HistoryOf) (" + n + " probes)");
		Check(atrOk, "DV-8.1 every history snapshot (endAgo 1..8) has ATR(t-1) (ATR history depth fix)");
	}

	static void D5Tests() { NodeSwap(); QueryOrder(); T18(); T19(); T19b(); T24(); HistoryPathIndependence(); RailJournalTest(); ZeroAllocD5(); D5Host(); }
}
