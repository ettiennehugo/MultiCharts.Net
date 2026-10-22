using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	// Spec VPB-9 (analyst-base-class-spec.md): ProfileMath (histogram math, VPB-1 Accumulator = the developing profile) moved verbatim out of _VolumeProfileBase.Strategy.CS into its own
	// helper file, per the CS-0 probe convention (_SignalJournal.Function.CS: plain class in PowerLanguage.Function, class name = file name).

	// <profile-math>
	/// <summary>
	/// VPB-1 / VPB-2 / VPB-6 / VPB-7: MultiCharts-free histogram math. Pure functions on a dense, ascending 1-tick grid
	/// (prices[i] = lowest + i x tick, vols[i] >= 0). tests/ProfileTests extracts this block and tests it off MultiCharts.
	/// </summary>
	public class _ProfileMath
	{
		protected _ProfileMath() { }   // static members only; derivable so _VolumeProfileBase.ProfileMath can alias it

		public enum ValueRel { Unknown = 0, Higher, Lower, OverlappingHigher, OverlappingLower, Inside, Outside }
		public enum Shape { Unknown = 0, D, P, b, Thin, Double }

		// VPB-6 ProfileShape thresholds (documented hypothesis, not tuned).
		public const double SHAPE_SKEW_SHARE = 0.50;      // P / b: >= 50% of the volume in the upper / lower third and the POC in that third
		public const double SHAPE_THIN_PEAK_TO_MEAN = 1.5; // Thin/Trend: flat histogram, POC volume <= 1.5 x mean traded-row volume
		public const double SHAPE_DOUBLE_PEAK = 0.60;     // Double: second peak >= 60% of the POC volume ...
		public const double SHAPE_DOUBLE_VALLEY = 0.50;   // ... with a valley <= 50% of the smaller peak between them ...
		public const double SHAPE_DOUBLE_SEPARATION = 0.25; // ... and the peaks >= 25% of the range apart

		public static long Key(double price, double tick) { return (long)Math.Round(price / tick); }

		/// <summary>Sparse (price, volume) pairs onto a dense ascending 1-tick grid. Volumes on the same tick add up.</summary>
		public static bool ToDense(IList<double> prices, IList<double> vols, double tick, out double[] densePrices, out double[] denseVols)
		{
			densePrices = new double[0]; denseVols = new double[0];
			if (prices == null || vols == null || tick <= 0 || prices.Count == 0 || prices.Count != vols.Count) return false;
			long lo = long.MaxValue, hi = long.MinValue;
			for (int i = 0; i < prices.Count; i++)
			{
				if (vols[i] <= 0) continue;
				long k = Key(prices[i], tick);
				if (k < lo) lo = k;
				if (k > hi) hi = k;
			}
			if (lo > hi || hi - lo + 1 > 200000) return false;
			int n = (int)(hi - lo + 1);
			densePrices = new double[n]; denseVols = new double[n];
			for (int i = 0; i < n; i++) densePrices[i] = (lo + i) * tick;
			for (int i = 0; i < prices.Count; i++)
				if (vols[i] > 0) denseVols[(int)(Key(prices[i], tick) - lo)] += vols[i];
			return true;
		}

		/// <summary>Raw max-volume row. Ties: the row nearest the volume-weighted mean, then the lower row.</summary>
		public static int PocIndex(double[] vols)
		{
			if (vols == null || vols.Length == 0) return -1;
			double max = 0, sum = 0, wsum = 0;
			for (int i = 0; i < vols.Length; i++) { if (vols[i] > max) max = vols[i]; sum += vols[i]; wsum += i * vols[i]; }
			if (max <= 0) return -1;
			double mean = wsum / sum;
			int best = -1;
			for (int i = 0; i < vols.Length; i++)
				if (vols[i] == max && (best < 0 || Math.Abs(i - mean) < Math.Abs(best - mean))) best = i;
			return best;
		}

		/// <summary>
		/// VPB-2 VolumeExpansion: start at the POC row; compare the next two rows above with the next two below and add the
		/// larger pair; a tie adds both pairs. A side that has run out of rows offers fewer (or zero) rows. Stops once the
		/// included volume reaches percent x total. Returns false for an empty histogram.
		/// </summary>
		public static bool ValueAreaExpansion(double[] vols, double percent, out int lo, out int hi, out int poc)
		{
			lo = hi = poc = PocIndex(vols);
			if (poc < 0) return false;
			double total = 0;
			for (int i = 0; i < vols.Length; i++) total += vols[i];
			double target = total * percent, acc = vols[poc];
			int n = vols.Length;
			while (acc < target && (lo > 0 || hi < n - 1))
			{
				int upRows = Math.Min(2, n - 1 - hi), downRows = Math.Min(2, lo);
				double up = 0, down = 0;
				for (int k = 1; k <= upRows; k++) up += vols[hi + k];
				for (int k = 1; k <= downRows; k++) down += vols[lo - k];
				if (upRows > 0 && (downRows == 0 || up > down)) { hi += upRows; acc += up; }
				else if (downRows > 0 && (upRows == 0 || down > up)) { lo -= downRows; acc += down; }
				else { hi += upRows; lo -= downRows; acc += up + down; }
			}
			return true;
		}

		/// <summary>VPB-2 Sigma: volume-weighted mean ± k·σ of the histogram, clamped to the traded range.</summary>
		public static bool ValueAreaSigma(double[] prices, double[] vols, double k, out double vah, out double val)
		{
			vah = val = 0;
			if (prices == null || vols == null || prices.Length == 0 || prices.Length != vols.Length) return false;
			double w = 0, m = 0;
			for (int i = 0; i < vols.Length; i++) { w += vols[i]; m += vols[i] * prices[i]; }
			if (w <= 0) return false;
			m /= w;
			double v = 0;
			for (int i = 0; i < vols.Length; i++) v += vols[i] * (prices[i] - m) * (prices[i] - m);
			double sd = Math.Sqrt(v / w);
			vah = Math.Min(prices[prices.Length - 1], m + k * sd);
			val = Math.Max(prices[0], m - k * sd);
			return true;
		}

		/// <summary>VPB-6 Dalton value-area relationship of the current profile to the previous one.</summary>
		public static ValueRel Relationship(double prevVah, double prevVal, double curVah, double curVal)
		{
			if (prevVah <= 0 || prevVal <= 0 || curVah <= 0 || curVal <= 0 || prevVah < prevVal || curVah < curVal) return ValueRel.Unknown;
			if (curVal > prevVah) return ValueRel.Higher;
			if (curVah < prevVal) return ValueRel.Lower;
			if (curVah <= prevVah && curVal >= prevVal) return ValueRel.Inside;
			if (curVah > prevVah && curVal < prevVal) return ValueRel.Outside;
			return curVah > prevVah ? ValueRel.OverlappingHigher : ValueRel.OverlappingLower;
		}

		/// <summary>
		/// VPB-6 poor high (top = true) / poor low: the extreme row and at least minRows rows from the extreme each hold
		/// >= fraction x the mean traded-row volume (no tapering tail).
		/// </summary>
		public static bool IsPoorExtreme(double[] vols, bool top, double fraction, int minRows)
		{
			if (vols == null || vols.Length < minRows || minRows < 1) return false;
			double sum = 0; int traded = 0;
			for (int i = 0; i < vols.Length; i++) if (vols[i] > 0) { sum += vols[i]; traded++; }
			if (traded == 0) return false;
			double floor = fraction * sum / traded;
			int count = 0;
			for (int j = 0; j < vols.Length; j++)
			{
				double vv = vols[top ? vols.Length - 1 - j : j];
				if (vv >= floor && vv > 0) count++; else break;
			}
			return count >= minRows;
		}

		/// <summary>
		/// VPB-6 single prints: runs of >= minRows rows with volume <= the pct-percentile of traded rows, between a value-area
		/// edge and the profile extreme (outside [vaLo, vaHi]). Returns [startIndex, endIndex] pairs, low to high.
		/// </summary>
		public static List<int[]> SinglePrints(double[] vols, int vaLo, int vaHi, double pct, int minRows)
		{
			var res = new List<int[]>();
			if (vols == null || vols.Length == 0 || vaLo < 0 || vaHi >= vols.Length || vaLo > vaHi) return res;
			var traded = new List<double>();
			for (int i = 0; i < vols.Length; i++) if (vols[i] > 0) traded.Add(vols[i]);
			if (traded.Count == 0) return res;
			traded.Sort();
			double cut = traded[Math.Max(0, Math.Min(traded.Count - 1, (int)Math.Floor(pct * (traded.Count - 1))))];
			int a = -1;
			for (int i = 0; i <= vols.Length; i++)
			{
				bool inZone = i < vols.Length && (i < vaLo || i > vaHi) && vols[i] <= cut;
				if (inZone && a < 0) a = i;
				if (!inZone && a >= 0) { if (i - a >= minRows) res.Add(new[] { a, i - 1 }); a = -1; }
			}
			return res;
		}

		/// <summary>VPB-6 profile shape with the SHAPE_* thresholds above.</summary>
		public static Shape ProfileShape(double[] vols, double vaPercent)
		{
			int n = vols == null ? 0 : vols.Length;
			if (n < 3) return Shape.Unknown;
			int lo, hi, poc;
			if (!ValueAreaExpansion(vols, vaPercent, out lo, out hi, out poc)) return Shape.Unknown;
			double total = 0, upper = 0, lower = 0;
			int third = n / 3;
			for (int i = 0; i < n; i++) { total += vols[i]; if (i >= n - third) upper += vols[i]; if (i < third) lower += vols[i]; }
			if (total <= 0) return Shape.Unknown;
			// Double: a second peak far enough from the POC with a deep enough valley between them.
			for (int i = 1; i < n - 1; i++)
			{
				if (i == poc || vols[i] < vols[i - 1] || vols[i] < vols[i + 1]) continue;
				if (vols[i] < SHAPE_DOUBLE_PEAK * vols[poc] || Math.Abs(i - poc) < SHAPE_DOUBLE_SEPARATION * (n - 1)) continue;
				double valley = double.MaxValue;
				for (int j = Math.Min(i, poc) + 1; j < Math.Max(i, poc); j++) valley = Math.Min(valley, vols[j]);
				if (valley <= SHAPE_DOUBLE_VALLEY * Math.Min(vols[i], vols[poc])) return Shape.Double;
			}
			int tradedRows = 0;
			for (int i = 0; i < n; i++) if (vols[i] > 0) tradedRows++;
			if (vols[poc] <= SHAPE_THIN_PEAK_TO_MEAN * total / tradedRows) return Shape.Thin;
			if (third > 0 && upper >= SHAPE_SKEW_SHARE * total && poc >= n - third) return Shape.P;
			if (third > 0 && lower >= SHAPE_SKEW_SHARE * total && poc < third) return Shape.b;
			return Shape.D;
		}

		public enum MigrationState { Unknown = 0, Down, Neutral, Up }

		/// <summary>VPB-6 (Rev 2.4) s(a, b): 0 when |a - b| &lt; d, else sign(a - b).</summary>
		public static int MigrationStep(double a, double b, double d)
		{
			double diff = a - b;
			if (Math.Abs(diff) < d) return 0;
			return Math.Sign(diff);
		}

		/// <summary>
		/// VPB-6 (Rev 2.4) value-migration score V = sum_{i=0}^{n-1} s(POC_i, POC_{i+1}) (+ s(devPoc, POC_0) when devPoc is
		/// given). pocsOldestToNewest: completed-session POCs, newest last; only the newest n+1 are used. Returns null
		/// (Unknown) with fewer than n+1 POCs, n &lt; 1, or a non-finite / negative d.
		/// </summary>
		public static int? MigrationScore(IList<double> pocsOldestToNewest, int n, double d, double? devPoc)
		{
			if (pocsOldestToNewest == null || n < 1 || pocsOldestToNewest.Count < n + 1 || double.IsNaN(d) || double.IsInfinity(d) || d < 0) return null;
			int last = pocsOldestToNewest.Count - 1;   // POC_0
			int v = 0;
			for (int i = 0; i < n; i++) v += MigrationStep(pocsOldestToNewest[last - i], pocsOldestToNewest[last - i - 1], d);
			if (devPoc.HasValue && devPoc.Value > 0) v += MigrationStep(devPoc.Value, pocsOldestToNewest[last], d);
			return v;
		}

		/// <summary>VPB-6 (Rev 2.4): Up when V >= upAt, Down when V &lt;= -downAt, Neutral otherwise, Unknown when V is null.</summary>
		public static MigrationState ClassifyMigration(int? v, int upAt = 2, int downAt = 2)
		{
			if (!v.HasValue) return MigrationState.Unknown;
			if (v.Value >= upAt) return MigrationState.Up;
			if (v.Value <= -downAt) return MigrationState.Down;
			return MigrationState.Neutral;
		}

		/// <summary>VPB-7: true when every consecutive pair of value areas overlaps by >= minOverlap of the narrower one.</summary>
		public static bool IsBalanced(IList<double> vahs, IList<double> vals, double minOverlap)
		{
			if (vahs == null || vals == null || vahs.Count < 2 || vahs.Count != vals.Count) return false;
			for (int i = 1; i < vahs.Count; i++)
			{
				double w = Math.Min(vahs[i] - vals[i], vahs[i - 1] - vals[i - 1]);
				double ov = Math.Min(vahs[i], vahs[i - 1]) - Math.Max(vals[i], vals[i - 1]);
				if (w < 0 || ov < 0) return false;
				if (w == 0 ? ov < 0 : ov / w < minOverlap) return false;
			}
			return true;
		}

		/// <summary>
		/// VPB-6: value-migration score V per final-synthesis section 4.0 (a hypothesis, K4 8.4 "last 3 windows").
		/// <paramref name="completedPocs"/> holds completed-session POCs oldest first; the last n differences are scored
		/// (so n + 1 POCs are used). Each term is sign(POC[i] - POC[i-1]), and a change smaller than
		/// <paramref name="minChange"/> (D = 0.10 x ATR20 daily) counts as 0. When <paramref name="developingPoc"/> > 0 the
		/// developing term sign(developing POC - newest completed POC) is added, so V is in [-(n+1), n+1] ([-4, +4] at n = 3).
		/// Returns false (V = 0) when fewer than n + 1 usable POCs exist. Up-migration V >= +2, down V <= -2, neutral |V| <= 1.
		/// </summary>
		public static bool ValueMigrationScore(IList<double> completedPocs, int n, double developingPoc, double minChange, out int score)
		{
			score = 0;
			if (completedPocs == null || n < 1 || completedPocs.Count < n + 1 || minChange < 0) return false;
			int last = completedPocs.Count - 1;
			for (int i = last - n; i <= last; i++) if (completedPocs[i] <= 0) return false;
			for (int i = last - n + 1; i <= last; i++) score += migrationTerm(completedPocs[i] - completedPocs[i - 1], minChange);
			if (developingPoc > 0) score += migrationTerm(developingPoc - completedPocs[last], minChange);
			return true;
		}

		private static int migrationTerm(double change, double minChange)
		{
			if (Math.Abs(change) < minChange || change == 0) return 0;
			return change > 0 ? 1 : -1;
		}

		/// <summary>
		/// VPB-1 bar-by-bar accumulator on a 1-tick grid. Deterministic: the same bars in the same order give the same
		/// histogram, so backtest and bar replay agree by construction.
		/// mode 1 = VolumeAtPrice (whole bar volume at the close: exact on a 1-tick source), 2 = BarUniform (volume spread
		/// evenly over the ticks low..high), 3 = BarTypical (all volume at (H+L+C)/3 rounded to the tick).
		/// </summary>
		public sealed class Accumulator
		{
			public readonly SortedDictionary<long, double> rows = new SortedDictionary<long, double>();
			public double tick;
			public int bars;
			public double open, high, low, close;
			public int firstBar = -1, lastBar = -1;
			public DateTime firstTime, lastTime;

			public Accumulator(double tick) { this.tick = tick > 0 ? tick : 0.01; }

			public void Reset() { rows.Clear(); bars = 0; open = high = low = close = 0; firstBar = lastBar = -1; firstTime = lastTime = DateTime.MinValue; }

			public void Add(int barIndex, DateTime time, double o, double h, double l, double c, double volume, int mode)
			{
				if (h < l || l <= 0) return;
				if (bars == 0) { open = o; high = h; low = l; firstBar = barIndex; firstTime = time; }
				if (h > high) high = h;
				if (l < low) low = l;
				close = c; lastBar = barIndex; lastTime = time; bars++;
				if (volume <= 0) return;
				if (mode == 2)
				{
					long a = Key(l, tick), b = Key(h, tick);
					double each = volume / (b - a + 1);
					for (long k = a; k <= b; k++) addRow(k, each);
				}
				else if (mode == 3) addRow(Key((h + l + c) / 3.0, tick), volume);
				else addRow(Key(c, tick), volume);
			}

			/// <summary>Phase 3 VP-1: add volume to one 1-tick row (ladder rows, merges). Does not touch bars/OHLC.</summary>
			public void AddRow(long k, double v) { addRow(k, v); }

			private void addRow(long k, double v)
			{
				double cur;
				rows.TryGetValue(k, out cur);
				rows[k] = cur + v;
			}

			/// <summary>Dense ascending grid from the lowest to the highest traded row.</summary>
			public void Dense(out double[] prices, out double[] vols)
			{
				prices = new double[0]; vols = new double[0];
				if (rows.Count == 0) return;
				long lo = long.MaxValue, hi = long.MinValue;
				foreach (long k in rows.Keys) { if (k < lo) lo = k; if (k > hi) hi = k; }
				int n = (int)Math.Min(200000, hi - lo + 1);
				prices = new double[n]; vols = new double[n];
				for (int i = 0; i < n; i++) prices[i] = (lo + i) * tick;
				foreach (var kv in rows) { long i = kv.Key - lo; if (i >= 0 && i < n) vols[i] += kv.Value; }
			}
		}
	}
	// </profile-math>
}
