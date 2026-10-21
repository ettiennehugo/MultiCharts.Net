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
	/// <summary>Protected DailyConfig on _CustomSignal (no [Input]).</summary>
	public sealed class DailyConfig
	{
		public int dataStream = 0;      // 0 = aggregate Data1 per trading day (fallback); n = Data n daily bars (same unadjusted symbol)
		public int weeklyStream = 0;    // 0 = weekly high/low from the daily bars (Monday-start weeks); n = Data n weekly bars
		public int atrLength = 20;      // Wilder ATR (seed = SMA of the first atrLength TRs)
		public int smaLength = 200;
		public int rsiLength = 2;       // Wilder RSI
		public int[] tsmomLookbacks = { 252, 126, 63 };

		public string Validate()
		{
			var e = new List<string>();
			if (dataStream < 0) e.Add($"dataStream={dataStream}");
			if (weeklyStream < 0) e.Add($"weeklyStream={weeklyStream}");
			if (atrLength < 1) e.Add($"atrLength={atrLength}");
			if (smaLength < 1) e.Add($"smaLength={smaLength}");
			if (rsiLength < 1) e.Add($"rsiLength={rsiLength}");
			if (tsmomLookbacks == null) e.Add("tsmomLookbacks=null"); else foreach (int n in tsmomLookbacks) if (n < 1) e.Add($"tsmomLookbacks contains {n}");
			return e.Count == 0 ? null : "DailyConfig invalid: " + string.Join(", ", e);
		}
	}

	internal sealed class _DailyContext
	{
		public DailyConfig config = new DailyConfig();
		public string label = "DAILY";
		public Action<string> journal;
		public Action<string> warnOnce;

		private struct Day { public DateTime date; public double o, h, l, c, rc; }   // rc = ratio-adjusted close (return series)
		private Day[] m_days; private int m_count, m_next;
		private double m_atr = double.NaN, m_trSeed; private int m_trSeen;
		private double m_rsiUp = double.NaN, m_rsiDn = double.NaN, m_rsiSeedUp, m_rsiSeedDn; private int m_rsiSeen;
		private double m_smaSum;                   // over the last smaLength level closes
		private DateTime m_lastDate = DateTime.MinValue;
		private long m_weekKey = long.MinValue; private double m_wkH = double.NaN, m_wkL = double.NaN, m_prevWkH = double.NaN, m_prevWkL = double.NaN;
		private bool m_extWeek; private bool m_warned;

		public bool Configured { get; private set; }

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
			Configured = true; Reset();
			return null;
		}

		public void Reset()
		{
			if (!Configured) return;
			m_count = m_next = 0; m_atr = double.NaN; m_trSeed = 0; m_trSeen = 0;
			m_rsiUp = m_rsiDn = double.NaN; m_rsiSeedUp = m_rsiSeedDn = 0; m_rsiSeen = 0; m_smaSum = 0;
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
		public bool AddDay(DateTime date, double o, double h, double l, double c, bool firstDayOfNewContract = false, double rollOffset = double.NaN)
		{
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

			// SMA window over level closes
			if (m_count >= config.smaLength) m_smaSum -= at(config.smaLength - 1).c;
			m_days[m_next] = new Day { date = date, o = o, h = h, l = l, c = c, rc = rc };
			m_next = (m_next + 1) % m_days.Length; if (m_count < m_days.Length) m_count++;
			m_smaSum += c;
			return true;
		}

		private bool m_skipTr;

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
			for (int i = 0; i < m_count; i++) { var x = at(i); x.o += d; x.h += d; x.l += d; x.c += d; set(i, x); }
			m_smaSum = 0; for (int i = 0; i < Math.Min(m_count, config.smaLength); i++) m_smaSum += at(i).c;
			m_wkH += d; m_wkL += d; m_prevWkH += d; m_prevWkL += d;   // weekly-stream values too, until the next weekly bar replaces them
			m_atr = double.NaN; m_trSeed = 0; m_trSeen = 0;   // §0.3: Wilder ATR restarts its seed
			journal?.Invoke($"{label} roll reset {date:yyyy-MM-dd}: history adjusted by {d:0.#####}, ATR reseeding");
		}
	}
}
