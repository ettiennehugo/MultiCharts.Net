using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Gate 0 (G0-b + level baseline) engine for _TestGate0. Pure C# (no MultiCharts types) so tests/Gate0Tests runs it
	// off-platform. It consumes CLOSED Data1 bars (tick or minute) already converted to Eastern time and stamped with the
	// Globex trading day, aggregates them to 1m / 5m / 30m buckets itself, and
	//   - builds the Gate-0 level families each RTH session and feeds them, with one _Rng placebo per level, to a hosted
	//     _LevelStudy (A2-33) that sees 1-minute RTH bars only;
	//   - or runs one G0-b canary and publishes the order it wants (Gate0Intent) for the strategy to translate.
	// A2-39 roll honesty (review: Reset only): every multi-session source (prior profiles, naked POCs, weekly, composite,
	// week/month AVWAP) restarts at the roll; analysis drops events on a roll session +-1 (review M2).
	// Development window only: nothing is registered or traded on a trading day >= DevEnd (2025-01-01).
	// ============================================================================================

	public enum EGate0Mode { Strategy = 0, EventStudy = 1, ControlRT = 2, ControlFL = 3, Canary = 4 }

	/// <summary>Variant in the level-study modes. 0 = all families.</summary>
	public enum EGate0Family { All = 0, PriorProfile = 1, NakedPoc = 2, IbEdge = 3, Overnight = 4, Avwap = 5, Vwap = 6, RangeWeekly = 7, CompositeVa = 8, SinglePrint = 9, DvSource = 10 }

	/// <summary>Variant in Canary mode.</summary>
	public enum EGate0Canary { VW08 = 0, A2_24_i = 1, A2_24_ii = 2, A2_24_iii = 3, DV10 = 4, A2_53 = 5, DV01 = 6, PS03 = 7 }

	public enum EGate0Entry { None = 0, Market = 1, Limit = 2 }

	public struct Gate0Bar
	{
		public DateTime et;    // bar close time, Eastern
		public DateTime day;   // Globex trading day (date part used)
		public double o, h, l, c, v;
		public bool roll;      // first bar of a new contract (rollOn(1))
		public Gate0Bar(DateTime et, DateTime day, double o, double h, double l, double c, double v, bool roll = false)
		{ this.et = et; this.day = day.Date; this.o = o; this.h = h; this.l = l; this.c = c; this.v = v; this.roll = roll; }
	}

	/// <summary>What the canary wants on the next bar. Re-read after every Bar() call.</summary>
	public sealed class Gate0Intent
	{
		public int side;                       // wanted position sign: +1 / -1 / 0 (0 = flat or no opinion when entry None)
		public EGate0Entry entry;              // how to enter `side` while flat (or reverse, Market only)
		public double entryPrice = double.NaN; // limit price for EGate0Entry.Limit
		public double stop = double.NaN, target = double.NaN;   // protective exits while in a position
		public double reverseStop = double.NaN;// stop ENTRY in the opposite direction while in a position (A2-53 SAR)
		public bool flatten;                   // exit at market now
		public string tag = "";
		public void Clear() { side = 0; entry = EGate0Entry.None; entryPrice = stop = target = reverseStop = double.NaN; flatten = false; tag = ""; }
	}

	/// <summary>Volume-at-price on a tick grid; volume of a bar spread uniformly over its range.</summary>
	public sealed class Gate0Profile
	{
		public readonly SortedDictionary<long, double> rows = new SortedDictionary<long, double>();
		public double tick;
		public double total;
		public Gate0Profile(double tick) { this.tick = tick; }
		public void Add(double h, double l, double v)
		{
			if (!(v > 0)) return;
			long lo = (long)Math.Round(l / tick), hi = (long)Math.Round(h / tick);
			if (hi < lo) { long t = hi; hi = lo; lo = t; }
			double per = v / (hi - lo + 1);
			for (long r = lo; r <= hi; r++) { double x; rows.TryGetValue(r, out x); rows[r] = x + per; }
			total += v;
		}
		public void Merge(Gate0Profile p) { foreach (var kv in p.rows) { double x; rows.TryGetValue(kv.Key, out x); rows[kv.Key] = x + kv.Value; } total += p.total; }
		public bool Empty { get { return rows.Count == 0 || !(total > 0); } }

		/// <summary>POC (highest row, the one nearest the profile midpoint on ties) and the value area by two-sided expansion.</summary>
		public bool ValueArea(double pct, out double poc, out double vah, out double val)
		{
			poc = vah = val = double.NaN;
			if (Empty) return false;
			var keys = new List<long>(rows.Keys); var vol = new double[keys.Count];
			for (int i = 0; i < keys.Count; i++) vol[i] = rows[keys[i]];
			int p = 0; double mid = (keys.Count - 1) / 2.0;
			for (int i = 1; i < vol.Length; i++) if (vol[i] > vol[p] || (vol[i] == vol[p] && Math.Abs(i - mid) < Math.Abs(p - mid))) p = i;
			int a = p, b = p; double acc = vol[p], need = pct * total;
			while (acc < need && (a > 0 || b < vol.Length - 1))
			{
				double up = b < vol.Length - 1 ? vol[b + 1] : -1, dn = a > 0 ? vol[a - 1] : -1;
				if (up >= dn) { b++; acc += vol[b]; } else { a--; acc += vol[a]; }
			}
			poc = keys[p] * tick; vah = keys[b] * tick; val = keys[a] * tick;
			return true;
		}

		/// <summary>Single-print shelves: runs of >= minRows contiguous rows (inside the range) with volume <= frac x POC row volume.
		/// Each run gives its upper and lower edge price.</summary>
		public List<double[]> SinglePrintRuns(double frac, int minRows)
		{
			var res = new List<double[]>(); if (Empty) return res;
			double max = 0; foreach (var x in rows.Values) if (x > max) max = x;
			long first = 0, last = 0; bool f = true; foreach (var k in rows.Keys) { if (f) { first = k; f = false; } last = k; }
			long runStart = long.MinValue;
			for (long r = first; r <= last + 1; r++)
			{
				double x = 0; bool thin = r <= last && (!rows.TryGetValue(r, out x) || x <= frac * max);
				if (thin && r > first && runStart == long.MinValue) runStart = r;
				if (!thin && runStart != long.MinValue)
				{
					if (r - runStart >= minRows && r <= last) res.Add(new[] { (r - 1) * tick, runStart * tick });   // {upper, lower}
					runStart = long.MinValue;
				}
			}
			return res;
		}
	}

	/// <summary>Running VWAP with volume-weighted sigma.</summary>
	public sealed class Gate0Vwap
	{
		public double pv, p2v, v;
		public void Reset() { pv = p2v = v = 0; }
		public void Add(double price, double vol) { if (!(vol > 0)) return; pv += price * vol; p2v += price * price * vol; v += vol; }
		public bool Ready { get { return v > 0; } }
		public double Value { get { return v > 0 ? pv / v : double.NaN; } }
		public double Sigma { get { if (!(v > 0)) return double.NaN; double m = pv / v, s = p2v / v - m * m; return s > 0 ? Math.Sqrt(s) : 0; } }
	}

	/// <summary>Wilder ATR over completed bars.</summary>
	public sealed class Gate0Atr
	{
		private readonly int m_len; private int m_n; private double m_prevC = double.NaN, m_sum;
		public double Value = double.NaN;
		public Gate0Atr(int len) { m_len = len; }
		/// <summary>Forget the prior close (roll splice: the next true range is the bar's own range).</summary>
		public void ResetPrev() { m_prevC = double.NaN; }
		public void Add(double h, double l, double c)
		{
			double tr = double.IsNaN(m_prevC) ? h - l : Math.Max(h, m_prevC) - Math.Min(l, m_prevC);
			m_prevC = c; m_n++;
			if (m_n <= m_len) { m_sum += tr; if (m_n == m_len) Value = m_sum / m_len; }
			else Value = (Value * (m_len - 1) + tr) / m_len;
		}
	}

	/// <summary>Time-bucket aggregator: a bar belongs to the bucket that ends at its close time rounded up to `minutes`.
	/// A bucket closes on the bar stamped exactly at its end (minute data) or when a bar of a later bucket arrives (tick data).</summary>
	public sealed class Gate0Bucket
	{
		public readonly int minutes;
		public bool open; public DateTime end; public double o, h, l, c, v;
		public Gate0Bucket(int minutes) { this.minutes = minutes; }
		public static DateTime EndOf(DateTime t, int minutes)
		{
			long span = TimeSpan.FromMinutes(minutes).Ticks; long k = (t.Ticks + span - 1) / span; return new DateTime(k * span);
		}
		/// <summary>Returns true (with the closed bucket in the out params) when a bucket completed BEFORE this bar was added.</summary>
		public bool PreBar(DateTime t, out DateTime endOut, out double oo, out double hh, out double ll, out double cc, out double vv)
		{
			endOut = end; oo = o; hh = h; ll = l; cc = c; vv = v;
			if (open && EndOf(t, minutes) != end) { open = false; return true; }
			return false;
		}
		public void Add(DateTime t, double bo, double bh, double bl, double bc, double bv)
		{
			if (!open) { open = true; end = EndOf(t, minutes); o = bo; h = bh; l = bl; v = 0; }
			if (bh > h) h = bh; if (bl < l) l = bl; c = bc; v += bv;
		}
		/// <summary>True when the bar just added closes the bucket exactly (bar stamped at the bucket end).</summary>
		public bool PostBar(DateTime t) { if (open && t == end) { open = false; return true; } return false; }
	}

	/// <summary>One RTH session's facts (09:30-16:00 ET value window) plus the overnight before it.</summary>
	public sealed class Gate0Session
	{
		public DateTime day; public bool roll;
		public double rthO = double.NaN, rthH = double.MinValue, rthL = double.MaxValue, rthC = double.NaN;
		public double ibH = double.MinValue, ibL = double.MaxValue, onH = double.MinValue, onL = double.MaxValue;
		public Gate0Profile profile, opening;      // RTH profile; 09:30-11:30 range profile
		public Gate0Vwap vwap = new Gate0Vwap(), avHi = new Gate0Vwap(), avLo = new Gate0Vwap();
		public double poc = double.NaN, vah = double.NaN, val = double.NaN;
		public List<double> clusters = new List<double>();   // DV cluster prices
		public List<double> closes1m = new List<double>();   // RTH 1m closes (ControlRT draws)
		public bool pocNaked = true;                         // POC not traded since this session ended
		public bool HasRth { get { return !double.IsNaN(rthO); } }
		public Gate0Session(DateTime day, double tick) { this.day = day; profile = new Gate0Profile(tick); opening = new Gate0Profile(tick); }
	}

	/// <summary>Per-type Gate-0 result.</summary>
	public sealed class Gate0Result
	{
		public string type = ""; public int levels, touches, holds, placeboTouches, placeboHolds, sessions;
		// descriptive only (ruling on PR #20): binary hold = touched with positive rejection asymmetry, vs the random-price placebo
		public double holdRate = double.NaN, placeboHoldRate = double.NaN, adjusted = double.NaN;
		// G0-d decision metric (A2-33 verbatim): edge = E[reaction R | type] - E[reaction R | matched controls], paired per level
		public int pairs; public double edge = double.NaN, t = double.NaN, pHolm = double.NaN, ciLo = double.NaN, ciHi = double.NaN;
		public string a2Verdict = "";
		public string verdict = "";
		public string Line()
		{
			return string.Format(CultureInfo.InvariantCulture,
				"G0 type({0}) levels({1}) pairs({2}) edgeR({3:+0.000;-0.000;0.000}) t({4:0.00}) holm({5:0.0000}) ci95[{6:0.000},{7:0.000}] sessions({8}) A2-33({9}) | descriptive: touches({10}) holds({11}) holdRate({12:0.000}) placebo touches({13}) holds({14}) rate({15:0.000}) adj({16:+0.000;-0.000;0.000}) => {17}",
				type, levels, pairs, edge, t, pHolm, ciLo, ciHi, sessions, a2Verdict, touches, holds, holdRate, placeboTouches, placeboHolds, placeboHoldRate, adjusted, verdict);
		}
	}

	public sealed class Gate0Engine
	{
		public static readonly DateTime DevEnd = new DateTime(2025, 1, 1);
		public const string PlaceboPrefix = "PLACEBO:";
		public const int BootstrapDraws = 500, BootstrapBlock = 5;   // review M6
		public const int PriorProfileMaxAge = 5, NakedPocMaxAge = 15, CompositeSessions = 5;

		// ---- configuration (set before the first Bar) ----
		public EGate0Mode mode = EGate0Mode.EventStudy;
		public int variant;
		public double tick = 0.25;
		public long seed = 33;
		public double vaPct = 0.70;
		public Action<string> journal;
		// canary parameters (catalogue values where it gives one; see the _TestGate0 header for the rest)
		public double a253Gap = 0.005; public int a253Bar = 1;
		public double dv01F = 0.15, dv01K = 1.0;
		public double dv10S = 0.5, dv10F = 0.20;

		internal readonly _LevelStudy study = new _LevelStudy();
		public readonly Gate0Intent intent = new Gate0Intent();
		public readonly List<DateTime> rollDays = new List<DateTime>();
		public readonly List<DateTime> sessionDays = new List<DateTime>();
		public int Trades { get; private set; }
		/// <summary>Canary: sessions skipped because they are a roll day or the session right after a roll splice.</summary>
		public int RollSkips { get; private set; }

		// ---- state ----
		private readonly List<Gate0Session> m_hist = new List<Gate0Session>();   // completed sessions since the last roll, oldest first
		private Gate0Session m_cur;
		private DateTime m_day = DateTime.MinValue; private bool m_rthOn, m_studyOn;
		private readonly Gate0Bucket m_b1 = new Gate0Bucket(1), m_b5 = new Gate0Bucket(5), m_b30 = new Gate0Bucket(30);
		private readonly Gate0Atr m_atr5 = new Gate0Atr(14), m_atr30 = new Gate0Atr(14), m_atrD = new Gate0Atr(20);
		private readonly List<double> m_dailyRets = new List<double>(); private double m_prevDailyClose = double.NaN; private bool m_rollSplice;
		private readonly Gate0Vwap m_avWeek = new Gate0Vwap(), m_avMonth = new Gate0Vwap(), m_wkVwap = new Gate0Vwap(), m_avPc = new Gate0Vwap();
		private bool m_prevRth;   // AVWAP anchored at the prior RTH close: reset on the first bar after the RTH close
		private double m_lastStudyClose = double.NaN, m_lastC = double.NaN;
		private List<Gate0Profile> m_weekProfiles = new List<Gate0Profile>(), m_lastWeek; private int m_lastWeekAge; private DateTime m_weekKey;
		private Gate0Profile m_block; private double m_blockH, m_blockL; private readonly List<double> m_block30Ranges = new List<double>(); private readonly List<double[]> m_blocks = new List<double[]>();
		private readonly List<Gate0Profile> m_blockProfiles = new List<Gate0Profile>();
		private int m_mp; private bool m_inited;

		// ---- public read-outs (tests) ----
		public IList<Gate0Session> History { get { return m_hist; } }
		public Gate0Session Current { get { return m_cur; } }
		public double SessionVwap { get { return m_cur != null ? m_cur.vwap.Value : double.NaN; } }
		public double Atr20d { get { return m_atrD.Value; } }

		public string Configure()
		{
			if (tick <= 0) return "tick must be > 0";
			if (mode == EGate0Mode.Canary && (variant < 0 || variant > 7)) return "Canary Variant must be 0..7";
			if (mode != EGate0Mode.Canary && (variant < 0 || variant > 10)) return "level-study Variant must be 0..10";
			study.tickSize = tick;
			study.config.enabled = true; study.config.to = DevEnd.AddDays(-1); study.config.maxAge = NakedPocMaxAge;
			if (mode != EGate0Mode.Canary && variant == 0) study.config.controls = 5;   // memory: all families at once
			m_inited = true;
			return study.Configure();
		}

		private bool StudyMode { get { return mode != EGate0Mode.Canary; } }
		private bool Fam(EGate0Family f) { return variant == 0 || variant == (int)f; }
		private static bool IsRthTime(TimeSpan t) { return t > new TimeSpan(9, 30, 0) && t <= new TimeSpan(16, 0, 0); }

		/// <summary>Feed one CLOSED Data1 bar. mp = current market position sign (0 in study modes).</summary>
		public void Bar(Gate0Bar b, int mp = 0)
		{
			if (!m_inited) Configure();
			m_mp = mp;
			if (b.day != m_day) newDay(b);
			if (b.roll) onRoll(b.day);
			TimeSpan t = b.et.TimeOfDay;
			bool rth = IsRthTime(t);
			DateTime e; double o, h, l, c, v;
			if (m_b1.PreBar(b.et, out e, out o, out h, out l, out c, out v)) on1m(e, o, h, l, c, v);
			if (m_b5.PreBar(b.et, out e, out o, out h, out l, out c, out v)) on5m(e, o, h, l, c, v);
			if (m_b30.PreBar(b.et, out e, out o, out h, out l, out c, out v)) on30m(e, o, h, l, c, v);
			double tp = (b.h + b.l + b.c) / 3;
			if (!rth && m_prevRth) m_avPc.Reset();
			m_prevRth = rth;
			m_wkVwap.Add(tp, b.v); m_avWeek.Add(tp, b.v); m_avMonth.Add(tp, b.v); m_avPc.Add(tp, b.v);
			if (rth)
			{
				if (!m_rthOn) rthOpen(b);
				var s = m_cur;
				if (b.h > s.rthH) { s.rthH = b.h; s.avHi.Reset(); }
				if (b.l < s.rthL) { s.rthL = b.l; s.avLo.Reset(); }
				s.avHi.Add(tp, b.v); s.avLo.Add(tp, b.v);
				s.rthC = b.c; s.vwap.Add(tp, b.v); s.profile.Add(b.h, b.l, b.v);
				if (t <= new TimeSpan(10, 30, 0)) { if (b.h > s.ibH) s.ibH = b.h; if (b.l < s.ibL) s.ibL = b.l; }
				// range profile window (09:30, 11:30]: half-open per the PR #18 ruling (bars are end-stamped). The 11:30 end has NO
				// spec basis (A2 gives no window for a Gate-0 range profile) - Coder-defined, flagged in PR #20 pending Analyst 2.
				if (t <= new TimeSpan(11, 30, 0)) s.opening.Add(b.h, b.l, b.v);
				if (m_block == null) { m_block = new Gate0Profile(tick); m_blockH = double.MinValue; m_blockL = double.MaxValue; }
				m_block.Add(b.h, b.l, b.v); if (b.h > m_blockH) m_blockH = b.h; if (b.l < m_blockL) m_blockL = b.l;
				foreach (var old in m_hist) if (old.pocNaked && b.l <= old.poc && old.poc <= b.h) old.pocNaked = false;
				m_b1.Add(b.et, b.o, b.h, b.l, b.c, b.v); m_b5.Add(b.et, b.o, b.h, b.l, b.c, b.v); m_b30.Add(b.et, b.o, b.h, b.l, b.c, b.v);
			}
			else if (m_cur != null && !m_rthOn)
			{
				if (b.h > m_cur.onH) m_cur.onH = b.h; if (b.l < m_cur.onL) m_cur.onL = b.l;
			}
			if (rth)
			{
				if (m_b1.PostBar(b.et)) on1m(m_b1.end, m_b1.o, m_b1.h, m_b1.l, m_b1.c, m_b1.v);
				if (m_b5.PostBar(b.et)) on5m(m_b5.end, m_b5.o, m_b5.h, m_b5.l, m_b5.c, m_b5.v);
				if (m_b30.PostBar(b.et)) on30m(m_b30.end, m_b30.o, m_b30.h, m_b30.l, m_b30.c, m_b30.v);
			}
			if (mode == EGate0Mode.Canary) canaryBar(b, rth);
			m_lastC = b.c;
		}

		// ================================ sessions =================================
		private void newDay(Gate0Bar b)
		{
			closeRth();
			if (m_cur != null && m_cur.HasRth)
			{
				m_hist.Add(m_cur); if (m_hist.Count > NakedPocMaxAge + 1) m_hist.RemoveAt(0);
				m_weekProfiles.Add(m_cur.profile);
			}
			DateTime wk = b.day.AddDays(-(((int)b.day.DayOfWeek + 6) % 7));
			if (wk != m_weekKey)
			{
				m_avWeek.Reset(); m_wkVwap.Reset();
				if (m_weekProfiles.Count > 0) { m_lastWeek = m_weekProfiles; m_lastWeekAge = 1; }
				m_weekProfiles = new List<Gate0Profile>(); m_weekKey = wk;
			}
			else if (m_lastWeek != null) m_lastWeekAge++;
			// the overnight of b.day begins now: the new session object collects ONH/ONL before its RTH
			m_cur = new Gate0Session(b.day, tick);
			m_day = b.day; m_rthOn = false; sessionDays.Add(b.day);
			// month anchor (A2-39: week / month anchors also reset at the roll, see onRoll)
			if (sessionDays.Count < 2 || sessionDays[sessionDays.Count - 2].Month != b.day.Month) m_avMonth.Reset();
			canaryNewDay();
		}

		private void onRoll(DateTime day)
		{
			if (rollDays.Count == 0 || rollDays[rollDays.Count - 1] != day) rollDays.Add(day);
			if (m_cur != null) m_cur.roll = true;
			// A2-39 Reset: forget every multi-session source built on the old contract
			m_hist.Clear(); m_weekProfiles.Clear(); m_lastWeek = null; m_avWeek.Reset(); m_avMonth.Reset(); m_wkVwap.Reset(); m_avPc.Reset();
			m_atrD.ResetPrev(); m_rollSplice = true;
			m_lastStudyClose = double.NaN;
		}

		private void rthOpen(Gate0Bar b)
		{
			m_rthOn = true;
			var s = m_cur; s.rthO = b.o;
			m_block = null; m_blocks.Clear(); m_blockProfiles.Clear();
			if (StudyMode)
			{
				study.BeginSession(b.day);
				m_studyOn = b.day < DevEnd;
				if (m_studyOn) registerLevels(b);
			}
			canaryRthOpen(b);
		}

		private void closeRth()
		{
			if (m_cur == null || !m_cur.HasRth) return;
			var s = m_cur;
			double poc, vah, val;
			if (s.profile.ValueArea(vaPct, out poc, out vah, out val)) { s.poc = poc; s.vah = vah; s.val = val; }
			closeBlock();
			// DV clusters (CODER-DEFINED, pending Analyst 2: no spec rule exists; no separate accumulation rule): 30m blocks with a narrow range whose POC row is >= 2x the block's median row volume
			double meanR = 0; foreach (var r in m_block30Ranges) meanR += r; meanR = m_block30Ranges.Count > 0 ? meanR / m_block30Ranges.Count : double.NaN;
			for (int i = 0; i < m_blockProfiles.Count; i++)
			{
				var p = m_blockProfiles[i]; double range = m_blocks[i][0] - m_blocks[i][1];
				if (!(meanR > 0) || range > 0.7 * meanR || p.rows.Count < 3) continue;
				var vols = new List<double>(p.rows.Values); vols.Sort(); double med = vols[vols.Count / 2];
				double bp, bh, bl; if (!p.ValueArea(vaPct, out bp, out bh, out bl)) continue;
				if (p.rows[(long)Math.Round(bp / tick)] >= 2 * med) s.clusters.Add(bp);
			}
			m_atrD.Add(s.rthH, s.rthL, s.rthC);
			if (!double.IsNaN(m_prevDailyClose) && !m_rollSplice) { m_dailyRets.Add(Math.Log(s.rthC / m_prevDailyClose)); if (m_dailyRets.Count > 300) m_dailyRets.RemoveAt(0); }
			m_prevDailyClose = s.rthC; m_rollSplice = false;
		}

		private void closeBlock()
		{
			if (m_block == null || m_block.Empty) { m_block = null; return; }
			m_blockProfiles.Add(m_block); m_blocks.Add(new[] { m_blockH, m_blockL }); m_block30Ranges.Add(m_blockH - m_blockL);
			if (m_block30Ranges.Count > 260) m_block30Ranges.RemoveAt(0);
			m_block = null;
		}

		private void on1m(DateTime end, double o, double h, double l, double c, double v)
		{
			if (!IsRthTime(end.TimeOfDay)) return;
			if (m_cur != null) m_cur.closes1m.Add(c);
			if (StudyMode && m_studyOn) { study.Bar(o, h, l, c, v); m_lastStudyClose = c; }
			if (mode == EGate0Mode.Canary) canary1m(end, o, h, l, c);
		}
		private void on5m(DateTime end, double o, double h, double l, double c, double v) { m_atr5.Add(h, l, c); }
		private void on30m(DateTime end, double o, double h, double l, double c, double v)
		{
			m_atr30.Add(h, l, c); closeBlock();
			if (mode == EGate0Mode.Canary) canary30m(end, o, h, l, c);
		}

		// ================================ levels =================================
		/// <summary>Raw (type, price, age) candidates of the enabled families for the session that opens now.</summary>
		public List<KeyValuePair<string, double[]>> Candidates()
		{
			var r = new List<KeyValuePair<string, double[]>>();
			Action<string, double, int> add = (ty, px, age) => { if (px > 0 && !double.IsNaN(px) && px < 1e12 && age >= 1) r.Add(new KeyValuePair<string, double[]>(ty, new[] { px, age })); };
			int n = m_hist.Count;
			Gate0Session p1 = n > 0 ? m_hist[n - 1] : null;
			if (Fam(EGate0Family.PriorProfile))
				for (int a = 1; a <= Math.Min(PriorProfileMaxAge, n); a++) { var s = m_hist[n - a]; add("POC", s.poc, a); add("VAH", s.vah, a); add("VAL", s.val, a); }
			if (Fam(EGate0Family.NakedPoc))
				for (int a = 2; a <= Math.Min(NakedPocMaxAge, n); a++) { var s = m_hist[n - a]; if (s.pocNaked) add("NPOC", s.poc, a); }
			if (p1 != null && Fam(EGate0Family.IbEdge)) { add("IBH", p1.ibH, 1); add("IBL", p1.ibL, 1); }
			if (m_cur != null && Fam(EGate0Family.Overnight) && m_cur.onH > double.MinValue) { add("ONH", m_cur.onH, 1); add("ONL", m_cur.onL, 1); }
			if (p1 != null && Fam(EGate0Family.Avwap))
			{
				add("AV_PH", p1.avHi.Value, 1); add("AV_PL", p1.avLo.Value, 1);
				if (m_avWeek.Ready) add("AV_WK", m_avWeek.Value, 1);
				if (m_avMonth.Ready) add("AV_MO", m_avMonth.Value, 1);
				// ruling on PR #20: also the session-open anchor (prior RTH session's open-anchored AVWAP; same price as that
				// session's VWAP, kept separately so the AVWAP family is complete) and the prior-RTH-close anchor (VWAP of every bar
				// since the prior RTH close, through the overnight, as of today's first RTH bar)
				if (p1.vwap.Ready) add("AV_OPEN", p1.vwap.Value, 1);
				if (m_avPc.Ready) add("AV_PC", m_avPc.Value, 1);
			}
			if (p1 != null && Fam(EGate0Family.Vwap) && p1.vwap.Ready)
			{ double vw = p1.vwap.Value, sd = p1.vwap.Sigma; add("VWAP", vw, 1); if (sd > 0) { add("VWAP+1", vw + sd, 1); add("VWAP-1", vw - sd, 1); } }
			if (Fam(EGate0Family.RangeWeekly))
			{
				double pc, ph, pl;
				if (p1 != null && p1.opening.ValueArea(vaPct, out pc, out ph, out pl)) { add("RPOC", pc, 1); add("RVAH", ph, 1); add("RVAL", pl, 1); }
				if (m_lastWeek != null && m_lastWeekAge >= 1 && m_lastWeekAge <= 5)
				{
					var w = new Gate0Profile(tick); foreach (var p in m_lastWeek) w.Merge(p);
					if (w.ValueArea(vaPct, out pc, out ph, out pl)) { add("WPOC", pc, m_lastWeekAge); add("WVAH", ph, m_lastWeekAge); add("WVAL", pl, m_lastWeekAge); }
				}
			}
			if (Fam(EGate0Family.CompositeVa) && n >= CompositeSessions)
			{
				var cp = new Gate0Profile(tick); for (int i = n - CompositeSessions; i < n; i++) cp.Merge(m_hist[i].profile);
				double pc, ph, pl; if (cp.ValueArea(vaPct, out pc, out ph, out pl)) { add("CVAH", ph, 1); add("CVAL", pl, 1); }
			}
			if (p1 != null && Fam(EGate0Family.SinglePrint))
				foreach (var run in p1.profile.SinglePrintRuns(0.05, 4)) { add("SP_HI", run[0], 1); add("SP_LO", run[1], 1); }
			if (p1 != null && Fam(EGate0Family.DvSource)) foreach (var px in p1.clusters) add("DV_CLUSTER", px, 1);
			return r;
		}

		private void registerLevels(Gate0Bar b)
		{
			double refPx = m_lastStudyClose;
			if (double.IsNaN(refPx)) return;
			Gate0Session p1 = m_hist.Count > 0 ? m_hist[m_hist.Count - 1] : null;
			int no = 0;
			foreach (var kv in Candidates())
			{
				no++;
				string type = kv.Key; double px = kv.Value[0]; int age = (int)kv.Value[1];
				var rng = new _Rng(_Rng.Seed(seed, _Rng.Hash(type), b.day.Ticks, no));
				if (mode == EGate0Mode.ControlFL)
				{
					double atr = m_atr5.Value; if (!(atr > 0)) continue;
					double[] k = { 0.5, 1, 2 }; px += (rng.NextInt(2) == 0 ? -1 : 1) * k[rng.NextInt(3)] * atr; type = "FL:" + type;
				}
				else if (mode == EGate0Mode.ControlRT)
				{
					if (p1 == null || p1.closes1m.Count == 0) continue;
					px = p1.closes1m[rng.NextInt(p1.closes1m.Count)]; type = "RT:" + type;
				}
				px = Math.Round(px / tick) * tick;
				string key = type + "@" + px.ToString("R", CultureInfo.InvariantCulture) + "#" + no;
				if (!study.AddLevel(type, px, age, double.NaN, key)) continue;
				// matched _Rng placebo: same side-agnostic distance band, random sign
				double dist = Math.Abs(px - refPx); if (dist < tick) dist = tick;
				double u = 0.5 + rng.NextDouble();
				double pb = refPx + (rng.NextInt(2) == 0 ? -1 : 1) * dist * u; pb = Math.Round(pb / tick) * tick;
				study.AddLevel(PlaceboPrefix + type, pb, age, double.NaN, "PB:" + key);
			}
		}

		/// <summary>True when day is a roll session or the session before / after one (review M2).</summary>
		public bool InRollWindow(DateTime day)
		{
			int i = sessionDays.BinarySearch(day.Date); if (i < 0) return false;
			for (int k = Math.Max(0, i - 1); k <= Math.Min(sessionDays.Count - 1, i + 1); k++) if (rollDays.Contains(sessionDays[k])) return true;
			return false;
		}

		/// <summary>Finish the study and score every level type with A2-33's reaction edge vs its 20 matched controls (KM level
		/// death and Holm across types come from _LevelStudy.Analyse). The seeded random-price placebo hold rate is descriptive only.</summary>
		public List<Gate0Result> Finish()
		{
			var res = new List<Gate0Result>();
			if (!StudyMode) return res;
			study.Finish();
			var kept = new List<LevelEvent>();
			foreach (var e in study.Events) if (e.day < DevEnd && !InRollWindow(e.day)) kept.Add(e);
			var real = new List<LevelEvent>();
			foreach (var e in kept) if (!e.type.StartsWith(PlaceboPrefix)) real.Add(e);
			var a2 = new Dictionary<string, LevelTypeResult>(StringComparer.Ordinal);
			foreach (var r in _LevelStudy.Analyse(real, study.config)) { a2[r.type] = r; if (journal != null) journal(_LevelStudy.Summary(r)); }
			var placebo = new Dictionary<string, LevelEvent>();
			foreach (var e in kept) if (!e.control && e.type.StartsWith(PlaceboPrefix)) placebo[e.day.Ticks + "|" + e.key] = e;
			var byType = new SortedDictionary<string, List<KeyValuePair<LevelEvent, LevelEvent>>>(StringComparer.Ordinal);
			var ctlBy = new Dictionary<string, List<LevelEvent>>();
			foreach (var e in real)
			{
				if (!e.control) continue;
				string k = e.type + "|" + e.day.Ticks + "|" + e.levelNo; List<LevelEvent> c;
				if (!ctlBy.TryGetValue(k, out c)) ctlBy[k] = c = new List<LevelEvent>(); c.Add(e);
			}
			var diffs = new Dictionary<string, List<KeyValuePair<DateTime, double>>>(StringComparer.Ordinal);
			foreach (var e in real)
			{
				if (e.control) continue;
				LevelEvent pb; placebo.TryGetValue(e.day.Ticks + "|PB:" + e.key, out pb);
				List<KeyValuePair<LevelEvent, LevelEvent>> l;
				if (!byType.TryGetValue(e.type, out l)) byType[e.type] = l = new List<KeyValuePair<LevelEvent, LevelEvent>>();
				l.Add(new KeyValuePair<LevelEvent, LevelEvent>(e, pb));
				List<LevelEvent> ctl; ctlBy.TryGetValue(e.type + "|" + e.day.Ticks + "|" + e.levelNo, out ctl);
				double d = PairedReaction(e, ctl);
				if (double.IsNaN(d)) continue;
				List<KeyValuePair<DateTime, double>> dl;
				if (!diffs.TryGetValue(e.type, out dl)) diffs[e.type] = dl = new List<KeyValuePair<DateTime, double>>();
				dl.Add(new KeyValuePair<DateTime, double>(e.day, d));
			}
			foreach (var kv in byType)
			{
				var r = Score(kv.Key, kv.Value, seed);   // descriptive hold counts
				List<KeyValuePair<DateTime, double>> dl; diffs.TryGetValue(kv.Key, out dl);
				LevelTypeResult lr; a2.TryGetValue(kv.Key, out lr);
				ScoreEdge(r, dl ?? new List<KeyValuePair<DateTime, double>>(), lr, seed);
				res.Add(r);
				if (journal != null) journal(r.Line());
			}
			return res;
		}

		/// <summary>A2-33 paired difference: level reaction R minus the mean R of its touched matched controls (NaN if either is missing).</summary>
		public static double PairedReaction(LevelEvent lv, IList<LevelEvent> ctl)
		{
			if (lv == null || !lv.touched || double.IsNaN(lv.reaction) || ctl == null) return double.NaN;
			double s = 0; int n = 0;
			foreach (var c in ctl) if (c.touched && !double.IsNaN(c.reaction)) { s += c.reaction; n++; }
			return n == 0 ? double.NaN : lv.reaction - s / n;
		}

		/// <summary>Descriptive only: binary hold = touched with positive rejection asymmetry.</summary>
		public static bool Hold(LevelEvent e) { return e != null && e.touched && e.asymmetry > 0; }

		/// <summary>Descriptive hold counts vs the random-price placebo (not used for the verdict).</summary>
		public static Gate0Result Score(string type, IList<KeyValuePair<LevelEvent, LevelEvent>> pairs, long seed)
		{
			var r = new Gate0Result { type = type, levels = pairs.Count };
			foreach (var p in pairs)
			{
				if (p.Key.touched) r.touches++; if (Hold(p.Key)) r.holds++;
				if (p.Value != null && p.Value.touched) r.placeboTouches++; if (Hold(p.Value)) r.placeboHolds++;
			}
			r.holdRate = r.touches > 0 ? (double)r.holds / r.touches : double.NaN;
			r.placeboHoldRate = r.placeboTouches > 0 ? (double)r.placeboHolds / r.placeboTouches : double.NaN;
			r.adjusted = r.holdRate - r.placeboHoldRate;
			return r;
		}

		/// <summary>G0-d decision: the A2-33 reaction edge (paired vs matched controls) with a 95% percentile CI from a moving block
		/// bootstrap over sessions (500 draws, blocks of 5 consecutive sessions, _Rng seeded), then the verdict.</summary>
		public static void ScoreEdge(Gate0Result r, IList<KeyValuePair<DateTime, double>> diffs, LevelTypeResult a2, long seed)
		{
			r.pairs = diffs.Count;
			var days = new SortedDictionary<DateTime, double[]>();   // per session {sum d, n}
			foreach (var kv in diffs) { double[] a; if (!days.TryGetValue(kv.Key, out a)) days[kv.Key] = a = new double[2]; a[0] += kv.Value; a[1]++; }
			var s = new List<double[]>(days.Values); r.sessions = s.Count;
			double sum = 0; foreach (var kv in diffs) sum += kv.Value;
			r.edge = diffs.Count > 0 ? sum / diffs.Count : double.NaN;
			if (a2 != null) { r.t = a2.t; r.pHolm = a2.pHolm; r.a2Verdict = a2.verdict; }
			if (s.Count > 0)
			{
				var rng = new _Rng(_Rng.Seed(seed, _Rng.Hash("Gate0"), _Rng.Hash(r.type)));
				var draws = new List<double>(BootstrapDraws);
				int nb = (s.Count + BootstrapBlock - 1) / BootstrapBlock, starts = Math.Max(1, s.Count - BootstrapBlock + 1);
				for (int d = 0; d < BootstrapDraws; d++)
				{
					double t = 0, n = 0;
					for (int k = 0; k < nb; k++)
					{
						int st = rng.NextInt(starts);
						for (int j = st; j < Math.Min(s.Count, st + BootstrapBlock); j++) { t += s[j][0]; n += s[j][1]; }
					}
					if (n > 0) draws.Add(t / n);
				}
				draws.Sort();
				if (draws.Count >= 20) { r.ciLo = draws[(int)Math.Floor(0.025 * (draws.Count - 1))]; r.ciHi = draws[(int)Math.Ceiling(0.975 * (draws.Count - 1))]; }
			}
			r.verdict = Verdict(r.pairs, r.t, r.a2Verdict);
		}

		/// <summary>G0-d verdict. §2.3 sample-size gates on the paired n, then A2-33's decision rule (|t| &lt; 2 -> not traded; Holm
		/// across level types; half-life and top-tercile restrictions) as computed by _LevelStudy.Verdict.</summary>
		public static string Verdict(int n, double t, string a2)
		{
			if (n < 30) return "PARK (no evaluation: n<30)";
			if (n < 150) return "PARK (inconclusive: 30<=n<150)";
			if (double.IsNaN(t) || Math.Abs(t) < 2) return "PARK (kill: edge ~ 0, |t| < 2 - level type not traded)";
			if (a2 != null && (a2.StartsWith("keep") || a2.StartsWith("trade"))) return "PASS (" + a2 + ")";
			return "PARK (kill: " + (string.IsNullOrEmpty(a2) ? "A2-33 drop" : a2) + ")";
		}

		// ================================ canaries =================================
		private double m_vwPrev = double.NaN; private bool m_touched1, m_longDone, m_shortDone, m_dayDone, m_rthLive;
		private double m_priorClose = double.NaN, m_gap, m_sessHi, m_sessLo; private bool m_a253Ok, m_reversed; private int m_bar30;
		private double m_dvPoc = double.NaN, m_dvSl; private int m_dvState;   // 0 wait move-away, 1 armed, 2 done
		private Gate0Session m_prior; private bool m_psOutside; private int m_psSide;
		private readonly List<double> m_wkUpper = new List<double>(), m_wkLower = new List<double>();
		private int m_prevMp; private bool m_skipDay;

		private Gate0Session PriorSession { get { return m_hist.Count > 0 ? m_hist[m_hist.Count - 1] : null; } }

		private void canaryNewDay()
		{
			m_touched1 = m_longDone = m_shortDone = m_dayDone = m_rthLive = m_reversed = false;
			m_vwPrev = double.NaN; m_bar30 = 0; m_dvState = 0; m_psOutside = false; m_psSide = 0;
			if (mode == EGate0Mode.Canary) intent.Clear();
		}

		private void canaryRthOpen(Gate0Bar b)
		{
			if (mode != EGate0Mode.Canary) return;
			m_prior = PriorSession;
			bool rollDay = m_cur.roll, afterRoll = sessionDays.Count >= 2 && rollDays.Contains(sessionDays[sessionDays.Count - 2]);
			m_skipDay = b.day >= DevEnd || rollDay || afterRoll || m_prior == null || !(m_atrD.Value > 0);
			m_rthLive = !m_skipDay;
			if (rollDay || afterRoll) { RollSkips++; if (journal != null) journal(string.Format(CultureInfo.InvariantCulture, "G0-b roll-skip {0:yyyy-MM-dd} total({1})", b.day, RollSkips)); }
			m_sessHi = b.h; m_sessLo = b.l;
			m_priorClose = m_prior != null ? m_prior.rthC : double.NaN;
			m_gap = m_prior != null ? (b.o - m_priorClose) / m_priorClose : 0;
			if (m_prior != null && (EGate0Canary)variant == EGate0Canary.A2_53)
			{
				bool inside = m_hist.Count >= 2 && m_prior.rthH < m_hist[m_hist.Count - 2].rthH && m_prior.rthL > m_hist[m_hist.Count - 2].rthL;
				double hv6 = Hv(6), hv100 = Hv(100);
				m_a253Ok = Math.Abs(m_gap) >= a253Gap && (inside || (hv6 < hv100));
			}
			if (m_prior != null) { m_dvPoc = m_prior.poc; m_dvSl = dv01F * m_atrD.Value; m_psOutside = b.o > m_prior.vah || b.o < m_prior.val; m_psSide = b.o > m_prior.vah ? -1 : 1; }
		}


		/// <summary>Sample stdev of the last n RTH close-to-close log returns (the return across a roll splice is skipped); NaN when short.</summary>
		public double Hv(int n)
		{
			int c = m_dailyRets.Count; if (c < n || n < 2) return double.NaN;
			double m = 0; var r = new double[n];
			for (int i = 0; i < n; i++) { r[i] = m_dailyRets[c - n + i]; m += r[i]; }
			m /= n; double ss = 0; foreach (var x in r) ss += (x - m) * (x - m);
			return Math.Sqrt(ss / (n - 1));
		}

		private void enter(int side, EGate0Entry kind, double px, double stop, double target, string tag)
		{
			intent.side = side; intent.entry = kind; intent.entryPrice = px; intent.stop = stop; intent.target = target; intent.tag = tag; intent.flatten = false;
		}

		/// <summary>Raw bar step: position bookkeeping and the 15:55 flat.</summary>
		private void canaryBar(Gate0Bar b, bool rth)
		{
			if (m_mp != 0 && m_prevMp == 0) { Trades++; intent.entry = EGate0Entry.None; if (journal != null) journal(string.Format(CultureInfo.InvariantCulture, "G0-b {0} fill side({1}) {2:yyyy-MM-dd HH:mm}", (EGate0Canary)variant, m_mp, b.et)); }
			if (m_mp != 0 && m_prevMp != 0 && m_mp != m_prevMp) { Trades++; onReversed(); }
			if (m_mp == 0 && m_prevMp != 0) { intent.stop = intent.target = intent.reverseStop = double.NaN; if ((EGate0Canary)variant != EGate0Canary.VW08) intent.side = 0; }
			m_prevMp = m_mp;
			if (rth) { if (b.h > m_sessHi) m_sessHi = b.h; if (b.l < m_sessLo) m_sessLo = b.l; }
			TimeSpan t = b.et.TimeOfDay;
			if (t >= new TimeSpan(15, 55, 0) || !rth)
			{
				m_rthLive = false;
				if (m_mp != 0 || intent.entry != EGate0Entry.None) { intent.Clear(); intent.flatten = m_mp != 0; }
				else intent.flatten = false;
			}
		}

		private void onReversed()
		{
			if ((EGate0Canary)variant == EGate0Canary.A2_53)
			{
				m_reversed = true; intent.reverseStop = double.NaN;
				intent.stop = m_mp > 0 ? m_sessLo - tick : m_sessHi + tick;
			}
		}

		private void canary1m(DateTime end, double o, double h, double l, double c)
		{
			var cv = (EGate0Canary)variant;
			double vw = m_cur.vwap.Value, sd = m_cur.vwap.Sigma, vwBefore = m_vwPrev;
			m_vwPrev = vw;
			if (!m_rthLive || end.TimeOfDay >= new TimeSpan(15, 55, 0)) return;
			switch (cv)
			{
				case EGate0Canary.VW08:
					if (c > vw) enter(1, EGate0Entry.Market, double.NaN, double.NaN, double.NaN, "VW08L");
					else if (c < vw) enter(-1, EGate0Entry.Market, double.NaN, double.NaN, double.NaN, "VW08S");
					break;
				// A2-24 (catalogue VW-08 entry, verbatim): (i) fade the first sVWAP touch 1R/1R - R = ATR14 on 5m is Coder-defined (spec silent);
				// (ii) fade a 2-sigma band close, target = sVWAP FROZEN AT ENTRY (intent.target is set once in enter() and never moved);
				// stop = mirror distance (Coder-defined); (iii) 2xATR-from-VWAP fade, ATR = ATR14 on 5m, stop mirror distance (Coder-defined).
				case EGate0Canary.A2_24_i:
					if (m_touched1 || double.IsNaN(vwBefore) || end.TimeOfDay < new TimeSpan(9, 35, 0)) break;
					if (l <= vwBefore && vwBefore <= h)
					{
						m_touched1 = true; double R = m_atr5.Value; if (!(R > 0) || m_mp != 0) break;
						int side = o >= vwBefore ? 1 : -1;   // approached from above -> VWAP as support -> long
						enter(side, EGate0Entry.Market, double.NaN, c - side * R, c + side * R, "A224i");
					}
					break;
				case EGate0Canary.A2_24_ii:
					if (m_mp != 0 || !(sd > 0)) break;
					if (c > vw + 2 * sd && !m_shortDone) { m_shortDone = true; enter(-1, EGate0Entry.Market, double.NaN, c + (c - vw), vw, "A224ii"); }
					else if (c < vw - 2 * sd && !m_longDone) { m_longDone = true; enter(1, EGate0Entry.Market, double.NaN, c - (vw - c), vw, "A224ii"); }
					break;
				case EGate0Canary.A2_24_iii:
					{
						double a = m_atr5.Value; if (m_mp != 0 || !(a > 0)) break;
						if (c - vw >= 2 * a && !m_shortDone) { m_shortDone = true; enter(-1, EGate0Entry.Market, double.NaN, c + (c - vw), vw, "A224iii"); }
						else if (vw - c >= 2 * a && !m_longDone) { m_longDone = true; enter(1, EGate0Entry.Market, double.NaN, c - (vw - c), vw, "A224iii"); }
					}
					break;
				case EGate0Canary.DV01:
					if (double.IsNaN(m_dvPoc) || m_dvState == 2) break;
					if (m_dvState == 0)
					{
						bool touch = l <= m_dvPoc && m_dvPoc <= h;
						double away = Math.Max(Math.Abs(h - m_dvPoc), Math.Abs(l - m_dvPoc)), need = dv01K * m_atr5.Value;
						if (touch && !(Math.Abs(o - m_dvPoc) >= need)) { m_dvState = 2; break; }   // first touch came before the move-away
						if (need > 0 && (Math.Abs(c - m_dvPoc) >= need || away >= need) && !touch)
						{
							m_dvState = 1; int side = c > m_dvPoc ? 1 : -1;
							enter(side, EGate0Entry.Limit, m_dvPoc, m_dvPoc - side * m_dvSl, m_dvPoc + side * m_dvSl, "DV01");
						}
					}
					else if (m_dvState == 1 && l <= m_dvPoc && m_dvPoc <= h)
					{
						m_dvState = 2; if (m_mp == 0) intent.entry = EGate0Entry.None;   // first touch only: unfilled -> cancel
					}
					break;
			}
		}

		private void canary30m(DateTime end, double o, double h, double l, double c)
		{
			var cv = (EGate0Canary)variant;
			if (!IsRthTime(end.TimeOfDay)) { if (cv == EGate0Canary.DV10) wkBand(); return; }
			m_bar30++;
			if (cv == EGate0Canary.DV10) wkBand();
			if (!m_rthLive || end.TimeOfDay >= new TimeSpan(15, 55, 0)) return;
			switch (cv)
			{
				case EGate0Canary.A2_53:
					if (m_bar30 != a253Bar || !m_a253Ok || m_mp != 0) break;
					if (m_gap > 0 && m_sessLo > m_priorClose) { enter(-1, EGate0Entry.Market, double.NaN, double.NaN, double.NaN, "A253S"); intent.reverseStop = m_sessHi + tick; }
					else if (m_gap < 0 && m_sessHi < m_priorClose) { enter(1, EGate0Entry.Market, double.NaN, double.NaN, double.NaN, "A253L"); intent.reverseStop = m_sessLo - tick; }
					break;
				case EGate0Canary.PS03:
					if (!m_psOutside || m_dayDone || m_mp != 0 || m_prior == null) break;
					if (c < m_prior.vah && c > m_prior.val)
					{
						m_dayDone = true; double W = m_prior.vah - m_prior.val;
						if (m_psSide < 0) enter(-1, EGate0Entry.Market, double.NaN, m_prior.vah + 0.25 * W, m_prior.poc, "PS03S");
						else enter(1, EGate0Entry.Market, double.NaN, m_prior.val - 0.25 * W, m_prior.poc, "PS03L");
					}
					break;
				case EGate0Canary.DV10:
					{
						if (m_dayDone || m_mp != 0) break;
						double vw = m_wkVwap.Value, sd = m_wkVwap.Sigma, a = m_atr30.Value; int n = m_wkUpper.Count;
						if (!(sd > 0) || !(a > 0) || n < 9) break;
						bool flat = Math.Abs(m_wkUpper[n - 1] - m_wkUpper[n - 9]) < dv10S * a && Math.Abs(m_wkLower[n - 1] - m_wkLower[n - 9]) < dv10S * a;
						if (!flat) { if (intent.entry == EGate0Entry.Limit) intent.entry = EGate0Entry.None; break; }
						double sl = dv10F * m_atrD.Value;
						if (c > vw && c < vw + sd) enter(-1, EGate0Entry.Limit, Math.Round((vw + sd) / tick) * tick, vw + sd + sl, vw, "DV10S");
						else if (c < vw && c > vw - sd) enter(1, EGate0Entry.Limit, Math.Round((vw - sd) / tick) * tick, vw - sd - sl, vw, "DV10L");
					}
					break;
			}
		}

		private void wkBand()
		{
			double vw = m_wkVwap.Value, sd = m_wkVwap.Sigma; if (double.IsNaN(vw)) return;
			m_wkUpper.Add(vw + sd); m_wkLower.Add(vw - sd);
			if (m_wkUpper.Count > 64) { m_wkUpper.RemoveAt(0); m_wkLower.RemoveAt(0); }
		}
	}
}
