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
	/// On that same dump, TraceLookbackNakedPocs lists naked POCs on this
	/// stream that still fall inside ConfluenceLookbackProfiles (current
	/// window plus older windows, newest first). The developing profile is
	/// never naked.
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
		/// <summary>Include naked POCs on this stream inside ConfluenceLookbackProfiles.</summary>
		[Input] public bool TraceNakedPocs { get; set; }
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
			TraceNakedPocs = true;
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
				if (TraceNakedPocs)
					TraceLookbackNakedPocs(datastream, newProfileBarIndex);
				return;
			}

			VpExtra extra = ExtraVolumeProfileForBar(datastream, priorBar);
			if (extra == null)
			{
				trace(ETraceLevel.Info,
					$"[VP-TEST] Data{datastream} priorBar={priorBar} ExtraVolumeProfileForBar returned null.");
				if (TraceNakedPocs)
					TraceLookbackNakedPocs(datastream, newProfileBarIndex);
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
			{
				TraceConfluenceSnapshot(datastream, newProfileBarIndex);
				TraceConfluenceTargets(datastream, priorBar, extra);
			}
			if (TraceNakedPocs)
				TraceLookbackNakedPocs(datastream, newProfileBarIndex);
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
		/// Info-trace the core VpExtra snapshot (POC / naked POC / VA / VWAP / percentiles).
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
				"[VP-TEST] {0} POC={1} pocVol={2} VAH={3} VAL={4} vaWidth={5}",
				label,
				fmt(extra.poc), fmt(extra.pocVol),
				fmt(extra.vah), fmt(extra.val), fmt(vaWidth)));

			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] {0} nakedPOC={1} complete={2} touchedBar={3}",
				label, extra.pocNaked, extra.pocComplete, extra.pocTouchedBar));

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

		/// <summary>
		/// Info-trace the top 5 confluence targets beyond the completed profile's POC
		/// and each HVN, both bullish (magnets above) and bearish (magnets below).
		/// Written once, with the prior-profile dump.
		/// </summary>
		public void TraceConfluenceTargets(int datastream, int barIndex, VpExtra extra)
		{
			const int top = 5;
			if (extra == null)
			{
				trace(ETraceLevel.Info, $"[VP-TEST] TGT Data{datastream} bar={barIndex} extra=null");
				return;
			}

			TraceTargetsFrom(datastream, barIndex, "POC", extra.poc, 0, 0, false, top);
			if (extra.hvns == null || extra.hvns.Count == 0)
			{
				trace(ETraceLevel.Info, $"[VP-TEST] TGT Data{datastream} bar={barIndex} HVN count=0");
				return;
			}

			for (int i = 0; i < extra.hvns.Count; i++)
			{
				Node n = extra.hvns[i];
				TraceTargetsFrom(datastream, barIndex, "HVN[" + i + "]", n.peak, n.low, n.high, n.isPoc, top);
			}
		}

		private void TraceTargetsFrom(int datastream, int barIndex, string from, double reference, double low, double high, bool isPoc, int top)
		{
			if (reference <= 0)
			{
				trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
					"[VP-TEST] TGT Data{0} bar={1} from={2} ref={3} skipped",
					datastream, barIndex, from, fmt(reference)));
				return;
			}

			string range = low > 0 && high > 0
				? string.Format(CultureInfo.InvariantCulture, " range={0}-{1}", fmt(low), fmt(high))
				: "";
			string pocTag = isPoc ? " isPoc=True" : "";
			TraceTargetsOneSide(datastream, barIndex, from, reference, range, pocTag, EVpDirection.Bullish, top);
			TraceTargetsOneSide(datastream, barIndex, from, reference, range, pocTag, EVpDirection.Bearish, top);
		}

		private void TraceTargetsOneSide(int datastream, int barIndex, string from, double reference, string range, string pocTag, EVpDirection direction, int top)
		{
			List<ConfluenceTarget> targets = GetConfluenceTargets(reference, direction, top);
			int n = targets == null ? 0 : targets.Count;
			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] TGT Data{0} bar={1} from={2} ref={3}{4}{5} {6} count={7}",
				datastream, barIndex, from, fmt(reference), range, pocTag, direction, n));
			if (n == 0) return;

			StringBuilder kinds = new StringBuilder();
			for (int i = 0; i < n; i++)
			{
				ConfluenceTarget t = targets[i];
				if (t == null) continue;
				ConfluenceZone z = t.zone;
				kinds.Length = 0;
				if (z != null)
				{
					for (int h = 0; h < z.hits.Count; h++)
					{
						if (h > 0) kinds.Append(',');
						kinds.Append(z.hits[h].kind);
						kinds.Append('@');
						kinds.Append(z.hits[h].dataStream);
						kinds.Append("#");
						kinds.Append(z.hits[h].profileStartBar);
					}
				}
				trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
					"[VP-TEST]   TGT[{0}] {1} from={2} touch={3} price={4} dist={5} z={6} raw={7} hits={8} [{9}]",
					i, direction, from,
					fmt(t.touch), fmt(t.price), fmt(t.distance),
					fmt(z != null ? z.zScore : 0), fmt(z != null ? z.rawScore : 0),
					z != null ? z.hitCount : 0,
					kinds.ToString()));
			}
		}

		/// <summary>
		/// Info-trace naked POCs on <paramref name="datastream"/> that still sit
		/// inside the confluence lookback for that stream. Age 0 is the profile
		/// open on <paramref name="asOfBar"/> (the current / developing window —
		/// never naked). Older ages are prior windows, newest first, capped at
		/// ConfluenceLookbackProfiles. Profiles outside that walk are omitted
		/// even if NakedPocs still has them.
		/// </summary>
		public void TraceLookbackNakedPocs(int datastream, int asOfBar)
		{
			if (asOfBar < 1) asOfBar = currentBarIndex(datastream);
			int lookback = ConfluenceLookbackProfiles;
			if (lookback < 1) lookback = 1;

			// Public query stamps pocNaked / pocTouchedBar as of the calc bar, then
			// answers the historical question. The walk below is the lookback filter.
			List<VpExtra> nakedAll = NakedPocs(datastream, asOfBar);
			var nakedStarts = new HashSet<int>();
			if (nakedAll != null)
			{
				for (int i = 0; i < nakedAll.Count; i++)
				{
					VpExtra n = nakedAll[i];
					if (n != null && n.startBar >= 1) nakedStarts.Add(n.startBar);
				}
			}

			int bar = asOfBar;
			var seen = new HashSet<int>();
			int window = 0;
			int nakedCount = 0;
			int currentStart = -1;
			bool currentNaked = false;
			var rows = new List<string>();

			for (int age = 0; age < lookback && bar >= 1; age++)
			{
				VpExtra extra = ExtraVolumeProfileForBar(datastream, bar);
				if (extra == null || extra.startBar < 1) break;
				if (!seen.Add(extra.startBar)) break;
				window++;
				if (age == 0) currentStart = extra.startBar;

				bool naked = extra.IsNakedPoc(asOfBar) || nakedStarts.Contains(extra.startBar);
				if (age == 0) currentNaked = naked;
				if (!naked)
				{
					bar = extra.startBar - 1;
					continue;
				}

				rows.Add(string.Format(CultureInfo.InvariantCulture,
					"[VP-TEST]   NAKED[{0}] age={1} start={2} end={3} POC={4} pocVol={5} complete={6} touchedBar={7} current={8}",
					nakedCount, age, extra.startBar, extra.endBar,
					fmt(extra.poc), fmt(extra.pocVol), extra.pocComplete, extra.pocTouchedBar,
					age == 0));
				nakedCount++;
				bar = extra.startBar - 1;
			}

			trace(ETraceLevel.Info, string.Format(CultureInfo.InvariantCulture,
				"[VP-TEST] NAKED Data{0} bar={1} lookback={2} window={3} naked={4} streamNaked={5} currentStart={6} currentNaked={7} track={8}",
				datastream, asOfBar, lookback, window, nakedCount,
				nakedAll == null ? 0 : nakedAll.Count,
				currentStart, currentNaked, TrackNakedPoc));
			for (int i = 0; i < rows.Count; i++)
				trace(ETraceLevel.Info, rows[i]);
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
