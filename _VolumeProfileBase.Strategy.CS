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
	///
	/// TODO:
	///   - volume normalization
	///   - HVN, LVN detection - DONE NEED TO TEST
	///     - Add directional checks (bullish/bearish) that would return the list of HVN's, VAL, VAH, POC that would be considered high probability zones - DONE TO TEST
	///   - VWAP integration - DONE (internal engines + profile-histogram VWAP)
	///   - confluence zones - DONE NEED TO TEST
	///     - TODO there might still be DOUBLE COUNTING happening on the generated code for the confluence zones - you need to make sure you count unique factors in the z-scoring - DONE NEED TO TEST
	///   - directionality of confluence zones - TODO
	///   - ranking of best zones (add parameter to keep only a certain number of zones, e.g. 5) - DONE NEED TO TEST
	///   - can you introduce a mechanism for if-then logic for the different zones (accept vs reject, per setup). - this should rather go to a signal
	/// </summary>
	public class _VolumeProfileBase : _CustomSignal
	{
		//constants
		public const double DEFAULT_HVN_PERCENTILE = 0.8;  // Default percentile of prices relative to POC considered high volume node prices.
		public const double DEFAULT_LVN_PERCENTILE = 0.2;  // Default percentile of prices relative to POC considered low volume node prices.
		public const int DEFAULT_SMOOTH_TICKS = 3;  // Default smoothing length used for ticks.
		public const int DEFAULT_MIN_NODE_TICKS = 3;  // min HVN/LVN high-low span in ticks
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
		public const double DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE = 0.95;

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
			public double poc;    // Point of control for the volume profile.
			public double pocVol; // Volume at the point of control.
			public double vah;  // Value area high.
			public double val;  // Value area low.
			public double profileVwap;  // Σ(price * levelVolume) / Σ(levelVolume) from the same rows used for HVN/LVN.
			public double vwap;         // slot 1 snapshot (0 when Disabled / not yet valid)
			public double vwap2;        // slot 2 snapshot
			public double vwap3;        // slot 3 snapshot
			public double vwapSigma;    // slot 1 σ from the running window (0 when unused)
			public double vwap2Sigma;
			public double vwap3Sigma;
			public int vwapStream;      // chart data stream slot 1 was built from (0 = unused)
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
			ProfileVwap = 4,
			Vwap = 5,
			Vwap2 = 6,
			Vwap3 = 7
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
			public double weight;          // after stream weight, kind weight, and age decay
		}

		/// <summary>
		/// Cluster of aligned VAL / VAH / HVN / POC / VWAP levels.
		/// Z-score is computed across the current bar's zone raw scores
		/// (stream weights already applied).
		/// </summary>
		public class ConfluenceZone
		{
			public double low;
			public double high;
			public double center;          // weight-weighted mean of hit prices
			public double rawScore;
			public double zScore;
			public int hitCount;
			public int streamCount;
			public int profileCount;
			public readonly List<ConfluenceHit> hits = new List<ConfluenceHit>();

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
		/// in a given direction. price is the magnet (zone center); touch is the first
		/// edge price would meet (zone.low on a long, zone.high on a short).
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
		/// Working row used only while building confluence zones.
		/// One row per contributing level (POC / VAH / VAL / HVN / profile VWAP / live VWAP)
		/// after stream weight and same-stream age decay have been applied.
		/// Sorted by price and clustered within ConfluenceAlignTicks; each finished
		/// cluster becomes a public ConfluenceZone with ConfluenceHit copies of these rows.
		/// Not persisted and not part of the subclass API.
		/// </summary>
		private struct ConfluenceAnchor
		{
			public int dataStream;
			public int profileStartBar;
			public int profileAge;
			public EConfluenceKind kind;
			public double price;
			public double weight;
		}

		/// <summary>
		/// Internal storage class to associate a specific data stream with it's profiles collection and
		/// extra data stores for it and the update handler delegate.
		/// </summary>
		private class StreamPack
		{
			public readonly int datastream;
			public IProfilesCollection pack;
			public readonly Dictionary<VpKey, VpExtra> extra = new Dictionary<VpKey, VpExtra>();
			public TProfilesChanged handler;   // must keep this instance to unsubscribe
			public StreamPack(int datastream) { this.datastream = datastream; }
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

			public void Reset()
			{
				m_sumPV = 0;
				m_sumP2V = 0;
				m_sumV = 0;
				m_seenAnchorBar = false;
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

			private void stepLength(IInstrument bars)
			{
				int n = length < 1 ? DEFAULT_VWAP_LENGTH : length;
				if (bars.CurrentBar < n) return;

				clearSums();
				for (int i = 0; i < n; i++)
					add(priceAt(bars, i), volume(bars, i));
				finish();
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

		//properties
		[Input] public double HvnPercentile { get; set; }  // Percentile of POC that would be considered high volume nodes.
		[Input] public double LvnPercentile { get; set; }  // Percentile of POC that would be considered low volume nodes.
		[Input] public int MinNodeTicks { get; set; }      // Drop HVN/LVN whose high-low span is under this many ticks.

		[Input] public EVwapType VwapType { get; set; }     // Slot 1. Disabled = unused.
		[Input] public int VwapLength { get; set; }         // Used when VwapType = Length.
		[Input] public string VwapFromDate { get; set; }    // Used when VwapType = AdaptiveFromDate.
		[Input] public int VwapDataStream { get; set; }     // Chart data stream slot 1 reads. 1 = Data1, 2 = Data2, …

		[Input] public EVwapType Vwap2Type { get; set; }    // Slot 2. Disabled = unused.
		[Input] public int Vwap2Length { get; set; }        // Used when Vwap2Type = Length.
		[Input] public string Vwap2FromDate { get; set; }   // Used when Vwap2Type = AdaptiveFromDate.
		[Input] public int Vwap2DataStream { get; set; }    // Chart data stream slot 2 reads. 1 = Data1, 2 = Data2, …

		[Input] public EVwapType Vwap3Type { get; set; }    // Slot 3. Disabled = unused.
		[Input] public int Vwap3Length { get; set; }        // Used when Vwap3Type = Length.
		[Input] public string Vwap3FromDate { get; set; }   // Used when Vwap3Type = AdaptiveFromDate.
		[Input] public int Vwap3DataStream { get; set; }    // Chart data stream slot 3 reads. 1 = Data1, 2 = Data2, …

		// Confluence configuration. Not [Input] — subclasses decide what to expose.
		// Stream index 0 (or less) disables that slot. Weight 0 drops the stream from scoring.
		public int ConfluenceDataStream1 { get; set; }
		public int ConfluenceDataStream2 { get; set; }
		public int ConfluenceDataStream3 { get; set; }
		public double ConfluenceWeight1 { get; set; }
		public double ConfluenceWeight2 { get; set; }
		public double ConfluenceWeight3 { get; set; }
		public int ConfluenceLookbackProfiles { get; set; }   // succeeding VPs per stream, live included
		public int ConfluenceAlignTicks { get; set; }        // cluster width in ticks
		public int ConfluenceMinHits { get; set; }           // minimum aligned levels to keep a zone
		public int ConfluenceTopCount { get; set; }          // default N for GetTopConfluenceZones()
		public double ConfluenceSameStreamDecay { get; set; } // age 0 = 1, age 1 = decay, age 2 = decay^2
		public double ConfluenceCrossStreamBonus { get; set; } // extra raw-score multiplier per extra stream
		public bool ConfluenceIncludeHvns { get; set; }
		public bool ConfluenceIncludeVwapSlots { get; set; }
		public bool ConfluenceIncludeProfileVwap { get; set; }
		public double ConfluenceTargetMinZScore { get; set; }  // drop zones below this z-score
		public int ConfluenceTargetClearTicks { get; set; }    // zone must clear the reference price by this many ticks
		public int ConfluenceTargetCount { get; set; }         // default N for GetConfluenceTargets()

		//attribute
		private Dictionary<int, StreamPack> m_streams;  /// Stream packs by data stream index.
		private VwapEngine m_vwap1;
		private VwapEngine m_vwap2;
		private VwapEngine m_vwap3;
		private readonly List<ConfluenceZone> m_confluenceZones = new List<ConfluenceZone>();
		private bool m_confluenceDirty;
		private int m_confluenceBuiltBar;
		private bool m_volumePctDirty;

		//interface methods
		public _VolumeProfileBase(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			HvnPercentile = DEFAULT_HVN_PERCENTILE;
			LvnPercentile = DEFAULT_LVN_PERCENTILE;
			MinNodeTicks = DEFAULT_MIN_NODE_TICKS;

			VwapType = EVwapType.Session;
			VwapLength = DEFAULT_VWAP_LENGTH;
			VwapFromDate = "";
			VwapDataStream = 1;

			Vwap2Type = EVwapType.Disabled;
			Vwap2Length = DEFAULT_VWAP_LENGTH;
			Vwap2FromDate = "";
			Vwap2DataStream = 2;

			Vwap3Type = EVwapType.Disabled;
			Vwap3Length = DEFAULT_VWAP_LENGTH;
			Vwap3FromDate = "";
			Vwap3DataStream = 1;

			ConfluenceDataStream1 = 1;
			ConfluenceDataStream2 = 2;
			ConfluenceDataStream3 = 3;
			ConfluenceWeight1 = 1.00;   // execution TF
			ConfluenceWeight2 = 1.25;   // higher TF carries more by default
			ConfluenceWeight3 = 1.50;
			ConfluenceLookbackProfiles = DEFAULT_CONFLUENCE_LOOKBACK;
			ConfluenceAlignTicks = DEFAULT_CONFLUENCE_ALIGN_TICKS;
			ConfluenceMinHits = DEFAULT_CONFLUENCE_MIN_HITS;
			ConfluenceTopCount = DEFAULT_CONFLUENCE_TOP_COUNT;
			ConfluenceSameStreamDecay = DEFAULT_CONFLUENCE_DECAY;
			ConfluenceCrossStreamBonus = DEFAULT_CONFLUENCE_CROSS_BONUS;
			ConfluenceIncludeHvns = true;
			ConfluenceIncludeVwapSlots = true;
			ConfluenceIncludeProfileVwap = true;
			ConfluenceTargetMinZScore = 0;
			ConfluenceTargetClearTicks = DEFAULT_CONFLUENCE_TARGET_CLEAR_TICKS;
			ConfluenceTargetCount = DEFAULT_CONFLUENCE_TOP_COUNT;
			m_confluenceDirty = true;
			m_confluenceBuiltBar = int.MinValue;
			m_volumePctDirty = true;

			m_streams = new Dictionary<int, StreamPack>();
			m_vwap1 = new VwapEngine { label = "VWAP1", lastInSession = new VariableSeries<bool>(this) };
			m_vwap2 = new VwapEngine { label = "VWAP2", lastInSession = new VariableSeries<bool>(this) };
			m_vwap3 = new VwapEngine { label = "VWAP3", lastInSession = new VariableSeries<bool>(this) };
		}

		protected override void Destroy()
		{
			unhookStreams();
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (ConfluenceMinHits < 1) throw new ArgumentException("ConfluenceMinHits must be 1 or greater.");
			if (ConfluenceCrossStreamBonus < 0.0) throw new ArgumentException("ConfluenceCrossStreamBonus must be 0.0 or greater.");
			if (HvnPercentile < 0 || HvnPercentile > 1.0) throw new ArgumentException("HvnPercentile must be a percentage, 0 to 1.0.");
			if (LvnPercentile < 0 || LvnPercentile > 1.0) throw new ArgumentException("LvnPercentile must be a percentage, 0 to 1.0.");
			if (HvnPercentile < LvnPercentile) throw new ArgumentException("HvnPercentile must be larger than LvnPercentile.");
			if (MinNodeTicks < 1) throw new ArgumentException("MinNodeTicks must be 1 or greater.");
			unhookStreams();
			m_streams.Clear();
			hookStreams();
			configureVwap(m_vwap1, VwapType, VwapLength, VwapFromDate, VwapDataStream, "VwapFromDate", "VwapDataStream");
			configureVwap(m_vwap2, Vwap2Type, Vwap2Length, Vwap2FromDate, Vwap2DataStream, "Vwap2FromDate", "Vwap2DataStream");
			configureVwap(m_vwap3, Vwap3Type, Vwap3Length, Vwap3FromDate, Vwap3DataStream, "Vwap3FromDate", "Vwap3DataStream");
			m_confluenceDirty = true;
			m_confluenceBuiltBar = int.MinValue;
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
				ensureExtra(streamPack, vp);

			VpExtra extra = extraFor(streamPack, datastream, vp);
			if (extra == null)
				extra = extraForStart(streamPack, datastream, barIndex);
			if (extra == null)
			{
				trace(ETraceLevel.Verbose, $"Failed to find volume profile for data stream {datastream} for bar {barIndex}.");
				return null;
			}
			stampExtra(extra);
			refreshVolumePercentiles();
			return extra;
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
		/// Sorted by z-score, then raw score, then hit count.
		/// </summary>
		public List<ConfluenceZone> GetTopConfluenceZones()
		{
			return GetTopConfluenceZones(ConfluenceTopCount);
		}

		/// <summary>
		/// Highest-ranked confluence zones, at most <paramref name="count"/>.
		/// Sorted by z-score, then raw score, then hit count.
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
			refreshVolumePercentiles();
		}

		/// <summary>Slot 1 current VWAP, or NaN when Disabled / not valid yet.</summary>
		public double CurrentVwap { get { return m_vwap1 != null && m_vwap1.valid ? m_vwap1.value : double.NaN; } }
		/// <summary>Slot 2 current VWAP, or NaN when Disabled / not valid yet.</summary>
		public double CurrentVwap2 { get { return m_vwap2 != null && m_vwap2.valid ? m_vwap2.value : double.NaN; } }
		/// <summary>Slot 3 current VWAP, or NaN when Disabled / not valid yet.</summary>
		public double CurrentVwap3 { get { return m_vwap3 != null && m_vwap3.valid ? m_vwap3.value : double.NaN; } }
		public double CurrentVwapSigma { get { return m_vwap1 != null && m_vwap1.valid ? m_vwap1.sigma : double.NaN; } }
		public double CurrentVwap2Sigma { get { return m_vwap2 != null && m_vwap2.valid ? m_vwap2.sigma : double.NaN; } }
		public double CurrentVwap3Sigma { get { return m_vwap3 != null && m_vwap3.valid ? m_vwap3.sigma : double.NaN; } }
		public bool VwapActive { get { return m_vwap1 != null && m_vwap1.type != EVwapType.Disabled && m_vwap1.valid; } }
		public bool Vwap2Active { get { return m_vwap2 != null && m_vwap2.type != EVwapType.Disabled && m_vwap2.valid; } }
		public bool Vwap3Active { get { return m_vwap3 != null && m_vwap3.type != EVwapType.Disabled && m_vwap3.valid; } }
		/// <summary>Resolved chart data stream for slot 1 (1 = Data1). 0 if unused.</summary>
		public int CurrentVwapStream { get { return slotStream(m_vwap1); } }
		public int CurrentVwap2Stream { get { return slotStream(m_vwap2); } }
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
			eng.length = length < 1 ? DEFAULT_VWAP_LENGTH : length;
			eng.datastream = 0;
			eng.fromDate = DateTime.MinValue;
			eng.Reset();

			if (type == EVwapType.Disabled) return;

			if (stream < 1)
				throw new ArgumentException(streamInputName + " must be a chart data stream index >= 1 (1 = Data1, 2 = Data2, …).");
			if (stream > MaxDataStream)
				throw new ArgumentException(streamInputName + " = " + stream + " is not on this chart. Add that data stream or set " + eng.label + " Type to Disabled.");
			eng.datastream = stream;

			if (type == EVwapType.Length && length < 1)
				throw new ArgumentException(eng.label + " length must be >= 1 when Type is Length.");
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
		}

		private void ensureExtraAt(StreamPack sp, int barIndex)
		{
			if (sp == null || barIndex < 1) return;
			IProfile vp = itemForBar(sp, sp.datastream, barIndex);
			if (vp == null) return;
			ensureExtra(sp, vp);
		}

		private static VpExtra extraFor(StreamPack sp, int datastream, IProfile vp)
		{
			if (sp == null || vp == null) return null;
			VpKey key = new VpKey(datastream, vp);
			VpExtra extra;
			if (sp.extra.TryGetValue(key, out extra)) return extra;
			if (key.startBar >= 0 && sp.extra.TryGetValue(new VpKey(datastream, key.startBar), out extra))
				return extra;
			return null;
		}

		private static VpExtra extraForStart(StreamPack sp, int datastream, int startBar)
		{
			if (sp == null) return null;
			VpExtra extra;
			if (startBar >= 0 && sp.extra.TryGetValue(new VpKey(datastream, startBar), out extra))
				return extra;
			if (startBar > 0 && sp.extra.TryGetValue(new VpKey(datastream, startBar - 1), out extra))
				return extra;
			if (startBar >= 0 && sp.extra.TryGetValue(new VpKey(datastream, startBar + 1), out extra))
				return extra;
			return null;
		}

		private void stampExtra(VpExtra extra)
		{
			if (extra == null) return;
			extra.vwap = m_vwap1 != null && m_vwap1.valid ? m_vwap1.value : 0;
			extra.vwap2 = m_vwap2 != null && m_vwap2.valid ? m_vwap2.value : 0;
			extra.vwap3 = m_vwap3 != null && m_vwap3.valid ? m_vwap3.value : 0;
			extra.vwapSigma = m_vwap1 != null && m_vwap1.valid ? m_vwap1.sigma : 0;
			extra.vwap2Sigma = m_vwap2 != null && m_vwap2.valid ? m_vwap2.sigma : 0;
			extra.vwap3Sigma = m_vwap3 != null && m_vwap3.valid ? m_vwap3.sigma : 0;
			extra.vwapStream = slotStream(m_vwap1);
			extra.vwap2Stream = slotStream(m_vwap2);
			extra.vwap3Stream = slotStream(m_vwap3);
		}

		private void anchorSlot1(DateTime from)
		{
			if (m_vwap1 == null || m_vwap1.type == EVwapType.Disabled) return;
			VwapType = EVwapType.AdaptiveFromDate;
			VwapFromDate = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
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

		/// <param name="barIndex">1-based chart bar.</param>
		private IProfile itemForBar(StreamPack streamPack, int datastream, int barIndex)
		{
			if (streamPack == null || streamPack.pack == null || barIndex < 1) return null;

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

		private static bool profileOwnsBar(IProfile vp, int barIndex)
		{
			if (vp == null || barIndex < 1) return false;
			int start = profileStartBar(vp);
			int end = profileEndBar(vp);
			if (start < 1 && end < 1) return false;
			if (start < 1) start = end;
			if (end < 1 || end < start) end = start;

			if (barIndex >= start && barIndex <= end) return true;
			if (barIndex > start && barIndex <= end) return true;
			if (barIndex >= start && barIndex < end) return true;
			if (barIndex == start || barIndex == end) return true;
			return false;
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
			int clearTicks = ConfluenceTargetClearTicks < 0 ? 0 : ConfluenceTargetClearTicks;
			double clear = clearTicks * tickSize();
			if (clear < 0) clear = 0;
			double minZ = ConfluenceTargetMinZScore;

			for (int i = 0; i < m_confluenceZones.Count; i++)
			{
				ConfluenceZone zone = m_confluenceZones[i];
				if (zone == null || zone.center <= 0) continue;
				if (zone.zScore < minZ) continue;
				if (!zoneClearsPrice(zone, price, dir, clear)) continue;

				double touch = dir > 0 ? zone.low : zone.high;
				if (touch <= 0) touch = zone.center;
				list.Add(new ConfluenceTarget
				{
					price = zone.center,
					touch = touch,
					distance = Math.Abs(zone.center - price),
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
			int z = b.zScore.CompareTo(a.zScore);
			if (z != 0) return z;
			int r = b.rawScore.CompareTo(a.rawScore);
			if (r != 0) return r;
			return b.hitCount.CompareTo(a.hitCount);
		}

		private void ensureConfluenceZones()
		{
			int bar = 0;
			try { bar = Bars.CurrentBar; } catch { }
			if (!m_confluenceDirty && m_confluenceBuiltBar == bar) return;
			rebuildConfluenceZones();
			m_confluenceDirty = false;
			m_confluenceBuiltBar = bar;
		}

		private void rebuildConfluenceZones()
		{
			m_confluenceZones.Clear();
			List<ConfluenceAnchor> anchors = new List<ConfluenceAnchor>(64);
			collectConfluenceAnchors(anchors);
			if (anchors.Count == 0) return;

			double tol = confluenceTolerance();
			anchors.Sort((a, b) => a.price.CompareTo(b.price));

			List<ConfluenceAnchor> cluster = new List<ConfluenceAnchor>();
			for (int i = 0; i < anchors.Count; i++)
			{
				if (cluster.Count == 0 || anchors[i].price - cluster[cluster.Count - 1].price <= tol)
				{
					cluster.Add(anchors[i]);
					continue;
				}
				addConfluenceZone(cluster);
				cluster.Clear();
				cluster.Add(anchors[i]);
			}
			addConfluenceZone(cluster);
			applyConfluenceZScores();
		}

		private void collectConfluenceAnchors(List<ConfluenceAnchor> anchors)
		{
			collectStreamAnchors(ConfluenceDataStream1, ConfluenceWeight1, anchors);
			collectStreamAnchors(ConfluenceDataStream2, ConfluenceWeight2, anchors);
			collectStreamAnchors(ConfluenceDataStream3, ConfluenceWeight3, anchors);
			if (ConfluenceIncludeVwapSlots)
				collectVwapSlotAnchors(anchors);
		}

		private void collectStreamAnchors(int datastream, double streamWeight, List<ConfluenceAnchor> anchors)
		{
			if (datastream < 1 || streamWeight <= 0) return;
			if (!m_streams.TryGetValue(datastream, out StreamPack sp) || sp.pack == null) return;

			List<IProfile> profiles = listedProfiles(sp);
			if (profiles.Count == 0) return;

			int take = ConfluenceLookbackProfiles < 1 ? DEFAULT_CONFLUENCE_LOOKBACK : ConfluenceLookbackProfiles;
			int from = Math.Max(0, profiles.Count - take);
			double decay = ConfluenceSameStreamDecay;
			if (decay < 0) decay = 0;
			if (decay > 1) decay = 1;

			for (int i = from; i < profiles.Count; i++)
			{
				IProfile vp = profiles[i];
				ensureExtra(sp, vp);
				VpKey key = new VpKey(datastream, vp);
				if (!sp.extra.TryGetValue(key, out VpExtra extra) || extra == null) continue;

				int age = profiles.Count - 1 - i;   // 0 = most recent on this stream
				double ageW = Math.Pow(decay, age);
				double w = streamWeight * ageW;
				int start = profileStartBar(vp);
				addProfileAnchors(anchors, extra, datastream, start, age, w);
			}
		}

		private static List<IProfile> listedProfiles(StreamPack sp)
		{
			List<IProfile> list = new List<IProfile>();
			if (sp == null || sp.pack == null) return list;
			try
			{
				if (sp.pack.Items == null) return list;
			}
			catch
			{
				// Items can be non-null but already disposed / unusable.
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
				return list;
			}
			list.Sort((a, b) => profileStartBar(a).CompareTo(profileStartBar(b)));
			return list;
		}

		private void addProfileAnchors(List<ConfluenceAnchor> anchors, VpExtra extra, int datastream, int startBar, int age, double streamAgeWeight)
		{
			addAnchor(anchors, datastream, startBar, age, EConfluenceKind.Poc, extra.poc, streamAgeWeight * 1.00);
			addAnchor(anchors, datastream, startBar, age, EConfluenceKind.Vah, extra.vah, streamAgeWeight * 0.90);
			addAnchor(anchors, datastream, startBar, age, EConfluenceKind.Val, extra.val, streamAgeWeight * 0.90);
			if (ConfluenceIncludeProfileVwap)
				addAnchor(anchors, datastream, startBar, age, EConfluenceKind.ProfileVwap, extra.profileVwap, streamAgeWeight * 0.80);

			if (!ConfluenceIncludeHvns || extra.hvns == null) return;
			for (int i = 0; i < extra.hvns.Count; i++)
			{
				Node n = extra.hvns[i];
				if (n.isPoc) continue;   // already counted as POC
				double hs = n.strength > 0.35 ? n.strength : 0.35;
				addAnchor(anchors, datastream, startBar, age, EConfluenceKind.Hvn, n.peak, streamAgeWeight * 0.85 * hs);
			}
		}

		private void collectVwapSlotAnchors(List<ConfluenceAnchor> anchors)
		{
			addSlotAnchor(anchors, m_vwap1, EConfluenceKind.Vwap);
			addSlotAnchor(anchors, m_vwap2, EConfluenceKind.Vwap2);
			addSlotAnchor(anchors, m_vwap3, EConfluenceKind.Vwap3);
		}

		private void addSlotAnchor(List<ConfluenceAnchor> anchors, VwapEngine eng, EConfluenceKind kind)
		{
			if (eng == null || eng.type == EVwapType.Disabled || !eng.valid || eng.value <= 0) return;
			int ds = eng.datastream;
			double w = streamWeightFor(ds);
			if (w <= 0) return;
			addAnchor(anchors, ds, -1, 0, kind, eng.value, w * 0.85);
		}

		private double streamWeightFor(int datastream)
		{
			if (datastream < 1) return 0;
			if (datastream == ConfluenceDataStream1) return ConfluenceWeight1;
			if (datastream == ConfluenceDataStream2) return ConfluenceWeight2;
			if (datastream == ConfluenceDataStream3) return ConfluenceWeight3;
			return 0;   // slot VWAP only votes when its stream is one of the three confluence streams
		}

		private static void addAnchor(List<ConfluenceAnchor> anchors, int datastream, int startBar, int age, EConfluenceKind kind, double price, double weight)
		{
			if (anchors == null || price <= 0 || weight <= 0) return;
			anchors.Add(new ConfluenceAnchor
			{
				dataStream = datastream,
				profileStartBar = startBar,
				profileAge = age,
				kind = kind,
				price = price,
				weight = weight
			});
		}

		private void addConfluenceZone(List<ConfluenceAnchor> cluster)
		{
			if (cluster == null || cluster.Count == 0) return;
			if (cluster.Count < ConfluenceMinHits) return;

			ConfluenceZone zone = new ConfluenceZone();
			double wSum = 0;
			double pw = 0;
			double lo = cluster[0].price;
			double hi = cluster[0].price;
			HashSet<int> streams = new HashSet<int>();
			HashSet<long> profiles = new HashSet<long>();

			for (int i = 0; i < cluster.Count; i++)
			{
				ConfluenceAnchor a = cluster[i];
				if (a.price < lo) lo = a.price;
				if (a.price > hi) hi = a.price;
				wSum += a.weight;
				pw += a.price * a.weight;
				streams.Add(a.dataStream);
				if (a.profileStartBar >= 0)
					profiles.Add(((long)a.dataStream << 32) ^ (uint)a.profileStartBar);
				zone.hits.Add(new ConfluenceHit
				{
					dataStream = a.dataStream,
					profileStartBar = a.profileStartBar,
					profileAge = a.profileAge,
					kind = a.kind,
					price = a.price,
					weight = a.weight
				});
			}

			zone.low = lo;
			zone.high = hi;
			zone.center = wSum > 0 ? pw / wSum : (lo + hi) * 0.5;
			zone.hitCount = zone.hits.Count;
			zone.streamCount = streams.Count;
			zone.profileCount = profiles.Count;
			zone.rawScore = wSum * (1.0 + ConfluenceCrossStreamBonus * Math.Max(0, zone.streamCount - 1));
			m_confluenceZones.Add(zone);
		}

		private void applyConfluenceZScores()
		{
			int n = m_confluenceZones.Count;
			if (n == 0) return;
			if (n == 1)
			{
				m_confluenceZones[0].zScore = 0;
				return;
			}
			double mean = 0;
			for (int i = 0; i < n; i++)
				mean += m_confluenceZones[i].rawScore;
			mean /= n;
			double var = 0;
			for (int i = 0; i < n; i++)
			{
				double d = m_confluenceZones[i].rawScore - mean;
				var += d * d;
			}
			double stdev = Math.Sqrt(var / n);
			if (stdev <= 0)
			{
				for (int i = 0; i < n; i++)
					m_confluenceZones[i].zScore = 0;
				return;
			}
			for (int i = 0; i < n; i++)
				m_confluenceZones[i].zScore = (m_confluenceZones[i].rawScore - mean) / stdev;
		}

		private double confluenceTolerance()
		{
			int ticks = ConfluenceAlignTicks < 1 ? DEFAULT_CONFLUENCE_ALIGN_TICKS : ConfluenceAlignTicks;
			double tick = tickSize();
			if (tick <= 0) tick = 0.01;
			return ticks * tick;
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
			int ticks = MinNodeTicks < 1 ? DEFAULT_MIN_NODE_TICKS : MinNodeTicks;
			double need = ticks * tickSize();
			if (need <= 0) return true;
			return (high - low) + 1e-12 >= need;
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

		private void onVpChanged(StreamPack sp, bool full)
		{
			if (sp.pack == null) return;
			if (full) sp.extra.Clear();   // objects behind old VpKeys are gone, below code will recompute them for a full refresh
			try
			{
				if (sp.pack.Items == null) return;
				foreach (IProfile vp in sp.pack.Items)
					ensureExtra(sp, vp);
			}
			catch { }
			m_confluenceDirty = true;
			m_volumePctDirty = true;
		}

		private void ensureExtra(StreamPack sp, IProfile vp)
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
				fillNodes(vp, extra);
			stampExtra(extra);
			m_volumePctDirty = true;
		}

		private void fillNodes(IProfile vp, VpExtra extra)
		{
			extra.hvns.Clear();
			extra.lvns.Clear();
			extra.levels.Clear();
			extra.poc = 0;
			extra.vah = 0;
			extra.val = 0;
			extra.pocVol = 0;
			extra.profileVwap = 0;
			extra.maxTotalPercentile = 0;
			extra.maxAskPercentile = 0;
			extra.maxBidPercentile = 0;
			if (vp == null || vp.Values == null) return;
			readLevels(vp, extra.levels);
			List<Row> rows = rowsFromLevels(extra.levels);
			if (rows.Count < 3) return;
			extra.poc = levelPrice(vp.POC);
			extra.vah = vaExtreme(vp, true);
			extra.val = vaExtreme(vp, false);

			double pv = 0;
			double tv = 0;
			for (int r = 0; r < rows.Count; r++)
			{
				pv += rows[r].price * rows[r].volume;
				tv += rows[r].volume;
			}
			if (tv > 0) extra.profileVwap = pv / tv;

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
			extra.pocVol = pocVol;
			if (extra.poc <= 0) extra.poc = rows[pocIdx].price;
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

		private void refreshVolumePercentiles()
		{
			if (!m_volumePctDirty) return;
			m_volumePctDirty = false;

			foreach (var kv in m_streams)
			{
				StreamPack sp = kv.Value;
				try
				{ if (sp == null || sp.pack == null || sp.pack.Items == null) continue; }
				catch { continue; /* sp.pack.Items can be in an unusable state where it's not null but should be disposed */ }
				List<VpExtra> streamExtras = new List<VpExtra>();
				try
				{
					foreach (IProfile vp in sp.pack.Items)
					{
						if (vp == null || vp.Empty) continue;
						VpKey key = new VpKey(sp.datastream, vp);
						VpExtra extra;
						if (!sp.extra.TryGetValue(key, out extra) || extra == null || extra.levels.Count == 0)
							continue;
						streamExtras.Add(extra);
					}
				}
				catch { }
				if (streamExtras.Count == 0) continue;

				double[] sTotal, sAsk, sBid;
				gatherLevelVolumes(streamExtras, out sTotal, out sAsk, out sBid);
				Array.Sort(sTotal);
				Array.Sort(sAsk);
				Array.Sort(sBid);
				for (int e = 0; e < streamExtras.Count; e++)
					stampLevelPercentiles(streamExtras[e], sTotal, sAsk, sBid);
			}
		}

		private static void gatherLevelVolumes(List<VpExtra> extras, out double[] totals, out double[] asks, out double[] bids)
		{
			int n = 0;
			if (extras != null)
			{
				for (int i = 0; i < extras.Count; i++)
					if (extras[i] != null) n += extras[i].levels.Count;
			}
			totals = new double[n];
			asks = new double[n];
			bids = new double[n];
			int w = 0;
			if (extras == null) return;
			for (int i = 0; i < extras.Count; i++)
			{
				VpExtra extra = extras[i];
				if (extra == null) continue;
				for (int k = 0; k < extra.levels.Count; k++)
				{
					totals[w] = extra.levels[k].total;
					asks[w] = extra.levels[k].ask;
					bids[w] = extra.levels[k].bid;
					w++;
				}
			}
		}

		private static void stampLevelPercentiles(VpExtra extra, double[] streamTotal, double[] streamAsk, double[] streamBid)
		{
			if (extra == null) return;
			extra.maxTotalPercentile = 0;
			extra.maxAskPercentile = 0;
			extra.maxBidPercentile = 0;
			for (int i = 0; i < extra.levels.Count; i++)
			{
				LevelVol lv = extra.levels[i];
				lv.totalPercentile = rankPercentile(streamTotal, lv.total);
				lv.askPercentile = rankPercentile(streamAsk, lv.ask);
				lv.bidPercentile = rankPercentile(streamBid, lv.bid);
				extra.levels[i] = lv;
				if (lv.totalPercentile > extra.maxTotalPercentile) extra.maxTotalPercentile = lv.totalPercentile;
				if (lv.askPercentile > extra.maxAskPercentile) extra.maxAskPercentile = lv.askPercentile;
				if (lv.bidPercentile > extra.maxBidPercentile) extra.maxBidPercentile = lv.bidPercentile;
			}
		}

		/// <summary>
		/// Empirical CDF: fraction of samples with value &lt;= x. 0..1.
		/// </summary>
		private static double rankPercentile(double[] sortedAscending, double x)
		{
			if (sortedAscending == null || sortedAscending.Length == 0) return 0;
			int n = sortedAscending.Length;
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
