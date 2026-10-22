// ============================================================================================
// M-AVWAP — event-anchored VWAPs (spec /workspace/specs/missing-functionality-specs.md rev 4 §2, §0.1-§0.3).
// Plain internal sealed class (VPB-9 convention), MC-free: the owner (_CustomSignal) feeds closed bars and events, so the
// whole file compiles in tests/AvwapTests without MultiCharts. Created in StartCalc only when AvwapEvents != None.
// Same math as _VpVwapEngine: typical price (H+L+C)/3, sums PV, P2V, V; sigma = sqrt(P2V/V - VWAP^2). Bars with
// volume <= 0 or price <= 0 are skipped (same rule as _VpVwapEngine.add).
// ============================================================================================
using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	/// <summary>M-AVWAP anchor events (protected AvwapEvents on _CustomSignal). None = manager not created.</summary>
	[Flags]
	public enum EAvwapAnchor
	{
		None = 0,
		SwingHigh = 1 << 0,     // PRICEACTION layer-0 confirmed pivot high; anchored at the extreme bar, usable from confirmation
		SwingLow = 1 << 1,
		Breakout = 1 << 2,      // PRICEACTION layer-0 BOS bar or BreakoutUp/Down candle flag
		GapOpen = 1 << 3,       // first RTH bar with |open - prior RTH close| >= gapAtrFraction x daily ATR (fallback: 3 x SMA-TR(20))
		VolumeClimax = 1 << 4,  // bar volume z >= climaxZ (M-TOD clock-minute z when available, else 20-bar z)
		WeekOpen = 1 << 5,      // first bar of a new exchange-session week / month / quarter
		MonthOpen = 1 << 6,
		QuarterOpen = 1 << 7,
		NewsBar = 1 << 8,       // reserved, inert (M-NEWS deferred)
		Swing = SwingHigh | SwingLow
	}

	public enum EAvwapPeriod { Week, Month, Quarter }

	/// <summary>Calendar keys on the exchange-session date (Globex date on futures).</summary>
	public static class _AvwapCalendar
	{
		/// <summary>Monotonic period key: Week = Monday-start week, Month = y*12+m, Quarter = y*4+q.</summary>
		public static long PeriodKey(EAvwapPeriod p, DateTime sessionDate)
		{
			DateTime d = sessionDate.Date;
			switch (p)
			{
				case EAvwapPeriod.Week: return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)).Ticks / TimeSpan.TicksPerDay;
				case EAvwapPeriod.Month: return d.Year * 12L + d.Month - 1;
				default: return d.Year * 4L + (d.Month - 1) / 3;
			}
		}
	}

	/// <summary>Spec S3: AVWAP placebo twins. P1 = unweighted mean TP from the anchor; P2 = volume-shuffled AVWAP (each bar's volume
	/// replaced by a seeded draw from the same clock-minute slot of earlier sessions). P3 (EMA20) needs no engine work.</summary>
	[Flags]
	public enum EAvwapPlacebo { None = 0, P1 = 1, P2 = 2 }

	/// <summary>Protected AvwapLimits on _CustomSignal (no [Input]). Defaults per spec §2.</summary>
	public sealed class AvwapLimits
	{
		public int maxAnchors = 6;
		public int maxAgeSessions = 10;           // expire after this many completed sessions
		public bool replaceSameType = true;       // a new anchor of a type expires the live one(s) of that type
		public double[] bandSigmas = { 1, 2, 3 };
		public int slopeBars = 5;
		public int dataStream = 1;
		public double gapAtrFraction = 0.5;       // GapOpen vs daily ATR(20)
		public double gapFallbackTrMultiple = 3;  // GapOpen fallback vs SMA-TR(fallbackLength) of the stream
		public double climaxZ = 3;
		public int fallbackLength = 20;           // SMA-TR and volume-z fallback window
		public int backfillBars = 2;              // PV ring for swing back-fill; owner sets PRICEACTION strength + 1
		public EAvwapPlacebo placebos = EAvwapPlacebo.None;   // S3: twin accumulators (off by default)
		public int p2MinPool = 10;                // S3: fewer same-slot values -> P2 uses the bar's own volume (p2Degraded)

		public string Validate()
		{
			var e = new List<string>();
			if (maxAnchors < 1) e.Add($"maxAnchors={maxAnchors}");
			if (maxAgeSessions < 1) e.Add($"maxAgeSessions={maxAgeSessions}");
			if (bandSigmas == null) e.Add("bandSigmas=null");
			else foreach (double k in bandSigmas) if (!(k > 0)) e.Add($"bandSigmas contains {k}");
			if (slopeBars < 1) e.Add($"slopeBars={slopeBars}");
			if (dataStream < 1) e.Add($"dataStream={dataStream}");
			if (!(gapAtrFraction > 0)) e.Add($"gapAtrFraction={gapAtrFraction}");
			if (!(gapFallbackTrMultiple > 0)) e.Add($"gapFallbackTrMultiple={gapFallbackTrMultiple}");
			if (!(climaxZ > 0)) e.Add($"climaxZ={climaxZ}");
			if (fallbackLength < 2) e.Add($"fallbackLength={fallbackLength}");
			if (backfillBars < 1) e.Add($"backfillBars={backfillBars}");
			if (p2MinPool < 1) e.Add($"p2MinPool={p2MinPool}");
			return e.Count == 0 ? null : "AvwapLimits invalid: " + string.Join(", ", e);
		}
	}

	/// <summary>One closed bar fed to the manager.</summary>
	public struct AvwapBar
	{
		public int bar;               // CurrentBar of the manager's stream
		public DateTime time;
		public double o, h, l, c, v;
		public DateTime sessionDate;  // exchange-session date (trading-day key)
		public bool rth;              // bar is in RTH (true for every bar on non-futures)
		public int slot;              // S3: clock-minute slot of the session (M-TOD minuteOfSession); used by P2 only
	}

	/// <summary>Events of the bar from PRICEACTION (layer 0). Extreme bar/time of a confirmed pivot are used for the back-fill.</summary>
	public struct AvwapPaEvents
	{
		public bool swingHigh, swingLow;
		public int swingHighBar, swingLowBar;
		public DateTime swingHighTime, swingLowTime;
		public bool breakoutUp, breakoutDown;   // BosUp/BosDown or BreakoutUp/BreakoutDown
	}

	/// <summary>One anchored VWAP. Read-only outside the manager.</summary>
	public sealed class _Avwap
	{
		internal double sPV, sP2V, sV;
		internal double tS, tS2; internal int tN;          // S3 P1: sum TP, sum TP^2, bars
		internal double qPV, qP2V, qV;                      // S3 P2: shuffled-volume sums
		internal bool p2Journaled;
		/// <summary>S3 P1: unweighted mean TP from the anchor (NaN when P1 is off).</summary>
		public double P1 { get; internal set; } = double.NaN;
		public double P1Sigma { get; internal set; } = double.NaN;
		/// <summary>S3 P2: TP weighted by same-slot volumes drawn from earlier sessions (NaN when P2 is off).</summary>
		public double P2 { get; internal set; } = double.NaN;
		public double P2Sigma { get; internal set; } = double.NaN;
		/// <summary>S3: at least one bar used its own volume because the slot pool had fewer than p2MinPool values.</summary>
		public bool p2Degraded { get; internal set; }
		/// <summary>S3 trade tag of a placebo arm: AV:{type}:{yyyyMMddHHmm}:P1|P2.</summary>
		public string TagFor(EAvwapPlacebo arm) { return arm == EAvwapPlacebo.P1 ? Tag + ":P1" : arm == EAvwapPlacebo.P2 ? Tag + ":P2" : Tag; }
		internal double[] hist; internal int histCount, histNext;   // last slopeBars+1 committed values
		public int Id { get; internal set; }
		public EAvwapAnchor Type { get; internal set; }
		public DateTime AnchorTime { get; internal set; }
		public int AnchorBar { get; internal set; }
		public DateTime UsableFrom { get; internal set; }        // confirmation time for swing anchors, anchor time otherwise
		public int Direction { get; internal set; }              // +1 up event (swing low, breakout up, gap up), -1 down, 0 neutral
		public double Value { get; internal set; } = double.NaN;
		public double Sigma { get; internal set; } = double.NaN;
		public double SlopeTicks { get; internal set; } = double.NaN;   // per bar over slopeBars
		public double SlopeAtr { get; internal set; } = double.NaN;
		public int AgeBars { get; internal set; }
		public int AgeSessions { get; internal set; }
		public bool Touched { get; internal set; }
		public int TouchCount { get; internal set; }
		public bool Expired { get; internal set; }
		public string ExpireReason { get; internal set; }
		/// <summary>Journal tag (§0.2): AV:{type}:{anchorTime:yyyyMMddHHmm}.</summary>
		public string Tag { get { return $"AV:{Type}:{AnchorTime:yyyyMMddHHmm}"; } }
		public double Band(double k) { return double.IsNaN(Value) || double.IsNaN(Sigma) ? double.NaN : Value + k * Sigma; }
		public bool Valid { get { return !Expired && !double.IsNaN(Value); } }
	}

	internal sealed class _AvwapManager
	{
		public AvwapLimits limits = new AvwapLimits();
		public EAvwapAnchor events = EAvwapAnchor.None;
		public double tickSize = 0.25;
		public string label = "AV";
		public Action<string> journal;     // ETraceLevel.Journal: create / expire / invalidate (bar times only)
		public Action<string> trace;       // Debug
		public Action<string> warnOnce;
		public Func<double> dailyAtr;      // M-DAILY Atr20d (NaN when not valid) -> GapOpen fallback otherwise
		public Func<double> volumeZ;       // M-TOD clock-minute volume z (NaN when not valid) -> 20-bar z otherwise
		/// <summary>S3: same-slot volumes of earlier COMPLETED sessions (never the current one), oldest first; null = no pool.</summary>
		public Func<int, IList<double>> volumePool;

		public bool Configured { get; private set; }
		public int LastBar { get { return m_lastBar; } }

		private readonly List<_Avwap> m_live = new List<_Avwap>();
		private int m_nextId;
		private int m_lastBar = int.MinValue;
		private bool m_warned;
		// PV ring for swing back-fill (bar, price, vol)
		private int[] m_rBar; private double[] m_rP, m_rV; private DateTime[] m_rT; private int m_rCount, m_rNext;
		private IList<double>[] m_rPool;   // S3: the slot pool each ring bar saw, so a back-filled P2 equals a forward-built one
		// fallback stats
		private double[] m_tr, m_vol; private int m_fCount, m_fNext; private double m_trSum, m_vSum, m_v2Sum, m_prevClose = double.NaN;
		private bool m_skipTr;
		// calendar / RTH tracking
		private DateTime m_session = DateTime.MinValue;
		private long m_wk = long.MinValue, m_mo = long.MinValue, m_qt = long.MinValue;
		private double m_lastRthClose = double.NaN;
		private DateTime m_lastRthSession = DateTime.MinValue, m_rthOpenedSession = DateTime.MinValue;

		/// <summary>S3 ruling 3: StartCalc error (null = fine) when P2 is on but the Data1 M-TOD volume pool is unavailable.</summary>
		public static string P2PoolError(EAvwapPlacebo placebos, int dataStream, bool todRunning)
		{
			if ((placebos & EAvwapPlacebo.P2) == 0 || (dataStream == 1 && todRunning)) return null;
			return $"AvwapLimits.placebos P2 on Data{dataStream} has no M-TOD volume pool" + (dataStream == 1 ? " (M-TOD not running)" : "") +
				": AvwapLimits.placebos P2 needs AvwapLimits.dataStream = 1 (M-TOD slots are Data1); every P2 bar would use its own volume (p2Degraded). " +
				"Refusing to start: set AvwapLimits.dataStream = 1 or remove P2.";
		}

		public string Configure()
		{
			string err = limits.Validate();
			if (err != null) { Configured = false; return err; }
			if (!(tickSize > 0)) { Configured = false; return $"AvwapManager tickSize={tickSize}"; }
			int n = limits.backfillBars + 1;
			m_rBar = new int[n]; m_rP = new double[n]; m_rV = new double[n]; m_rT = new DateTime[n]; m_rPool = new IList<double>[n];
			m_tr = new double[limits.fallbackLength]; m_vol = new double[limits.fallbackLength];
			Configured = true;
			Reset();
			return null;
		}

		public void Reset()
		{
			if (!Configured) return;
			m_live.Clear(); m_nextId = 0; m_lastBar = int.MinValue; m_warned = false;
			m_rCount = m_rNext = 0; resetFallback();
			m_session = DateTime.MinValue; m_wk = m_mo = m_qt = long.MinValue;
			m_lastRthClose = double.NaN; m_lastRthSession = m_rthOpenedSession = DateTime.MinValue;
		}

		private void resetFallback() { m_fCount = m_fNext = 0; m_trSum = m_vSum = m_v2Sum = 0; m_prevClose = double.NaN; }

		/// <summary>Live (not expired) anchors, oldest first. Do not modify.</summary>
		public IReadOnlyList<_Avwap> Anchors { get { return m_live; } }
		/// <summary>Newest live anchor of a type (null if none).</summary>
		public _Avwap Latest(EAvwapAnchor type)
		{
			for (int i = m_live.Count - 1; i >= 0; i--) if (m_live[i].Type == type) return m_live[i];
			return null;
		}

		/// <summary>SMA of TR over fallbackLength (NaN until full). Splice-bar TR skipped after a roll.</summary>
		public double FallbackTrAvg { get { return m_fCount == m_tr.Length ? m_trSum / m_fCount : double.NaN; } }

		/// <summary>§0.3: invalidate (not shift) every live anchor; the next bar's TR is the splice and is skipped.</summary>
		public void OnRoll(DateTime barTime)
		{
			if (!Configured) return;
			foreach (var a in m_live) { a.Expired = true; a.ExpireReason = "roll"; journal?.Invoke($"{label} invalidated (roll) {a.Tag} at {barTime:yyyy-MM-dd HH:mm:ss}"); }
			m_live.Clear();
			m_rCount = m_rNext = 0;
			resetFallback(); m_skipTr = true;
			m_lastRthClose = double.NaN;   // the prior RTH close is old-contract price space
		}

		/// <summary>Feed one CLOSED bar plus its PRICEACTION events. Bars must have strictly increasing numbers (duplicates ignored).</summary>
		public bool Add(AvwapBar b, AvwapPaEvents pa)
		{
			if (!Configured) return false;
			if (b.bar == m_lastBar) return false;
			if (b.bar < m_lastBar) { warn($"{label}: out-of-order bar {b.bar} < {m_lastBar} at {b.time:yyyy-MM-dd HH:mm:ss} ignored"); return false; }
			m_lastBar = b.bar;
			double price = (b.h > 0 && b.l > 0) ? (b.h + b.l + b.c) / 3.0 : b.c;
			IList<double> pool = (limits.placebos & EAvwapPlacebo.P2) != 0 && volumePool != null ? volumePool(b.slot) : null;

			// fallback stats: z uses the prior bars only (the climax bar must not dilute its own norm)
			double zFallback = double.NaN;
			if (m_fCount == m_vol.Length) { double mean = m_vSum / m_fCount, var = m_v2Sum / m_fCount - mean * mean; if (var > 0) zFallback = (b.v - mean) / Math.Sqrt(var); }
			double trAvgBefore = FallbackTrAvg;
			double tr = (double.IsNaN(m_prevClose) || m_skipTr) ? b.h - b.l : Math.Max(b.h, m_prevClose) - Math.Min(b.l, m_prevClose);
			m_skipTr = false; m_prevClose = b.c;
			if (m_fCount == m_tr.Length) { m_trSum -= m_tr[m_fNext]; double ov = m_vol[m_fNext]; m_vSum -= ov; m_v2Sum -= ov * ov; } else m_fCount++;
			m_tr[m_fNext] = tr; m_vol[m_fNext] = b.v; m_trSum += tr; m_vSum += b.v; m_v2Sum += b.v * b.v; m_fNext = (m_fNext + 1) % m_tr.Length;

			// sessions: age live anchors on a new session date
			bool newSession = b.sessionDate.Date != m_session;
			if (newSession && m_session != DateTime.MinValue) foreach (var a in m_live) a.AgeSessions++;
			if (newSession) m_session = b.sessionDate.Date;

			// update existing anchors with this bar (touch is tested against the value before the bar is added)
			foreach (var a in m_live)
			{
				if (!double.IsNaN(a.Value) && b.l <= a.Value && b.h >= a.Value) { a.Touched = true; a.TouchCount++; }
				addTo(a, price, b.v); addTwins(a, price, b.v, b.time, pool); a.AgeBars++; finish(a, trAvgBefore);
			}

			// ring for back-fill (after the update so the current bar is in it)
			m_rBar[m_rNext] = b.bar; m_rP[m_rNext] = price; m_rV[m_rNext] = b.v; m_rT[m_rNext] = b.time; m_rPool[m_rNext] = pool; m_rNext = (m_rNext + 1) % m_rBar.Length; if (m_rCount < m_rBar.Length) m_rCount++;

			// calendar anchors
			long wk = _AvwapCalendar.PeriodKey(EAvwapPeriod.Week, b.sessionDate), mo = _AvwapCalendar.PeriodKey(EAvwapPeriod.Month, b.sessionDate), qt = _AvwapCalendar.PeriodKey(EAvwapPeriod.Quarter, b.sessionDate);
			bool first = m_wk == long.MinValue;
			if (!first && wk != m_wk && on(EAvwapAnchor.WeekOpen)) create(EAvwapAnchor.WeekOpen, b, 0, price);
			if (!first && mo != m_mo && on(EAvwapAnchor.MonthOpen)) create(EAvwapAnchor.MonthOpen, b, 0, price);
			if (!first && qt != m_qt && on(EAvwapAnchor.QuarterOpen)) create(EAvwapAnchor.QuarterOpen, b, 0, price);
			m_wk = wk; m_mo = mo; m_qt = qt;

			// GapOpen: first RTH bar of a session vs the prior session's last RTH close
			if (b.rth && m_rthOpenedSession != b.sessionDate.Date)
			{
				m_rthOpenedSession = b.sessionDate.Date;
				if (on(EAvwapAnchor.GapOpen) && !double.IsNaN(m_lastRthClose) && m_lastRthSession != b.sessionDate.Date)
				{
					double datr = dailyAtr != null ? dailyAtr() : double.NaN;
					double thr = !double.IsNaN(datr) && datr > 0 ? limits.gapAtrFraction * datr : limits.gapFallbackTrMultiple * trAvgBefore;
					double gap = b.o - m_lastRthClose;
					if (!double.IsNaN(thr) && Math.Abs(gap) >= thr) create(EAvwapAnchor.GapOpen, b, Math.Sign(gap), price);
				}
			}
			if (b.rth) { m_lastRthClose = b.c; m_lastRthSession = b.sessionDate.Date; }

			// VolumeClimax
			if (on(EAvwapAnchor.VolumeClimax))
			{
				double z = volumeZ != null ? volumeZ() : double.NaN;
				if (double.IsNaN(z)) z = zFallback;
				if (!double.IsNaN(z) && z >= limits.climaxZ) create(EAvwapAnchor.VolumeClimax, b, Math.Sign(b.c - b.o), price);
			}

			// PRICEACTION anchors
			if (pa.breakoutUp && on(EAvwapAnchor.Breakout)) create(EAvwapAnchor.Breakout, b, +1, price);
			else if (pa.breakoutDown && on(EAvwapAnchor.Breakout)) create(EAvwapAnchor.Breakout, b, -1, price);
			if (pa.swingHigh && on(EAvwapAnchor.SwingHigh)) createSwing(EAvwapAnchor.SwingHigh, b, pa.swingHighBar, pa.swingHighTime, -1);
			if (pa.swingLow && on(EAvwapAnchor.SwingLow)) createSwing(EAvwapAnchor.SwingLow, b, pa.swingLowBar, pa.swingLowTime, +1);

			// expiry by age
			for (int i = m_live.Count - 1; i >= 0; i--)
				if (m_live[i].AgeSessions > limits.maxAgeSessions) expire(i, "age", b.time);
			return true;
		}

		private bool on(EAvwapAnchor f) { return (events & f) != 0; }
		private void warn(string s) { if (!m_warned) { m_warned = true; warnOnce?.Invoke(s); } }

		private static void addTo(_Avwap a, double price, double vol)
		{
			if (vol <= 0 || price <= 0) return;
			a.sPV += price * vol; a.sP2V += price * price * vol; a.sV += vol;
		}

		// S3 twins: P1 counts every bar (a V = 0 bar too); P2 weights TP by a seeded same-slot draw (deterministic, no lookahead)
		private void addTwins(_Avwap a, double price, double vol, DateTime time, IList<double> pool)
		{
			if (limits.placebos == EAvwapPlacebo.None || !(price > 0)) return;
			if ((limits.placebos & EAvwapPlacebo.P1) != 0)
			{
				a.tS += price; a.tS2 += price * price; a.tN++;
				double m = a.tS / a.tN, var = a.tS2 / a.tN - m * m;
				a.P1 = m; a.P1Sigma = var > 0 ? Math.Sqrt(var) : 0;
			}
			if ((limits.placebos & EAvwapPlacebo.P2) != 0)
			{
				double v;
				if (pool != null && pool.Count >= limits.p2MinPool) v = pool[new _Rng(_Rng.Seed(a.Id, time.Ticks, 2)).NextInt(pool.Count)];
				else
				{
					v = vol; a.p2Degraded = true;
					if (!a.p2Journaled) { a.p2Journaled = true; journal?.Invoke($"{label} p2Degraded {a.Tag} at {time:yyyy-MM-dd HH:mm:ss}: slot pool {(pool == null ? 0 : pool.Count)} < {limits.p2MinPool}, own volume used"); }
				}
				if (v > 0) { a.qPV += price * v; a.qP2V += price * price * v; a.qV += v; }
				if (a.qV > 0) { double m = a.qPV / a.qV, var = a.qP2V / a.qV - m * m; a.P2 = m; a.P2Sigma = var > 0 ? Math.Sqrt(var) : 0; }
			}
		}


		private void finish(_Avwap a, double atr)
		{
			if (a.sV <= 0) return;
			double v = a.sPV / a.sV, var = a.sP2V / a.sV - v * v;
			a.Value = v; a.Sigma = var > 0 ? Math.Sqrt(var) : 0;
			int n = a.hist.Length;
			a.hist[a.histNext] = v; a.histNext = (a.histNext + 1) % n; if (a.histCount < n) a.histCount++;
			if (a.histCount == n)
			{
				double oldest = a.hist[a.histNext];   // after the advance, histNext is the oldest slot
				double perBar = (v - oldest) / limits.slopeBars;
				a.SlopeTicks = perBar / tickSize;
				a.SlopeAtr = atr > 0 ? perBar / atr : double.NaN;
			}
		}

		private _Avwap newAnchor(EAvwapAnchor type, DateTime anchorTime, int anchorBar, DateTime usableFrom, int dir)
		{
			var a = new _Avwap { Id = ++m_nextId, Type = type, AnchorTime = anchorTime, AnchorBar = anchorBar, UsableFrom = usableFrom, Direction = dir };
			a.hist = new double[limits.slopeBars + 1];
			return a;
		}

		private void create(EAvwapAnchor type, AvwapBar b, int dir, double price)
		{
			var a = newAnchor(type, b.time, b.bar, b.time, dir);
			addTo(a, price, b.v); addTwins(a, price, b.v, b.time, m_rPool[(m_rNext - 1 + m_rPool.Length) % m_rPool.Length]); finish(a, FallbackTrAvg);
			admit(a, b.time);
		}

		/// <summary>Back-fill PV from the extreme bar to the confirming bar out of the ring; skipped (warned) if the extreme fell out.</summary>
		private void createSwing(EAvwapAnchor type, AvwapBar b, int extremeBar, DateTime extremeTime, int dir)
		{
			int oldest = m_rBar[(m_rNext - m_rCount + m_rBar.Length) % m_rBar.Length];
			if (extremeBar < oldest || extremeBar > b.bar)
			{
				warn($"{label}: {type} extreme bar {extremeBar} outside the back-fill ring [{oldest}..{b.bar}] at {b.time:yyyy-MM-dd HH:mm:ss}; raise AvwapLimits.backfillBars");
				return;
			}
			var a = newAnchor(type, extremeTime, extremeBar, b.time, dir);
			for (int k = m_rCount - 1; k >= 0; k--)
			{
				int i = (m_rNext - 1 - k + 2 * m_rBar.Length) % m_rBar.Length;
				if (m_rBar[i] < extremeBar) continue;
				addTo(a, m_rP[i], m_rV[i]); addTwins(a, m_rP[i], m_rV[i], m_rT[i], m_rPool[i]); a.AgeBars++;
				if (m_rBar[i] != b.bar) finish(a, FallbackTrAvg);   // history for the slope, bar by bar
			}
			a.AgeBars--; finish(a, FallbackTrAvg);
			admit(a, b.time);
		}

		private void admit(_Avwap a, DateTime t)
		{
			if (limits.replaceSameType)
				for (int i = m_live.Count - 1; i >= 0; i--) if (m_live[i].Type == a.Type) expire(i, "replaced", t);
			while (m_live.Count >= limits.maxAnchors)
			{
				int victim = 0;
				for (int i = 0; i < m_live.Count; i++) if (m_live[i].Type == a.Type) { victim = i; break; }   // oldest of the same type, else oldest
				expire(victim, "evicted", t);
			}
			m_live.Add(a);
			journal?.Invoke($"{label} created {a.Tag} usable={a.UsableFrom:yyyy-MM-dd HH:mm:ss} value={a.Value:0.#####}" + placeboNote());
		}

		private string placeboNote()
		{
			if (limits.placebos == EAvwapPlacebo.None) return "";
			var p = new List<string>();
			if ((limits.placebos & EAvwapPlacebo.P1) != 0) p.Add("P1");
			if ((limits.placebos & EAvwapPlacebo.P2) != 0) p.Add("P2");
			return $" placebos({string.Join(",", p)})" + ((limits.placebos & EAvwapPlacebo.P2) != 0 ? " p2seed(_Rng.Seed(anchorId, barTime.Ticks, 2))" : "");
		}

		private void expire(int i, string why, DateTime t)
		{
			var a = m_live[i]; a.Expired = true; a.ExpireReason = why; m_live.RemoveAt(i);
			journal?.Invoke($"{label} expired ({why}) {a.Tag} at {t:yyyy-MM-dd HH:mm:ss}");
		}
	}
}
