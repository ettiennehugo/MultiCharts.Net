using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 VP-3 — per-bar profile ladder (bid / ask / total volume per 1-tick row per closed bar), tagged with the rev 2.7
	// order-flow quality chain, plus HTF aggregation (e.g. the 30m ladder PS-12 needs). Built on _OrderFlow.FootprintBar.
	// Pure C# (tests/VpHelperTests); hosted lazily by _VolumeProfileBase (BarLadders.enabled, default false).
	// Bid = sell-classified volume, Ask = buy-classified volume (footprint naming). These are CLASSIFIED volumes
	// (Lee-Ready / tick rule), never an aggressor flag: MC .NET exposes none (BCS §A3).
	// S1 (McBidAsk tier) is not built yet; HasBidAsk relies on FlowSource order (sources up to TickRule carry bid/ask).
	// ============================================================================================

	// <bar-ladder>
	public sealed class LadderBar
	{
		public int bar; public DateTime time; public double tick;
		public _OrderFlow.FlowSource source = _OrderFlow.FlowSource.Unavailable;
		public _OrderFlow.FlowQuality quality = _OrderFlow.FlowQuality.Unknown;
		public long[] rows = new long[0]; public double[] bid = new double[0], ask = new double[0], total = new double[0];   // ascending rows, parallel arrays
		public double High, Low, Buy, Sell, Volume, coverage = 1;   // Buy = sum ask, Sell = sum bid; coverage for HTF bars
		public bool crossesRoll;
		public bool HasBidAsk { get { return source <= _OrderFlow.FlowSource.TickRule; } }
		public bool HasRows { get { return rows != null && rows.Length > 0; } }
		public double Delta { get { return HasBidAsk ? Buy - Sell : double.NaN; } }
	}

	public sealed class BarLadderConfig
	{
		public bool enabled = false;
		public int keepBars = 2000;                     // Data1 ladders memoised
		public int[] htfStreams = new int[0];           // Data n streams that get an aggregated ladder
		public _OrderFlow.FlowQuality minQuality = _OrderFlow.FlowQuality.Low;   // default consumer filter (consumers may pass stricter)

		public string Validate()
		{
			if (keepBars < 1) return "BarLadderConfig invalid: keepBars=" + keepBars;
			if (htfStreams == null) return "BarLadderConfig invalid: htfStreams null";
			foreach (int s in htfStreams) if (s < 2) return "BarLadderConfig invalid: htfStreams contains " + s + " (Data2+)";
			return null;
		}
		public string Canon() { return "lad:" + keepBars + ":" + string.Join(",", htfStreams ?? new int[0]) + ":" + (int)minQuality; }
	}

	public sealed class _BarLadderStore
	{
		public readonly BarLadderConfig config;
		public readonly double tick;
		private readonly Dictionary<int, LadderBar> m_byBar = new Dictionary<int, LadderBar>();
		private readonly Queue<int> m_order = new Queue<int>();

		public _BarLadderStore(BarLadderConfig cfg, double tick) { config = cfg ?? new BarLadderConfig(); this.tick = tick > 0 ? tick : 0.01; }

		public int Count { get { return m_byBar.Count; } }

		/// <summary>Memoised ladder of a Data1 bar; builds it on first query from the footprint (null = none) and the ring bar. A ladder
		/// built without a footprint is memoised only when `final` (the footprint can no longer arrive, e.g. an older bar).</summary>
		public LadderBar Get(RingBar rb, _OrderFlow.FootprintBar fp, bool final = true)
		{
			LadderBar lb;
			if (m_byBar.TryGetValue(rb.bar, out lb)) return lb;
			lb = Build(rb, fp, tick);
			if (fp == null && !final) return lb;
			m_byBar[rb.bar] = lb; m_order.Enqueue(rb.bar);
			while (m_order.Count > config.keepBars) m_byBar.Remove(m_order.Dequeue());
			return lb;
		}

		public bool TryGet(int bar, out LadderBar lb) { return m_byBar.TryGetValue(bar, out lb); }
		public void Clear() { m_byBar.Clear(); m_order.Clear(); }

		/// <summary>Ladder from a footprint when it carries one (Quote / TickRule / Aggressor), else BarUpDown totals spread uniformly over [L, H].</summary>
		public static LadderBar Build(RingBar rb, _OrderFlow.FootprintBar fp, double tick)
		{
			if (fp != null && fp.HasLadder)
			{
				var lb = new LadderBar { bar = rb.bar, time = rb.time, tick = tick, source = fp.Source, quality = fp.Quality, High = rb.h, Low = rb.l, crossesRoll = rb.rollBar };
				int n = fp.Ladder.Count, i = 0;
				lb.rows = new long[n]; lb.bid = new double[n]; lb.ask = new double[n]; lb.total = new double[n];
				foreach (var kv in fp.Ladder)   // SortedDictionary: ascending rows
				{
					lb.rows[i] = kv.Key; lb.bid[i] = kv.Value[0]; lb.ask[i] = kv.Value[1]; lb.total[i] = kv.Value[0] + kv.Value[1];
					lb.Buy += lb.ask[i]; lb.Sell += lb.bid[i]; lb.Volume += lb.total[i]; i++;
				}
				return lb;
			}
			return BarUpDown(rb, tick);
		}

		/// <summary>BarUpDown tier: total = V uniformly over the rows of [L, H]; bid / ask = NaN.</summary>
		public static LadderBar BarUpDown(RingBar rb, double tick)
		{
			var lb = new LadderBar { bar = rb.bar, time = rb.time, tick = tick, source = _OrderFlow.FlowSource.BarUpDown, quality = _OrderFlow.FlowQuality.Low,
				High = rb.h, Low = rb.l, Buy = double.NaN, Sell = double.NaN, Volume = rb.v, crossesRoll = rb.rollBar };
			if (rb.h < rb.l || rb.l <= 0 || rb.v <= 0) return lb;
			long a = _ProfileMath.Key(rb.l, tick), b = _ProfileMath.Key(rb.h, tick);
			int n = (int)(b - a + 1);
			lb.rows = new long[n]; lb.bid = new double[n]; lb.ask = new double[n]; lb.total = new double[n];
			double each = rb.v / n;
			for (int i = 0; i < n; i++) { lb.rows[i] = a + i; lb.total[i] = each; lb.bid[i] = double.NaN; lb.ask[i] = double.NaN; }
			return lb;
		}

		/// <summary>
		/// HTF ladder: row-wise merge of the Data1 ladders stamped in (prevHtfTime, htfTime]. source / quality = the worst of the parts
		/// (BCS: a cumulative value carries the minimum quality). coverage = ladders found / expectedSlots; when coverage &lt; 1 (a
		/// missing minute or a part without a ladder) bid / ask are NaN, the source is BarUpDown and the totals come from the ring
		/// bars spread uniformly. crossesRoll when any part is a roll bar.
		/// </summary>
		public static LadderBar Merge(IList<LadderBar> parts, IList<RingBar> ringBars, int expectedSlots, int htfBar, DateTime htfTime, double tick)
		{
			var res = new LadderBar { bar = htfBar, time = htfTime, tick = tick, High = double.MinValue, Low = double.MaxValue,
				source = _OrderFlow.FlowSource.Aggressor, quality = _OrderFlow.FlowQuality.Exact };
			int withRows = 0;
			bool allBidAsk = true;
			var acc = new SortedDictionary<long, double[]>();
			if (parts != null)
				foreach (LadderBar p in parts)
				{
					if (p == null) continue;
					if (p.source > res.source) res.source = p.source;
					if (p.quality > res.quality) res.quality = p.quality;
					if (p.crossesRoll) res.crossesRoll = true;
					if (!p.HasRows) continue;
					withRows++;
					if (!p.HasBidAsk) allBidAsk = false;
					for (int i = 0; i < p.rows.Length; i++)
					{
						double[] v;
						if (!acc.TryGetValue(p.rows[i], out v)) { v = new double[3]; acc[p.rows[i]] = v; }
						v[0] += p.bid[i]; v[1] += p.ask[i]; v[2] += p.total[i];
					}
				}
			if (ringBars != null)
				foreach (RingBar rb in ringBars)
				{
					if (rb.h > res.High) res.High = rb.h;
					if (rb.l > 0 && rb.l < res.Low) res.Low = rb.l;
					if (rb.rollBar) res.crossesRoll = true;
				}
			if (res.High == double.MinValue) { res.High = double.NaN; res.Low = double.NaN; }
			res.coverage = expectedSlots > 0 ? Math.Min(1.0, (double)withRows / expectedSlots) : 0;
			if (parts == null || parts.Count == 0) { res.source = _OrderFlow.FlowSource.Unavailable; res.quality = _OrderFlow.FlowQuality.Unknown; }
			if (res.coverage < 1)
			{
				res.source = _OrderFlow.FlowSource.BarUpDown;
				if (res.quality < _OrderFlow.FlowQuality.Low) res.quality = _OrderFlow.FlowQuality.Low;
				var tot = new SortedDictionary<long, double>();
				if (ringBars != null)
					foreach (RingBar rb in ringBars)
					{
						if (rb.h < rb.l || rb.l <= 0 || rb.v <= 0) continue;
						long a = _ProfileMath.Key(rb.l, tick), b = _ProfileMath.Key(rb.h, tick);
						double each = rb.v / (b - a + 1);
						for (long k = a; k <= b; k++) { double cur; tot.TryGetValue(k, out cur); tot[k] = cur + each; }
					}
				fill(res, tot.Count);
				int i = 0;
				foreach (var kv in tot) { res.rows[i] = kv.Key; res.total[i] = kv.Value; res.bid[i] = double.NaN; res.ask[i] = double.NaN; res.Volume += kv.Value; i++; }
				res.Buy = res.Sell = double.NaN;
				return res;
			}
			fill(res, acc.Count);
			int j = 0;
			foreach (var kv in acc)
			{
				res.rows[j] = kv.Key; res.total[j] = kv.Value[2];
				res.bid[j] = allBidAsk ? kv.Value[0] : double.NaN; res.ask[j] = allBidAsk ? kv.Value[1] : double.NaN;
				res.Volume += kv.Value[2];
				if (allBidAsk) { res.Buy += kv.Value[1]; res.Sell += kv.Value[0]; }
				j++;
			}
			if (!allBidAsk) { res.Buy = res.Sell = double.NaN; }
			return res;
		}

		private static void fill(LadderBar r, int n) { r.rows = new long[n]; r.bid = new double[n]; r.ask = new double[n]; r.total = new double[n]; }

		/// <summary>Dense ascending 1-tick grid of a ladder's totals (zero rows included), for fillFromHistogram / statistics.</summary>
		public static bool Dense(LadderBar lb, out double[] prices, out double[] vols)
		{
			prices = new double[0]; vols = new double[0];
			if (lb == null || !lb.HasRows) return false;
			long lo = lb.rows[0], hi = lb.rows[lb.rows.Length - 1];
			int n = (int)(hi - lo + 1);
			prices = new double[n]; vols = new double[n];
			for (int i = 0; i < n; i++) prices[i] = (lo + i) * lb.tick;
			for (int i = 0; i < lb.rows.Length; i++) vols[lb.rows[i] - lo] += lb.total[i];
			return true;
		}
	}
	// </bar-ladder>
}
