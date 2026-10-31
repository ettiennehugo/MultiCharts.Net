using ATCenterProxy.interop;
using PowerLanguage.Function;
using static PowerLanguage.Function._VpHistogram;   // VPB-9: histogram helpers live in _VpHistogram.Function.CS
using PowerLanguage.VolumeProfile;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Base class for volume profile analysis signals, implements logic to determine things like HVN, LVN and detection of different
	/// confluence zones with directional enumeration, weights associated with each zone and ranking of the zones in terms of highest probability
	/// trade zones. A lot of information from Volume Profile and Order Flow book chapter 3.6.
	///
	/// VWAP is computed inside this class (no FunctionSeries / _VWAP dependency).
	/// Three slots. Set a slot to Disabled to skip it entirely.
	/// Each slot has its own data-stream input (VwapDataStream / Vwap2DataStream /
	/// Vwap3DataStream). 1 = Data1 on the chart, 2 = Data2, and so on.
	/// AdaptiveFromDate reads VwapFromDate / Vwap2FromDate / Vwap3FromDate as strings
	/// ("yyyy-MM-dd" or "yyyy-MM-dd HH:mm[:ss]").
	/// </summary>
	public abstract class _VolumeProfileBase : _CustomSignal
	{
		//constants
		public const double DEFAULT_HVN_PERCENTILE = 0.8;  // Default percentile of prices relative to POC considered high volume node prices.
		public const double DEFAULT_LVN_PERCENTILE = 0.2;  // Default percentile of prices relative to POC considered low volume node prices.
		public const int DEFAULT_SMOOTH_TICKS = 3;  // Default smoothing length used for ticks.
		public const int DEFAULT_MIN_NODE_TICKS = 3;  // min HVN/LVN high-low span in ticks
		public const double PRICE_EPSILON = 1e-12;  // tolerance for price-span compares; about 1 ULP near 7800, far below a tick
		public const double DEFAULT_MAX_LVN_PCT_OF_RANGE = 0.12; // default of MaxLvnFractionOfRange: an LVN wider than 12% of the profile range is dropped (one LVN must not swallow the profile)
		public const int DEFAULT_VWAP_LENGTH = 20;
		public const int DEFAULT_CONFLUENCE_LOOKBACK = 4;
		public const int DEFAULT_CONFLUENCE_ALIGN_TICKS = 4;
		public const int DEFAULT_CONFLUENCE_TOP_COUNT = 5;
		public const int DEFAULT_CONFLUENCE_TARGET_CLEAR_TICKS = 1;
		public const double DEFAULT_CONFLUENCE_STREAM_WEIGHT_1 = 1.00;  // execution / primary stream
		public const double DEFAULT_CONFLUENCE_STREAM_WEIGHT_2 = 1.25;  // higher timeframe
		public const double DEFAULT_CONFLUENCE_STREAM_WEIGHT_3 = 1.50;  // highest timeframe
		public const double DEFAULT_CONFLUENCE_NAKED_MULTIPLIER = 1.15; // naked modifies the POC credit; it is not a second source
		public const double DEFAULT_CONFLUENCE_REFINEMENT = 0.25;       // one nested child per parent already in the zone
		public const double DEFAULT_CONFLUENCE_OVERLAY_FACTOR = 0.50;   // VWAP context only; never acceptance or rejection
		public const double DEFAULT_CONFLUENCE_SHARE_PIVOT = 0.15;      // volume-share factor pivot
		public const double DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE = 0.95; // percentile used to determine whether volume on a node it exceptional
		public const int MAX_DENSE_ROWS = 20000;  // maximum dense rows in a volume profile to use for LVN detection
		private const int MAX_RESOLVED_CACHE = 4096;   // itemForBar memo entries per stream before it is cleared
		internal const double FALLBACK_TICK = 0.01;     // tickSize() when the symbol reports no usable MinMove/PriceScale
		internal const int CONFLUENCE_NODES_PER_PROFILE = 3;         // HVNs and LVNs (each, top by volume) taken from each lookback profile
		internal const double SHARE_FACTOR_MIN = 0.5;                // volume-share factor clamp
		internal const double SHARE_FACTOR_MAX = 1.5;
		internal const double LEVEL_MATCH_TICK_FRACTION = 0.51;      // > half a tick: neighbouring levels never match
		public const int DEFAULT_LVN_HVN_MAX_GAP_TICKS = 4;   // an LVN edge must be within this many ticks of an HVN edge

		//types
		/// <summary>
		/// User VWAP window. Disabled means the slot is not stepped and not stamped onto VpExtra.
		/// </summary>
		public enum EVwapType
		{
			Disabled = 0,          // skip — not computed, not used
			Session = 1,           // reset on first bar of the session
			Length = 2,            // rolling last VwapLength / Vwap2Length / Vwap3Length bars
			AdaptiveFromDate = 3,  // accumulate from VwapFromDate / Vwap2FromDate / Vwap3FromDate
			// M-AVWAP (missing-functionality spec rev 4 §2): single-anchor calendar resets. Values 0-3 unchanged.
			Week = 4,              // reset on the first bar whose exchange-session date is in a new (Monday-start) week
			Month = 5,             // ... in a new month
			Quarter = 6,           // ... in a new calendar quarter
			RollDate = 7           // reset on the VPF-4 roll bar (futures base; rollOn(ds) on the slot's stream, Data n uses the Data1 root; RollDetection must be on)
		}

		/// <summary>
		/// Price level in the volume profile - Multicharts .Net ILevel interface looks like a pain to work with.
		/// </summary>
		internal struct Row
		{
			public double price;
			public double volume;
		}

		/// <summary>
		/// Node type to store data for a volume profile node segment.
		/// </summary>
		public struct Node
		{
			public double low;
			public double high;
			public double peak;       // price of the local max / min
			public double peakVol;
			public double strength;   // peakVol / pocVol, 0..1
			public bool isPoc;
		};

		/// <summary>
		/// Volume Profile Key object used to locate extra data stored per volume profile.
		/// Keyed by data stream + start bar only. endBar walks forward while a
		/// profile is still forming (every-N-bar VP on bar 0 / 13 / 26 …), so it
		/// must not be part of equality or extras disappear on window boundaries.
		/// </summary>
		private struct VpKey : IEquatable<VpKey>
		{
			public readonly int datastream;
			public readonly int startBar;
			public VpKey(int datastream, IProfile p)
			{
				this.datastream = datastream;
				startBar = -1;
				if (p == null) return;
				try
				{
					startBar = toChartBar(p.BarsInterval.First);
				}
				catch { }
			}
			public VpKey(int datastream, int startBar)
			{
				this.datastream = datastream;
				this.startBar = startBar;
			}
			public bool Equals(VpKey o)
			{
				return datastream == o.datastream && startBar == o.startBar;
			}
			public override bool Equals(object o) { return o is VpKey k && Equals(k); }
			public override int GetHashCode() { return unchecked(datastream ^ (startBar * 397)); }   // hash: wrap intended
		};

		/// <summary>
		/// Extra data stored for a volume profile.
		/// </summary>
		public class VpExtra
		{
			public int dataStream;
			public int startBar;  // 1-based Status Bar # / FullSymbolData.Current of BarsInterval.First
			public int endBar;    // 1-based Status Bar # of BarsInterval.Second (walks while developing)
			public double open;   // open of the profile's first bar
			public double high;   // highest high across the profile window
			public double low;    // lowest low across the profile window
			public double close;  // close of the profile's last bar (developing bar while the window is open)
			public double poc;    // Point of control for the volume profile.
			public double pocVol; // Volume at the point of control.
			public bool pocComplete;   // window closed relative to the current calc bar, and poc > 0 (VPB-5: independent of TrackNakedPoc)
			public bool pocStale;      // VPB-4: developing window whose as-of POC could not be read (poc is not the as-of POC)
			public bool isDeveloping;  // VPB-1: set by DevelopingExtra / the Own engine for a window still open at the calc bar
			public bool approximate;   // VPB-1: Own profile built from bars (BarUniform / BarTypical or a non-1-tick source)
			public bool isOwn;         // VPB-1 / VPB-7: built by this base (Own engine or composite), not read from MultiCharts
			public bool isPartial;     // VPB-1: Own profile whose window began before the first readable bar
			public bool crossesRoll;   // VPF-4: the window spans a continuous-contract roll splice (set by the futures base)
			public bool pocNaked;      // pocComplete and no later bar has traded poc, as of the current calc bar
			public int pocTouchedBar;  // 1-based first post-close bar whose range covered poc; 0 if none at or before current

			// Scan / build cursors. Not trading state. IsNakedPoc still derives from
			// pocComplete, poc, endBar, and pocTouchedBar — there is no per-bar naked flag.
			// lastNakedScanBar lets the next naked scan start after the last bar already
			// checked, instead of re-reading endBar+1..current every CalcBar.
			// ohlc* remembers which window has been folded so a developing profile only
			// reads new bars, and a MaxBarsBack miss does not wipe a good snapshot.
			// nodesBuilt* freezes a closed histogram until the stream generation bumps
			// (EChanged / StartCalc). A developing profile never freezes.
			internal int lastNakedScanBar; // last 1-based bar included in the naked scan; 0 if never
			internal bool nakedTracked;    // VPB-5: stamped with TrackNakedPoc on; IsNakedPoc is false otherwise
			internal int ohlcStartBar;      // startBar the OHLC cursor belongs to; mismatch resets the fold
			internal int ohlcFoldedThrough; // last 1-based bar folded into high/low
			internal int ohlcEndBar;        // endBar last fully folded; equal endBar skips a closed rescan
			internal bool nodesBuilt;
			internal int nodesBuiltForStart;
			internal int nodesBuiltGeneration = -1;
			internal int filledAtBar = -1;          // stream calc bar of the last developing fill
			internal bool filledAtClose;            // that fill saw the bar's final state
			internal int filledGeneration = -1;     // stream generation of the last developing fill

			internal bool inClosedSet;          // levels already merged into the stream's closed reference
			internal int pctStampedFill = -1;   // filledAtBar of the last percentile stamp for a developing extra
			internal int pctStampedClosedGen = -1;

			/// <summary>
			/// True when this completed POC was still naked as of <paramref name="asOfBar"/>.
			/// asOfBar is 1-based (Status Bar #). A developing profile is never naked.
			/// A stored touch after asOfBar does not mitigate the historical question.
			/// </summary>
			public bool IsNakedPoc(int asOfBar)
			{
				if (!nakedTracked || !pocComplete || poc <= 0 || asOfBar <= endBar) return false;
				return pocTouchedBar == 0 || pocTouchedBar > asOfBar;
			}
			public double vah;  // Value area high.
			public double val;  // Value area low.
			public double vwap1;         // slot 1 snapshot (0 when Disabled / not yet valid)
			public double vwap2;        // slot 2 snapshot
			public double vwap3;        // slot 3 snapshot
			public double vwap1Sigma;    // slot 1 σ from the running window (0 when unused)
			public double vwap2Sigma;
			public double vwap3Sigma;
			public int vwap1Stream;      // chart data stream slot 1 was built from (0 = unused)
			public int vwap2Stream;
			public int vwap3Stream;
			public readonly List<Node> hvns = new List<Node>(); // List of high volume nodes for a volume profile.
			public readonly List<Node> lvns = new List<Node>(); // List of low volume nodes for a volume profile.

			/// <summary>
			/// Every ILevel on this profile with raw Total / Ask / Bid volume and
			/// empirical percentiles vs every ILevel on every IProfile on this
			/// extra's data stream only.
			/// </summary>
			public readonly List<LevelVol> levels = new List<LevelVol>();
			public double maxTotalPercentile;
			public double maxAskPercentile;
			public double maxBidPercentile;

			public bool HasExceptionalVolume(double minPercentile)
			{
				if (minPercentile <= 0) minPercentile = DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE;
				return maxTotalPercentile >= minPercentile
					|| maxAskPercentile >= minPercentile
					|| maxBidPercentile >= minPercentile;
			}

			public bool HasExceptionalVolume()
			{
				return HasExceptionalVolume(DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE);
			}

			public List<LevelVol> ExceptionalLevels(double minPercentile)
			{
				if (minPercentile <= 0) minPercentile = DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE;
				List<LevelVol> hit = new List<LevelVol>();
				for (int i = 0; i < levels.Count; i++)
				{
					LevelVol lv = levels[i];
					if (lv.totalPercentile >= minPercentile
						|| lv.askPercentile >= minPercentile
						|| lv.bidPercentile >= minPercentile)
						hit.Add(lv);
				}
				return hit;
			}

			public LevelVol PeakTotalLevel()
			{
				LevelVol best = new LevelVol();
				for (int i = 0; i < levels.Count; i++)
				{
					if (levels[i].totalPercentile > best.totalPercentile)
						best = levels[i];
				}
				return best;
			}
		};

		/// <summary>
		/// One ILevel snapshot plus empirical volume percentiles.
		/// Percentiles are the CDF of this level vs every ILevel on every IProfile
		/// on the same data stream only. 1.0 = largest volume on that stream.
		/// </summary>
		public struct LevelVol
		{
			public double price;
			public double total;                 // same as ILevel.TotalValue (ask+bid fallback)
			public double ask;                   // same as ILevel.AskTradedValue
			public double bid;                   // same as ILevel.BidTradedValue
			public double totalPercentile;
			public double askPercentile;
			public double bidPercentile;

			public bool IsExceptional(double minPercentile)
			{
				if (minPercentile <= 0) minPercentile = DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE;
				return totalPercentile >= minPercentile
					|| askPercentile >= minPercentile
					|| bidPercentile >= minPercentile;
			}
		}

		/// <summary>
		/// Level kind that can vote for a confluence zone.
		/// </summary>
		public enum EConfluenceKind
		{
			Poc = 0,
			Vah = 1,
			Val = 2,
			Hvn = 3,
			Vwap1 = 4,
			Vwap2 = 5,
			Vwap3 = 6,
			NakedPoc = 7,
			Lvn = 8
		}

		/// <summary>
		/// Role of a zone from the labels that actually received credit.
		/// Mixed means acceptance and rejection both scored; do not average them into a trade.
		/// </summary>
		public enum EConfluenceRole
		{
			Acceptance = 0,
			Rejection = 1,
			Magnet = 2,
			Mixed = 3
		}

		/// <summary>
		/// How a confluence zone should be used (VPB-26, was a "target"/"entry"/"conflict" string). Derived from role.
		/// </summary>
		public enum ESuggestedUse
		{
			Target = 0,
			Entry = 1,
			Conflict = 2
		}

		/// <summary>
		/// Zone versus the highest-timeframe value area on this rebuild.
		/// </summary>
		public enum EConfluenceLocation
		{
			Unknown = 0,
			Premium = 1,
			Discount = 2,
			InsideValue = 3
		}

		/// <summary>
		/// Trade direction used when asking confluence zones for targets.
		/// Bullish = magnets above price. Bearish = magnets below price.
		/// </summary>
		public enum EVpDirection
		{
			Bearish = -1,
			Bullish = 1
		}

		/// <summary>
		/// One contributing level inside a confluence zone.
		/// </summary>
		public struct ConfluenceHit
		{
			public int dataStream;
			public int profileStartBar;   // -1 when the hit is a live VWAP slot, not a profile
			public int profileAge;        // 0 = most recent profile on that stream
			public EConfluenceKind kind;
			public double price;
			public double weight;          // credit awarded; 0 when the label was refused
			public string sourceVolumeId;  // one id per volume mass; a developing profile uses its parent id
			public string absorbedLabels;  // same-source labels that did not score
			public double credit;
			public double volumeShare;
			public bool naked;
			public bool independent;       // full credit. A refinement is not an independent source.
			public bool developing;        // PR #47: the label comes from a developing (incomplete) profile
			public double completeness;    // dev-weight spec: w_complete applied to credit (1 when completed, an overlay, or weighting Off)
			public double rawCredit;       // dev-weight spec: credit before the completeness weight (= credit when no weight applied)
		}

		/// <summary>
		/// A label that landed in the zone and was refused credit.
		/// </summary>
		public class ConfluenceDrop
		{
			public int dataStream;
			public int profileStartBar;
			public EConfluenceKind kind;
			public double price;
			public string sourceVolumeId;
			public string reason;
		}

		/// <summary>
		/// Cluster of aligned levels. Score counts each volume mass once.
		/// </summary>
		public class ConfluenceZone
		{
			public double low;
			public double high;
			public double center;          // anchor of the highest-credit surviving label
			public double rawScore;        // acceptance + rejection; overlays are not included
			public double zScore;
			public double anchor;
			public double acceptanceScore;
			public double rejectionScore;
			public double contextScore;    // VWAP overlays only
			public int independentSources; // PR #47 (V5): distinct independent volume masses (stream, profile window), not survivors
			public EConfluenceRole role;
			public EConfluenceLocation location;
			public ESuggestedUse suggestedUse;    // Target | Entry | Conflict
			public double exitPrice;       // anchor when this zone is a target; otherwise 0
			public double entryPrice;      // 0 — the entry edge depends on which side is touched
			public double invalidationBelow;
			public double invalidationAbove;
			public int hitCount;
			public int streamCount;
			public int profileCount;       // distinct profiles among the independent survivors (>= independentSources when equal windows merge)
			public readonly List<ConfluenceHit> hits = new List<ConfluenceHit>();
			/// <summary>PR #47: the developing profile's confluence so far, separate from the scored hits and tagged developing.
			/// Every developing-profile label in the zone; credit 0 when it was absorbed into its parent's source or refused.
			/// Null (allocated lazily) when no developing profile has a label in the zone.</summary>
			public List<ConfluenceHit> developingHits;
			public double developingScore; // sum of developingHits credit, completeness-weighted (already inside rawScore; report only)
			public bool HasDeveloping { get { return developingHits != null && developingHits.Count > 0; } }
			public readonly List<ConfluenceDrop> dropped = new List<ConfluenceDrop>();

			public bool HasStream(int dataStream)
			{
				for (int i = 0; i < hits.Count; i++)
					if (hits[i].dataStream == dataStream) return true;
				return false;
			}

			public bool HasKind(EConfluenceKind kind)
			{
				for (int i = 0; i < hits.Count; i++)
					if (hits[i].kind == kind) return true;
				return false;
			}
		}

		/// <summary>
		/// A confluence zone selected as a high-probability target beyond a reference price
		/// in a given direction. price is the zone anchor; touch is the first
		/// edge price would meet (zone.low on a long, zone.high on a short). Rejection zones are not targets.
		/// </summary>
		public class ConfluenceTarget
		{
			public double price;
			public double touch;
			public double distance;
			public EVpDirection direction;
			public ConfluenceZone zone;
		}

		/// <summary>
		/// Internal storage class to associate a specific data stream with it's profiles collection and
		/// extra data stores for it and the update handler delegate.
		///
		/// generation is the invalidation token for this stream. onVpChanged and StartCalc bump it.
		/// listedCache is the sorted pack.Items snapshot for that generation, so itemForBar does not
		/// walk Items on every bar; listedStarts holds the start bar of each listed profile (same order)
		/// for a binary search. resolved memoizes itemForBar for bars before the calc bar, cachedBarA the
		/// calc bar itself (VPB-17). A generation bump drops all of them.
		/// nakedSuppressed is the TrackNakedPoc-off clear-once flag. levelsGeneration moves only
		/// when an extra gains, loses, or rebuilds levels, so percentiles can skip a quiet stream.
		/// </summary>
		private class StreamPack
		{
			public readonly int datastream;
			public IProfilesCollection pack;
			public readonly Dictionary<VpKey, VpExtra> extra = new Dictionary<VpKey, VpExtra>();
			public TProfilesChanged handler;   // must keep this instance to unsubscribe
			public int generation;
			public int listedGeneration = -1;
			public List<IProfile> listedCache;
			public int[] listedStarts = new int[0];   // profileStartBar(listedCache[i]), ascending
			public int emptySkipped;                  // Empty profiles dropped by the last listing (diagnostic)
			public int resolveGeneration = -1;
			public readonly Dictionary<int, IProfile> resolved = new Dictionary<int, IProfile>();   // bars < calc bar
			public int cachedBarA = -1;               // calc bar memo (its window can still grow)
			public IProfile cachedProfileA;
			public bool nakedSuppressed;
			public int nakedStampedBar = -1;          // VPB-16: stampNakedPocs ran for this (bar, close state, generation, extra count)
			public bool nakedStampedAtClose;
			public int nakedStampedGeneration = -1;
			public int nakedStampedExtraCount = -1;
			public int levelsGeneration;
			public int percentilesStampedGeneration = -1;

			// Closed-profile reference distribution (sorted ascending, filled prefix closedCount).
			public double[] closedTotal = new double[0], closedAsk = new double[0], closedBid = new double[0];
			public int closedCount;
			public int closedLevelsGeneration;      // bumps when a profile joins the closed set
			public int closedStampedGeneration = -1;

			public volatile bool pendingChange;   // set by EChanged, consumed in CalcBar
			public volatile bool pendingFull;
			public StreamPack(int datastream) { this.datastream = datastream; }
			public void bumpGeneration()
			{
				generation++;
				if (generation == int.MaxValue) generation = 1;
				listedGeneration = -1;
				resolveGeneration = -1;
				resolved.Clear();
				cachedBarA = -1;
				cachedProfileA = null;
			}
		};



		//properties
		// [Input] properties are plain auto-properties. Their bounds are checked once in StartCalc.
		// Non-[Input] properties keep the check in the getter.
		/// <summary>
		/// VPB-10: a row is HVN material when its smoothed volume is at or above this percentile of the profile's traded rows
		/// (not a fraction of the POC volume). 0..1 and >= LvnPercentile. Contiguous runs at or above it that contain a peak
		/// and are at least MinNodeTicks wide become HVNs.
		/// </summary>
		[Input] public double HvnPercentile { get; set; }
		/// <summary>
		/// VPB-10: a local trough whose smoothed volume is at or below this percentile of the traded rows is an LVN candidate; it
		/// is kept only between two HVNs (within DEFAULT_LVN_HVN_MAX_GAP_TICKS) and if no wider than MaxLvnFractionOfRange.
		/// </summary>
		[Input] public double LvnPercentile { get; set; }
		[Input] public int MinNodeTicks { get; set; }      // Drop HVN/LVN whose high-low span is under this many ticks. >= 1.
		[Input] public bool TrackNakedPoc { get; set; }        // Stamp pocComplete / pocNaked / pocTouchedBar. Default true.
		[Input] public int NakedPocTickTolerance { get; set; } // Ticks of range overlap that mitigate a completed POC. >= 0. Default 1. 0 = exact inclusion.


		[Input] public EVwapType Vwap1Type { get; set; }    // Slot 1. Disabled = unused.
		[Input] public int Vwap1Length { get; set; }        // Used when Vwap1Type = Length. >= 1.
		[Input] public string Vwap1FromDate { get; set; }   // Used when Vwap1Type = AdaptiveFromDate.
		[Input] public int Vwap1DataStream { get; set; }    // Chart data stream slot 1 reads. 1 = Data1, 2 = Data2, …

		[Input] public EVwapType Vwap2Type { get; set; }    // Slot 2. Disabled = unused.
		[Input] public int Vwap2Length { get; set; }        // Used when Vwap2Type = Length. >= 1.
		[Input] public string Vwap2FromDate { get; set; }   // Used when Vwap2Type = AdaptiveFromDate.
		[Input] public int Vwap2DataStream { get; set; }    // Chart data stream slot 2 reads. 1 = Data1, 2 = Data2, …

		[Input] public EVwapType Vwap3Type { get; set; }    // Slot 3. Disabled = unused.
		[Input] public int Vwap3Length { get; set; }        // Used when Vwap3Type = Length. >= 1.
		[Input] public string Vwap3FromDate { get; set; }   // Used when Vwap3Type = AdaptiveFromDate.
		[Input] public int Vwap3DataStream { get; set; }    // Chart data stream slot 3 reads. 1 = Data1, 2 = Data2, …

		// Confluence configuration. Not [Input] — subclasses decide what to expose.
		// Stream index 0 or less disables that slot. Weight 0 drops the stream from scoring.
		private int m_confluenceDataStream1 = 1;
		private int m_confluenceDataStream2 = -1; // by default disable the second data stream
		private int m_confluenceDataStream3 = -1; // by default disable the third data stream
		private double m_confluenceWeight1 = DEFAULT_CONFLUENCE_STREAM_WEIGHT_1;
		private double m_confluenceWeight2 = DEFAULT_CONFLUENCE_STREAM_WEIGHT_2;
		private double m_confluenceWeight3 = DEFAULT_CONFLUENCE_STREAM_WEIGHT_3;
		private int m_confluenceLookbackProfiles = DEFAULT_CONFLUENCE_LOOKBACK;
		private int m_confluenceAlignTicks = DEFAULT_CONFLUENCE_ALIGN_TICKS;
		private int m_confluenceTopCount = DEFAULT_CONFLUENCE_TOP_COUNT;
		private int m_confluenceTargetClearTicks = DEFAULT_CONFLUENCE_TARGET_CLEAR_TICKS;
		private int m_confluenceTargetCount = DEFAULT_CONFLUENCE_TOP_COUNT;

		// Confluence configuration. Not [Input] — subclasses decide what to expose.
		// Setter throws ArgumentException and does not store the value when it is out of range.
		/// <summary>
		/// Chart stream whose volume profiles vote in confluence slot 1.
		/// 1 = Data1. Only this slot is required. Valid: 1 … MaxDataStream.
		/// ≤ 0 disables the slot. Default 1.
		/// </summary>
		public int ConfluenceDataStream1
		{
			get { return m_confluenceDataStream1; }
			set { requireOptionalDataStream("ConfluenceDataStream1", value); m_confluenceDataStream1 = value; }
		}
		/// <summary>
		/// Chart stream for confluence slot 2. Optional higher timeframe.
		/// Valid: 1 … MaxDataStream, or ≤ 0 to disable. Default -1 (disabled).
		/// Do not default this to 2 — Data2 is not required on the chart.
		/// </summary>
		public int ConfluenceDataStream2
		{
			get { return m_confluenceDataStream2; }
			set { requireOptionalDataStream("ConfluenceDataStream2", value); m_confluenceDataStream2 = value; }
		}
		/// <summary>
		/// Chart stream for confluence slot 3. Optional highest timeframe.
		/// Valid: 1 … MaxDataStream, or ≤ 0 to disable. Default -1 (disabled).
		/// </summary>
		public int ConfluenceDataStream3
		{
			get { return m_confluenceDataStream3; }
			set { requireOptionalDataStream("ConfluenceDataStream3", value); m_confluenceDataStream3 = value; }
		}
		/// <summary>
		/// Score multiplier for anchors from ConfluenceDataStream1.
		/// Valid: ≥ 0. 0 drops the stream from scoring. Default 1.00 (execution timeframe).
		/// </summary>
		public double ConfluenceWeight1
		{
			get { return m_confluenceWeight1; }
			set { requireNonNegative("ConfluenceWeight1", value); m_confluenceWeight1 = value; }
		}
		/// <summary>
		/// Score multiplier for anchors from ConfluenceDataStream2.
		/// Valid: ≥ 0. 0 drops the stream. Default 1.25. Unused while the slot is disabled.
		/// </summary>
		public double ConfluenceWeight2
		{
			get { return m_confluenceWeight2; }
			set { requireNonNegative("ConfluenceWeight2", value); m_confluenceWeight2 = value; }
		}
		/// <summary>
		/// Score multiplier for anchors from ConfluenceDataStream3.
		/// Valid: ≥ 0. 0 drops the stream. Default 1.50. Unused while the slot is disabled.
		/// </summary>
		public double ConfluenceWeight3
		{
			get { return m_confluenceWeight3; }
			set { requireNonNegative("ConfluenceWeight3", value); m_confluenceWeight3 = value; }
		}
		/// <summary>
		/// How many volume profiles per enabled stream to include, newest first.
		/// Age 0 is the profile that owns the stream's current bar, resolved with
		/// ItemForBar — not pack.Items. Valid: ≥ 1. Default 4.
		/// </summary>
		public int ConfluenceLookbackProfiles
		{
			get { return m_confluenceLookbackProfiles; }
			set { requireAtLeast("ConfluenceLookbackProfiles", value, 1); m_confluenceLookbackProfiles = value; }
		}
		/// <summary>
		/// Prices within this many ticks are clustered into one confluence zone.
		/// Valid: ≥ 1. Default 4.
		/// </summary>
		public int ConfluenceAlignTicks
		{
			get { return m_confluenceAlignTicks; }
			set { requireAtLeast("ConfluenceAlignTicks", value, 1); m_confluenceAlignTicks = value; }
		}
		/// <summary>
		/// Default count for GetTopConfluenceZones(). Valid: ≥ 1. Default 5.
		/// </summary>
		public int ConfluenceTopCount
		{
			get { return m_confluenceTopCount; }
			set { requireAtLeast("ConfluenceTopCount", value, 1); m_confluenceTopCount = value; }
		}
		/// <summary>
		/// When true, HVN peaks and LVN troughs (other than the POC) are confluence anchors. Default true.
		/// </summary>
		public bool ConfluenceIncludeHvns { get; set; }
		/// <summary>PR #47: a confluence cluster of >= 3 anchors splits at its largest gap when wider than
		/// max(4 x ConfluenceAlignTicks x tick, this x Atr20d). Atr20d is the host's M-DAILY value (NeedsDaily); while it is not
		/// ready the 4 x tol rule alone applies. Default VpConfluenceRules.DefaultSplitAtrFraction (0.03). Protected config, not an [Input].</summary>
		protected internal double ConfluenceSplitAtrFraction { get; set; } = VpConfluenceRules.DefaultSplitAtrFraction;

		// vp-confluence-developing-weight-spec.md §5: completeness weight for developing profiles. Protected config, not [Input]s.
		/// <summary>Developing-profile weighting basis. Off = full credit (pre-spec behaviour). Default Time.</summary>
		protected internal EDevWeighting ConfluenceDevWeighting { get; set; } = VpConfluenceRules.DefaultDevWeighting;
		/// <summary>Completeness ramp shape. Default Linear.</summary>
		protected internal EDevRamp ConfluenceDevRamp { get; set; } = VpConfluenceRules.DefaultDevRamp;
		private double m_confluenceDevStartFraction = VpConfluenceRules.DefaultDevStartFraction;
		private double m_confluenceDevMinWeight = VpConfluenceRules.DefaultDevMinWeight;
		private double m_confluenceDevNoRefWeight = VpConfluenceRules.DefaultDevNoRefWeight;
		private double m_confluenceDevSourceThreshold = VpConfluenceRules.DefaultDevSourceThreshold;
		/// <summary>Below this completeness fraction a developing label gets credit 0 (still listed). [0, 1]. Default 0.10.</summary>
		protected internal double ConfluenceDevStartFraction
		{
			get { return m_confluenceDevStartFraction; }
			set { m_confluenceDevStartFraction = VpConfluenceRules.RequireRange("ConfluenceDevStartFraction", value, 0, 1); }
		}
		/// <summary>Weight floor once started. [0, 1]. Default 0.10.</summary>
		protected internal double ConfluenceDevMinWeight
		{
			get { return m_confluenceDevMinWeight; }
			set { m_confluenceDevMinWeight = VpConfluenceRules.RequireRange("ConfluenceDevMinWeight", value, 0, 1); }
		}
		/// <summary>Weight when the stream has no completed reference profile. [0, 1]. Default 0.50.</summary>
		protected internal double ConfluenceDevNoRefWeight
		{
			get { return m_confluenceDevNoRefWeight; }
			set { m_confluenceDevNoRefWeight = VpConfluenceRules.RequireRange("ConfluenceDevNoRefWeight", value, 0, 1); }
		}
		/// <summary>A developing profile counts in independentSources / profileCount at weight >= this. [0, 1.01] (1.01 = never). Default 0.67.</summary>
		protected internal double ConfluenceDevSourceThreshold
		{
			get { return m_confluenceDevSourceThreshold; }
			set { m_confluenceDevSourceThreshold = VpConfluenceRules.RequireRange("ConfluenceDevSourceThreshold", value, 0, VpConfluenceRules.MaxDevSourceThreshold); }
		}

		/// <summary>PR #47: host Atr20d for the confluence split, NaN when the host has no M-DAILY context (NeedsDaily false) or it is not ready.</summary>
		internal double confluenceAtr20d()
		{
			return VpConfluenceRules.HostAtr20d(NeedsDaily, () => DailyContext == null ? double.NaN : DailyContext.Atr20d);
		}

		/// <summary>PR #47 review: a strategy that reads confluence zones / targets sets this (Create or before base.StartCalc) so
		/// the base builds M-DAILY for the 3% x Atr20d split. Default false: no daily context; zones use the 4 x tol limit.</summary>
		protected bool UsesConfluenceZones { get; set; } = false;
		private bool m_confluenceAtrWarned;

		/// <summary>PR #47: journal line from the confluence engine (text built only when the journal is on).</summary>
		internal void confluenceJournal(Func<string> text)
		{
			if (text == null || !traceOn(ETraceLevel.Journal)) return;
			trace(ETraceLevel.Journal, text());
		}

		/// <summary>VPB-10: an LVN wider than this fraction of the profile's range is dropped. (0, 1]. Default DEFAULT_MAX_LVN_PCT_OF_RANGE (0.12). Not an [Input].</summary>
		public double MaxLvnFractionOfRange { get; set; } = DEFAULT_MAX_LVN_PCT_OF_RANGE;
		/// <summary>
		/// When true, live VWAP slots add context_score only (0.5 × slot weight) and never acceptance or rejection credit. Default true.
		/// </summary>
		public bool ConfluenceIncludeVwapSlots { get; set; }
		/// <summary>
		/// Drop confluence targets whose zone z-score is below this. Any finite value is valid, including negative. Default 0.
		/// </summary>
		public double ConfluenceTargetMinZScore { get; set; }
		/// <summary>
		/// A target zone must clear the reference price by this many ticks (bullish: zone low above price; bearish: zone high below price).
		/// Valid: ≥ 0. Default 1. 0 allows a zone that starts on the reference price.
		/// </summary>
		public int ConfluenceTargetClearTicks
		{
			get { return m_confluenceTargetClearTicks; }
			set { requireNonNegative("ConfluenceTargetClearTicks", value); m_confluenceTargetClearTicks = value; }
		}
		/// <summary>
		/// Default count for GetConfluenceTargets(). Valid: ≥ 1. Default 5.
		/// </summary>
		public int ConfluenceTargetCount
		{
			get { return m_confluenceTargetCount; }
			set { requireAtLeast("ConfluenceTargetCount", value, 1); m_confluenceTargetCount = value; }
		}

		private void validateInputs()
		{
			requireUnitInterval("HvnPercentile", HvnPercentile);
			requireUnitInterval("LvnPercentile", LvnPercentile);
			if (HvnPercentile < LvnPercentile)
				throw new ArgumentException("HvnPercentile must be larger than LvnPercentile.");
			requireAtLeast("MinNodeTicks", MinNodeTicks, 1);
			if (MaxLvnFractionOfRange <= 0 || MaxLvnFractionOfRange > 1) throw new ArgumentException("MaxLvnFractionOfRange must be in (0, 1].");
			requireNonNegative("NakedPocTickTolerance", NakedPocTickTolerance);
			requireAtLeast("Vwap1Length", Vwap1Length, 1);
			requireAtLeast("Vwap2Length", Vwap2Length, 1);
			requireAtLeast("Vwap3Length", Vwap3Length, 1);
			requireDataStream("Vwap1DataStream", Vwap1DataStream, Vwap1Type);
			requireDataStream("Vwap2DataStream", Vwap2DataStream, Vwap2Type);
			requireDataStream("Vwap3DataStream", Vwap3DataStream, Vwap3Type);
			// PR #47: a duplicate enabled confluence stream double-counts the same volume (V5); reject it here, not in the setters.
			VpConfluenceRules.ValidateStreams(ConfluenceDataStream1, ConfluenceDataStream2, ConfluenceDataStream3);
		}

		// requireUnitInterval / requireAtLeast / requireNonNegative / requireDataStream(name, stream) live in _CustomSignal (spec CS-3).

		/// <summary>M-AVWAP: exchange-session date of a bar (the trading-day key: Globex date on futures) for the Week/Month/Quarter VWAP resets.</summary>
		internal DateTime vwapSessionDate(DateTime barTime) { return tradingDayKey(barTime); }
		/// <summary>M-AVWAP: roll signal for the RollDate VWAP reset (§0.3).</summary>
		internal bool vwapRollOn(int stream) { return rollOn(stream); }

		// A disabled VWAP slot is not stepped, so its stream is not required on the chart.
		private void requireDataStream(string name, int stream, EVwapType type)
		{
			if (type == EVwapType.Disabled) return;
			requireDataStream(name, stream);
		}

		private void requireOptionalDataStream(string name, int stream)
		{
			if (stream <= 0) return;
			requireDataStream(name, stream);
		}

		//attribute
		private Dictionary<int, StreamPack> m_streams;  /// Stream packs by data stream index.
		internal _VpVwapEngine m_vwap1;
		internal _VpVwapEngine m_vwap2;
		internal _VpVwapEngine m_vwap3;
		internal readonly List<ConfluenceZone> m_confluenceZones = new List<ConfluenceZone>();
		private _VpConfluenceEngine m_confluenceEngine;
		private bool m_confluenceDirty;
		// Generation, not Bars.CurrentBar. CurrentBar does not move on an IOG tick, so a
		// CurrentBar gate could skip a rebuild the stream's Status bar had already moved
		// past, and it could also skip a tick that did not change CurrentBar. Invalidate and
		// onVpChanged bump the generation. Built bars are streamItemBarIndex of each enabled slot.
		private int m_confluenceGeneration;
		private int m_confluenceBuiltGeneration = -1;
		private int m_confluenceBuiltBar1 = int.MinValue;
		private int m_confluenceBuiltBar2 = int.MinValue;
		private int m_confluenceBuiltBar3 = int.MinValue;
		private bool m_volumePctDirty;
		private int m_pocUnreadableCount;   // VPB-4: developing POC unreadable events this run
		// Grow-only gather buffers. Shrinking every bar was pure allocation. rankPercentile
		// takes the filled count so the spare tail is not part of the CDF.

		// <profile-math> moved to _ProfileMath.Function.CS (VPB-9). This nested alias keeps ProfileMath.X source-compatible
		// for derived strategies (static members and nested types are inherited through the class name).
		public sealed class ProfileMath : _ProfileMath { private ProfileMath() { } }

		// ---------------------------------------------------------------------------------------------------------------
		// VPB-1 own developing profile, VPB-2 value area for Own profiles, VPB-3 look-ahead audit.
		// ---------------------------------------------------------------------------------------------------------------

		/// <summary>VPB-1: where DevelopingExtra() takes the developing profile from.</summary>
		public enum EDevelopingSource { MultiCharts = 0, Own = 1 }
		/// <summary>VPB-1: how the Own engine turns a source bar into volume at price. Auto = VolumeAtPrice on a 1-tick source, BarUniform otherwise.</summary>
		public enum EDevProfileSourceMode { Auto = 0, VolumeAtPrice = 1, BarUniform = 2, BarTypical = 3 }
		/// <summary>VPB-2: value-area method for Own profiles and composites. MultiCharts = the MC VA for MC profiles; Own profiles then use VolumeExpansion.</summary>
		public enum EValueAreaMethod { MultiCharts = 0, VolumeExpansion = 1, Sigma = 2 }

		public const double DEFAULT_VALUE_AREA_PERCENT = 0.70;
		public const double DEFAULT_VALUE_AREA_SIGMA = 1.0;
		private const int MAX_OWN_COMPLETED = 500;
		private const int MAX_AUDIT_WARNINGS = 20;

		/// <summary>VPB-1. Default MultiCharts (no change). Switch to Own once the VPB-3 audit is clean (D6).</summary>
		[Input] public EDevelopingSource DevelopingSource { get; set; }
		/// <summary>VPB-1. Auto: exact on a 1-tick source, BarUniform (approximate) otherwise.</summary>
		[Input] public EDevProfileSourceMode DevProfileSourceMode { get; set; }
		/// <summary>VPB-1. Chart data stream the Own engine reads bars from (1 = Data1). Should be as fine or finer than Data1.</summary>
		[Input] public int DevProfileDataStream { get; set; }
		/// <summary>VPB-2. Value-area method for Own profiles and composites.</summary>
		[Input] public EValueAreaMethod ValueAreaMethod { get; set; }
		/// <summary>VPB-3. Debug: re-check look-ahead invariants on every closed calc bar. Slow. Default false.</summary>
		[Input] public bool LookaheadAudit { get; set; }

		/// <summary>VPB-2. Fraction of volume in the value area (VolumeExpansion). (0,1]. Not an input.</summary>
		public double ValueAreaPercent { get; set; } = DEFAULT_VALUE_AREA_PERCENT;
		/// <summary>VPB-2. k in mean ± k·σ (Sigma method). > 0. Not an input.</summary>
		public double ValueAreaSigma { get; set; } = DEFAULT_VALUE_AREA_SIGMA;
		/// <summary>VPB-3 plant-a-leak test: the audit's MC streaming value reads the profile's end-bar POC. Must be reported.</summary>
		protected bool AuditPlantLeak { get; set; }

		private ProfileMath.Accumulator m_own;
		private int m_ownFoldedBar;              // last source bar folded (1-based Status Bar #)
		private DateTime m_ownPrevTime;
		private bool m_ownApproximate;
		private bool m_ownWindowFromStart;
		private int m_ownMode;
		private VpExtra m_ownSnapshot;
		private int m_ownSnapshotBars = -1;
		private int m_ownSnapshotLast = -1;
		private readonly List<VpExtra> m_ownCompleted = new List<VpExtra>();
		private int m_parityProfiles, m_parityPocMatch, m_parityVaMatch, m_parityUnavailable;
		private int m_auditChecks, m_auditMismatch, m_auditFutureStart, m_auditCompletedLeak, m_auditWarned;
		private bool m_vpbSummaryDone;

		private bool ownEngineOn() { return DevelopingSource == EDevelopingSource.Own || LookaheadAudit; }

		private void startOwnEngine()
		{
			if (ValueAreaPercent <= 0 || ValueAreaPercent > 1) throw new ArgumentException("ValueAreaPercent must be in (0, 1].");
			if (ValueAreaSigma <= 0) throw new ArgumentException("ValueAreaSigma must be > 0.");
			m_own = null; m_ownFoldedBar = 0; m_ownWindowFromStart = false; m_ownPrevTime = DateTime.MinValue; m_ownSnapshot = null; m_ownSnapshotBars = -1; m_ownSnapshotLast = -1;
			m_ownCompleted.Clear();
			m_parityProfiles = m_parityPocMatch = m_parityVaMatch = m_parityUnavailable = 0;
			m_auditChecks = m_auditMismatch = m_auditFutureStart = m_auditCompletedLeak = m_auditWarned = 0;
			m_pocUnreadableCount = 0;
			m_vpbSummaryDone = false;
			if (!ownEngineOn()) return;
			requireDataStream("DevProfileDataStream", DevProfileDataStream);
			IInstrument src = barsOf(DevProfileDataStream);
			bool oneTick = false;
			try { oneTick = src != null && src.Info.Resolution.Type.ToString() == "Tick" && src.Info.Resolution.Size == 1; } catch { }
			m_ownMode = (int)DevProfileSourceMode;
			if (DevProfileSourceMode == EDevProfileSourceMode.Auto) m_ownMode = oneTick ? 1 : 2;
			m_ownApproximate = !(m_ownMode == 1 && oneTick);
			m_own = new ProfileMath.Accumulator(tickSize());
			if (traceOn(ETraceLevel.Info))
				trace(ETraceLevel.Info, $"VPB-1 own profile: source(Data{DevProfileDataStream}) mode({(EDevProfileSourceMode)m_ownMode}) approximate({m_ownApproximate}) " +
					$"valueArea({ValueAreaMethod} {ValueAreaPercent} sigma {ValueAreaSigma}) audit({LookaheadAudit}). MC VA % is a chart setting, not readable from code - check chart.");
		}

		/// <summary>
		/// VPB-1: true when source bar <paramref name="barIndex"/> starts a new profile window. Default: a new MultiCharts
		/// profile starts on that bar of the source stream. VPF overrides with the Globex / RTH day.
		/// </summary>
		protected virtual bool isNewProfileWindow(int stream, int barIndex, DateTime barTime, DateTime prevBarTime)
		{
			return IsNewVolumeProfile(stream, barIndex);
		}

		/// <summary>VPB-1: false drops the bar from the Own profile (VPF uses it for RTH-only profiles). Default true.</summary>
		protected virtual bool includeBarInProfile(DateTime barTime) { return true; }

		/// <summary>VPB-1: fold every closed source bar up to the calc bar. Only closed bars, so backtest and real time agree.</summary>
		private void stepOwnEngine()
		{
			if (m_own == null) return;
			IInstrument src = barsOf(DevProfileDataStream);
			int cur = streamItemBarIndex(DevProfileDataStream);
			if (src == null || cur < 1) return;
			int last = src.Status == EBarState.Close ? cur : cur - 1;
			if (m_ownFoldedBar < 1) m_ownFoldedBar = Math.Max(0, last - ExecInfo.MaxBarsBack);   // history before MaxBarsBack is unreadable
			for (int b = m_ownFoldedBar + 1; b <= last; b++)
			{
				int ago = cur - b;
				if (ago > ExecInfo.MaxBarsBack) continue;
				DateTime t = src.Time[ago];
				bool isNew = isNewProfileWindow(DevProfileDataStream, b, t, m_ownPrevTime);
				if (isNew && m_own.bars > 0) completeOwnProfile();
				if (isNew) m_ownWindowFromStart = true;
				m_ownPrevTime = t;
				if (!includeBarInProfile(t)) continue;
				m_own.Add(b, t, src.Open[ago], src.High[ago], src.Low[ago], src.Close[ago], sourceVolume(src, ago), m_ownMode);
			}
			if (last > m_ownFoldedBar) m_ownFoldedBar = last;
		}

		private static double sourceVolume(IInstrument src, int ago)
		{
			double v = 0;
			try { v = src.Volume[ago]; } catch { }
			if (v <= 0) { try { v = src.Ticks[ago]; } catch { } }
			return v;
		}

		private void completeOwnProfile()
		{
			VpExtra done = buildOwnExtra(m_own, false);
			if (done != null)
			{
				done.isPartial = !m_ownWindowFromStart;   // first window after the start of history: began mid-window
				m_ownCompleted.Add(done);
				if (m_ownCompleted.Count > MAX_OWN_COMPLETED) m_ownCompleted.RemoveAt(0);
				if (!done.isPartial) parityCheck(done);
			}
			m_own.Reset();
			m_ownSnapshot = null; m_ownSnapshotBars = -1;
		}

		/// <summary>VPB-1: compare a completed Own profile with MC's completed profile that starts on the same bar.</summary>
		private void parityCheck(VpExtra own)
		{
			if (own == null || own.poc <= 0) return;
			VpExtra mc = null;
			try { mc = ExtraVolumeProfileForBar(DevProfileDataStream, own.startBar); } catch { }
			if (mc == null || mc.poc <= 0 || mc.startBar != own.startBar)
			{
				m_parityUnavailable++;
				return;
			}
			double tick = tickSize();
			m_parityProfiles++;
			bool pocOk = Math.Abs(mc.poc - own.poc) < tick * 0.5;
			bool vaOk = mc.vah > 0 && mc.val > 0 && Math.Abs(mc.vah - own.vah) <= tick * 1.5 && Math.Abs(mc.val - own.val) <= tick * 1.5;
			if (pocOk) m_parityPocMatch++;
			if (vaOk) m_parityVaMatch++;
			if ((!pocOk || !vaOk) && traceOn(ETraceLevel.Verbose))
				trace(ETraceLevel.Verbose, $"VPB-1 parity start({own.startBar}) own poc({own.poc}) vah({own.vah}) val({own.val}) mc poc({mc.poc}) vah({mc.vah}) val({mc.val})");
		}

		/// <summary>Snapshot of an accumulator as a VpExtra (levels, POC, VA by ValueAreaMethod, HVN/LVN).</summary>
		private VpExtra buildOwnExtra(ProfileMath.Accumulator acc, bool developing)
		{
			if (acc == null || acc.bars == 0) return null;
			var e = new VpExtra();
			e.dataStream = DevProfileDataStream;
			e.startBar = acc.firstBar; e.endBar = acc.lastBar;
			e.open = acc.open; e.high = acc.high; e.low = acc.low; e.close = acc.close;
			e.isDeveloping = developing;
			e.approximate = m_ownApproximate;
			e.isOwn = true;
			double[] prices, vols;
			acc.Dense(out prices, out vols);
			fillFromHistogram(e, prices, vols);
			e.pocComplete = !developing && e.poc > 0;
			e.nakedTracked = false;
			return e;
		}

		/// <summary>levels, raw-max POC, value area (VPB-2) and nodes for a histogram that MC did not compute.</summary>
		protected void fillFromHistogram(VpExtra e, double[] prices, double[] vols)
		{
			e.levels.Clear(); e.hvns.Clear(); e.lvns.Clear();
			e.poc = e.pocVol = e.vah = e.val = 0;
			if (prices == null || vols == null || prices.Length == 0) return;
			for (int i = 0; i < prices.Length; i++)
				if (vols[i] > 0) e.levels.Add(new LevelVol { price = prices[i], total = vols[i] });
			int lo, hi, poc;
			if (!ProfileMath.ValueAreaExpansion(vols, ValueAreaPercent, out lo, out hi, out poc)) return;
			e.poc = prices[poc]; e.pocVol = vols[poc];
			if (ValueAreaMethod == EValueAreaMethod.Sigma)
			{
				double vah, val;
				if (ProfileMath.ValueAreaSigma(prices, vols, ValueAreaSigma, out vah, out val)) { e.vah = vah; e.val = val; }
			}
			else { e.vah = prices[hi]; e.val = prices[lo]; }
			buildNodesFromLevels(e);
		}

		/// <summary>
		/// VPB-1: developing profile on <paramref name="stream"/> as of the calc bar. Own (DevelopingSource = Own and stream =
		/// DevProfileDataStream): the Own snapshot built from closed source bars only. Otherwise MultiCharts' developing extra.
		/// isDeveloping is set on the returned extra.
		/// </summary>
		public VpExtra DevelopingExtra(int stream)
		{
			if (DevelopingSource == EDevelopingSource.Own && stream == DevProfileDataStream && m_own != null)
				return ownSnapshot();
			int cur = streamItemBarIndex(stream);
			VpExtra e = cur >= 1 ? ExtraVolumeProfileForBar(stream, cur) : null;
			if (e != null) e.isDeveloping = !e.pocComplete;
			return e;
		}

		/// <summary>VPB-1: completed Own profiles, newest first (at most MAX_OWN_COMPLETED kept).</summary>
		public List<VpExtra> OwnCompletedProfiles(int count)
		{
			var res = new List<VpExtra>();
			for (int i = m_ownCompleted.Count - 1; i >= 0 && res.Count < count; i--) res.Add(m_ownCompleted[i]);
			return res;
		}

		private VpExtra ownSnapshot()
		{
			if (m_own == null) return null;
			if (m_ownSnapshot == null || m_ownSnapshotBars != m_own.bars || m_ownSnapshotLast != m_own.lastBar)
			{
				m_ownSnapshot = buildOwnExtra(m_own, true);
				m_ownSnapshotBars = m_own.bars; m_ownSnapshotLast = m_own.lastBar;
			}
			return m_ownSnapshot;
		}

		/// <summary>
		/// VPB-3: on every closed calc bar, (1) no stored extra starts after its stream's calc bar; (2) RecentCompletedProfiles
		/// only returns profiles with endBar before the calc bar; (3) the Own snapshot equals a from-scratch rebuild over the
		/// current window's bars; (4) the MC developing POC/VAH/VAL from the streaming extra equals a fresh as-of read.
		/// </summary>
		private void runLookaheadAudit()
		{
			if (!LookaheadAudit || Bars.Status != EBarState.Close) return;
			m_auditChecks++;
			foreach (var kv in m_streams)
			{
				StreamPack sp = kv.Value;
				if (sp == null || sp.extra == null) continue;
				int cur = streamItemBarIndex(kv.Key);
				foreach (VpExtra e in sp.extra.Values)
					if (e != null && cur >= 1 && e.startBar > cur) { m_auditFutureStart++; auditWarn($"extra startBar({e.startBar}) > current({cur}) stream({kv.Key})"); }
				foreach (VpExtra e in RecentCompletedProfiles(kv.Key, int.MaxValue, cur))
					if (e.endBar >= cur) { m_auditCompletedLeak++; auditWarn($"completed profile endBar({e.endBar}) >= current({cur}) stream({kv.Key})"); }

				// (4) MC path: streaming developing extra vs a fresh as-of geometry read.
				IProfile vp = sp.pack != null ? itemForBar(sp, kv.Key, cur) : null;
				VpExtra streaming = vp != null ? lookupExtra(sp, kv.Key, vp) : null;
				if (vp != null && streaming != null && profileEndBar(vp) >= cur)
				{
					var fresh = new VpExtra { dataStream = kv.Key };
					int unreadable = m_pocUnreadableCount;
					applyProfileGeometry(vp, fresh, cur);
					m_pocUnreadableCount = unreadable;   // the audit read is not a strategy read
					double sPoc = AuditPlantLeak ? priceForBar(vp, profileEndBar(vp), 0) : streaming.poc;
					if (!samePrice(sPoc, fresh.poc) || !samePrice(streaming.vah, fresh.vah) || !samePrice(streaming.val, fresh.val))
					{
						m_auditMismatch++;
						auditWarn($"MC developing stream({kv.Key}) start({fresh.startBar}) bar({cur}) streaming poc({sPoc}) vah({streaming.vah}) val({streaming.val}) fresh poc({fresh.poc}) vah({fresh.vah}) val({fresh.val})");
					}
				}
			}

			// (3) Own path: rebuild the current window from its first bar.
			if (m_own != null && m_own.bars > 0)
			{
				IInstrument src = barsOf(DevProfileDataStream);
				int cur = streamItemBarIndex(DevProfileDataStream);
				if (src != null && cur >= m_own.lastBar && cur - m_own.firstBar <= ExecInfo.MaxBarsBack)
				{
					var acc = new ProfileMath.Accumulator(m_own.tick);
					for (int b = m_own.firstBar; b <= m_own.lastBar; b++)
					{
						int ago = cur - b;
						DateTime t = src.Time[ago];
						if (!includeBarInProfile(t)) continue;
						acc.Add(b, t, src.Open[ago], src.High[ago], src.Low[ago], src.Close[ago], sourceVolume(src, ago), m_ownMode);
					}
					VpExtra a = ownSnapshot(), f = buildOwnExtra(acc, true);
					if (a == null || f == null || !samePrice(a.poc, f.poc) || !samePrice(a.vah, f.vah) || !samePrice(a.val, f.val) || a.hvns.Count != f.hvns.Count)
					{
						m_auditMismatch++;
						auditWarn($"Own developing bar({cur}) streaming poc({a?.poc}) vah({a?.vah}) val({a?.val}) rebuilt poc({f?.poc}) vah({f?.vah}) val({f?.val})");
					}
				}
			}
		}

		private bool samePrice(double a, double b) { return Math.Abs(a - b) < tickSize() * 0.5; }

		private void auditWarn(string msg)
		{
			if (m_auditWarned++ < MAX_AUDIT_WARNINGS && traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "VPB-3 audit: " + msg);
		}

		/// <summary>Once on the last bar: VPB-1 parity, VPB-3 audit and VPB-4 counters.</summary>
		private void vpbRunSummary()
		{
			if (m_vpbSummaryDone || !Bars.LastBarOnChart || Bars.Status != EBarState.Close) return;
			m_vpbSummaryDone = true;
			if (!traceOn(ETraceLevel.Info)) return;
			if (m_own != null)
			{
				string res = "";
				try { IInstrument src = barsOf(DevProfileDataStream); res = $"{src.Info.Resolution.Type} {src.Info.Resolution.Size}"; } catch { }
				trace(ETraceLevel.Info, $"VPB-1 parity resolution({res}) approximate({m_ownApproximate}) profiles({m_parityProfiles}) pocMatch({m_parityPocMatch}) " +
					$"vaMatch1Tick({m_parityVaMatch}) mcUnavailable({m_parityUnavailable})" + (m_parityProfiles > 0 ? $" pocMatchRate({(double)m_parityPocMatch / m_parityProfiles:0.000})" : ""));
			}
			if (LookaheadAudit)
				trace(ETraceLevel.Info, $"VPB-3 audit checks({m_auditChecks}) mismatches({m_auditMismatch}) futureStart({m_auditFutureStart}) completedLeak({m_auditCompletedLeak}) plantLeak({AuditPlantLeak})");
			trace(ETraceLevel.Info, $"VPB-4 developing POC unreadable events({m_pocUnreadableCount})");
		}

		// ---------------------------------------------------------------------------------------------------------------
		// VPB-6 completed-profile analytics and VPB-7 composites. Completed profiles only (endBar before the stream's calc
		// bar); a developing extra returns the neutral value. Read-only: nothing here reads bars after the calc bar.
		// ---------------------------------------------------------------------------------------------------------------

		private bool isCompleted(VpExtra e)
		{
			if (e == null || e.startBar < 1) return false;
			int cur = streamItemBarIndex(e.dataStream > 0 ? e.dataStream : 1);
			return cur >= 1 && e.endBar >= e.startBar && e.endBar < cur;
		}

		private bool denseOf(VpExtra e, out double[] prices, out double[] vols)
		{
			prices = vols = null;
			if (e == null || e.levels.Count == 0) return false;
			var p = new List<double>(e.levels.Count); var v = new List<double>(e.levels.Count);
			for (int i = 0; i < e.levels.Count; i++) { p.Add(e.levels[i].price); v.Add(e.levels[i].total); }
			return ProfileMath.ToDense(p, v, tickSize(), out prices, out vols);
		}

		private static int indexOf(double[] prices, double price, double tick)
		{
			if (prices == null || prices.Length == 0 || tick <= 0) return -1;
			long i = (long)Math.Round((price - prices[0]) / tick);
			return i < 0 ? 0 : i >= prices.Length ? prices.Length - 1 : (int)i;
		}

		/// <summary>VPB-6: Dalton value-area relationship of <paramref name="cur"/> to <paramref name="prev"/> (both completed).</summary>
		public ProfileMath.ValueRel ValueRelationship(VpExtra prev, VpExtra cur)
		{
			if (!isCompleted(prev) || !isCompleted(cur)) return ProfileMath.ValueRel.Unknown;
			return ProfileMath.Relationship(prev.vah, prev.val, cur.vah, cur.val);
		}

		/// <summary>VPB-6: poor high — the top row and at least <paramref name="minRows"/> rows below it each hold >= fraction x mean row volume.</summary>
		public bool IsPoorHigh(VpExtra e, double fraction = 0.5, int minRows = 2)
		{
			double[] p, v;
			return isCompleted(e) && denseOf(e, out p, out v) && ProfileMath.IsPoorExtreme(v, true, fraction, minRows);
		}

		/// <summary>VPB-6: poor low (mirror of IsPoorHigh).</summary>
		public bool IsPoorLow(VpExtra e, double fraction = 0.5, int minRows = 2)
		{
			double[] p, v;
			return isCompleted(e) && denseOf(e, out p, out v) && ProfileMath.IsPoorExtreme(v, false, fraction, minRows);
		}

		/// <summary>VPB-6: unfinished auction — both ask and bid volume traded on the extreme row. False without ask/bid data.</summary>
		public bool HasUnfinishedAuction(VpExtra e, bool high)
		{
			if (!isCompleted(e) || e.levels.Count == 0) return false;
			LevelVol x = e.levels[0];
			for (int i = 1; i < e.levels.Count; i++)
				if (high ? e.levels[i].price > x.price : e.levels[i].price < x.price) x = e.levels[i];
			return x.ask > 0 && x.bid > 0;
		}

		/// <summary>
		/// VPB-6: single-print runs (>= minTicks rows with volume <= the pct-percentile of traded rows) between the value area and
		/// the extremes. Each entry is { low, high } in price.
		/// </summary>
		public List<double[]> SinglePrints(VpExtra e, int minTicks, double pct = 0.2)
		{
			var res = new List<double[]>();
			double[] p, v;
			if (!isCompleted(e) || e.vah <= 0 || e.val <= 0 || !denseOf(e, out p, out v)) return res;
			double tick = tickSize();
			foreach (int[] r in ProfileMath.SinglePrints(v, indexOf(p, e.val, tick), indexOf(p, e.vah, tick), pct, Math.Max(1, minTicks)))
				res.Add(new[] { p[r[0]], p[r[1]] });
			return res;
		}

		/// <summary>VPB-6: profile shape (thresholds: ProfileMath.SHAPE_*).</summary>
		public ProfileMath.Shape ProfileShape(VpExtra e)
		{
			double[] p, v;
			if (!isCompleted(e) || !denseOf(e, out p, out v)) return ProfileMath.Shape.Unknown;
			return ProfileMath.ProfileShape(v, ValueAreaPercent);
		}

		/// <summary>
		/// VPB-6: true while no bar after <paramref name="e"/> closed, up to <paramref name="asOfBar"/>, has traded
		/// <paramref name="level"/> (within NakedPocTickTolerance). Use with e.vah / e.val for naked VAH / VAL.
		/// </summary>
		public bool IsLevelNaked(VpExtra e, double level, int asOfBar)
		{
			if (!isCompleted(e) || level <= 0) return false;
			int stream = e.dataStream > 0 ? e.dataStream : 1;
			int cur = streamItemBarIndex(stream);
			if (asOfBar > cur) asOfBar = cur;
			if (asOfBar <= e.endBar) return false;
			IInstrument bars = barsOf(stream);
			double tick = tickSize();
			double tol = NakedPocTickTolerance * tick + tick * 1e-6;
			for (int b = e.endBar + 1; b <= asOfBar; b++)
			{
				double h = barPrice(bars, cur, b, 1), l = barPrice(bars, cur, b, 2);
				if (h <= 0 || l <= 0) continue;
				if (h >= level - tol && l <= level + tol) return false;
			}
			return true;
		}

		public bool IsNakedVah(VpExtra e, int asOfBar) { return e != null && IsLevelNaked(e, e.vah, asOfBar); }
		public bool IsNakedVal(VpExtra e, int asOfBar) { return e != null && IsLevelNaked(e, e.val, asOfBar); }

		/// <summary>
		/// VPB-6: first bar after <paramref name="reference"/> closed at which <paramref name="minCloses"/> consecutive closes were
		/// above its VAH (above = true) or below its VAL; 0 when not (yet) accepted. Scans closed bars up to the calc bar.
		/// </summary>
		public int AcceptanceOutsideValue(VpExtra reference, bool above, int minCloses)
		{
			if (!isCompleted(reference) || reference.vah <= 0 || reference.val <= 0 || minCloses < 1) return 0;
			int stream = reference.dataStream > 0 ? reference.dataStream : 1;
			int cur = streamItemBarIndex(stream);
			IInstrument bars = barsOf(stream);
			int last = bars != null && bars.Status == EBarState.Close ? cur : cur - 1;
			int run = 0;
			for (int b = reference.endBar + 1; b <= last; b++)
			{
				double c = barPrice(bars, cur, b, 3);
				if (c <= 0) { run = 0; continue; }
				run = (above ? c > reference.vah : c < reference.val) ? run + 1 : 0;
				if (run >= minCloses) return b;
			}
			return 0;
		}

		/// <summary>
		/// VPB-6 (Rev 2.4): completed session profiles for value migration, newest first. Default: the stream's completed
		/// profiles ending before the calc bar. The futures base returns completed Own RTH profiles when OwnProfileWindow = Rth.
		/// </summary>
		protected virtual List<VpExtra> migrationProfiles(int stream, int count)
		{
			return RecentCompletedProfiles(stream, count, streamItemBarIndex(stream));
		}

		/// <summary>
		/// VPB-6 (Rev 2.4) ValueMigrationScore (Keller Book 4 §8.4 coding; a hypothesis, not a measured edge). Settings are
		/// parameters, not inputs. D = minChangeAtrFraction x ATR, where ATR = dailyAtr when > 0 (caller supplies a daily ATR
		/// ending at the last completed session), else the mean high-low range of the last atrLen completed sessions. Null
		/// (Unknown) with fewer than n+1 completed POCs or fewer than atrLen sessions for the fallback ATR. The developing
		/// term uses DevelopingExtra(stream) (VPB-1 source) as of the calc bar. Range [-n, +n], [-(n+1), +(n+1)] with it.
		/// </summary>
		public int? ValueMigrationScore(int stream, int n = 3, double minChangeAtrFraction = 0.10, int atrLen = 20,
			bool includeDeveloping = true, double dailyAtr = 0)
		{
			if (n < 1 || atrLen < 1 || minChangeAtrFraction < 0) return null;
			List<VpExtra> done = migrationProfiles(stream, Math.Max(n + 1, dailyAtr > 0 ? 0 : atrLen));
			if (done.Count < n + 1) return null;
			double atr = dailyAtr;
			if (!(atr > 0))
			{
				if (done.Count < atrLen) return null;
				double sum = 0;
				for (int i = 0; i < atrLen; i++)
				{
					if (!(done[i].high > 0 && done[i].low > 0)) return null;
					sum += done[i].high - done[i].low;
				}
				atr = sum / atrLen;
			}
			var pocs = new List<double>(n + 1);
			for (int i = n; i >= 0; i--)
			{
				if (!(done[i].poc > 0)) return null;
				pocs.Add(done[i].poc);   // oldest first
			}
			double? dev = null;
			if (includeDeveloping)
			{
				VpExtra d = DevelopingExtra(stream);
				if (d != null && d.poc > 0 && d.startBar > done[0].endBar) dev = d.poc;
			}
			return ProfileMath.MigrationScore(pocs, n, minChangeAtrFraction * atr, dev);
		}

		/// <summary>VPB-6 (Rev 2.4): Up / Down / Neutral from ValueMigrationScore with thresholds (default 2); Unknown when it is null.</summary>
		public ProfileMath.MigrationState ValueMigrationState(int stream, int n = 3, double minChangeAtrFraction = 0.10, int atrLen = 20,
			bool includeDeveloping = true, double dailyAtr = 0, int upAt = 2, int downAt = 2)
		{
			return ProfileMath.ClassifyMigration(ValueMigrationScore(stream, n, minChangeAtrFraction, atrLen, includeDeveloping, dailyAtr), upAt, downAt);
		}

		/// <summary>VPF-4 hook: does this profile span a contract-roll splice? Default false; the futures base overrides.</summary>
		protected virtual bool profileCrossesRoll(VpExtra e) { return false; }

		private VpExtra m_compositeCache;
		private int m_compositeStream = -1, m_compositeN = -1, m_compositeBar = -1, m_compositeNewest = -1;

		/// <summary>
		/// VPB-7: merge the levels of the last <paramref name="n"/> completed profiles on <paramref name="stream"/> onto one tick
		/// grid; POC / VA (ValueAreaMethod; MultiCharts → VolumeExpansion) / HVN / LVN from the merged histogram. Never contains
		/// the developing profile. Cached per (stream, n, calc bar, newest profile). Null when no completed profile.
		/// </summary>
		public VpExtra Composite(int stream, int n)
		{
			int cur = streamItemBarIndex(stream);
			List<VpExtra> src = RecentCompletedProfiles(stream, n, cur);
			if (src.Count == 0) return null;
			if (m_compositeCache != null && m_compositeStream == stream && m_compositeN == n && m_compositeBar == cur && m_compositeNewest == src[0].startBar)
				return m_compositeCache;
			var p = new List<double>(); var v = new List<double>();
			var e = new VpExtra { dataStream = stream, isOwn = true, startBar = src[src.Count - 1].startBar, endBar = src[0].endBar };
			e.open = src[src.Count - 1].open; e.close = src[0].close; e.high = double.MinValue; e.low = double.MaxValue;
			foreach (VpExtra x in src)
			{
				if (x.high > e.high) e.high = x.high;
				if (x.low > 0 && x.low < e.low) e.low = x.low;
				x.crossesRoll = profileCrossesRoll(x);
				e.crossesRoll |= x.crossesRoll;
				for (int i = 0; i < x.levels.Count; i++) { p.Add(x.levels[i].price); v.Add(x.levels[i].total); }
			}
			double[] dp, dv;
			if (ProfileMath.ToDense(p, v, tickSize(), out dp, out dv)) fillFromHistogram(e, dp, dv);
			e.pocComplete = e.poc > 0;
			m_compositeCache = e; m_compositeStream = stream; m_compositeN = n; m_compositeBar = cur; m_compositeNewest = src[0].startBar;
			return e;
		}

		/// <summary>VPB-7: the last <paramref name="n"/> completed value areas overlap pairwise by >= minVaOverlap of the narrower.</summary>
		public bool IsBalanced(int stream, int n, double minVaOverlap = 0.6)
		{
			List<VpExtra> src = RecentCompletedProfiles(stream, n, streamItemBarIndex(stream));
			if (src.Count < Math.Max(2, n)) return false;
			var hi = new List<double>(); var lo = new List<double>();
			for (int i = src.Count - 1; i >= 0; i--) { hi.Add(src[i].vah); lo.Add(src[i].val); }
			return ProfileMath.IsBalanced(hi, lo, minVaOverlap);
		}

		/// <summary>VPB-7: union (or intersection) of the last n completed value areas. False when fewer than n or empty.</summary>
		public bool BalanceRange(int stream, int n, bool union, out double high, out double low)
		{
			high = low = 0;
			List<VpExtra> src = RecentCompletedProfiles(stream, n, streamItemBarIndex(stream));
			if (src.Count < n || n < 1) return false;
			high = union ? double.MinValue : double.MaxValue; low = union ? double.MaxValue : double.MinValue;
			foreach (VpExtra x in src)
			{
				if (x.vah <= 0 || x.val <= 0) return false;
				high = union ? Math.Max(high, x.vah) : Math.Min(high, x.vah);
				low = union ? Math.Min(low, x.val) : Math.Max(low, x.val);
			}
			return high >= low;
		}

		/// <summary>
		/// VPB-6: value-migration score V (final-synthesis section 4.0) over the last <paramref name="n"/> completed profiles on
		/// <paramref name="stream"/>, which should be an RTH-session profile stream. <paramref name="atr20Daily"/> is ATR(20) on
		/// daily bars, supplied by the caller (the base has no daily stream of its own); changes smaller than
		/// <paramref name="minChangeFraction"/> x atr20Daily score 0. With <paramref name="includeDeveloping"/> the developing
		/// POC (DevelopingExtra) adds a fourth term; a stale or unreadable developing POC (pocStale / 0) adds nothing.
		/// Returns 0 when fewer than n + 1 completed profiles are stored. On non-adjusted continuous charts a roll between
		/// sessions shows as a POC jump; use a back-adjusted series or check crossesRoll / RollDates before trusting V there.
		/// </summary>
		public int ValueMigrationScore(int stream, double atr20Daily, int n = 3, double minChangeFraction = 0.10, bool includeDeveloping = true)
		{
			if (n < 1 || atr20Daily < 0 || minChangeFraction < 0) return 0;
			List<VpExtra> src = RecentCompletedProfiles(stream, n + 1, streamItemBarIndex(stream));
			if (src.Count < n + 1) return 0;
			var pocs = new List<double>(src.Count);
			for (int i = src.Count - 1; i >= 0; i--) pocs.Add(src[i].poc);   // oldest first
			double dev = 0;
			if (includeDeveloping)
			{
				VpExtra d = DevelopingExtra(stream);
				if (d != null && d.isDeveloping && !d.pocStale && d.poc > 0) dev = d.poc;
			}
			int v;
			return ProfileMath.ValueMigrationScore(pocs, n, dev, minChangeFraction * atr20Daily, out v) ? v : 0;
		}

		//interface methods
		public _VolumeProfileBase(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			HvnPercentile = DEFAULT_HVN_PERCENTILE;
			LvnPercentile = DEFAULT_LVN_PERCENTILE;
			MinNodeTicks = DEFAULT_MIN_NODE_TICKS;
			TrackNakedPoc = true;
			NakedPocTickTolerance = 1;
			DevelopingSource = EDevelopingSource.MultiCharts;
			DevProfileSourceMode = EDevProfileSourceMode.Auto;
			DevProfileDataStream = 1;
			ValueAreaMethod = EValueAreaMethod.MultiCharts;
			LookaheadAudit = false;

			Vwap1Type = EVwapType.Session;
			Vwap1Length = DEFAULT_VWAP_LENGTH;
			Vwap1FromDate = "";
			Vwap1DataStream = 1;

			Vwap2Type = EVwapType.Disabled;
			Vwap2Length = DEFAULT_VWAP_LENGTH;
			Vwap2FromDate = "";
			Vwap2DataStream = 2;

			Vwap3Type = EVwapType.Disabled;
			Vwap3Length = DEFAULT_VWAP_LENGTH;
			Vwap3FromDate = "";
			Vwap3DataStream = 1;

			//NOTE: ConfluenceDataStreamX is already set and since it accesses MaxDataStream to validate we skip
			//      the initialization here since MC.Net will raise an exception accessing MaxDataStream in the
			//      Create method.
			ConfluenceWeight1 = DEFAULT_CONFLUENCE_STREAM_WEIGHT_1;   // execution TF
			ConfluenceWeight2 = DEFAULT_CONFLUENCE_STREAM_WEIGHT_2;   // higher TF carries more by default
			ConfluenceWeight3 = DEFAULT_CONFLUENCE_STREAM_WEIGHT_3;
			ConfluenceLookbackProfiles = DEFAULT_CONFLUENCE_LOOKBACK;
			ConfluenceAlignTicks = DEFAULT_CONFLUENCE_ALIGN_TICKS;
			ConfluenceTopCount = DEFAULT_CONFLUENCE_TOP_COUNT;
			ConfluenceIncludeHvns = true;
			ConfluenceIncludeVwapSlots = true;
			ConfluenceTargetMinZScore = 0;
			ConfluenceTargetClearTicks = DEFAULT_CONFLUENCE_TARGET_CLEAR_TICKS;
			ConfluenceTargetCount = DEFAULT_CONFLUENCE_TOP_COUNT;
			m_confluenceDirty = true;
			m_confluenceGeneration = 1;
			m_confluenceBuiltGeneration = -1;
			m_volumePctDirty = true;
			// PR #47: allocate lazily. m_streams is created by hookStreams, a VWAP engine by StartCalc only for an enabled slot,
			// and the confluence engine by ensureConfluenceZones on first use.
		}

		protected override void Destroy()
		{
			unhookStreams();
			base.Destroy();
		}

		protected override void StartCalc()
		{
			// PR #47 review: Atr20d reaches the confluence split only through M-DAILY, so enable it (lazily) when the split uses ATR
			// and a confluence slot is on. Hosts that already set NeedsDaily are unaffected.
			m_confluenceAtrWarned = false;
			if (VpConfluenceRules.NeedsAtr20d(UsesConfluenceZones, ConfluenceSplitAtrFraction, ConfluenceDataStream1, ConfluenceDataStream2, ConfluenceDataStream3)) NeedsDaily = true;
			base.StartCalc();
			validateInputs();
			m_hookRetried = false; m_zeroVolumeWarned = false; m_zeroVolumeBars = 0; m_vpDiagSummaryDone = false;
			unhookStreams();
			hookStreams();
			m_vwap1 = configureVwap(m_vwap1, "VWAP1", Vwap1Type, Vwap1Length, Vwap1FromDate, Vwap1DataStream, "VwapFromDate", "VwapDataStream");
			m_vwap2 = configureVwap(m_vwap2, "VWAP2", Vwap2Type, Vwap2Length, Vwap2FromDate, Vwap2DataStream, "Vwap2FromDate", "Vwap2DataStream");
			m_vwap3 = configureVwap(m_vwap3, "VWAP3", Vwap3Type, Vwap3Length, Vwap3FromDate, Vwap3DataStream, "Vwap3FromDate", "Vwap3DataStream");
			if (VwapConfig.resetPeriodSlotsOnRoll && RollDetection == ERollDetection.Off)
				trace(ETraceLevel.Warning, "VWAP resetPeriodSlotsOnRoll is on but RollDetection is Off: Week / Month slots will not reset at a roll (use RollDetection.Table).");
			foreach (var kv in m_streams)
			{
				if (kv.Value != null)
				{
					kv.Value.bumpGeneration();
					kv.Value.closedCount = 0; kv.Value.closedLevelsGeneration++; kv.Value.closedStampedGeneration = -1;
					foreach (VpExtra e in kv.Value.extra.Values) if (e != null) { e.inClosedSet = false; e.pctStampedFill = -1; }
				}
			}

			m_confluenceGeneration++;
			m_confluenceDirty = true;
			m_confluenceBuiltGeneration = -1;
			m_confluenceZones.Clear();
			m_volumePctDirty = true;
			warnIfLengthExceedsMaxBarsBack(m_vwap1);
			warnIfLengthExceedsMaxBarsBack(m_vwap2);
			warnIfLengthExceedsMaxBarsBack(m_vwap3);
			startOwnEngine();
			startVpbHelpers();      // Phase 3 VP-1..VP-4 / VP-6 (no-op unless enabled)
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			vpConnectionDiagnostics();
			processPendingProfileChanges();
			stepVwap(m_vwap1);
			stepVwap(m_vwap2);
			stepVwap(m_vwap3);
			stampCurrentExtras();
			m_confluenceDirty = true;
			refreshVolumePercentiles();
			stepOwnEngine();        // VPB-1 (no-op unless DevelopingSource = Own or LookaheadAudit)
			runLookaheadAudit();    // VPB-3
			vpbRunSummary();
			stepVpbHelpers();       // Phase 3 VP-1..VP-3 handles / weekly sessions (no-op unless enabled)
		}

		/// <summary>
		/// Returns the extra volume profile information computed for the given bar.
		/// </summary>
		/// <param name="barIndex">1-based chart bar (Status Bar # / FullSymbolData.Current).</param>
		/// <returns>Extra volume profile information or null if nothing found.</returns>
		public VpExtra ExtraVolumeProfileForBar(int barIndex)
		{
			return ExtraVolumeProfileForBar(1, barIndex);
		}

		/// <summary>
		/// Returns the extra volume profile information computed for the given bar.
		/// </summary>
		/// <param name="datastream">Chart data stream. 1 = Data1.</param>
		/// <param name="barIndex">1-based chart bar (Status Bar # / FullSymbolData.Current).</param>
		/// <returns>Extra volume profile information or null if nothing found.</returns>
		public VpExtra ExtraVolumeProfileForBar(int datastream, int barIndex)
		{
			if (barIndex < 1) return null;
			if (m_streams == null || !m_streams.TryGetValue(datastream, out StreamPack streamPack) || streamPack.pack == null)
				return null;

			// VPB-28: a query must not rewrite history. Only the developing profile is refreshed, and only as of the
			// calc bar; a closed profile keeps its final POC/VA and its VWAP snapshot from its own last bar. A closed
			// profile with no extra yet (older than anything CalcBar saw, or after a full profile change) is built as
			// of its own end bar.
			IProfile vp = itemForBar(streamPack, datastream, barIndex);
			int cur = streamItemBarIndex(datastream);
			bool developingQuery = vp != null && (cur < 1 || profileEndBar(vp) >= cur);
			if (vp != null && developingQuery)
				ensureExtra(streamPack, vp, cur >= 1 ? cur : barIndex);

			VpExtra extra = vp != null ? lookupExtra(streamPack, datastream, vp) : null;
			if (extra == null && vp != null && !developingQuery)
			{
				ensureExtra(streamPack, vp, profileEndBar(vp));
				extra = lookupExtra(streamPack, datastream, vp);
			}
			if (extra == null)
				extra = extraForStart(streamPack, datastream, barIndex);
			if (extra == null)
			{
				if (traceOn(ETraceLevel.Verbose)) trace(ETraceLevel.Verbose, $"Failed to find volume profile for data stream {datastream} for bar {barIndex}.");
				return null;
			}
			stampNakedPocs(streamPack);
			refreshVolumePercentiles();
			return extra;
		}

		/// <summary>
		/// DER-06. Up to <paramref name="count"/> completed profiles on <paramref name="datastream"/> that ended before
		/// <paramref name="asOfBar"/> (1-based Status Bar #), newest first, straight from the stored extras. Read-only: no fill,
		/// no geometry, no VWAP stamping; only the (idempotent, once-per-bar) naked-POC stamp that sets pocComplete.
		/// Extras exist for every profile CalcBar has seen (current and just-closed profile each bar) and for every profile in the
		/// collection after a full change; profiles that ended before the first calculated bar may be missing, so callers that
		/// need exactly <paramref name="count"/> profiles should fall back to ExtraVolumeProfileForBar when fewer are returned.
		/// </summary>
		public bool IsProfileStreamConnected(int datastream)
		{
			StreamPack sp;
			return m_streams != null && m_streams.TryGetValue(datastream, out sp) && sp != null && sp.pack != null;
		}

		/// <summary>Start bars (1-based, ascending, -1 = unreadable) of the listed non-empty MC profiles on a stream; empty when
		/// the stream is not connected. Shared cache: callers must not modify the array.</summary>
		public int[] ProfileStartBars(int datastream)
		{
			StreamPack sp;
			if (m_streams == null || !m_streams.TryGetValue(datastream, out sp) || sp == null || sp.pack == null) return new int[0];
			List<IProfile> listed = listedProfiles(sp);
			return sp.listedStarts.Length == listed.Count ? sp.listedStarts : new int[0];
		}

		public List<VpExtra> RecentCompletedProfiles(int datastream, int count, int asOfBar)
		{
			var result = new List<VpExtra>();
			StreamPack sp;
			if (count <= 0 || m_streams == null || !m_streams.TryGetValue(datastream, out sp) || sp == null) return result;
			stampNakedPocs(sp);
			foreach (VpExtra e in sp.extra.Values)
				if (e != null && e.pocComplete && e.startBar >= 1 && e.endBar < asOfBar) result.Add(e);
			result.Sort((a, b) => b.startBar.CompareTo(a.startBar));   // newest first
			if (result.Count > count) result.RemoveRange(count, result.Count - count);
			return result;
		}

		/// <summary>
		/// Completed POCs on <paramref name="datastream"/> that no later bar has traded
		/// as of that stream's current calculation bar. Newest unfinished auction first.
		/// The developing profile is never included.
		/// </summary>
		public List<VpExtra> NakedPocs(int datastream)
		{
			return NakedPocs(datastream, streamItemBarIndex(datastream));
		}

		/// <summary>
		/// Completed POCs on <paramref name="datastream"/> still naked as of
		/// <paramref name="asOfBar"/> (1-based Status Bar #). Stamps as of the
		/// current calculation bar first, then answers the historical question
		/// from pocTouchedBar. Newest unfinished auction first.
		/// </summary>
		public List<VpExtra> NakedPocs(int datastream, int asOfBar)
		{
			List<VpExtra> hit = new List<VpExtra>();
			if (!TrackNakedPoc || m_streams == null || !m_streams.TryGetValue(datastream, out StreamPack sp) || sp == null)
				return hit;   // VPB-5: no naked POCs without naked tracking
			stampNakedPocs(sp);
			foreach (VpExtra extra in sp.extra.Values)
			{
				if (extra != null && extra.IsNakedPoc(asOfBar))
				{
					extra.crossesRoll = profileCrossesRoll(extra);   // VPF-4
					hit.Add(extra);
				}
			}
			hit.Sort((a, b) => b.startBar.CompareTo(a.startBar));
			return hit;
		}

		/// <summary>
		/// True when Data1's current chart bar is the first bar of a new volume profile.
		/// Current bar is FullSymbolData.Current (1-based Status Bar #).
		/// Do not pass Bars.CurrentBar — that is MaxBarsBack-relative.
		/// </summary>
		public bool IsNewVolumeProfile()
		{
			return IsNewVolumeProfile(1, streamItemBarIndex(1));
		}

		/// <summary>
		/// True when the given data stream's current chart bar is the first bar of a new volume profile.
		/// </summary>
		public bool IsNewVolumeProfile(int datastream)
		{
			return IsNewVolumeProfile(datastream, streamItemBarIndex(datastream));
		}

		/// <summary>
		/// True when the volume profile that owns <paramref name="barIndex"/> is newly
		/// forming — the previous chart bar belonged to a different profile.
		///
		/// <paramref name="barIndex"/> is 1-based (Status Bar # / FullSymbolData.Current).
		/// Detection is start(this bar) vs start(previous bar) in that same 1-based space.
		/// </summary>
		public bool IsNewVolumeProfile(int datastream, int barIndex)
		{
			if (barIndex < 1) return false;
			if (m_streams == null || !m_streams.TryGetValue(datastream, out StreamPack streamPack) || streamPack.pack == null)
			{
				if (traceOn(ETraceLevel.Verbose)) trace(ETraceLevel.Verbose, $"IsNewVolumeProfile: no volume profile collection on data stream {datastream}.");
				return false;
			}

			IProfile vp = itemForBar(streamPack, datastream, barIndex);
			if (vp == null)
			{
				if (traceOn(ETraceLevel.Verbose)) trace(ETraceLevel.Verbose, $"IsNewVolumeProfile: no profile on data stream {datastream} for bar {barIndex}.");
				return false;
			}

			if (barIndex == 1)
				return true;

			IProfile prev = itemForBar(streamPack, datastream, barIndex - 1);
			if (prev == null)
				return false;

			int start = profileStartBar(vp);
			int prevStart = profileStartBar(prev);
			if (start >= 1 && prevStart >= 1)
				return start != prevStart;

			return !object.ReferenceEquals(vp, prev);
		}

		/// <summary>
		/// All confluence zones on this bar, sorted by price (low → high).
		/// Rebuilds when the bar or the profile collection has changed.
		/// </summary>
		public List<ConfluenceZone> GetConfluenceZones()
		{
			ensureConfluenceZones();
			List<ConfluenceZone> copy = new List<ConfluenceZone>(m_confluenceZones);
			copy.Sort((a, b) => a.center.CompareTo(b.center));
			return copy;
		}

		/// <summary>
		/// Highest-ranked confluence zones using ConfluenceTopCount.
		/// Sorted by independent sources, then the role score, then raw score.
		/// </summary>
		public List<ConfluenceZone> GetTopConfluenceZones()
		{
			return GetTopConfluenceZones(ConfluenceTopCount);
		}

		/// <summary>
		/// Highest-ranked confluence zones, at most <paramref name="count"/>.
		/// Sorted by independent sources, then the role score, then raw score.
		/// </summary>
		public List<ConfluenceZone> GetTopConfluenceZones(int count)
		{
			ensureConfluenceZones();
			if (count <= 0 || m_confluenceZones.Count == 0)
				return new List<ConfluenceZone>();
			List<ConfluenceZone> copy = new List<ConfluenceZone>(m_confluenceZones);
			copy.Sort(compareZonesByRank);
			if (copy.Count > count)
				copy.RemoveRange(count, copy.Count - count);
			return copy;
		}

		/// <summary>
		/// High-probability confluence targets beyond <paramref name="price"/>
		/// in <paramref name="direction"/>. Uses ConfluenceTargetCount.
		/// Sorted by z-score, then raw score, then nearest first.
		/// </summary>
		public List<ConfluenceTarget> GetConfluenceTargets(double price, EVpDirection direction)
		{
			return GetConfluenceTargets(price, direction, ConfluenceTargetCount);
		}

		/// <summary>
		/// High-probability confluence targets beyond <paramref name="price"/>
		/// in <paramref name="direction"/>, at most <paramref name="count"/>.
		/// Bullish keeps zones fully above price; bearish keeps zones fully below.
		/// Zones the price is already inside are skipped.
		/// </summary>
		public List<ConfluenceTarget> GetConfluenceTargets(double price, EVpDirection direction, int count)
		{
			List<ConfluenceTarget> all = collectConfluenceTargets(price, direction);
			if (count <= 0 || all.Count == 0)
				return count <= 0 ? new List<ConfluenceTarget>() : all;
			if (all.Count > count)
				all.RemoveRange(count, all.Count - count);
			return all;
		}

		/// <summary>
		/// Same filter as GetConfluenceTargets but returns the zones themselves,
		/// still ordered as high-probability targets in that direction.
		/// </summary>
		public List<ConfluenceZone> GetConfluenceTargetZones(double price, EVpDirection direction)
		{
			return GetConfluenceTargetZones(price, direction, ConfluenceTargetCount);
		}

		/// <summary>
		/// Same filter as GetConfluenceTargets but returns the zones themselves.
		/// </summary>
		public List<ConfluenceZone> GetConfluenceTargetZones(double price, EVpDirection direction, int count)
		{
			List<ConfluenceTarget> targets = GetConfluenceTargets(price, direction, count);
			List<ConfluenceZone> zones = new List<ConfluenceZone>(targets.Count);
			for (int i = 0; i < targets.Count; i++)
				if (targets[i].zone != null) zones.Add(targets[i].zone);
			return zones;
		}

		/// <summary>
		/// Force a rebuild on the next GetConfluenceZones / GetTopConfluenceZones call.
		/// </summary>
		public void InvalidateConfluenceZones()
		{
			m_confluenceGeneration++;
			m_confluenceDirty = true;
		}

		/// <summary>
		/// Recompute ILevel volume percentiles per data stream
		/// (each stream ranked only against its own profiles).
		/// Called from CalcBar; also safe to call after a manual profile refresh.
		/// </summary>
		public void RefreshVolumePercentiles()
		{
			m_volumePctDirty = true;
			if (m_streams != null)
			{
				foreach (var kv in m_streams)
					if (kv.Value != null) kv.Value.percentilesStampedGeneration = -1;
			}
			refreshVolumePercentiles();
		}

		/// <summary>Slot 1 current VWAP, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwap1 { get { return m_vwap1 != null && m_vwap1.valid ? m_vwap1.value : double.NaN; } }
		/// <summary>Slot 2 current VWAP, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwap2 { get { return m_vwap2 != null && m_vwap2.valid ? m_vwap2.value : double.NaN; } }
		/// <summary>Slot 3 current VWAP, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwap3 { get { return m_vwap3 != null && m_vwap3.valid ? m_vwap3.value : double.NaN; } }
		/// <summary>Slot 1 running standard deviation, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwapSigma1 { get { return m_vwap1 != null && m_vwap1.valid ? m_vwap1.sigma : double.NaN; } }
		/// <summary>Slot 2 running standard deviation, or NaN when Disabled / not valid yet. Read-only.</summary>
		/// <summary>VPB-10: alias of CurrentVwapSigma1, named like CurrentVwap2Sigma / CurrentVwap3Sigma.</summary>
		public double CurrentVwap1Sigma { get { return CurrentVwapSigma1; } }
		public double CurrentVwap2Sigma { get { return m_vwap2 != null && m_vwap2.valid ? m_vwap2.sigma : double.NaN; } }
		/// <summary>Slot 3 running standard deviation, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwap3Sigma { get { return m_vwap3 != null && m_vwap3.valid ? m_vwap3.sigma : double.NaN; } }
		/// <summary>True when slot 1 is not Disabled and has a valid value. Read-only.</summary>
		public bool Vwap1Active { get { return m_vwap1 != null && m_vwap1.type != EVwapType.Disabled && m_vwap1.valid; } }
		/// <summary>True when slot 2 is not Disabled and has a valid value. Read-only.</summary>
		public bool Vwap2Active { get { return m_vwap2 != null && m_vwap2.type != EVwapType.Disabled && m_vwap2.valid; } }
		/// <summary>True when slot 3 is not Disabled and has a valid value. Read-only.</summary>
		public bool Vwap3Active { get { return m_vwap3 != null && m_vwap3.type != EVwapType.Disabled && m_vwap3.valid; } }
		/// <summary>Chart stream slot 1 was built from (1 = Data1). 0 if unused. Read-only.</summary>
		public int CurrentVwap1Stream { get { return slotStream(m_vwap1); } }
		/// <summary>Chart stream slot 2 was built from (1 = Data1). 0 if unused. Read-only.</summary>
		public int CurrentVwap2Stream { get { return slotStream(m_vwap2); } }
		/// <summary>Chart stream slot 3 was built from (1 = Data1). 0 if unused. Read-only.</summary>
		public int CurrentVwap3Stream { get { return slotStream(m_vwap3); } }

		//methods
		private static int slotStream(_VpVwapEngine eng)
		{
			if (eng == null || eng.type == EVwapType.Disabled) return 0;
			return eng.datastream;
		}

		private void warnIfLengthExceedsMaxBarsBack(_VpVwapEngine eng)
		{
			if (eng == null || eng.type != EVwapType.Length) return;
			if (eng.length - 1 > ExecInfo.MaxBarsBack)
				trace(ETraceLevel.Warning, eng.label + ": Length " + eng.length + " needs MaxBarsBack >= " + (eng.length - 1) +
							" (currently " + ExecInfo.MaxBarsBack + "). Use auto-detect or raise it.");
		}

		// PR #47: lazy. A disabled slot keeps no engine (null; every reader null-checks); an enabled one is created on first use.
		private _VpVwapEngine configureVwap(_VpVwapEngine eng, string label, EVwapType type, int length, string fromText, int stream, string dateInputName, string streamInputName)
		{
			if (type == EVwapType.Disabled) return null;
			if (eng == null) eng = new _VpVwapEngine { label = label };
			eng.parent = this;
			eng.type = type;
			eng.length = length;
			eng.datastream = 0;
			eng.fromDate = DateTime.MinValue;
			eng.Reset();

			eng.datastream = stream;
			eng.resetPeriodOnRoll = VwapConfig.resetPeriodSlotsOnRoll;
			if (type != EVwapType.AdaptiveFromDate) return eng;

			if (!tryParseDate(fromText, out DateTime dt))
				throw new ArgumentException(dateInputName + " must be a date string (yyyy-MM-dd or yyyy-MM-dd HH:mm[:ss]) when Type is AdaptiveFromDate.");
			eng.fromDate = dt;
			return eng;
		}

		private void stepVwap(_VpVwapEngine eng)
		{
			if (eng == null || eng.type == EVwapType.Disabled)
			{
				if (eng != null) { eng.valid = false; eng.value = double.NaN; eng.sigma = double.NaN; }
				return;
			}

			IInstrument bars = barsOf(eng.datastream);

			if (bars == null)
			{
				eng.valid = false;
				eng.value = double.NaN;
				eng.sigma = double.NaN;
				return;
			}

			// only update the VWAP state when the bar actually closes
			// otherwise we double count bars
			if (bars.Status == EBarState.Close)
				eng.Step(bars);
		}

		private IInstrument barsOf(int stream)
		{
			if (stream <= 1) return Bars;
			if (stream > MaxDataStream) return null;
			return BarsOfData(stream);
		}

		/// <summary>
		/// 1-based chart bar for this stream's current calculation bar.
		/// FullSymbolData.Current (Status Bar #). CurrentBar is MaxBarsBack-relative and is not used.
		/// </summary>
		internal int streamItemBarIndex(int stream)
		{
			IInstrument bars = barsOf(stream);
			if (bars == null) return -1;
			try
			{
				int n = bars.FullSymbolData.Current;
				if (n >= 1) return n;
			}
			catch { }
			return -1;
		}

		/// <summary>Native IProfilesCollection.ItemForBar argument. MC is 0-based.</summary>
		internal static int toNativeItemBar(int chartBar)
		{
			return chartBar < 1 ? -1 : chartBar - 1;
		}

		/// <summary>Status Bar # / public API index from a native 0-based ItemForBar / BarsInterval value.</summary>
		private static int toChartBar(int nativeBar)
		{
			return nativeBar < 0 ? nativeBar : nativeBar + 1;
		}

		private void stampCurrentExtras()
		{
			foreach (var kv in m_streams)
				syncStreamExtras(kv.Value);
		}

		/// <summary>
		/// Create / refresh VpExtra for the live profile on this stream (and any
		/// other profiles already in the collection). Must run from CalcBar so
		/// extras exist on the first bar of an every-N-bar window, before EChanged.
		/// </summary>
		private void syncStreamExtras(StreamPack sp)
		{
			if (sp == null || sp.pack == null) return;

			int chartBar = streamItemBarIndex(sp.datastream);
			if (chartBar >= 1)
			{
				IProfile cur = itemForBar(sp, sp.datastream, chartBar);
				if (chartBar > 1)
				{
					// First bar of a new window: give the just-closed profile its final fill.
					IProfile prev = itemForBar(sp, sp.datastream, chartBar - 1);
					if (prev != null && (cur == null || profileStartBar(prev) != profileStartBar(cur)))
						ensureExtra(sp, prev, chartBar - 1);
				}
				if (cur != null)
					ensureExtra(sp, cur, chartBar);   // last: leaves the developing extra stamped as of chartBar
			}
			stampNakedPocs(sp);
		}

		/// <summary>
		/// Stamp pocComplete / pocNaked / pocTouchedBar on extras already stored
		/// for this stream. As-of bound is streamItemBarIndex, never FullSymbolData.Count.
		/// A gap through the POC does not mitigate it; the bar range must cover poc.
		/// Untouched scans start at max(endBar + 1, lastNakedScanBar + 1). A touch already
		/// in (endBar, current] is not rescanned. Scan order does not change the result,
		/// so extras are not sorted. TrackNakedPoc false clears once per stream until the
		/// flag turns back on. In-window trade does not mitigate.
		/// After this returns, pocNaked equals IsNakedPoc(streamItemBarIndex).
		/// Bars outside MaxBarsBack return 0 from barPrice and are not a fill, so a
		/// POC older than MaxBarsBack can stay naked even if dropped history traded it.
		/// </summary>
		private void stampNakedPocs(StreamPack sp)
		{
			if (sp == null || sp.extra == null) return;
			int current = streamItemBarIndex(sp.datastream);
			if (current < 1) return;
			IInstrument bars = barsOf(sp.datastream);

			// VPB-16: called from CalcBar and from every query; stamp once per exact state. ensureExtra invalidates
			// the stamp whenever it (re)fills or re-stamps a developing extra, so each CalcBar tick still rescans.
			bool atClose = bars == null || bars.Status == EBarState.Close;
			if (sp.nakedStampedBar == current && sp.nakedStampedGeneration == sp.generation
				&& sp.nakedStampedExtraCount == sp.extra.Count && (sp.nakedStampedAtClose || !atClose)
				&& sp.nakedSuppressed == !TrackNakedPoc)
				return;

			if (!TrackNakedPoc)
			{
				// VPB-5 (D4): completeness is a fact about the window and is always stamped; only the touch scan
				// (pocNaked / pocTouchedBar / IsNakedPoc / NakedPocs) is switched off.
				foreach (VpExtra extra in sp.extra.Values)
				{
					if (extra == null) continue;
					clearNakedPoc(extra);
					extra.pocComplete = extra.poc > 0 && extra.endBar >= extra.startBar && extra.endBar < current;
				}
				sp.nakedSuppressed = true;
				sp.nakedStampedBar = current;
				sp.nakedStampedAtClose = atClose;
				sp.nakedStampedGeneration = sp.generation;
				sp.nakedStampedExtraCount = sp.extra.Count;
				return;
			}
			sp.nakedSuppressed = false;

			double tick = tickSize();
			// Half a micro-tick of slack so an exact touch is never lost to rounding.
			double tol = tick > 0 ? NakedPocTickTolerance * tick + tick * 1e-6 : PRICE_EPSILON;

			foreach (VpExtra extra in sp.extra.Values)
			{
				if (extra == null) continue;
				extra.nakedTracked = true;
				if (extra.poc <= 0)
				{
					clearNakedPoc(extra);
					continue;
				}

				bool complete = extra.endBar >= extra.startBar && extra.endBar < current;
				extra.pocComplete = complete;
				if (!complete)
				{
					extra.pocNaked = false;
					extra.pocTouchedBar = 0;
					extra.lastNakedScanBar = 0;
					continue;
				}

				int touched = extra.pocTouchedBar;
				if (touched <= extra.endBar || touched > current)
					touched = 0;
				if (touched == 0)
				{
					double poc = extra.poc;
					// Cursor, not a stored naked flag. A later IsNakedPoc(asOfBar) still
					// answers from pocTouchedBar, so history before the cursor stays valid.
					int from = extra.endBar + 1;
					if (extra.lastNakedScanBar + 1 > from)
						from = extra.lastNakedScanBar + 1;
					for (int b = from; b <= current; b++)
					{
						double h = barPrice(bars, current, b, 1);
						double l = barPrice(bars, current, b, 2);
						if (h <= 0 || l <= 0) continue;
						if (h >= poc - tol && l <= poc + tol)
						{
							touched = b;
							break;
						}
					}
					// Only bars that can no longer change are final; the forming bar is rescanned next call.
					bool currentClosed = bars != null && bars.Status == EBarState.Close;
					int lastFinal = currentClosed ? current : current - 1;
					if (lastFinal > extra.lastNakedScanBar) extra.lastNakedScanBar = lastFinal;
				}
				extra.pocTouchedBar = touched;
				extra.pocNaked = touched == 0;
			}
			sp.nakedStampedBar = current;
			sp.nakedStampedAtClose = atClose;
			sp.nakedStampedGeneration = sp.generation;
			sp.nakedStampedExtraCount = sp.extra.Count;
		}

		private static void clearNakedPoc(VpExtra extra)
		{
			if (extra == null) return;
			extra.pocComplete = false;
			extra.pocNaked = false;
			extra.pocTouchedBar = 0;
			extra.lastNakedScanBar = 0;
			extra.nakedTracked = false;
		}

		// Read-only lookup (VPB-28: the old extraFor also re-ran applyProfileGeometry as of the queried bar, which rewrote
		// a closed profile's POC/VA to an interior, developing value).
		private VpExtra lookupExtra(StreamPack sp, int datastream, IProfile vp)
		{
			if (sp == null || vp == null) return null;
			VpKey key = new VpKey(datastream, vp);
			VpExtra extra;
			if (!sp.extra.TryGetValue(key, out extra) && key.startBar >= 1)
				sp.extra.TryGetValue(new VpKey(datastream, key.startBar), out extra);
			return extra;
		}

		private VpExtra extraForStart(StreamPack sp, int datastream, int startBar)
		{
			if (sp == null) return null;
			VpExtra extra = null;
			if (startBar >= 1)
				sp.extra.TryGetValue(new VpKey(datastream, startBar), out extra);
			if (extra == null && startBar > 1)
				sp.extra.TryGetValue(new VpKey(datastream, startBar - 1), out extra);
			if (extra == null && startBar >= 1)
				sp.extra.TryGetValue(new VpKey(datastream, startBar + 1), out extra);
			return extra;   // read-only (VPB-28)
		}

		private void stampExtra(VpExtra extra)
		{
			if (extra == null) return;
			extra.vwap1 = m_vwap1 != null && m_vwap1.valid ? m_vwap1.value : 0;
			extra.vwap2 = m_vwap2 != null && m_vwap2.valid ? m_vwap2.value : 0;
			extra.vwap3 = m_vwap3 != null && m_vwap3.valid ? m_vwap3.value : 0;
			extra.vwap1Sigma = m_vwap1 != null && m_vwap1.valid ? m_vwap1.sigma : 0;
			extra.vwap2Sigma = m_vwap2 != null && m_vwap2.valid ? m_vwap2.sigma : 0;
			extra.vwap3Sigma = m_vwap3 != null && m_vwap3.valid ? m_vwap3.sigma : 0;
			extra.vwap1Stream = slotStream(m_vwap1);
			extra.vwap2Stream = slotStream(m_vwap2);
			extra.vwap3Stream = slotStream(m_vwap3);
		}

		private static bool tryParseDate(string text, out DateTime dt)
		{
			dt = DateTime.MinValue;
			if (string.IsNullOrWhiteSpace(text)) return false;
			string s = text.Trim();
			string[] formats =
			{
				"yyyy-MM-dd",
				"yyyy-MM-dd HH:mm",
				"yyyy-MM-dd HH:mm:ss",
				"yyyy/MM/dd",
				"yyyy/MM/dd HH:mm",
				"yyyy/MM/dd HH:mm:ss",
				"MM/dd/yyyy",
				"MM/dd/yyyy HH:mm",
				"MM/dd/yyyy HH:mm:ss"
			};
			return DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt)
				|| DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt);
		}

		private IProfilesCollection getPack(int stream)
		{
			try
			{
				if (stream <= 1) return this.VolumeProfile;
				return VolumeProfileByDataStream(stream);
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Profile that owns <paramref name="barIndex"/> (1-based Status Bar # /
		/// FullSymbolData.Current). A single ItemForBar call is not trusted.
		///
		/// Two independent failures show up in MultiCharts: the native ItemForBar
		/// index is 0-based on some builds and the 1-based Status index on others,
		/// and at a window edge ItemForBar often returns the neighbor profile or
		/// null — especially on the first bar of a new profile, and while the live
		/// profile is still developing (BarsInterval.Second walks forward, so the
		/// end bar is not a stable identity).
		///
		/// Probes ItemForBar at bar, bar-1, and bar+1, then every profile in
		/// pack.Items (that scan does not depend on ItemForBar's index).
		/// considerProfile keeps a hit only if BarsInterval contains the bar, and
		/// among those keeps the start closest to barIndex. Closest-start is what
		/// IsNewVolumeProfile needs: start(bar) vs start(bar-1). The previous
		/// window winning on the first bar of a new profile would miss the event
		/// or fire it a bar late.
		///
		/// If nothing passes ownership (BarsInterval threw, or start and end were
		/// both unusable), returns the raw ItemForBar hit anyway — exact bar, then
		/// previous, then next. That is better than null, not an ownership guarantee.
		///
		/// The cache is only a generation-scoped memo of that resolution (VPB-17): bars before the
		/// calc bar go into a dictionary (backward walks such as DER-06 no longer evict the CalcBar
		/// answers), the calc bar itself into a single slot. A generation bump (profile collection
		/// changed) drops the memo; the dictionary is also cleared past MAX_RESOLVED_CACHE entries.
		/// The bar+1 probe is still part of the cold resolution; do not remove it.
		/// </summary>
		/// <param name="barIndex">1-based chart bar (Status Bar # / FullSymbolData.Current).</param>
		private IProfile itemForBar(StreamPack streamPack, int datastream, int barIndex)
		{
			if (streamPack == null || streamPack.pack == null || barIndex < 1) return null;
			if (streamPack.resolveGeneration != streamPack.generation || streamPack.resolved.Count > MAX_RESOLVED_CACHE)
			{
				streamPack.resolveGeneration = streamPack.generation;
				streamPack.resolved.Clear();
				streamPack.cachedBarA = -1;
				streamPack.cachedProfileA = null;
			}

			IProfile hit;
			int current = streamItemBarIndex(datastream);
			if (current >= 1 && barIndex < current)
			{
				if (streamPack.resolved.TryGetValue(barIndex, out hit)) return hit;
				hit = resolveItemForBar(streamPack, datastream, barIndex);
				streamPack.resolved[barIndex] = hit;
				return hit;
			}

			if (barIndex == streamPack.cachedBarA) return streamPack.cachedProfileA;
			hit = resolveItemForBar(streamPack, datastream, barIndex);
			streamPack.cachedBarA = barIndex;
			streamPack.cachedProfileA = hit;
			return hit;
		}

		private IProfile resolveItemForBar(StreamPack streamPack, int datastream, int barIndex)
		{
			IProfile best = null;
			int bestDist = int.MaxValue;
			considerProfile(rawItemForBar(streamPack, datastream, barIndex), barIndex, ref best, ref bestDist);
			if (barIndex > 1)
				considerProfile(rawItemForBar(streamPack, datastream, barIndex - 1), barIndex, ref best, ref bestDist);
			considerProfile(rawItemForBar(streamPack, datastream, barIndex + 1), barIndex, ref best, ref bestDist);

			// VPB-17: the closest owning start is the last listed start <= barIndex (windows on one stream are
			// sequential), so only look at that neighbourhood; equal starts are walked back over. Fall back to the
			// full scan only when nothing owns the bar (gaps, profiles whose BarsInterval could not be read).
			List<IProfile> listed = listedProfiles(streamPack);
			int[] starts = streamPack.listedStarts;
			if (starts.Length == listed.Count && listed.Count > 0)
			{
				int idx = Array.BinarySearch(starts, barIndex);
				if (idx < 0) idx = ~idx - 1;   // last start <= barIndex (-1 when all starts are later)
				if (idx >= 0)
				{
					int lo = idx;
					while (lo > 0 && starts[lo - 1] == starts[idx]) lo--;
					while (idx + 1 < starts.Length && starts[idx + 1] == starts[lo]) idx++;
					for (int i = Math.Max(0, lo - 1); i <= Math.Min(listed.Count - 1, idx + 1); i++)
						considerProfile(listed[i], barIndex, ref best, ref bestDist);
				}
			}
			if (best == null)
			{
				for (int i = 0; i < listed.Count; i++)
					considerProfile(listed[i], barIndex, ref best, ref bestDist);
			}

			if (best != null) return best;

			return rawItemForBar(streamPack, datastream, barIndex)
				?? (barIndex > 1 ? rawItemForBar(streamPack, datastream, barIndex - 1) : null)
				?? rawItemForBar(streamPack, datastream, barIndex + 1);
		}

		/// <summary>
		/// Ownership filter for itemForBar. Drops a null hit and any profile whose
		/// BarsInterval does not contain barIndex. Among owners, keeps the one whose
		/// start is closest to barIndex so an off-by-one neighbor (previous window
		/// or the next developing window) does not win on the boundary bar.
		/// </summary>
		private static void considerProfile(IProfile vp, int barIndex, ref IProfile best, ref int bestDist)
		{
			if (vp == null || !profileOwnsBar(vp, barIndex)) return;
			int start = profileStartBar(vp);
			int dist = start >= 1 ? Math.Abs(start - barIndex) : int.MaxValue / 2;
			if (dist < bestDist)
			{
				best = vp;
				bestDist = dist;
			}
		}

		/// <summary>
		/// Unfiltered ItemForBar for a 1-based chart bar, probed at the native 0-based index (barIndex - 1) only.
		/// Does not check that the returned profile owns the bar — itemForBar / considerProfile do that.
		/// VPB-21: bars after the stream's calc bar are never probed (they throw in real time and are future
		/// objects in a backtest). The old 1-based retry is gone: BarsInterval.First is confirmed correct (VPB-07).
		/// </summary>
		/// <param name="barIndex">1-based chart bar. Converted to native 0-based for pack.ItemForBar.</param>
		private IProfile rawItemForBar(StreamPack streamPack, int datastream, int barIndex)
		{
			if (streamPack == null || streamPack.pack == null || barIndex < 1) return null;
			int current = streamItemBarIndex(datastream);
			if (current >= 1 && barIndex > current) return null;   // not built yet / future: never probe
			int native = toNativeItemBar(barIndex);
			return rawNativeItemForBar(streamPack, datastream, native);
		}

		/// <summary>
		/// pack.ItemForBar(nativeBar). Swallows the exception MultiCharts throws when
		/// the index is out of range, the pack is disposed, or no profile is registered
		/// for that bar yet. rawItemForBar passes the native 0-based index.
		/// </summary>
		/// <param name="nativeBar">Index passed straight through to ItemForBar.</param>
		private IProfile rawNativeItemForBar(StreamPack streamPack, int datastream, int nativeBar)
		{
			if (streamPack == null || streamPack.pack == null || nativeBar < 0) return null;
			try
			{
				return streamPack.pack.ItemForBar(nativeBar);
			}
			catch (Exception e)
			{
				if (traceOn(ETraceLevel.Verbose)) trace(ETraceLevel.Verbose, $"Exception trying to find volume profile on data stream {datastream} for bar {nativeBar} - {e.Message}");
				return null;
			}
		}

		// The old four range checks were the same test written four ways. Start/end repair
		// is unchanged: a missing start borrows the end, a missing or inverted end borrows the start.
		private static bool profileOwnsBar(IProfile vp, int barIndex)
		{
			if (vp == null || barIndex < 1) return false;
			int start = profileStartBar(vp);
			int end = profileEndBar(vp);
			if (start < 1 && end < 1) return false;
			if (start < 1) start = end;
			if (end < 1 || end < start) end = start;

			return barIndex >= start && barIndex <= end;
		}

		/// <summary>1-based start of the profile window (Status Bar #).</summary>
		private static int profileStartBar(IProfile vp)
		{
			if (vp == null) return -1;
			try
			{
				return toChartBar(vp.BarsInterval.First);
			}
			catch
			{
				return -1;
			}
		}

		/// <summary>1-based end of the profile window (Status Bar #).</summary>
		private static int profileEndBar(IProfile vp)
		{
			if (vp == null) return -1;
			try
			{
				return toChartBar(vp.BarsInterval.Second);
			}
			catch
			{
				return -1;
			}
		}

		private List<ConfluenceTarget> collectConfluenceTargets(double price, EVpDirection direction)
		{
			ensureConfluenceZones();
			List<ConfluenceTarget> list = new List<ConfluenceTarget>();
			if (price <= 0 || m_confluenceZones.Count == 0) return list;

			int dir = direction == EVpDirection.Bearish ? -1 : 1;
			int clearTicks = ConfluenceTargetClearTicks;
			double tick = tickSize();
			double minZ = ConfluenceTargetMinZScore;

			for (int i = 0; i < m_confluenceZones.Count; i++)
			{
				ConfluenceZone zone = m_confluenceZones[i];
				if (zone == null || zone.center <= 0) continue;
				if (zone.role == EConfluenceRole.Rejection || zone.suggestedUse != ESuggestedUse.Target) continue;
				if (zone.zScore < minZ) continue;
				if (!zoneClearsPrice(zone, price, dir, clearTicks, tick)) continue;

				double magnet = zone.anchor > 0 ? zone.anchor : zone.center;
				double touch = dir > 0 ? zone.low : zone.high;
				if (touch <= 0) touch = magnet;
				list.Add(new ConfluenceTarget
				{
					price = magnet,
					touch = touch,
					distance = Math.Abs(magnet - price),
					direction = dir > 0 ? EVpDirection.Bullish : EVpDirection.Bearish,
					zone = zone
				});
			}
			list.Sort(compareTargetsByRank);
			return list;
		}

		private static bool zoneClearsPrice(ConfluenceZone zone, double price, int dir, int clearTicks, double tick)
		{
			if (tick <= 0) tick = FALLBACK_TICK;
			// Compare in whole ticks: robust to 1-ULP noise in derived zone edges.
			if (dir > 0)
				return Math.Round((zone.low - price) / tick) >= clearTicks;
			return Math.Round((price - zone.high) / tick) >= clearTicks;
		}

		private static int compareTargetsByRank(ConfluenceTarget a, ConfluenceTarget b)
		{
			if (a == null && b == null) return 0;
			if (a == null) return 1;
			if (b == null) return -1;
			ConfluenceZone za = a.zone;
			ConfluenceZone zb = b.zone;
			int z = compareZonesByRank(za ?? new ConfluenceZone(), zb ?? new ConfluenceZone());
			if (z != 0) return z;
			return a.distance.CompareTo(b.distance);
		}

		private static int compareZonesByRank(ConfluenceZone a, ConfluenceZone b)
		{
			if (a == null && b == null) return 0;
			if (a == null) return 1;
			if (b == null) return -1;
			int s = b.independentSources.CompareTo(a.independentSources);
			if (s != 0) return s;
			double sb = b.role == EConfluenceRole.Rejection ? b.rejectionScore : b.acceptanceScore;
			double sa = a.role == EConfluenceRole.Rejection ? a.rejectionScore : a.acceptanceScore;
			if (b.role == EConfluenceRole.Mixed) sb = Math.Max(b.acceptanceScore, b.rejectionScore);
			if (a.role == EConfluenceRole.Mixed) sa = Math.Max(a.acceptanceScore, a.rejectionScore);
			int r = sb.CompareTo(sa);
			if (r != 0) return r;
			return b.rawScore.CompareTo(a.rawScore);
		}

		// Rebuild when CalcBar marked dirty, when Invalidate / EChanged bumped the generation,
		// or when any enabled confluence stream's Status bar changed. A disabled slot (stream
		// <= 0, the default for streams 2 and 3) contributes int.MinValue and does not force a rebuild.
		// Do not gate this on Bars.CurrentBar.
		private void ensureConfluenceZones()
		{
			int b1 = confluenceStreamBar(ConfluenceDataStream1);
			int b2 = confluenceStreamBar(ConfluenceDataStream2);
			int b3 = confluenceStreamBar(ConfluenceDataStream3);
			bool sameBars = b1 == m_confluenceBuiltBar1 && b2 == m_confluenceBuiltBar2 && b3 == m_confluenceBuiltBar3;
			if (!m_confluenceDirty && m_confluenceBuiltGeneration == m_confluenceGeneration && sameBars)
				return;
			if (m_confluenceEngine == null)
			{
				m_confluenceEngine = new _VpConfluenceEngine(this);
				if (!UsesConfluenceZones && !m_confluenceAtrWarned && ConfluenceSplitAtrFraction > 0 && traceOn(ETraceLevel.Warning))
				{
					m_confluenceAtrWarned = true;
					trace(ETraceLevel.Warning, "VPCONF: confluence zones used but UsesConfluenceZones is false; no M-DAILY, split uses the 4 x tol limit");
				}
			}
			m_confluenceEngine.Rebuild();
			m_confluenceDirty = false;
			m_confluenceBuiltGeneration = m_confluenceGeneration;
			m_confluenceBuiltBar1 = b1;
			m_confluenceBuiltBar2 = b2;
			m_confluenceBuiltBar3 = b3;
		}

		private int confluenceStreamBar(int datastream)
		{
			if (datastream < 1) return int.MinValue;
			return streamItemBarIndex(datastream);
		}

		/// <summary>
		/// Lookback extras for one confluence stream, age 0 = the profile that owns the
		/// stream's current bar. The walk matches the old engine loop (itemForBar, then
		/// start-1, stop on a repeated start). ensureExtra is allowed here because this
		/// is the base; the engine only reads the list. Naked POC is stamped once after
		/// the walk, not once per profile. Histogram fill still freezes closed profiles.
		/// </summary>
		internal List<VpExtra> confluenceLookbackExtras(int datastream)
		{
			List<VpExtra> list = new List<VpExtra>();
			if (datastream < 1 || m_streams == null) return list;
			if (!m_streams.TryGetValue(datastream, out StreamPack sp) || sp == null || sp.pack == null)
				return list;
			int bar = streamItemBarIndex(datastream);
			if (bar < 1) return list;
			int take = ConfluenceLookbackProfiles;
			var seen = new HashSet<int>();
			for (int age = 0; age < take && bar >= 1; age++)
			{
				IProfile vp = itemForBar(sp, datastream, bar);
				if (vp == null) break;
				int start = profileStartBar(vp);
				if (start < 1) start = bar;
				if (!seen.Add(start)) break;
				ensureExtra(sp, vp, bar);
				VpKey key = new VpKey(datastream, vp);
				VpExtra extra;
				if (!sp.extra.TryGetValue(key, out extra) || extra == null)
					sp.extra.TryGetValue(new VpKey(datastream, start), out extra);
				if (extra != null)
					list.Add(extra);
				bar = start - 1;
			}
			stampNakedPocs(sp);
			return list;
		}

		// Warm generation: return the cached Items snapshot. itemForBar must not walk
		// pack.Items again until onVpChanged or StartCalc bumps the generation.
		private static List<IProfile> listedProfiles(StreamPack sp)
		{
			if (sp == null || sp.pack == null) return new List<IProfile>();
			if (sp.listedGeneration == sp.generation && sp.listedCache != null)
				return sp.listedCache;
			List<IProfile> list = new List<IProfile>();
			try
			{
				if (sp.pack.Items == null)
				{
					sp.listedCache = list;
					sp.listedStarts = new int[0];
					sp.listedGeneration = sp.generation;
					return list;
				}
			}
			catch
			{
				// Items can be non-null but already disposed / unusable.
				sp.listedCache = list;
				sp.listedStarts = new int[0];   // length mismatch: resolveItemForBar falls back to the full scan
				sp.listedGeneration = sp.generation;
				return list;
			}

			int empties = 0;
			try
			{
				foreach (IProfile vp in sp.pack.Items)
				{
					if (vp == null) continue;
					if (vp.Empty) { empties++; continue; }
					list.Add(vp);
				}
			}
			catch
			{
				sp.listedCache = list;
				sp.listedStarts = new int[0];   // length mismatch: resolveItemForBar falls back to the full scan
				sp.listedGeneration = sp.generation;
				return list;
			}
			sp.emptySkipped = empties;
			list.Sort((a, b) => profileStartBar(a).CompareTo(profileStartBar(b)));
			var starts = new int[list.Count];
			for (int i = 0; i < list.Count; i++) starts[i] = profileStartBar(list[i]);
			sp.listedStarts = starts;
			sp.listedCache = list;
			sp.listedGeneration = sp.generation;
			return list;
		}

		/// <summary>
		/// Chart time of a 1-based Status Bar on a stream. DateTime.MinValue when the
		/// bar is outside MaxBarsBack. Used only to test profile-window containment.
		/// </summary>
		/// <summary>VPB-9: tickSize() for the helper-file engines (tickSize is protected on _CustomSignal).</summary>
		internal double helperTickSize() { return tickSize(); }

		internal DateTime barTime(int stream, int chartBar)
		{
			IInstrument bars = barsOf(stream);
			if (bars == null || chartBar < 1) return DateTime.MinValue;
			int current = streamItemBarIndex(stream);
			if (current < 1 || chartBar > current) return DateTime.MinValue;
			int ago = current - chartBar;
			if (ago > ExecInfo.MaxBarsBack) return DateTime.MinValue;
			return bars.Time[ago];
		}

		// tickSize() (VPB-20) lives in _CustomSignal since spec CS-1 (the journal needs ticks); same resolution and cache.

		private bool nodeWideEnough(double low, double high)
		{
			int ticks = MinNodeTicks;
			double need = ticks * tickSize();
			if (need <= 0) return true;
			return (high - low) + PRICE_EPSILON >= need;
		}

		// LELX no-profiles diagnosis: every hook outcome is logged; Data1 not connected is a Warning (it used to be silent).
		private void hookStreams()
		{
			if (m_streams == null) m_streams = new Dictionary<int, StreamPack>();
			m_streams.Clear();
			for (int s = 1; s <= MaxDataStream; ++s)
			{
				IProfilesCollection pack = getPack(s);
				if (pack == null || !pack.Visible)
				{
					// VP not enabled on this stream
					string why = pack == null ? "no profile" : "hidden";
					if (s == 1)
						trace(ETraceLevel.Warning, $"VP hook Data1 not connected ({why}): no visible MultiCharts volume profile on Data1; completed-profile targets disabled");
					else if (traceOn(ETraceLevel.Info))
						trace(ETraceLevel.Info, $"VP hook Data{s} not connected ({why})");
					continue;
				}
				if (traceOn(ETraceLevel.Info))
				{
					int count = -1;
					try { count = pack.Items == null ? 0 : pack.Items.Count; } catch { count = -1; }
					trace(ETraceLevel.Info, $"VP hook Data{s} connected profiles({count})");
				}
				StreamPack sp = new StreamPack(s);
				sp.pack = pack;
				sp.handler = delegate (bool full) { onVpChanged(sp, full); };
				pack.EChanged += sp.handler;
				m_streams.Add(s, sp);
			}
		}

		private bool m_hookRetried;
		private bool m_zeroVolumeWarned;
		private int m_zeroVolumeBars;
		private bool m_vpDiagSummaryDone;

		// First calc bar: retry the hook once when Data1 was not connected at StartCalc (VP load race). Closed Data1 bars with zero
		// volume are counted (Warning on the first); on the last bar one Info line reports empty profiles skipped and the count.
		private void vpConnectionDiagnostics()
		{
			if (!m_hookRetried)
			{
				m_hookRetried = true;
				if (!IsProfileStreamConnected(1))
				{
					unhookStreams();
					hookStreams();
					trace(IsProfileStreamConnected(1) ? ETraceLevel.Info : ETraceLevel.Warning, $"VP hook retry on first calc bar: Data1 connected({IsProfileStreamConnected(1)})");
				}
			}
			if (Bars.Status == EBarState.Close && Bars.Volume[0] == 0)
			{
				m_zeroVolumeBars++;
				if (!m_zeroVolumeWarned)
				{
					m_zeroVolumeWarned = true;
					trace(ETraceLevel.Warning, $"Data1 closed bar has zero volume (bar {Bars.FullSymbolData.Current}): check Build Volume On / feed volume; profiles may be empty");
				}
			}
			if (!m_vpDiagSummaryDone && Bars.LastBarOnChart && Bars.Status == EBarState.Close)
			{
				m_vpDiagSummaryDone = true;
				if (traceOn(ETraceLevel.Info))
				{
					var sb = new System.Text.StringBuilder();
					if (m_streams != null)
						foreach (var kv in m_streams)
							if (kv.Value != null) { listedProfiles(kv.Value); sb.Append($" Data{kv.Key}(listed {kv.Value.listedStarts.Length}, empty skipped {kv.Value.emptySkipped})"); }
					trace(ETraceLevel.Info, $"VP run summary: connected streams({(m_streams == null ? 0 : m_streams.Count)}){sb} zeroVolumeClosedBars({m_zeroVolumeBars})");
				}
			}
		}

		private void unhookStreams()
		{
			if (m_streams == null) return;
			foreach (var stream in m_streams)
			{
				if (stream.Value.pack == null || stream.Value.handler == null) continue;
				try { stream.Value.pack.EChanged -= stream.Value.handler; } catch { }
			}
		}

		// EChanged may arrive off the calculation thread: only flag the stream here; the generation bump and the
		// rebuild run in processPendingProfileChanges at the top of CalcBar (VPB-09).
		private void onVpChanged(StreamPack sp, bool full)
		{
			if (full) sp.pendingFull = true;
			sp.pendingChange = true;
		}

		// Runs on the calculation thread at the top of CalcBar.
		private void processPendingProfileChanges()
		{
			foreach (var kv in m_streams)
			{
				StreamPack sp = kv.Value;
				if (sp == null || !sp.pendingChange) continue;
				bool full = sp.pendingFull;
				sp.pendingChange = false;
				sp.pendingFull = false;
				applyVpChanged(sp, full);
			}
		}

		private void applyVpChanged(StreamPack sp, bool full)
		{
			if (sp.pack == null) return;
			sp.bumpGeneration();

			sp.closedCount = 0; sp.closedLevelsGeneration++; sp.closedStampedGeneration = -1;
			foreach (VpExtra e in sp.extra.Values) if (e != null) { e.inClosedSet = false; e.pctStampedFill = -1; }

			if (full)
			{
				sp.extra.Clear();   // objects behind old VpKeys are gone, below code will recompute them for a full refresh
				sp.levelsGeneration++;
			}
			try
			{
				if (sp.pack.Items == null) return;
				foreach (IProfile vp in sp.pack.Items)
					ensureExtra(sp, vp);
			}
			catch { }
			m_confluenceGeneration++;
			m_confluenceDirty = true;
			m_volumePctDirty = true;
		}

		private void ensureExtra(StreamPack sp, IProfile vp)
		{
			ensureExtra(sp, vp, 0);
		}

		private void ensureExtra(StreamPack sp, IProfile vp, int chartBar)
		{
			if (sp == null || vp == null) return;
			VpKey key = new VpKey(sp.datastream, vp);
			if (key.startBar < 0) return;
			int current = streamItemBarIndex(sp.datastream);
			if (current >= 1 && key.startBar > current) return;   // starts after the calc bar: does not exist yet
			VpExtra extra;
			if (!sp.extra.TryGetValue(key, out extra))
			{
				extra = new VpExtra();
				sp.extra[key] = extra;
			}
			extra.dataStream = sp.datastream;
			bool empty = false;
			try { empty = vp.Empty; } catch { empty = true; }
			if (!empty)
			{
				// Closed profile whose histogram was built for this startBar and this
				// generation: do not clear and rebuild. endBar < current is the closed test,
				// so the developing profile (endBar walks with the calc bar) still fills.
				// A generation bump fails the generation check and rebuilds, which also
				// refreshes POC / VAH / VAL. VWAP is still stamped below either way.
				bool closed = current >= 1
					&& extra.endBar >= extra.startBar
					&& extra.endBar < current
					&& extra.poc > 0
					&& extra.nodesBuilt
					&& extra.nodesBuiltForStart == extra.startBar
					&& extra.nodesBuiltGeneration == sp.generation
					&& extra.levels.Count > 0;
				if (!closed)
				{
					sp.nakedStampedBar = -1;   // VPB-16: poc / endBar of this extra may change below
					bool atClose = isStreamBarClosed(sp.datastream);
					bool sameStamp = extra.filledAtBar == current
																	&& extra.filledGeneration == sp.generation
																	&& (extra.filledAtClose || !atClose)
																	&& atClose;
					if (sameStamp)
					{
						applyProfileGeometry(vp, extra, chartBar);   // as-of POC/VA can still move intrabar
						if (current < 1 || extra.endBar >= current) stampExtra(extra);   // VPB-28: frozen after close
						return;
					}

					int windowEnd = profileEndBar(vp);
					bool windowOpenPastCalcBar = current >= 1 && windowEnd > current;
					if (windowOpenPastCalcBar)
					{
						// VPB-03: in a backtest MC builds the profile over its whole window, so the histogram would include
						// bars after the calc bar. Keep geometry (as-of POC/VA) only; the histogram is filled once the window
						// closes (nodesBuilt stays false so the closed test above fails until then). Real time never gets here.
						clearHistogram(extra);
						applyProfileGeometry(vp, extra, chartBar);
						extra.nodesBuilt = false;
					}
					else
					{
						fillNodes(vp, extra, chartBar);
						extra.nodesBuilt = extra.levels.Count > 0 && extra.poc > 0;
					}
					extra.nodesBuiltForStart = extra.startBar;
					extra.nodesBuiltGeneration = sp.generation;
					extra.filledAtBar = current;
					extra.filledAtClose = atClose;
					extra.filledGeneration = sp.generation;
					sp.levelsGeneration++;
					m_volumePctDirty = true;
				}
			}
			// VPB-28: VWAP snapshot = value as of the profile's last bar; frozen once the window has closed, so a later
			// query no longer overwrites it with the current VWAP.
			if (current < 1 || extra.endBar >= current) stampExtra(extra);
		}

		private bool isStreamBarClosed(int stream)
		{
			IInstrument bars = barsOf(stream);
			return bars == null || bars.Status == EBarState.Close;
		}

		private void fillNodes(IProfile vp, VpExtra extra, int chartBar)
		{
			extra.hvns.Clear();
			extra.lvns.Clear();
			extra.levels.Clear();
			extra.poc = 0;
			extra.vah = 0;
			extra.val = 0;
			extra.pocVol = 0;
			extra.maxTotalPercentile = 0;
			extra.maxAskPercentile = 0;
			extra.maxBidPercentile = 0;
			if (vp == null || vp.Values == null) return;
			readLevels(vp, extra.levels);
			applyProfileGeometry(vp, extra, chartBar);
			buildNodesFromLevels(extra);
		}

		/// <summary>
		/// POC fallback, pocVol and HVN/LVN nodes from extra.levels alone (no IProfile). Shared by the MultiCharts
		/// fill and the own developing profile (VPB-1). extra.levels must be sorted by ascending price.
		/// </summary>
		protected void buildNodesFromLevels(VpExtra extra)
		{
			bool[] traded;
			List<Row> rows = denseRowsFromLevels(extra.levels, tickSize(), out traded);
			if (rows.Count == 0) { rows = rowsFromLevels(extra.levels); traded = null; }   // fallback: old sparse behaviour
			if (rows.Count < 3) return;

			double[] vols = new double[rows.Count];
			for (int j = 0; j < rows.Count; j++)
				vols[j] = rows[j].volume;
			// VPB-4: the fallback POC is the raw max-volume row, not the smoothed peak (smoothing can move it a tick).
			int rawPocIdx = 0;
			for (int k = 1; k < vols.Length; k++)
				if (vols[k] > vols[rawPocIdx]) rawPocIdx = k;
			vols = smooth(vols, DEFAULT_SMOOTH_TICKS);
			int pocIdx = 0;
			double pocVol = 0;
			for (int k = 0; k < vols.Length; k++)
			{
				if (vols[k] > pocVol)
				{
					pocVol = vols[k];
					pocIdx = k;
				}
			}
			if (extra.poc <= 0 && !extra.pocStale) extra.poc = rows[rawPocIdx].price;
			if (extra.pocVol <= 0) extra.pocVol = volumeAtPrice(extra, extra.poc);
			if (pocVol <= 0) return;

			// Thresholds from traded rows only, so adding empty rows does not drag the cut-offs down.
			double[] basis = vols;
			if (traded != null)
			{
				var tv = new List<double>(vols.Length);
				for (int k = 0; k < vols.Length; k++) if (traded[k]) tv.Add(vols[k]);
				if (tv.Count >= 3) basis = tv.ToArray();
			}
			double hvnFloor = percentile(basis, HvnPercentile);
			double lvnCeil = percentile(basis, LvnPercentile);

			double range = rows[rows.Count - 1].price - rows[0].price;
			if (range <= 0) return;
			// HVN: contiguous runs above the high percentile that contain a peak
			int i = 0;
			while (i < rows.Count)
			{
				if (vols[i] < hvnFloor) { i++; continue; }
				int a = i;
				while (i < rows.Count && vols[i] >= hvnFloor) i++;
				int b = i - 1;
				if (!nodeWideEnough(rows[a].price, rows[b].price)) continue;
				int peak = a;
				for (int k = a + 1; k <= b; k++)
					if (vols[k] > vols[peak]) peak = k;
				extra.hvns.Add(makeNode(rows, vols, a, b, peak, pocVol, peak == pocIdx));
			}
			// LVN: local troughs under the low percentile, expanded, width-capped
			for (int t = 0; t < rows.Count; t++)
			{
				if (vols[t] > lvnCeil) continue;
				if (!isLocalMin(vols, t)) continue;
				int a = t;
				while (a > 0 && vols[a - 1] <= lvnCeil) a--;
				int b = t;
				while (b < rows.Count - 1 && vols[b + 1] <= lvnCeil) b++;
				if (!nodeWideEnough(rows[a].price, rows[b].price)) continue;
				// unused wings with no HVN on both sides are not nodes
				double maxGap = DEFAULT_LVN_HVN_MAX_GAP_TICKS * tickSize();
				bool hvnBelow = hasHvnAdjacent(extra.hvns, rows[a].price, true, maxGap);
				bool hvnAbove = hasHvnAdjacent(extra.hvns, rows[b].price, false, maxGap);
				if (!hvnBelow || !hvnAbove) continue;   // LVN = valley between two HVNs
				double width = rows[b].price - rows[a].price;
				if (width > range * MaxLvnFractionOfRange) continue;
				extra.lvns.Add(makeNode(rows, vols, a, b, t, pocVol, false));
				t = b;   // skip the rest of this valley
			}
		}

		// Extras are the source the rest of the class queries. Walking pack.Items rebuilt a
		// VpKey (BarsInterval) and skipped an extra that was not in the current Items snapshot.
		// A stream whose levels did not gain, lose, or resize since the last stamp is skipped.
		// Developing profiles still dirty this when fillNodes rewrites their levels.
		private void refreshVolumePercentiles()
		{
			if (!m_volumePctDirty || m_streams == null) return;
			m_volumePctDirty = false;
			foreach (var kv in m_streams)
			{
				StreamPack sp = kv.Value;
				if (sp == null || sp.extra == null) continue;
				if (sp.percentilesStampedGeneration == sp.levelsGeneration) continue;
				int current = streamItemBarIndex(sp.datastream);

				// 1. Merge newly closed profiles into the reference (O(P) scan, O(N) merge per close).
				//    After a reset (StartCalc / profile-collection change) every closed profile is new at once:
				//    merging them one by one copies the growing prefix each time (O(P x N)), so rebuild the
				//    reference with a single sort instead.
				m_newClosedScratch.Clear();
				foreach (VpExtra e in sp.extra.Values)
				{
					if (e == null || e.inClosedSet || e.levels.Count == 0) continue;
					bool closed = current >= 1 && e.endBar >= e.startBar && e.endBar < current && e.nodesBuilt;
					if (!closed) continue;
					m_newClosedScratch.Add(e);
				}
				if (m_newClosedScratch.Count > 1 && sp.closedCount == 0)
					rebuildClosed(sp, m_newClosedScratch);
				else
					for (int i = 0; i < m_newClosedScratch.Count; i++)
						mergeIntoClosed(sp, m_newClosedScratch[i]);
				for (int i = 0; i < m_newClosedScratch.Count; i++)
				{
					m_newClosedScratch[i].inClosedSet = true;
					sp.closedLevelsGeneration++;
				}
				m_newClosedScratch.Clear();

				// 2. Reference grew: restamp every closed extra (once per profile close, not per bar).
				if (sp.closedStampedGeneration != sp.closedLevelsGeneration)
				{
					foreach (VpExtra e in sp.extra.Values)
						if (e != null && e.inClosedSet)
							stampLevelPercentiles(e, sp.closedTotal, sp.closedAsk, sp.closedBid, sp.closedCount);
					sp.closedStampedGeneration = sp.closedLevelsGeneration;
				}

				// 3. Developing extras: stamp against the closed reference only when their fill changed.
				foreach (VpExtra e in sp.extra.Values)
				{
					if (e == null || e.inClosedSet || e.levels.Count == 0) continue;
					if (e.pctStampedFill >= 0 && e.pctStampedFill == e.filledAtBar
							&& e.pctStampedClosedGen == sp.closedLevelsGeneration) continue;   // same fill, same reference
					stampLevelPercentiles(e, sp.closedTotal, sp.closedAsk, sp.closedBid, sp.closedCount);
					e.pctStampedFill = e.filledAtBar;
					e.pctStampedClosedGen = sp.closedLevelsGeneration;
				}
				sp.percentilesStampedGeneration = sp.levelsGeneration;
			}
		}

		private readonly List<VpExtra> m_newClosedScratch = new List<VpExtra>();

		// Bulk build of an empty closed reference: concatenate and sort each column once (VPB-15B follow-up).
		private static void rebuildClosed(StreamPack sp, List<VpExtra> extras)
		{
			int n = 0;
			for (int i = 0; i < extras.Count; i++) n += extras[i].levels.Count;
			var t = new double[n];
			var a = new double[n];
			var b = new double[n];
			int w = 0;
			for (int i = 0; i < extras.Count; i++)
			{
				List<LevelVol> levels = extras[i].levels;
				for (int k = 0; k < levels.Count; k++, w++)
				{
					t[w] = levels[k].total;
					a[w] = levels[k].ask;
					b[w] = levels[k].bid;
				}
			}
			Array.Sort(t);
			Array.Sort(a);
			Array.Sort(b);
			sp.closedTotal = t;
			sp.closedAsk = a;
			sp.closedBid = b;
			sp.closedCount = n;
		}

		private static void mergeIntoClosed(StreamPack sp, VpExtra e)
		{
			int add = e.levels.Count;
			int n = sp.closedCount + add;
			sp.closedTotal = mergeSorted(sp.closedTotal, sp.closedCount, e.levels, 0, n);
			sp.closedAsk = mergeSorted(sp.closedAsk, sp.closedCount, e.levels, 1, n);
			sp.closedBid = mergeSorted(sp.closedBid, sp.closedCount, e.levels, 2, n);
			sp.closedCount = n;
		}

		/// <summary>
		/// POC / VAH / VAL / pocVol from the live IProfile. Chart labels use
		/// POCForBar / HighVAForBar / LowVAForBar; vp.POC and vp.VA can lag
		/// or point at a different tick than the histogram.
		/// </summary>
		private void applyProfileGeometry(IProfile vp, VpExtra extra, int chartBar)
		{
			if (vp == null || extra == null) return;

			int endChart = profileEndBar(vp);
			int startChart = profileStartBar(vp);
			if (startChart >= 1) extra.startBar = startChart;
			if (endChart >= 1) extra.endBar = endChart;
			int bar = chartBar >= 1 ? chartBar : (endChart >= 1 ? endChart : startChart);
			// VPB-03: never read past the calc bar; the whole-window fallbacks are only legitimate once the window has
			// closed relative to the calc bar (endChart <= asOf: a window ending on the calc bar is complete as of it).
			int asOf = streamItemBarIndex(extra.dataStream < 1 ? 1 : extra.dataStream);
			if (asOf >= 1 && bar > asOf) bar = asOf;
			bool whole = asOf < 1 || endChart <= asOf;   // VPB-03: never look ahead of the calc bar

			// VPB-4: the first-bar POC is only a fallback for a whole window. On a developing window an unreadable as-of POC
			// stays 0 and is flagged (pocStale) instead of silently becoming the POC as of the profile's first bar.
			double poc = firstPositive(
				priceForBar(vp, bar, 0),
				priceForBar(vp, bar - 1, 0),
				whole ? priceForBar(vp, endChart, 0) : 0,
				whole ? priceForBar(vp, startChart, 0) : 0,
				whole ? levelPrice(vp.POC) : 0);
			extra.pocStale = !whole && poc <= 0;
			if (extra.pocStale)
			{
				m_pocUnreadableCount++;
				if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"VPB-4 developing POC unreadable stream({extra.dataStream}) start({startChart}) asOf({bar})");
			}
			double vah = firstPositive(
				priceForBar(vp, bar, 1),
				priceForBar(vp, bar - 1, 1),
				whole ? priceForBar(vp, endChart, 1) : 0,
				whole ? vaExtreme(vp, true) : 0);
			double val = firstPositive(
				priceForBar(vp, bar, 2),
				priceForBar(vp, bar - 1, 2),
				whole ? priceForBar(vp, endChart, 2) : 0,
				whole ? vaExtreme(vp, false) : 0);

			if (poc > 0) extra.poc = poc;
			if (vah > 0) extra.vah = vah;
			if (val > 0) extra.val = val;
			applyProfileOhlc(extra);

			double pocVol = 0;
			try
			{
				if (whole && vp.POC != null) pocVol = (double)vp.POC.TotalValue;
			}
			catch { }
			if (pocVol <= 0) pocVol = volumeAtPrice(extra, extra.poc);
			if (pocVol > 0) extra.pocVol = pocVol;
		}

		/// <summary>
		/// Bar OHLC of the profile window on extra.dataStream. Open is the first
		/// bar, close is the last bar, high/low are the extremes of that span.
		/// Not VAH/VAL.
		/// Does not assign 0 before a successful read. A start bar outside MaxBarsBack
		/// leaves the stored window alone. A new startBar resets the cursor only after
		/// that start bar is readable. A developing profile folds bars after
		/// ohlcFoldedThrough and refreshes close from the new end. A closed window
		/// whose endBar has not changed and whose high/low are already set is not rescanned.
		/// </summary>
		private void applyProfileOhlc(VpExtra extra)
		{
			if (extra == null) return;
			int start = extra.startBar;
			int end = extra.endBar;
			if (start < 1) return;
			if (end < start) end = start;

			int stream = extra.dataStream < 1 ? 1 : extra.dataStream;
			IInstrument bars = barsOf(stream);
			if (bars == null) return;
			int current = streamItemBarIndex(stream);
			if (current < 1) return;
			if (end > current) end = current;
			if (start > current) return;

			if (extra.ohlcStartBar != start)
			{
				double o = barPrice(bars, current, start, 0);
				double h = barPrice(bars, current, start, 1);
				// Cannot see the new window. Keep the previous snapshot rather than zeroing it.
				if (o <= 0 && h <= 0)
					return;
				extra.ohlcStartBar = start;
				extra.ohlcFoldedThrough = 0;
				extra.ohlcEndBar = 0;
				extra.open = o > 0 ? o : 0;
				extra.high = 0;
				extra.low = 0;
				extra.close = 0;
			}

			// Closed window already folded for this endBar. Do not walk the span again.
			if (extra.ohlcEndBar == end && extra.ohlcFoldedThrough >= end && extra.high > 0 && extra.low > 0)
				return;

			if (extra.open <= 0)
			{
				double o = barPrice(bars, current, start, 0);
				if (o > 0) extra.open = o;
			}

			int from = extra.ohlcFoldedThrough >= start ? extra.ohlcFoldedThrough + 1 : start;
			for (int b = from; b <= end; b++)
			{
				double h = barPrice(bars, current, b, 1);
				double l = barPrice(bars, current, b, 2);
				if (h <= 0 && l <= 0)
					break; // outside MaxBarsBack or unreadable; do not advance the cursor past it
				if (h > extra.high) extra.high = h;
				if (l > 0 && (extra.low <= 0 || l < extra.low)) extra.low = l;
				extra.ohlcFoldedThrough = b;
			}
			double c = barPrice(bars, current, end, 3);
			if (c > 0) extra.close = c;
			if (extra.ohlcFoldedThrough >= end)
				extra.ohlcEndBar = end;
		}

		/// <summary>kind: 0 open, 1 high, 2 low, 3 close. chartBar is 1-based Status Bar #.</summary>
		private double barPrice(IInstrument bars, int current, int chartBar, int kind)
		{
			if (bars == null || chartBar < 1 || current < chartBar) return 0;
			int ago = current - chartBar;
			if (ago > ExecInfo.MaxBarsBack) return 0;   // outside guaranteed history: report "unreadable", do not probe
			if (ago == 0)
			{
				if (kind == 0) return bars.OpenValue;
				if (kind == 1) return bars.HighValue;
				if (kind == 2) return bars.LowValue;
				return bars.CloseValue;
			}
			if (kind == 0) return bars.Open[ago];
			if (kind == 1) return bars.High[ago];
			if (kind == 2) return bars.Low[ago];
			return bars.Close[ago];
		}


		// ==== Phase 3 VP helpers (phase3-specs.md VP-1..VP-4, VP-6 host; all off by default, no new [Input]s) ====

		/// <summary>VP-1 range / composite profiles over arbitrary closed-bar windows (enabled=false: no ring, no work).</summary>
		protected RangeProfileConfig RangeProfiles { get; } = new RangeProfileConfig();
		/// <summary>VP-2 weekly profile stream (enabled=false: not created).</summary>
		protected WeeklyProfileConfig WeeklyProfiles { get; } = new WeeklyProfileConfig();
		/// <summary>VP-4 / VP-6 host accessors (the static _ProfileStats math needs no switch); enabled forces the Data1 ring.</summary>
		protected ProfileStatsConfig ProfileStats { get; } = new ProfileStatsConfig();
		/// <summary>VP-3 per-bar ladder (enabled=false: not created; on the futures base it forces OrderFlowEnabled).</summary>
		protected BarLadderConfig BarLadders { get; } = new BarLadderConfig();
		/// <summary>VWAP slot options; resetPeriodSlotsOnRoll (default false) resets Week / Month slots at a roll too. Needs RollDetection != Off;
		/// RollDetection.Table is the reproducible roll source for it (Series misses some contracts; see the manual).
		/// Set resetPeriodSlotsOnRoll before base.StartCalc() (constructor / Create): configureVwap copies it there, later changes are ignored.</summary>
		protected VwapConfig VwapConfig { get; } = new VwapConfig();

		private bool m_rangeOn;
		private readonly List<RangeProfileHandle> m_rangeHandles = new List<RangeProfileHandle>();
		private readonly Dictionary<RangeProfileHandle, int> m_rangeHandleClosedSession = new Dictionary<RangeProfileHandle, int>();
		private sealed class RangeCacheEntry { public int stream, lastBar; public DateTime t0, t1; public bool excl; public VpExtra extra; public RangeStats stats; }
		private readonly List<RangeCacheEntry> m_rangeCache = new List<RangeCacheEntry>();
		private readonly Dictionary<VpExtra, RangeStats> m_rangeStats = new Dictionary<VpExtra, RangeStats>();
		private bool m_rangeQueryWarned, m_rangeHandleWarned;
		private int m_vpSessionNo; private DateTime m_vpSessionDay = DateTime.MinValue; private int m_vpSessionLastBar = -1;

		private _WeeklyProfiles m_weekly;
		private _ProfileMath.Accumulator m_weekSession;
		private DateTime m_weekSessionDay = DateTime.MinValue;
		private bool m_weekSessionRoll, m_weekSessionPartial;
		private int m_weekLastBar = -1;
		private readonly Dictionary<WeekProfile, VpExtra> m_weekExtras = new Dictionary<WeekProfile, VpExtra>();
		private bool m_weeklyQueryWarned;

		private _BarLadderStore m_ladders;
		private readonly Dictionary<long, LadderBar> m_htfLadders = new Dictionary<long, LadderBar>();
		private bool m_ladderQueryWarned, m_ladderNoFlowWarned;
		private Func<RingBar, LadderBar> m_ladderFn;

		/// <summary>VP-1 coverage: is the session clock open in the slot starting at slotStart (chart time)? Base: always; the futures base uses the Globex clock.</summary>
		protected virtual bool sessionOpenAt(DateTime slotStart) { return true; }
		/// <summary>VP-3: footprint of a Data1 bar (1-based bar number) for the ladder. Base: none (BarUpDown only); the futures base reads its flow engine.</summary>
		protected virtual _OrderFlow.FootprintBar footprintForLadder(int barNumber) { return null; }
		/// <summary>VP-3: true when footprintForLadder can return order-flow ladders.</summary>
		protected virtual bool ladderHasFlow { get { return false; } }

		private void startVpbHelpers()
		{
			m_rangeOn = false; m_rangeHandles.Clear(); m_rangeHandleClosedSession.Clear(); m_rangeCache.Clear(); m_rangeStats.Clear();
			m_rangeQueryWarned = m_rangeHandleWarned = false; m_vpSessionNo = 0; m_vpSessionDay = DateTime.MinValue; m_vpSessionLastBar = -1;
			m_weekly = null; m_weekSession = null; m_weekSessionDay = DateTime.MinValue; m_weekLastBar = -1; m_weekExtras.Clear(); m_weeklyQueryWarned = false;
			m_ladders = null; m_htfLadders.Clear(); m_ladderQueryWarned = m_ladderNoFlowWarned = false; m_ladderFn = null;
			string err;
			if (BarLadders.enabled)
			{
				if ((err = BarLadders.Validate()) != null) throw new ArgumentException(err);
				m_ladders = new _BarLadderStore(BarLadders, tickSize());
				requireRing(1, BarLadders.keepBars, "VP-3");
				foreach (int ds in BarLadders.htfStreams) requireDataStream("BarLadders.htfStreams", ds);
				_BarLadderStore store = m_ladders;
				m_ladderFn = rb => store.Get(rb, footprintForLadder(rb.bar), rb.bar < newestRingBar(1));
			}
			if (RangeProfiles.enabled)
			{
				if ((err = RangeProfiles.Validate()) != null) throw new ArgumentException(err);
				foreach (int ds in RangeProfiles.streams) requireRing(ds, RangeProfiles.ringBars, "VP-1");
				m_rangeOn = true;
			}
			if (ProfileStats.enabled)
			{
				if ((err = ProfileStats.Validate()) != null) throw new ArgumentException(err);
				requireRing(1, ProfileStats.ringBars, "VP-4/6");
				m_rangeOn = true;   // VP-4 / VP-6 build their histograms through VP-1
				if (!RangeProfiles.enabled) { RangeProfiles.enabled = true; if (Array.IndexOf(RangeProfiles.streams, 1) < 0) RangeProfiles.streams = new[] { 1 }; }
			}
			if (WeeklyProfiles.enabled)
			{
				if ((err = WeeklyProfiles.Validate()) != null) throw new ArgumentException(err);
				requireDataStream("WeeklyProfiles.dataStream", WeeklyProfiles.dataStream);
				m_weekly = new _WeeklyProfiles(WeeklyProfiles, tickSize()) { journal = engineSink(ETraceLevel.Journal) };
			}
		}

		private int newestRingBar(int ds) { _BarRing r = Ring(ds); return r != null ? r.LastBarNumber : -1; }

		private void stepVpbHelpers()
		{
			if (!m_rangeOn && m_weekly == null && m_ladders == null) return;
			if (Bars.Status == EBarState.Close && Bars.CurrentBar != m_vpSessionLastBar)
			{
				m_vpSessionLastBar = Bars.CurrentBar;
				DateTime day = tradingDayKey(Bars.Time[0]);
				if (day != m_vpSessionDay) { m_vpSessionDay = day; m_vpSessionNo++; }
			}
			if (m_ladders != null && !ladderHasFlow && !m_ladderNoFlowWarned)
			{
				m_ladderNoFlowWarned = true;
				if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "BarLadders enabled on a non-futures base: only the BarUpDown tier (totals, no bid/ask) is available");
			}
			if (m_rangeOn) stepRangeHandles();
			if (m_weekly != null) stepWeekly();
		}

		// ---------------- VP-1 ----------------

		private bool rangeStreamOk(int stream)
		{
			if (!m_rangeOn || Array.IndexOf(RangeProfiles.streams, stream) < 0 || Ring(stream) == null)
			{
				if (!m_rangeQueryWarned) { m_rangeQueryWarned = true; if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, $"RangeProfile queried for Data{stream} but RangeProfiles is not enabled for it"); }
				return false;
			}
			return true;
		}

		private TimeSpan streamResolution(int stream) { return BarsOfData(stream).Info.Resolution.Duration(); }

		/// <summary>VP-1: profile of the closed bars with t0 &lt;= time &lt;= t1 on `stream` (inclusive start, for explicit callers; null when none,
		/// Reject across a roll, or disabled). Bars are assumed end-stamped, pending Probe P2.</summary>
		public VpExtra RangeProfile(int stream, DateTime t0, DateTime t1) { return rangeProfile(stream, t0, t1, false); }

		/// <summary>VP-1 / A2-30: profile of the closed bars with t0 &lt; time &lt;= t1 (exclusive start, e.g. (t - 30m, t]). Ruling 3: A2-30 consumers
		/// use this form; bars are assumed end-stamped (MC close stamps), pending Probe P2.</summary>
		public VpExtra RangeProfileAfter(int stream, DateTime t0, DateTime t1) { return rangeProfile(stream, t0, t1, true); }

		private VpExtra rangeProfile(int stream, DateTime t0, DateTime t1, bool excl)
		{
			if (!rangeStreamOk(stream)) return null;
			_BarRing ring = Ring(stream);
			int last = ring.LastBarNumber;
			for (int i = 0; i < m_rangeCache.Count; i++)
			{
				RangeCacheEntry c = m_rangeCache[i];
				if (c.stream == stream && c.lastBar == last && c.t0 == t0 && c.t1 == t1 && c.excl == excl)
				{
					if (i != m_rangeCache.Count - 1) { m_rangeCache.RemoveAt(i); m_rangeCache.Add(c); }
					return c.extra;
				}
			}
			RangeHistogram h = RangeProfileBuilder.Build(ring, t0, t1, tickSize(), RangeProfiles, stream == 1 ? m_ladderFn : null, excl, RangeProfileBuilder.Tag(t0, t1));
			VpExtra e = rangeExtra(h, stream);
			RangeStats st = RangeProfileBuilder.Stats(h, RangeProfileBuilder.ExpectedSlots(t0, t1, streamResolution(stream), sessionOpenAt, excl));
			if (e != null) rememberStats(e, st);
			if (RangeProfiles.cacheSize > 0)
			{
				m_rangeCache.Add(new RangeCacheEntry { stream = stream, lastBar = last, t0 = t0, t1 = t1, excl = excl, extra = e, stats = st });
				while (m_rangeCache.Count > RangeProfiles.cacheSize) m_rangeCache.RemoveAt(0);
			}
			if (e != null && traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, rangeLine(h.tag, stream, t0, t1, e, st));
			return e;
		}

		/// <summary>VP-1: profile of `count` ring bars ending `barsBack` bars before the newest closed bar.</summary>
		public VpExtra RangeProfileBars(int stream, int barsBack, int count)
		{
			if (!rangeStreamOk(stream)) return null;
			RangeHistogram h = RangeProfileBuilder.BuildBars(Ring(stream), barsBack, count, tickSize(), RangeProfiles, stream == 1 ? m_ladderFn : null);
			VpExtra e = rangeExtra(h, stream);
			if (e != null) rememberStats(e, RangeProfileBuilder.Stats(h, count));
			return e;
		}

		/// <summary>VP-1: anchored handle that grows with each closed bar from t0 (backfilled from the ring). Null when disabled or maxOpenHandles is reached.</summary>
		public RangeProfileHandle OpenRange(int stream, DateTime t0, string tag)
		{
			if (!rangeStreamOk(stream)) return null;
			int open = 0;
			foreach (RangeProfileHandle x in m_rangeHandles) if (!x.IsClosed) open++;
			if (open >= RangeProfiles.maxOpenHandles)
			{
				if (!m_rangeHandleWarned) { m_rangeHandleWarned = true; if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, $"OpenRange: maxOpenHandles ({RangeProfiles.maxOpenHandles}) reached"); }
				return null;
			}
			var h = new RangeProfileHandle(stream, t0, tag, tickSize(), RangeProfiles);
			h.Backfill(Ring(stream), stream == 1 ? m_ladderFn : null);
			h.beforeRead = hh => catchUpHandle(hh, true);
			m_rangeHandles.Add(h);
			if (h.IsClosed) onHandleClosed(h);
			return h;
		}

		/// <summary>VP-1: freeze a handle at t1 and journal it (RP build ...).</summary>
		public void CloseRange(RangeProfileHandle h, DateTime t1)
		{
			if (h == null || h.IsClosed) return;
			catchUpHandle(h, true);
			if (!h.IsClosed) { h.Close(t1); onHandleClosed(h); }
		}

		/// <summary>VP-1: the VpExtra of a handle (rebuilt lazily when dirty).</summary>
		public VpExtra RangeHandleProfile(RangeProfileHandle h)
		{
			if (h == null) return null;
			RangeHistogram hist = h.Profile;
			VpExtra e = rangeExtra(hist, h.stream);
			if (e != null) rememberStats(e, RangeProfileBuilder.Stats(hist, RangeProfileBuilder.ExpectedSlots(h.t0, h.IsClosed ? h.ClosedAt : hist.lastTime, streamResolution(h.stream), sessionOpenAt)));
			return e;
		}

		/// <summary>VP-1: {bars, expectedBars, coverage, ladderBars, rows, totalVol} of a range VpExtra (null when unknown).</summary>
		public RangeStats RangeStatsOf(VpExtra e)
		{
			RangeStats st;
			return e != null && m_rangeStats.TryGetValue(e, out st) ? st : null;
		}

		/// <summary>VP-1 / §0.6: POC / VAH / VAL of a range VpExtra as LevelRefs (isRollWindow from the window's last trading day).</summary>
		public List<LevelRef> RangeLevels(VpExtra e, DateTime t0, DateTime t1, string tag)
		{
			var res = new List<LevelRef>();
			if (e == null || !(e.poc > 0)) return res;
			DateTime created = Bars.Time[0];
			res.Add(LevelRef.Line(ELevelKind.RangePoc, e.poc, created, t0, t1, e.dataStream, tag));
			res.Add(LevelRef.Line(ELevelKind.RangeVah, e.vah, created, t0, t1, e.dataStream, tag));
			res.Add(LevelRef.Line(ELevelKind.RangeVal, e.val, created, t0, t1, e.dataStream, tag));
			bool rw = IsRollWindow(tradingDayKey(t1));
			foreach (LevelRef r in res) { r.crossesRoll = e.crossesRoll; r.isRollWindow = rw; }
			return res;
		}

		private void rememberStats(VpExtra e, RangeStats st)
		{
			if (st == null) return;
			if (m_rangeStats.Count > 256) m_rangeStats.Clear();
			m_rangeStats[e] = st;
		}

		private void catchUpHandle(RangeProfileHandle h, bool includeNewest)
		{
			_BarRing ring = Ring(h.stream);
			if (ring == null || ring.Count == 0 || h.IsClosed) return;
			int newest = ring.Count - 1, upTo = includeNewest ? newest : newest - 1;
			int i = upTo;
			while (i >= 0 && ring.At(i).bar > h.LastBar) i--;
			Func<RingBar, LadderBar> lf = h.stream == 1 && RangeProfiles.useTickLadder ? m_ladderFn : null;
			for (int k = i + 1; k <= upTo && !h.IsClosed; k++)
			{
				RingBar b = ring.At(k);
				if (h.Step(b, lf != null ? lf(b) : null)) onHandleClosed(h);
			}
		}

		private void onHandleClosed(RangeProfileHandle h)
		{
			m_rangeHandleClosedSession[h] = m_vpSessionNo;
			Action<string> j = engineSink(ETraceLevel.Journal);
			if (j == null) return;
			RangeHistogram hist = h.Profile;
			VpExtra e = rangeExtra(hist, h.stream);
			RangeStats st = RangeProfileBuilder.Stats(hist, RangeProfileBuilder.ExpectedSlots(h.t0, h.ClosedAt, streamResolution(h.stream), sessionOpenAt));
			j(rangeLine(h.Tag, h.stream, h.t0, h.ClosedAt, e, st));
			if (st != null && st.coverage < 0.95) j($"RP coverage {h.Tag} Data{h.stream} {st}");
		}

		private static string rangeLine(string tag, int stream, DateTime t0, DateTime t1, VpExtra e, RangeStats st)
		{
			return string.Format(System.Globalization.CultureInfo.InvariantCulture, "RP build {0} Data{1} [{2:yyyy-MM-dd HH:mm:ss}..{3:yyyy-MM-dd HH:mm:ss}] bars({4}/{5}) poc({6}) va({7}-{8}) approx({9}) roll({10})",
				tag, stream, t0, t1, st != null ? st.bars : 0, st != null ? st.expectedBars : 0, e != null ? e.poc : 0, e != null ? e.val : 0, e != null ? e.vah : 0,
				e != null && e.approximate ? 1 : 0, e != null && e.crossesRoll ? 1 : 0);
		}

		private void stepRangeHandles()
		{
			bool lad = RangeProfiles.useTickLadder && m_ladders != null;
			for (int i = m_rangeHandles.Count - 1; i >= 0; i--)
			{
				RangeProfileHandle h = m_rangeHandles[i];
				if (!h.IsClosed) catchUpHandle(h, !(lad && h.stream == 1));   // the newest Data1 ladder may not be built yet: read-time catch-up adds it
				int at;
				if (h.IsClosed && m_rangeHandleClosedSession.TryGetValue(h, out at) && m_vpSessionNo - at > 5) { m_rangeHandles.RemoveAt(i); m_rangeHandleClosedSession.Remove(h); }
			}
		}

		/// <summary>RangeHistogram -> VpExtra through fillFromHistogram (POC / VA / HVN / LVN exactly as Composite), ask / bid on the levels when known.</summary>
		private VpExtra rangeExtra(RangeHistogram h, int stream)
		{
			if (h == null || h.tooManyRows || h.Rows == 0) return null;
			var e = new VpExtra { dataStream = stream, isOwn = true, approximate = h.approximate, isPartial = h.isPartial, crossesRoll = h.crossesRoll,
				startBar = h.startBar, endBar = h.endBar, open = h.open, high = h.high, low = h.low, close = h.close };
			fillWithVa(e, h.prices, h.vols, RangeProfiles.valueAreaPercent);
			setLevelSides(e, h.prices, h.vols, h.ask, h.bid);
			e.pocComplete = e.poc > 0;
			return e;
		}

		private void fillWithVa(VpExtra e, double[] prices, double[] vols, double vaPercent)
		{
			double saved = ValueAreaPercent;
			if (!double.IsNaN(vaPercent)) ValueAreaPercent = vaPercent;
			try { fillFromHistogram(e, prices, vols); }
			finally { ValueAreaPercent = saved; }
		}

		private static void setLevelSides(VpExtra e, double[] prices, double[] vols, double[] ask, double[] bid)
		{
			int j = 0;
			for (int i = 0; i < prices.Length && j < e.levels.Count; i++)
			{
				if (!(vols[i] > 0)) continue;
				LevelVol lv = e.levels[j];
				lv.ask = ask != null ? ask[i] : double.NaN; lv.bid = bid != null ? bid[i] : double.NaN;
				e.levels[j++] = lv;
			}
		}

		// ---------------- VP-2 ----------------

		private _WeeklyProfiles weekly()
		{
			if (m_weekly == null && !m_weeklyQueryWarned) { m_weeklyQueryWarned = true; if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "Weekly profile queried but WeeklyProfiles.enabled is false"); }
			return m_weekly;
		}

		private void stepWeekly()
		{
			IInstrument b = BarsOfData(WeeklyProfiles.dataStream);
			if (b.Status != EBarState.Close || b.CurrentBar == m_weekLastBar) return;
			m_weekLastBar = b.CurrentBar;
			DateTime t = b.Time[0], day = tradingDayKey(t);
			bool roll = rollOn(WeeklyProfiles.dataStream);
			if (day != m_weekSessionDay)
			{
				completeWeekSession();
				m_weekSessionPartial = m_weekSessionDay == DateTime.MinValue;   // the first session seen may have started before the chart
				m_weekSessionDay = day; m_weekSessionRoll = false;
				m_weekSession = new _ProfileMath.Accumulator(tickSize());
			}
			if (roll) m_weekSessionRoll = true;
			if (WeeklyProfiles.source == EWeeklySource.RthSessions && !isRthBar(t)) return;
			m_weekSession.Add(b.FullSymbolData.Current, t, b.Open[0], b.High[0], b.Low[0], b.Close[0], b.Volume[0], WeeklyProfiles.distribution);
		}

		private void completeWeekSession()
		{
			if (m_weekSession == null || m_weekSessionDay == DateTime.MinValue) return;
			var rows = new Dictionary<long, double[]>();
			foreach (var kv in m_weekSession.rows) rows[kv.Key] = new[] { kv.Value, double.NaN, double.NaN };
			_ProfileMath.Accumulator a = m_weekSession;
			m_weekly.AddSession(m_weekSessionDay, rows, m_weekSessionRoll, m_weekSessionPartial, a.open, a.high, a.low, a.close, a.bars, IsRollWindow(m_weekSessionDay));
			m_weekly.RefreshRollWindow(IsRollWindow);
			m_weekExtras.Clear();
		}

		/// <summary>VP-2: weekly profile (0 = developing week: its completed sessions, plus the developing session when includeDeveloping).</summary>
		public VpExtra Week(int weeksAgo)
		{
			_WeeklyProfiles w = weekly();
			if (w == null) return null;
			WeekProfile wp = w.Week(weeksAgo);
			if (weeksAgo == 0 && WeeklyProfiles.includeDeveloping && m_weekSession != null && m_weekSession.rows.Count > 0)
			{
				var dev = new WeekProfile { key = _WeeklyProfiles.WeekKeyOf(m_weekSessionDay), tick = tickSize(), bidAskValid = false };
				if (wp != null && wp.key == dev.key) { foreach (var kv in wp.rows) { double[] v; if (!dev.rows.TryGetValue(kv.Key, out v)) { v = new double[3]; dev.rows[kv.Key] = v; } v[0] += kv.Value[0]; } dev.sessions.AddRange(wp.sessions); dev.isPartial = wp.isPartial; dev.high = wp.high; dev.low = wp.low; dev.open = wp.open; }
				foreach (var kv in m_weekSession.rows) { double[] v; if (!dev.rows.TryGetValue(kv.Key, out v)) { v = new double[3]; dev.rows[kv.Key] = v; } v[0] += kv.Value; }
				if (m_weekSession.high > dev.high) dev.high = m_weekSession.high;
				if (m_weekSession.low > 0 && m_weekSession.low < dev.low) dev.low = m_weekSession.low;
				if (double.IsNaN(dev.open)) dev.open = m_weekSession.open;
				dev.close = m_weekSession.close;
				VpExtra de = weekExtra(dev);
				if (de != null) de.isDeveloping = true;
				return de;
			}
			VpExtra e = weekExtra(wp);
			if (e != null && weeksAgo == 0) e.isDeveloping = true;
			return e;
		}

		/// <summary>VP-2: the last nWeeks COMPLETED weeks merged; null if fewer than nWeeks after the last roll.</summary>
		public VpExtra WeeklyComposite(int nWeeks)
		{
			_WeeklyProfiles w = weekly();
			return w != null ? weekExtra(w.Composite(nWeeks)) : null;
		}

		public int CompletedWeeks { get { return m_weekly != null ? m_weekly.CompletedWeeks : 0; } }
		public DateTime WeekKey(int weeksAgo) { return m_weekly != null ? m_weekly.WeekKey(weeksAgo) : DateTime.MinValue; }

		/// <summary>VP-2 / §0.6: weekly POC / VAH / VAL as LevelRefs (tag WK:yyyy-MM-dd; isRollWindow when any session of the week is in a roll window).</summary>
		public List<LevelRef> WeekLevels(int weeksAgo)
		{
			var res = new List<LevelRef>();
			WeekProfile wp = m_weekly != null ? m_weekly.Week(weeksAgo) : null;
			VpExtra e = Week(weeksAgo);
			if (wp == null || e == null || !(e.poc > 0)) return res;
			DateTime t0 = wp.sessions.Count > 0 ? wp.sessions[0].day : wp.key, t1 = wp.sessions.Count > 0 ? wp.sessions[wp.sessions.Count - 1].day : wp.key, created = Bars.Time[0];
			res.Add(LevelRef.Line(ELevelKind.WeekPoc, e.poc, created, t0, t1, WeeklyProfiles.dataStream, wp.Tag));
			res.Add(LevelRef.Line(ELevelKind.WeekVah, e.vah, created, t0, t1, WeeklyProfiles.dataStream, wp.Tag));
			res.Add(LevelRef.Line(ELevelKind.WeekVal, e.val, created, t0, t1, WeeklyProfiles.dataStream, wp.Tag));
			foreach (LevelRef r in res) { r.crossesRoll = wp.crossesRoll; r.isRollWindow = wp.IsRollWindow; }
			return res;
		}

		private VpExtra weekExtra(WeekProfile wp)
		{
			if (wp == null) return null;
			VpExtra e;
			if (m_weekExtras.TryGetValue(wp, out e)) return e;
			double[] p, tot, ask, bid;
			if (!wp.Dense(out p, out tot, out ask, out bid)) return null;
			e = new VpExtra { dataStream = WeeklyProfiles.dataStream, isOwn = true, approximate = true, isPartial = wp.isPartial, crossesRoll = wp.crossesRoll,
				open = wp.open, high = wp.high, low = wp.low, close = wp.close };
			fillFromHistogram(e, p, tot);
			setLevelSides(e, p, tot, ask, bid);
			e.pocComplete = wp.complete && e.poc > 0;
			if (m_weekExtras.Count > 64) m_weekExtras.Clear();
			m_weekExtras[wp] = e;
			return e;
		}

		// ---------------- VP-3 ----------------

		private bool laddersOk()
		{
			if (m_ladders == null && !m_ladderQueryWarned) { m_ladderQueryWarned = true; if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "Ladder queried but BarLadders.enabled is false"); }
			return m_ladders != null;
		}

		/// <summary>VP-3: ladder of the Data1 closed bar `ago` (0 = the newest closed bar); null when disabled / not in the ring / worse than minQuality.</summary>
		public LadderBar Ladder(int ago) { return Ladder(ago, BarLadders.minQuality); }

		public LadderBar Ladder(int ago, _OrderFlow.FlowQuality minQuality)
		{
			if (!laddersOk()) return null;
			_BarRing r = Ring(1);
			if (r == null || ago < 0 || ago >= r.Count) return null;
			LadderBar lb = m_ladderFn(r[ago]);
			return lb != null && lb.quality <= minQuality ? lb : null;
		}

		/// <summary>VP-3: ladder of the Data1 bar stamped barTime (null when not in the ring).</summary>
		public LadderBar LadderAt(DateTime barTime)
		{
			if (!laddersOk()) return null;
			_BarRing r = Ring(1);
			int i = r != null ? r.IndexAtOrBefore(barTime) : -1;
			return i >= 0 && r.At(i).time == barTime ? m_ladderFn(r.At(i)) : null;
		}

		/// <summary>VP-3: aggregate of the Data1 ladders in (prevHtfTime, htfTime] for the closed Data n bar `ago` (HtfClosed). Assumes MC close stamps (Probe P2).</summary>
		public LadderBar HtfLadder(int htfStream, int ago)
		{
			if (!laddersOk()) return null;
			if (Array.IndexOf(BarLadders.htfStreams, htfStream) < 0) { if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"HtfLadder: Data{htfStream} not in BarLadders.htfStreams"); return null; }
			IInstrument hb = BarsOfData(htfStream);
			int i = HtfClosedIndex(htfStream, ago);
			if (i < 0 || hb.CurrentBar <= i + 1) return null;
			DateTime t = hb.Time[i], prev = hb.Time[i + 1];
			long key = ((long)htfStream << 56) ^ t.Ticks;
			LadderBar lb;
			if (m_htfLadders.TryGetValue(key, out lb)) return lb;
			_BarRing r = Ring(1);
			if (r == null || !r.Covers(prev)) return null;
			List<RingBar> win = r.Window(prev, t, true);
			var parts = new List<LadderBar>(win.Count);
			foreach (RingBar rb in win) parts.Add(m_ladderFn(rb));
			int expected = RangeProfileBuilder.ExpectedSlots(prev, t, streamResolution(1), sessionOpenAt, true);
			lb = _BarLadderStore.Merge(parts, win, expected, hb.FullSymbolData.Current - (hb.Status == EBarState.Close ? ago : ago + 1), t, tickSize());
			if (m_htfLadders.Count > 512) m_htfLadders.Clear();
			m_htfLadders[key] = lb;
			if (lb.coverage < 1) { Action<string> j = engineSink(ETraceLevel.Journal); if (j != null) j($"LADDER HTF Data{htfStream} {t:yyyy-MM-dd HH:mm:ss} coverage({lb.coverage:0.###}) -> totals-only"); }
			return lb;
		}

		/// <summary>VP-3: a ladder's rows through fillFromHistogram, so HVN / LVN nodes match the session logic (PS-12).</summary>
		public VpExtra LadderProfile(LadderBar b)
		{
			double[] p, v;
			if (!_BarLadderStore.Dense(b, out p, out v)) return null;
			var e = new VpExtra { dataStream = 1, isOwn = true, approximate = !b.HasBidAsk, crossesRoll = b.crossesRoll, startBar = b.bar, endBar = b.bar, high = b.High, low = b.Low };
			fillFromHistogram(e, p, v);
			if (b.HasBidAsk)
			{
				var ask = new double[p.Length]; var bid = new double[p.Length];
				long lo = b.rows[0];
				for (int i = 0; i < b.rows.Length; i++) { ask[b.rows[i] - lo] += b.ask[i]; bid[b.rows[i] - lo] += b.bid[i]; }
				setLevelSides(e, p, v, ask, bid);
			}
			else setLevelSides(e, p, v, null, null);
			e.pocComplete = e.poc > 0;
			return e;
		}

		// ---------------- VP-4 / VP-6 convenience ----------------

		/// <summary>VP-4: entropy of a profile's dense 1-tick histogram with the configured row basis; mmN = Miller-Madow N (contributing bars by default).</summary>
		public EntropyResult ProfileEntropy(VpExtra e, double mmN)
		{
			var p = new List<double>(); var v = new List<double>();
			if (e != null) foreach (LevelVol l in e.levels) { p.Add(l.price); v.Add(l.total); }
			double[] dp, dv;
			if (!ProfileMath.ToDense(p, v, tickSize(), out dp, out dv)) return _ProfileStats.Entropy(null, ProfileStats.entropyRows, mmN);
			return _ProfileStats.Entropy(dv, ProfileStats.entropyRows, mmN);
		}

		/// <summary>VP-4 / VP-6: Data1 ring bars of one trading day (RTH only when rthOnly), oldest first; empty when ProfileStats / RangeProfiles are off.</summary>
		public List<RingBar> SessionBars(DateTime tradingDay, bool rthOnly)
		{
			_BarRing r = Ring(1);
			return r != null ? r.Session(tradingDay, rthOnly) : new List<RingBar>();
		}
	}
}
