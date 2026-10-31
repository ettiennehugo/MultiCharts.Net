using ATCenterProxy.interop;
using PowerLanguage.VolumeProfile;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PowerLanguage.Strategy;
using static PowerLanguage.Strategy._CustomSignal;
using static PowerLanguage.Strategy._VolumeProfileBase;

namespace PowerLanguage.Function
{
	// Spec VPB-9 (analyst-base-class-spec.md): _VpConfluenceEngine moved verbatim out of _VolumeProfileBase.Strategy.CS into its own
	// helper file, per the CS-0 probe convention (_SignalJournal.Function.CS: plain class in PowerLanguage.Function, class name = file name).

	// <vpconf-rules>
	/// <summary>
	/// PR #47 (PR F scope): MC-free confluence rules, extracted by tests/VpHelperTests/gen.sh.
	/// Zone split as a width rule: a cluster of at least MinSplitCount anchors splits at its largest gap when its width
	/// exceeds max(SplitTolMultiple x tol, atrFraction x Atr20d). When Atr20d is not ready (NaN / not positive) the width
	/// limit falls back to SplitTolMultiple x tol. There is no gap > tol veto: the largest gap always splits a too-wide cluster.
	/// </summary>
	/// <summary>The engine's anchor as the rules see it.</summary>
	public interface IVpConfAnchor { string SourceVolumeId { get; } string ProfileId { get; } bool Developing { get; } bool Overlay { get; } }
	/// <summary>The engine's same-source survivor as the rules see it.</summary>
	/// <summary>Completeness: the developing weight w_complete applied to Credit (1 for completed profiles and overlays).</summary>
	public interface IVpConfSurvivor { IVpConfAnchor Anchor { get; } bool Independent { get; } double Credit { get; } double Completeness { get; } }

	/// <summary>Developing-profile weighting basis (vp-confluence-developing-weight-spec.md §3, §5). Off = w 1 (pre-spec behaviour).</summary>
	public enum EDevWeighting { Off, Time, Volume, Either }
	/// <summary>Shape of the completeness ramp r(raw).</summary>
	public enum EDevRamp { Linear, Sqrt }

	/// <summary>One developing profile's weight and how it was reached (journal / tests).</summary>
	public struct VpDevWeight
	{
		public double w;          // w_complete in [0, 1]
		public double raw;        // elapsed / expected clamped to [0, 1]; NaN when there was no reference
		public double elapsed;
		public double expected;   // median of the reference set; NaN when there was none
		public bool noRef;        // no completed reference: w = noRef weight
		public bool belowStart;   // raw < start: w = 0
	}

	public static class VpConfluenceRules
	{
		public const double SplitTolMultiple = 4.0;          // a cluster wider than 4 x tol may split (was CLUSTER_SPLIT_TOL_MULTIPLE)
		public const int MinSplitCount = 3;                  // ... and only when it holds at least this many anchors (was CLUSTER_MIN_SPLIT_COUNT)
		public const double DefaultSplitAtrFraction = 0.03;  // 3% of Atr20d (protected _VolumeProfileBase.ConfluenceSplitAtrFraction)
		private const double Eps = 1e-12;

		// Developing-profile completeness weight (spec §5 defaults, DECIDED 2026-10-30 / noRef 2026-10-31).
		public const EDevWeighting DefaultDevWeighting = EDevWeighting.Time;
		public const EDevRamp DefaultDevRamp = EDevRamp.Linear;
		public const double DefaultDevStartFraction = 0.10;
		public const double DefaultDevMinWeight = 0.10;
		public const double DefaultDevNoRefWeight = 0.50;
		public const double DefaultDevSourceThreshold = 0.67;
		public const double MaxDevSourceThreshold = 1.01;    // 1.01 = a developing profile never counts as a source

