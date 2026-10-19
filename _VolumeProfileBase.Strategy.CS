using ATCenterProxy.interop;
using PowerLanguage.Function;
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
		public const double DEFAULT_MAX_LVN_PCT_OF_RANGE = 0.12; // TBD - Do we need this?? Maximum percentage size of the volume profile a node can be - prevents one LVN from swallowing too much of profile.
		public const int DEFAULT_VWAP_LENGTH = 20;
		public const int DEFAULT_CONFLUENCE_LOOKBACK = 4;
		public const int DEFAULT_CONFLUENCE_ALIGN_TICKS = 4;
		public const int DEFAULT_CONFLUENCE_MIN_HITS = 2;
		public const int DEFAULT_CONFLUENCE_TOP_COUNT = 5;
		public const int DEFAULT_CONFLUENCE_TARGET_CLEAR_TICKS = 1;
		public const double DEFAULT_CONFLUENCE_DECAY = 0.75;
		public const double DEFAULT_CONFLUENCE_CROSS_BONUS = 0.35;
		public const double DEFAULT_CONFLUENCE_STREAM_WEIGHT_1 = 1.00;  // execution / primary stream
		public const double DEFAULT_CONFLUENCE_STREAM_WEIGHT_2 = 1.25;  // higher timeframe
		public const double DEFAULT_CONFLUENCE_STREAM_WEIGHT_3 = 1.50;  // highest timeframe
		public const double DEFAULT_CONFLUENCE_NAKED_MULTIPLIER = 1.15; // naked modifies the POC credit; it is not a second source
		public const double DEFAULT_CONFLUENCE_REFINEMENT = 0.25;       // one nested child per parent already in the zone
		private const double MAX_PARENT_CREDIT_MULTIPLIER = 1.25;   // a refined child never lifts its parent above 1.25 x credit
		public const double DEFAULT_CONFLUENCE_OVERLAY_FACTOR = 0.50;   // VWAP context only; never acceptance or rejection
		public const double DEFAULT_CONFLUENCE_SHARE_PIVOT = 0.15;      // volume-share factor pivot
		public const double DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE = 0.95; // percentile used to determine whether volume on a node it exceptional
		public const int MAX_DENSE_ROWS = 20000;  // maximum dense rows in a volume profile to use for LVN detection
		private const int MAX_RESOLVED_CACHE = 4096;   // itemForBar memo entries per stream before it is cleared
		private const double FALLBACK_TICK = 0.01;     // tickSize() when the symbol reports no usable MinMove/PriceScale
		private const double CLUSTER_SPLIT_TOL_MULTIPLE = 4.0;      // a confluence cluster wider than 4 x tol may split
		private const int CLUSTER_MIN_SPLIT_COUNT = 3;              // ... and only when it holds at least this many anchors
		private const int ZONE_TOP_SURVIVORS = 3;                   // survivors kept per zone
		private const double FALLBACK_TIMEFRAME_WEIGHT_STEP = 0.25; // stream weight = 1 + step x timeframeRank when unset
		private const double SHARE_FACTOR_MIN = 0.5;                // volume-share factor clamp
		private const double SHARE_FACTOR_MAX = 1.5;
		private const double LEVEL_MATCH_TICK_FRACTION = 0.51;      // > half a tick: neighbouring levels never match
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
			AdaptiveFromDate = 3   // accumulate from VwapFromDate / Vwap2FromDate / Vwap3FromDate
		}

		/// <summary>
		/// Price level in the volume profile - Multicharts .Net ILevel interface looks like a pain to work with.
		/// </summary>
		private struct Row
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
			public override int GetHashCode() { return datastream ^ (startBar * 397); }
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
			public bool pocComplete;   // window closed relative to the current calc bar, and poc > 0
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
				if (!pocComplete || poc <= 0 || asOfBar <= endBar) return false;
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
			public int independentSources;
			public EConfluenceRole role;
			public EConfluenceLocation location;
			public ESuggestedUse suggestedUse;    // Target | Entry | Conflict
			public double exitPrice;       // anchor when this zone is a target; otherwise 0
			public double entryPrice;      // 0 — the entry edge depends on which side is touched
			public double invalidationBelow;
			public double invalidationAbove;
			public int hitCount;
			public int streamCount;
			public int profileCount;       // independent volume masses; same value as independentSources
			public readonly List<ConfluenceHit> hits = new List<ConfluenceHit>();
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

		/// <summary>
		/// Self-contained VWAP accumulator. Stepped from CalcBar against a data stream's Bars.
		/// Allows IOG and calculations on same bars by calculating provisional values and only
		/// committing the result to the Vwap once a bar is completed and no more ticks/data are
		/// expected for it.
		/// </summary>
		private sealed class VwapEngine
		{
			public _VolumeProfileBase parent;
			public EVwapType type;
			public int length;
			public DateTime fromDate;
			public int datastream;
			public string label;

			public double value;
			public double sigma;
			public bool valid;

			private double m_sumPV;
			private double m_sumP2V;
			private double m_sumV;

			// Provisional = the stream bar currently at index 0 (may be forming). Committed sums exclude it.
			private int m_curBar = int.MinValue;      // FullSymbolData.Current of the provisional bar
			private bool m_curLastInSession;          // LastBarInSession of that bar (latest call)
			private double m_provPV, m_provP2V, m_provV;
			private bool m_gapWarned;

			private bool m_seenAnchorBar;
			// Length VWAP only. Session and AdaptiveFromDate stay incremental in their own methods.
			// Ring holds the typical price and volume that add() would have seen, oldest slot at
			// m_lenNext. Reset / configureVwap clears the count so the next step refills.
			private double[] m_lenPrice;
			private double[] m_lenVol;
			private int m_lenCap;
			private int m_lenCount;
			private int m_lenNext;
			private int m_lenBar = int.MinValue;

			public void Reset()
			{
				m_sumPV = 0;
				m_sumP2V = 0;
				m_sumV = 0;
				m_seenAnchorBar = false;
				m_lenCount = 0;
				m_lenNext = 0;
				m_lenBar = int.MinValue;
				m_curBar = int.MinValue;
				m_curLastInSession = false;
				clearProvisional();
				m_gapWarned = false;
				value = double.NaN;
				sigma = double.NaN;
				valid = false;
			}

			public void Step(IInstrument bars)
			{
				valid = false;
				value = double.NaN;
				sigma = double.NaN;
				if (type == EVwapType.Disabled || bars == null) return;

				switch (type)
				{
					case EVwapType.Session:
						stepSession(bars);
						break;
					case EVwapType.Length:
						stepLength(bars);
						break;
					case EVwapType.AdaptiveFromDate:
						stepFromDate(bars);
						break;
				}
			}

			private void stepSession(IInstrument bars)
			{
				int bar = bars.FullSymbolData.Current;
				if (bar != m_curBar)
				{
					// The previously held bar is final now. A session starts after a bar that was LastBarInSession.
					bool newSession = m_curBar == int.MinValue || m_curLastInSession;
					if (newSession)
					{
						if (parent.TraceLevel >= ETraceLevel.Debug) parent.trace(ETraceLevel.Debug, $"Resetting session VWAP {bars.Time[0]:yyyy-MM-dd HH:mm:ss}");
						clearSums();
						clearProvisional();
					}
					else
						commitProvisional();
					warnIfGap(bar);
					m_curBar = bar;
				}
				m_curLastInSession = bars.LastBarInSession;
				setProvisional(priceAt(bars, 0), volume(bars, 0));
				finish();
			}

			/// <summary>
			/// Rolling length VWAP. Must match the old clear-and-resum of the last n bars,
			/// including the CurrentBar less-than n early-out and the vol/price skip inside add.
			/// sameBar replaces the newest slot so an IOG tick does not double-count.
			/// A CurrentBar jump or a length change refills the ring. The falling bar is
			/// subtracted, then sums are rebuilt newest-first so floating-point order matches
			/// the old loop (barsAgo 0 .. n-1).
			/// </summary>
			private void stepLength(IInstrument bars)
			{
				int n = length;
				if (n < 1 || bars.CurrentBar < n) return;
				ensureLengthRing(n);

				bool contiguous = m_lenCount == n && m_lenBar >= 0 && bars.CurrentBar == m_lenBar + 1;
				bool sameBar = m_lenCount == n && bars.CurrentBar == m_lenBar;
				if (sameBar)
				{
					// IOG: CurrentBar did not advance. Replace the slot written for this bar.
					int slot = (m_lenNext - 1 + n) % n;
					writeLengthSlot(slot, bars, 0);
				}
				else if (contiguous)
				{
					// m_lenNext is the oldest sample. Drop it, write the new bar there, advance.
					writeLengthSlot(m_lenNext, bars, 0);
					m_lenNext = (m_lenNext + 1) % n;
					m_lenBar = bars.CurrentBar;
				}
				else
				{
					// First fill, length change, or a non-contiguous CurrentBar. Oldest first so
					// m_lenNext lands on the bar that will fall out next.
					clearSums();
					m_lenCount = 0;
					m_lenNext = 0;
					for (int ago = n - 1; ago >= 0; ago--)
					{
						writeLengthSlot(m_lenNext, bars, ago);
						m_lenNext = (m_lenNext + 1) % n;
						m_lenCount++;
					}
					m_lenBar = bars.CurrentBar;
				}
				// Newest-first resync matches clear-and-resum addition order, including vol/price skips. It rebuilds the
				// sums on every path, so the ring is not adjusted incrementally (VPB-19: the old subtractions were dead work).
				resyncLengthSums(n);
				finish();
			}

			private void ensureLengthRing(int n)
			{
				if (m_lenPrice != null && m_lenCap == n) return;
				m_lenPrice = new double[n];
				m_lenVol = new double[n];
				m_lenCap = n;
				m_lenCount = 0;
				m_lenNext = 0;
				m_lenBar = int.MinValue;
			}

			private void writeLengthSlot(int slot, IInstrument bars, int barsAgo)
			{
				m_lenPrice[slot] = priceAt(bars, barsAgo);
				m_lenVol[slot] = volume(bars, barsAgo);
			}

			private void resyncLengthSums(int n)
			{
				clearSums();
				int count = m_lenCount < n ? m_lenCount : n;
				for (int k = 0; k < count; k++)
				{
					int slot = (m_lenNext - 1 - k + (n * 2)) % n;
					add(m_lenPrice[slot], m_lenVol[slot]);
				}
			}

			private void stepFromDate(IInstrument bars)
			{
				if (fromDate == DateTime.MinValue) return;
				if (bars.TimeValue < fromDate) return;
				int bar = bars.FullSymbolData.Current;
				if (!m_seenAnchorBar)
				{
					if (parent.TraceLevel >= ETraceLevel.Debug) parent.trace(ETraceLevel.Debug, $"Resetting anchored VWAP {bars.Time[0]:yyyy-MM-dd HH:mm:ss}");
					clearSums();
					clearProvisional();
					m_seenAnchorBar = true;
					m_curBar = bar;
				}
				else if (bar != m_curBar)
				{
					commitProvisional();
					warnIfGap(bar);
					m_curBar = bar;
				}
				setProvisional(priceAt(bars, 0), volume(bars, 0));
				finish();
			}

			private void clearSums()
			{
				m_sumPV = 0;
				m_sumP2V = 0;
				m_sumV = 0;
			}

			private void add(double price, double vol)
			{
				if (vol <= 0 || price <= 0) return;
				m_sumPV += price * vol;
				m_sumP2V += price * price * vol;
				m_sumV += vol;
			}

			private void clearProvisional()
			{
				m_provPV = 0;
				m_provP2V = 0;
				m_provV = 0;
			}

			private void setProvisional(double price, double vol)
			{
				if (vol <= 0 || price <= 0) { clearProvisional(); return; }   // same skip rule as add()
				m_provPV = price * vol;
				m_provP2V = price * price * vol;
				m_provV = vol;
			}

			private void commitProvisional()
			{
				m_sumPV += m_provPV;
				m_sumP2V += m_provP2V;
				m_sumV += m_provV;
				clearProvisional();
			}

			private void warnIfGap(int bar)
			{
				if (m_gapWarned || m_curBar == int.MinValue || bar <= m_curBar + 1) return;
				m_gapWarned = true;
				parent.trace(ETraceLevel.Warning, $"{label}: data stream {datastream} advanced {bar - m_curBar} bars between Data1 calculations; " +
																					"intermediate bars are not in the VWAP. Use a stream with bars at least as long as Data1.");
			}

			private void finish()
			{
				double v = m_sumV + m_provV;
				if (v <= 0) return;
				value = (m_sumPV + m_provPV) / v;
				double var = ((m_sumP2V + m_provP2V) / v) - (value * value);
				sigma = var > 0 ? Math.Sqrt(var) : 0;
				valid = value > 0;
			}

			private static double priceAt(IInstrument bars, int barsAgo)
			{
				double h = barsAgo == 0 ? bars.HighValue : bars.High[barsAgo];
				double l = barsAgo == 0 ? bars.LowValue : bars.Low[barsAgo];
				double c = barsAgo == 0 ? bars.CloseValue : bars.Close[barsAgo];
				if (h > 0 && l > 0) return (h + l + c) / 3.0;
				return c;
			}

			private static double volume(IInstrument bars, int barsAgo)
			{
				double v = barsAgo == 0 ? bars.VolumeValue : bars.Volume[barsAgo];
				if (v > 0) return v;
				return barsAgo == 0 ? bars.TicksValue : bars.Ticks[barsAgo];
			}
		}

		/// <summary>
		/// Rebuilds <see cref="m_confluenceZones"/> on the owning study.
		/// The zone list itself stays on <see cref="_VolumeProfileBase"/>.
		/// </summary>
		/// <summary>
		/// Rebuilds m_confluenceZones. One volume mass scores once per zone.
		/// POC / VAH / VAL / HVN from the same profile are labels, not extra
		/// confirmation. A profile whose window sits inside a larger profile
		/// is a refinement (0.25, once) and is not an independent source.
		/// Naked multiplies the surviving POC. VWAP is context only.
		/// Composite profiles are not built here: MultiCharts streams are the
		/// caller's profile list, and adjacent lookback windows do not overlap.
		/// Prior-day / prior-week high-low overlays stay off (no independence
		/// proof beyond the volume profiles and optional VWAP slots).
		/// </summary>
		private sealed class ConfluenceEngine
		{
			private sealed class Anchor
			{
				public int dataStream;
				public int profileStartBar;
				public int profileEndBar;
				public int profileAge;
				public int timeframeRank;
				public EConfluenceKind kind;
				public double price;
				public double volumeShare;
				public bool naked;
				public bool developing;
				public bool overlay;
				public string sourceVolumeId;
				public string profileId;
				public DateTime windowStart;
				public DateTime windowEnd;
			}

			private sealed class Survivor
			{
				public Anchor anchor;
				public double baseCredit;
				public double credit;
				public bool independent;
				public string absorbed = "";
				public string parentSource;
			}

			// One volume-share lookup per extra per rebuild. shareAt used to sum levels and
			// scan them twice per label (POC, VAH, VAL, and each node). Prices stay in the
			// readLevels order (ascending), so the first price within tick*0.51 is the same
			// hit the old linear scan returned. pocVol is the same fallback.
			private struct ShareSnap
			{
				public double total;
				public double tol;
				public double poc;
				public double pocVol;
				public double[] prices;
				public double[] vols;
			}

			private readonly _VolumeProfileBase m_owner;
			private readonly Dictionary<VpExtra, ShareSnap> m_shares = new Dictionary<VpExtra, ShareSnap>();
			private double m_htfVah;
			private double m_htfVal;
			private int m_htfRank = -1;
			private int m_htfAge = int.MaxValue;

			public ConfluenceEngine(_VolumeProfileBase owner)
			{
				m_owner = owner;
			}

			public void Rebuild()
			{
				m_owner.m_confluenceZones.Clear();
				m_shares.Clear();
				m_htfVah = 0;
				m_htfVal = 0;
				m_htfRank = -1;
				m_htfAge = int.MaxValue;

				List<Anchor> anchors = new List<Anchor>(64);
				collectAnchors(anchors);
				if (anchors.Count == 0) return;
				collapseEqualWindows(anchors);
				assignDevelopingSources(anchors);

				double tol = confluenceTolerance();
				anchors.Sort((a, b) => a.price.CompareTo(b.price));

				List<Anchor> cluster = new List<Anchor>();
				for (int i = 0; i < anchors.Count; i++)
				{
					if (cluster.Count == 0 || anchors[i].price - clusterHigh(cluster) <= tol)
					{
						cluster.Add(anchors[i]);
						continue;
					}
					emitClusters(cluster, 0, cluster.Count, tol);
					cluster.Clear();
					cluster.Add(anchors[i]);
				}
				emitClusters(cluster, 0, cluster.Count, tol);
				applyZScores();
			}

			// Anchors are sorted ascending by price before clustering and appended in that order,
			// so the cluster's high is its last element (VPB-18: was an O(n) scan per anchor).
			private static double clusterHigh(List<Anchor> cluster)
			{
				return cluster[cluster.Count - 1].price;
			}

			// Index range instead of GetRange. A split used to copy both sides; the anchors
			// stay in price order, so [lo, hi) is the same cluster the copy would have been.
			private void emitClusters(List<Anchor> cluster, int lo, int hi, double tol)
			{
				if (cluster == null || hi <= lo) return;
				double priceLo = cluster[lo].price;
				double priceHi = cluster[lo].price;
				for (int i = lo + 1; i < hi; i++)
				{
					if (cluster[i].price < priceLo) priceLo = cluster[i].price;
					if (cluster[i].price > priceHi) priceHi = cluster[i].price;
				}
				int count = hi - lo;
				if (priceHi - priceLo <= CLUSTER_SPLIT_TOL_MULTIPLE * tol + PRICE_EPSILON || count < CLUSTER_MIN_SPLIT_COUNT)
				{
					addZone(cluster, lo, hi, tol);
					return;
				}
				int split = -1;
				double gap = 0;
				for (int i = lo + 1; i < hi; i++)
				{
					double g = cluster[i].price - cluster[i - 1].price;
					if (g > gap)
					{
						gap = g;
						split = i;
					}
				}
				if (split <= lo || gap <= tol)
				{
					addZone(cluster, lo, hi, tol);
					return;
				}
				emitClusters(cluster, lo, split, tol);
				emitClusters(cluster, split, hi, tol);
			}

			private void collectAnchors(List<Anchor> anchors)
			{
				collectStream(m_owner.ConfluenceDataStream1, 0, m_owner.ConfluenceWeight1, anchors);
				collectStream(m_owner.ConfluenceDataStream2, 1, m_owner.ConfluenceWeight2, anchors);
				collectStream(m_owner.ConfluenceDataStream3, 2, m_owner.ConfluenceWeight3, anchors);
				if (m_owner.ConfluenceIncludeVwapSlots)
					collectVwap(anchors);
			}

			// Reads extras the base already stamped. Do not call ensureExtra, fillNodes,
			// applyProfileGeometry, stampNakedPocs, or pack.Items from here — that work
			// was repeated once per lookback profile and does not change the credit rules.
			private void collectStream(int datastream, int rank, double rankWeight, List<Anchor> anchors)
			{
				if (datastream < 1 || rankWeight <= 0) return;
				int current = m_owner.streamItemBarIndex(datastream);
				if (current < 1) return;
				List<VpExtra> extras = m_owner.confluenceLookbackExtras(datastream);
				for (int age = 0; age < extras.Count; age++)
				{
					VpExtra extra = extras[age];
					if (extra == null) continue;
					int start = extra.startBar >= 1 ? extra.startBar : current;
					addProfile(anchors, extra, datastream, start, age, rank, rankWeight, current);
				}
			}

			private void addProfile(List<Anchor> anchors, VpExtra extra, int datastream, int startBar, int age, int rank, double rankWeight, int current)
			{
				if (extra == null) return;
				int endBar = extra.endBar >= startBar ? extra.endBar : startBar;
				if (endBar > current) endBar = current;
				bool developing = !extra.pocComplete;
				// Built once per profile. Labels share it; absorbed labels do not get their own id.
				string profileId = "ds" + datastream.ToString() + ":s" + startBar.ToString();
				DateTime ws = m_owner.barTime(datastream, startBar);
				DateTime we = m_owner.barTime(datastream, endBar);
				double tick = m_owner.tickSize();
				ShareSnap snap = snapFor(extra, tick);

				noteHtf(extra, rank, age);
				addLevel(anchors, extra, datastream, startBar, endBar, age, rank, rankWeight, developing, profileId, ws, we,
					extra.pocNaked ? EConfluenceKind.NakedPoc : EConfluenceKind.Poc, extra.poc, shareAt(snap, extra.poc), extra.pocNaked);
				addLevel(anchors, extra, datastream, startBar, endBar, age, rank, rankWeight, developing, profileId, ws, we,
					EConfluenceKind.Vah, extra.vah, shareAt(snap, extra.vah), false);
				addLevel(anchors, extra, datastream, startBar, endBar, age, rank, rankWeight, developing, profileId, ws, we,
					EConfluenceKind.Val, extra.val, shareAt(snap, extra.val), false);

				if (m_owner.ConfluenceIncludeHvns)
				{
					addNodes(anchors, extra, extra.hvns, false, datastream, startBar, endBar, age, rank, rankWeight, developing, profileId, ws, we, snap);
					addNodes(anchors, extra, extra.lvns, true, datastream, startBar, endBar, age, rank, rankWeight, developing, profileId, ws, we, snap);
				}
			}

			private void addNodes(List<Anchor> anchors, VpExtra extra, List<Node> nodes, bool lvn, int datastream, int startBar, int endBar, int age, int rank, double rankWeight, bool developing, string profileId, DateTime ws, DateTime we, ShareSnap snap)
			{
				if (nodes == null || nodes.Count == 0) return;
				List<int> order = new List<int>();
				for (int i = 0; i < nodes.Count; i++)
				{
					if (nodes[i].isPoc) continue;
					if (nodes[i].peak <= 0) continue;
					order.Add(i);
				}
				order.Sort((a, b) => lvn
					? nodes[a].strength.CompareTo(nodes[b].strength)
					: nodes[b].strength.CompareTo(nodes[a].strength));
				int cap = order.Count < ZONE_TOP_SURVIVORS ? order.Count : ZONE_TOP_SURVIVORS;
				for (int i = 0; i < cap; i++)
				{
					Node n = nodes[order[i]];
					addLevel(anchors, extra, datastream, startBar, endBar, age, rank, rankWeight, developing, profileId, ws, we,
						lvn ? EConfluenceKind.Lvn : EConfluenceKind.Hvn, n.peak, shareAt(snap, n.peak), false);
				}
			}

			private void addLevel(List<Anchor> anchors, VpExtra extra, int datastream, int startBar, int endBar, int age, int rank, double rankWeight, bool developing, string profileId, DateTime ws, DateTime we, EConfluenceKind kind, double price, double share, bool naked)
			{
				if (price <= 0) return;
				anchors.Add(new Anchor
				{
					dataStream = datastream,
					profileStartBar = startBar,
					profileEndBar = endBar,
					profileAge = age,
					timeframeRank = rank,
					kind = kind,
					price = price,
					volumeShare = share,
					naked = naked,
					developing = developing,
					overlay = false,
					sourceVolumeId = profileId,
					profileId = profileId,
					windowStart = ws,
					windowEnd = we
				});
			}

			private void noteHtf(VpExtra extra, int rank, int age)
			{
				if (extra == null || extra.vah <= 0 || extra.val <= 0 || extra.vah < extra.val) return;
				if (rank < m_htfRank) return;
				if (rank == m_htfRank && age >= m_htfAge) return;
				m_htfRank = rank;
				m_htfAge = age;
				m_htfVah = extra.vah;
				m_htfVal = extra.val;
			}

			private void collectVwap(List<Anchor> anchors)
			{
				addVwap(anchors, m_owner.m_vwap1, EConfluenceKind.Vwap1);
				addVwap(anchors, m_owner.m_vwap2, EConfluenceKind.Vwap2);
				addVwap(anchors, m_owner.m_vwap3, EConfluenceKind.Vwap3);
			}

			private void addVwap(List<Anchor> anchors, VwapEngine eng, EConfluenceKind kind)
			{
				if (eng == null || eng.type == EVwapType.Disabled || !eng.valid || eng.value <= 0) return;
				int ds = eng.datastream;
				double w = rankWeightFor(ds);
				if (w <= 0) return;
				string name = kind.ToString();
				anchors.Add(new Anchor
				{
					dataStream = ds,
					profileStartBar = -1,
					profileEndBar = -1,
					profileAge = 0,
					timeframeRank = slotRank(ds),
					kind = kind,
					price = eng.value,
					volumeShare = 0,
					naked = false,
					developing = false,
					overlay = true,
					sourceVolumeId = "overlay:" + name,
					profileId = "overlay:" + name,
					windowStart = DateTime.MinValue,
					windowEnd = DateTime.MinValue
				});
			}

			private double rankWeightFor(int datastream)
			{
				if (datastream == m_owner.ConfluenceDataStream1) return m_owner.ConfluenceWeight1;
				if (datastream == m_owner.ConfluenceDataStream2) return m_owner.ConfluenceWeight2;
				if (datastream == m_owner.ConfluenceDataStream3) return m_owner.ConfluenceWeight3;
				return 0;
			}

			private int slotRank(int datastream)
			{
				if (datastream > 0 && datastream == m_owner.ConfluenceDataStream3) return 2;
				if (datastream > 0 && datastream == m_owner.ConfluenceDataStream2) return 1;
				return 0;
			}

			private void collapseEqualWindows(List<Anchor> anchors)
			{
				for (int i = 0; i < anchors.Count; i++)
				{
					Anchor a = anchors[i];
					if (a.overlay || a.windowStart == DateTime.MinValue) continue;
					for (int j = 0; j < i; j++)
					{
						Anchor b = anchors[j];
						if (b.overlay || b.windowStart == DateTime.MinValue) continue;
						if (a.profileId == b.profileId) continue;
						if (Math.Abs((a.windowStart - b.windowStart).TotalSeconds) > 1) continue;
						if (Math.Abs((a.windowEnd - b.windowEnd).TotalSeconds) > 1) continue;
						string keep = b.timeframeRank >= a.timeframeRank ? b.profileId : a.profileId;
						string drop = keep == a.profileId ? b.profileId : a.profileId;
						for (int k = 0; k < anchors.Count; k++)
							if (anchors[k].sourceVolumeId == drop)
								anchors[k].sourceVolumeId = keep;
					}
				}
			}

			private void assignDevelopingSources(List<Anchor> anchors)
			{
				for (int i = 0; i < anchors.Count; i++)
				{
					Anchor child = anchors[i];
					if (child.overlay || !child.developing) continue;
					Anchor parent = smallestContainer(anchors, child, true);
					if (parent == null) continue;
					child.sourceVolumeId = parent.profileId;
				}
			}

			private readonly HashSet<string> m_seenScratch = new HashSet<string>();   // reused by smallestContainer (VPB-18)

			private Anchor smallestContainer(List<Anchor> anchors, Anchor child, bool completedOnly)
			{
				Anchor best = null;
				double bestSpan = double.MaxValue;
				var seen = m_seenScratch;
				seen.Clear();
				for (int i = 0; i < anchors.Count; i++)
				{
					Anchor parent = anchors[i];
					if (parent.overlay || parent.profileId == child.profileId) continue;
					if (completedOnly && parent.developing) continue;
					if (!seen.Add(parent.profileId)) continue;
					if (!contains(parent, child)) continue;
					double span = windowSpan(parent);
					if (span < bestSpan)
					{
						bestSpan = span;
						best = parent;
					}
				}
				return best;
			}

			private static bool contains(Anchor parent, Anchor child)
			{
				if (parent == null || child == null) return false;
				if (parent.windowStart != DateTime.MinValue && child.windowStart != DateTime.MinValue)
				{
					if (parent.windowStart > child.windowStart) return false;
					if (parent.windowEnd < child.windowEnd) return false;
					return windowSpan(parent) > windowSpan(child) + 1.0;
				}
				if (parent.dataStream != child.dataStream) return false;
				return parent.profileStartBar <= child.profileStartBar
					&& parent.profileEndBar >= child.profileEndBar
					&& parent.profileStartBar != child.profileStartBar;
			}

			private static double windowSpan(Anchor a)
			{
				if (a.windowStart != DateTime.MinValue && a.windowEnd >= a.windowStart)
					return (a.windowEnd - a.windowStart).TotalSeconds;
				return Math.Max(0, a.profileEndBar - a.profileStartBar);
			}

			private void addZone(List<Anchor> cluster, int loIdx, int hiIdx, double tol)
			{
				List<Survivor> survivors = collapseSameSource(cluster, loIdx, hiIdx);
				if (survivors.Count == 0) return;
				applyNested(survivors);

				ConfluenceZone zone = new ConfluenceZone();
				double lo = cluster[loIdx].price;
				double hi = cluster[loIdx].price;
				for (int i = loIdx + 1; i < hiIdx; i++)
				{
					if (cluster[i].price < lo) lo = cluster[i].price;
					if (cluster[i].price > hi) hi = cluster[i].price;
				}

				double acc = 0;
				double rej = 0;
				double ctx = 0;
				int independent = 0;
				var sources = new HashSet<string>();
				var streams = new HashSet<int>();
				Survivor anchorPick = null;

				for (int i = 0; i < survivors.Count; i++)
				{
					Survivor s = survivors[i];
					if (s.credit <= 0) continue;
					if (isOverlay(s.anchor.kind))
						ctx += s.credit;
					else if (isRejection(s.anchor.kind))
						rej += s.credit;
					else
						acc += s.credit;

					if (s.independent)
					{
						independent++;
						sources.Add(s.anchor.sourceVolumeId);
						streams.Add(s.anchor.dataStream);
					}
					zone.hits.Add(toHit(s));
					if (anchorPick == null || betterAnchor(s, anchorPick))
						anchorPick = s;
				}
				if (anchorPick == null) return;
				if (acc <= 0 && rej <= 0) return;

				for (int i = 0; i < survivors.Count; i++)
				{
					Survivor s = survivors[i];
					if (s.credit > 0) continue;
					if (string.IsNullOrEmpty(s.parentSource)) continue;
					zone.dropped.Add(new ConfluenceDrop
					{
						dataStream = s.anchor.dataStream,
						profileStartBar = s.anchor.profileStartBar,
						kind = s.anchor.kind,
						price = s.anchor.price,
						sourceVolumeId = s.anchor.profileId,
						reason = "nested_in:" + s.parentSource
					});
				}

				double anchor = anchorPick.anchor.price;
				zone.low = lo;
				zone.high = hi;
				zone.anchor = anchor;
				zone.center = anchor;
				zone.acceptanceScore = acc;
				zone.rejectionScore = rej;
				zone.contextScore = ctx;
				zone.rawScore = acc + rej;
				zone.independentSources = independent;
				zone.profileCount = independent;
				zone.streamCount = streams.Count;
				zone.hitCount = zone.hits.Count;
				zone.role = acc > 0 && rej > 0 ? EConfluenceRole.Mixed : pickRole(survivors, acc, rej);
				zone.location = locate(anchor);
				zone.suggestedUse = zone.role == EConfluenceRole.Mixed ? ESuggestedUse.Conflict
					: (zone.role == EConfluenceRole.Rejection ? ESuggestedUse.Entry : ESuggestedUse.Target);
				zone.exitPrice = zone.suggestedUse == ESuggestedUse.Target ? anchor : 0;
				zone.entryPrice = 0;
				zone.invalidationBelow = lo - tol;
				zone.invalidationAbove = hi + tol;
				m_owner.m_confluenceZones.Add(zone);
			}

			private List<Survivor> collapseSameSource(List<Anchor> cluster, int lo, int hi)
			{
				var groups = new Dictionary<string, List<Anchor>>();
				for (int i = lo; i < hi; i++)
				{
					Anchor a = cluster[i];
					string id = string.IsNullOrEmpty(a.sourceVolumeId) ? a.profileId : a.sourceVolumeId;
					if (!groups.TryGetValue(id, out List<Anchor> list))
					{
						list = new List<Anchor>();
						groups[id] = list;
					}
					list.Add(a);
				}

				List<Survivor> survivors = new List<Survivor>();
				foreach (var kv in groups)
				{
					List<Anchor> list = kv.Value;
					Anchor bestAcc = null;
					Anchor bestRej = null;
					Anchor bestOver = null;
					for (int i = 0; i < list.Count; i++)
					{
						Anchor a = list[i];
						if (isOverlay(a.kind))
						{
							if (bestOver == null) bestOver = a;
							continue;
						}
						if (isRejection(a.kind))
						{
							if (bestRej == null || a.volumeShare < bestRej.volumeShare)
								bestRej = a;
							continue;
						}
						if (bestAcc == null || kindRank(a.kind) > kindRank(bestAcc.kind))
							bestAcc = a;
					}
					if (bestAcc != null)
						survivors.Add(makeSurvivor(bestAcc, absorbed(list, bestAcc), true));
					if (bestRej != null)
						survivors.Add(makeSurvivor(bestRej, absorbed(list, bestRej), true));
					if (bestOver != null)
						survivors.Add(makeSurvivor(bestOver, "", false));
				}
				return survivors;
			}

			private Survivor makeSurvivor(Anchor a, string absorbedLabels, bool volume)
			{
				double weight = rankWeightFor(a.dataStream);
				if (weight <= 0) weight = 1.0 + FALLBACK_TIMEFRAME_WEIGHT_STEP * a.timeframeRank;
				double credit;
				if (a.overlay)
					credit = DEFAULT_CONFLUENCE_OVERLAY_FACTOR * weight;
				else
				{
					credit = weight * shareFactor(a.volumeShare);
					if (a.naked && a.kind == EConfluenceKind.NakedPoc)
						credit *= DEFAULT_CONFLUENCE_NAKED_MULTIPLIER;
				}
				return new Survivor
				{
					anchor = a,
					baseCredit = credit,
					credit = credit,
					independent = volume,
					absorbed = absorbedLabels
				};
			}

			private static string absorbed(List<Anchor> list, Anchor kept)
			{
				var text = new System.Text.StringBuilder();
				for (int i = 0; i < list.Count; i++)
				{
					Anchor a = list[i];
					if (object.ReferenceEquals(a, kept)) continue;
					if (isOverlay(a.kind) || isRejection(a.kind) != isRejection(kept.kind)) continue;
					if (text.Length > 0) text.Append(',');
					text.Append(a.kind.ToString());
				}
				return text.ToString();
			}

			private void applyNested(List<Survivor> survivors)
			{
				bool[] full = new bool[survivors.Count];
				for (int i = 0; i < survivors.Count; i++)
					full[i] = survivors[i].independent && !survivors[i].anchor.overlay;

				var claimed = new HashSet<string>();
				for (int i = 0; i < survivors.Count; i++)
				{
					if (!full[i]) continue;
					Survivor child = survivors[i];
					Survivor parent = null;
					double bestSpan = double.MaxValue;
					for (int j = 0; j < survivors.Count; j++)
					{
						if (i == j || !full[j]) continue;
						Survivor maybe = survivors[j];
						if (maybe.anchor.sourceVolumeId == child.anchor.sourceVolumeId) continue;
						if (!contains(maybe.anchor, child.anchor)) continue;
						double span = windowSpan(maybe.anchor);
						if (span < bestSpan)
						{
							bestSpan = span;
							parent = maybe;
						}
					}
					if (parent == null) continue;

					child.parentSource = parent.anchor.sourceVolumeId;
					child.independent = false;
					bool parentNested = false;
					for (int k = 0; k < survivors.Count; k++)
					{
						if (!full[k] || survivors[k] == parent) continue;
						if (contains(survivors[k].anchor, parent.anchor)) parentNested = true;
					}
					bool sameClass = isRejection(parent.anchor.kind) == isRejection(child.anchor.kind);
					if (parentNested || !sameClass || !claimed.Add(parent.anchor.sourceVolumeId))
					{
						child.credit = 0;
						continue;
					}
					double bonus = DEFAULT_CONFLUENCE_REFINEMENT * parent.credit;
					bonus = Math.Min(bonus, Math.Max(0.0, (MAX_PARENT_CREDIT_MULTIPLIER - 1.0) * parent.credit));   // VPB-14: named cap (no-op at 0.25)
					child.credit = bonus;
				}
			}

			private static bool betterAnchor(Survivor s, Survivor current)
			{
				if (s.anchor.overlay && !current.anchor.overlay) return false;
				if (!s.anchor.overlay && current.anchor.overlay) return true;
				int c = s.credit.CompareTo(current.credit);
				if (c != 0) return c > 0;
				if (s.anchor.timeframeRank != current.anchor.timeframeRank)
					return s.anchor.timeframeRank > current.anchor.timeframeRank;
				return kindRank(s.anchor.kind) > kindRank(current.anchor.kind);
			}

			private static EConfluenceRole pickRole(List<Survivor> survivors, double acc, double rej)
			{
				bool naked = false;
				int accRank = -1;
				int rejRank = -1;
				for (int i = 0; i < survivors.Count; i++)
				{
					Survivor s = survivors[i];
					if (s.credit <= 0 || s.anchor.overlay) continue;
					if (isRejection(s.anchor.kind))
					{
						if (s.anchor.timeframeRank > rejRank) rejRank = s.anchor.timeframeRank;
					}
					else
					{
						if (s.anchor.timeframeRank > accRank) accRank = s.anchor.timeframeRank;
						if (s.anchor.kind == EConfluenceKind.NakedPoc) naked = true;
					}
				}
				if (acc > 0 && rej > 0)
				{
					if (rejRank > accRank) return EConfluenceRole.Rejection;
					return naked ? EConfluenceRole.Magnet : EConfluenceRole.Acceptance;
				}
				if (rej > 0) return EConfluenceRole.Rejection;
				if (naked) return EConfluenceRole.Magnet;
				return EConfluenceRole.Acceptance;
			}

			private EConfluenceLocation locate(double price)
			{
				if (m_htfVah <= 0 || m_htfVal <= 0) return EConfluenceLocation.Unknown;
				if (price > m_htfVah) return EConfluenceLocation.Premium;
				if (price < m_htfVal) return EConfluenceLocation.Discount;
				return EConfluenceLocation.InsideValue;
			}

			private static ConfluenceHit toHit(Survivor s)
			{
				return new ConfluenceHit
				{
					dataStream = s.anchor.dataStream,
					profileStartBar = s.anchor.profileStartBar,
					profileAge = s.anchor.profileAge,
					kind = s.anchor.kind,
					price = s.anchor.price,
					weight = s.credit,
					sourceVolumeId = s.anchor.sourceVolumeId,
					absorbedLabels = s.absorbed,
					credit = s.credit,
					volumeShare = s.anchor.volumeShare,
					naked = s.anchor.naked,
					independent = s.independent
				};
			}

			private static bool isRejection(EConfluenceKind k)
			{
				return k == EConfluenceKind.Lvn;
			}

			private static bool isOverlay(EConfluenceKind k)
			{
				return k == EConfluenceKind.Vwap1 || k == EConfluenceKind.Vwap2 || k == EConfluenceKind.Vwap3;
			}

			private static int kindRank(EConfluenceKind k)
			{
				if (k == EConfluenceKind.NakedPoc || k == EConfluenceKind.Poc) return 3;
				if (k == EConfluenceKind.Vah || k == EConfluenceKind.Val) return 2;
				if (k == EConfluenceKind.Hvn) return 1;
				return 0;
			}

			private static double shareFactor(double share)
			{
				double f = share / DEFAULT_CONFLUENCE_SHARE_PIVOT;
				if (f < SHARE_FACTOR_MIN) return SHARE_FACTOR_MIN;
				if (f > SHARE_FACTOR_MAX) return SHARE_FACTOR_MAX;
				return f;
			}

			private ShareSnap snapFor(VpExtra extra, double tick)
			{
				ShareSnap snap;
				if (extra != null && m_shares.TryGetValue(extra, out snap)) return snap;
				snap = new ShareSnap();
				snap.tol = tick > 0 ? tick * LEVEL_MATCH_TICK_FRACTION : 1e-6;
				if (extra == null || extra.levels == null || extra.levels.Count == 0)
				{
					if (extra != null) m_shares[extra] = snap;
					return snap;
				}
				snap.poc = extra.poc;
				snap.pocVol = extra.pocVol;
				int n = extra.levels.Count;
				snap.prices = new double[n];
				snap.vols = new double[n];
				double total = 0;
				for (int i = 0; i < n; i++)
				{
					snap.prices[i] = extra.levels[i].price;
					snap.vols[i] = extra.levels[i].total;
					total += extra.levels[i].total;
				}
				snap.total = total;
				m_shares[extra] = snap;
				return snap;
			}

			// Same result as the old two-pass scan: first level within tick*0.51, else pocVol
			// at the POC price, else DEFAULT_CONFLUENCE_SHARE_PIVOT. Empty levels use the pivot.
			private static double shareAt(ShareSnap snap, double price)
			{
				if (price <= 0 || snap.prices == null || snap.prices.Length == 0)
					return DEFAULT_CONFLUENCE_SHARE_PIVOT;
				if (snap.total <= 0) return DEFAULT_CONFLUENCE_SHARE_PIVOT;
				double tol = snap.tol > 0 ? snap.tol : 1e-6;
				double at = 0;
				int lo = 0;
				int hi = snap.prices.Length;
				double floor = price - tol;
				while (lo < hi)
				{
					int mid = lo + ((hi - lo) / 2);
					if (snap.prices[mid] < floor) lo = mid + 1;
					else hi = mid;
				}
				if (lo < snap.prices.Length && Math.Abs(snap.prices[lo] - price) <= tol)
					at = snap.vols[lo];
				if (at <= 0 && snap.pocVol > 0 && Math.Abs(snap.poc - price) <= tol)
					at = snap.pocVol;
				if (at <= 0) return DEFAULT_CONFLUENCE_SHARE_PIVOT;
				return at / snap.total;
			}

			private void applyZScores()
			{
				List<ConfluenceZone> zones = m_owner.m_confluenceZones;
				int n = zones.Count;
				if (n == 0) return;
				if (n == 1)
				{
					zones[0].zScore = 0;
					return;
				}
				double mean = 0;
				for (int i = 0; i < n; i++)
					mean += zones[i].rawScore;
				mean /= n;
				double var = 0;
				for (int i = 0; i < n; i++)
				{
					double d = zones[i].rawScore - mean;
					var += d * d;
				}
				double stdev = Math.Sqrt(var / n);
				if (stdev <= 0)
				{
					for (int i = 0; i < n; i++)
						zones[i].zScore = 0;
					return;
				}
				for (int i = 0; i < n; i++)
					zones[i].zScore = (zones[i].rawScore - mean) / stdev;
			}

			private double confluenceTolerance()
			{
				int ticks = m_owner.ConfluenceAlignTicks;
				double tick = m_owner.tickSize();
				if (tick <= 0) tick = FALLBACK_TICK;
				return ticks * tick;
			}
		}

		//properties
		// [Input] properties are plain auto-properties. Their bounds are checked once in StartCalc.
		// Non-[Input] properties keep the check in the getter.
		[Input] public double HvnPercentile { get; set; }  // Percentile of POC that would be considered high volume nodes. 0..1, and above LvnPercentile.
		[Input] public double LvnPercentile { get; set; }  // Percentile of POC that would be considered low volume nodes. 0..1, and below HvnPercentile.
		[Input] public int MinNodeTicks { get; set; }      // Drop HVN/LVN whose high-low span is under this many ticks. >= 1.
		[Input] public bool TrackNakedPoc { get; set; }        // Stamp pocComplete / pocNaked / pocTouchedBar. Default true.
		[Input] public int NakedPocTickTolerance { get; set; } // Ticks of range overlap that mitigate a completed POC. >= 0. Default 1. 0 = exact inclusion.

		/// <summary>
		/// VPB-03. When false (default) a profile whose window extends past the calculation bar (backtest only) contributes only
		/// POCForBar/HighVAForBar/LowVAForBar values as of the calc bar: no histogram, no end-of-window fallbacks. Set true in a
		/// visualization study to get the old whole-window display. Deliberately not an [Input] so an optimization cannot flip it.
		/// </summary>
		protected bool AllowLookAheadHistogram { get; set; }

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
		private int m_confluenceMinHits = DEFAULT_CONFLUENCE_MIN_HITS;
		private int m_confluenceTopCount = DEFAULT_CONFLUENCE_TOP_COUNT;
		private double m_confluenceSameStreamDecay = DEFAULT_CONFLUENCE_DECAY;
		private double m_confluenceCrossStreamBonus = DEFAULT_CONFLUENCE_CROSS_BONUS;
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
		/// Retained for compatibility. Emission does not use it — a single POC is a zone. Valid: ≥ 1. Default 2.
		/// </summary>
		public int ConfluenceMinHits
		{
			get { return m_confluenceMinHits; }
			set { requireAtLeast("ConfluenceMinHits", value, 1); m_confluenceMinHits = value; }
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
		/// Same-stream age decay is retained but not applied. An older session is its own volume mass;
		/// Valid: 0.0–1.0. Default 0.75. 1.0 keeps older profiles at full weight.
		/// </summary>
		public double ConfluenceSameStreamDecay
		{
			get { return m_confluenceSameStreamDecay; }
			set { requireUnitInterval("ConfluenceSameStreamDecay", value); m_confluenceSameStreamDecay = value; }
		}
		/// <summary>
		/// Retained for compatibility and not applied. A second stream is either an independent
		/// source or a nested refinement; a bonus would count it again. Valid: ≥ 0. Default 0.35.
		/// </summary>
		public double ConfluenceCrossStreamBonus
		{
			get { return m_confluenceCrossStreamBonus; }
			set { requireNonNegative("ConfluenceCrossStreamBonus", value); m_confluenceCrossStreamBonus = value; }
		}
		/// <summary>
		/// When true, HVN peaks and LVN troughs (other than the POC) are confluence anchors. Default true.
		/// </summary>
		public bool ConfluenceIncludeHvns { get; set; }
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
			requireNonNegative("NakedPocTickTolerance", NakedPocTickTolerance);
			requireAtLeast("Vwap1Length", Vwap1Length, 1);
			requireAtLeast("Vwap2Length", Vwap2Length, 1);
			requireAtLeast("Vwap3Length", Vwap3Length, 1);
			requireDataStream("Vwap1DataStream", Vwap1DataStream, Vwap1Type);
			requireDataStream("Vwap2DataStream", Vwap2DataStream, Vwap2Type);
			requireDataStream("Vwap3DataStream", Vwap3DataStream, Vwap3Type);
		}

		private static void requireUnitInterval(string name, double value)
		{
			if (value < 0.0 || value > 1.0)
				throw new ArgumentException(name + " must be between 0.0 and 1.0.");
		}

		private static void requireAtLeast(string name, int value, int min)
		{
			if (value < min)
				throw new ArgumentException(name + " must be " + min + " or greater.");
		}

		private static void requireNonNegative(string name, double value)
		{
			if (value < 0.0)
				throw new ArgumentException(name + " must be 0.0 or greater.");
		}

		private static void requireNonNegative(string name, int value)
		{
			if (value < 0)
				throw new ArgumentException(name + " must be 0 or greater.");
		}

		private void requireDataStream(string name, int stream)
		{
			if (stream < 1)
				throw new ArgumentException(name + " must be a chart data stream index >= 1 (1 = Data1, 2 = Data2, …).");
			if (MaxDataStream >= 1 && stream > MaxDataStream)
				throw new ArgumentException(name + " = " + stream + " is not on this chart (MaxDataStream = " + MaxDataStream + ").");
		}

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
		private VwapEngine m_vwap1;
		private VwapEngine m_vwap2;
		private VwapEngine m_vwap3;
		private readonly List<ConfluenceZone> m_confluenceZones = new List<ConfluenceZone>();
		private ConfluenceEngine m_confluenceEngine;
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
		private double m_tick;   // tickSize() cache, reset in StartCalc
		// Grow-only gather buffers. Shrinking every bar was pure allocation. rankPercentile
		// takes the filled count so the spare tail is not part of the CDF.

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
			ConfluenceMinHits = DEFAULT_CONFLUENCE_MIN_HITS;
			ConfluenceTopCount = DEFAULT_CONFLUENCE_TOP_COUNT;
			ConfluenceSameStreamDecay = DEFAULT_CONFLUENCE_DECAY;
			ConfluenceCrossStreamBonus = DEFAULT_CONFLUENCE_CROSS_BONUS;
			ConfluenceIncludeHvns = true;
			ConfluenceIncludeVwapSlots = true;
			ConfluenceTargetMinZScore = 0;
			ConfluenceTargetClearTicks = DEFAULT_CONFLUENCE_TARGET_CLEAR_TICKS;
			ConfluenceTargetCount = DEFAULT_CONFLUENCE_TOP_COUNT;
			m_confluenceDirty = true;
			m_confluenceGeneration = 1;
			m_confluenceBuiltGeneration = -1;
			m_volumePctDirty = true;

			m_streams = new Dictionary<int, StreamPack>();
			m_vwap1 = new VwapEngine { label = "VWAP1" };
			m_vwap2 = new VwapEngine { label = "VWAP2" };
			m_vwap3 = new VwapEngine { label = "VWAP3" };
			m_confluenceEngine = new ConfluenceEngine(this);
		}

		protected override void Destroy()
		{
			unhookStreams();
			base.Destroy();
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_tick = 0;   // re-resolved on first use (VPB-20)
			validateInputs();
			unhookStreams();
			m_streams.Clear();
			hookStreams();
			configureVwap(m_vwap1, Vwap1Type, Vwap1Length, Vwap1FromDate, Vwap1DataStream, "VwapFromDate", "VwapDataStream");
			configureVwap(m_vwap2, Vwap2Type, Vwap2Length, Vwap2FromDate, Vwap2DataStream, "Vwap2FromDate", "Vwap2DataStream");
			configureVwap(m_vwap3, Vwap3Type, Vwap3Length, Vwap3FromDate, Vwap3DataStream, "Vwap3FromDate", "Vwap3DataStream");
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
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			processPendingProfileChanges();
			stepVwap(m_vwap1);
			stepVwap(m_vwap2);
			stepVwap(m_vwap3);
			stampCurrentExtras();
			m_confluenceDirty = true;
			refreshVolumePercentiles();
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
			if (!m_streams.TryGetValue(datastream, out StreamPack streamPack) || streamPack.pack == null)
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
			if (m_streams == null || !m_streams.TryGetValue(datastream, out StreamPack sp) || sp == null)
				return hit;
			stampNakedPocs(sp);
			foreach (VpExtra extra in sp.extra.Values)
			{
				if (extra != null && extra.IsNakedPoc(asOfBar))
					hit.Add(extra);
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
			if (!m_streams.TryGetValue(datastream, out StreamPack streamPack) || streamPack.pack == null)
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
		private static int slotStream(VwapEngine eng)
		{
			if (eng == null || eng.type == EVwapType.Disabled) return 0;
			return eng.datastream;
		}

		private void warnIfLengthExceedsMaxBarsBack(VwapEngine eng)
		{
			if (eng == null || eng.type != EVwapType.Length) return;
			if (eng.length - 1 > ExecInfo.MaxBarsBack)
				trace(ETraceLevel.Warning, eng.label + ": Length " + eng.length + " needs MaxBarsBack >= " + (eng.length - 1) +
							" (currently " + ExecInfo.MaxBarsBack + "). Use auto-detect or raise it.");
		}

		private void configureVwap(VwapEngine eng, EVwapType type, int length, string fromText, int stream, string dateInputName, string streamInputName)
		{
			eng.parent = this;
			eng.type = type;
			eng.length = length;
			eng.datastream = 0;
			eng.fromDate = DateTime.MinValue;
			eng.Reset();

			if (type == EVwapType.Disabled) return;

			eng.datastream = stream;
			if (type != EVwapType.AdaptiveFromDate) return;

			if (!tryParseDate(fromText, out DateTime dt))
				throw new ArgumentException(dateInputName + " must be a date string (yyyy-MM-dd or yyyy-MM-dd HH:mm[:ss]) when Type is AdaptiveFromDate.");
			eng.fromDate = dt;
		}

		private void stepVwap(VwapEngine eng)
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
		private int streamItemBarIndex(int stream)
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
		private static int toNativeItemBar(int chartBar)
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
				traceProfileBoundary(sp, chartBar);   // Phase-0 diagnostic, Debug trace only
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
		/// Phase-0 diagnostic for VPB-07 / VPB-03 / VPB-21 (TraceLevel Debug only; no behaviour change). On the first bar
		/// of a new profile it logs the native BarsInterval of the profiles ItemForBar returns for this bar and the bar before:
		///  - cur.First == native: BarsInterval start is inclusive, keep the current code (do NOT apply VPB-07 step 2);
		///    cur.First == native - 1 == prev.Second: start is exclusive, apply VPB-07 step 2.
		///  - cur.Second > native on a historical (realtime=False) bar: MultiCharts built the whole window up front, so the
		///    VPB-03 look-ahead guard is doing real work; cur.Second == native throughout: it is a harmless no-op.
		///  - cur found via ItemForBar(native): 0-based indexing confirmed, the VPB-21 1-based fallback can be removed.
		/// </summary>
		private void traceProfileBoundary(StreamPack sp, int chartBar)
		{
			if (!traceOn(ETraceLevel.Debug) || sp == null || sp.pack == null || chartBar < 2) return;
			int native = toNativeItemBar(chartBar);
			IProfile cur = rawNativeItemForBar(sp, sp.datastream, native);
			IProfile prev = rawNativeItemForBar(sp, sp.datastream, native - 1);
			if (cur == null || prev == null || object.ReferenceEquals(cur, prev)) return;
			try
			{
				trace(ETraceLevel.Debug, $"VP-PHASE0 Data{sp.datastream} boundary native={native} realtime={Environment.IsRealTimeCalc} cur=({cur.BarsInterval.First},{cur.BarsInterval.Second}) prev=({prev.BarsInterval.First},{prev.BarsInterval.Second})");
			}
			catch (Exception e)
			{
				trace(ETraceLevel.Debug, $"VP-PHASE0 Data{sp.datastream} boundary native={native} BarsInterval unreadable - {e.Message}");
			}
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
			if (!TrackNakedPoc)
			{
				// Clear once. New extras default to not-naked, and nothing else sets the flags
				// while this returns early. Turning the input back on clears the flag below.
				if (sp.nakedSuppressed) return;
				foreach (VpExtra extra in sp.extra.Values)
					clearNakedPoc(extra);
				sp.nakedSuppressed = true;
				sp.nakedStampedBar = -1;
				return;
			}
			sp.nakedSuppressed = false;

			int current = streamItemBarIndex(sp.datastream);
			if (current < 1) return;
			IInstrument bars = barsOf(sp.datastream);

			// VPB-16: called from CalcBar and from every query; stamp once per exact state. ensureExtra invalidates
			// the stamp whenever it (re)fills or re-stamps a developing extra, so each CalcBar tick still rescans.
			bool atClose = bars == null || bars.Status == EBarState.Close;
			if (sp.nakedStampedBar == current && sp.nakedStampedGeneration == sp.generation
				&& sp.nakedStampedExtraCount == sp.extra.Count && (sp.nakedStampedAtClose || !atClose))
				return;

			double tick = tickSize();
			// Half a micro-tick of slack so an exact touch is never lost to rounding.
			double tol = tick > 0 ? NakedPocTickTolerance * tick + tick * 1e-6 : PRICE_EPSILON;

			foreach (VpExtra extra in sp.extra.Values)
			{
				if (extra == null) continue;
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
		/// Unfiltered ItemForBar for a 1-based chart bar. Tries the native 0-based
		/// index (barIndex - 1) first, then the 1-based Status index, because some
		/// builds accept the Status index directly. Does not check that the returned
		/// profile owns the bar — itemForBar / considerProfile do that.
		/// VPB-21: bars after the stream's calc bar are never probed (they throw in real time and are
		/// future objects in a backtest); the 1-based fallback is only tried when its native index
		/// (barIndex) is not past the calc bar either. Removing the fallback entirely waits on the
		/// VPB-07 diagnostic (ItemForBar(Current - 1) returns the current profile).
		/// </summary>
		/// <param name="barIndex">1-based chart bar. Converted to native 0-based for pack.ItemForBar.</param>
		private IProfile rawItemForBar(StreamPack streamPack, int datastream, int barIndex)
		{
			if (streamPack == null || streamPack.pack == null || barIndex < 1) return null;
			int current = streamItemBarIndex(datastream);
			if (current >= 1 && barIndex > current) return null;   // not built yet / future: never probe
			int native = toNativeItemBar(barIndex);
			IProfile vp = rawNativeItemForBar(streamPack, datastream, native);
			if (vp != null) return vp;
			// Some builds accept the 1-based Status index directly (native index barIndex == chart bar barIndex + 1).
			if (current >= 1 && barIndex >= current) return null;
			return rawNativeItemForBar(streamPack, datastream, barIndex);
		}

		/// <summary>
		/// pack.ItemForBar(nativeBar). Swallows the exception MultiCharts throws when
		/// the index is out of range, the pack is disposed, or no profile is registered
		/// for that bar yet. Index convention is the caller's problem: rawItemForBar
		/// passes both the 0-based and the 1-based index.
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
			if (m_confluenceEngine == null) m_confluenceEngine = new ConfluenceEngine(this);
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
		private List<VpExtra> confluenceLookbackExtras(int datastream)
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

			try
			{
				foreach (IProfile vp in sp.pack.Items)
				{
					if (vp == null || vp.Empty) continue;
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
		private DateTime barTime(int stream, int chartBar)
		{
			IInstrument bars = barsOf(stream);
			if (bars == null || chartBar < 1) return DateTime.MinValue;
			int current = streamItemBarIndex(stream);
			if (current < 1 || chartBar > current) return DateTime.MinValue;
			int ago = current - chartBar;
			if (ago > ExecInfo.MaxBarsBack) return DateTime.MinValue;
			return bars.Time[ago];
		}

		/// <summary>
		/// Minimum price move of Data1 (VPB-20). Resolved once per StartCalc (lazily, so it is valid even if something
		/// in StartCalc asks first) instead of a try/catch on every call inside per-extra loops. Derived classes use it.
		/// </summary>
		protected double tickSize()
		{
			if (m_tick <= 0) m_tick = resolveTick();
			return m_tick;
		}

		private double resolveTick()
		{
			try
			{
				double mm = Bars.Info.MinMove;
				double ps = Bars.Info.PriceScale;
				if (ps != 0)
				{
					double t = Math.Abs(mm / ps);
					if (t > 0 && !double.IsInfinity(t)) return t;
				}
				if (mm > 0) return mm;
			}
			catch { }
			return FALLBACK_TICK;
		}

		private bool nodeWideEnough(double low, double high)
		{
			int ticks = MinNodeTicks;
			double need = ticks * tickSize();
			if (need <= 0) return true;
			return (high - low) + PRICE_EPSILON >= need;
		}

		private void hookStreams()
		{
			m_streams.Clear();
			for (int s = 1; s <= MaxDataStream; ++s)
			{
				IProfilesCollection pack = getPack(s);
				if (pack == null || !pack.Visible) continue; // VP not enabled on this stream
				StreamPack sp = new StreamPack(s);
				sp.pack = pack;
				sp.handler = delegate (bool full) { onVpChanged(sp, full); };
				pack.EChanged += sp.handler;
				m_streams.Add(s, sp);
			}
		}

		private void unhookStreams()
		{
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
					if (windowOpenPastCalcBar && !AllowLookAheadHistogram)
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

		private static void clearHistogram(VpExtra extra)
		{
			extra.hvns.Clear();
			extra.lvns.Clear();
			extra.levels.Clear();
			extra.maxTotalPercentile = 0;
			extra.maxAskPercentile = 0;
			extra.maxBidPercentile = 0;
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

			bool[] traded;
			List<Row> rows = denseRowsFromLevels(extra.levels, tickSize(), out traded);
			if (rows.Count == 0) { rows = rowsFromLevels(extra.levels); traded = null; }   // fallback: old sparse behaviour
			if (rows.Count < 3) return;

			double[] vols = new double[rows.Count];
			for (int j = 0; j < rows.Count; j++)
				vols[j] = rows[j].volume;
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
			if (extra.poc <= 0) extra.poc = rows[pocIdx].price;
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
				if (width > range * DEFAULT_MAX_LVN_PCT_OF_RANGE) continue;
				extra.lvns.Add(makeNode(rows, vols, a, b, t, pocVol, false));
				t = b;   // skip the rest of this valley
			}
		}

		private static void readLevels(IProfile vp, List<LevelVol> dest)
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

		private static List<Row> rowsFromLevels(List<LevelVol> levels)
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
		private static List<Row> denseRowsFromLevels(List<LevelVol> levels, double tick, out bool[] traded)
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

		// Merge the existing sorted prefix with the extra's values (kind 0 total, 1 ask, 2 bid).
		private static double[] mergeSorted(double[] a, int count, List<LevelVol> levels, int kind, int n)
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

		private static void stampLevelPercentiles(VpExtra extra, double[] streamTotal, double[] streamAsk, double[] streamBid, int count)
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
		private static double rankPercentile(double[] sortedAscending, int count, double x)
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

		private static Node makeNode(List<Row> rows, double[] vols, int a, int b, int peak, double pocVol, bool isPoc)
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

		private static bool isLocalMin(double[] v, int i)
		{
			bool leLeft = i == 0 || v[i] <= v[i - 1];
			bool leRight = i == v.Length - 1 || v[i] <= v[i + 1];
			return leLeft && leRight;
		}

		private static bool hasHvnAdjacent(List<Node> hvns, double edge, bool lookingDown, double maxGap)
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

		private static double[] smooth(double[] src, int window)
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
		private static double percentile(double[] src, double pct)
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
			bool whole = AllowLookAheadHistogram || asOf < 1 || endChart <= asOf;

			double poc = firstPositive(
				priceForBar(vp, bar, 0),
				priceForBar(vp, bar - 1, 0),
				whole ? priceForBar(vp, endChart, 0) : 0,
				priceForBar(vp, startChart, 0),
				whole ? levelPrice(vp.POC) : 0);
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

		/// <summary>
		/// kind: 0 POC, 1 VAH, 2 VAL. chartBar is 1-based; 
		/// POCForBar/HighVAForBar/LowVAForBar take the absolute 0-based bar (same as ItemForBar).
		/// Never probe chartBar itself: that is the next bar.
		/// </summary>
		private static double priceForBar(IProfile vp, int chartBar, int kind)
		{
			return priceForBarNative(vp, toNativeItemBar(chartBar), kind);
		}

		private static double priceForBarNative(IProfile vp, int bar, int kind)
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

		private static double firstPositive(params double[] xs)
		{
			if (xs == null) return 0;
			for (int i = 0; i < xs.Length; i++)
				if (xs[i] > 0) return xs[i];
			return 0;
		}

		private static double volumeAtPrice(VpExtra extra, double price)
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

		private static double levelPrice(ILevel lvl)
		{
			try
			{
				if (lvl != null && lvl.Price != null) return lvl.Price.Dbl;
			}
			catch { }
			return 0;
		}

		private static double vaExtreme(IProfile vp, bool high)
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
