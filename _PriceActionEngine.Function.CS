// ============================================================================================
// rev 5 — C# 7.3 syntax + stub type check + tests/PriceActionTests pass off-MC; compile in the PowerLanguage .NET Editor still to do.
// Feature PRICEACTION (formerly M-SWING). SUPERSEDES _SwingEngine.Function.CS (rev 2, removed; the
// swing logic moved here unchanged in behaviour except where noted in the spec §1).
// Spec: /workspace/specs/missing-functionality-specs.md §1 (rev 3); review items R-H1..R-L6 from
// /workspace/specs/priceaction-improvements.md are referenced inline as [H1], [M3], ...
//
// Ported in spirit from github.com/ettiennehugo/PriceAction (user library, NOT modified): candle
// patterns, layered fractal pivots, HH/LH/HL/LL, double top/bottom, breakout — re-implemented with:
//  * closed-bar commit only (no repaint) [H3]; pivots immutable with confirmTime + per-layer sequence
//    numbers assigned at confirmation, never renumbered [H4]; older pivots never relabelled [H5]
//  * ring buffers keyed by the stream's CurrentBar, O(1) ATR (SMA of TR) and volume mean/sigma, no per-bar
//    allocation except one _Pivot object per confirmed pivot [H2][H9][M1][M2]
//  * roll handling for unadjusted continuous futures [M3]; validation that reports, never clamps [M4]
//  * read-only API [M8]; pure static candle classifier for unit tests [M9]; MC adapter Step(IInstrument) [M10]
// VPB-9 convention: plain internal sealed class in PowerLanguage.Function, class name = file name.
// LAZY: _CustomSignal creates it only when PriceActionConfig.enabled (protected property, no [Input]).
// ============================================================================================
using System;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	public enum EPivotMode { Fractal = 1, AtrZigZag = 2 }
	public enum EPivotTieRule { Strict = 0, LeftInclusive = 1 }          // [M6] Strict == PriceAction parity
	public enum EPivotLabel { None = 0, HH, LH, HL, LL }                  // ties -> LH / LL (documented) [M5]
	public enum EStructure { Unknown = 0, Up, Down, Range }

	/// <summary>Preset bundles (rev 4). Library = PriceAction-library parity (default). Futures = variant for tick-rounded
	/// futures: LeftInclusive ties (flat tops pivot) and volume norm from prior bars only. Used by _VolumeProfileFuturesBase.</summary>
	public enum EPriceActionPreset { Library = 0, Futures = 1 }

	/// <summary>Candle flags of one closed bar. [Flags] so "what changed" is explicit [L4].</summary>
	[Flags]
	public enum ECandle
	{
		None = 0,
		Bull = 1 << 0, Bear = 1 << 1, Doji = 1 << 2,
		BodyAtHigh = 1 << 3,   // small body in the top of the range, long lower tail (hammer). PriceAction "highPin" [M7]
		BodyAtLow = 1 << 4,    // small body in the bottom of the range, long upper tail (shooting star). PriceAction "lowPin"
		Inside = 1 << 5, Outside = 1 << 6, Engulfing = 1 << 7,
		BullReversal = 1 << 8, BearReversal = 1 << 9, Strong = 1 << 10,
		VolAboveAvg = 1 << 11, VolPlus1SD = 1 << 12, VolPlus2SD = 1 << 13, VolPlus3SD = 1 << 14,
		VolBelowAvg = 1 << 15, VolMinus1SD = 1 << 16, VolMinus2SD = 1 << 17, VolMinus3SD = 1 << 18,
		BreakoutUp = 1 << 19, BreakoutDown = 1 << 20
	}

	/// <summary>Protected-property configuration (no [Input]s). Immutable once Configure() succeeded [M4].</summary>
	public sealed class PriceActionConfig
	{
		public bool enabled = false;
		public int dataStream = 1;
		public EPivotMode pivotMode = EPivotMode.Fractal;
		public EPivotTieRule tieRule = EPivotTieRule.Strict;
		public int layers = 2;
		public int strength = 1;             // bars each side; 1 == PriceAction 3-bar fractal
		public int atrLength = 20;           // PriceAction DefaultAtrLength (SMA of TR)
		public int volumeLength = 20;        // PriceAction DefaultVolumeLength
		public bool volumeIncludesCurrent = true;   // PriceAction parity; false = norm from prior bars only
		public double atrMultiple = 1.5;     // AtrZigZag reversal threshold
		public double factorDoji = 0.1;      // PriceAction DefaultFactorDoji
		public double factorClose = 0.1;     // PriceAction DefaultFactorClose ("near" tolerance, x ATR)
		public double factorStrong = 1.0;    // PriceAction DefaultFactorStrong
		public double factorEpsilon = 0.05;  // PriceAction DefaultFactorEpsilon (body-at-extreme tolerance)
		public double equalFactor = 0.1;     // equal high/low tolerance x ATR (PriceAction uses FactorClose)
		public int lookbackPivots = 5;       // PriceAction PivotsToCheckForTopsBottoms
		public int minBreakoutLayer = 1;     // [H6] layers >= this are checked for breakouts
		public bool breakoutNeedsStrong = true;
		public int history = 50;             // pivots kept per layer
		public bool candles = true;          // candle classifier on/off (pivots still run)
		public EPriceActionPreset preset = EPriceActionPreset.Library;   // informational; set by ApplyPreset

		/// <summary>Applies a preset over the current values (only the fields the preset owns); returns this for chaining.</summary>
		public PriceActionConfig ApplyPreset(EPriceActionPreset p)
		{
			preset = p;
			switch (p)
			{
				case EPriceActionPreset.Library: tieRule = EPivotTieRule.Strict; volumeIncludesCurrent = true; break;
				case EPriceActionPreset.Futures: tieRule = EPivotTieRule.LeftInclusive; volumeIncludesCurrent = false; break;
			}
			return this;
		}
		/// <summary>New config with the Futures preset (still enabled=false until a consumer enables it).</summary>
		public static PriceActionConfig Futures() { return new PriceActionConfig().ApplyPreset(EPriceActionPreset.Futures); }
		/// <summary>New config with library-parity defaults.</summary>
		public static PriceActionConfig Library() { return new PriceActionConfig(); }

		/// <summary>Null when valid; else every bad value.</summary>
		public string Validate()
		{
			if (!enabled) return null;
			var e = new List<string>();
			if (dataStream < 1) e.Add($"dataStream={dataStream}");
			if (layers < 1) e.Add($"layers={layers}");
			if (strength < 1) e.Add($"strength={strength}");
			if (atrLength < 1) e.Add($"atrLength={atrLength}");
			if (volumeLength < 2) e.Add($"volumeLength={volumeLength}");
			if (pivotMode == EPivotMode.AtrZigZag && !(atrMultiple > 0)) e.Add($"atrMultiple={atrMultiple}");
			if (!(factorDoji > 0 && factorDoji <= 1)) e.Add($"factorDoji={factorDoji}");
			if (!(factorClose > 0 && factorClose < 1)) e.Add($"factorClose={factorClose}");
			if (!(factorStrong > 0)) e.Add($"factorStrong={factorStrong}");
			if (!(factorEpsilon > 0)) e.Add($"factorEpsilon={factorEpsilon}");
			if (equalFactor < 0) e.Add($"equalFactor={equalFactor}");
			if (lookbackPivots < 1) e.Add($"lookbackPivots={lookbackPivots}");
			if (minBreakoutLayer < 0 || minBreakoutLayer >= layers) e.Add($"minBreakoutLayer={minBreakoutLayer} (0..layers-1)");
			if (history < Math.Max(4, lookbackPivots + 1)) e.Add($"history={history} (< max(4, lookbackPivots+1))");
			return e.Count == 0 ? null : "PriceActionConfig invalid: " + string.Join(", ", e);
		}
	}

	/// <summary>Confirmed pivot. Immutable (readonly fields) [H4][H5][M8].</summary>
	public sealed class _Pivot
	{
		public readonly bool isHigh;
		public readonly int layer;
		public readonly long seq;              // per (layer, side) sequence, assigned at confirmation, never renumbered [H4]
		public readonly double price;
		public readonly DateTime extremeTime;
		public readonly int extremeBar;        // CurrentBar of the stream — never journal / compare across streams
		public readonly DateTime confirmTime;  // rules and journal use this
		public readonly int confirmBar;
		public readonly EPivotLabel label;
		public readonly bool isEqual;          // double top/bottom on THIS pivot only [H5]
		public readonly long equalToSeq;       // seq of the matched earlier pivot, -1 if none
		public readonly double legSizeAtr;
		public _Pivot(bool isHigh, int layer, long seq, double price, DateTime extremeTime, int extremeBar, DateTime confirmTime, int confirmBar,
			EPivotLabel label, bool isEqual, long equalToSeq, double legSizeAtr)
		{
			this.isHigh = isHigh; this.layer = layer; this.seq = seq; this.price = price; this.extremeTime = extremeTime; this.extremeBar = extremeBar;
			this.confirmTime = confirmTime; this.confirmBar = confirmBar; this.label = label; this.isEqual = isEqual; this.equalToSeq = equalToSeq; this.legSizeAtr = legSizeAtr;
		}
	}

	/// <summary>Fixed-capacity ring (oldest..newest), no allocation after construction [M2].</summary>
	internal sealed class _Ring<T>
	{
		private readonly T[] m_a; private int m_start, m_count;
		public _Ring(int capacity) { m_a = new T[capacity]; }
		public int Count { get { return m_count; } }
		public int Capacity { get { return m_a.Length; } }
		public void Clear() { m_start = 0; m_count = 0; }
		public void Add(T v) { if (m_count < m_a.Length) m_a[(m_start + m_count++) % m_a.Length] = v; else { m_a[m_start] = v; m_start = (m_start + 1) % m_a.Length; } }
		/// <summary>ago 0 = newest.</summary>
		public T Ago(int ago) { return m_a[(m_start + m_count - 1 - ago) % m_a.Length]; }
		public T Oldest() { return m_a[m_start]; }
	}

	internal struct _PaBar { public double o, h, l, c, v; public DateTime t; public int bar; }

	internal sealed class _PriceActionEngine
	{
		//configuration / sinks
		public PriceActionConfig config = new PriceActionConfig();
		public string label = "PA";
		public Action<string> trace;     // Debug (per-bar detail). Owner passes null unless TraceLevel >= Debug [L-trace cost]
		public Action<string> journal;   // ETraceLevel.Journal facts: pivots, breakouts, roll resets. Times only.
		public Action<string> warnOnce;
		public Func<double> volumeZOverride;   // optional external normaliser (M-TOD Volume z) [L3]; NaN -> internal stats

		//outputs for the last CLOSED bar
		public bool Configured { get; private set; }
		public bool IsWarm { get; private set; }                    // [L5]
		public double Atr { get { return IsWarm ? m_atr : double.NaN; } }
		public double VolumeMean { get; private set; } = double.NaN;
		public double VolumeSigma { get; private set; } = double.NaN;
		public ECandle Candle { get; private set; }
		public ECandle Changed { get; private set; }                // flags that differ from the previous bar's flags [L4]
		public bool NewPivotHigh { get; private set; }               // layer 0
		public bool NewPivotLow { get; private set; }
		public bool BosUp { get; private set; }                      // layer-0 close beyond last swing high
		public bool BosDown { get; private set; }
		public bool ChochUp { get; private set; }                    // BOS against the structure BEFORE the break
		public bool ChochDown { get; private set; }
		public DateTime LastBarTime { get; private set; }

		//state
		private _Ring<_PaBar> m_bars;                                // window = max(2*strength+1, 3) bars
		private _Ring<double> m_tr, m_vol;
		private double m_trSum, m_vSum, m_v2Sum, m_atr = double.NaN, m_prevClose = double.NaN;
		private bool m_skipNextTr;
		private _Ring<_Pivot>[] m_hi, m_lo;
		private long[] m_seqHi, m_seqLo;
		private long[] m_brokenHiSeq, m_brokenLoSeq;                 // last pivot seq per layer already broken [H6]
		private EStructure[] m_structure;
		private int m_lastClosedBar = int.MinValue;
		private ECandle m_prevCandle;
		private bool m_warned;
		// ZigZag (starts undecided)
		private int m_zzDir; private double m_zzHi = double.NaN, m_zzLo = double.NaN; private DateTime m_zzHiT, m_zzLoT; private int m_zzHiBar, m_zzLoBar;

		private static readonly IReadOnlyList<_Pivot> s_empty = new _Pivot[0];

		// ---------------------------------------------------------------- lifecycle
		/// <summary>Validate once in StartCalc; returns error text (owner throws). Config is frozen afterwards [M4].</summary>
		public string Configure()
		{
			string err = config.Validate();
			if (err != null || !config.enabled) { Configured = false; return err; }
			int L = config.layers;
			m_bars = new _Ring<_PaBar>(Math.Max(2 * config.strength + 1, 3));
			m_tr = new _Ring<double>(config.atrLength);
			m_vol = new _Ring<double>(config.volumeLength);
			m_hi = new _Ring<_Pivot>[L]; m_lo = new _Ring<_Pivot>[L];
			m_seqHi = new long[L]; m_seqLo = new long[L]; m_brokenHiSeq = new long[L]; m_brokenLoSeq = new long[L];
			m_structure = new EStructure[L];
			for (int i = 0; i < L; i++) { m_hi[i] = new _Ring<_Pivot>(config.history); m_lo[i] = new _Ring<_Pivot>(config.history); }
			Configured = true;
			Reset();
			return null;
		}

		/// <summary>Full reset of every field [H7].</summary>
		public void Reset()
		{
			if (!Configured) return;
			for (int i = 0; i < m_seqHi.Length; i++) { m_seqHi[i] = 0; m_seqLo[i] = 0; }   // before clearPivots: it copies seq into the broken markers
			m_bars.Clear(); resetIndicators(); clearPivots(); m_skipNextTr = false; m_warned = false;
			m_lastClosedBar = int.MinValue; m_prevCandle = ECandle.None; Candle = ECandle.None; Changed = ECandle.None;
			clearEventFlags(); LastBarTime = DateTime.MinValue;
		}

		/// <summary>Owner's VPF-4 roll detector fired on this (not yet stepped) bar [M3].</summary>
		public void OnRoll(DateTime barTime)
		{
			if (!Configured) return;
			m_bars.Clear(); resetIndicators(); clearPivots();          // sequence numbers continue (monotonic)
			m_skipNextTr = true;
			journal?.Invoke($"{label} roll reset at {barTime:yyyy-MM-dd HH:mm:ss}");
		}

		// ---------------------------------------------------------------- MC adapter [M10]
		/// <summary>Call from owner's CalcBar. Commits only on bars.Status == Close [H3]; dedupe by CurrentBar.</summary>
		public void Step(IInstrument bars)
		{
			if (!Configured || bars == null) return;
			if (bars.Status != EBarState.Close) return;
			Add(bars.CurrentBar, bars.Time[0], bars.Open[0], bars.High[0], bars.Low[0], bars.Close[0], bars.Volume[0]);
		}

		/// <summary>MC-independent entry point (unit tests). Bars must arrive closed, with strictly increasing barNumber [H9].</summary>
		public bool Add(int barNumber, DateTime time, double o, double h, double l, double c, double v)
		{
			if (!Configured) return false;
			if (barNumber == m_lastClosedBar) return false;                      // same closed bar seen again
			if (barNumber < m_lastClosedBar) { warn($"{label}: out-of-order bar {barNumber} < {m_lastClosedBar} at {time:yyyy-MM-dd HH:mm:ss} ignored"); return false; }
			m_lastClosedBar = barNumber; LastBarTime = time;
			clearEventFlags();

			updateIndicators(h, l, c, v);
			m_bars.Add(new _PaBar { o = o, h = h, l = l, c = c, v = v, t = time, bar = barNumber });
			if (!IsWarm) { Candle = ECandle.None; return true; }

			if (config.candles && m_bars.Count >= 2) Candle = ClassifyCandle(m_bars.Ago(0), m_bars.Ago(1), m_atr, config) | volumeFlags(v);
			else Candle = ECandle.None;

			if (config.pivotMode == EPivotMode.Fractal) stepFractal(); else stepZigZag();
			detectBreakouts(c, h, l, time);
			detectBos(c, time);

			Changed = Candle ^ m_prevCandle; m_prevCandle = Candle;
			if (trace != null && Candle != ECandle.None) trace($"{label} {time:yyyy-MM-dd HH:mm:ss} {Candle}");
			return true;
		}

		// ---------------------------------------------------------------- read-only queries [M8]
		public _Pivot LastHigh(int layer = 0) { return pivot(layer, true, 0); }
		public _Pivot LastLow(int layer = 0) { return pivot(layer, false, 0); }
		public _Pivot High(int layer, int ago) { return pivot(layer, true, ago); }
		public _Pivot Low(int layer, int ago) { return pivot(layer, false, ago); }
		public int PivotCount(int layer, bool isHigh) { return layerOk(layer) ? (isHigh ? m_hi : m_lo)[layer].Count : 0; }
		public EStructure Structure(int layer = 0) { return layerOk(layer) ? m_structure[layer] : EStructure.Unknown; }
		public bool DoubleTop(int layer = 0) { var p = pivot(layer, true, 0); return p != null && p.isEqual; }
		public bool DoubleBottom(int layer = 0) { var p = pivot(layer, false, 0); return p != null && p.isEqual; }
		public bool Has(ECandle f) { return (Candle & f) == f; }

		// ---------------------------------------------------------------- pure candle classifier [M7][M9]
		public const double SMALL_BODY_RATIO = 1.0 / 3.0;   // body < 1/3 of range for BodyAtHigh/Low (PriceAction 0.3333333334)
		public const double TAIL_MIN_RATIO = 1.0 / 3.0;     // Brooks p.89 rule 2: reversal tail 1/3 .. 1/2 of bar height
		public const double TAIL_MAX_RATIO = 1.0 / 2.0;

		/// <summary>Stateless classification of bar b given prior bar p and ATR. Deterministic, unit-testable.</summary>
		internal static ECandle ClassifyCandle(_PaBar b, _PaBar p, double atr, PriceActionConfig k)
		{
			ECandle f = ECandle.None;
			double range = b.h - b.l, body = Math.Abs(b.o - b.c);
			double near = k.factorClose * atr, eps = k.factorEpsilon * atr;
			bool bull = b.c > b.o, bear = b.c < b.o;
			if (bull) f |= ECandle.Bull;
			if (bear) f |= ECandle.Bear;
			bool doji = body <= k.factorDoji * atr;
			if (doji) f |= ECandle.Doji;
			if (range > 0 && body / range < SMALL_BODY_RATIO)
			{
				double bodyTop = Math.Max(b.o, b.c), bodyBottom = Math.Min(b.o, b.c);
				if (b.h - bodyTop < eps) f |= ECandle.BodyAtHigh;
				if (bodyBottom - b.l < eps) f |= ECandle.BodyAtLow;
			}
			if (b.h < p.h && b.l > p.l) f |= ECandle.Inside;
			if (b.h > p.h && b.l < p.l) f |= ECandle.Outside;
			// engulfing (PriceAction rule: body engulfs prior RANGE)
			if (bull && b.o < p.l && b.c > p.h) f |= ECandle.Engulfing;
			if (bear && b.c < p.l && b.o > p.h) f |= ECandle.Engulfing;
			// reversal: Brooks, Trading Price Action Trends p.89; rules 1, 2, 5 implemented (3, 4 not); >= 2 rules
			if (!doji && range > 0)
			{
				bool pBear = p.c < p.o, pBull = p.c > p.o;
				if (pBear && bull)
				{
					int r = 0;
					if (b.o <= p.c + near && b.c > b.o && b.c > p.c) r++;                                            // 1 open near/below prior close, close above it
					double lowerTail = b.o - b.l, upperTail = b.h - b.c;
					if (lowerTail >= TAIL_MIN_RATIO * range && lowerTail <= TAIL_MAX_RATIO * range && upperTail <= near) r++;   // 2 [M7 corrected]
					if (b.c > p.h) r++;                                                                             // 5 close above prior high
					if (r >= 2) f |= ECandle.BullReversal;
				}
				if (pBull && bear)
				{
					int r = 0;
					if (b.o >= p.c - near && b.c < p.c - near) r++;                                                 // 1 open near/above prior close, close WELL below it [M7 corrected]
					double upperTail = b.h - b.o, lowerTail = b.c - b.l;
					if (upperTail >= TAIL_MIN_RATIO * range && upperTail <= TAIL_MAX_RATIO * range && lowerTail <= near) r++;
					if (b.c < p.l) r++;
					if (r >= 2) f |= ECandle.BearReversal;
				}
			}
			// strong: body >= factorStrong*ATR, opens near one extreme, closes near the other
			if (body >= k.factorStrong * atr)
			{
				if (bull && b.o - b.l <= near && b.h - b.c <= near) f |= ECandle.Strong;
				if (bear && b.h - b.o <= near && b.c - b.l <= near) f |= ECandle.Strong;
			}
			return f;
		}

		// ---------------------------------------------------------------- internals
		private void warn(string s) { if (!m_warned) { m_warned = true; warnOnce?.Invoke(s); } }
		private bool layerOk(int layer) { if (Configured && layer >= 0 && layer < m_hi.Length) return true; warn($"{label}: query before Configure or bad layer {layer}"); return false; }
		private void clearEventFlags() { NewPivotHigh = NewPivotLow = BosUp = BosDown = ChochUp = ChochDown = false; }

		private void resetIndicators()
		{
			m_tr.Clear(); m_vol.Clear(); m_trSum = m_vSum = m_v2Sum = 0; m_atr = double.NaN; m_prevClose = double.NaN;
			IsWarm = false; VolumeMean = VolumeSigma = double.NaN;
		}

		private void clearPivots()
		{
			for (int i = 0; i < m_hi.Length; i++) { m_hi[i].Clear(); m_lo[i].Clear(); m_structure[i] = EStructure.Unknown; m_brokenHiSeq[i] = m_seqHi[i]; m_brokenLoSeq[i] = m_seqLo[i]; }
			m_bosHiSeq = m_seqHi.Length > 0 ? m_seqHi[0] : 0; m_bosLoSeq = m_seqLo.Length > 0 ? m_seqLo[0] : 0;   // pre-roll / pre-reset pivots never break
			m_zzDir = 0; m_zzHi = m_zzLo = double.NaN;
		}

		/// <summary>O(1): ATR = SMA of TR; volume mean / population sigma from running sums [M1].</summary>
		private void updateIndicators(double h, double l, double c, double v)
		{
			double tr = (double.IsNaN(m_prevClose) || m_skipNextTr) ? h - l : Math.Max(h, m_prevClose) - Math.Min(l, m_prevClose);
			m_skipNextTr = false; m_prevClose = c;
			if (m_tr.Count == m_tr.Capacity) m_trSum -= m_tr.Oldest();
			m_tr.Add(tr); m_trSum += tr; m_atr = m_trSum / m_tr.Count;

			if (!config.volumeIncludesCurrent) setVolStats();          // norm from prior bars only
			if (m_vol.Count == m_vol.Capacity) { double o = m_vol.Oldest(); m_vSum -= o; m_v2Sum -= o * o; }
			m_vol.Add(v); m_vSum += v; m_v2Sum += v * v;
			if (config.volumeIncludesCurrent) setVolStats();
			IsWarm = m_tr.Count == m_tr.Capacity && m_vol.Count == m_vol.Capacity;
		}

		private void setVolStats()
		{
			int n = m_vol.Count; if (n < 2) { VolumeMean = VolumeSigma = double.NaN; return; }
			double mean = m_vSum / n, var = m_v2Sum / n - mean * mean;
			VolumeMean = mean; VolumeSigma = Math.Sqrt(var > 0 ? var : 0);   // population sigma, PriceAction parity [L3]
		}

		private ECandle volumeFlags(double v)
		{
			double z = volumeZOverride != null ? volumeZOverride() : double.NaN;
			if (double.IsNaN(z)) { if (!(VolumeSigma > 0)) return ECandle.None; z = (v - VolumeMean) / VolumeSigma; }
			ECandle f = ECandle.None;
			if (z > 0) f |= ECandle.VolAboveAvg; if (z > 1) f |= ECandle.VolPlus1SD; if (z > 2) f |= ECandle.VolPlus2SD; if (z > 3) f |= ECandle.VolPlus3SD;
			if (z < 0) f |= ECandle.VolBelowAvg; if (z < -1) f |= ECandle.VolMinus1SD; if (z < -2) f |= ECandle.VolMinus2SD; if (z < -3) f |= ECandle.VolMinus3SD;
			return f;
		}

		private bool beats(double mid, double left, double right, bool high)
		{
			if (config.tieRule == EPivotTieRule.LeftInclusive) return high ? (mid >= left && mid > right) : (mid <= left && mid < right);
			return high ? (mid > left && mid > right) : (mid < left && mid < right);   // strict: flat tops never pivot [M6]
		}

		private void stepFractal()
		{
			int s = config.strength, need = 2 * s + 1;
			if (m_bars.Count < need) return;
			_PaBar mid = m_bars.Ago(s), now = m_bars.Ago(0);
			bool ph = true, pl = true;
			for (int k = 1; k <= s; k++)
			{
				_PaBar left = m_bars.Ago(s + k), right = m_bars.Ago(s - k);
				ph &= beats(mid.h, left.h, right.h, true);
				pl &= beats(mid.l, left.l, right.l, false);
			}
			if (ph) { addPivot(0, true, mid.h, mid.t, mid.bar, now.t, now.bar); NewPivotHigh = true; }
			if (pl) { addPivot(0, false, mid.l, mid.t, mid.bar, now.t, now.bar); NewPivotLow = true; }
			if (ph || pl) promoteAll(now.t, now.bar);
		}

		private void stepZigZag()
		{
			_PaBar b = m_bars.Ago(0); double thr = config.atrMultiple * m_atr;
			if (m_zzDir >= 0 && (double.IsNaN(m_zzHi) || b.h > m_zzHi)) { m_zzHi = b.h; m_zzHiT = b.t; m_zzHiBar = b.bar; }
			if (m_zzDir <= 0 && (double.IsNaN(m_zzLo) || b.l < m_zzLo)) { m_zzLo = b.l; m_zzLoT = b.t; m_zzLoBar = b.bar; }
			if (m_zzDir == 0)
			{   // undecided: direction picked by the first >= thr move; nothing confirmed yet
				if (b.h - m_zzLo >= thr && m_zzLoBar < b.bar) { m_zzDir = 1; m_zzHi = b.h; m_zzHiT = b.t; m_zzHiBar = b.bar; }
				else if (m_zzHi - b.l >= thr && m_zzHiBar < b.bar) { m_zzDir = -1; m_zzLo = b.l; m_zzLoT = b.t; m_zzLoBar = b.bar; }
				return;
			}
			if (m_zzDir > 0 && m_zzHi - b.l >= thr) { addPivot(0, true, m_zzHi, m_zzHiT, m_zzHiBar, b.t, b.bar); NewPivotHigh = true; m_zzDir = -1; m_zzLo = b.l; m_zzLoT = b.t; m_zzLoBar = b.bar; }
			else if (m_zzDir < 0 && b.h - m_zzLo >= thr) { addPivot(0, false, m_zzLo, m_zzLoT, m_zzLoBar, b.t, b.bar); NewPivotLow = true; m_zzDir = 1; m_zzHi = b.h; m_zzHiT = b.t; m_zzHiBar = b.bar; }
			if (NewPivotHigh || NewPivotLow) promoteAll(b.t, b.bar);
		}

		private void promoteAll(DateTime t, int bar)
		{
			for (int layer = 1; layer < config.layers; layer++) { promote(layer, true, t, bar); promote(layer, false, t, bar); }
		}

		/// <summary>Layer k pivot = middle of the last three confirmed layer k-1 same-side pivots. Confirmed together with the newest of them (no lookahead).</summary>
		private void promote(int layer, bool isHigh, DateTime t, int bar)
		{
			_Pivot c = pivot(layer - 1, isHigh, 0), m = pivot(layer - 1, isHigh, 1), p = pivot(layer - 1, isHigh, 2);
			if (c == null || m == null || p == null) return;
			if (!beats(m.price, p.price, c.price, isHigh)) return;
			var prev = pivot(layer, isHigh, 0);
			if (prev != null && prev.extremeTime == m.extremeTime) return;
			addPivot(layer, isHigh, m.price, m.extremeTime, m.extremeBar, t, bar);
		}

		private void addPivot(int layer, bool isHigh, double price, DateTime extT, int extBar, DateTime confT, int confBar)
		{
			_Pivot prevSame = pivot(layer, isHigh, 0), prevOpp = pivot(layer, !isHigh, 0);
			EPivotLabel lbl = EPivotLabel.None;
			if (prevSame != null) lbl = isHigh ? (price > prevSame.price ? EPivotLabel.HH : EPivotLabel.LH) : (price > prevSame.price ? EPivotLabel.HL : EPivotLabel.LL);
			bool eq = false; long eqSeq = -1; double tol = config.equalFactor * m_atr;
			for (int ago = 0; ago < config.lookbackPivots; ago++)
			{
				var q = pivot(layer, isHigh, ago); if (q == null) break;
				if (Math.Abs(price - q.price) < tol) { eq = true; eqSeq = q.seq; break; }   // older pivot NOT modified [H5]
			}
			long seq = isHigh ? ++m_seqHi[layer] : ++m_seqLo[layer];
			double leg = prevOpp != null && m_atr > 0 ? Math.Abs(price - prevOpp.price) / m_atr : double.NaN;
			var pv = new _Pivot(isHigh, layer, seq, price, extT, extBar, confT, confBar, lbl, eq, eqSeq, leg);
			(isHigh ? m_hi : m_lo)[layer].Add(pv);
			updateStructure(layer);
			journal?.Invoke($"{label} L{layer} {(isHigh ? "PivotHigh" : "PivotLow")} #{seq} {price} {lbl}{(eq ? " EQ#" + eqSeq : "")} ext={extT:yyyy-MM-dd HH:mm:ss} conf={confT:yyyy-MM-dd HH:mm:ss}");
		}

		private void updateStructure(int layer)
		{
			_Pivot h = pivot(layer, true, 0), l = pivot(layer, false, 0);
			if (h == null || l == null) return;
			m_structure[layer] = (h.label == EPivotLabel.HH && l.label == EPivotLabel.HL) ? EStructure.Up
				: (h.label == EPivotLabel.LH && l.label == EPivotLabel.LL) ? EStructure.Down : EStructure.Range;
		}

		/// <summary>[H6] Close beyond the last UNBROKEN pivot of any layer >= minBreakoutLayer; each pivot breaks once.</summary>
		private void detectBreakouts(double c, double h, double l, DateTime t)
		{
			if (config.breakoutNeedsStrong && (Candle & ECandle.Strong) == 0) return;
			for (int layer = config.layers - 1; layer >= config.minBreakoutLayer; layer--)
			{
				var ph = pivot(layer, true, 0);
				if (ph != null && ph.seq > m_brokenHiSeq[layer] && c > ph.price && (Candle & ECandle.Bull) != 0)
				{ m_brokenHiSeq[layer] = ph.seq; Candle |= ECandle.BreakoutUp; journal?.Invoke($"{label} BreakoutUp L{layer} of #{ph.seq} {ph.price} at {t:yyyy-MM-dd HH:mm:ss}"); break; }
				var pl = pivot(layer, false, 0);
				if (pl != null && pl.seq > m_brokenLoSeq[layer] && c < pl.price && (Candle & ECandle.Bear) != 0)
				{ m_brokenLoSeq[layer] = pl.seq; Candle |= ECandle.BreakoutDown; journal?.Invoke($"{label} BreakoutDown L{layer} of #{pl.seq} {pl.price} at {t:yyyy-MM-dd HH:mm:ss}"); break; }
			}
		}

		/// <summary>Layer-0 BOS (any bar, not only strong); CHoCH uses the structure before the break.</summary>
		private long m_bosHiSeq, m_bosLoSeq;
		private void detectBos(double c, DateTime t)
		{
			EStructure before = m_structure[0];
			var ph = pivot(0, true, 0); var pl = pivot(0, false, 0);
			if (ph != null && ph.seq > m_bosHiSeq && c > ph.price) { m_bosHiSeq = ph.seq; BosUp = true; ChochUp = before == EStructure.Down; }
			if (pl != null && pl.seq > m_bosLoSeq && c < pl.price) { m_bosLoSeq = pl.seq; BosDown = true; ChochDown = before == EStructure.Up; }
			if ((BosUp || BosDown) && journal != null) journal($"{label} BOS {(BosUp ? "up" : "down")}{(ChochUp || ChochDown ? " CHoCH" : "")} at {t:yyyy-MM-dd HH:mm:ss}");
		}

		private _Pivot pivot(int layer, bool isHigh, int ago)
		{
			if (!Configured || layer < 0 || layer >= m_hi.Length) return null;
			var r = (isHigh ? m_hi : m_lo)[layer];
			return ago >= 0 && ago < r.Count ? r.Ago(ago) : null;
		}
	}
}