		/// <summary>Config setter check: throws ArgumentException when value is NaN or outside [lo, hi].</summary>
		public static double RequireRange(string name, double value, double lo, double hi)
		{
			if (double.IsNaN(value) || value < lo || value > hi)
				throw new ArgumentException(name + " must be between " + lo.ToString(System.Globalization.CultureInfo.InvariantCulture) + " and "
					+ hi.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
			return value;
		}

		/// <summary>Median of the values (mean of the middle two for an even count); NaN when empty. Does not reorder `values`.</summary>
		public static double Median(IList<double> values)
		{
			if (values == null || values.Count == 0) return double.NaN;
			var v = new double[values.Count];
			values.CopyTo(v, 0);
			Array.Sort(v);
			int n = v.Length;
			return (n & 1) == 1 ? v[n / 2] : 0.5 * (v[n / 2 - 1] + v[n / 2]);
		}

		/// <summary>Spec §3 for one basis: raw = elapsed / expected clamped to [0, 1]; w = 0 when raw &lt; start, else max(min, ramp(raw)).
		/// No usable reference (expected NaN / not positive) or no usable elapsed (NaN / negative) gives noRef.</summary>
		public static VpDevWeight Weigh(double elapsed, double expected, EDevRamp ramp, double start, double min, double noRef)
		{
			var r = new VpDevWeight { elapsed = elapsed, expected = expected, raw = double.NaN };
			if (double.IsNaN(expected) || double.IsInfinity(expected) || !(expected > 0) || double.IsNaN(elapsed) || elapsed < 0)
			{
				r.noRef = true;
				r.w = noRef;
				return r;
			}
			double raw = elapsed / expected;
			if (raw > 1) raw = 1;
			if (raw < 0) raw = 0;
			r.raw = raw;
			if (raw < start) { r.belowStart = true; r.w = 0; return r; }
			double shaped = ramp == EDevRamp.Sqrt ? Math.Sqrt(raw) : raw;
			r.w = shaped > min ? shaped : min;
			return r;
		}

		/// <summary>Spec §3: completeness weight of one developing profile. Off: 1. Time: bars; Volume: snapshot volume; Either: the larger of the two.</summary>
		public static VpDevWeight Weigh(EDevWeighting mode, double elapsedBars, double expectedBars, double elapsedVol, double expectedVol,
			EDevRamp ramp, double start, double min, double noRef)
		{
			switch (mode)
			{
				case EDevWeighting.Time: return Weigh(elapsedBars, expectedBars, ramp, start, min, noRef);
				case EDevWeighting.Volume: return Weigh(elapsedVol, expectedVol, ramp, start, min, noRef);
				case EDevWeighting.Either:
				{
					VpDevWeight t = Weigh(elapsedBars, expectedBars, ramp, start, min, noRef);
					VpDevWeight v = Weigh(elapsedVol, expectedVol, ramp, start, min, noRef);
					return v.w > t.w ? v : t;
				}
				default: return new VpDevWeight { w = 1, raw = 1, elapsed = elapsedBars, expected = expectedBars };
			}
		}

		/// <summary>Spec §3: a lookback profile is a reference for the developing weight when it is completed, not partial
		/// (window began before the first bar) and has a start bar.</summary>
		public static bool IsReference(bool pocComplete, bool isPartial, int startBar) { return pocComplete && !isPartial && startBar >= 1; }

		/// <summary>Spec §8 journal line for one developing profile.</summary>
		public static string DevWeightLine(int datastream, string profileId, EDevWeighting mode, VpDevWeight d, EDevRamp ramp, double start, int refN, double sourceThreshold)
		{
			var ci = System.Globalization.CultureInfo.InvariantCulture;
			string basis = d.noRef ? "noref" : mode.ToString().ToLowerInvariant();
			string text = "VPCONF devweight ds(" + datastream.ToString(ci) + ") profile(" + profileId + ") basis(" + basis + ")"
				+ " elapsed(" + d.elapsed.ToString("0.###", ci) + ") expected(" + (double.IsNaN(d.expected) ? "na" : d.expected.ToString("0.###", ci)) + ")"
				+ " raw(" + (double.IsNaN(d.raw) ? "na" : d.raw.ToString("0.000", ci)) + ") ramp(" + ramp.ToString().ToLowerInvariant() + ")"
				+ " w(" + (d.belowStart ? "0" : d.w.ToString("0.000", ci)) + ")";
			if (d.belowStart) text += " start(" + start.ToString("0.00", ci) + ")";
			return text + " refN(" + refN.ToString(ci) + ") source(" + (d.w >= sourceThreshold ? "yes" : "no") + ")";
		}

		public static bool AtrReady(double atr20d) { return !double.IsNaN(atr20d) && !double.IsInfinity(atr20d) && atr20d > 0; }

		/// <summary>Width a cluster may span before it splits.</summary>
		public static double SplitWidth(double tol, double atr20d, double atrFraction)
		{
			double w = SplitTolMultiple * tol;
			if (AtrReady(atr20d) && atrFraction > 0 && atrFraction * atr20d > w) w = atrFraction * atr20d;
			return w;
		}

		/// <summary>prices[lo..hi) ascending. Returns the first index of the upper part (split at the largest gap; the
		/// lowest such gap on a tie), or -1 when the cluster stays one zone.</summary>
		public static int SplitAt(IList<double> prices, int lo, int hi, double tol, double atr20d, double atrFraction)
		{
			if (prices == null || hi - lo < MinSplitCount) return -1;
			if (prices[hi - 1] - prices[lo] <= SplitWidth(tol, atr20d, atrFraction) + Eps) return -1;
			int split = -1;
			double gap = 0;
			for (int i = lo + 1; i < hi; i++)
			{
				double g = prices[i] - prices[i - 1];
				if (g > gap) { gap = g; split = i; }
			}
			return split > lo ? split : -1;
		}

		/// <summary>Distinct non-empty ids (independent sources = distinct volume masses, i.e. (stream, profile window)).</summary>
		public static int DistinctCount(IEnumerable<string> ids)
		{
			var set = new HashSet<string>();
			if (ids != null) foreach (var s in ids) if (!string.IsNullOrEmpty(s)) set.Add(s);
			return set.Count;
		}

		/// <summary>PR #47 review: the cluster's zones. Applies SplitAt recursively to prices[lo..hi) and appends each final zone as
		/// {start, end} to `zones` (ascending); every split taken is appended to `splits` as {lo, hi, at} when splits is not null.</summary>
		public static void Partition(IList<double> prices, int lo, int hi, double tol, double atr20d, double atrFraction, List<int[]> zones, List<int[]> splits)
		{
			if (prices == null || zones == null || hi <= lo) return;
			int at = SplitAt(prices, lo, hi, tol, atr20d, atrFraction);
			if (at < 0) { zones.Add(new[] { lo, hi }); return; }
			if (splits != null) splits.Add(new[] { lo, hi, at });
			Partition(prices, lo, at, tol, atr20d, atrFraction, zones, splits);
			Partition(prices, at, hi, tol, atr20d, atrFraction, zones, splits);
		}

		/// <summary>The volume mass an anchor belongs to: its (remapped) source id, else its own profile id.</summary>
		public static string SourceId(string sourceVolumeId, string profileId)
		{
			return string.IsNullOrEmpty(sourceVolumeId) ? profileId : sourceVolumeId;
		}

		/// <summary>V5 zone counts over the zone's survivors: independentSources = distinct SourceId of the credited
		/// independent survivors (labels of one volume mass count once); profileCount = their distinct profiles.
		/// Spec §4: a developing survivor counts (in both) only when its Completeness >= devSourceThreshold.</summary>
		public static void ZoneSourceCounts(IReadOnlyList<IVpConfSurvivor> survivors, double devSourceThreshold, out int independentSources, out int profileCount)
		{
			var src = new List<string>(); var prof = new List<string>();
			if (survivors != null)
				for (int i = 0; i < survivors.Count; i++)
				{
					IVpConfSurvivor s = survivors[i];
					if (!(s.Credit > 0) || !s.Independent) continue;
					if (s.Anchor.Developing && s.Completeness < devSourceThreshold) continue;
					src.Add(SourceId(s.Anchor.SourceVolumeId, s.Anchor.ProfileId));
					prof.Add(s.Anchor.ProfileId);
				}
			independentSources = DistinctCount(src);
			profileCount = DistinctCount(prof);
		}

		/// <summary>The cluster's zones: Partition, then emitZone(start, end) once per final range in ascending order, after
		/// onSplit(lo, hi, at) for every split taken (journal). Returns the number of zones emitted.</summary>
		public static int EmitZones(IList<double> prices, int lo, int hi, double tol, Func<double> hostAtr20d, double atrFraction,
			Action<int, int> emitZone, Action<int, int, int, double> onSplit)
		{
			double atr20d = hostAtr20d != null ? hostAtr20d() : double.NaN;   // the host's Atr20d, read once per cluster
			var zones = new List<int[]>(); var splits = new List<int[]>();
			Partition(prices, lo, hi, tol, atr20d, atrFraction, zones, splits);
			if (onSplit != null) foreach (var sp in splits) onSplit(sp[0], sp[1], sp[2], atr20d);
			foreach (var z in zones) emitZone(z[0], z[1]);
			return zones.Count;
		}

		/// <summary>Developing confluence for cluster[lo..hi): every developing, non-overlay anchor, in order, becomes
		/// makeHit(anchor, its survivor or null, credit), where credit is the survivor's credit when > 0, else 0 (absorbed into
		/// its parent's source or refused). Hits go to `hits` (allocated on the first one; stays null when none).
		/// Returns developingScore (sum of credits). Credit is already completeness-weighted (spec §4), so the score is the weighted sum;
		/// a label weighted to 0 (raw &lt; start) is still listed, with credit 0.</summary>
		public static double DevelopingHits<THit>(IReadOnlyList<IVpConfAnchor> cluster, int lo, int hi, IReadOnlyList<IVpConfSurvivor> survivors,
			Func<IVpConfAnchor, IVpConfSurvivor, double, THit> makeHit, ref List<THit> hits)
		{
			double score = 0;
			for (int i = lo; i < hi; i++)
			{
				IVpConfAnchor a = cluster[i];
				if (!a.Developing || a.Overlay) continue;
				IVpConfSurvivor own = null;
				if (survivors != null)
					for (int k = 0; k < survivors.Count; k++)
						if (object.ReferenceEquals(survivors[k].Anchor, a)) { own = survivors[k]; break; }
				double c = own != null && own.Credit > 0 ? own.Credit : 0;
				if (hits == null) hits = new List<THit>();
				hits.Add(makeHit(a, c > 0 ? own : null, c));
				score += c;
			}
			return score;
		}

		/// <summary>VP hosts build M-DAILY (Atr20d) only when the strategy declares it uses confluence zones
		/// (UsesConfluenceZones), the split uses ATR, and at least one confluence slot is enabled.</summary>
		public static bool NeedsAtr20d(bool usesConfluenceZones, double atrFraction, int ds1, int ds2, int ds3)
		{
			return usesConfluenceZones && atrFraction > 0 && (ds1 >= 1 || ds2 >= 1 || ds3 >= 1);
		}

		/// <summary>The host's Atr20d for the split: dailyAtr() when M-DAILY is on (needsDaily), else NaN (4 x tol fallback).</summary>
		public static double HostAtr20d(bool needsDaily, Func<double> dailyAtr)
		{
			return needsDaily && dailyAtr != null ? dailyAtr() : double.NaN;
		}

		/// <summary>StartCalc: throws ArgumentException(DuplicateStreamError) for duplicate enabled confluence streams.</summary>
		public static void ValidateStreams(int ds1, int ds2, int ds3)
		{
			string e = DuplicateStreamError(ds1, ds2, ds3);
			if (e != null) throw new ArgumentException(e);
		}

		/// <summary>StartCalc check: every enabled confluence slot (stream >= 1) needs its own stream. Returns the error or null.</summary>
		public static string DuplicateStreamError(int ds1, int ds2, int ds3)
		{
			int[] ds = { ds1, ds2, ds3 };
			for (int j = 1; j < 3; j++)
				for (int i = 0; i < j; i++)
					if (ds[j] >= 1 && ds[j] == ds[i])
						return "ConfluenceDataStream" + (j + 1) + " = ConfluenceDataStream" + (i + 1) + " (" + ds[j] + "): each confluence slot needs its own stream";
			return null;
		}
	}
	// </vpconf-rules>

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
	internal sealed class _VpConfluenceEngine
	{
		private sealed class Anchor : IVpConfAnchor
		{
			string IVpConfAnchor.SourceVolumeId { get { return sourceVolumeId; } }
			string IVpConfAnchor.ProfileId { get { return profileId; } }
			bool IVpConfAnchor.Developing { get { return developing; } }
			bool IVpConfAnchor.Overlay { get { return overlay; } }
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
			public double completeness = 1;   // spec §3 w_complete; 1 for completed profiles, overlays and when weighting is Off
		}

