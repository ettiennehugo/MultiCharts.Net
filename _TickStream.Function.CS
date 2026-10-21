// ============================================================================================
// M-TICK — lazy DataLoader 1-tick engine (spec /workspace/specs/missing-functionality-specs.md rev 4 §7).
// Plain internal sealed class, MC-free (tests/TickTests). The owner (_CustomSignal) issues the DataLoader 1-tick Trade request
// (PG p71-75, Subscribe2RT for real time) and forwards each tick via Enqueue() from the loader thread; this class only buffers
// there (lock-protected queue). CalcBar consumes on the Data1 closed bar with Aggregate(barStart, barEnd).
// Gate (§7): trade-size outputs (histogram, large prints) stay parked until rev 2.7 §E6 (dxFeed size fidelity through
// DataLoader) is verified; SizeOutputsVerified must be set by the strategy to read them (otherwise NaN / empty).
// ============================================================================================
using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	/// <summary>M-TICK configuration (protected TickConfig on _CustomSignal; no [Input]).</summary>
	public sealed class TickConfig
	{
		public bool enabled = false;
		public int historyTicks = 2000000;        // DataLoader BarsBack count in 1-tick bars (PG p73 documents BarsBack only; dxFeed tick history ~2 years)
		public bool realTime = true;              // Subscribe2RT
		public double burstZ = 3.0;               // burst flag when max prints/s z >= burstZ
		public int burstNormLength = 200;         // rolling norm of max prints/s (bars)
		public int burstMinSamples = 30;
		public double[] sizeBuckets = { 1, 2, 5, 10, 20, 50 };  // histogram upper bounds (inclusive); last bucket = above
		public double largePrintSize = 20;
		public bool sizeOutputsVerified = false;  // set true only after §E6 is verified

		public string Validate()
		{
			var e = new List<string>();
			if (historyTicks < 1) e.Add($"historyTicks={historyTicks}");
			if (!(burstZ > 0)) e.Add($"burstZ={burstZ}");
			if (burstNormLength < 3) e.Add($"burstNormLength={burstNormLength}");
			if (burstMinSamples < 2 || burstMinSamples > burstNormLength) e.Add($"burstMinSamples={burstMinSamples}");
			if (sizeBuckets == null || sizeBuckets.Length == 0) e.Add("sizeBuckets empty");
			else for (int i = 1; i < sizeBuckets.Length; i++) if (!(sizeBuckets[i] > sizeBuckets[i - 1])) { e.Add("sizeBuckets not ascending"); break; }
			if (!(largePrintSize > 0)) e.Add($"largePrintSize={largePrintSize}");
			return e.Count == 0 ? null : "TickConfig invalid: " + string.Join(", ", e);
		}
	}

	/// <summary>Per-Data1-bar tick aggregate. valid=false: no tick data covered the bar (missing/late) — do not trade on it.</summary>
	public sealed class TickBarStats
	{
		public bool valid;
		public string quality = "Unknown";        // FlowQuality tag of the source ("Tick" when valid)
		public int prints;
		public int maxPrintsPerSecond;
		public double interPrintMedianMs = double.NaN, interPrintP10Ms = double.NaN;
		public double burstZ = double.NaN; public bool burst;
		public int[] sizeHistogram;                // null unless sizeOutputsVerified
		public int largePrints = -1;               // -1 unless sizeOutputsVerified
		public double volume = double.NaN;         // NaN unless sizeOutputsVerified
	}

	internal sealed class _TickStream
	{
		public TickConfig config = new TickConfig();
		public Action<string> journal;

		private struct Tick { public DateTime t; public double p, s; }
		private readonly object m_lock = new object();
		private List<Tick> m_in = new List<Tick>();          // filled by the loader thread
		private readonly List<Tick> m_work = new List<Tick>(); // owned by the calc thread, time-sorted pending ticks
		private volatile bool m_historyDone;
		private DateTime m_lastEnd = DateTime.MinValue;
		private double[] m_norm; private int m_nCount, m_nNext; private double m_nSum, m_n2Sum;
		private bool m_invalidLogged;
		private int m_late;

		public bool Configured { get; private set; }
		public bool HistoryComplete { get { return m_historyDone; } }
		public TickBarStats Last { get; private set; } = new TickBarStats();

		public string Configure()
		{
			string e = config.Validate(); if (e != null) { Configured = false; return e; }
			m_norm = new double[config.burstNormLength]; Configured = true; Reset(); return null;
		}

		public void Reset()
		{
			lock (m_lock) m_in = new List<Tick>();
			m_work.Clear(); m_historyDone = false; m_lastEnd = DateTime.MinValue;
			m_nCount = m_nNext = 0; m_nSum = m_n2Sum = 0; m_invalidLogged = false; m_late = 0; Last = new TickBarStats();
		}

		/// <summary>Loader thread: buffer one tick (time, price, size). Never touches calc state.</summary>
		public void Enqueue(DateTime time, double price, double size)
		{
			lock (m_lock) m_in.Add(new Tick { t = time, p = price, s = size });
		}
		/// <summary>Loader thread: the History callback completed.</summary>
		public void MarkHistoryComplete() { m_historyDone = true; }

		private void drain()
		{
			List<Tick> got;
			lock (m_lock) { if (m_in.Count == 0) return; got = m_in; m_in = new List<Tick>(); }
			foreach (var k in got)
			{
				if (m_lastEnd != DateTime.MinValue && k.t <= m_lastEnd)
				{ m_late++; continue; }   // its bar is already committed: never rewrite a closed bar
				m_work.Add(k);
			}
			// loader callbacks are chronological per request; keep a stable sort for safety (history + RT overlap)
			bool sorted = true; for (int i = 1; i < m_work.Count; i++) if (m_work[i].t < m_work[i - 1].t) { sorted = false; break; }
			if (!sorted) { var a = new List<Tick>(m_work); m_work.Clear(); m_work.AddRange(System.Linq.Enumerable.OrderBy(a, k => k.t)); }   // stable
		}

		/// <summary>
		/// Calc thread, Data1 closed bar covering (barStart, barEnd] (MC bar-end stamping). Ticks with time in that range are
		/// aggregated and removed. valid=false (Journal fact on change) when no tick data has arrived up to barEnd yet.
		/// </summary>
		public TickBarStats Aggregate(DateTime barStart, DateTime barEnd)
		{
			var r = new TickBarStats();
			if (!Configured) { Last = r; return r; }
			drain();
			int lateNow = m_late; m_late = 0;
			if (lateNow > 0) journal?.Invoke($"TICK {lateNow} late tick(s) dropped before {barEnd:yyyy-MM-dd HH:mm:ss} (bar already committed)");
			// coverage: a tick at/after barEnd has arrived (the stream has passed the bar), or the history is complete and the bar
			// has ticks (the latest bar). Otherwise the data is missing or late -> invalid.
			int n = 0; while (n < m_work.Count && m_work[n].t <= barEnd) n++;
			bool covered = (m_work.Count > 0 && m_work[m_work.Count - 1].t >= barEnd) || (m_historyDone && n > 0);
			var ticks = new List<Tick>(n);
			for (int i = 0; i < n; i++) if (m_work[i].t > barStart) ticks.Add(m_work[i]);
			m_work.RemoveRange(0, n);
			m_lastEnd = barEnd;
			if (!covered)
			{
				if (!m_invalidLogged) { m_invalidLogged = true; journal?.Invoke($"TICK data missing/late for bar ending {barEnd:yyyy-MM-dd HH:mm:ss}: outputs invalid"); }
				Last = r; return r;
			}
			if (m_invalidLogged) { m_invalidLogged = false; journal?.Invoke($"TICK data valid again at {barEnd:yyyy-MM-dd HH:mm:ss}"); }
			fill(r, ticks); Last = r; return r;
		}

		private void fill(TickBarStats r, List<Tick> t)
		{
			r.valid = true; r.quality = "Tick"; r.prints = t.Count;
			int best = 0, run = 0; long sec = long.MinValue;
			for (int i = 0; i < t.Count; i++)
			{
				long s = t[i].t.Ticks / TimeSpan.TicksPerSecond;
				run = s == sec ? run + 1 : 1; sec = s; if (run > best) best = run;
			}
			r.maxPrintsPerSecond = best;
			if (t.Count >= 2)
			{
				var d = new double[t.Count - 1];
				for (int i = 1; i < t.Count; i++) d[i - 1] = (t[i].t - t[i - 1].t).TotalMilliseconds;
				Array.Sort(d); r.interPrintMedianMs = Quantile(d, 0.5); r.interPrintP10Ms = Quantile(d, 0.1);
			}
			// burst z vs the rolling norm of PREVIOUS bars (the bar itself is added after scoring)
			if (m_nCount >= config.burstMinSamples)
			{
				double m = m_nSum / m_nCount, v = m_n2Sum / m_nCount - m * m;
				if (v > 0) { r.burstZ = (best - m) / Math.Sqrt(v); r.burst = r.burstZ >= config.burstZ; }
			}
			if (m_nCount == m_norm.Length) { double o = m_norm[m_nNext]; m_nSum -= o; m_n2Sum -= o * o; } else m_nCount++;
			m_norm[m_nNext] = best; m_nSum += best; m_n2Sum += (double)best * best; m_nNext = (m_nNext + 1) % m_norm.Length;
			if (config.sizeOutputsVerified)
			{
				var b = config.sizeBuckets; r.sizeHistogram = new int[b.Length + 1]; r.largePrints = 0; r.volume = 0;
				foreach (var k in t)
				{
					int j = 0; while (j < b.Length && k.s > b[j]) j++;
					r.sizeHistogram[j]++; if (k.s >= config.largePrintSize) r.largePrints++; r.volume += k.s;
				}
			}
		}

		/// <summary>Linear-interpolated quantile of a sorted array (type 7).</summary>
		public static double Quantile(double[] sorted, double q)
		{
			if (sorted.Length == 0) return double.NaN;
			double h = (sorted.Length - 1) * q; int lo = (int)Math.Floor(h); int hi = Math.Min(lo + 1, sorted.Length - 1);
			return sorted[lo] + (h - lo) * (sorted[hi] - sorted[lo]);
		}
	}
}
