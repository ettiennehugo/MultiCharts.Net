using ATCenterProxy.interop;
using PowerLanguage.Function;
using PowerLanguage.VolumeProfile;
using System;
using System.Collections.Generic;
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
	///   - HVN, LVN detection - DONE
	///   - VWAP integration - DONE (internal engines + profile-histogram VWAP)
	///   - confluence zones
	///   - directionality of confluence zones
	///   - ranking of best zones (add parameter to keep only a certain number of zones, e.g. 5)
	///   - can you introduce a mechanism for if-then logic for the different zones (accept vs reject, per setup).
	/// </summary>
	public class _VolumeProfileBase : _CustomSignal
	{
		//constants
		public const double DEFAULT_HVN_PERCENTILE = 0.8;  // Default percentile of prices relative to POC considered high volume node prices.
		public const double DEFAULT_LVN_PERCENTILE = 0.2;  // Default percentile of prices relative to POC considered low volume node prices.
		public const int DEFAULT_SMOOTH_TICKS = 3;  // Default smoothing length used for ticks.
		public const int DEFAULT_MIN_NODE_TICKS = 1;
		public const double DEFAULT_MAX_LVN_PCT_OF_RANGE = 0.12; // TBD - Do we need this?? Maximum percentage size of the volume profile a node can be - prevents one LVN from swallowing too much of profile.
		public const int DEFAULT_VWAP_LENGTH = 20;
		public const int VWAP_SLOT_COUNT = 3;

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
		/// </summary>
		private struct VpKey : IEquatable<VpKey>
		{
			public readonly int datastream;
			public readonly int startBar;
			public readonly int endBar;

			public VpKey(int datastream, IProfile p)
			{
				this.datastream = datastream;
				Pair<int> iv = p.BarsInterval;
				startBar = iv.First;
				endBar = iv.Second;
			}

			public bool Equals(VpKey o)
			{
				return datastream == o.datastream && startBar == o.startBar && endBar == o.endBar;
			}

			public override bool Equals(object o) { return o is VpKey k && Equals(k); }
			public override int GetHashCode() { return datastream ^ (startBar * 397) ^ endBar; }

		};

		/// <summary>
		/// Extra data stored for a volume profile.
		/// </summary>
		public class VpExtra
		{
			public double poc;    // Point of control for the volume profile.
			public double pocVol; // Volume at the point of control.
			public double vah;  // Value area high.
			public double val;  // Value area low.
			public double profileVwap;  // Σ(price * levelVolume) / Σ(levelVolume) from the same rows used for HVN/LVN.
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
		};

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
				bool last = false;
				try { last = bars.LastBarInSession; } catch { }
				if (lastInSession != null) lastInSession.Value = last;

				bool first = bars.CurrentBar <= 1;
				if (!first && lastInSession != null)
				{
					try { first = lastInSession[1]; }
					catch { first = false; }
				}
				if (first) clearSums();

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

		//attribute
		private Dictionary<int, StreamPack> m_streams;  /// Stream packs by data stream index.
		private VwapEngine m_vwap1;
		private VwapEngine m_vwap2;
		private VwapEngine m_vwap3;

		//interface methods
		public _VolumeProfileBase(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			HvnPercentile = DEFAULT_HVN_PERCENTILE;
			LvnPercentile = DEFAULT_LVN_PERCENTILE;

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
			if (HvnPercentile < 0 || HvnPercentile > 1.0) throw new ArgumentException("HvnPercentile must be a percentage, 0 to 1.0.");
			if (LvnPercentile < 0 || LvnPercentile > 1.0) throw new ArgumentException("LvnPercentile must be a percentage, 0 to 1.0.");
			if (HvnPercentile < LvnPercentile) throw new ArgumentException("HvnPercentile must be larger than LvnPercentile.");
			unhookStreams();
			m_streams.Clear();
			hookStreams();
			configureVwap(m_vwap1, VwapType, VwapLength, VwapFromDate, VwapDataStream, "VwapFromDate", "VwapDataStream");
			configureVwap(m_vwap2, Vwap2Type, Vwap2Length, Vwap2FromDate, Vwap2DataStream, "Vwap2FromDate", "Vwap2DataStream");
			configureVwap(m_vwap3, Vwap3Type, Vwap3Length, Vwap3FromDate, Vwap3DataStream, "Vwap3FromDate", "Vwap3DataStream");
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			stepVwap(m_vwap1);
			stepVwap(m_vwap2);
			stepVwap(m_vwap3);
			stampCurrentExtras();
		}

		/// <summary>
		/// Returns the extra volume profile information computed for the given bar.
		/// </summary>
		/// <param name="barIndex">Index of the bar for which to get the extra volume profile information on data stream 1</param>
		/// <returns>Extra volume profile information or null if nothing found.</returns>
		public VpExtra ExtraVolumeProfileForBar(int barIndex)
		{
			return ExtraVolumeProfileForBar(1, barIndex);
		}

		/// <summary>
		/// Returns the extra volume profile information computed for the given bar.
		/// </summary>
		/// <param name="barIndex">Index of the bar for which to get the extra volume profile information on data stream 1</param>
		/// <returns>Extra volume profile information or null if nothing found.</returns>
		public VpExtra ExtraVolumeProfileForBar(int datastream, int barIndex)
		{
			if (m_streams.TryGetValue(datastream, out StreamPack streamPack))
			{
				IProfile vp = null;
				try
				{
					vp = streamPack.pack.ItemForBar(barIndex);
				}
				catch (Exception e)
				{
					trace(ETraceLevel.Verbose, $"Exception trying to find volume profile on data stream {datastream} for bar {barIndex} - {e.Message}");
					return null;
				}
				if (vp == null || vp.Empty)
				{
					trace(ETraceLevel.Verbose, $"Failed to find volume profile for data stream {datastream} for bar {barIndex}.");
					return null;
				}
				VpKey key = new VpKey(datastream, vp);
				if (streamPack.extra.TryGetValue(key, out VpExtra extra))
				{
					stampExtra(extra);
					return extra;
				}
				trace(ETraceLevel.Verbose, $"Failed to find extra entry for data stream {datastream} for bar {barIndex}.");
			}
			return null;
		}

		/// <summary>Slot 1 current VWAP, or NaN when Disabled / not valid yet.</summary>
		public double CurrentVwap1 { get { return m_vwap1 != null && m_vwap1.valid ? m_vwap1.value : double.NaN; } }
		/// <summary>Slot 2 current VWAP, or NaN when Disabled / not valid yet.</summary>
		public double CurrentVwap2 { get { return m_vwap2 != null && m_vwap2.valid ? m_vwap2.value : double.NaN; } }
		/// <summary>Slot 3 current VWAP, or NaN when Disabled / not valid yet.</summary>
		public double CurrentVwap3 { get { return m_vwap3 != null && m_vwap3.valid ? m_vwap3.value : double.NaN; } }
		public double CurrentVwapSigma1 { get { return m_vwap1 != null && m_vwap1.valid ? m_vwap1.sigma : double.NaN; } }
		public double CurrentVwap2Sigma { get { return m_vwap2 != null && m_vwap2.valid ? m_vwap2.sigma : double.NaN; } }
		public double CurrentVwap3Sigma { get { return m_vwap3 != null && m_vwap3.valid ? m_vwap3.sigma : double.NaN; } }
		public bool VwapActive1 { get { return m_vwap1 != null && m_vwap1.type != EVwapType.Disabled && m_vwap1.valid; } }
		public bool Vwap2Active { get { return m_vwap2 != null && m_vwap2.type != EVwapType.Disabled && m_vwap2.valid; } }
		public bool Vwap3Active { get { return m_vwap3 != null && m_vwap3.type != EVwapType.Disabled && m_vwap3.valid; } }
		/// <summary>Resolved chart data stream for slot 1 (1 = Data1). 0 if unused.</summary>
		public int CurrentVwapStream1 { get { return slotStream(m_vwap1); } }
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
			eng.type = type;
			eng.length = length < 1 ? DEFAULT_VWAP_LENGTH : length;
			eng.datastream = 0;
			eng.fromDate = DateTime.MinValue;
			eng.Reset();

			if (type == EVwapType.Disabled) return;

			if (stream < 1)
				throw new ArgumentException(streamInputName + " must be a chart data stream index >= 1 (1 = Data1, 2 = Data2, …).");
			if (!IsExist(stream))
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
				if (!IsExist(stream)) return null;
				return BarsOfData(stream);
			}
			catch
			{
				return null;
			}
		}

		private void stampCurrentExtras()
		{
			foreach (var kv in m_streams)
			{
				StreamPack sp = kv.Value;
				if (sp.pack == null) continue;
				int barIndex = 0;
				try
				{
					IInstrument b = barsOf(sp.datastream);
					if (b == null) continue;
					barIndex = b.CurrentBar;
				}
				catch { continue; }
				try
				{
					IProfile vp = sp.pack.ItemForBar(barIndex);
					if (vp == null || vp.Empty) continue;
					VpKey key = new VpKey(sp.datastream, vp);
					if (sp.extra.TryGetValue(key, out VpExtra extra))
						stampExtra(extra);
				}
				catch { }
			}
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

		private void hookStreams()
		{
			m_streams.Clear();
			for (int s = 1; s <= MaxDataStream; ++s)
			{
				if (!IsExist(s)) continue;
				IProfilesCollection pack = getPack(s);
				if (pack == null) continue;
				bool visible = false;
				try { visible = pack.Visible; } catch { }
				if (!visible) continue;          // VP not enabled on this stream
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
		}

		private void ensureExtra(StreamPack sp, IProfile vp)
		{
			if (vp == null || vp.Empty) return;
			VpKey key = new VpKey(sp.datastream, vp);
			VpExtra extra;
			if (!sp.extra.TryGetValue(key, out extra))
			{
				extra = new VpExtra();
				sp.extra[key] = extra;
			}
			fillNodes(vp, extra);
			stampExtra(extra);
		}

		private void fillNodes(IProfile vp, VpExtra extra)
		{
			extra.hvns.Clear();
			extra.lvns.Clear();
			extra.poc = 0;
			extra.vah = 0;
			extra.val = 0;
			extra.pocVol = 0;
			extra.profileVwap = 0;
			if (vp == null || vp.Values == null) return;
			List<Row> rows = readRows(vp);
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
				if (b - a + 1 < DEFAULT_MIN_NODE_TICKS) continue;
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
				if (b - a + 1 < DEFAULT_MIN_NODE_TICKS) continue;
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

		private List<Row> readRows(IProfile vp)
		{
			var rows = new List<Row>();
			foreach (ILevel lvl in vp.Values)
			{
				if (lvl == null || lvl.Price == null) continue;
				double vol = (double)(lvl.AskTradedValue + lvl.BidTradedValue);
				if (vol <= 0) continue;
				rows.Add(new Row { price = lvl.Price.Dbl, volume = vol });
			}
			rows.Sort((x, y) => x.price.CompareTo(y.price));
			return rows;
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
