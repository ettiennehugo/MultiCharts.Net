/*
  VP_LevelStatistics — MultiCharts .NET indicator
  ================================================
  Empirical probabilities around a volume-profile reference:
    occupancy of VA / above VAH / below VAL
    VAH & VAL tests, failed auctions, acceptance
    POC tags and crosses
    HVN / LVN tags
    excursion size above VAH and below VAL
    forward paths after those events (return-to-value, opposite-edge, continuation)
    rotation after a new profile starts (open vs the prior VP)

  Chart
  -----
  Format Instrument → Volume Profile → Enable, and confirm the profiles are
  actually drawn (the .NET collection is empty when Visible is false).
  ProfileRef = PriorVP (default) uses the previous completed profile in the
  MultiCharts collection — whatever VP type you built (session, every-N-bars,
  week, etc.). No calendar date is involved. If the chart only has one live
  profile (a single composite), the study falls back to that profile and says
  so in the Output bar. Developing profiles contaminate the sample.

  MultiCharts often builds the collection AFTER the first study pass. This
  study subscribes to VolumeProfile.EChanged and recalculates when the
  profiles finish loading. If the first Output dump is empty, wait for the
  recalc or press Recalculate.

  If VolumeProfileByDataStream / VolumeProfileOfDataStream is needed for Data2,
  this study reads Data1 only. Pair it with VP_TwoTarget_Levels if you also want
  the secondary profile drawn.

  Output is written to the PowerLanguage Output bar on the last chart bar.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PowerLanguage.VolumeProfile;

namespace PowerLanguage.Indicator
{
	public enum EVpRefMode
	{
		PriorVP = 0,        // previous completed profile in the collection
		PriorBar = 1,       // profile as of the previous bar
		Developing = 2      // current bar's profile (look-ahead — research only)
	}

	public enum EVpSide
	{
		Both,
		AboveVAH,
		BelowVAL
	}

	[SameAsSymbol(true)]
	public class _VolumeProfileStatistics : IndicatorObject
	{
		// ---------- inputs -------------------------------------------------
		[Input] public EVpRefMode ProfileRef { get; set; }
		[Input] public int ForwardLookback { get; set; }
		[Input] public double HvnRelThreshold { get; set; }   // vol >= this × POC vol
		[Input] public double LvnRelThreshold { get; set; }   // vol <= this × POC vol + local min
		[Input] public int NodeTickTolerance { get; set; }    // ticks around HVN/LVN to count a tag
		[Input] public double BucketStepPct { get; set; }     // histogram step for price-% moves
		[Input] public double BucketMaxPct { get; set; }
		[Input] public EVpSide SideFilter { get; set; }
		[Input] public bool PlotReference { get; set; }
		[Input] public bool LogToOutput { get; set; }
		[Input] public int MinEvents { get; set; }
		[Input] public double VaPercent { get; set; }          // used only if MC does not supply VAH/VAL

		// ---------- plots --------------------------------------------------
		private IPlotObject m_poc, m_vah, m_val;

		// ---------- reference freeze --------------------------------------
		private VpSnap m_frozen;
		private IProfile m_lastLive;
		private bool m_newPeriod;           // live profile object changed this bar

		// ---------- bar history (index 0 = current bar) --------------------
		private struct OHLCV
		{
			public DateTime time;
			public double open, high, low, close, volume;
			public OHLCV(DateTime t, double o, double h, double l, double c, double v)
			{
				time = t; open = o; high = h; low = l; close = c; volume = v;
			}
		}
		private List<OHLCV> m_bars;

		private struct EventHit
		{
			public int barNum;
			public double level;
			public double poc, vah, val;
			public double vaWidth;
			public double excursionPct;     // vs event-bar close
			public double excursionVa;      // vs VA width
		}

		// occupancy
		private int m_barsUsed;
		private int m_closeInVA, m_closeAbove, m_closeBelow;
		private int m_highAboveVAH, m_lowBelowVAL;
		private readonly List<double> m_vaWidthPct = new List<double>();
		private readonly List<double> m_closeToPocVa = new List<double>(); // signed, + above POC

		// events
		private readonly List<EventHit> m_vahFail = new List<EventHit>();
		private readonly List<EventHit> m_vahAccept = new List<EventHit>();
		private readonly List<EventHit> m_vahTest = new List<EventHit>();
		private readonly List<EventHit> m_valFail = new List<EventHit>();
		private readonly List<EventHit> m_valAccept = new List<EventHit>();
		private readonly List<EventHit> m_valTest = new List<EventHit>();
		private readonly List<EventHit> m_pocTagUp = new List<EventHit>();   // approached from below
		private readonly List<EventHit> m_pocTagDn = new List<EventHit>();
		private readonly List<EventHit> m_pocCrossUp = new List<EventHit>();
		private readonly List<EventHit> m_pocCrossDn = new List<EventHit>();
		private readonly List<EventHit> m_hvnTag = new List<EventHit>();
		private readonly List<EventHit> m_lvnTag = new List<EventHit>();
		private readonly List<EventHit> m_excAbove = new List<EventHit>();
		private readonly List<EventHit> m_excBelow = new List<EventHit>();

		// rotation at each new profile boundary (first bar of a new VP vs PriorVP)
		private int m_sessDir;              // +1 opened above VAH, -1 below VAL, 0 inside
		private bool m_sessReentered;
		private bool m_sessOpposite;
		private int m_sessOpenOut, m_sessReenter, m_sessOppHit;
		private bool m_periodOpen;

		// diagnostics
		private int m_dbgBars, m_dbgOk, m_dbgPackNull, m_dbgVpNull, m_dbgEmpty, m_dbgNoVa, m_dbgEx;
		private int m_dbgItemCount, m_dbgFallback;
		private bool m_dbgVisible;
		private string m_dbgLastEx = "";
		private string m_dbgHow = "";
		private bool m_printed;

		public _VolumeProfileStatistics(object ctx) : base(ctx)
		{
			ProfileRef = EVpRefMode.PriorVP;
			ForwardLookback = 21;
			HvnRelThreshold = 0.60;
			LvnRelThreshold = 0.25;
			NodeTickTolerance = 2;
			BucketStepPct = 0.50;
			BucketMaxPct = 8.00;
			SideFilter = EVpSide.Both;
			PlotReference = true;
			LogToOutput = true;
			MinEvents = 3;
			VaPercent = 0.70;
		}

		protected override void Create()
		{
			m_poc = AddPlot(new PlotAttributes("Ref POC", EPlotShapes.Line, Color.Orange, Color.Black, 2, 0, true));
			m_vah = AddPlot(new PlotAttributes("Ref VAH", EPlotShapes.Line, Color.LimeGreen, Color.Black, 1, 0, true));
			m_val = AddPlot(new PlotAttributes("Ref VAL", EPlotShapes.Line, Color.IndianRed, Color.Black, 1, 0, true));
			m_bars = new List<OHLCV>();
		}

		protected override void StartCalc()
		{
			m_frozen = new VpSnap();
			m_lastLive = null;
			m_newPeriod = false;
			m_bars.Clear();
			m_barsUsed = 0;
			m_closeInVA = m_closeAbove = m_closeBelow = 0;
			m_highAboveVAH = m_lowBelowVAL = 0;
			m_vaWidthPct.Clear();
			m_closeToPocVa.Clear();
			m_vahFail.Clear(); m_vahAccept.Clear(); m_vahTest.Clear();
			m_valFail.Clear(); m_valAccept.Clear(); m_valTest.Clear();
			m_pocTagUp.Clear(); m_pocTagDn.Clear();
			m_pocCrossUp.Clear(); m_pocCrossDn.Clear();
			m_hvnTag.Clear(); m_lvnTag.Clear();
			m_excAbove.Clear(); m_excBelow.Clear();
			m_sessDir = 0;
			m_sessReentered = m_sessOpposite = false;
			m_sessOpenOut = m_sessReenter = m_sessOppHit = 0;
			m_periodOpen = false;
			m_dbgBars = m_dbgOk = m_dbgPackNull = m_dbgVpNull = m_dbgEmpty = m_dbgNoVa = m_dbgEx = 0;
			m_dbgItemCount = m_dbgFallback = 0;
			m_dbgVisible = false;
			m_dbgLastEx = "";
			m_dbgHow = "";
			m_printed = false;
			HookProfileEvents();
		}

		protected override void CalcBar()
		{
			if (Environment.CalcReason != CalculationReason.Default) return;

			m_bars.Insert(0, new OHLCV(
					Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]));

			VpSnap refer;
			if (!ResolveReference(out refer) || !refer.Valid)
			{
				if (Bars.LastBarOnChart && LogToOutput) PrintAll();
				return;
			}

			if (PlotReference)
			{
				if (refer.POC > 0) m_poc.Set(refer.POC);
				if (refer.VAH > 0) m_vah.Set(refer.VAH);
				if (refer.VAL > 0) m_val.Set(refer.VAL);
			}

			if (Bars.Status != EBarState.Close && !Bars.LastBarOnChart)
				return;

			ClassifyBar(refer);

			if (Bars.LastBarOnChart && LogToOutput)
				PrintAll();
		}

		// =====================================================================
		// Reference profile
		// =====================================================================
		private struct VpSnap
		{
			public bool Valid;
			public double POC, VAH, VAL;
			public double VaWidth { get { return (VAH > VAL && VAL > 0) ? (VAH - VAL) : 0; } }
			public List<double> Hvns;
			public List<double> Lvns;
		}

		private bool ResolveReference(out VpSnap snap)
		{
			snap = new VpSnap();
			m_dbgBars++;

			int abs0 = BarIndex0();
			int abs1 = BarIndex1();

			if (ProfileRef == EVpRefMode.Developing)
				return ReadBest(abs0, abs1, out snap, false);

			if (ProfileRef == EVpRefMode.PriorBar)
				return ReadBest(abs1, abs0, out snap, false);

			// PriorVP: previous completed profile in the collection. No dates.
			return ResolvePriorVp(abs0, abs1, out snap);
		}

		private bool ResolvePriorVp(int abs0, int abs1, out VpSnap snap)
		{
			snap = new VpSnap();
			m_newPeriod = false;

			IProfilesCollection pack = GetPack();
			if (pack == null)
			{
				m_dbgPackNull++;
				return false;
			}

			IProfile live = SafeItem(pack, abs0) ?? SafeItem(pack, Bars.CurrentBar);
			IProfile priorP = FindOtherProfile(pack, live);

			if (m_lastLive != null && live != null && !object.ReferenceEquals(m_lastLive, live))
				m_newPeriod = true;

			if (priorP != null && FillSnap(priorP, abs1, abs0, out snap) && snap.Valid)
			{
				m_frozen = snap;
				m_lastLive = live;
				m_dbgHow = "PriorVP from collection";
				m_dbgOk++;
				try { if (pack.Items != null) m_dbgItemCount = pack.Items.Count; } catch { }
				try { m_dbgVisible = pack.Visible; } catch { }
				return true;
			}

			// Single live profile on the chart (composite / last-only).
			if (ReadBest(abs0, abs1, out snap, false) && snap.Valid)
			{
				bool sameFreeze = m_frozen.Valid
						&& m_frozen.POC == snap.POC && m_frozen.VAH == snap.VAH && m_frozen.VAL == snap.VAL;
				if (!sameFreeze && m_frozen.Valid)
					m_newPeriod = true;
				m_frozen = snap;
				m_lastLive = live;
				m_dbgFallback++;
				m_dbgHow = "fallback-live-profile";
				return true;
			}

			m_lastLive = live;
			return false;
		}

		private int BarIndex0()
		{
			// Wiki sample uses FullSymbolData.Current - 1
			int a = Bars.FullSymbolData.Current - 1;
			return a < 0 ? 0 : a;
		}

		private int BarIndex1()
		{
			int a = Bars.FullSymbolData.Current - 2;
			if (a < 0) a = BarIndex0();
			return a;
		}

		private IProfilesCollection GetPack()
		{
			try
			{
				if (this.VolumeProfile != null)
					return this.VolumeProfile;
			}
			catch (Exception ex)
			{
				m_dbgLastEx = "VolumeProfile: " + ex.Message;
			}
			try
			{
				return VolumeProfileByDataStream(1);
			}
			catch (Exception ex)
			{
				m_dbgLastEx = "VolumeProfileByDataStream: " + ex.Message;
				return null;
			}
		}

		private void HookProfileEvents()
		{
			try
			{
				IProfilesCollection pack = GetPack();
				if (pack == null) return;
				pack.EChanged -= OnProfilesChanged;
				pack.EChanged += OnProfilesChanged;
			}
			catch (Exception ex)
			{
				m_dbgLastEx = "Hook: " + ex.Message;
			}
		}

		private void OnProfilesChanged(bool full)
		{
			if (!full) return;
			try { this.ExecControl.Recalculate(); }
			catch { }
		}

		private bool ReadBest(int firstBar, int secondBar, out VpSnap snap, bool preferOtherThanLive)
		{
			snap = new VpSnap();
			IProfilesCollection pack = GetPack();
			if (pack == null)
			{
				m_dbgPackNull++;
				return false;
			}
			try { m_dbgVisible = pack.Visible; }
			catch { }

			IProfile live = SafeItem(pack, firstBar) ?? SafeItem(pack, Bars.CurrentBar);
			IProfile prev = SafeItem(pack, secondBar);

			IProfile chosen = null;
			string how = "";

			if (preferOtherThanLive)
			{
				if (prev != null && !prev.Empty && !SameProfile(prev, live))
				{
					chosen = prev;
					how = "ItemForBar(prev)";
				}
				else
				{
					IProfile older = FindOtherProfile(pack, live);
					if (older != null)
					{
						chosen = older;
						how = "Items[] previous profile";
					}
				}
			}

			if (chosen == null)
			{
				if (prev != null && !prev.Empty)
				{
					chosen = prev;
					how = "ItemForBar(prev)";
				}
				else if (live != null && !live.Empty)
				{
					chosen = live;
					how = "ItemForBar(current)";
				}
				else
				{
					chosen = FirstNonEmpty(pack);
					how = "Items[] first non-empty";
				}
			}

			try
			{
				if (pack.Items != null) m_dbgItemCount = pack.Items.Count;
			}
			catch { }

			if (chosen == null)
			{
				m_dbgVpNull++;
				return false;
			}
			if (chosen.Empty)
			{
				m_dbgEmpty++;
				return false;
			}

			if (!FillSnap(chosen, firstBar, secondBar, out snap))
			{
				m_dbgNoVa++;
				return false;
			}
			m_dbgOk++;
			if (string.IsNullOrEmpty(m_dbgHow)) m_dbgHow = how;
			return true;
		}

		private static bool SameProfile(IProfile a, IProfile b)
		{
			if (a == null || b == null) return false;
			return object.ReferenceEquals(a, b);
		}

		private IProfile SafeItem(IProfilesCollection pack, int bar)
		{
			if (pack == null || bar < 0) return null;
			try { return pack.ItemForBar(bar); }
			catch (Exception ex)
			{
				m_dbgEx++;
				m_dbgLastEx = "ItemForBar(" + bar + "): " + ex.Message;
				return null;
			}
		}

		private IProfile FindOtherProfile(IProfilesCollection pack, IProfile live)
		{
			if (pack == null || pack.Items == null) return null;
			IProfile last = null;
			try
			{
				foreach (IProfile p in pack.Items)
				{
					if (p == null || p.Empty) continue;
					if (SameProfile(p, live)) continue;
					last = p;
				}
			}
			catch (Exception ex)
			{
				m_dbgLastEx = "Items: " + ex.Message;
			}
			return last;
		}

		private IProfile FirstNonEmpty(IProfilesCollection pack)
		{
			if (pack == null || pack.Items == null) return null;
			try
			{
				foreach (IProfile p in pack.Items)
				{
					if (p != null && !p.Empty) return p;
				}
			}
			catch { }
			return null;
		}

		private bool FillSnap(IProfile vp, int barA, int barB, out VpSnap snap)
		{
			snap = new VpSnap { Hvns = new List<double>(), Lvns = new List<double>() };
			try
			{
				snap.POC = FirstPrice(
						PriceAt(vp, barA, 0), PriceAt(vp, barB, 0),
						PriceAt(vp, Bars.CurrentBar, 0), PriceAt(vp, Bars.CurrentBar - 1, 0),
						LevelPrice(vp.POC));
				snap.VAH = FirstPrice(
						PriceAt(vp, barA, 1), PriceAt(vp, barB, 1),
						PriceAt(vp, Bars.CurrentBar, 1), VaExtreme(vp, true));
				snap.VAL = FirstPrice(
						PriceAt(vp, barA, 2), PriceAt(vp, barB, 2),
						PriceAt(vp, Bars.CurrentBar, 2), VaExtreme(vp, false));

				if (!(snap.VAH > snap.VAL && snap.VAL > 0))
					SynthesizeVa(vp, ref snap);

				snap.Valid = snap.VAH > snap.VAL && snap.VAL > 0;
				if (snap.Valid) ExtractNodes(vp, ref snap);
				return snap.Valid;
			}
			catch (Exception ex)
			{
				m_dbgEx++;
				m_dbgLastEx = "FillSnap: " + ex.Message;
				return false;
			}
		}

		// kind: 0 POC, 1 VAH, 2 VAL
		private static double PriceAt(IProfile vp, int bar, int kind)
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

		private static double LevelPrice(ILevel lvl)
		{
			try
			{
				if (lvl != null && lvl.Price != null) return lvl.Price.Dbl;
			}
			catch { }
			return 0;
		}

		private static double VaExtreme(IProfile vp, bool high)
		{
			if (vp == null || vp.VA == null) return 0;
			double ext = 0;
			bool any = false;
			try
			{
				foreach (ILevel lvl in vp.VA)
				{
					double px = LevelPrice(lvl);
					if (px <= 0) continue;
					if (!any) { ext = px; any = true; }
					else ext = high ? Math.Max(ext, px) : Math.Min(ext, px);
				}
			}
			catch { }
			return ext;
		}

		private static double FirstPrice(params double[] xs)
		{
			if (xs == null) return 0;
			for (int i = 0; i < xs.Length; i++)
				if (xs[i] > 0) return xs[i];
			return 0;
		}

		private void SynthesizeVa(IProfile vp, ref VpSnap snap)
		{
			if (vp == null || vp.Values == null) return;
			var rows = new List<KeyValuePair<double, double>>();
			double total = 0;
			try
			{
				foreach (ILevel lvl in vp.Values)
				{
					if (lvl == null || lvl.Price == null) continue;
					double vol = LevelVol(lvl);
					if (vol <= 0) continue;
					rows.Add(new KeyValuePair<double, double>(lvl.Price.Dbl, vol));
					total += vol;
				}
			}
			catch { return; }
			if (rows.Count < 3 || total <= 0) return;
			rows.Sort((x, y) => x.Key.CompareTo(y.Key));

			int poc = 0;
			for (int i = 1; i < rows.Count; i++)
				if (rows[i].Value > rows[poc].Value) poc = i;
			if (snap.POC <= 0) snap.POC = rows[poc].Key;

			double need = total * Math.Max(0.50, Math.Min(0.95, VaPercent));
			double acc = rows[poc].Value;
			int lo = poc, hi = poc;
			while (acc < need && (lo > 0 || hi < rows.Count - 1))
			{
				double below = lo > 0 ? rows[lo - 1].Value : -1;
				double above = hi < rows.Count - 1 ? rows[hi + 1].Value : -1;
				if (above >= below)
				{
					hi++;
					acc += rows[hi].Value;
				}
				else
				{
					lo--;
					acc += rows[lo].Value;
				}
			}
			snap.VAL = rows[lo].Key;
			snap.VAH = rows[hi].Key;
		}

		private void ExtractNodes(IProfile vp, ref VpSnap snap)
		{
			snap.Hvns = new List<double>();
			snap.Lvns = new List<double>();
			if (vp == null || vp.Values == null) return;

			var rows = new List<KeyValuePair<double, double>>();
			double pocVol = 0;
			if (vp.POC != null) pocVol = LevelVol(vp.POC);

			foreach (ILevel lvl in vp.Values)
			{
				if (lvl == null || lvl.Price == null) continue;
				double vol = LevelVol(lvl);
				rows.Add(new KeyValuePair<double, double>(lvl.Price.Dbl, vol));
				if (vol > pocVol) pocVol = vol;
			}
			if (rows.Count < 3 || pocVol <= 0) return;
			rows.Sort((x, y) => x.Key.CompareTo(y.Key));

			double hvnFloor = pocVol * HvnRelThreshold;
			double lvnCeil = pocVol * LvnRelThreshold;

			for (int i = 0; i < rows.Count; i++)
			{
				double px = rows[i].Key;
				double vol = rows[i].Value;
				if (vol >= hvnFloor)
					snap.Hvns.Add(px);

				bool localMin = true;
				if (i > 0 && vol > rows[i - 1].Value) localMin = false;
				if (i < rows.Count - 1 && vol > rows[i + 1].Value) localMin = false;
				if (localMin && vol <= lvnCeil && vol < pocVol)
					snap.Lvns.Add(px);
			}
		}

		private static double LevelVol(ILevel lvl)
		{
			if (lvl == null) return 0;
			return (double)(lvl.AskTradedValue + lvl.BidTradedValue);
		}

		// =====================================================================
		// Classification
		// =====================================================================
		private void ClassifyBar(VpSnap vp)
		{
			double o = Bars.Open[0], h = Bars.High[0], l = Bars.Low[0], c = Bars.Close[0];
			if (vp.VAH <= 0 || vp.VAL <= 0 || vp.VAH <= vp.VAL) return;
			if (c <= 0) return;

			bool wantAbove = SideFilter != EVpSide.BelowVAL;
			bool wantBelow = SideFilter != EVpSide.AboveVAH;

			m_barsUsed++;
			double vaW = vp.VaWidth;
			if (c > vp.VAH) m_closeAbove++;
			else if (c < vp.VAL) m_closeBelow++;
			else m_closeInVA++;

			if (h > vp.VAH) m_highAboveVAH++;
			if (l < vp.VAL) m_lowBelowVAL++;

			if (Bars.Close[0] > 0)
				m_vaWidthPct.Add(vaW / Bars.Close[0] * 100.0);
			if (vp.POC > 0 && vaW > 0)
				m_closeToPocVa.Add((c - vp.POC) / vaW);

			bool prevOk = Bars.CurrentBar > 1;
			double pc = prevOk ? Bars.Close[1] : c;
			bool prevIn = pc >= vp.VAL && pc <= vp.VAH;
			bool prevAbove = pc > vp.VAH;
			bool prevBelow = pc < vp.VAL;
			bool thisIn = c >= vp.VAL && c <= vp.VAH;

			// ----- VAH -----
			if (wantAbove && prevIn && h >= vp.VAH)
			{
				double extPct = (h - vp.VAH) / c * 100.0;
				double extVa = vaW > 0 ? (h - vp.VAH) / vaW : 0;
				var ev = Hit(vp, vp.VAH, extPct, extVa);
				m_vahTest.Add(ev);
				if (c <= vp.VAH) m_vahFail.Add(ev);
			}
			if (wantAbove && (prevIn || prevBelow) && c > vp.VAH)
			{
				double extPct = (c - vp.VAH) / c * 100.0;
				double extVa = vaW > 0 ? (c - vp.VAH) / vaW : 0;
				m_vahAccept.Add(Hit(vp, vp.VAH, extPct, extVa));
			}
			if (wantAbove && h > vp.VAH)
			{
				double extPct = (h - vp.VAH) / c * 100.0;
				double extVa = vaW > 0 ? (h - vp.VAH) / vaW : 0;
				m_excAbove.Add(Hit(vp, vp.VAH, extPct, extVa));
			}

			// ----- VAL -----
			if (wantBelow && prevIn && l <= vp.VAL)
			{
				double extPct = (vp.VAL - l) / c * 100.0;
				double extVa = vaW > 0 ? (vp.VAL - l) / vaW : 0;
				var ev = Hit(vp, vp.VAL, extPct, extVa);
				m_valTest.Add(ev);
				if (c >= vp.VAL) m_valFail.Add(ev);
			}
			if (wantBelow && (prevIn || prevAbove) && c < vp.VAL)
			{
				double extPct = (vp.VAL - c) / c * 100.0;
				double extVa = vaW > 0 ? (vp.VAL - c) / vaW : 0;
				m_valAccept.Add(Hit(vp, vp.VAL, extPct, extVa));
			}
			if (wantBelow && l < vp.VAL)
			{
				double extPct = (vp.VAL - l) / c * 100.0;
				double extVa = vaW > 0 ? (vp.VAL - l) / vaW : 0;
				m_excBelow.Add(Hit(vp, vp.VAL, extPct, extVa));
			}

			// ----- POC -----
			if (vp.POC > 0 && l <= vp.POC && h >= vp.POC)
			{
				var ev = Hit(vp, vp.POC, 0, 0);
				if (prevBelow || pc < vp.POC) m_pocTagUp.Add(ev);
				else if (prevAbove || pc > vp.POC) m_pocTagDn.Add(ev);
				else m_pocTagUp.Add(ev);
			}
			if (vp.POC > 0 && prevOk)
			{
				if (pc < vp.POC && c > vp.POC)
					m_pocCrossUp.Add(Hit(vp, vp.POC, 0, 0));
				if (pc > vp.POC && c < vp.POC)
					m_pocCrossDn.Add(Hit(vp, vp.POC, 0, 0));
			}

			// ----- nodes -----
			double tol = NodeTickTolerance * TickSize();
			if (vp.Hvns != null)
			{
				foreach (double px in vp.Hvns)
				{
					if (l - tol <= px && h + tol >= px)
					{
						m_hvnTag.Add(Hit(vp, px, 0, 0));
						break; // one HVN tag per bar
					}
				}
			}
			if (vp.Lvns != null)
			{
				foreach (double px in vp.Lvns)
				{
					if (l - tol <= px && h + tol >= px)
					{
						m_lvnTag.Add(Hit(vp, px, 0, 0));
						break;
					}
				}
			}

			UpdateRotation(vp, o, h, l, c, thisIn);
		}

		private EventHit Hit(VpSnap vp, double level, double extPct, double extVa)
		{
			return new EventHit
			{
				barNum = Bars.CurrentBar,
				level = level,
				poc = vp.POC,
				vah = vp.VAH,
				val = vp.VAL,
				vaWidth = vp.VaWidth,
				excursionPct = extPct,
				excursionVa = extVa
			};
		}

		private void UpdateRotation(VpSnap vp, double o, double h, double l, double c, bool thisIn)
		{
			// A new period starts when the live profile object changes — first bar
			// of that profile is tested against the PriorVP snapshot.
			if (m_newPeriod || !m_periodOpen)
			{
				if (m_periodOpen && m_sessDir != 0)
				{
					m_sessOpenOut++;
					if (m_sessReentered) m_sessReenter++;
					if (m_sessOpposite) m_sessOppHit++;
				}
				m_periodOpen = true;
				m_sessReentered = false;
				m_sessOpposite = false;
				if (o > vp.VAH) m_sessDir = 1;
				else if (o < vp.VAL) m_sessDir = -1;
				else m_sessDir = 0;
			}

			if (m_sessDir == 0) return;
			if (!m_sessReentered && thisIn)
				m_sessReentered = true;
			if (m_sessReentered)
			{
				if (m_sessDir > 0 && l <= vp.VAL) m_sessOpposite = true;
				if (m_sessDir < 0 && h >= vp.VAH) m_sessOpposite = true;
			}
		}

		// =====================================================================
		// Output
		// =====================================================================
		private void PrintAll()
		{
			if (m_printed) return;
			m_printed = true;

			// flush last profile period
			if (m_periodOpen && m_sessDir != 0)
			{
				m_sessOpenOut++;
				if (m_sessReentered) m_sessReenter++;
				if (m_sessOpposite) m_sessOppHit++;
				m_sessDir = 0;
				m_periodOpen = false;
			}

			Output.WriteLine("=== VOLUME PROFILE LEVEL STATISTICS ===");
			Output.WriteLine("Symbol: {0} | Ref={1} | Forward={2} | HVN>={3:F2}xPOC | LVN<={4:F2}xPOC | Side={5}",
					Bars.Info.Name, ProfileRef, ForwardLookback, HvnRelThreshold, LvnRelThreshold, SideFilter);
			try
			{
				Output.WriteLine("Date Range: {0:yyyy/MM/dd} - {1:yyyy/MM/dd}",
						Bars.Request.Range.From.Date, Bars.Request.Range.To.Date);
			}
			catch { }
			Output.WriteLine("");
			PrintDiagnose();
			PrintOccupancy();
			PrintEdgeSection("VAH  (tests from inside value)", m_vahTest, m_vahFail, m_vahAccept, m_excAbove, true);
			PrintEdgeSection("VAL  (tests from inside value)", m_valTest, m_valFail, m_valAccept, m_excBelow, false);
			PrintPocSection();
			PrintNodeSection();
			PrintRotation();
			PrintForwardBlock("AFTER VAH FAILED AUCTION — next " + ForwardLookback + " bars", m_vahFail, true, true);
			PrintForwardBlock("AFTER VAH ACCEPTANCE (close above) — next " + ForwardLookback + " bars", m_vahAccept, true, false);
			PrintForwardBlock("AFTER VAL FAILED AUCTION — next " + ForwardLookback + " bars", m_valFail, false, true);
			PrintForwardBlock("AFTER VAL ACCEPTANCE (close below) — next " + ForwardLookback + " bars", m_valAccept, false, false);
			PrintForwardBlock("AFTER POC TAG FROM BELOW — next " + ForwardLookback + " bars", m_pocTagUp, true, false);
			PrintForwardBlock("AFTER POC TAG FROM ABOVE — next " + ForwardLookback + " bars", m_pocTagDn, false, false);
			PrintForwardBlock("AFTER HVN TAG — next " + ForwardLookback + " bars", m_hvnTag, true, false);
			PrintForwardBlock("AFTER LVN TAG — next " + ForwardLookback + " bars", m_lvnTag, true, false);
			Output.WriteLine("------------------------------------------------");
		}

		private void PrintDiagnose()
		{
			Output.WriteLine("--- PROFILE READ DIAGNOSTIC ---");
			Output.WriteLine("Bars visited           : {0}", m_dbgBars);
			Output.WriteLine("Successful VA reads    : {0}", m_dbgOk);
			Output.WriteLine("Profiles in collection : {0}", m_dbgItemCount);
			Output.WriteLine("Collection Visible     : {0}", m_dbgVisible);
			Output.WriteLine("How last profile found : {0}", string.IsNullOrEmpty(m_dbgHow) ? "(none)" : m_dbgHow);
			Output.WriteLine("Live-profile fallbacks : {0}", m_dbgFallback);
			Output.WriteLine("pack=null / vp=null / empty / no-VA / exceptions : {0} / {1} / {2} / {3} / {4}",
					m_dbgPackNull, m_dbgVpNull, m_dbgEmpty, m_dbgNoVa, m_dbgEx);
			if (!string.IsNullOrEmpty(m_dbgLastEx))
				Output.WriteLine("Last exception         : {0}", m_dbgLastEx);
			if (m_barsUsed < 1)
			{
				Output.WriteLine("");
				Output.WriteLine("Nothing was classified. Typical causes:");
				Output.WriteLine("  1. Volume Profile is not enabled on THIS instrument (Data1).");
				Output.WriteLine("     Format Instrument → Volume Profile → Enable, apply, wait until the");
				Output.WriteLine("     histogram actually draws. Visible=false means the .NET collection is empty.");
				Output.WriteLine("  2. First pass ran before MultiCharts finished building profiles.");
				Output.WriteLine("     This study now recalculates on VolumeProfile.EChanged. If this dump");
				Output.WriteLine("     is still empty, press Recalculate or toggle VP off/on.");
				Output.WriteLine("  3. Only one profile is in the collection (composite / last-only).");
				Output.WriteLine("     PriorVP has nothing previous to lock. Use session or every-N-bar VP,");
				Output.WriteLine("     or switch ProfileRef to PriorBar / Developing.");
				Output.WriteLine("  4. Volume Profile drawing tool is NOT the same as Format Instrument VP.");
				Output.WriteLine("     Only the instrument VP is visible to PowerLanguage .NET.");
			}
			Output.WriteLine("");
		}

		private void PrintOccupancy()
		{
			Output.WriteLine("--- LOCATION OCCUPANCY (close vs reference VA) ---");
			if (m_barsUsed < 1)
			{
				Output.WriteLine("No classified bars. See PROFILE READ DIAGNOSTIC above.");
				Output.WriteLine("");
				return;
			}
			Output.WriteLine("Bars classified     : {0}", m_barsUsed);
			Output.WriteLine("Close inside VA     : {0}  ({1:F2}%)", m_closeInVA, Pct(m_closeInVA, m_barsUsed));
			Output.WriteLine("Close above VAH     : {0}  ({1:F2}%)", m_closeAbove, Pct(m_closeAbove, m_barsUsed));
			Output.WriteLine("Close below VAL     : {0}  ({1:F2}%)", m_closeBelow, Pct(m_closeBelow, m_barsUsed));
			Output.WriteLine("Bar high > VAH      : {0}  ({1:F2}%)", m_highAboveVAH, Pct(m_highAboveVAH, m_barsUsed));
			Output.WriteLine("Bar low  < VAL      : {0}  ({1:F2}%)", m_lowBelowVAL, Pct(m_lowBelowVAL, m_barsUsed));
			if (m_vaWidthPct.Count > 0)
			{
				Output.WriteLine("VA width avg / med  : {0:F4}% / {1:F4}% of price",
						m_vaWidthPct.Average(), Median(m_vaWidthPct));
			}
			if (m_closeToPocVa.Count > 0)
			{
				Output.WriteLine("Close vs POC        : avg {0:F3} VA-widths (signed, + = above POC)",
						m_closeToPocVa.Average());
				var abs = m_closeToPocVa.Select(Math.Abs).ToList();
				Output.WriteLine("|Close − POC| med   : {0:F3} VA-widths", Median(abs));
			}
			Output.WriteLine("");
		}

		private void PrintEdgeSection(
				string title,
				List<EventHit> tests,
				List<EventHit> fails,
				List<EventHit> accepts,
				List<EventHit> excursions,
				bool isHighSide)
		{
			Output.WriteLine("--- {0} ---", title);
			Output.WriteLine("Tests from inside   : {0}  ({1:F2}% of classified bars)",
					tests.Count, Pct(tests.Count, m_barsUsed));
			if (tests.Count >= MinEvents)
			{
				Output.WriteLine("Failed auction      : {0}  ({1:F2}% of tests)   close back inside / at edge",
						fails.Count, Pct(fails.Count, tests.Count));
				Output.WriteLine("Accepted through    : {0}  ({1:F2}% of tests+entries that closed through)",
						accepts.Count, Pct(accepts.Count, Math.Max(1, tests.Count + accepts.Count)));
			}
			if (excursions.Count >= MinEvents)
			{
				var pcts = excursions.Select(e => e.excursionPct).ToList();
				var vas = excursions.Select(e => e.excursionVa).ToList();
				Output.WriteLine("Excursion sample    : {0} bars that traded {1} the edge",
						excursions.Count, isHighSide ? "above VAH" : "below VAL");
				Output.WriteLine("Avg / med excursion : {0:F4}% / {1:F4}% of price",
						pcts.Average(), Median(pcts));
				Output.WriteLine("Avg / med excursion : {0:F3} / {1:F3} VA-widths",
						vas.Average(), Median(vas));
				PrintHistogram("EXCURSION SIZE (% of price)", pcts);
				PrintCumulative("CUMULATIVE P(excursion ≥ X% of price)", pcts);
				PrintVaHist(vas);
			}
			else
			{
				Output.WriteLine("(need {0}+ excursion bars to print size distribution)", MinEvents);
			}
			Output.WriteLine("");
		}

		private void PrintPocSection()
		{
			int tags = m_pocTagUp.Count + m_pocTagDn.Count;
			Output.WriteLine("--- POC ---");
			Output.WriteLine("Bars that tagged POC: {0}  ({1:F2}% of classified bars)",
					tags, Pct(tags, m_barsUsed));
			Output.WriteLine("  from below / above: {0} / {1}", m_pocTagUp.Count, m_pocTagDn.Count);
			Output.WriteLine("Close crossed up    : {0}  ({1:F2}%)", m_pocCrossUp.Count, Pct(m_pocCrossUp.Count, m_barsUsed));
			Output.WriteLine("Close crossed down  : {0}  ({1:F2}%)", m_pocCrossDn.Count, Pct(m_pocCrossDn.Count, m_barsUsed));
			Output.WriteLine("");
		}

		private void PrintNodeSection()
		{
			Output.WriteLine("--- HVN / LVN TAGS ---");
			Output.WriteLine("HVN = level vol ≥ {0:F2} × POC vol", HvnRelThreshold);
			Output.WriteLine("LVN = local volume min and vol ≤ {0:F2} × POC vol", LvnRelThreshold);
			Output.WriteLine("Bars tagging an HVN : {0}  ({1:F2}%)", m_hvnTag.Count, Pct(m_hvnTag.Count, m_barsUsed));
			Output.WriteLine("Bars tagging an LVN : {0}  ({1:F2}%)", m_lvnTag.Count, Pct(m_lvnTag.Count, m_barsUsed));
			Output.WriteLine("");
		}

		private void PrintRotation()
		{
			Output.WriteLine("--- PRIOR-VP ROTATION ---");
			Output.WriteLine("New profiles that opened outside PriorVP VA : {0}", m_sessOpenOut);
			if (m_sessOpenOut >= MinEvents)
			{
				Output.WriteLine("Re-entered PriorVP value in that profile   : {0}  ({1:F2}%)",
						m_sessReenter, Pct(m_sessReenter, m_sessOpenOut));
				if (m_sessReenter > 0)
				{
					Output.WriteLine("…and reached the opposite VA edge          : {0}  ({1:F2}% of re-entries, {2:F2}% of opens-outside)",
							m_sessOppHit,
							Pct(m_sessOppHit, m_sessReenter),
							Pct(m_sessOppHit, m_sessOpenOut));
				}
			}
			else
			{
				Output.WriteLine("(need {0}+ profile-boundary opens outside PriorVP VA)", MinEvents);
			}
			Output.WriteLine("");
		}

		private void PrintForwardBlock(string title, List<EventHit> events, bool lookUp, bool isFail)
		{
			if (events == null || events.Count < MinEvents)
				return;

			int n = 0;
			int pocWithRet = 0, pocNoRet = 0, oppWithRet = 0, oppNoRet = 0;
			var contPct = new List<double>();
			var retPct = new List<double>();

			foreach (EventHit ev in events)
			{
				int barsAgo = Bars.CurrentBar - ev.barNum;
				if (barsAgo < ForwardLookback) continue;
				if (barsAgo >= m_bars.Count) continue;
				OHLCV src = m_bars[barsAgo];
				if (src.close <= 0) continue;

				double maxH = src.high;
				double minL = src.low;
				bool sawLevel = false, sawPoc = false, sawOpp = false;
				for (int i = 1; i <= ForwardLookback; i++)
				{
					int idx = barsAgo - i;
					if (idx < 0) break;
					OHLCV b = m_bars[idx];
					maxH = Math.Max(maxH, b.high);
					minL = Math.Min(minL, b.low);
					if (b.low <= ev.level && b.high >= ev.level) sawLevel = true;
					if (ev.poc > 0 && b.low <= ev.poc && b.high >= ev.poc) sawPoc = true;
					if (lookUp && ev.val > 0 && b.low <= ev.val) sawOpp = true;
					if (!lookUp && ev.vah > 0 && b.high >= ev.vah) sawOpp = true;
				}

				n++;
				if (sawPoc && sawLevel) pocWithRet++;
				else if (sawPoc && !sawLevel) pocNoRet++;
				if (sawOpp && sawLevel) oppWithRet++;
				else if (sawOpp && !sawLevel) oppNoRet++;

				if (lookUp)
				{
					double cont = (maxH / src.close - 1.0) * 100.0;
					double ret = (src.close - minL) / src.close * 100.0;
					contPct.Add(Math.Max(0, cont));
					retPct.Add(Math.Max(0, ret));
				}
				else
				{
					double cont = (src.close - minL) / src.close * 100.0;
					double ret = (maxH / src.close - 1.0) * 100.0;
					contPct.Add(Math.Max(0, cont));
					retPct.Add(Math.Max(0, ret));
				}
			}

			if (n < MinEvents) return;

			Output.WriteLine("--- {0} ---", title);
			Output.WriteLine("Events with full forward window : {0}  (of {1} total)", n, events.Count);
			Output.WriteLine("{0,-48} {1:F2}%", "Tagged POC (with return to event)", Pct(pocWithRet, n));
			Output.WriteLine("{0,-48} {1:F2}%", "Tagged opposite VA edge (with return to event)", Pct(oppWithRet, n));
			Output.WriteLine("{0,-48} {1:F2}%", "Tagged POC (no return to event)", Pct(pocNoRet, n));
			Output.WriteLine("{0,-48} {1:F2}%", "Tagged opposite VA edge (no return to event)", Pct(oppNoRet, n));
			if (contPct.Count > 0)
			{
				Output.WriteLine("Avg / med continuation (favorable) : {0:F4}% / {1:F4}%",
						contPct.Average(), Median(contPct));
				Output.WriteLine("Avg / med adverse excursion        : {0:F4}% / {1:F4}%",
						retPct.Average(), Median(retPct));
				PrintHistogram("FORWARD FAVORABLE MOVE (% of price)", contPct);
				PrintCumulative("CUMULATIVE P(favorable move ≥ X%)", contPct);
			}
			Output.WriteLine("");
		}

		private void PrintHistogram(string title, List<double> values)
		{
			if (values == null || values.Count == 0) return;
			var buckets = new SortedDictionary<int, int>();
			int total = 0;
			foreach (double v in values)
			{
				total++;
				int key = BucketKey(v);
				if (!buckets.ContainsKey(key)) buckets[key] = 0;
				buckets[key]++;
			}

			Output.WriteLine("{0}", title);
			Output.WriteLine(string.Format("{0,-16} {1,-10} {2,10}", "Bucket", "Count", "Probability"));
			Output.WriteLine(new string('-', 38));

			int under = buckets.ContainsKey(-1) ? buckets[-1] : 0;
			Output.WriteLine(string.Format("{0,-16} {1,-10} {2,9:F2}%",
					"< " + Fmt(BucketStepPct) + "%", under, under / (double)total * 100.0));

			int steps = (int)Math.Round(BucketMaxPct / BucketStepPct);
			// i starts at 1 so the first closed bucket is [Step, 2Step),
			// not a second copy of [0, Step) which already printed as "< Step%".
			for (int i = 1; i < steps; i++)
			{
				double lo = i * BucketStepPct;
				int key = (int)Math.Round(lo / BucketStepPct);
				int c = buckets.ContainsKey(key) ? buckets[key] : 0;
				string lab = string.Format("{0}-{1}%", Fmt(lo), Fmt(lo + BucketStepPct));
				Output.WriteLine(string.Format("{0,-16} {1,-10} {2,9:F2}%", lab, c, c / (double)total * 100.0));
			}
			int over = buckets.ContainsKey(999) ? buckets[999] : 0;
			Output.WriteLine(string.Format("{0,-16} {1,-10} {2,9:F2}%",
					"> " + Fmt(BucketMaxPct) + "%", over, over / (double)total * 100.0));
			Output.WriteLine("");
		}

		private void PrintCumulative(string title, List<double> values)
		{
			if (values == null || values.Count == 0) return;
			int total = values.Count;
			Output.WriteLine("{0}", title);
			Output.WriteLine(string.Format("{0,-16} {1,10}", "At least X%", "Probability"));

			double max = BucketMaxPct;
			for (double x = max; x >= BucketStepPct - 1e-9; x -= BucketStepPct)
			{
				int c = values.Count(v => v + 1e-12 >= x);
				Output.WriteLine(string.Format("{0,-16} {1,9:F2}%",
						"At least " + Fmt(x) + "%", c / (double)total * 100.0));
			}
			Output.WriteLine("");
		}

		private void PrintVaHist(List<double> vaWidths)
		{
			if (vaWidths == null || vaWidths.Count < MinEvents) return;
			double[] edges = { 0.10, 0.25, 0.50, 0.75, 1.00, 1.50, 2.00, 3.00 };
			Output.WriteLine("EXCURSION SIZE (VA-widths)");
			Output.WriteLine(string.Format("{0,-16} {1,-10} {2,10}", "Bucket", "Count", "Probability"));
			Output.WriteLine(new string('-', 38));
			int total = vaWidths.Count;
			double prev = 0;
			for (int i = 0; i < edges.Length; i++)
			{
				double hi = edges[i];
				int c = vaWidths.Count(v => v >= prev && v < hi);
				string lab = string.Format("{0:0.00}-{1:0.00} VA", prev, hi);
				Output.WriteLine(string.Format("{0,-16} {1,-10} {2,9:F2}%", lab, c, c / (double)total * 100.0));
				prev = hi;
			}
			int tail = vaWidths.Count(v => v >= prev);
			Output.WriteLine(string.Format("{0,-16} {1,-10} {2,9:F2}%",
					"≥ " + prev.ToString("0.00") + " VA", tail, tail / (double)total * 100.0));
			Output.WriteLine("");
		}

		private int BucketKey(double pct)
		{
			if (pct < BucketStepPct) return -1;
			if (pct > BucketMaxPct) return 999;
			double lo = Math.Floor(pct / BucketStepPct) * BucketStepPct;
			// snap to the left edge index
			return (int)Math.Round(lo / BucketStepPct);
		}

		private static string Fmt(double x)
		{
			if (Math.Abs(x - Math.Round(x)) < 1e-9) return ((int)Math.Round(x)).ToString();
			return x.ToString("0.00");
		}

		private static double Pct(int n, int d)
		{
			if (d <= 0) return 0;
			return n / (double)d * 100.0;
		}

		private static double Median(List<double> src)
		{
			if (src == null || src.Count == 0) return 0;
			var a = src.ToList();
			a.Sort();
			int m = a.Count / 2;
			if ((a.Count & 1) == 1) return a[m];
			return 0.5 * (a[m - 1] + a[m]);
		}

		private double TickSize()
		{
			return Bars.Info.MinMove / Bars.Info.PriceScale;
		}
	}
}