		private sealed class Survivor : IVpConfSurvivor
		{
			IVpConfAnchor IVpConfSurvivor.Anchor { get { return anchor; } }
			bool IVpConfSurvivor.Independent { get { return independent; } }
			double IVpConfSurvivor.Credit { get { return credit; } }
			double IVpConfSurvivor.Completeness { get { return anchor.completeness; } }
			public Anchor anchor;
			public double baseCredit;
			public double credit;
			public bool independent;
			public string absorbed = "";
			public string parentSource;
			// Credit after applyNested and before the completeness weight (set only when the weight is applied). Not baseCredit:
			// baseCredit is the makeSurvivor credit before nesting, so for a refined child it is not the REFINEMENT x parent value
			// that the weight scales. The spec's "(= baseCredit)" means this unweighted value.
			public double rawCredit = double.NaN;
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
		private readonly List<double> m_prices = new List<double>();   // cluster prices for VpConfluenceRules.EmitZones
		// Spec §6: developing weight scratch, allocated on the first stream with a developing profile while weighting is on.
		private List<double> m_refBars;
		private List<double> m_refVols;
		private EDevWeighting m_devMode;
		private double m_devSourceThreshold;

		public _VpConfluenceEngine(_VolumeProfileBase owner)
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
			m_devMode = m_owner.ConfluenceDevWeighting;
			// Off keeps the pre-spec source count (every credited developing survivor counts); completeness is 1 there anyway.
			m_devSourceThreshold = m_devMode == EDevWeighting.Off ? 0 : m_owner.ConfluenceDevSourceThreshold;

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
		// PR #47: width rule (VpConfluenceRules.SplitAt). The anchors are sorted ascending, so m_prices mirrors the cluster.
		private void emitClusters(List<Anchor> cluster, int lo, int hi, double tol)
		{
			if (cluster == null || hi <= lo) return;
			if (lo == 0 && hi == cluster.Count)
			{
				m_prices.Clear();
				for (int i = 0; i < cluster.Count; i++) m_prices.Add(cluster[i].price);
			}
			double atrFraction = m_owner.ConfluenceSplitAtrFraction;
			VpConfluenceRules.EmitZones(m_prices, lo, hi, tol, m_owner.confluenceAtr20d, atrFraction, (zlo, zhi) => addZone(cluster, zlo, zhi, tol), (a, b, split, atr) => journalSplit(a, b, split, tol, atrFraction, atr));
		}

