using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// PR-D9a (specs/m-dvp-spec.md rev 6, §16 item 12 and DV-R3): REPORT-ONLY comparison of the online degree path of one
	// SelfCalibrate DVP stream (the DV-3.10 monthly rechecks, the "DVP DEGREE sel" lines) with the offline pooled d* and the
	// per-year d* of the same run's scored windows (DvpCalib.Years / Pooled / DStar, i.e. the "DVP CAL" table, DV-T38).
	// It changes nothing: it only reads DvpStream / DvpSelfCalib / DvpCalib counters after the engine step, never calls a fit,
	// adds no DvpConfig field (Canon() unchanged), writes no table and gates nothing. MC-free (tests/Dvp links this file whole).
	// Per bar: two long compares (allocation-free; every array is sized in the constructor). Allocates only in Lines().
	// Hosted by _DvpEngineTest.Strategy.CS (one report per DVP stream).
	// ============================================================================================
	public sealed class DvpDegreeReport
	{
		/// <summary>Recorded rechecks per stream (one per tradingDay month: 4096 = 341 years). Further rechecks are counted, not recorded.</summary>
		public const int DefaultCapacity = 4096;
		/// <summary>|endpointDrift| histogram edges in ATR (DV-3.8): bins [0,0.05) [0.05,0.1) ... [2,inf).</summary>
		public static readonly double[] DriftEdges = { 0.05, 0.1, 0.25, 0.5, 1, 2 };

		public readonly int Ds;
		readonly int[] m_ym, m_from, m_to, m_cand, m_n;
		readonly bool[] m_warm, m_changed, m_eligible;
		readonly double[] m_sFrom, m_sCand, m_need;
		int m_count;
		long m_seenBars = -1, m_seenRechecks;
		readonly long[] m_driftBins = new long[DriftEdges.Length + 1];
		double m_driftAbsSum, m_driftAbsMax;

		/// <summary>Rechecks seen but not recorded (capacity full).</summary>
		public long Overflow { get; private set; }
		/// <summary>Closed bars the host withheld from the stream (dvpFeed false: on/after HOLDOUT_FROM), and the first one's time.</summary>
		public long Withheld { get; private set; }
		public DateTime FirstWithheld { get; private set; }
		public long DriftCount { get; private set; }
		public int Count { get { return m_count; } }

		public DvpDegreeReport(int ds, int capacity = DefaultCapacity)
		{
			Ds = ds;
			int c = Math.Max(1, capacity);
			m_ym = new int[c]; m_from = new int[c]; m_to = new int[c]; m_cand = new int[c]; m_n = new int[c];
			m_warm = new bool[c]; m_changed = new bool[c]; m_eligible = new bool[c];
			m_sFrom = new double[c]; m_sCand = new double[c]; m_need = new double[c];
		}

		/// <summary>Call once per closed chart bar AFTER the engine step (repeat calls on the same stream bar are no-ops). Returns true when
		/// the stream took a new bar since the last call. Records the recheck the engine made on that bar, if any (at most one per bar).</summary>
		public bool Observe(DvpStream s)
		{
			if (s == null || s.BarsSeen == m_seenBars) return false;
			m_seenBars = s.BarsSeen;
			DvpSelfCalib c = s.Self;
			if (c == null || c.Rechecks == m_seenRechecks) return true;
			m_seenRechecks = c.Rechecks;
			if (m_count >= m_ym.Length) { Overflow++; return true; }
			int i = m_count++;
			DateTime td = s.SessionDay;   // the recheck bar's tradingDayKey (DV-3.10.5): the month the new degree starts
			m_ym[i] = td.Year * 12 + td.Month - 1;
			m_from[i] = c.LastFrom; m_to[i] = c.Current; m_cand[i] = c.LastCandidate; m_n[i] = c.Count;
			m_warm[i] = c.Warming; m_changed[i] = c.LastChanged; m_eligible[i] = c.Count >= c.MinScored;
			double sf = c.LastFrom >= 1 && c.LastFrom < c.Scores.Length ? c.Scores[c.LastFrom] : double.NaN;
			double sb = c.LastCandidate >= 1 && c.LastCandidate < c.Scores.Length ? c.Scores[c.LastCandidate] : double.NaN;
			m_sFrom[i] = sf; m_sCand[i] = sb;
			m_need[i] = double.IsNaN(sf) ? double.NaN : Math.Max(c.MarginAbs, c.MarginRel * sf);   // DV-3.10.6 switch threshold
			return true;
		}

		/// <summary>One refit snapshot's endpointDrift (ATR, DV-3.8); NaN / infinite values are ignored.</summary>
		public void ObserveDrift(double driftAtr)
		{
			if (double.IsNaN(driftAtr) || double.IsInfinity(driftAtr)) return;
			double a = Math.Abs(driftAtr);
			int b = 0; while (b < DriftEdges.Length && a >= DriftEdges[b]) b++;
			m_driftBins[b]++; DriftCount++; m_driftAbsSum += a; if (a > m_driftAbsMax) m_driftAbsMax = a;
		}

		/// <summary>A closed bar dvpFeed withheld from the stream.</summary>
		public void Withhold(DateTime barTime)
		{
			if (Withheld == 0) FirstWithheld = barTime;
			Withheld++;
		}

		// ---- read-back (tests) ----
		public int YearMonth(int i) { return m_ym[i]; }
		public int From(int i) { return m_from[i]; }
		public int To(int i) { return m_to[i]; }
		public int Candidate(int i) { return m_cand[i]; }
		public int N(int i) { return m_n[i]; }
		public bool WarmingAfter(int i) { return m_warm[i]; }
		public bool Changed(int i) { return m_changed[i]; }
		/// <summary>n &gt;= calibMinScored at the recheck (the switch rule applied).</summary>
		public bool Eligible(int i) { return m_eligible[i]; }
		/// <summary>score(from) - score(candidate) at the recheck (&gt;= 0; 0 when the candidate is the current d; NaN when a score is NaN).</summary>
		public double Gap(int i) { return m_sFrom[i] - m_sCand[i]; }
		public double Need(int i) { return m_need[i]; }
		/// <summary>The switch rule applied, the candidate differs from the current d, and the margin kept d (DV-3.10.6).</summary>
		public bool Blocked(int i) { return m_eligible[i] && m_cand[i] >= 1 && m_cand[i] != m_from[i] && !m_changed[i]; }
		public long DriftBin(int b) { return m_driftBins[b]; }

		/// <summary>Summary numbers (computed by Compute).</summary>
		public sealed class Summary
		{
			public int pooledD, months, postWarm, matchPooled, yearMonths, matchYear, switches, blocked, flipBacks, warmMonths;
			public int warmEndYm = -1;
			public double[] gapAll = new double[0], gapCand = new double[0], relCand = new double[0];
			public SortedDictionary<int, int> yearD = new SortedDictionary<int, int>();
		}

		/// <summary>Compares the recorded online path with the offline d* of cal (pooled over cal.Years, and per calendar year). The online d
		/// of month M is the d in effect after M's recheck; only months after warm-up count (d is pinned to 1 while warming).</summary>
		public Summary Compute(DvpCalib cal)
		{
			var r = new Summary();
			if (cal != null)
			{
				r.pooledD = DvpCalib.DStar(cal.Pooled());
				foreach (var kv in cal.Years) r.yearD[kv.Key] = DvpCalib.DStar(kv.Value);
			}
			var all = new List<double>(); var cand = new List<double>(); var rel = new List<double>();
			int lastSwitchFrom = -1, lastSwitchAt = -1;
			r.months = m_count;
			for (int i = 0; i < m_count; i++)
			{
				if (m_warm[i]) { r.warmMonths++; continue; }
				if (r.warmEndYm < 0) r.warmEndYm = m_ym[i];
				r.postWarm++;
				if (r.pooledD >= 1 && m_to[i] == r.pooledD) r.matchPooled++;
				int yd;
				if (r.yearD.TryGetValue(m_ym[i] / 12, out yd) && yd >= 1) { r.yearMonths++; if (m_to[i] == yd) r.matchYear++; }
				if (m_changed[i])
				{
					r.switches++;
					if (lastSwitchAt >= 0 && i - lastSwitchAt <= 3 && m_to[i] == lastSwitchFrom) r.flipBacks++;
					lastSwitchFrom = m_from[i]; lastSwitchAt = i;
				}
				if (Blocked(i)) r.blocked++;
				double g = Gap(i);
				if (m_eligible[i] && !double.IsNaN(g) && !double.IsInfinity(g))
				{
					all.Add(g);
					if (m_cand[i] != m_from[i]) { cand.Add(g); if (m_sFrom[i] > 0) rel.Add(g / m_sFrom[i]); }
				}
			}
			r.gapAll = all.ToArray(); Array.Sort(r.gapAll);
			r.gapCand = cand.ToArray(); Array.Sort(r.gapCand);
			r.relCand = rel.ToArray(); Array.Sort(r.relCand);
			return r;
		}

		/// <summary>Linear-interpolated quantile of an ascending array (NaN when empty).</summary>
		public static double Quantile(double[] sorted, double q)
		{
			if (sorted == null || sorted.Length == 0) return double.NaN;
			double h = (sorted.Length - 1) * q; int lo = (int)Math.Floor(h); int hi = Math.Min(lo + 1, sorted.Length - 1);
			return sorted[lo] + (h - lo) * (sorted[hi] - sorted[lo]);
		}

		static string F(double x) { return x.ToString("R", CultureInfo.InvariantCulture); }
		static string I(long x) { return x.ToString(CultureInfo.InvariantCulture); }
		static string Ym(int ym) { return ym < 0 ? "NaN" : I(ym / 12) + "-" + (ym % 12 + 1).ToString("00", CultureInfo.InvariantCulture); }
		static string Share(int k, int n) { return n > 0 ? F((double)k / n) : "NaN"; }
		static string Dist(double[] s)
		{
			return "{n=" + I(s.Length) + " p10=" + F(Quantile(s, 0.10)) + " p25=" + F(Quantile(s, 0.25)) + " p50=" + F(Quantile(s, 0.50))
				+ " p75=" + F(Quantile(s, 0.75)) + " p90=" + F(Quantile(s, 0.90)) + " max=" + F(s.Length > 0 ? s[s.Length - 1] : double.NaN) + "}";
		}

		/// <summary>The report: one "DVP D9A month" line per recorded recheck, then one "DVP D9A summary" line. Report-only.</summary>
		public void Lines(Action<string> journal, string runId, DvpStream s)
		{
			if (journal == null) return;
			string head = "DVP D9A";
			string tail = " runId(" + runId + ") stream(Data" + I(Ds) + ")";
			if (s == null || s.Self == null)
			{
				journal(head + " skipped" + tail + " reason(" + (s == null ? "no stream" : "degMode " + s.Config.degMode + " is not SelfCalibrate") + ")");
				return;
			}
			DvpSelfCalib c = s.Self; DvpCalib cal = s.Calib;
			Summary r = Compute(cal);
			for (int i = 0; i < m_count; i++)
			{
				int yd; bool hasY = r.yearD.TryGetValue(m_ym[i] / 12, out yd);
				journal(head + " month" + tail + " ym(" + Ym(m_ym[i]) + ") from(" + I(m_from[i]) + ") to(" + I(m_to[i]) + ") cand(" + I(m_cand[i])
					+ ") n(" + I(m_n[i]) + ") warming(" + (m_warm[i] ? 1 : 0) + ") changed(" + (m_changed[i] ? 1 : 0) + ") blocked(" + (Blocked(i) ? 1 : 0)
					+ ") sFrom(" + F(m_sFrom[i]) + ") sCand(" + F(m_sCand[i]) + ") gap(" + F(Gap(i)) + ") need(" + F(m_need[i])
					+ ") pooledD(" + I(r.pooledD) + ") yearD(" + (hasY ? I(yd) : "NaN") + ")");
			}
			var years = new StringBuilder();
			foreach (var kv in r.yearD) { if (years.Length > 0) years.Append(','); years.Append(I(kv.Key)).Append(':').Append(I(kv.Value)); }
			var bins = new StringBuilder();
			for (int b = 0; b < m_driftBins.Length; b++) { if (b > 0) bins.Append(','); bins.Append(I(m_driftBins[b])); }
			journal(head + " summary" + tail + " winLen(" + I(s.Config.winLen) + ") rechecks(" + I(c.Rechecks) + ") recorded(" + I(m_count) + ") overflow(" + I(Overflow)
				+ ") warmMonths(" + I(r.warmMonths) + ") warmEndYm(" + Ym(r.warmEndYm) + ") warmEnd(" + (c.Warming ? "NaN" : c.WarmEnd.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
				+ ") postWarmMonths(" + I(r.postWarm) + ") pooledDStar(" + I(r.pooledD) + ") yearDStar{" + years + "} matchPooled(" + I(r.matchPooled) + "/" + I(r.postWarm)
				+ " " + Share(r.matchPooled, r.postWarm) + ") matchYear(" + I(r.matchYear) + "/" + I(r.yearMonths) + " " + Share(r.matchYear, r.yearMonths)
				+ ") switches(" + I(r.switches) + ") engineChanges(" + I(c.Changes) + ") flipBacks3(" + I(r.flipBacks) + ") blocked(" + I(r.blocked)
				+ ") gapAll" + Dist(r.gapAll) + " gapCand" + Dist(r.gapCand) + " relGapCand" + Dist(r.relCand)
				+ " dFinal(" + I(c.Current) + ") skipShare(" + F(s.SkipShare) + ") meanCovIn(" + F(s.MeanCovIn) + ") meanCovFwd(" + F(s.MeanCovFwd)
				+ ") drift{n=" + I(DriftCount) + " meanAbs=" + F(DriftCount > 0 ? m_driftAbsSum / DriftCount : double.NaN) + " maxAbs=" + F(DriftCount > 0 ? m_driftAbsMax : double.NaN)
				+ " bins[" + bins + "] edges[0.05,0.1,0.25,0.5,1,2]} withheld(" + I(Withheld) + ") firstWithheld(" + (Withheld > 0 ? FirstWithheld.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "NaN")
				+ ") probe(calibLen=" + I(c.Len) + " calibMinScored=" + I(c.MinScored) + " calibMarginAbs=" + F(c.MarginAbs) + " calibMarginRel=" + F(c.MarginRel)
				+ " calibStep=" + I(s.Config.calibStep) + ") reportOnly(1)");
		}
	}
}
