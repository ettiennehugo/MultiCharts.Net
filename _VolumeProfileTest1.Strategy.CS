using PowerLanguage.VolumeProfile;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Diagnostic signal for _VolumeProfileBase.
	///
	/// When a new volume profile starts to form on a watched data stream,
	/// dumps the *previous* (now completed) profile's VpExtra state to the
	/// _CustomSignal trace file at ETraceLevel.Info — once per profile.
	///
	/// IsNewVolumeProfile(stream) stays true for the entire life of the bar
	/// that opened the profile. On a higher-TF stream that bar does not
	/// advance while Data1 keeps calculating, so the gate is the profile
	/// start bar: log the prior profile only when this stream's start bar
	/// changes, and never again for that start bar.
	///
	/// Chart: Format Instrument → Volume Profile → Enable (Visible).
	/// Add this signal; leave AutoEntry / orders alone — this class does not trade.
	/// </summary>
	public class _VolumeProfileTest1 : _VolumeProfileBase
	{
		//constants
		public const int DEFAULT_TRACE_STREAM = 1;
		public const int DEFAULT_MAX_TRACE_LEVELS = 16;
		public const int MAX_STREAM_SCAN = 8;

		//properties
		/// <summary>When true, dump prior-VP state on the first bar of each new profile.</summary>
		[Input] public bool TracePriorOnNewProfile { get; set; }
		/// <summary>When true, watch every existing chart data stream. When false, only TraceDataStream.</summary>
		[Input] public bool TraceAllDataStreams { get; set; }
		/// <summary>Data stream to watch when TraceAllDataStreams is false. 1 = Data1.</summary>
		[Input] public int TraceDataStream { get; set; }
		/// <summary>Include HVN / LVN rows in the Info dump.</summary>
		[Input] public bool TraceNodes { get; set; }
		/// <summary>Include exceptional ILevel rows (HasExceptionalVolume / ExceptionalLevels).</summary>
		[Input] public bool TraceExceptionalLevels { get; set; }
		/// <summary>Include a short confluence snapshot taken on the new-profile bar.</summary>
		[Input] public bool TraceConfluence { get; set; }
		/// <summary>Cap on exceptional-level rows written per profile. 0 writes none.</summary>
		[Input] public int TraceMaxLevels { get; set; }

		// last profile start bar observed on each stream (1-based). 0 = not yet seen.
		private readonly Dictionary<int, int> m_openStart = new Dictionary<int, int>();
		// prior profiles already written, key = "datastream:startBar"
		private readonly HashSet<string> m_loggedPrior = new HashSet<string>();

		//interface methods
		public _VolumeProfileTest1(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			TracePriorOnNewProfile = true;
			TraceAllDataStreams = false;
			TraceDataStream = DEFAULT_TRACE_STREAM;
			TraceNodes = true;
			TraceExceptionalLevels = true;
			TraceConfluence = true;
			TraceMaxLevels = DEFAULT_MAX_TRACE_LEVELS;
		}

		protected override void StartCalc()
		{
			if (MaxDataStream >= 2) ConfluenceDataStream2 = 2;
			if (MaxDataStream >= 3) ConfluenceDataStream3 = 3;
			base.StartCalc();
			if (TraceDataStream < 1)
				throw new ArgumentException("TraceDataStream must be a chart data stream index >= 1 (1 = Data1).");
			if (MaxDataStream >= 1 && TraceDataStream > MaxDataStream)
				throw new ArgumentException("TraceDataStream = " + TraceDataStream + " is not on this chart (MaxDataStream = " + MaxDataStream + ").");
			if (TraceMaxLevels < 0)
				throw new ArgumentException("TraceMaxLevels must be 0 or greater.");
			m_openStart.Clear();
			m_loggedPrior.Clear();
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (!TracePriorOnNewProfile) return;

			if (TraceAllDataStreams)
				TracePriorProfilesOnNewForAllStreams();
			else
				TracePriorProfileOnNew(TraceDataStream);
		}

		//methods
		/// <summary>
		/// Walk Data1..DataN and dump the previous profile on any stream
		/// that just opened a new volume profile.
		/// </summary>
		public void TracePriorProfilesOnNewForAllStreams()
		{
			for (int ds = 1; ds <= MAX_STREAM_SCAN; ds++)
			{
				if (!streamExists(ds)) continue;
				TracePriorProfileOnNew(ds);
			}
		}

		/// <summary>
		/// If this stream's volume profile start bar just changed, the previous
		/// profile is fully formed. Write it once. Same start bar on later
		/// Data1 calculations (higher-TF bar still open) is ignored.
		/// </summary>
		public void TracePriorProfileOnNew(int datastream)
		{
			int barIndex = currentBarIndex(datastream);
			if (barIndex < 1) return;

			int start = profileStartOnBar(datastream, barIndex);
			if (start < 1) return;

			if (m_openStart.TryGetValue(datastream, out int seen) && seen == start)
				return;

			m_openStart[datastream] = start;
			if (seen < 1)
				return; // first profile on this stream — not completed yet

			string key = datastream.ToString(CultureInfo.InvariantCulture)
				+ ":" + seen.ToString(CultureInfo.InvariantCulture);
			if (!m_loggedPrior.Add(key))
				return;

			int priorBar = start - 1;
			if (priorBar < 1) priorBar = barIndex - 1;
			TracePreviousVolumeProfile(datastream, barIndex, priorBar, seen);
		}

		/// <summary>
		/// Info-trace the completed profile that owned <paramref name="priorBar"/>.
		/// Called once per prior start bar, when the next profile begins.
		/// </summary>
		public void TracePreviousVolumeProfile(int datastream, int newProfileBarIndex, int priorBar, int priorStart)
		{
			DateTime barTime = barTimeOf(datastream, 0);
			string when = barTime == DateTime.MinValue
				? "n/a"
				: barTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] NEW profile Data{0} bar={1} time={2} — dumping PRIOR once (priorStart={3})",
				datastream, newProfileBarIndex, when, priorStart));

			if (priorBar < 1)
			{
				trace(ETraceLevel.Info,
					$"[VP-TEST] Data{datastream} bar={newProfileBarIndex} has no previous bar; no prior profile to dump.");
				return;
			}

			VpExtra extra = ExtraVolumeProfileForBar(datastream, priorBar);
			if (extra == null)
			{
				trace(ETraceLevel.Info,
					$"[VP-TEST] Data{datastream} priorBar={priorBar} ExtraVolumeProfileForBar returned null.");
				return;
			}

			TraceVpExtra(datastream, priorBar, extra, "PRIOR");
			if (TraceNodes)
			{
				TraceNodesList(extra.hvns, "HVN", datastream, priorBar);
				TraceNodesList(extra.lvns, "LVN", datastream, priorBar);
			}
			if (TraceExceptionalLevels)
				TraceExceptionalLevelsList(extra, datastream, priorBar);
			TraceLiveVwapSlots();
			if (TraceConfluence)
				TraceConfluenceSnapshot(datastream, newProfileBarIndex);
		}

		/// <summary>
		/// Info-trace the completed profile that owned <paramref name="newProfileBarIndex"/> - 1.
		/// Still once-only: a repeated call for the same prior start bar is dropped.
		/// </summary>
		public void TracePreviousVolumeProfile(int datastream, int newProfileBarIndex)
		{
			int priorBar = newProfileBarIndex - 1;
			int priorStart = priorBar;
			VpExtra extra = priorBar >= 1 ? ExtraVolumeProfileForBar(datastream, priorBar) : null;
			if (extra != null && extra.startBar >= 1) priorStart = extra.startBar;
			if (priorStart >= 1)
			{
				string key = datastream.ToString(CultureInfo.InvariantCulture)
					+ ":" + priorStart.ToString(CultureInfo.InvariantCulture);
				if (!m_loggedPrior.Add(key))
					return;
			}
			TracePreviousVolumeProfile(datastream, newProfileBarIndex, priorBar, priorStart);
		}

		private int profileStartOnBar(int datastream, int barIndex)
		{
			VpExtra extra = ExtraVolumeProfileForBar(datastream, barIndex);
			if (extra != null && extra.startBar >= 1)
				return extra.startBar;
			if (IsNewVolumeProfile(datastream, barIndex))
				return barIndex;
			return -1;
		}

		/// <summary>
		/// Info-trace the core VpExtra snapshot (POC / VA / VWAP / percentiles).
		/// </summary>
		public void TraceVpExtra(int datastream, int barIndex, VpExtra extra, string label)
		{
			if (extra == null)
			{
				trace(ETraceLevel.Info, $"[VP-TEST] {label} Data{datastream} bar={barIndex} extra=null");
				return;
			}

			double vaWidth = extra.vah > 0 && extra.val > 0 ? extra.vah - extra.val : 0;
			string header = string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] {0} Data{1} bar={2} startBar={3} endBar={4} streamOnExtra={5} levels={6} hvn={7} lvn={8}",
				label, datastream, barIndex, extra.startBar, extra.endBar, extra.dataStream, extra.levels.Count, extra.hvns.Count, extra.lvns.Count);
			trace(ETraceLevel.Info, header);

			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] {0} OHLC open={1} high={2} low={3} close={4}",
				label, fmt(extra.open), fmt(extra.high), fmt(extra.low), fmt(extra.close)));

			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] {0} POC={1} pocVol={2} VAH={3} VAL={4} vaWidth={5} profileVwap={6}",
				label,
				fmt(extra.poc), fmt(extra.pocVol),
				fmt(extra.vah), fmt(extra.val), fmt(vaWidth),
				fmt(extra.profileVwap)));

			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] {0} VWAP slots  vwap={1}(s{2} σ={3})  vwap2={4}(s{5} σ={6})  vwap3={7}(s{8} σ={9})",
				label,
				fmt(extra.vwap1), extra.vwap1Stream, fmt(extra.vwap1Sigma),
				fmt(extra.vwap2), extra.vwap2Stream, fmt(extra.vwap2Sigma),
				fmt(extra.vwap3), extra.vwap3Stream, fmt(extra.vwap3Sigma)));

			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] {0} maxPercentiles total={1} ask={2} bid={3} exceptional={4} threshold={5}",
				label,
				fmt(extra.maxTotalPercentile),
				fmt(extra.maxAskPercentile),
				fmt(extra.maxBidPercentile),
				extra.HasExceptionalVolume(),
				fmt(DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE)));
		}

		/// <summary>
		/// Info-trace a node list (HVN or LVN) from the previous profile.
		/// </summary>
		public void TraceNodesList(List<Node> nodes, string kind, int datastream, int barIndex)
		{
			if (nodes == null || nodes.Count == 0)
			{
				trace(ETraceLevel.Info,
					$"[VP-TEST] {kind} Data{datastream} bar={barIndex} count=0");
				return;
			}

			trace(ETraceLevel.Info,
				$"[VP-TEST] {kind} Data{datastream} bar={barIndex} count={nodes.Count}");

			int cap = TraceMaxLevels > 0 ? Math.Min(TraceMaxLevels, nodes.Count) : nodes.Count;
			for (int i = 0; i < cap; i++)
			{
				Node n = nodes[i];
				trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
					"[VP-TEST]   {0}[{1}] low={2} high={3} peak={4} peakVol={5} strength={6} isPoc={7}",
					kind, i, fmt(n.low), fmt(n.high), fmt(n.peak), fmt(n.peakVol), fmt(n.strength), n.isPoc));
			}
			if (nodes.Count > cap)
			{
				trace(ETraceLevel.Info,
					$"[VP-TEST]   {kind} … {nodes.Count - cap} more not traced (TraceMaxLevels={TraceMaxLevels})");
			}
		}

		/// <summary>
		/// Info-trace exceptional volume levels on the previous profile.
		/// </summary>
		public void TraceExceptionalLevelsList(VpExtra extra, int datastream, int barIndex)
		{
			if (extra == null)
			{
				trace(ETraceLevel.Info,
					$"[VP-TEST] EXC Data{datastream} bar={barIndex} extra=null");
				return;
			}

			if (!extra.HasExceptionalVolume())
			{
				trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
					"[VP-TEST] EXC Data{0} bar={1} none above {2}",
					datastream, barIndex, fmt(DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE)));
				return;
			}

			List<LevelVol> hit = extra.ExceptionalLevels(DEFAULT_EXCEPTIONAL_VOLUME_PERCENTILE);
			LevelVol peak = extra.PeakTotalLevel();
			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] EXC Data{0} bar={1} count={2} peakTotal px={3} tot={4} pct={5}",
				datastream, barIndex, hit.Count, fmt(peak.price), fmt(peak.total), fmt(peak.totalPercentile)));

			int cap = TraceMaxLevels > 0 ? Math.Min(TraceMaxLevels, hit.Count) : hit.Count;
			for (int i = 0; i < cap; i++)
			{
				LevelVol lv = hit[i];
				trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
					"[VP-TEST]   EXC[{0}] px={1} tot={2}({3}) ask={4}({5}) bid={6}({7})",
					i,
					fmt(lv.price),
					fmt(lv.total), fmt(lv.totalPercentile),
					fmt(lv.ask), fmt(lv.askPercentile),
					fmt(lv.bid), fmt(lv.bidPercentile)));
			}
			if (hit.Count > cap)
			{
				trace(ETraceLevel.Info,
					$"[VP-TEST]   EXC … {hit.Count - cap} more not traced (TraceMaxLevels={TraceMaxLevels})");
			}
		}

		/// <summary>
		/// Info-trace the live VWAP engines as they stand on the new-profile bar.
		/// </summary>
		public void TraceLiveVwapSlots()
		{
			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] LIVE VWAP  slot1={0} s{1} σ={2} active={3}  slot2={4} s{5} σ={6} active={7}  slot3={8} s{9} σ={10} active={11}",
				fmt(CurrentVwap), CurrentVwapStream, fmt(CurrentVwapSigma), VwapActive,
				fmt(CurrentVwap2), CurrentVwap2Stream, fmt(CurrentVwap2Sigma), Vwap2Active,
				fmt(CurrentVwap3), CurrentVwap3Stream, fmt(CurrentVwap3Sigma), Vwap3Active));
		}

		/// <summary>
		/// Info-trace top confluence zones computed on the bar the new profile opens.
		/// Zones still include the prior profile (lookback) plus the just-started one.
		/// </summary>
		public void TraceConfluenceSnapshot(int datastream, int barIndex)
		{
			List<ConfluenceZone> top = GetTopConfluenceZones();
			if (top == null || top.Count == 0)
			{
				trace(ETraceLevel.Info,
					$"[VP-TEST] CONF Data{datastream} bar={barIndex} top=0");
				return;
			}

			trace(ETraceLevel.Info,
				$"[VP-TEST] CONF Data{datastream} bar={barIndex} topCount={top.Count} alignTicks={ConfluenceAlignTicks}");

			StringBuilder kinds = new StringBuilder();
			int cap = TraceMaxLevels > 0 ? Math.Min(TraceMaxLevels, top.Count) : top.Count;
			for (int i = 0; i < cap; i++)
			{
				ConfluenceZone z = top[i];
				if (z == null) continue;
				kinds.Length = 0;
				for (int h = 0; h < z.hits.Count; h++)
				{
					if (h > 0) kinds.Append(',');
					kinds.Append(z.hits[h].kind);
					kinds.Append('@');
					kinds.Append(z.hits[h].dataStream);
					kinds.Append("#");
					kinds.Append(z.hits[h].profileStartBar);
					kinds.Append("/a");
					kinds.Append(z.hits[h].profileAge);
				}
				trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
					"[VP-TEST]   CONF[{0}] center={1} lo={2} hi={3} z={4} raw={5} hits={6} streams={7} profiles={8} [{9}]",
					i, fmt(z.center), fmt(z.low), fmt(z.high),
					fmt(z.zScore), fmt(z.rawScore),
					z.hitCount, z.streamCount, z.profileCount,
					kinds.ToString()));
			}
		}

		private int currentBarIndex(int datastream)
		{
			IInstrument bars = barsOfStream(datastream);
			if (bars == null) return -1;
			try
			{
				int n = bars.FullSymbolData.Current;
				if (n >= 1) return n;
			}
			catch { }
			return -1;
		}

		private DateTime barTimeOf(int datastream, int barsBack)
		{
			IInstrument bars = barsOfStream(datastream);
			if (bars == null) return DateTime.MinValue;
			try { return bars.Time[barsBack]; }
			catch { return DateTime.MinValue; }
		}

		private IInstrument barsOfStream(int datastream)
		{
			try
			{
				if (datastream <= 1) return Bars;
				if (datastream < 1 || datastream > MaxDataStream) return null;
				return BarsOfData(datastream);
			}
			catch
			{
				return null;
			}
		}

		private bool streamExists(int datastream)
		{
			return barsOfStream(datastream) != null;
		}

		private static string fmt(double v)
		{
			if (double.IsNaN(v)) return "NaN";
			if (double.IsInfinity(v)) return v > 0 ? "+Inf" : "-Inf";
			return v.ToString("0.####", CultureInfo.InvariantCulture);
		}
	}
}