		private void journalSplit(int a, int b, int split, double tol, double atrFraction, double atr20d)
		{
			m_owner.confluenceJournal(() => string.Format(CultureInfo.InvariantCulture,
				"VPCONF split anchors({0}) range({1}-{2}) width({3}) limit({4}) at({5}) gap({6}) atr20d({7}) basis({8})",
				b - a, m_prices[a], m_prices[b - 1], m_prices[b - 1] - m_prices[a], VpConfluenceRules.SplitWidth(tol, atr20d, atrFraction),
				m_prices[split], m_prices[split] - m_prices[split - 1], atr20d,
				VpConfluenceRules.AtrReady(atr20d) ? "atr" : "tol-fallback"));
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
			bool weigh = m_devMode != EDevWeighting.Off && hasDeveloping(extras);
			double refBars = double.NaN, refVols = double.NaN;
			int refN = 0;
			if (weigh) referenceMedians(extras, current, out refBars, out refVols, out refN);
			for (int age = 0; age < extras.Count; age++)
			{
				VpExtra extra = extras[age];
				if (extra == null) continue;
				int start = extra.startBar >= 1 ? extra.startBar : current;
				int before = anchors.Count;
				addProfile(anchors, extra, datastream, start, age, rank, rankWeight, current);
				if (weigh && !extra.pocComplete) weighDeveloping(anchors, before, extra, datastream, start, current, refBars, refVols, refN);
			}
		}

