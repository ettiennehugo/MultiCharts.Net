// ============================================================================================
// M-XMKT — cross-market bar streams (spec /workspace/specs/missing-functionality-specs.md rev 4 §6).
// Extra chart series (BarsOfData(n): NQ for ES, ZN, VIX, ...) at bar resolutions; tick-level legs use M-TICK instead.
// Plain internal sealed class, MC-free (tests/XmktTests). _CustomSignal creates one per XmktLegs entry in StartCalc and feeds
// it the leg's last CLOSED bar with Time <= the Data1 bar time (closedBarsOnly). Measures use the leg's own bars, never
// interpolated: a sample is taken when the leg's closed bar advances, paired with the Data1 close at that calc.
// ============================================================================================
using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	/// <summary>One cross-market leg (protected XmktLegs on _CustomSignal; no [Input]).</summary>
	public sealed class XmktLegConfig
	{
		public string role = "leg";      // e.g. "NQ", "ZN", "VIX"
		public int dataStream = 2;
		public int maxStaleSec = 0;      // 0 = 2 x the leg's bar size (set by the owner from Resolution)
		public int returnBars = 1;       // k-bar log return on the leg's own bars
		public int corrLength = 60;      // rolling correlation / beta window (paired samples)
		public int rsLength = 60;        // RS spread z window
		public bool leaderSwing = false; // own _PriceActionEngine on the leg stream (owner side)

		public string Validate()
		{
			var e = new List<string>();
			if (dataStream < 2) e.Add($"dataStream={dataStream} (a leg is Data2 or higher)");
			if (maxStaleSec < 0) e.Add($"maxStaleSec={maxStaleSec}");
			if (returnBars < 1) e.Add($"returnBars={returnBars}");
			if (corrLength < 3) e.Add($"corrLength={corrLength}");
			if (rsLength < 3) e.Add($"rsLength={rsLength}");
			return e.Count == 0 ? null : $"XmktLegs[{role}] invalid: " + string.Join(", ", e);
		}
	}

	internal sealed class _XmktLeg
	{
		public XmktLegConfig config = new XmktLegConfig();
		public TimeSpan maxStale = TimeSpan.Zero;   // resolved by the owner; zero = staleness check off
		public Action<string> journal;

		private double[] m_legC, m_d1C; private int m_cCount, m_cNext;        // closes at sample points (returnBars+1 ring)
		private double[] m_rx, m_ry; private int m_rCount, m_rNext;            // paired per-sample log returns (leg, data1)
		private double m_sx, m_sy, m_sxx, m_syy, m_sxy;
		private double[] m_rs; private int m_rsCount, m_rsNext; private double m_rsSum, m_rs2Sum;
		private int m_lastLegBar = int.MinValue;
		private DateTime m_lastLegTime = DateTime.MinValue;
		private bool m_stale = true, m_staleLogged;

		public bool Configured { get; private set; }
		/// <summary>False while the leg is stale (no closed bar within maxStale of Data1) or has no data yet: no trade on its signal.</summary>
		public bool Available { get { return Configured && !m_stale && m_cCount > 0; } }
		public DateTime LastLegTime { get { return m_lastLegTime; } }
		public double LastClose { get { return m_cCount > 0 ? m_legC[(m_cNext - 1 + m_legC.Length) % m_legC.Length] : double.NaN; } }

		public string Configure()
		{
			string err = config.Validate();
			if (err != null) { Configured = false; return err; }
			m_legC = new double[config.returnBars + 1]; m_d1C = new double[config.returnBars + 1];
			m_rx = new double[config.corrLength]; m_ry = new double[config.corrLength]; m_rs = new double[config.rsLength];
			Configured = true; Reset();
			return null;
		}

		public void Reset()
		{
			if (!Configured) return;
			clearHistory();
			m_lastLegBar = int.MinValue; m_lastLegTime = DateTime.MinValue; m_stale = true; m_staleLogged = false;
		}

		private void clearHistory()
		{
			m_cCount = m_cNext = m_rCount = m_rNext = m_rsCount = m_rsNext = 0;
			m_sx = m_sy = m_sxx = m_syy = m_sxy = m_rsSum = m_rs2Sum = 0;
		}

		/// <summary>
		/// Data1 closed-bar calc. legBar/legTime/legClose = the leg's mapped closed bar (legBar &lt; 0: none). Takes a sample when the
		/// leg bar advanced; updates staleness (Journal fact on every change).
		/// </summary>
		public void Step(DateTime d1Time, double d1Close, int legBar, DateTime legTime, double legClose)
		{
			if (!Configured) return;
			if (legBar >= 0 && legBar > m_lastLegBar && legClose > 0 && d1Close > 0)
			{
				m_lastLegBar = legBar; m_lastLegTime = legTime;
				if (m_cCount > 0)
				{
					int p = (m_cNext - 1 + m_legC.Length) % m_legC.Length;
					push(Math.Log(legClose / m_legC[p]), Math.Log(d1Close / m_d1C[p]));
				}
				m_legC[m_cNext] = legClose; m_d1C[m_cNext] = d1Close; m_cNext = (m_cNext + 1) % m_legC.Length; if (m_cCount < m_legC.Length) m_cCount++;
				double rs = Math.Log(legClose / d1Close);
				if (m_rsCount == m_rs.Length) { double o = m_rs[m_rsNext]; m_rsSum -= o; m_rs2Sum -= o * o; } else m_rsCount++;
				m_rs[m_rsNext] = rs; m_rsSum += rs; m_rs2Sum += rs * rs; m_rsNext = (m_rsNext + 1) % m_rs.Length;
			}
			bool stale = m_lastLegTime == DateTime.MinValue || _MtfMath.IsStale(m_lastLegTime, d1Time, maxStale);
			if (stale != m_stale || !m_staleLogged)
			{
				if (!m_staleLogged && !stale) journal?.Invoke($"XMKT {config.role} Data{config.dataStream} available at {d1Time:yyyy-MM-dd HH:mm:ss} (staleness limit {(maxStale > TimeSpan.Zero ? maxStale.ToString() : "off")})");
				else journal?.Invoke(stale ? $"XMKT {config.role} Data{config.dataStream} stale at {d1Time:yyyy-MM-dd HH:mm:ss} (last leg bar {(m_lastLegTime == DateTime.MinValue ? "none" : m_lastLegTime.ToString("yyyy-MM-dd HH:mm:ss"))})"
					: $"XMKT {config.role} Data{config.dataStream} available again at {d1Time:yyyy-MM-dd HH:mm:ss}");
				m_staleLogged = true; m_stale = stale;
			}
		}

		/// <summary>On the leg's own roll (unadjusted continuous): drop return history so the splice is never a return.</summary>
		public void OnRoll() { if (Configured) clearHistory(); }

		private void push(double x, double y)
		{
			if (m_rCount == m_rx.Length) { double ox = m_rx[m_rNext], oy = m_ry[m_rNext]; m_sx -= ox; m_sy -= oy; m_sxx -= ox * ox; m_syy -= oy * oy; m_sxy -= ox * oy; } else m_rCount++;
			m_rx[m_rNext] = x; m_ry[m_rNext] = y; m_sx += x; m_sy += y; m_sxx += x * x; m_syy += y * y; m_sxy += x * y; m_rNext = (m_rNext + 1) % m_rx.Length;
		}

		// ---- outputs (NaN when not Available or not enough samples) ----
		/// <summary>k-bar log return of the leg over its own last returnBars sampled bars.</summary>
		public double LogReturn { get { if (!Available || m_cCount < m_legC.Length) return double.NaN; return Math.Log(LastClose / m_legC[m_cNext % m_legC.Length]); } }
		/// <summary>Data1 log return over the same sample points.</summary>
		public double Data1LogReturn { get { if (!Available || m_cCount < m_d1C.Length) return double.NaN; int last = (m_cNext - 1 + m_d1C.Length) % m_d1C.Length; return Math.Log(m_d1C[last] / m_d1C[m_cNext % m_d1C.Length]); } }
		/// <summary>Rolling Pearson correlation of paired per-sample log returns (full window).</summary>
		public double Correlation { get { if (!Available || m_rCount < m_rx.Length) return double.NaN; double n = m_rCount, cxy = m_sxy - m_sx * m_sy / n, cxx = m_sxx - m_sx * m_sx / n, cyy = m_syy - m_sy * m_sy / n; return cxx > 0 && cyy > 0 ? cxy / Math.Sqrt(cxx * cyy) : double.NaN; } }
		/// <summary>Beta of Data1 returns on leg returns (cov / var(leg)): the hedge ratio for Data1 vs the leg.</summary>
		public double Beta { get { if (!Available || m_rCount < m_rx.Length) return double.NaN; double n = m_rCount, cxy = m_sxy - m_sx * m_sy / n, cxx = m_sxx - m_sx * m_sx / n; return cxx > 0 ? cxy / cxx : double.NaN; } }
		/// <summary>z of the log RS spread ln(leg / Data1) vs its rolling window (population sigma).</summary>
		public double RsZ { get { if (!Available || m_rsCount < m_rs.Length) return double.NaN; double mean = m_rsSum / m_rsCount, v = m_rs2Sum / m_rsCount - mean * mean; if (!(v > 0)) return double.NaN; return (m_rs[(m_rsNext - 1 + m_rs.Length) % m_rs.Length] - mean) / Math.Sqrt(v); } }
	}
}
