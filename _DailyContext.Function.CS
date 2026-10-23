// ============================================================================================
// M-DAILY — daily / weekly context (spec /workspace/specs/missing-functionality-specs.md rev 4 §5, §0.3).
// Plain internal sealed class, MC-free (tests/DailyTests compile this file as is). _CustomSignal creates it in StartCalc only when
// NeedsDaily is set, and feeds completed daily bars: from a daily chart stream (DailyConfig.dataStream > 0, closed bars only)
// or, as fallback, Data1 aggregated per trading day.
// Unadjusted continuous futures: on the first day of a new contract the prior history is roll-gap adjusted - difference for
// level series (SMA, previous H/L/C, previous week H/L), ratio for return series (TSMOM, RSI); the splice day's TR is skipped and
// the Wilder ATR restarts its seed.
// ============================================================================================
using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	/// <summary>Spec S2: which close feeds HV / ROC1 / LBR. RthValue (default): the last RTH-value bar's close of the day (A2 l.926,
	/// "RTH closes"); Bar: the daily bar / aggregate close.</summary>
	public enum EDailyClose { Bar = 0, RthValue = 1 }

	/// <summary>Protected DailyConfig on _CustomSignal (no [Input]).</summary>
	public sealed class DailyConfig
	{
		public int dataStream = 0;      // 0 = aggregate Data1 per trading day (fallback); n = Data n daily bars (same unadjusted symbol)
		public int weeklyStream = 0;    // 0 = weekly high/low from the daily bars (Monday-start weeks); n = Data n weekly bars
		public int atrLength = 20;      // Wilder ATR (seed = SMA of the first atrLength TRs)
		public int smaLength = 200;
		public int rsiLength = 2;       // Wilder RSI
		public int[] tsmomLookbacks = { 252, 126, 63 };
		// ---- Spec S2 (all off by default) ----
		public int[] hvLengths = { };    // e.g. {6, 20, 100}: HV_n = sqrt(hvAnnualisation) x sample SD of the last n daily log returns
		public int hvAnnualisation = 252;
		public int hvPctLookback = 0;    // 0 = off; 504 for A2-70 (mid-rank of today's HV_n among the previous hvPctLookback days)
		public int lbrRsiLength = 0;     // 0 = off; 3 for A2-56: Wilder RSI of the ROC1 series (review Q8 default reading)
		public EDailyClose closeBasis = EDailyClose.RthValue;
		public bool HvOn { get { return hvLengths != null && hvLengths.Length > 0; } }

		public string Validate()
		{
			var e = new List<string>();
			if (dataStream < 0) e.Add($"dataStream={dataStream}");
			if (weeklyStream < 0) e.Add($"weeklyStream={weeklyStream}");
			if (atrLength < 1) e.Add($"atrLength={atrLength}");
			if (smaLength < 1) e.Add($"smaLength={smaLength}");
			if (rsiLength < 1) e.Add($"rsiLength={rsiLength}");
			if (tsmomLookbacks == null) e.Add("tsmomLookbacks=null"); else foreach (int n in tsmomLookbacks) if (n < 1) e.Add($"tsmomLookbacks contains {n}");
			if (hvLengths == null) e.Add("hvLengths=null"); else foreach (int n in hvLengths) if (n < 2) e.Add($"hvLengths contains {n}");
			if (hvAnnualisation < 1) e.Add($"hvAnnualisation={hvAnnualisation}");
			if (hvPctLookback < 0) e.Add($"hvPctLookback={hvPctLookback}");
			if (lbrRsiLength < 0) e.Add($"lbrRsiLength={lbrRsiLength}");
			return e.Count == 0 ? null : "DailyConfig invalid: " + string.Join(", ", e);
		}
	}

	internal sealed class _DailyContext
	{
		public DailyConfig config = new DailyConfig();
		public string label = "DAILY";
		public Action<string> journal;
		public Action<string> warnOnce;

		private struct Day { public DateTime date; public double o, h, l, c, rc, rthc, rrc; }   // rc = ratio-adjusted close (return series); rthc / rrc: S2 basis close, level (difference-adjusted) / ratio-adjusted
		private Day[] m_days; private int m_count, m_next;
		private double m_atr = double.NaN, m_trSeed; private int m_trSeen;
		private double m_rsiUp = double.NaN, m_rsiDn = double.NaN, m_rsiSeedUp, m_rsiSeedDn; private int m_rsiSeen;
		private double m_smaSum;                   // over the last smaLength level closes
		private DateTime m_lastDate = DateTime.MinValue;
		private long m_weekKey = long.MinValue; private double m_wkH = double.NaN, m_wkL = double.NaN, m_prevWkH = double.NaN, m_prevWkL = double.NaN;
		private bool m_extWeek; private bool m_warned;

		public bool Configured { get; private set; }

		// ---- Spec S2 state ----
		private double[] m_ret; private int m_retCount, m_retNext;                  // ring of daily log returns on rrc
		private HashSet<string> m_hvFirst = new HashSet<string>();               // review L3: first valid HV_n / HV_n pct date journalled once
		private Dictionary<int, double> m_hv = new Dictionary<int, double>();      // today's HV_n (memoised per day)
		private Dictionary<int, double[]> m_hvHist = new Dictionary<int, double[]>(); private int m_hvHistCount, m_hvHistNext;   // previous HV_n values
		private double m_roc1 = double.NaN, m_prevRoc1 = double.NaN, m_lbrUp = double.NaN, m_lbrDn = double.NaN, m_lbrSeedUp, m_lbrSeedDn; private int m_lbrSeen;
		private bool m_s2Warned;

		private bool s2Ready(string what, bool on)
		{
			if (on) return true;
			if (!m_s2Warned) { m_s2Warned = true; warnOnce?.Invoke($"{label}: {what} queried but not configured (DailyConfig.hvLengths / hvPctLookback / lbrRsiLength)"); }
			return false;
		}
		/// <summary>S2: HV_n (annualised sample SD of the last n daily log returns, ratio-adjusted basis close); NaN until n + 1 closes.</summary>
		public double Hv(int n) { double v; return s2Ready("Hv", config.HvOn) && m_hv.TryGetValue(n, out v) ? v : double.NaN; }
		/// <summary>S2: mid-rank percentile (0..100) of today's HV_n among the previous hvPctLookback daily HV_n values; NaN until full.</summary>
		public double HvPercentile(int n)
		{
			double[] h; double cur;
			if (!s2Ready("HvPercentile", config.HvOn && config.hvPctLookback > 0) || !m_hv.TryGetValue(n, out cur) || double.IsNaN(cur) || !m_hvHist.TryGetValue(n, out h)) return double.NaN;
			int L = config.hvPctLookback;
			if (m_hvHistCount < L) return double.NaN;
			int below = 0, eq = 0;
			for (int i = 0; i < L; i++) { double x = h[(m_hvHistNext - 1 - i + 2 * h.Length) % h.Length]; if (double.IsNaN(x)) return double.NaN; if (x < cur) below++; else if (x == cur) eq++; }
			return 100.0 * (below + 0.5 * eq) / L;
		}
		/// <summary>S2: ROC1 = basis close - previous basis close, in points, on the difference-adjusted (level) series.</summary>
		public double Roc1 { get { return s2Ready("Roc1", config.lbrRsiLength > 0 || config.HvOn) ? m_roc1 : double.NaN; } }
		/// <summary>S2 / A2-56: Wilder RSI(lbrRsiLength) of the ROC1 series; NaN until lbrRsiLength + 2 closes (5 for RSI 3).</summary>
		public double Lbr
		{
			get
			{
				if (!s2Ready("Lbr", config.lbrRsiLength > 0) || m_lbrSeen < config.lbrRsiLength || double.IsNaN(m_lbrUp)) return double.NaN;
				return m_lbrDn == 0 ? (m_lbrUp == 0 ? 50 : 100) : 100 - 100 / (1 + m_lbrUp / m_lbrDn);
			}
		}
		/// <summary>S2 / G-R: the previous completed day's basis close on the ratio-adjusted (roll-continuous) chain; never rebased at a roll,
		/// so ratios of stored values are roll-adjusted returns. NaN before the first day.</summary>
		public double RthCloseAdj { get { return m_count > 0 ? at(0).rrc : double.NaN; } }
		/// <summary>S2: the previous completed day's basis close (difference-adjusted).</summary>
		public double RthClosePrev { get { return s2Ready("RthClosePrev", config.lbrRsiLength > 0 || config.HvOn) && m_count > 0 ? at(0).rthc : double.NaN; } }

		// ---- outputs (NaN until enough completed days) ----
		/// <summary>Wilder ATR of daily TR; NaN until atrLength TRs since the start or the last roll.</summary>
		public double Atr20d { get { return m_trSeen >= config.atrLength ? m_atr : double.NaN; } }
		public double Sma200 { get { return m_count >= config.smaLength ? m_smaSum / config.smaLength : double.NaN; } }
		public double Rsi2 { get { if (m_rsiSeen < config.rsiLength || double.IsNaN(m_rsiUp)) return double.NaN; return m_rsiDn == 0 ? (m_rsiUp == 0 ? 50 : 100) : 100 - 100 / (1 + m_rsiUp / m_rsiDn); } }
		public double PrevHigh { get { return m_count > 0 ? at(0).h : double.NaN; } }
		public double PrevLow { get { return m_count > 0 ? at(0).l : double.NaN; } }
		public double PrevClose { get { return m_count > 0 ? at(0).c : double.NaN; } }
		public DateTime PrevDate { get { return m_count > 0 ? at(0).date : DateTime.MinValue; } }
		public double PrevRange { get { return m_count > 0 ? at(0).h - at(0).l : double.NaN; } }
		public double PrevWeekHigh { get { return m_prevWkH; } }
		public double PrevWeekLow { get { return m_prevWkL; } }
		/// <summary>Valid once the ATR is seeded and a previous day exists.</summary>
		public bool Valid { get { return !double.IsNaN(Atr20d) && m_count > 0; } }
		/// <summary>Time-series momentum: ratio-adjusted close / close n days earlier - 1; NaN without n+1 days.</summary>
		public double Tsmom(int n) { return n >= 1 && n < m_count ? at(0).rc / at(n).rc - 1 : double.NaN; }
		/// <summary>(today's open - adjusted previous close) / previous close.</summary>
		public double GapPct(double todayOpen) { double pc = PrevClose; return pc > 0 && todayOpen > 0 ? (todayOpen - pc) / pc : double.NaN; }
		public int Days { get { return m_count; } }

		public string Configure()
		{
			string err = config.Validate();
			if (err != null) { Configured = false; return err; }
			int cap = config.smaLength + 1;
			foreach (int n in config.tsmomLookbacks) cap = Math.Max(cap, n + 1);
			m_days = new Day[cap];
			int retCap = 1;
			foreach (int n in config.hvLengths) retCap = Math.Max(retCap, n);
			m_ret = new double[Math.Max(retCap, config.hvPctLookback + 20)];
			m_hvHist = new Dictionary<int, double[]>();
			if (config.hvPctLookback > 0) foreach (int n in config.hvLengths) m_hvHist[n] = new double[config.hvPctLookback];
			Configured = true; Reset();
			return null;
		}

		public void Reset()
		{
			if (!Configured) return;
			m_count = m_next = 0; m_atr = double.NaN; m_trSeed = 0; m_trSeen = 0;
			m_rsiUp = m_rsiDn = double.NaN; m_rsiSeedUp = m_rsiSeedDn = 0; m_rsiSeen = 0; m_smaSum = 0;
			m_retCount = m_retNext = 0; m_hv.Clear(); m_hvHistCount = m_hvHistNext = 0; m_s2Warned = false; m_hvFirst.Clear();
			m_roc1 = m_prevRoc1 = m_lbrUp = m_lbrDn = double.NaN; m_lbrSeedUp = m_lbrSeedDn = 0; m_lbrSeen = 0;
			m_skipTr = false; m_lastDate = DateTime.MinValue; m_weekKey = long.MinValue; m_wkH = m_wkL = m_prevWkH = m_prevWkL = double.NaN; m_extWeek = false; m_warned = false;
		}

		private Day at(int ago) { return m_days[(m_next - 1 - ago + 2 * m_days.Length) % m_days.Length]; }
		private void set(int ago, Day d) { m_days[(m_next - 1 - ago + 2 * m_days.Length) % m_days.Length] = d; }

		/// <summary>Weekly stream source (DailyConfig.weeklyStream > 0): last completed week's high/low (already in current-contract space).</summary>
		public void SetPrevWeek(double high, double low) { m_extWeek = true; m_prevWkH = high; m_prevWkL = low; }

		/// <summary>
		/// One COMPLETED daily bar, in date order. firstDayOfNewContract: this bar is the first full day of the new contract (§0.3);
		/// rollOffset: new-minus-old contract price difference at the splice, NaN = estimate it as open - previous close.
		/// </summary>
		public bool AddDay(DateTime date, double o, double h, double l, double c, bool firstDayOfNewContract = false, double rollOffset = double.NaN, double rthClose = double.NaN)
		{
			DateTime prevDate = m_lastDate;
			if (!Configured) return false;
			date = date.Date;
			if (date <= m_lastDate) { if (date < m_lastDate && !m_warned) { m_warned = true; warnOnce?.Invoke($"{label}: out-of-order day {date:yyyy-MM-dd} < {m_lastDate:yyyy-MM-dd} ignored"); } return false; }
			m_lastDate = date;
			if (firstDayOfNewContract && m_count > 0) applyRoll(double.IsNaN(rollOffset) ? o - at(0).c : rollOffset, date);
			bool splice = m_skipTr; m_skipTr = false;

			// ATR (Wilder): TR needs the previous close; the splice day's TR is skipped and the seed restarts
			if (m_count > 0 && !splice)
			{
				double pc = at(0).c, tr = Math.Max(h, pc) - Math.Min(l, pc);
				m_trSeen++;
				if (m_trSeen <= config.atrLength) { m_trSeed += tr; if (m_trSeen == config.atrLength) m_atr = m_trSeed / config.atrLength; }
				else m_atr = (m_atr * (config.atrLength - 1) + tr) / config.atrLength;
			}

			// RSI (Wilder) on ratio-adjusted closes
			double rc = m_count > 0 ? at(0).rc * (c / at(0).c) : c;
			if (m_count > 0)
			{
				double ch = rc - at(0).rc, up = ch > 0 ? ch : 0, dn = ch < 0 ? -ch : 0;
				m_rsiSeen++;
				if (m_rsiSeen <= config.rsiLength) { m_rsiSeedUp += up; m_rsiSeedDn += dn; if (m_rsiSeen == config.rsiLength) { m_rsiUp = m_rsiSeedUp / config.rsiLength; m_rsiDn = m_rsiSeedDn / config.rsiLength; } }
				else { m_rsiUp = (m_rsiUp * (config.rsiLength - 1) + up) / config.rsiLength; m_rsiDn = (m_rsiDn * (config.rsiLength - 1) + dn) / config.rsiLength; }
			}

			// week aggregation (Monday-start on the session date)
			if (!m_extWeek)
			{
				long wk = _AvwapCalendar.PeriodKey(EAvwapPeriod.Week, date);
				if (wk != m_weekKey)
				{
					if (m_weekKey != long.MinValue) { m_prevWkH = m_wkH; m_prevWkL = m_wkL; }
					m_weekKey = wk; m_wkH = h; m_wkL = l;
				}
				else { m_wkH = Math.Max(m_wkH, h); m_wkL = Math.Min(m_wkL, l); }
			}

			// S2 basis close: RTH close when supplied (closeBasis RthValue), else the bar close
			double bc = config.closeBasis == EDailyClose.RthValue && !double.IsNaN(rthClose) ? rthClose : c;
			double rrc = m_count > 0 && at(0).rthc != 0 ? at(0).rrc * (bc / at(0).rthc) : bc;
			if (m_count > 0 && (config.HvOn || config.lbrRsiLength > 0)) stepS2(date, prevDate, bc - at(0).rthc, Math.Log(rrc / at(0).rrc));

			// SMA window over level closes
			if (m_count >= config.smaLength) m_smaSum -= at(config.smaLength - 1).c;
			m_days[m_next] = new Day { date = date, o = o, h = h, l = l, c = c, rc = rc, rthc = bc, rrc = rrc };
			m_next = (m_next + 1) % m_days.Length; if (m_count < m_days.Length) m_count++;
			m_smaSum += c;
			return true;
		}

		private bool m_skipTr;

		private static int businessDaysBetween(DateTime a, DateTime b)
		{
			int n = 0;
			for (DateTime d = a.AddDays(1); d < b; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) n++;
			return n;
		}

		// S2: one new return (log, ratio-adjusted) and ROC1 (points, difference-adjusted) per completed day
		private void stepS2(DateTime date, DateTime prevDate, double roc1, double r)
		{
			if (prevDate != DateTime.MinValue && businessDaysBetween(prevDate, date) > 4)
				journal?.Invoke($"{label} {date:yyyy-MM-dd} return spans a gap of {businessDaysBetween(prevDate, date)} business days since {prevDate:yyyy-MM-dd}");
			// HV history: push yesterday's HV values before today's replace them
			if (config.hvPctLookback > 0 && m_hv.Count > 0)
			{
				foreach (var kv in m_hvHist) { double v; kv.Value[m_hvHistNext] = m_hv.TryGetValue(kv.Key, out v) ? v : double.NaN; }
				m_hvHistNext = (m_hvHistNext + 1) % config.hvPctLookback; if (m_hvHistCount < config.hvPctLookback) m_hvHistCount++;
			}
			m_ret[m_retNext] = r; m_retNext = (m_retNext + 1) % m_ret.Length; if (m_retCount < m_ret.Length) m_retCount++;
			m_hv.Clear();
			foreach (int n in config.hvLengths) m_hv[n] = HvOf(m_ret, m_retNext, m_retCount, n, config.hvAnnualisation);
			// ROC1 and Wilder RSI of the ROC1 series
			m_prevRoc1 = m_roc1; m_roc1 = roc1;
			int L = config.lbrRsiLength;
			if (L > 0 && !double.IsNaN(m_prevRoc1))
			{
				double d = m_roc1 - m_prevRoc1, up = d > 0 ? d : 0, dn = d < 0 ? -d : 0;
				m_lbrSeen++;
				if (m_lbrSeen <= L) { m_lbrSeedUp += up; m_lbrSeedDn += dn; if (m_lbrSeen == L) { m_lbrUp = m_lbrSeedUp / L; m_lbrDn = m_lbrSeedDn / L; } }
				else { m_lbrUp = (m_lbrUp * (L - 1) + up) / L; m_lbrDn = (m_lbrDn * (L - 1) + dn) / L; }
			}
			if (journal != null)
			{
				var sb = new System.Text.StringBuilder($"{label} {date:yyyy-MM-dd}");
				foreach (int n in config.hvLengths) sb.Append($" hv{n}({m_hv[n]:0.######})");
				if (config.hvPctLookback > 0) foreach (int n in config.hvLengths) sb.Append($" hv{n}pct({HvPercentile(n):0.##})");
				if (L > 0) sb.Append($" lbr({Lbr:0.####})");
				journal(sb.ToString());
				foreach (int n in config.hvLengths)   // review L3: HV_n needs n log returns = n + 1 closes; the percentile another hvPctLookback days
				{
					if (!double.IsNaN(m_hv[n]) && m_hvFirst.Add("hv" + n)) journal($"{label} hv{n} first valid {date:yyyy-MM-dd} (n={n} returns, {n + 1} closes)");
					if (config.hvPctLookback > 0 && !double.IsNaN(HvPercentile(n)) && m_hvFirst.Add("pct" + n)) journal($"{label} hv{n}pct first valid {date:yyyy-MM-dd} (lookback {config.hvPctLookback} days)");
				}
			}
		}

		/// <summary>Annualised sample SD (n - 1) of the newest n values of a return ring; NaN with fewer than n returns.</summary>
		public static double HvOf(double[] ring, int next, int count, int n, int annualisation)
		{
			if (n < 2 || count < n) return double.NaN;
			double sum = 0, sq = 0;
			for (int i = 0; i < n; i++) { double x = ring[(next - 1 - i + 2 * ring.Length) % ring.Length]; sum += x; }
			double mean = sum / n;
			for (int i = 0; i < n; i++) { double x = ring[(next - 1 - i + 2 * ring.Length) % ring.Length] - mean; sq += x * x; }
			return Math.Sqrt(annualisation) * Math.Sqrt(sq / (n - 1));
		}

		/// <summary>
		/// Roll seen intraday (Data1 roll bar) before the new contract's first day completes: adjust the history now so PrevClose /
		/// GapPct are already in the new contract's price space; the next AddDay skips its TR. offset = new minus old price.
		/// </summary>
		public void OnRoll(DateTime date, double offset)
		{
			if (!Configured || m_count == 0 || double.IsNaN(offset)) return;
			applyRoll(offset, date);
		}

		private void applyRoll(double d, DateTime date)
		{
			m_skipTr = true;
			// difference-adjust the level history; the return series stays ratio-continuous because rc is chained from c ratios and
			// the old close is rebased below (ratio = adjusted / unadjusted previous close)
			for (int i = 0; i < m_count; i++) { var x = at(i); x.o += d; x.h += d; x.l += d; x.c += d; x.rthc += d; set(i, x); }
			m_smaSum = 0; for (int i = 0; i < Math.Min(m_count, config.smaLength); i++) m_smaSum += at(i).c;
			m_wkH += d; m_wkL += d; m_prevWkH += d; m_prevWkL += d;   // weekly-stream values too, until the next weekly bar replaces them
			m_atr = double.NaN; m_trSeed = 0; m_trSeen = 0;   // §0.3: Wilder ATR restarts its seed
			journal?.Invoke($"{label} roll reset {date:yyyy-MM-dd}: history adjusted by {d:0.#####}, ATR reseeding");
		}
	}

	/// <summary>Spec S2: the day's RTH close = the close of the last bar stamped in the RTH value window (RthStart, RthValueEndEt],
	/// which honours early closes; NaN for a day without RTH bars.</summary>
	internal sealed class _RthCloseSelector
	{
		public DateTime Day { get; private set; } = DateTime.MinValue;
		public double Close { get; private set; } = double.NaN;
		/// <summary>Observe one closed bar of trading day `day`; returns the completed previous day's RTH close when the day changes, else NaN.</summary>
		public double Observe(DateTime day, bool isRthValueBar, double close)
		{
			double done = double.NaN;
			if (day != Day) { done = Close; Day = day; Close = double.NaN; }
			if (isRthValueBar) Close = close;
			return done;
		}
	}
}
