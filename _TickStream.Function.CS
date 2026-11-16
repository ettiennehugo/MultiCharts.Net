// ============================================================================================
// M-TICK — lazy DataLoader 1-tick engine (spec /workspace/specs/missing-functionality-specs.md rev 4 §7).
// Plain internal sealed classes, MC-free (tests/TickTests). The owner (_CustomSignal) issues the DataLoader 1-tick Trade requests
// (PG p71-75; Subscribe2RT only on a live chart) and forwards the ticks from the loader thread; this file only buffers there
// (lock-protected queues). CalcBar consumes on the Data1 closed bar with Aggregate(barStart, barEnd[, ready]).
// FromTo chart: TickChunkFeed plans one request per trading session of Data1 bars (first bar - 1 day .. last bar), holds each day
// until it completes, releases days in order and answers Ready(barEnd) for the bounded wait (tick-chunk pattern of
// _VolumeProfileFuturesBase, specs/mcbidask-recalc-chunking-spec.md §5). Other charts: one BarsBack request of historyTicks.
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
		public int historyTicks = 2000000;        // DataLoader BarsBack count in 1-tick bars; used only when the chart range is not FromTo
		public bool realTime = true;              // Subscribe2RT, and only on a live chart (never on a historical backtest)
		// FromTo chart (tick-chunk pattern of _VolumeProfileFuturesBase, specs/mcbidask-recalc-chunking-spec.md §5): one FromTo request
		// per trading session of Data1 bars (gap > sessionGapMinutes), from the first bar - 1 day to the last bar (never past it), and a bounded wait in CalcBar.
		public bool chunkByDay = true;            // false = the single BarsBack request (A/B only)
		public int chunkWaitMs = 120000;          // bounded wait (historical calc) for the day covering the bar; 0 = no wait
		public int chunkMaxInFlightDays = 2;      // 1..4 day requests in flight (prefetch)
		public int chunkSplitOnFail = 4;          // a failed day is re-requested once as this many sub-windows; 0 = no retry
		public int chunkTimeoutSec = 300;         // an in-flight day silent this long is failed (then split); 0 = no timeout
		public int batchLines = 20;               // 'TICK callback' lines for the first N DataLoader callbacks of a pass (completed ones always)
		public int sessionGapMinutes = 30;        // a gap between Data1 bar closes longer than this starts a new request window (session)
		public int firstCallbackSec = 30;         // no callback at all this long after the first request: Warning, waits off for the run
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
			if (chunkWaitMs < 0) e.Add($"chunkWaitMs={chunkWaitMs}");
			if (chunkMaxInFlightDays < 1 || chunkMaxInFlightDays > 4) e.Add($"chunkMaxInFlightDays={chunkMaxInFlightDays}");
			if (chunkSplitOnFail < 0) e.Add($"chunkSplitOnFail={chunkSplitOnFail}");
			if (chunkTimeoutSec < 0) e.Add($"chunkTimeoutSec={chunkTimeoutSec}");
			if (batchLines < 0) e.Add($"batchLines={batchLines}");
			if (sessionGapMinutes < 1) e.Add($"sessionGapMinutes={sessionGapMinutes}");
			if (firstCallbackSec < 1) e.Add($"firstCallbackSec={firstCallbackSec}");
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

	/// <summary>One trade print (close-stamped time, price, size).</summary>
	public struct TickPrint { public DateTime t; public double p, s; }

	internal sealed class _TickStream
	{
		public TickConfig config = new TickConfig();
		public Action<string> journal;

		private readonly object m_lock = new object();
		private List<TickPrint> m_in = new List<TickPrint>();          // filled by the loader thread
		private readonly List<TickPrint> m_work = new List<TickPrint>(); // owned by the calc thread, time-sorted pending ticks
		private volatile bool m_historyDone;
		private DateTime m_lastEnd = DateTime.MinValue;
		private double[] m_norm; private int m_nCount, m_nNext; private double m_nSum, m_n2Sum;
		private bool m_invalidLogged;
		private int m_late;
		private DateTime m_firstSeen = DateTime.MaxValue;   // earliest tick ever drained (legacy coverage: no bar before the stream starts)
		private List<string> m_notes = new List<string>();  // loader-thread lines, drained to the journal on the calc thread

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
			lock (m_lock) m_in = new List<TickPrint>();
			m_work.Clear(); m_historyDone = false; m_lastEnd = DateTime.MinValue;
			m_nCount = m_nNext = 0; m_nSum = m_n2Sum = 0; m_invalidLogged = false; m_late = 0; Last = new TickBarStats();
			m_firstSeen = DateTime.MaxValue;
			lock (m_lock) m_notes = new List<string>();
		}

		/// <summary>Any thread: queue a journal line (DataLoader callbacks must not trace). Written by DrainNotes / Aggregate.</summary>
		public void Note(string line) { lock (m_lock) m_notes.Add(line); }

		/// <summary>Calc thread: write the queued lines to the journal.</summary>
		public void DrainNotes()
		{
			List<string> n;
			lock (m_lock) { if (m_notes.Count == 0) return; n = m_notes; m_notes = new List<string>(); }
			if (journal != null) foreach (string l in n) journal(l);
		}

		/// <summary>Loader thread: buffer many ticks at once (one lock).</summary>
		public void EnqueueRange(List<TickPrint> ticks)
		{
			if (ticks == null || ticks.Count == 0) return;
			lock (m_lock) m_in.AddRange(ticks);
		}

		/// <summary>Loader thread: buffer one tick (time, price, size). Never touches calc state.</summary>
		public void Enqueue(DateTime time, double price, double size)
		{
			lock (m_lock) m_in.Add(new TickPrint { t = time, p = price, s = size });
		}
		/// <summary>Loader thread: the History callback completed.</summary>
		public void MarkHistoryComplete() { m_historyDone = true; }

		private void drain()
		{
			List<TickPrint> got;
			lock (m_lock) { if (m_in.Count == 0) return; got = m_in; m_in = new List<TickPrint>(); }
			foreach (var k in got)
			{
				if (m_lastEnd != DateTime.MinValue && k.t <= m_lastEnd)
				{ m_late++; continue; }   // its bar is already committed: never rewrite a closed bar
				if (k.t < m_firstSeen) m_firstSeen = k.t;
				m_work.Add(k);
			}
			// loader callbacks are chronological per request; keep a stable sort for safety (history + RT overlap)
			bool sorted = true; for (int i = 1; i < m_work.Count; i++) if (m_work[i].t < m_work[i - 1].t) { sorted = false; break; }
			if (!sorted) { var a = new List<TickPrint>(m_work); m_work.Clear(); m_work.AddRange(System.Linq.Enumerable.OrderBy(a, k => k.t)); }   // stable
		}

		/// <summary>
		/// Calc thread, Data1 closed bar covering (barStart, barEnd] (MC bar-end stamping). Ticks with time in that range are
		/// aggregated and removed. valid=false (Journal fact on change) when no tick data has arrived up to barEnd yet.
		/// </summary>
		public TickBarStats Aggregate(DateTime barStart, DateTime barEnd) { return Aggregate(barStart, barEnd, null); }

		/// <summary>
		/// As Aggregate(barStart, barEnd), with the coverage decided by the caller when it knows it: ready = true when the loaded window
		/// covering barEnd is complete and non-empty (chunked FromTo history), false when it is not (missing, failed or still loading).
		/// null = the stream rule: a tick at/after barEnd has arrived, or the history is complete and the bar has ticks; and in both
		/// cases the stream started at or before barEnd (a bar before the first tick is never valid with zero prints).
		/// </summary>
		public TickBarStats Aggregate(DateTime barStart, DateTime barEnd, bool? ready)
		{
			var r = new TickBarStats();
			if (!Configured) { Last = r; return r; }
			DrainNotes();
			drain();
			int lateNow = m_late; m_late = 0;
			if (lateNow > 0) journal?.Invoke($"TICK {lateNow} late tick(s) dropped before {barEnd:yyyy-MM-dd HH:mm:ss} (bar already committed)");
			// coverage: a tick at/after barEnd has arrived (the stream has passed the bar), or the history is complete and the bar
			// has ticks (the latest bar). Otherwise the data is missing or late -> invalid.
			int n = 0; while (n < m_work.Count && m_work[n].t <= barEnd) n++;
			bool covered = ready.HasValue ? ready.Value
				: m_firstSeen <= barEnd && ((m_work.Count > 0 && m_work[m_work.Count - 1].t >= barEnd) || (m_historyDone && n > 0));
			var ticks = new List<TickPrint>(n);
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

		private void fill(TickBarStats r, List<TickPrint> t)
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

	/// <summary>
	/// M-TICK chunked FromTo history (MC-free; tests/TickTests). Reuses the tick-chunk scheduler of _VolumeProfileFuturesBase
	/// (_OrderFlow.TickChunkScheduler, specs/mcbidask-recalc-chunking-spec.md §5) with one stream (Trade). The owner (_CustomSignal)
	/// issues the DataLoader requests on the calc thread (Next, ShouldEnd) and forwards each callback batch (OnHistory, OnFail,
	/// OnRealtime) from the loader thread. A day's ticks are held until the day completes, then handed to the _TickStream in day
	/// order. Ready() tells the owner whether the bar's day is loaded (wait / valid logic). Every member locks (lock order: this
	/// feed, then the stream). Windows are in the request (bar) zone; DayChunk.FromEt/ToEt carry them unchanged.
	/// </summary>
	internal sealed class TickChunkFeed
	{
		private sealed class ChunkBuf { public List<TickPrint> ticks = new List<TickPrint>(); public int batches; public long outOfWindow; public DateTime first = DateTime.MaxValue, last = DateTime.MinValue; }

		private readonly object m_lock = new object();
		private readonly _OrderFlow.TickChunkScheduler m_s;
		private readonly _TickStream m_stream;
		private readonly int m_batchLines;
		private int m_batches;
		private bool m_batchCapNoted;
		private List<TickPrint> m_rtBuf;
		private volatile bool m_wake = true;
		private volatile bool m_allFinished;
		private DateTime m_first = DateTime.MaxValue, m_last = DateTime.MinValue;
		private long m_outOfWindow;
		private int m_emptyDays; private DateTime m_emptyFrom, m_emptyTo;   // sessions that completed with no tick in the window
		private DateTime m_coverFrom, m_coverTo; private bool m_coverOk;   // calc thread: the last ready window
		/// <summary>Plan window: (From, To]; To is the last bar of the chart (no request goes past it).</summary>
		public readonly DateTime From, To;
		public readonly int Days;
		public int NextId { get { lock (m_lock) return m_s.NextId; } }
		/// <summary>Every day finished (done, failed or given up) and released.</summary>
		public bool AllFinished { get { return m_allFinished; } }
		/// <summary>Sessions whose request completed with no tick in the window (no local 1-tick history), and the first one.</summary>
		public int EmptyDays(out DateTime firstFrom, out DateTime firstTo) { lock (m_lock) { firstFrom = m_emptyFrom; firstTo = m_emptyTo; return m_emptyDays; } }

		public TickChunkFeed(_TickStream stream, List<_OrderFlow.DayChunk> days, int maxInFlightDays, int splitOnFail, int firstId, int batchLines)
		{
			m_stream = stream;
			m_batchLines = Math.Max(0, batchLines);
			m_s = new _OrderFlow.TickChunkScheduler(days, new[] { 0 }, maxInFlightDays, splitOnFail, firstId);
			Days = days.Count;
			if (days.Count > 0) { From = days[0].FromEt; To = days[days.Count - 1].ToEt; }
			m_allFinished = days.Count == 0;
		}

		/// <summary>
		/// One window per calendar date (bar zone) of the Data1 bar closes, ascending: (end of the previous window, last close of the
		/// date]. The first window starts at from (first bar - 1 day); the last ends at the chart's last bar. Contiguous and half-open,
		/// so every bar's (Time[1], Time[0]] lies in exactly one window and no window reaches past the last bar. Days without bars
		/// (weekends, holidays) get no request. Non-increasing times are ignored.
		/// </summary>
		/// <summary>
		/// One window per trading session of the Data1 bars: a gap between consecutive bar closes longer than gap (default 30 min)
		/// ends a session (CME: the 16:00-17:00 CT halt and the weekend; stocks: the overnight). Window = (end of the previous window,
		/// last close of the session]; the first starts at from (first bar - 1 day), the last ends at the chart's last bar. So on a
		/// CME chart in Central time a window runs from the previous session's 16:00 close to this session's 16:00 close (the halt
		/// carries no ticks), the same session cut as the VPF Globex-day chunks. Contiguous and half-open. Label = date of the last bar.
		/// </summary>
		public static List<_OrderFlow.DayChunk> PlanSessions(IEnumerable<DateTime> barCloses, DateTime from, TimeSpan gap)
		{
			var days = new List<_OrderFlow.DayChunk>();
			DateTime prevEnd = from, curLast = DateTime.MinValue, last = DateTime.MinValue;
			foreach (DateTime t in barCloses)
			{
				if (t <= last || t <= from) continue;
				if (curLast != DateTime.MinValue && t - curLast > gap)
				{
					days.Add(new _OrderFlow.DayChunk { GlobexDate = curLast.Date, FromEt = prevEnd, ToEt = curLast });
					prevEnd = curLast;
				}
				last = t; curLast = t;
			}
			if (curLast != DateTime.MinValue) days.Add(new _OrderFlow.DayChunk { GlobexDate = curLast.Date, FromEt = prevEnd, ToEt = curLast });
			return days;
		}

		/// <summary>Fail-fast rule: requests were issued at firstIssueUtc, no callback of any kind has arrived (callbacks == 0), and
		/// sec seconds have passed.</summary>
		public static bool NoCallbackTimeout(int callbacks, DateTime firstIssueUtc, DateTime nowUtc, int sec)
		{
			return callbacks == 0 && firstIssueUtc != DateTime.MinValue && (nowUtc - firstIssueUtc).TotalSeconds >= sec;
		}

		/// <summary>The plain-language hint when no 1-tick history came back: MultiCharts reads 1-tick history from the local
		/// QuoteManager database, so a backtest window must be downloaded there first (owner, 2026-11-16).</summary>
		public static string MissingHistoryText(string symbol, DateTime from, DateTime to)
		{
			return $"no 1-tick history was returned for {symbol} from {from:yyyy-MM-dd HH:mm} to {to:yyyy-MM-dd HH:mm}: the 1-tick data may need " +
				$"downloading in QuoteManager for that window before the backtest";
		}

		/// <summary>Calendar-day windows (superseded by PlanSessions for M-TICK; kept for tests and A/B).</summary>
		public static List<_OrderFlow.DayChunk> PlanDays(IEnumerable<DateTime> barCloses, DateTime from)
		{
			var days = new List<_OrderFlow.DayChunk>();
			DateTime prevEnd = from, curDate = DateTime.MinValue, curLast = DateTime.MinValue, last = DateTime.MinValue;
			foreach (DateTime t in barCloses)
			{
				if (t <= last || t <= from) continue;
				last = t;
				if (curDate != DateTime.MinValue && t.Date != curDate)
				{
					days.Add(new _OrderFlow.DayChunk { GlobexDate = curDate, FromEt = prevEnd, ToEt = curLast });
					prevEnd = curLast;
				}
				curDate = t.Date; curLast = t;
			}
			if (curDate != DateTime.MinValue) days.Add(new _OrderFlow.DayChunk { GlobexDate = curDate, FromEt = prevEnd, ToEt = curLast });
			return days;
		}

		/// <summary>Calc thread: true once after a callback changed something (the owner then pumps).</summary>
		public bool TakeWake() { if (!m_wake) return false; m_wake = false; return true; }

		/// <summary>Calc thread: the days to request now (InFlight; ReqFrom/ReqTo = the window). Never skips late days (wait mode).</summary>
		public List<_OrderFlow.TickChunk> Next(DateTime utcNow)
		{
			lock (m_lock)
			{
				List<_OrderFlow.TickChunk> issue = m_s.Next(DateTime.MinValue, false, null);
				foreach (_OrderFlow.TickChunk c in issue) { c.ReqFrom = c.FromEt; c.ReqTo = c.ToEt; c.IssuedAt = utcNow; }
				return issue;
			}
		}

		/// <summary>Calc thread: fail in-flight days silent for timeoutSec (a first failure splits). Returns the journal lines.</summary>
		public List<string> Timeouts(DateTime utcNow, int timeoutSec)
		{
			var lines = new List<string>();
			if (timeoutSec <= 0) return lines;
			lock (m_lock)
			{
				foreach (_OrderFlow.TickChunk c in m_s.TimedOut(utcNow, TimeSpan.FromSeconds(timeoutSec)))
				{
					string outcome = m_s.Complete(c, false, 0, null);
					lines.Add($"TICK chunk timeout day({c.Day:yyyy-MM-dd}) id({c.Id}) sub({c.Sub}) attempt({c.Attempt}) timeoutSec({timeoutSec}) next({outcome})");
				}
				if (lines.Count > 0) { release(); m_wake = true; }
			}
			return lines;
		}

		/// <summary>Calc thread: the request of chunk id can be ended (finished; a live chart keeps its done RT day open).</summary>
		public bool ShouldEnd(int id, bool live)
		{
			lock (m_lock)
			{
				_OrderFlow.TickChunk c = m_s.Find(id);
				if (c == null || c.Replaced) return true;
				if (!c.Finished) return false;
				return !(c.Rt && live && c.State == _OrderFlow.ChunkState.Done);
			}
		}

		/// <summary>Loader thread: one History callback batch of chunk id. Ticks outside (ReqFrom, ReqTo] are counted, never used.</summary>
		public void OnHistory(int id, List<TickPrint> batch, bool completed)
		{
			lock (m_lock)
			{
				_OrderFlow.TickChunk c = m_s.Find(id);
				if (c == null || c.Replaced || c.State != _OrderFlow.ChunkState.InFlight) return;   // an earlier pass, or replaced
				c.LastActivity = DateTime.UtcNow;
				var buf = c.Payload as ChunkBuf;
				if (buf == null) c.Payload = buf = new ChunkBuf();
				buf.batches++;
				int n = 0; DateTime bf = DateTime.MaxValue, bl = DateTime.MinValue;
				if (batch != null)
					foreach (TickPrint k in batch)
					{
						if (k.t <= c.ReqFrom || k.t > c.ReqTo) { buf.outOfWindow++; m_outOfWindow++; continue; }
						buf.ticks.Add(k); n++;
						if (k.t < bf) bf = k.t;
						if (k.t > bl) bl = k.t;
					}
				if (bf < buf.first) buf.first = bf;
				if (bl > buf.last) buf.last = bl;
				m_batches++;
				if (m_batches <= m_batchLines)
					m_stream.Note($"TICK batch day({c.Day:yyyy-MM-dd}) id({c.Id}) sub({c.Sub}) n({n}) first({fmt(bf)}) last({fmt(bl)}) outOfWindow({(batch == null ? 0 : batch.Count - n)}) completed({completed})");
				else if (!m_batchCapNoted && m_batchLines > 0)
				{
					m_batchCapNoted = true;
					m_stream.Note($"TICK batch lines capped at {m_batchLines}; one 'TICK chunk done' line per day follows");
				}
				if (!completed) return;
				m_s.Complete(c, true, buf.ticks.Count, buf);
				if (buf.ticks.Count == 0 && m_emptyDays++ == 0) { m_emptyFrom = c.ReqFrom; m_emptyTo = c.ReqTo; }
				m_stream.Note($"TICK chunk done day({c.Day:yyyy-MM-dd}) id({c.Id}) sub({c.Sub}) ticks({buf.ticks.Count}) batches({buf.batches}) first({fmt(buf.first)}) " +
					$"last({fmt(buf.last)}) window({c.ReqFrom:yyyy-MM-dd HH:mm}..{c.ReqTo:yyyy-MM-dd HH:mm}) outOfWindow({buf.outOfWindow}) sec({(DateTime.UtcNow - c.IssuedAt).TotalSeconds:0.0})");
				release();
				m_wake = true;
			}
		}

		/// <summary>Loader thread: the callback of chunk id threw (the chunk fails; a first failure splits).</summary>
		public void OnFail(int id, string error)
		{
			lock (m_lock)
			{
				_OrderFlow.TickChunk c = m_s.Find(id);
				if (c == null || c.Replaced || c.State != _OrderFlow.ChunkState.InFlight) return;
				string outcome = m_s.Complete(c, false, 0, null);
				m_stream.Note($"TICK chunk failed day({c.Day:yyyy-MM-dd}) id({c.Id}) sub({c.Sub}) attempt({c.Attempt}) next({outcome}) error({error})");
				release();
				m_wake = true;
			}
		}

		/// <summary>Loader thread: a real-time tick on the RT day (live chart). Held until that day's history is released.</summary>
		public void OnRealtime(int id, TickPrint t)
		{
			lock (m_lock)
			{
				_OrderFlow.TickChunk c = m_s.Find(id);
				if (c == null || !c.Rt || c.State == _OrderFlow.ChunkState.Failed) return;
				if (c.Released) { m_stream.Enqueue(t.t, t.p, t.s); return; }
				(m_rtBuf ?? (m_rtBuf = new List<TickPrint>())).Add(t);
			}
		}

		// Under m_lock: hand finished days to the stream in day order.
		private void release()
		{
			foreach (_OrderFlow.TickChunk c in m_s.ReleasableInOrder())
			{
				var buf = c.Payload as ChunkBuf;
				c.Payload = null;
				if (buf != null && buf.ticks.Count > 0)
				{
					m_stream.EnqueueRange(buf.ticks);
					if (buf.first < m_first) m_first = buf.first;
					if (buf.last > m_last) m_last = buf.last;
				}
				if (c.Rt && m_rtBuf != null) { m_stream.EnqueueRange(m_rtBuf); m_rtBuf = null; }
			}
			if (m_s.Pending() == 0) m_allFinished = true;
		}

		/// <summary>
		/// Calc thread: is the day covering the bar closing at barEnd loaded? true = done, released and non-empty (a day with no tick
		/// at all is missing data, not a quiet day); false = failed, empty or still loading (waitId = its chunk id while it is still
		/// pending or in flight, else 0); null = after the plan (a live chart's real-time bars: the caller uses the stream rule).
		/// </summary>
		public bool? Ready(DateTime barEnd, out int waitId)
		{
			waitId = 0;
			if (m_coverOk && barEnd > m_coverFrom && barEnd <= m_coverTo) return true;
			if (barEnd > To) return null;
			lock (m_lock)
			{
				_OrderFlow.TickChunk c = m_s.CoverAt(0, barEnd);
				if (c == null) return false;
				if (c.State == _OrderFlow.ChunkState.Done && c.Released)
				{
					if (c.Ticks <= 0) return false;
					m_coverFrom = c.FromEt; m_coverTo = c.ToEt; m_coverOk = true;
					return true;
				}
				if (!c.Finished || c.State == _OrderFlow.ChunkState.Done) waitId = c.Id;
				return false;
			}
		}

		/// <summary>Days still to finish or release.</summary>
		public int Pending() { lock (m_lock) return m_s.Pending(); }

		/// <summary>The history summary line (calc thread; once when AllFinished, or at StopCalc).</summary>
		public string SummaryLine(string what)
		{
			int done, failed, skipped; long ticks, late;
			lock (m_lock)
			{
				m_s.Summary(out done, out failed, out skipped, out ticks, out late);
				return $"TICK history {what} days({Days}) done({done}) failed({failed}) pending({m_s.Pending()}) ticks({ticks}) first({fmt(m_first)}) last({fmt(m_last)}) " +
					$"planTo({To:yyyy-MM-dd HH:mm:ss}) outOfWindow({m_outOfWindow}) batches({m_batches})";
			}
		}

		private static string fmt(DateTime t) { return t == DateTime.MaxValue || t == DateTime.MinValue ? "-" : t.ToString("yyyy-MM-dd HH:mm:ss.fff"); }
	}
}
