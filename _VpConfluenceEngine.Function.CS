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
			double tick = m_owner.helperTickSize();
			if (tick <= 0) tick = FALLBACK_TICK;
			return ticks * tick;
		}
	}
}
