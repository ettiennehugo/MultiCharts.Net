using ATCenterProxy.interop;
using PowerLanguage.Function;
using PowerLanguage.VolumeProfile;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
	public class _VolumeProfileBase : _CustomSignal
	{
		//constants
		public const double DEFAULT_HVN_PERCENTILE = 0.8;  // Default percentile of prices relative to POC considered high volume node prices.
		public const double DEFAULT_LVN_PERCENTILE = 0.2;  // Default percentile of prices relative to POC considered low volume node prices.
		public const int DEFAULT_SMOOTH_TICKS = 3;  // Default smoothing length used for ticks.
		public const int DEFAULT_MIN_NODE_TICKS = 3;  // min HVN/LVN high-low span in ticks
		public const double PRICE_EPSILON = 1e-12;  // tolerance for price-span compares; about 1 ULP near 7800, far below a tick
		public const double DEFAULT_MAX_LVN_PCT_OF_RANGE = 0.12; // TBD - Do we need this?? Maximum percentage size of the volume profile a node can be - prevents one LVN from swallowing too much of profile.
		public const int DEFAULT_VWAP_LENGTH = 20;
		public const int VWAP_SLOT_COUNT = 3;
		public const int CONFLUENCE_STREAM_COUNT = 3;
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
		public const double DEFAULT_CONFLUENCE_WEIGHT_POC = 1.00;   // default confluence weight for POC alignment
		public const double DEFAULT_CONFLUENCE_WEIGHT_NAKED_POC = 1.25; // naked (unmitigated) POC outweighs a mitigated POC
		public const double DEFAULT_CONFLUENCE_WEIGHT_VA = 0.90;    // default confluence weight for value area alignment
		public const double DEFAULT_CONFLUENCE_WEIGHT_HVN = 0.85;   // default confluence weight for high value area node alignment
		public const double DEFAULT_CONFLUENCE_WEIGHT_VWAP_SLOT = 0.85;   // default confluence weight for POC alignment
		public const double DEFAULT_CONFLUENCE_HVN_STRENGTH_FLOOR = 0.35; // default minimum HVN strength
		public const double DEFAULT_CONFLUENCE_NAKED_MULTIPLIER = 1.15; // naked modifies the POC credit; it is not a second source
		public const double DEFAULT_CONFLUENCE_REFINEMENT = 0.25;       // one nested child per parent already in the zone
		public const double DEFAULT_CONFLUENCE_OVERLAY_FACTOR = 0.50;   // VWAP context only; never acceptance or rejection
		public const double DEFAULT_CONFLUENCE_SHARE_PIVOT = 0.15;      // volume-share factor pivot
		public const double DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE = 0.95; // percentile used to determine whether volume on a node it exceptional

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
			public string suggestedUse;    // target | entry | conflict
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
		/// walk Items on every bar. cachedBarA/B remember the last two chartBar resolutions
		/// (CalcBar always asks for current and current-1). A generation bump drops both caches.
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
			public int resolveGeneration = -1;
			public int cachedBarA = -1;
			public IProfile cachedProfileA;
			public int cachedBarB = -1;
			public IProfile cachedProfileB;
			public bool nakedSuppressed;
			public int levelsGeneration;
			public int percentilesStampedGeneration = -1;
			public StreamPack(int datastream) { this.datastream = datastream; }
			public void bumpGeneration()
			{
				generation++;
				if (generation == int.MaxValue) generation = 1;
				listedGeneration = -1;
				resolveGeneration = -1;
				cachedBarA = -1;
				cachedBarB = -1;
				cachedProfileA = null;
				cachedProfileB = null;
			}
		};

		/// <summary>
		/// Self-contained VWAP accumulator. Stepped from CalcBar against a data stream's Bars.
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
			public VariableSeries<bool> lastInSession; // owned by the study, slot-specific

			private double m_sumPV;
			private double m_sumP2V;
			private double m_sumV;
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
				lastInSession.Value = bars.LastBarInSession;
				bool first = bars.CurrentBar <= 1;
				if (!first)
				{
					try { first = lastInSession[1]; }
					catch { first = false; }
				}

				if (first)
				{
					parent.trace(ETraceLevel.Debug, $"Resetting session VWAP {bars.Time[0]:yyyy-MM-dd HH:mm:ss}");
					clearSums();
				}

				add(priceAt(bars, 0), volume(bars, 0));
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
					subtractLengthSlot(slot);
					writeLengthSlot(slot, bars, 0);
				}
				else if (contiguous)
				{
					// m_lenNext is the oldest sample. Drop it, write the new bar there, advance.
					subtractLengthSlot(m_lenNext);
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
				// Newest-first resync matches clear-and-resum addition order, including vol/price skips.
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

			// Mirror add(): a skipped bar (vol <= 0 or price <= 0) contributed nothing, so it subtracts nothing.
			private void subtractLengthSlot(int slot)
			{
				double price = m_lenPrice[slot];
				double vol = m_lenVol[slot];
				if (vol <= 0 || price <= 0) return;
				m_sumPV -= price * vol;
				m_sumP2V -= price * price * vol;
				m_sumV -= vol;
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

				if (!m_seenAnchorBar)
				{
					parent.trace(ETraceLevel.Debug, $"Resetting anchored VWAP {bars.Time[0]:yyyy-MM-dd HH:mm:ss}");
					clearSums();
					m_seenAnchorBar = true;
				}

				add(priceAt(bars, 0), volume(bars, 0));
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

			private void finish()
			{
				if (m_sumV <= 0) return;
				value = m_sumPV / m_sumV;
				double var = (m_sumP2V / m_sumV) - (value * value);
				sigma = var > 0 ? Math.Sqrt(var) : 0;
				valid = value > 0;
			}

			private static double priceAt(IInstrument bars, int barsAgo)
			{
				try
				{
					double h = barsAgo == 0 ? bars.HighValue : bars.High[barsAgo];
					double l = barsAgo == 0 ? bars.LowValue : bars.Low[barsAgo];
					double c = barsAgo == 0 ? bars.CloseValue : bars.Close[barsAgo];
					if (h > 0 && l > 0) return (h + l + c) / 3.0;
					return c;
				}
				catch
				{
					try { return barsAgo == 0 ? bars.CloseValue : bars.Close[barsAgo]; }
					catch { return 0; }
				}
			}

			private static double volume(IInstrument bars, int barsAgo)
			{
				double v = 0;
				try { v = barsAgo == 0 ? bars.VolumeValue : bars.Volume[barsAgo]; }
				catch { }
				if (v > 0) return v;
				try { v = barsAgo == 0 ? bars.TicksValue : bars.Ticks[barsAgo]; }
				catch { }
				return v;
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

			private static double clusterHigh(List<Anchor> cluster)
			{
				double hi = cluster[0].price;
				for (int i = 1; i < cluster.Count; i++)
					if (cluster[i].price > hi) hi = cluster[i].price;
				return hi;
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
				if (priceHi - priceLo <= 4.0 * tol + PRICE_EPSILON || count < 3)
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
				int cap = order.Count < 3 ? order.Count : 3;
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

			private Anchor smallestContainer(List<Anchor> anchors, Anchor child, bool completedOnly)
			{
				Anchor best = null;
				double bestSpan = double.MaxValue;
				var seen = new HashSet<string>();
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
				zone.suggestedUse = zone.role == EConfluenceRole.Mixed ? "conflict" : (zone.role == EConfluenceRole.Rejection ? "entry" : "target");
				zone.exitPrice = zone.suggestedUse == "target" ? anchor : 0;
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
				if (weight <= 0) weight = 1.0 + 0.25 * a.timeframeRank;
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
				string text = "";
				for (int i = 0; i < list.Count; i++)
				{
					Anchor a = list[i];
					if (object.ReferenceEquals(a, kept)) continue;
					if (isOverlay(a.kind) || isRejection(a.kind) != isRejection(kept.kind)) continue;
					if (text.Length > 0) text += ",";
					text += a.kind.ToString();
				}
				return text;
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
					double room = (1.25 * parent.credit) - parent.credit;
					if (room < 0) room = 0;
					if (bonus > room) bonus = room;
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

			private static bool isAcceptance(EConfluenceKind k)
			{
				return k == EConfluenceKind.Poc || k == EConfluenceKind.NakedPoc
					|| k == EConfluenceKind.Vah || k == EConfluenceKind.Val
					|| k == EConfluenceKind.Hvn;
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
				if (f < 0.5) return 0.5;
				if (f > 1.5) return 1.5;
				return f;
			}

			private ShareSnap snapFor(VpExtra extra, double tick)
			{
				ShareSnap snap;
				if (extra != null && m_shares.TryGetValue(extra, out snap)) return snap;
				snap = new ShareSnap();
				snap.tol = tick > 0 ? tick * 0.51 : 1e-6;
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
				if (tick <= 0) tick = 0.01;
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
		// Grow-only gather buffers. Shrinking every bar was pure allocation. rankPercentile
		// takes the filled count so the spare tail is not part of the CDF.
		private double[] m_pctTotal = new double[0];
		private double[] m_pctAsk = new double[0];
		private double[] m_pctBid = new double[0];
		private readonly List<VpExtra> m_pctExtras = new List<VpExtra>();

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
			m_vwap1 = new VwapEngine { label = "VWAP1", lastInSession = new VariableSeries<bool>(this) };
			m_vwap2 = new VwapEngine { label = "VWAP2", lastInSession = new VariableSeries<bool>(this) };
			m_vwap3 = new VwapEngine { label = "VWAP3", lastInSession = new VariableSeries<bool>(this) };
			m_confluenceEngine = new ConfluenceEngine(this);
		}

		protected override void Destroy()
		{
			unhookStreams();
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			validateInputs();
			unhookStreams();
			m_streams.Clear();
			hookStreams();
			configureVwap(m_vwap1, Vwap1Type, Vwap1Length, Vwap1FromDate, Vwap1DataStream, "VwapFromDate", "VwapDataStream");
			configureVwap(m_vwap2, Vwap2Type, Vwap2Length, Vwap2FromDate, Vwap2DataStream, "Vwap2FromDate", "Vwap2DataStream");
			configureVwap(m_vwap3, Vwap3Type, Vwap3Length, Vwap3FromDate, Vwap3DataStream, "Vwap3FromDate", "Vwap3DataStream");
			foreach (var kv in m_streams)
				if (kv.Value != null) kv.Value.bumpGeneration();
			m_confluenceGeneration++;
			m_confluenceDirty = true;
			m_confluenceBuiltGeneration = -1;
			m_confluenceZones.Clear();
			m_volumePctDirty = true;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
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

			IProfile vp = itemForBar(streamPack, datastream, barIndex);
			if (vp != null)
				ensureExtra(streamPack, vp, barIndex);

			VpExtra extra = extraFor(streamPack, datastream, vp, barIndex);
			if (extra == null)
				extra = extraForStart(streamPack, datastream, barIndex);
			if (extra == null)
			{
				trace(ETraceLevel.Verbose, $"Failed to find volume profile for data stream {datastream} for bar {barIndex}.");
				return null;
			}
			if (vp != null)
				applyProfileGeometry(vp, extra, barIndex);
			stampExtra(extra);
			stampNakedPocs(streamPack);
			refreshVolumePercentiles();
			return extra;
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
				trace(ETraceLevel.Verbose, $"IsNewVolumeProfile: no volume profile collection on data stream {datastream}.");
				return false;
			}

			IProfile vp = itemForBar(streamPack, datastream, barIndex);
			if (vp == null)
			{
				trace(ETraceLevel.Verbose, $"IsNewVolumeProfile: no profile on data stream {datastream} for bar {barIndex}.");
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
		public double CurrentVwap { get { return m_vwap1 != null && m_vwap1.valid ? m_vwap1.value : double.NaN; } }
		/// <summary>Slot 2 current VWAP, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwap2 { get { return m_vwap2 != null && m_vwap2.valid ? m_vwap2.value : double.NaN; } }
		/// <summary>Slot 3 current VWAP, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwap3 { get { return m_vwap3 != null && m_vwap3.valid ? m_vwap3.value : double.NaN; } }
		/// <summary>Slot 1 running standard deviation, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwapSigma { get { return m_vwap1 != null && m_vwap1.valid ? m_vwap1.sigma : double.NaN; } }
		/// <summary>Slot 2 running standard deviation, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwap2Sigma { get { return m_vwap2 != null && m_vwap2.valid ? m_vwap2.sigma : double.NaN; } }
		/// <summary>Slot 3 running standard deviation, or NaN when Disabled / not valid yet. Read-only.</summary>
		public double CurrentVwap3Sigma { get { return m_vwap3 != null && m_vwap3.valid ? m_vwap3.sigma : double.NaN; } }
		/// <summary>True when slot 1 is not Disabled and has a valid value. Read-only.</summary>
		public bool VwapActive { get { return m_vwap1 != null && m_vwap1.type != EVwapType.Disabled && m_vwap1.valid; } }
		/// <summary>True when slot 2 is not Disabled and has a valid value. Read-only.</summary>
		public bool Vwap2Active { get { return m_vwap2 != null && m_vwap2.type != EVwapType.Disabled && m_vwap2.valid; } }
		/// <summary>True when slot 3 is not Disabled and has a valid value. Read-only.</summary>
		public bool Vwap3Active { get { return m_vwap3 != null && m_vwap3.type != EVwapType.Disabled && m_vwap3.valid; } }
		/// <summary>Chart stream slot 1 was built from (1 = Data1). 0 if unused. Read-only.</summary>
		public int CurrentVwapStream { get { return slotStream(m_vwap1); } }
		/// <summary>Chart stream slot 2 was built from (1 = Data1). 0 if unused. Read-only.</summary>
		public int CurrentVwap2Stream { get { return slotStream(m_vwap2); } }
		/// <summary>Chart stream slot 3 was built from (1 = Data1). 0 if unused. Read-only.</summary>
		public int CurrentVwap3Stream { get { return slotStream(m_vwap3); } }

		/// <summary>
		/// Re-anchor slot 1 from a swing / news / session-open time. No-op if the slot is Disabled.
		/// </summary>
		public void AnchorVwap(DateTime from)
		{
			anchorSlot1(from);
		}

		/// <summary>
		/// Re-anchor slot 2 from a swing / news / session-open time. No-op if the slot is Disabled.
		/// </summary>
		public void AnchorVwap2(DateTime from)
		{
			anchorSlot2(from);
		}

		/// <summary>
		/// Re-anchor slot 3 from a swing / news / session-open time. No-op if the slot is Disabled.
		/// </summary>
		public void AnchorVwap3(DateTime from)
		{
			anchorSlot3(from);
		}

		//methods
		private static int slotStream(VwapEngine eng)
		{
			if (eng == null || eng.type == EVwapType.Disabled) return 0;
			return eng.datastream;
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
			eng.Step(bars);
		}

		private IInstrument barsOf(int stream)
		{
			try
			{
				if (stream <= 1) return Bars;
				if (stream < 1 || stream > MaxDataStream) return null;
				return BarsOfData(stream);
			}
			catch
			{
				return null;
			}
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
				ensureExtraAt(sp, chartBar);
				if (chartBar > 1)
					ensureExtraAt(sp, chartBar - 1);
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
			if (!TrackNakedPoc)
			{
				// Clear once. New extras default to not-naked, and nothing else sets the flags
				// while this returns early. Turning the input back on clears the flag below.
				if (sp.nakedSuppressed) return;
				foreach (VpExtra extra in sp.extra.Values)
					clearNakedPoc(extra);
				sp.nakedSuppressed = true;
				return;
			}
			sp.nakedSuppressed = false;

			int current = streamItemBarIndex(sp.datastream);
			if (current < 1) return;

			double tick = tickSize();
			double tol = tick > 0 ? NakedPocTickTolerance * tick : 0;
			IInstrument bars = barsOf(sp.datastream);

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
					extra.lastNakedScanBar = current;
				}
				extra.pocTouchedBar = touched;
				extra.pocNaked = touched == 0;
			}
		}

		private static void clearNakedPoc(VpExtra extra)
		{
			if (extra == null) return;
			extra.pocComplete = false;
			extra.pocNaked = false;
			extra.pocTouchedBar = 0;
			extra.lastNakedScanBar = 0;
		}

		private void ensureExtraAt(StreamPack sp, int barIndex)
		{
			if (sp == null || barIndex < 1) return;
			IProfile vp = itemForBar(sp, sp.datastream, barIndex);
			if (vp == null) return;
			ensureExtra(sp, vp, barIndex);
		}

		private VpExtra extraFor(StreamPack sp, int datastream, IProfile vp, int chartBar)
		{
			if (sp == null || vp == null) return null;
			VpKey key = new VpKey(datastream, vp);
			VpExtra extra;
			if (!sp.extra.TryGetValue(key, out extra) && key.startBar >= 1)
				sp.extra.TryGetValue(new VpKey(datastream, key.startBar), out extra);
			if (extra != null)
				applyProfileGeometry(vp, extra, chartBar);
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
			if (extra == null) return null;
			IProfile vp = itemForBar(sp, datastream, startBar);
			if (vp != null)
				applyProfileGeometry(vp, extra, startBar);
			return extra;
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

		private void anchorSlot1(DateTime from)
		{
			if (m_vwap1 == null || m_vwap1.type == EVwapType.Disabled) return;
			Vwap1Type = EVwapType.AdaptiveFromDate;
			Vwap1FromDate = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			m_vwap1.type = EVwapType.AdaptiveFromDate;
			m_vwap1.fromDate = from;
			m_vwap1.Reset();
		}

		private void anchorSlot2(DateTime from)
		{
			if (m_vwap2 == null || m_vwap2.type == EVwapType.Disabled) return;
			Vwap2Type = EVwapType.AdaptiveFromDate;
			Vwap2FromDate = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			m_vwap2.type = EVwapType.AdaptiveFromDate;
			m_vwap2.fromDate = from;
			m_vwap2.Reset();
		}

		private void anchorSlot3(DateTime from)
		{
			if (m_vwap3 == null || m_vwap3.type == EVwapType.Disabled) return;
			Vwap3Type = EVwapType.AdaptiveFromDate;
			Vwap3FromDate = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			m_vwap3.type = EVwapType.AdaptiveFromDate;
			m_vwap3.fromDate = from;
			m_vwap3.Reset();
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
		/// The two-slot cache is only a generation-scoped memo of that resolution.
		/// CalcBar and IsNewVolumeProfile ask for the current bar and the bar before it.
		/// A generation bump (profile collection changed) drops the memo. The bar+1 probe
		/// is still part of the cold resolution; do not remove it.
		/// </summary>
		/// <param name="barIndex">1-based chart bar (Status Bar # / FullSymbolData.Current).</param>
		private IProfile itemForBar(StreamPack streamPack, int datastream, int barIndex)
		{
			if (streamPack == null || streamPack.pack == null || barIndex < 1) return null;
			if (streamPack.resolveGeneration == streamPack.generation)
			{
				if (barIndex == streamPack.cachedBarA) return streamPack.cachedProfileA;
				if (barIndex == streamPack.cachedBarB) return streamPack.cachedProfileB;
			}
			else
			{
				streamPack.resolveGeneration = streamPack.generation;
				streamPack.cachedBarA = -1;
				streamPack.cachedBarB = -1;
				streamPack.cachedProfileA = null;
				streamPack.cachedProfileB = null;
			}

			IProfile resolved = resolveItemForBar(streamPack, datastream, barIndex);
			streamPack.cachedBarB = streamPack.cachedBarA;
			streamPack.cachedProfileB = streamPack.cachedProfileA;
			streamPack.cachedBarA = barIndex;
			streamPack.cachedProfileA = resolved;
			return resolved;
		}

		private IProfile resolveItemForBar(StreamPack streamPack, int datastream, int barIndex)
		{
			IProfile best = null;
			int bestDist = int.MaxValue;
			considerProfile(rawItemForBar(streamPack, datastream, barIndex), barIndex, ref best, ref bestDist);
			if (barIndex > 1)
				considerProfile(rawItemForBar(streamPack, datastream, barIndex - 1), barIndex, ref best, ref bestDist);
			considerProfile(rawItemForBar(streamPack, datastream, barIndex + 1), barIndex, ref best, ref bestDist);

			List<IProfile> listed = listedProfiles(streamPack);
			for (int i = 0; i < listed.Count; i++)
				considerProfile(listed[i], barIndex, ref best, ref bestDist);

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
		/// </summary>
		/// <param name="barIndex">1-based chart bar. Converted to native 0-based for pack.ItemForBar.</param>
		private IProfile rawItemForBar(StreamPack streamPack, int datastream, int barIndex)
		{
			if (streamPack == null || streamPack.pack == null || barIndex < 1) return null;
			int native = toNativeItemBar(barIndex);
			IProfile vp = rawNativeItemForBar(streamPack, datastream, native);
			if (vp != null) return vp;
			// Some builds accept the 1-based Status index directly.
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
				trace(ETraceLevel.Verbose, $"Exception trying to find volume profile on data stream {datastream} for bar {nativeBar} - {e.Message}");
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
			double clear = clearTicks * tickSize();
			double minZ = ConfluenceTargetMinZScore;

			for (int i = 0; i < m_confluenceZones.Count; i++)
			{
				ConfluenceZone zone = m_confluenceZones[i];
				if (zone == null || zone.center <= 0) continue;
				if (zone.role == EConfluenceRole.Rejection || zone.suggestedUse == "entry" || zone.suggestedUse == "conflict") continue;
				if (zone.zScore < minZ) continue;
				if (!zoneClearsPrice(zone, price, dir, clear)) continue;

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

		private static bool zoneClearsPrice(ConfluenceZone zone, double price, int dir, double clear)
		{
			if (zone == null) return false;
			if (dir > 0)
				return zone.low >= price + clear;
			return zone.high <= price - clear;
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
					sp.listedGeneration = sp.generation;
					return list;
				}
			}
			catch
			{
				// Items can be non-null but already disposed / unusable.
				sp.listedCache = list;
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
				sp.listedGeneration = sp.generation;
				return list;
			}
			list.Sort((a, b) => profileStartBar(a).CompareTo(profileStartBar(b)));
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
			try { return bars.Time[current - chartBar]; }
			catch { return DateTime.MinValue; }
		}

		private double tickSize()
		{
			try
			{
				double mm = Bars.Info.MinMove;
				double ps = Bars.Info.PriceScale;
				if (ps != 0) return Math.Abs(mm / ps);
				if (mm > 0) return mm;
			}
			catch { }
			return 0.01;
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

		// Bump before ensureExtra so a closed histogram rebuilt for the old generation
		// is not treated as frozen against the new profile objects.
		private void onVpChanged(StreamPack sp, bool full)
		{
			if (sp.pack == null) return;
			sp.bumpGeneration();
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
				int current = streamItemBarIndex(sp.datastream);
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
					fillNodes(vp, extra, chartBar);
					extra.nodesBuilt = extra.levels.Count > 0 && extra.poc > 0;
					extra.nodesBuiltForStart = extra.startBar;
					extra.nodesBuiltGeneration = sp.generation;
					sp.levelsGeneration++;
					m_volumePctDirty = true;
				}
			}
			stampExtra(extra);
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
			List<Row> rows = rowsFromLevels(extra.levels);
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
			double hvnFloor = percentile(vols, HvnPercentile);
			double lvnCeil = percentile(vols, LvnPercentile);
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
				bool hvnBelow = hasHvnTouching(extra.hvns, rows[a].price, true);
				bool hvnAbove = hasHvnTouching(extra.hvns, rows[b].price, false);
				if (!hvnBelow && !hvnAbove) continue;
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
				// No extra on this stream gained, lost, or resized levels since the last stamp.
				if (sp.percentilesStampedGeneration == sp.levelsGeneration) continue;
				m_pctExtras.Clear();
				foreach (VpExtra extra in sp.extra.Values)
				{
					if (extra == null || extra.levels.Count == 0) continue;
					m_pctExtras.Add(extra);
				}
				if (m_pctExtras.Count == 0)
				{
					sp.percentilesStampedGeneration = sp.levelsGeneration;
					continue;
				}
				int n = 0;
				for (int i = 0; i < m_pctExtras.Count; i++)
					n += m_pctExtras[i].levels.Count;
				if (m_pctTotal.Length < n) m_pctTotal = new double[n];
				if (m_pctAsk.Length < n) m_pctAsk = new double[n];
				if (m_pctBid.Length < n) m_pctBid = new double[n];
				int w = 0;
				for (int i = 0; i < m_pctExtras.Count; i++)
				{
					VpExtra extra = m_pctExtras[i];
					for (int k = 0; k < extra.levels.Count; k++)
					{
						m_pctTotal[w] = extra.levels[k].total;
						m_pctAsk[w] = extra.levels[k].ask;
						m_pctBid[w] = extra.levels[k].bid;
						w++;
					}
				}
				Array.Sort(m_pctTotal, 0, n);
				Array.Sort(m_pctAsk, 0, n);
				Array.Sort(m_pctBid, 0, n);
				for (int e = 0; e < m_pctExtras.Count; e++)
					stampLevelPercentiles(m_pctExtras[e], m_pctTotal, m_pctAsk, m_pctBid, n);
				sp.percentilesStampedGeneration = sp.levelsGeneration;
			}
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

		private static bool hasHvnTouching(List<Node> hvns, double price, bool lookingDown)
		{
			for (int i = 0; i < hvns.Count; i++)
			{
				if (lookingDown && hvns[i].high <= price) return true;
				if (!lookingDown && hvns[i].low >= price) return true;
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

			double poc = firstPositive(
				priceForBar(vp, bar, 0),
				priceForBar(vp, bar - 1, 0),
				priceForBar(vp, endChart, 0),
				priceForBar(vp, startChart, 0),
				levelPrice(vp.POC));
			double vah = firstPositive(
				priceForBar(vp, bar, 1),
				priceForBar(vp, bar - 1, 1),
				priceForBar(vp, endChart, 1),
				vaExtreme(vp, true));
			double val = firstPositive(
				priceForBar(vp, bar, 2),
				priceForBar(vp, bar - 1, 2),
				priceForBar(vp, endChart, 2),
				vaExtreme(vp, false));

			if (poc > 0) extra.poc = poc;
			if (vah > 0) extra.vah = vah;
			if (val > 0) extra.val = val;
			applyProfileOhlc(extra);

			double pocVol = 0;
			try
			{
				if (vp.POC != null) pocVol = (double)vp.POC.TotalValue;
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
		private static double barPrice(IInstrument bars, int current, int chartBar, int kind)
		{
			if (bars == null || chartBar < 1 || current < chartBar) return 0;
			int ago = current - chartBar;
			try
			{
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
			catch
			{
				return 0;
			}
		}

		/// <summary>kind: 0 POC, 1 VAH, 2 VAL. Tries 1-based Status bar then native 0-based ItemForBar index.</summary>
		private static double priceForBar(IProfile vp, int chartBar, int kind)
		{
			if (vp == null || chartBar < 1) return 0;
			double px = priceForBarNative(vp, chartBar, kind);
			if (px > 0) return px;
			return priceForBarNative(vp, chartBar - 1, kind);
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