		private static bool hasDeveloping(List<VpExtra> extras)
		{
			for (int i = 0; i < extras.Count; i++)
				if (extras[i] != null && !extras[i].pocComplete) return true;
			return false;
		}

		private static int elapsedBars(VpExtra extra, int startBar, int current)
		{
			// Same window clip as addProfile.
			int endBar = extra.endBar >= startBar ? extra.endBar : startBar;
			if (endBar > current) endBar = current;
			return endBar - startBar + 1;
		}

		// Spec §3: the reference set is the completed, non-partial lookback profiles on this stream (same window type).
		private void referenceMedians(List<VpExtra> extras, int current, out double medBars, out double medVols, out int refN)
		{
			if (m_refBars == null) { m_refBars = new List<double>(8); m_refVols = new List<double>(8); }
			m_refBars.Clear();
			m_refVols.Clear();
			bool vol = m_devMode == EDevWeighting.Volume || m_devMode == EDevWeighting.Either;
			double tick = vol ? m_owner.helperTickSize() : 0;
			for (int i = 0; i < extras.Count; i++)
			{
				VpExtra e = extras[i];
				if (e == null || !VpConfluenceRules.IsReference(e.pocComplete, e.isPartial, e.startBar)) continue;
				m_refBars.Add(elapsedBars(e, e.startBar, current));
				if (vol)
				{
					double total = snapFor(e, tick).total;
					if (total > 0) m_refVols.Add(total);   // levels not read: no volume reference from this profile
				}
			}
			refN = m_refBars.Count;
			medBars = VpConfluenceRules.Median(m_refBars);
			medVols = VpConfluenceRules.Median(m_refVols);
		}

