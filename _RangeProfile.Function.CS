using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 VP-1 — volume profile over any closed-bar window [t0, t1] on a stream (breakout leg, balance zone, last 30
	// minutes, AVWAP anchor to now). Pure builder + incremental handle over a _BarRing; _VolumeProfileBase converts the
	// RangeHistogram into a VpExtra through fillFromHistogram so POC / VA / HVN / LVN match Composite.
	// Review H2: useTickLadder defaults to FALSE. The bar-distribution (window M, uniform) estimator is the one used for every
	// A2-25..35 statistic; the tick-ladder version is a separate arm (its own config / engine instance) and must never be
	// mixed into one percentile lookback. Pure C# (tests/VpHelperTests).
	// ============================================================================================

	// <range-profile>
	public enum ERangeRollPolicy { Reject = 0, Truncate = 1 }

	public sealed class RangeProfileConfig
	{
		public bool enabled = false;
		public int[] streams = { 1 };              // streams that may be profiled (each gets a ring)
		public int distribution = 2;               // ProfileMath.Accumulator.Add mode: 1 close, 2 uniform over [L, H], 3 typical
		public bool useTickLadder = false;         // review H2: false = bar distribution only; true = the tick-ladder ARM (separate instance)
		public bool rthOnly = false;
		public ERangeRollPolicy rollPolicy = ERangeRollPolicy.Reject;
		public double valueAreaPercent = double.NaN;   // NaN = the base ValueAreaPercent
		public int maxRows = 4000;
		public int cacheSize = 8;
		public int maxOpenHandles = 8;
		public int ringBars = 2880;                // ring need per stream (2 Globex sessions of 1m)

		public string Validate()
		{
			var e = new List<string>();
			if (streams == null || streams.Length == 0) e.Add("streams empty");
			else foreach (int s in streams) if (s < 1) e.Add("stream " + s);
			if (distribution < 1 || distribution > 3) e.Add("distribution=" + distribution);
			if (!double.IsNaN(valueAreaPercent) && (valueAreaPercent <= 0 || valueAreaPercent > 1)) e.Add("valueAreaPercent=" + valueAreaPercent);
			if (maxRows < 1) e.Add("maxRows=" + maxRows);
			if (cacheSize < 0) e.Add("cacheSize=" + cacheSize);
			if (maxOpenHandles < 0) e.Add("maxOpenHandles=" + maxOpenHandles);
			if (ringBars < 1) e.Add("ringBars=" + ringBars);
			return e.Count == 0 ? null : "RangeProfileConfig invalid: " + string.Join(", ", e);
		}

		public string Canon()
		{
			return string.Format(CultureInfo.InvariantCulture, "rp:{0}:{1}:{2}:{3}:{4}:{5}:{6}", string.Join(",", streams ?? new int[0]), distribution,
				useTickLadder ? 1 : 0, rthOnly ? 1 : 0, (int)rollPolicy, valueAreaPercent, maxRows);
		}
	}

	/// <summary>Built histogram of a window (dense ascending 1-tick grid, zero rows included) plus window facts.</summary>
	public sealed class RangeHistogram
	{
		public double tick;
		public double[] prices = new double[0], vols = new double[0];
		public double[] ask, bid;                 // per dense row; null unless every bar used a ladder with bid / ask
		public int bars, ladderBars;
		public int startBar = -1, endBar = -1;
		public DateTime t0, t1, firstTime, lastTime;
		public double open, high, low, close, totalVol;
		public bool isPartial, crossesRoll, approximate = true, tooManyRows;
		public string tag;
		public int Rows { get { return prices.Length; } }
	}

	public sealed class RangeStats
	{
		public int bars, expectedBars, ladderBars, rows; public double coverage = double.NaN, totalVol;
		public override string ToString() { return string.Format(CultureInfo.InvariantCulture, "bars({0}/{1}) cov({2:0.###}) ladder({3}) rows({4}) vol({5})", bars, expectedBars, coverage, ladderBars, rows, totalVol); }
	}

	/// <summary>Accumulates bars (or ladder rows) onto a 1-tick grid; shared by the one-shot builder and the handle so both agree exactly.</summary>
	public sealed class RangeAccumulator
	{
		public readonly _ProfileMath.Accumulator acc;
		private readonly SortedDictionary<long, double[]> m_ba = new SortedDictionary<long, double[]>();   // row -> {bid, ask}
		public readonly RangeProfileConfig cfg;
		public int ladderBars, bidAskBars;
		public bool crossesRoll, isPartial;
		public DateTime t0, t1;

		public RangeAccumulator(double tick, RangeProfileConfig cfg) { acc = new _ProfileMath.Accumulator(tick); this.cfg = cfg ?? new RangeProfileConfig(); }

		public int Bars { get { return acc.bars; } }

		public void Add(RingBar b, LadderBar lb)
		{
			if (cfg.rthOnly && !b.rth) return;
			if (cfg.useTickLadder && lb != null && lb.HasRows && lb.source != _OrderFlow.FlowSource.BarUpDown)
			{
				// OHLC / bar count through Add with zero volume, then the exact ladder rows
				acc.Add(b.bar, b.time, b.o, b.h, b.l, b.c, 0, cfg.distribution);
				for (int i = 0; i < lb.rows.Length; i++)
				{
					acc.AddRow(lb.rows[i], lb.total[i]);
					if (lb.HasBidAsk)
					{
						double[] v;
						if (!m_ba.TryGetValue(lb.rows[i], out v)) { v = new double[2]; m_ba[lb.rows[i]] = v; }
						v[0] += lb.bid[i]; v[1] += lb.ask[i];
					}
				}
				ladderBars++;
				if (lb.HasBidAsk) bidAskBars++;
			}
			else acc.Add(b.bar, b.time, b.o, b.h, b.l, b.c, b.v, cfg.distribution);
		}

		public RangeHistogram Build(string tag = null)
		{
			var h = new RangeHistogram { tick = acc.tick, bars = acc.bars, ladderBars = ladderBars, startBar = acc.firstBar, endBar = acc.lastBar,
				firstTime = acc.firstTime, lastTime = acc.lastTime, open = acc.open, high = acc.high, low = acc.low, close = acc.close,
				isPartial = isPartial, crossesRoll = crossesRoll, t0 = t0, t1 = t1, tag = tag };
			h.approximate = !(acc.bars > 0 && ladderBars == acc.bars);
			if (acc.rows.Count == 0) return h;
			long lo = long.MaxValue, hi = long.MinValue;
			foreach (long k in acc.rows.Keys) { if (k < lo) lo = k; if (k > hi) hi = k; }
			if (hi - lo + 1 > cfg.maxRows) { h.tooManyRows = true; return h; }
			double[] dp, dv;
			acc.Dense(out dp, out dv);
			h.prices = dp; h.vols = dv;
			for (int i = 0; i < dv.Length; i++) h.totalVol += dv[i];
			if (acc.bars > 0 && bidAskBars == acc.bars)
			{
				h.bid = new double[dv.Length]; h.ask = new double[dv.Length];
				foreach (var kv in m_ba) { long i = kv.Key - lo; if (i >= 0 && i < dv.Length) { h.bid[i] += kv.Value[0]; h.ask[i] += kv.Value[1]; } }
			}
			return h;
		}
	}

	public static class RangeProfileBuilder
	{
		/// <summary>
		/// One-shot profile of the ring bars with t0 &lt;= time &lt;= t1 (t0 &lt; time when t0Exclusive, e.g. the A2-30 (t-30m, t] window).
		/// Null when no bar is in the window or rollPolicy = Reject and a roll bar lies inside (t0, t1] (a roll on the first bar is fine).
		/// Truncate starts at the newest roll bar and sets isPartial. isPartial is also set when the ring has dropped bars at or after t0.
		/// ladder(ringBar) may be null (no VP-3 store).
		/// </summary>
		public static RangeHistogram Build(_BarRing ring, DateTime t0, DateTime t1, double tick, RangeProfileConfig cfg, Func<RingBar, LadderBar> ladder, bool t0Exclusive = false, string tag = null)
		{
			if (ring == null || ring.Count == 0 || t1 < t0) return null;
			cfg = cfg ?? new RangeProfileConfig();
			int i1 = ring.IndexAtOrBefore(t1), i0 = ring.FirstIndexAtOrAfter(t0, t0Exclusive);
			if (i1 < 0 || i0 > i1) return null;
			bool partial = ring.Dropped && ring.At(0).time > t0 && i0 == 0;
			for (int i = i1; i > i0; i--)
				if (ring.At(i).rollBar)
				{
					if (cfg.rollPolicy == ERangeRollPolicy.Reject) return null;
					i0 = i; partial = true; break;
				}
			var ra = new RangeAccumulator(tick, cfg) { t0 = t0, t1 = t1, isPartial = partial };
			for (int i = i0; i <= i1; i++)
			{
				RingBar b = ring.At(i);
				ra.Add(b, ladder != null && cfg.useTickLadder ? ladder(b) : null);
			}
			return ra.Build(tag);
		}

		/// <summary>Bars by ring offsets: the window of `count` bars ending `barsBack` bars before the newest (0 = ends at the newest).</summary>
		public static RangeHistogram BuildBars(_BarRing ring, int barsBack, int count, double tick, RangeProfileConfig cfg, Func<RingBar, LadderBar> ladder, string tag = null)
		{
			if (ring == null || count < 1 || barsBack < 0 || barsBack >= ring.Count) return null;
			int newest = ring.Count - 1 - barsBack, oldest = newest - count + 1;
			bool partial = oldest < 0;
			if (oldest < 0) oldest = 0;
			return Build(ring, ring.At(oldest).time, ring.At(newest).time, tick, cfg, ladder, false, tag) is RangeHistogram h ? markPartial(h, partial) : null;
		}

		private static RangeHistogram markPartial(RangeHistogram h, bool partial) { if (partial) h.isPartial = true; return h; }

		/// <summary>
		/// Expected stream-resolution slots in [t0, t1]: bar end stamps t = t0 + k x res (k &gt;= 0, or &gt;= 1 when t0Exclusive) up to t1
		/// whose slot start (t - res) the session clock reports open. Skips the daily halt, holidays and early closes via isOpen.
		/// </summary>
		public static int ExpectedSlots(DateTime t0, DateTime t1, TimeSpan res, Func<DateTime, bool> isOpen, bool t0Exclusive = false)
		{
			if (res <= TimeSpan.Zero || t1 < t0) return 0;
			int n = 0;
			for (DateTime t = t0Exclusive ? t0 + res : t0; t <= t1; t += res)
				if (isOpen == null || isOpen(t - res)) n++;
			return n;
		}

		public static RangeStats Stats(RangeHistogram h, int expectedBars)
		{
			if (h == null) return null;
			return new RangeStats { bars = h.bars, expectedBars = expectedBars, ladderBars = h.ladderBars, rows = h.Rows, totalVol = h.totalVol,
				coverage = expectedBars > 0 ? (double)h.bars / expectedBars : double.NaN };
		}

		public static string Tag(DateTime t0, DateTime t1)
		{
			return "RP:" + t0.ToString("yyyyMMdd'T'HHmm", CultureInfo.InvariantCulture) + "-" + t1.ToString("HHmm", CultureInfo.InvariantCulture);
		}
	}

	/// <summary>
	/// Anchored, open-ended range profile that grows with each closed bar (Step). Profile is rebuilt lazily only when read and
	/// dirty. Close(t1) freezes it. A roll bar after the anchor closes the handle with crossesRoll = true (the roll bar is not added).
	/// </summary>
	public sealed class RangeProfileHandle
	{
		private readonly RangeAccumulator m_acc;
		private RangeHistogram m_cached;
		private bool m_dirty = true;
		public readonly int stream;
		public readonly DateTime t0;
		public readonly string Tag;
		public bool IsClosed { get; private set; }
		public DateTime ClosedAt { get; private set; }
		public DateTime ClosedDay { get; private set; }
		public bool CrossesRoll { get { return m_acc.crossesRoll; } }
		public int Bars { get { return m_acc.Bars; } }
		/// <summary>Last bar number fed (bars must arrive in increasing order; older / repeated bars are ignored).</summary>
		public int LastBar { get { return m_lastBar; } }
		/// <summary>Optional host hook run before Profile is read (the host catches the handle up to the newest closed bar).</summary>
		public Action<RangeProfileHandle> beforeRead;
		private int m_lastBar = -1;
		private DateTime m_lastDay;

		public RangeProfileHandle(int stream, DateTime t0, string tag, double tick, RangeProfileConfig cfg)
		{
			this.stream = stream; this.t0 = t0; Tag = tag ?? ("RP:" + t0.ToString("yyyyMMdd'T'HHmm", CultureInfo.InvariantCulture) + "-open");
			m_acc = new RangeAccumulator(tick, cfg) { t0 = t0, t1 = DateTime.MaxValue };
		}

		/// <summary>Feed one closed bar (idempotent per bar number). Returns true when the bar caused a roll close.</summary>
		public bool Step(RingBar b, LadderBar lb)
		{
			if (IsClosed || b.time < t0 || b.bar <= m_lastBar) return false;
			if (b.rollBar && m_acc.Bars > 0)
			{
				m_acc.crossesRoll = true; IsClosed = true; ClosedAt = b.time; ClosedDay = b.tradingDay; m_dirty = true;
				return true;
			}
			m_lastBar = b.bar; m_lastDay = b.tradingDay;
			m_acc.Add(b, lb);
			m_dirty = true;
			return false;
		}

		/// <summary>Seed from bars already in the ring (anchor in the past).</summary>
		public void Backfill(_BarRing ring, Func<RingBar, LadderBar> ladder)
		{
			if (ring == null) return;
			int i0 = ring.FirstIndexAtOrAfter(t0);
			if (ring.Dropped && i0 == 0 && ring.Count > 0 && ring.At(0).time > t0) m_acc.isPartial = true;
			for (int i = i0; i < ring.Count && !IsClosed; i++) { RingBar b = ring.At(i); Step(b, ladder != null && m_acc.cfg.useTickLadder ? ladder(b) : null); }
		}

		public void Close(DateTime t1)
		{
			if (IsClosed) return;
			IsClosed = true; ClosedAt = t1; ClosedDay = m_lastDay; m_acc.t1 = t1; m_dirty = true;
		}

		public RangeHistogram Profile
		{
			get
			{
				if (beforeRead != null && !IsClosed) beforeRead(this);
				if (m_dirty || m_cached == null) { m_cached = m_acc.Build(Tag); m_dirty = false; }
				return m_cached;
			}
		}
	}
	// </range-profile>
}
