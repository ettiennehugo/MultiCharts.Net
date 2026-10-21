// ============================================================================================
// M-TOD — time-of-day normalisers (spec /workspace/specs/missing-functionality-specs.md rev 4 §4, §0.1-§0.3).
// Plain internal sealed class, MC-free (tests/TodTests compile this file as is). Created in _CustomSignal.StartCalc only when
// TodMetrics != None (or an M-AVWAP VolumeClimax anchor needs the Volume norm).
// Per metric: for every session bucket (minute of the exchange session / bucketMinutes) a ring of the last `sessions` COMPLETED
// sessions' values and session-to-date cumulative values. Norms use prior completed sessions only; half-days are excluded;
// the splice bar of a roll drops its Range / TrueRange samples. Delta norms reset on a FlowQuality tier change (journaled).
// ============================================================================================
using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	[Flags]
	public enum ETodMetric { None = 0, Volume = 1 << 0, Delta = 1 << 1, Range = 1 << 2, TrueRange = 1 << 3 }

	/// <summary>One closed Data1 bar for the normaliser. NaN = no sample for that metric.</summary>
	public struct TodSample
	{
		public DateTime sessionDate;   // exchange-session date (trading-day key)
		public int minuteOfSession;    // minutes since the session open (futures: since 18:00 ET), bar END minute
		public bool halfDay;           // session excluded from the norms
		public bool splice;            // roll splice bar: Range / TrueRange dropped
		public double volume, delta, range, trueRange;
		public int deltaTier;          // FlowQuality tag of the delta sample (lower = better)
	}

	internal sealed class _TodNormaliser
	{
		public ETodMetric metrics = ETodMetric.None;
		public int sessions = 20;
		public int minSessions = 10;
		public int bucketMinutes = 1;
		public string label = "TOD";
		public Action<string> journal;
		public Action<string> warnOnce;

		private sealed class Hist { public double[] v, cum; public int count, next; }
		private const int N = 4;
		private Dictionary<int, Hist>[] m_hist;
		private Dictionary<int, double>[] m_cur;        // current session: bucket -> value (sum of bars in the bucket)
		private Dictionary<int, double>[] m_curCum;     // current session: bucket -> session-to-date cumulative at that bucket
		private double[] m_runCum;
		private int[] m_done;                          // completed sessions in the norm
		private bool[] m_sampled;                      // current session produced samples
		private double[] m_z, m_med, m_pct, m_zcum; private bool[] m_valid;
		private DateTime m_session = DateTime.MinValue; private bool m_sessionHalf;
		private int m_tier = int.MinValue;
		private int m_lastBar = int.MinValue;
		private bool m_warned;
		private double[] m_scratch;

		public bool Configured { get; private set; }

		public string Validate()
		{
			var e = new List<string>();
			if (metrics == ETodMetric.None) e.Add("metrics=None");
			if (sessions < 2) e.Add($"sessions={sessions}");
			if (minSessions < 2 || minSessions > sessions) e.Add($"minSessions={minSessions} (2..sessions)");
			if (bucketMinutes < 1 || bucketMinutes > 1440) e.Add($"bucketMinutes={bucketMinutes}");
			return e.Count == 0 ? null : "TOD config invalid: " + string.Join(", ", e);
		}

		public string Configure()
		{
			string err = Validate();
			if (err != null) { Configured = false; return err; }
			m_hist = new Dictionary<int, Hist>[N]; m_cur = new Dictionary<int, double>[N]; m_curCum = new Dictionary<int, double>[N];
			for (int k = 0; k < N; k++) { m_hist[k] = new Dictionary<int, Hist>(); m_cur[k] = new Dictionary<int, double>(); m_curCum[k] = new Dictionary<int, double>(); }
			m_runCum = new double[N]; m_done = new int[N]; m_sampled = new bool[N];
			m_z = new double[N]; m_med = new double[N]; m_pct = new double[N]; m_zcum = new double[N]; m_valid = new bool[N];
			m_scratch = new double[sessions];
			Configured = true;
			Reset();
			return null;
		}

		public void Reset()
		{
			if (!Configured) return;
			for (int k = 0; k < N; k++) resetMetric(k);
			m_session = DateTime.MinValue; m_sessionHalf = false; m_tier = int.MinValue; m_lastBar = int.MinValue; m_warned = false;
		}

		private void resetMetric(int k)
		{
			m_hist[k].Clear(); m_cur[k].Clear(); m_curCum[k].Clear(); m_runCum[k] = 0; m_done[k] = 0; m_sampled[k] = false; clearOut(k);
		}
		private void clearOut(int k) { m_z[k] = m_med[k] = m_pct[k] = m_zcum[k] = double.NaN; m_valid[k] = false; }

		private static int idx(ETodMetric m) { switch (m) { case ETodMetric.Volume: return 0; case ETodMetric.Delta: return 1; case ETodMetric.Range: return 2; case ETodMetric.TrueRange: return 3; default: return -1; } }
		private bool has(int k) { return (metrics & (ETodMetric)(1 << k)) != 0; }

		// ---- outputs for the last closed bar (NaN / false when not valid) ----
		public bool Valid(ETodMetric m) { int k = idx(m); return Configured && k >= 0 && m_valid[k]; }
		/// <summary>z of this bar's bucket value vs the same bucket of prior completed sessions (population sigma).</summary>
		public double Z(ETodMetric m) { int k = idx(m); return Valid(m) ? m_z[k] : double.NaN; }
		public double Median(ETodMetric m) { int k = idx(m); return Valid(m) ? m_med[k] : double.NaN; }
		/// <summary>Fraction of prior sessions whose bucket value is &lt;= this bar's (0..1).</summary>
		public double Pct(ETodMetric m) { int k = idx(m); return Valid(m) ? m_pct[k] : double.NaN; }
		/// <summary>Session-to-date cumulative value vs the same-bucket cumulative norm.</summary>
		public double ZCum(ETodMetric m) { int k = idx(m); return Valid(m) ? m_zcum[k] : double.NaN; }
		/// <summary>Completed sessions currently in the norm of a metric.</summary>
		public int Sessions(ETodMetric m) { int k = idx(m); return Configured && k >= 0 ? m_done[k] : 0; }

		public bool Add(int barNumber, TodSample s)
		{
			if (!Configured) return false;
			if (barNumber == m_lastBar) return false;
			if (barNumber < m_lastBar) { if (!m_warned) { m_warned = true; warnOnce?.Invoke($"{label}: out-of-order bar {barNumber} < {m_lastBar} ignored"); } return false; }
			m_lastBar = barNumber;

			DateTime day = s.sessionDate.Date;
			if (day != m_session)
			{
				if (m_session != DateTime.MinValue) commitSession();
				m_session = day; m_sessionHalf = s.halfDay;
			}
			else if (s.halfDay) m_sessionHalf = true;

			// delta tier change: norm reset (journaled), valid=false until minSessions sessions of the same tier
			if (has(1) && !double.IsNaN(s.delta) && s.deltaTier != m_tier)
			{
				if (m_tier != int.MinValue) journal?.Invoke($"{label} delta tier change {m_tier} -> {s.deltaTier}: norm reset at session {day:yyyy-MM-dd}");
				resetMetric(1); m_tier = s.deltaTier;
			}

			int bucket = Math.Max(0, s.minuteOfSession) / bucketMinutes;
			for (int k = 0; k < N; k++)
			{
				clearOut(k);
				if (!has(k)) continue;
				double v = k == 0 ? s.volume : k == 1 ? s.delta : k == 2 ? (s.splice ? double.NaN : s.range) : (s.splice ? double.NaN : s.trueRange);
				if (double.IsNaN(v)) continue;
				double cur; m_cur[k].TryGetValue(bucket, out cur); cur += v; m_cur[k][bucket] = cur;
				m_runCum[k] += v; m_curCum[k][bucket] = m_runCum[k]; m_sampled[k] = true;
				Hist h;
				if (!m_hist[k].TryGetValue(bucket, out h) || h.count < minSessions) continue;
				double mean, sd; stats(h.v, h.count, out mean, out sd);
				m_z[k] = sd > 0 ? (cur - mean) / sd : double.NaN;
				m_med[k] = median(h.v, h.count);
				int le = 0; for (int i = 0; i < h.count; i++) if (h.v[i] <= cur) le++;
				m_pct[k] = (double)le / h.count;
				double cmean, csd; stats(h.cum, h.count, out cmean, out csd);
				m_zcum[k] = csd > 0 ? (m_runCum[k] - cmean) / csd : double.NaN;
				m_valid[k] = !double.IsNaN(m_z[k]);
			}
			return true;
		}

		private void commitSession()
		{
			for (int k = 0; k < N; k++)
			{
				if (has(k) && m_sampled[k] && !m_sessionHalf)
				{
					foreach (var kv in m_cur[k])
					{
						Hist h;
						if (!m_hist[k].TryGetValue(kv.Key, out h)) { h = new Hist { v = new double[sessions], cum = new double[sessions] }; m_hist[k][kv.Key] = h; }
						h.v[h.next] = kv.Value; h.cum[h.next] = m_curCum[k][kv.Key]; h.next = (h.next + 1) % sessions; if (h.count < sessions) h.count++;
					}
					if (m_done[k] < sessions) m_done[k]++;
				}
				m_cur[k].Clear(); m_curCum[k].Clear(); m_runCum[k] = 0; m_sampled[k] = false;
			}
		}

		private static void stats(double[] a, int n, out double mean, out double sd)
		{
			double s = 0, s2 = 0; for (int i = 0; i < n; i++) { s += a[i]; s2 += a[i] * a[i]; }
			mean = s / n; double var = s2 / n - mean * mean; sd = var > 0 ? Math.Sqrt(var) : 0;
		}

		private double median(double[] a, int n)
		{
			Array.Copy(a, m_scratch, n); Array.Sort(m_scratch, 0, n);
			return n % 2 == 1 ? m_scratch[n / 2] : 0.5 * (m_scratch[n / 2 - 1] + m_scratch[n / 2]);
		}
	}
}