		// Sets completeness on the developing profile's anchors (indices [from, anchors.Count)) and journals one line.
		private void weighDeveloping(List<Anchor> anchors, int from, VpExtra extra, int datastream, int startBar, int current, double refBars, double refVols, int refN)
		{
			double elapsedVol = double.NaN;
			if (m_devMode == EDevWeighting.Volume || m_devMode == EDevWeighting.Either)
			{
				double total = snapFor(extra, m_owner.helperTickSize()).total;
				if (total > 0) elapsedVol = total;
			}
			VpDevWeight d = VpConfluenceRules.Weigh(m_devMode, elapsedBars(extra, startBar, current), refBars, elapsedVol, refVols,
				m_owner.ConfluenceDevRamp, m_owner.ConfluenceDevStartFraction, m_owner.ConfluenceDevMinWeight, m_owner.ConfluenceDevNoRefWeight);
			for (int i = from; i < anchors.Count; i++)
				if (anchors[i].developing && !anchors[i].overlay) anchors[i].completeness = d.w;
			m_owner.confluenceJournal(() => VpConfluenceRules.DevWeightLine(datastream, "ds" + datastream.ToString() + ":s" + startBar.ToString(), m_devMode, d,
				m_owner.ConfluenceDevRamp, m_owner.ConfluenceDevStartFraction, refN, m_devSourceThreshold));
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
			double tick = m_owner.helperTickSize();
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
			int cap = order.Count < CONFLUENCE_NODES_PER_PROFILE ? order.Count : CONFLUENCE_NODES_PER_PROFILE;
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

		private void addVwap(List<Anchor> anchors, _VpVwapEngine eng, EConfluenceKind kind)
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
			// Spec §3 / §7.4: the developing weight, applied once, after nesting (it also scales a developing child's REFINEMENT bonus).
			if (m_devMode != EDevWeighting.Off)
				for (int i = 0; i < survivors.Count; i++)
				{
					Survivor s = survivors[i];
					if (!s.anchor.developing || s.anchor.overlay) continue;
					s.rawCredit = s.credit;
					s.credit *= s.anchor.completeness;
				}

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

				if (s.independent) streams.Add(s.anchor.dataStream);
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
			int nSources, nProfiles;
			VpConfluenceRules.ZoneSourceCounts(survivors, m_devSourceThreshold, out nSources, out nProfiles);
			zone.independentSources = nSources;   // V5: distinct sources, not survivors
			zone.profileCount = nProfiles;
			zone.developingScore = VpConfluenceRules.DevelopingHits(cluster, loIdx, hiIdx, survivors, developingHit, ref zone.developingHits);
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

		// PR #47: VpConfluenceRules.DevelopingHits builder. The developing profile's confluence so far, kept apart from the scored hits. Every developing-profile anchor
		// in the zone is listed (tagged developing). One that survived carries its credit; one absorbed into its completed
		// parent's source (assignDevelopingSources) or refused carries credit 0. developingScore sums the credited ones.
		private static ConfluenceHit developingHit(IVpConfAnchor a, IVpConfSurvivor own, double credit)
		{
			if (own != null) return toHit((Survivor)own);
			ConfluenceHit h = toHit(new Survivor { anchor = (Anchor)a, absorbed = "" });
			h.weight = 0; h.credit = 0; h.independent = false;
			return h;
		}

		private List<Survivor> collapseSameSource(List<Anchor> cluster, int lo, int hi)
		{
			var groups = new Dictionary<string, List<Anchor>>();
			for (int i = lo; i < hi; i++)
			{
				Anchor a = cluster[i];
				string id = VpConfluenceRules.SourceId(a.sourceVolumeId, a.profileId);
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
			// PR #47: StartCalc rejects duplicate enabled streams, so every anchor's stream maps to exactly one slot weight
			// (collectStream / addVwap skip weight <= 0); the old 1 + 0.25 x rank fallback is gone.
			double weight = rankWeightFor(a.dataStream);
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
				// VPB-14: a refined child adds DEFAULT_CONFLUENCE_REFINEMENT x parent credit, at most once per parent (claimed). The old
				// 1.25 x parent cap equalled that bonus exactly (credits are >= 0), so it was removed (educator review V9 / 6d).
				child.credit = DEFAULT_CONFLUENCE_REFINEMENT * parent.credit;
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
			for (int i = 0; i < survivors.Count; i++)
			{
				Survivor s = survivors[i];
				if (s.credit <= 0 || s.anchor.overlay || isRejection(s.anchor.kind)) continue;
				if (s.anchor.kind == EConfluenceKind.NakedPoc) naked = true;
			}
			// acc > 0 && rej > 0 (Mixed) is decided by the only caller before pickRole runs.
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
				independent = s.independent,
				developing = s.anchor.developing,
				completeness = s.anchor.completeness,
				rawCredit = double.IsNaN(s.rawCredit) ? s.credit : s.rawCredit
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
			double tick = m_owner.helperTickSize();
			if (tick <= 0) tick = FALLBACK_TICK;
			return ticks * tick;
		}
	}
}
