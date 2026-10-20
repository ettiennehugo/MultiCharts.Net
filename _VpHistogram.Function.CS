using PowerLanguage.Strategy;
using PowerLanguage.VolumeProfile;
using System;
using System.Collections.Generic;
using static PowerLanguage.Strategy._VolumeProfileBase;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Spec VPB-9 (analyst-base-class-spec.md): stateless histogram / level helpers of _VolumeProfileBase (dense rows, smoothing,
	/// percentiles, node construction, level reads), moved verbatim into a helper file per the CS-0 probe convention
	/// (_SignalJournal.Function.CS: plain class in PowerLanguage.Function, class name = file name). _VolumeProfileBase imports
	/// it with using static, so the call sites are unchanged.
	/// </summary>
	internal static class _VpHistogram
	{
	internal static void clearHistogram(VpExtra extra)
	{
		extra.hvns.Clear();
		extra.lvns.Clear();
		extra.levels.Clear();
		extra.maxTotalPercentile = 0;
		extra.maxAskPercentile = 0;
		extra.maxBidPercentile = 0;
	}

	internal static void readLevels(IProfile vp, List<LevelVol> dest)
	{
		if (vp == null || vp.Values == null || dest == null) return;
		foreach (ILevel lvl in vp.Values)
		{
			if (lvl == null || lvl.Price == null) continue;
			double ask = 0, bid = 0, total = 0;
			try { ask = (double)lvl.AskTradedValue; } catch { }
			try { bid = (double)lvl.BidTradedValue; } catch { }
			try { total = (double)lvl.TotalValue; } catch { }
			if (total <= 0) total = ask + bid;
			if (total <= 0 && ask <= 0 && bid <= 0) continue;
			dest.Add(new LevelVol
			{
				price = lvl.Price.Dbl,
				total = total,
				ask = ask,
				bid = bid
			});
		}
		dest.Sort((a, b) => a.price.CompareTo(b.price));
	}

	internal static List<Row> rowsFromLevels(List<LevelVol> levels)
	{
		List<Row> rows = new List<Row>(levels != null ? levels.Count : 0);
		if (levels == null) return rows;
		for (int i = 0; i < levels.Count; i++)
		{
			if (levels[i].total <= 0) continue;
			rows.Add(new Row { price = levels[i].price, volume = levels[i].total });
		}
		return rows;
	}

	/// Dense rows from lowest to highest level at the profile's row step (multiple of tick). Missing prices get volume 0.
	/// traded[i] is true where the source profile had volume.
	internal static List<Row> denseRowsFromLevels(List<LevelVol> levels, double tick, out bool[] traded)
	{
		traded = new bool[0];
		var rows = new List<Row>();
		if (levels == null || levels.Count == 0 || tick <= 0) return rows;
		double step = 0;
		for (int i = 1; i < levels.Count; i++)
		{
			double g = levels[i].price - levels[i - 1].price;
			if (g > tick * 0.5 && (step == 0 || g < step)) step = g;
		}
		if (step <= 0) step = tick;
		step = Math.Max(1, Math.Round(step / tick)) * tick;   // row size is a whole number of ticks
		double lo = levels[0].price, hi = levels[levels.Count - 1].price;
		int n = (int)Math.Round((hi - lo) / step) + 1;
		if (n < 1 || n > MAX_DENSE_ROWS) return rows;          // caller falls back to sparse rows
		var vol = new double[n];
		traded = new bool[n];
		for (int i = 0; i < levels.Count; i++)
		{
			int idx = (int)Math.Round((levels[i].price - lo) / step);
			if (idx < 0 || idx >= n || levels[i].total <= 0) continue;
			vol[idx] += levels[i].total;
			traded[idx] = true;
		}
		for (int i = 0; i < n; i++)
			rows.Add(new Row { price = Math.Round((lo + i * step) / tick) * tick, volume = vol[i] });
		return rows;
	}

	// Merge the existing sorted prefix with the extra's values (kind 0 total, 1 ask, 2 bid).
	internal static double[] mergeSorted(double[] a, int count, List<LevelVol> levels, int kind, int n)
	{
		var b = new double[levels.Count];
		for (int i = 0; i < b.Length; i++)
			b[i] = kind == 0 ? levels[i].total : (kind == 1 ? levels[i].ask : levels[i].bid);
		Array.Sort(b);
		var r = a.Length >= n ? a : new double[Math.Max(n, a.Length * 2)];
		int ia = count - 1, ib = b.Length - 1, w = n - 1;   // merge from the back so r may alias a
		while (ib >= 0)
			r[w--] = (ia >= 0 && a[ia] > b[ib]) ? a[ia--] : b[ib--];
		if (!object.ReferenceEquals(r, a))
			while (ia >= 0) r[w--] = a[ia--];
		return r;
	}

	internal static void stampLevelPercentiles(VpExtra extra, double[] streamTotal, double[] streamAsk, double[] streamBid, int count)
	{
		if (extra == null) return;
		extra.maxTotalPercentile = 0;
		extra.maxAskPercentile = 0;
		extra.maxBidPercentile = 0;
		for (int i = 0; i < extra.levels.Count; i++)
		{
			LevelVol lv = extra.levels[i];
			lv.totalPercentile = rankPercentile(streamTotal, count, lv.total);
			lv.askPercentile = rankPercentile(streamAsk, count, lv.ask);
			lv.bidPercentile = rankPercentile(streamBid, count, lv.bid);
			extra.levels[i] = lv;
			if (lv.totalPercentile > extra.maxTotalPercentile) extra.maxTotalPercentile = lv.totalPercentile;
			if (lv.askPercentile > extra.maxAskPercentile) extra.maxAskPercentile = lv.askPercentile;
			if (lv.bidPercentile > extra.maxBidPercentile) extra.maxBidPercentile = lv.bidPercentile;
		}
	}

	/// <summary>
	/// Empirical CDF: fraction of samples with value &lt;= x. 0..1.
	/// </summary>
	// count is the filled prefix. The buffer may be longer than this stamp, and the
	// spare tail must not enter the CDF.
	internal static double rankPercentile(double[] sortedAscending, int count, double x)
	{
		if (sortedAscending == null || count <= 0) return 0;
		int n = count;
		int lo = 0;
		int hi = n;
		while (lo < hi)
		{
			int mid = lo + ((hi - lo) / 2);
			if (sortedAscending[mid] <= x) lo = mid + 1;
			else hi = mid;
		}
		return lo / (double)n;
	}

	internal static Node makeNode(List<Row> rows, double[] vols, int a, int b, int peak, double pocVol, bool isPoc)
	{
		Node result = new Node
		{
			low = rows[a].price,
			high = rows[b].price,
			peak = rows[peak].price,
			peakVol = vols[peak],
			strength = pocVol > 0 ? vols[peak] / pocVol : 0,
			isPoc = isPoc
		};
		return result;
	}

	internal static bool isLocalMin(double[] v, int i)
	{
		bool leLeft = i == 0 || v[i] <= v[i - 1];
		bool leRight = i == v.Length - 1 || v[i] <= v[i + 1];
		return leLeft && leRight;
	}

	internal static bool hasHvnAdjacent(List<Node> hvns, double edge, bool lookingDown, double maxGap)
	{
		for (int i = 0; i < hvns.Count; i++)
		{
			if (lookingDown)
			{
				double gap = edge - hvns[i].high;
				if (gap >= -PRICE_EPSILON && gap <= maxGap + PRICE_EPSILON) return true;
			}
			else
			{
				double gap = hvns[i].low - edge;
				if (gap >= -PRICE_EPSILON && gap <= maxGap + PRICE_EPSILON) return true;
			}
		}
		return false;
	}

	internal static double[] smooth(double[] src, int window)
	{
		if (window < 3) return src;
		if ((window & 1) == 0) window++;          // force odd window
		int half = window / 2;
		double[] dst = new double[src.Length];
		for (int i = 0; i < src.Length; i++)
		{
			int a = Math.Max(0, i - half);
			int b = Math.Min(src.Length - 1, i + half);
			double sum = 0;
			for (int k = a; k <= b; k++) sum += src[k];
			dst[i] = sum / (b - a + 1);
		}
		return dst;
	}

	// Linear interpolation on a sorted copy.
	internal static double percentile(double[] src, double pct)
	{
		if (src == null || src.Length == 0) return 0;
		double[] s = (double[])src.Clone();
		Array.Sort(s);
		if (s.Length == 1) return s[0];
		double pos = pct * (s.Length - 1);
		int lo = (int)Math.Floor(pos);
		int hi = (int)Math.Ceiling(pos);
		if (lo < 0) lo = 0;
		if (hi >= s.Length) hi = s.Length - 1;
		if (lo == hi) return s[lo];
		double w = pos - lo;
		return s[lo] * (1.0 - w) + s[hi] * w;
	}

	/// <summary>
	/// kind: 0 POC, 1 VAH, 2 VAL. chartBar is 1-based; 
	/// POCForBar/HighVAForBar/LowVAForBar take the absolute 0-based bar (same as ItemForBar).
	/// Never probe chartBar itself: that is the next bar.
	/// </summary>
	internal static double priceForBar(IProfile vp, int chartBar, int kind)
	{
		return priceForBarNative(vp, toNativeItemBar(chartBar), kind);
	}

	internal static double priceForBarNative(IProfile vp, int bar, int kind)
	{
		if (vp == null || bar < 0) return 0;
		try
		{
			Price p = null;
			if (kind == 0) p = vp.POCForBar(bar);
			else if (kind == 1) p = vp.HighVAForBar(bar);
			else p = vp.LowVAForBar(bar);
			return p != null ? p.Dbl : 0;
		}
		catch { return 0; }
	}

	internal static double firstPositive(params double[] xs)
	{
		if (xs == null) return 0;
		for (int i = 0; i < xs.Length; i++)
			if (xs[i] > 0) return xs[i];
		return 0;
	}

	internal static double volumeAtPrice(VpExtra extra, double price)
	{
		if (extra == null || extra.levels == null || extra.levels.Count == 0 || price <= 0)
			return 0;
		double bestVol = 0;
		double bestDist = double.MaxValue;
		for (int i = 0; i < extra.levels.Count; i++)
		{
			LevelVol lv = extra.levels[i];
			double d = Math.Abs(lv.price - price);
			if (d < bestDist)
			{
				bestDist = d;
				bestVol = lv.total;
			}
		}
		return bestVol;
	}

	internal static double levelPrice(ILevel lvl)
	{
		try
		{
			if (lvl != null && lvl.Price != null) return lvl.Price.Dbl;
		}
		catch { }
		return 0;
	}

	internal static double vaExtreme(IProfile vp, bool high)
	{
		if (vp == null || vp.VA == null) return 0;
		double ext = 0;
		bool any = false;
		try
		{
			foreach (ILevel lvl in vp.VA)
			{
				double px = levelPrice(lvl);
				if (px <= 0) continue;
				if (!any) { ext = px; any = true; }
				else ext = high ? Math.Max(ext, px) : Math.Min(ext, px);
			}
		}
		catch { }
		return ext;
	}
	}
}
