using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Spec VPF-5 (analyst-base-class-spec.md): order-flow helpers on an APPROXIMATED aggressor side.
	///
	/// MultiCharts .NET does not expose the trade aggressor side to study code (confirmed by the user, also for feeds such as
	/// dxFeed). Every buy/sell split here is therefore an APPROXIMATION, never the true aggressor:
	/// * Tick rule (Lee-Ready style, price only): a trade above the previous trade price is a buy, below is a sell, an
	///   unchanged price (zero tick) inherits the previous classification. The very first trade of a run has no previous
	///   price and stays unclassified. Misclassifies trades inside the spread, iceberg / hidden fills and bundled ticks.
	/// * Fallback per bar: Bars.UpTicks / Bars.DownTicks (MultiCharts' own up/down tick split; depending on the chart's
	///   "Build volume on" setting these are tick counts or volumes). Bar delta only; no per-price footprint.
	/// Treat delta, imbalances, stacked imbalances, unfinished auctions and absorption as hypotheses to be tested, not as
	/// order-book facts. Helper-file convention per the CS-0 probe (plain class in PowerLanguage.Function, class name =
	/// file name). MultiCharts-free: tests/OrderFlowTests extracts the &lt;order-flow&gt; block.
	/// </summary>
	public sealed class _OrderFlow
	{
		private _OrderFlow() { }

		// <order-flow>
		/// <summary>VPF-5: approximated aggressor side. Unknown = not classifiable (first trade of a run).</summary>
		public enum Side { Unknown = 0, Buy = 1, Sell = -1 }

		/// <summary>VPF-5: where the approximation of a bar came from.</summary>
		public enum Source { None = 0, TickRule = 1, UpDownTicks = 2 }

		/// <summary>
		/// Tick rule classifier: uptick = buy, downtick = sell, zero tick inherits the previous classification.
		/// APPROXIMATION of the aggressor side (see _OrderFlow).
		/// </summary>
		public sealed class TickRule
		{
			private double m_lastPrice = double.NaN;
			private Side m_lastSide = Side.Unknown;
			private readonly double m_eps;

			/// <param name="tick">Instrument tick size; price changes smaller than tick/2 count as a zero tick.</param>
			public TickRule(double tick) { m_eps = tick > 0 ? tick * 0.5 : 1e-12; }

			public Side LastSide { get { return m_lastSide; } }

			public Side Classify(double price)
			{
				Side s;
				if (double.IsNaN(m_lastPrice)) s = Side.Unknown;
				else if (price > m_lastPrice + m_eps) s = Side.Buy;
				else if (price < m_lastPrice - m_eps) s = Side.Sell;
				else s = m_lastSide;   // zero tick inherits
				m_lastPrice = price;
				m_lastSide = s;
				return s;
			}

			public void Reset() { m_lastPrice = double.NaN; m_lastSide = Side.Unknown; }
		}

		/// <summary>
		/// Approximated footprint of one bar: buy / sell volume per price row (1 tick). "Ask" = approximated buy volume,
		/// "Bid" = approximated sell volume, the usual footprint naming.
		/// </summary>
		public sealed class Footprint
		{
			public readonly double Tick;
			public Source Source;
			public int Bar;                 // 1-based bar number (caller's)
			public double Open, High, Low, Close;
			public double UnclassifiedVolume;
			public bool Partial;            // the first ticks of the bar were not seen (e.g. calc started mid-bar)
			private readonly SortedDictionary<long, double[]> m_rows = new SortedDictionary<long, double[]>();
			private double m_buy, m_sell;

			public Footprint(double tick) { Tick = tick > 0 ? tick : 0.01; Open = High = Low = Close = double.NaN; }

			public long Row(double price) { return (long)Math.Round(price / Tick); }
			public double PriceOf(long row) { return row * Tick; }
			public bool HasRows { get { return m_rows.Count > 0; } }
			public double BuyVolume { get { return m_buy; } }
			public double SellVolume { get { return m_sell; } }
			public double Delta { get { return m_buy - m_sell; } }
			public double TotalVolume { get { return m_buy + m_sell + UnclassifiedVolume; } }

			/// <summary>Add one trade (tick rule path).</summary>
			public void Add(double price, double volume, Side side)
			{
				if (double.IsNaN(Open)) Open = price;
				if (double.IsNaN(High) || price > High) High = price;
				if (double.IsNaN(Low) || price < Low) Low = price;
				Close = price;
				if (volume <= 0) return;
				if (side == Side.Unknown) { UnclassifiedVolume += volume; return; }
				long r = Row(price);
				double[] v;
				if (!m_rows.TryGetValue(r, out v)) { v = new double[2]; m_rows[r] = v; }
				if (side == Side.Buy) { v[0] += volume; m_buy += volume; } else { v[1] += volume; m_sell += volume; }
			}

			/// <summary>Bar-level fallback: UpTicks / DownTicks, no per-price rows.</summary>
			public void SetBarTotals(double up, double down)
			{
				m_rows.Clear();
				m_buy = Math.Max(0, up);
				m_sell = Math.Max(0, down);
				UnclassifiedVolume = 0;
				Source = Source.UpDownTicks;
			}

			public double Ask(double price) { double[] v; return m_rows.TryGetValue(Row(price), out v) ? v[0] : 0; }
			public double Bid(double price) { double[] v; return m_rows.TryGetValue(Row(price), out v) ? v[1] : 0; }

			/// <summary>Rows low → high: (price, ask = approx. buy, bid = approx. sell).</summary>
			public List<double[]> Rows()
			{
				var list = new List<double[]>(m_rows.Count);
				foreach (var kv in m_rows) list.Add(new[] { PriceOf(kv.Key), kv.Value[0], kv.Value[1] });
				return list;
			}
		}

		/// <summary>
		/// Diagonal imbalances. Buy at p: Ask(p) >= ratio x Bid(p - tick); sell at p: Bid(p) >= ratio x Ask(p + tick).
		/// A zero opposite side counts when the row's own volume >= minVolume (an "infinite" imbalance). Rows whose own
		/// volume is below minVolume never count. Returns the imbalance prices low → high; empty without footprint rows.
		/// </summary>
		public static List<double> Imbalances(Footprint fp, Side side, double ratio, double minVolume)
		{
			var res = new List<double>();
			if (fp == null || !fp.HasRows || side == Side.Unknown || ratio <= 0) return res;
			long lo = fp.Row(fp.Low), hi = fp.Row(fp.High);
			for (long r = lo; r <= hi; r++)
			{
				double p = fp.PriceOf(r);
				double own = side == Side.Buy ? fp.Ask(p) : fp.Bid(p);
				double opp = side == Side.Buy ? fp.Bid(fp.PriceOf(r - 1)) : fp.Ask(fp.PriceOf(r + 1));
				if (own <= 0 || own < minVolume) continue;
				if (own >= ratio * opp) res.Add(p);
			}
			return res;
		}

		/// <summary>
		/// Runs of at least minCount imbalances on consecutive rows. Each run is {lowPrice, highPrice, count}, low → high.
		/// </summary>
		public static List<double[]> StackedImbalances(Footprint fp, Side side, double ratio, double minVolume, int minCount = 3)
		{
			var res = new List<double[]>();
			List<double> imb = Imbalances(fp, side, ratio, minVolume);
			int i = 0;
			while (i < imb.Count)
			{
				int j = i;
				while (j + 1 < imb.Count && fp.Row(imb[j + 1]) == fp.Row(imb[j]) + 1) j++;
				int n = j - i + 1;
				if (n >= Math.Max(1, minCount)) res.Add(new[] { imb[i], imb[j], n });
				i = j + 1;
			}
			return res;
		}

		/// <summary>
		/// Unfinished auction at the bar's high (high=true) or low: both approximated buyers and sellers traded at the
		/// extreme (a finished auction shows zero on the opposite side there). False without footprint rows.
		/// </summary>
		public static bool UnfinishedAuction(Footprint fp, bool high, double minVolume = 0)
		{
			if (fp == null || !fp.HasRows) return false;
			double p = high ? fp.High : fp.Low;
			double a = fp.Ask(p), b = fp.Bid(p);
			return a > 0 && b > 0 && a + b >= minVolume;
		}

		/// <summary>
		/// Absorption HYPOTHESIS (thresholds are parameters, not facts): exceptional bar volume (volumeRank >= minRank, the
		/// caller's percentile rank of this bar's volume vs prior bars), delta against the move and price progress
		/// &lt;= maxProgressTicks in the direction of the aggressor, with the bar's max-volume row within extremeTicks of the
		/// extreme the aggressor pushed into. Returns Buy for absorbed selling at the low (bullish), Sell for absorbed buying at
		/// the high (bearish), Unknown otherwise. prevClose = close of the previous bar.
		/// </summary>
		public static Side Absorption(Footprint fp, double prevClose, double volumeRank, double minRank, int maxProgressTicks, int extremeTicks)
		{
			if (fp == null || !fp.HasRows || volumeRank < minRank || double.IsNaN(prevClose)) return Side.Unknown;
			double poc = double.NaN, pocVol = -1;
			foreach (var r in fp.Rows()) { double v = r[1] + r[2]; if (v > pocVol) { pocVol = v; poc = r[0]; } }
			double tol = fp.Tick * 0.5;
			if (fp.Delta < 0 && prevClose - fp.Close <= maxProgressTicks * fp.Tick + tol && poc - fp.Low <= extremeTicks * fp.Tick + tol)
				return Side.Buy;    // sellers hit, price did not follow, volume parked at the low
			if (fp.Delta > 0 && fp.Close - prevClose <= maxProgressTicks * fp.Tick + tol && fp.High - poc <= extremeTicks * fp.Tick + tol)
				return Side.Sell;   // buyers lifted, price did not follow, volume parked at the high
			return Side.Unknown;
		}

		/// <summary>Percentile rank of x among values (fraction of values &lt;= x), NaN when values is empty.</summary>
		public static double PercentRank(IList<double> values, double x)
		{
			if (values == null || values.Count == 0) return double.NaN;
			int le = 0;
			for (int i = 0; i < values.Count; i++) if (values[i] <= x) le++;
			return (double)le / values.Count;
		}

		/// <summary>
		/// Delta z-score of the same clock minute over prior sessions only: history holds one value per prior session (the
		/// delta at that minute), current = today's delta at that minute. NaN with fewer than minSessions values or zero
		/// variance.
		/// </summary>
		public static double DeltaZ(IList<double> priorSessions, double current, int minSessions = 2)
		{
			if (priorSessions == null || priorSessions.Count < Math.Max(2, minSessions)) return double.NaN;
			double m = 0;
			foreach (double v in priorSessions) m += v;
			m /= priorSessions.Count;
			double ss = 0;
			foreach (double v in priorSessions) ss += (v - m) * (v - m);
			double sd = Math.Sqrt(ss / (priorSessions.Count - 1));
			return sd > 0 ? (current - m) / sd : double.NaN;
		}

		/// <summary>
		/// Per-bar delta history with an anchored cumulative delta and the same-minute delta history per session (DeltaZ).
		/// Bars are added once, when they close. Key = session key (e.g. Globex date) and minute of day (caller's clock).
		/// </summary>
		public sealed class DeltaHistory
		{
			private readonly int m_capacity;
			private readonly List<int> m_bars = new List<int>();
			private readonly List<double> m_deltas = new List<double>();
			private readonly Dictionary<int, List<KeyValuePair<DateTime, double>>> m_byMinute = new Dictionary<int, List<KeyValuePair<DateTime, double>>>();
			private int m_anchorBar = -1;
			private double m_cvd;
			public int MaxSessionsPerMinute = 60;

			public DeltaHistory(int capacity = 5000) { m_capacity = Math.Max(10, capacity); }

			public int Count { get { return m_bars.Count; } }
			public double AnchoredCvd { get { return m_anchorBar < 0 ? double.NaN : m_cvd; } }
			public int AnchorBar { get { return m_anchorBar; } }

			/// <summary>Start a new cumulative delta at this bar (the bar's own delta is included when it is added).</summary>
			public void Anchor(int bar) { m_anchorBar = bar; m_cvd = 0; }

			public void Add(int bar, double delta, DateTime sessionKey, int minuteOfDay)
			{
				if (m_bars.Count > 0 && bar <= m_bars[m_bars.Count - 1]) return;   // once per bar
				m_bars.Add(bar); m_deltas.Add(delta);
				if (m_bars.Count > m_capacity) { m_bars.RemoveAt(0); m_deltas.RemoveAt(0); }
				if (m_anchorBar >= 0 && bar >= m_anchorBar) m_cvd += delta;
				List<KeyValuePair<DateTime, double>> l;
				if (!m_byMinute.TryGetValue(minuteOfDay, out l)) { l = new List<KeyValuePair<DateTime, double>>(); m_byMinute[minuteOfDay] = l; }
				l.Add(new KeyValuePair<DateTime, double>(sessionKey.Date, delta));
				if (l.Count > MaxSessionsPerMinute + 1) l.RemoveAt(0);
			}

			/// <summary>Delta of a bar (1-based number), NaN if not stored.</summary>
			public double DeltaOf(int bar)
			{
				int i = m_bars.BinarySearch(bar);
				return i >= 0 ? m_deltas[i] : double.NaN;
			}

			/// <summary>Sum of deltas of stored bars in [fromBar, toBar].</summary>
			public double Cvd(int fromBar, int toBar)
			{
				double s = 0;
				for (int i = 0; i < m_bars.Count; i++) if (m_bars[i] >= fromBar && m_bars[i] <= toBar) s += m_deltas[i];
				return s;
			}

			/// <summary>
			/// z of `current` against the same minute of the last `sessions` PRIOR sessions (sessionKey earlier than
			/// `currentSession`; today's own value never enters its baseline).
			/// </summary>
			public double SameMinuteZ(int minuteOfDay, DateTime currentSession, double current, int sessions = 20)
			{
				List<KeyValuePair<DateTime, double>> l;
				if (!m_byMinute.TryGetValue(minuteOfDay, out l)) return double.NaN;
				var prior = new List<double>();
				for (int i = l.Count - 1; i >= 0 && prior.Count < sessions; i--)
					if (l[i].Key < currentSession.Date) prior.Add(l[i].Value);
				return DeltaZ(prior, current, 2);
			}

			/// <summary>Volumes are not stored here; this returns the last n bar deltas (oldest first).</summary>
			public List<double> LastDeltas(int n)
			{
				int from = Math.Max(0, m_deltas.Count - n);
				return m_deltas.GetRange(from, m_deltas.Count - from);
			}
		}
		// </order-flow>
	}
}
