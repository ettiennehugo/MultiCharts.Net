/*
  _VolumeProfileBaseLog — MultiCharts .NET indicator
  =================================================
  Reads a trace exported from _VolumeProfileTest1 (PowerLanguage Output / trace
  CSV, or a raw text log) and paints the completed volume-profile dump on the
  chart through IChartCustomDrawer.

  One [VP-TEST] NEW profile block is one completed profile. The block records
  the prior profile's stream, start/end bar, OHLC, POC / naked POC, VWAP slots,
  HVN, LVN, the confluence snapshot taken on the new-profile bar, bullish and
  bearish targets from that profile's POC and HVN peaks, and the naked-POC
  lookback.

  Bar indexes in the log are 1-based FullSymbolData.Current values (Status
  Bar #). Rectangles are anchored on the bar time logged with the NEW line
  when that time is known, so a Data2/Data3 dump still lands on the chart
  time axis. Bar-number ChartPoints are the fallback.

  Drawing
  -------
  HVN          light green rectangle over the profile window
  LVN          light red rectangle over the profile window
  Confluence   magnet orange, acceptance green, rejection red, mixed yellow
  Naked POC    gold band on the profile window (color not specified in the brief)
  Bullish      green arrow from the POC/HVN origin to the target price
  Bearish      red arrow from the POC/HVN origin to the target price
  Border and fill share a color. Fill alpha is FillOpacityPercent (default 65).

  Only zones that intersect the viewport are converted to screen points.
  Profiles are sorted by start time; the draw pass binary-searches that list
  and walks only the window that can overlap the visible time range.

  Chart: insert on the series you want the overlay on. Set LogFileName to the
  full path of the trace CSV. RenderDataStream 1 = Data1, 0 = every stream in the log.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;

namespace PowerLanguage.Indicator
{
	[SameAsSymbol(true)]
	public class _VolumeProfileBaseLog : IndicatorObject, IChartCustomDrawer
	{
		//constants
		const int DefaultOpacityPercent = 25;
		const int DefaultBorderWidth = 1;
		const int DefaultNakedBandTicks = 2;
		const float ArrowSize = 5f;

		//types
		sealed class PriceNode
		{
			public int Index;
			public double Low;
			public double High;
			public double Peak;
			public double PeakVol;
			public double Strength;
			public bool IsPoc;
		}

		sealed class ConfZone
		{
			public int Index;
			public string Role;       // Magnet, Acceptance, Rejection, Mixed
			public string Location;   // InsideValue, Discount, Premium
			public string Use;        // target, entry, conflict
			public double Anchor;
			public double Low;
			public double High;
			public double Acceptance;
			public double Rejection;
			public double Context;
			public int Sources;
		}

		sealed class TargetMark
		{
			public string Direction;  // Bullish, Bearish
			public string From;       // POC, HVN[n]
			public double Origin;
			public double Touch;
			public double Price;
			public double Distance;
			public double Z;
			public double Raw;
			public int Hits;
			public int Index;
		}

		sealed class NakedMark
		{
			public int Age;
			public int StartBar;
			public int EndBar;
			public double Poc;
			public double PocVol;
			public bool Complete;
			public int TouchedBar;
			public bool Current;
		}

		sealed class ProfileDump
		{
			public int DataStream;
			public int NewBar;
			public DateTime NewTime;
			public int PriorBar;
			public int StartBar;
			public int EndBar;
			public DateTime StartTime;
			public DateTime EndTime;
			public bool HasStartTime;
			public bool HasEndTime;
			public double Open;
			public double High;
			public double Low;
			public double Close;
			public double Poc;
			public double PocVol;
			public double Vah;
			public double Val;
			public bool NakedPoc;
			public bool Complete;
			public int TouchedBar;
			public double Vwap1;
			public int Vwap1Stream;
			public double Vwap1Sigma;
			public double Vwap2;
			public int Vwap2Stream;
			public double Vwap2Sigma;
			public double Vwap3;
			public int Vwap3Stream;
			public double Vwap3Sigma;
			public readonly List<PriceNode> Hvns = new List<PriceNode>();
			public readonly List<PriceNode> Lvns = new List<PriceNode>();
			public readonly List<ConfZone> Confluence = new List<ConfZone>();
			public readonly List<TargetMark> Targets = new List<TargetMark>();
			public readonly List<NakedMark> Nakeds = new List<NakedMark>();
		}

		sealed class LogSnapshot
		{
			public string Path = "";
			public DateTime FileStampUtc;
			public long FileLength;
			public int ProfileCount;
			public int StreamCount;
			public int ParseErrors;
			public string Status = "not loaded";
			public TimeSpan MaxDuration = TimeSpan.FromHours(12);
			public readonly List<ProfileDump> ByStartTime = new List<ProfileDump>();
			public readonly List<ProfileDump> Untimed = new List<ProfileDump>();
		}

		sealed class TargetHeader
		{
			public string From;
			public double Origin;
		}

		//properties
		/// <summary>Full path of the trace CSV or raw log produced by _VolumeProfileTest1.</summary>
		[Input] public string LogFileName { get; set; }
		/// <summary>Data stream to paint. 1 = Data1. 0 paints every stream in the log.</summary>
		[Input] public int RenderDataStream { get; set; }
		[Input] public bool ShowHvn { get; set; }
		[Input] public bool ShowLvn { get; set; }
		[Input] public bool ShowConfluence { get; set; }
		[Input] public bool ShowConfluenceMagnet { get; set; }
		[Input] public bool ShowConfluenceAcceptance { get; set; }
		[Input] public bool ShowConfluenceRejection { get; set; }
		[Input] public bool ShowConfluenceMixed { get; set; }
		[Input] public bool ShowNakedPoc { get; set; }
		[Input] public bool ShowBullishTargets { get; set; }
		[Input] public bool ShowBearishTargets { get; set; }
		/// <summary>Targets traced from the completed profile POC.</summary>
		[Input] public bool ShowTargetsFromPoc { get; set; }
		/// <summary>Targets traced from the completed profile HVN peaks.</summary>
		[Input] public bool ShowTargetsFromHvn { get; set; }
		/// <summary>Rectangle fill opacity, percent. 65 = 65% opaque.</summary>
		[Input] public int FillOpacityPercent { get; set; }
		[Input] public int ZoneBorderWidth { get; set; }
		/// <summary>Half-height of a naked-POC band, in ticks. Also the pad when a zone's low equals its high.</summary>
		[Input] public int NakedPocBandTicks { get; set; }
		[Input] public Color HvnColor { get; set; }
		[Input] public Color LvnColor { get; set; }
		[Input] public Color ConfluenceMagnetColor { get; set; }
		[Input] public Color ConfluenceAcceptanceColor { get; set; }
		[Input] public Color ConfluenceRejectionColor { get; set; }
		[Input] public Color ConfluenceMixedColor { get; set; }
		[Input] public Color BullishTargetColor { get; set; }
		[Input] public Color BearishTargetColor { get; set; }
		[Input] public Color NakedPocColor { get; set; }

		//attribute
		readonly object m_gate = new object();
		LogSnapshot m_snap = new LogSnapshot();
		bool m_registered;
		double m_tick = 0.25;
		string m_loadedKey = "";

		//interface methods
		public _VolumeProfileBaseLog(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			LogFileName = "";
			RenderDataStream = 1;
			ShowHvn = true;
			ShowLvn = true;
			ShowConfluence = true;
			ShowConfluenceMagnet = true;
			ShowConfluenceAcceptance = true;
			ShowConfluenceRejection = true;
			ShowConfluenceMixed = true;
			ShowNakedPoc = true;
			ShowBullishTargets = true;
			ShowBearishTargets = true;
			ShowTargetsFromPoc = true;
			ShowTargetsFromHvn = true;
			FillOpacityPercent = DefaultOpacityPercent;
			ZoneBorderWidth = DefaultBorderWidth;
			NakedPocBandTicks = DefaultNakedBandTicks;
			HvnColor = Color.LightGreen;
			LvnColor = Color.Red;
			ConfluenceMagnetColor = Color.Orange;
			ConfluenceAcceptanceColor = Color.LimeGreen;
			ConfluenceRejectionColor = Color.Red;
			ConfluenceMixedColor = Color.Yellow;
			BullishTargetColor = Color.LimeGreen;
			BearishTargetColor = Color.Red;
			NakedPocColor = Color.Goldenrod;
			if (!m_registered)
			{
				ChartCustomDraw.Register(this);
				m_registered = true;
			}
		}

		protected override void StartCalc()
		{
			if (RenderDataStream < 0)
				throw new ArgumentException("RenderDataStream must be 0 (all streams) or a chart data stream index >= 1.");
			if (FillOpacityPercent < 0 || FillOpacityPercent > 100)
				throw new ArgumentException("FillOpacityPercent must be 0..100 (65 = 65% opaque).");
			if (ZoneBorderWidth < 1)
				throw new ArgumentException("ZoneBorderWidth must be >= 1.");
			if (NakedPocBandTicks < 0)
				throw new ArgumentException("NakedPocBandTicks must be >= 0.");
			m_tick = ResolveTick();
			EnsureLoaded(force: true);
			if (!m_registered)
			{
				ChartCustomDraw.Register(this);
				m_registered = true;
			}
			RequestRedraw();
		}

		protected override void CalcBar()
		{
			// Price updates already repaint the chart and call Draw. Requesting
			// another ReDraw from here clears the overlay and paints it again.
		}

		protected override void StopCalc()
		{
			Detach();
		}

		protected override void Destroy()
		{
			Detach();
		}

		void Detach()
		{
			if (!m_registered) return;
			try { ChartCustomDraw.Unregister(this); }
			catch { }
			m_registered = false;
			Publish(new LogSnapshot());
			RequestRedraw();
		}

		void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase)
		{
			if (phase != EDrawPhases.BeforeSeries) return;
			if (context == null || context.graphics == null || context.Environment == null) return;
			// Draw only the last call of this paint. Earlier dirty-rect passes are
			// wiped when the price series updates, which is the flicker.
			if (!context.FinalDraw) return;
			RectangleF view = context.DrawRect;
			if (view.Width < 1f || view.Height < 1f) return;

			LogSnapshot snap = Snapshot();
			if (snap == null || snap.ProfileCount == 0) return;

			DateTime viewStart;
			DateTime viewEnd;
			double priceHi;
			double priceLo;
			if (!TryViewport(context, out viewStart, out viewEnd, out priceHi, out priceLo))
				return;

			int stream = RenderDataStream;
			int alpha = OpacityAlpha(FillOpacityPercent);
			float border = ZoneBorderWidth;
			double pad = m_tick * Math.Max(1, NakedPocBandTicks);

			Graphics g = context.graphics;
			GraphicsState state = g.Save();
			try
			{
				g.SetClip(view);
				using (Pen hvnPen = MakePen(HvnColor, border))
				using (Brush hvnFill = MakeFill(HvnColor, alpha))
				using (Pen lvnPen = MakePen(LvnColor, border))
				using (Brush lvnFill = MakeFill(LvnColor, alpha))
				using (Pen magnetPen = MakePen(ConfluenceMagnetColor, border))
				using (Brush magnetFill = MakeFill(ConfluenceMagnetColor, alpha))
				using (Pen acceptPen = MakePen(ConfluenceAcceptanceColor, border))
				using (Brush acceptFill = MakeFill(ConfluenceAcceptanceColor, alpha))
				using (Pen rejectPen = MakePen(ConfluenceRejectionColor, border))
				using (Brush rejectFill = MakeFill(ConfluenceRejectionColor, alpha))
				using (Pen mixedPen = MakePen(ConfluenceMixedColor, border))
				using (Brush mixedFill = MakeFill(ConfluenceMixedColor, alpha))
				using (Pen nakedPen = MakePen(NakedPocColor, border))
				using (Brush nakedFill = MakeFill(NakedPocColor, alpha))
				using (Pen bullPen = MakeArrowPen(BullishTargetColor, border))
				using (Pen bearPen = MakeArrowPen(BearishTargetColor, border))
				{
					foreach (ProfileDump profile in QueryVisible(snap, stream, viewStart, viewEnd))
					{
						if (!PriceMayIntersect(profile, priceLo, priceHi, pad)) continue;
						DrawProfile(context, g, view, profile, priceLo, priceHi, pad,
							hvnPen, hvnFill, lvnPen, lvnFill,
							magnetPen, magnetFill, acceptPen, acceptFill, rejectPen, rejectFill, mixedPen, mixedFill,
							nakedPen, nakedFill, bullPen, bearPen);
					}
				}
			}
			finally
			{
				g.Restore(state);
			}
		}

		//methods
		void EnsureLoaded(bool force)
		{
			string path = LogFileName ?? "";
			if (path.Length == 0 || !File.Exists(path))
			{
				string missing = path.Length == 0 ? "(empty LogFileName)" : path;
				Publish(new LogSnapshot
				{
					Path = path,
					Status = "log not found: " + missing
				});
				if (force)
					Output.WriteLine("[VP-LOG] log not found: " + missing);
				return;
			}

			DateTime stamp;
			long length;
			try
			{
				stamp = File.GetLastWriteTimeUtc(path);
				length = new FileInfo(path).Length;
			}
			catch (Exception ex)
			{
				Publish(new LogSnapshot { Path = path, Status = "log stat failed: " + ex.Message });
				return;
			}

			string key = path + "|" + stamp.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + length.ToString(CultureInfo.InvariantCulture);
			if (!force && key == m_loadedKey) return;

			LogSnapshot snap = ParseFile(path, stamp, length);
			m_loadedKey = key;
			Publish(snap);
			Output.WriteLine(string.Format(CultureInfo.InvariantCulture,
				"[VP-LOG] {0} profiles={1} streams={2} errors={3} path={4}",
				snap.Status, snap.ProfileCount, snap.StreamCount, snap.ParseErrors, snap.Path));
		}

		void Publish(LogSnapshot snap)
		{
			lock (m_gate) m_snap = snap ?? new LogSnapshot();
		}

		LogSnapshot Snapshot()
		{
			lock (m_gate) return m_snap;
		}

		void RequestRedraw()
		{
			try { ChartCustomDraw.ReDraw(); }
			catch { }
		}

		double ResolveTick()
		{
			try
			{
				double scale = Bars.Info.PriceScale;
				double move = Bars.Info.MinMove;
				if (scale > 0 && move > 0)
				{
					double tick = move / scale;
					if (tick > 0 && !double.IsInfinity(tick)) return tick;
				}
			}
			catch { }
			return 0.25;
		}

		static int OpacityAlpha(int percent)
		{
			if (percent < 0) percent = 0;
			if (percent > 100) percent = 100;
			return (int)Math.Round(percent * 255.0 / 100.0);
		}

		static Pen MakePen(Color color, float width)
		{
			return new Pen(Color.FromArgb(255, color.R, color.G, color.B), Math.Max(1f, width));
		}

		static Brush MakeFill(Color color, int alpha)
		{
			return new SolidBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
		}

		static Pen MakeArrowPen(Color color, float width)
		{
			Pen pen = new Pen(Color.FromArgb(255, color.R, color.G, color.B), Math.Max(1.5f, width));
			pen.CustomEndCap = new AdjustableArrowCap(ArrowSize, ArrowSize);
			pen.StartCap = LineCap.Round;
			return pen;
		}

		static LogSnapshot ParseFile(string path, DateTime stamp, long length)
		{
			var snap = new LogSnapshot
			{
				Path = path,
				FileStampUtc = stamp,
				FileLength = length
			};
			var dumps = new List<ProfileDump>();
			var barTime = new Dictionary<long, DateTime>();
			ProfileDump current = null;
			TargetHeader tgt = null;
			int errors = 0;

			try
			{
				using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var sr = new StreamReader(fs, Encoding.UTF8, true))
				{
					string line;
					while ((line = sr.ReadLine()) != null)
					{
						string msg = ExtractMessage(line);
						if (msg.Length == 0 || msg.IndexOf("[VP-TEST]", StringComparison.Ordinal) < 0)
							continue;
						try
						{
							if (Starts(msg, "[VP-TEST] NEW profile "))
							{
								current = ParseNew(msg, barTime);
								tgt = null;
								if (current != null) dumps.Add(current);
								else errors++;
								continue;
							}
							if (current == null) continue;
							if (Starts(msg, "[VP-TEST] PRIOR Data"))
								ParsePriorHeader(msg, current);
							else if (Starts(msg, "[VP-TEST] PRIOR OHLC "))
								ParseOhlc(msg, current);
							else if (Starts(msg, "[VP-TEST] PRIOR POC="))
								ParsePoc(msg, current);
							else if (Starts(msg, "[VP-TEST] PRIOR nakedPOC="))
								ParseNakedFlag(msg, current);
							else if (Starts(msg, "[VP-TEST] PRIOR VWAP "))
								ParseVwap(msg, current);
							else if (Starts(msg, "[VP-TEST]   HVN["))
								current.Hvns.Add(ParseNode(msg, "HVN"));
							else if (Starts(msg, "[VP-TEST]   LVN["))
								current.Lvns.Add(ParseNode(msg, "LVN"));
							else if (Starts(msg, "[VP-TEST]   CONF["))
								current.Confluence.Add(ParseConf(msg));
							else if (Starts(msg, "[VP-TEST] TGT ") && msg.IndexOf(" from=", StringComparison.Ordinal) >= 0)
								tgt = ParseTargetHeader(msg);
							else if (Starts(msg, "[VP-TEST]   TGT["))
							{
								TargetMark mark = ParseTarget(msg, tgt);
								if (mark != null) current.Targets.Add(mark);
							}
							else if (Starts(msg, "[VP-TEST]   NAKED["))
								current.Nakeds.Add(ParseNaked(msg));
						}
						catch
						{
							errors++;
						}
					}
				}
			}
			catch (Exception ex)
			{
				snap.Status = "read failed: " + ex.Message;
				snap.ParseErrors = errors + 1;
				return snap;
			}

			StampProfileTimes(dumps, barTime);
			var streams = new HashSet<int>();
			TimeSpan maxDur = TimeSpan.Zero;
			for (int i = 0; i < dumps.Count; i++)
			{
				ProfileDump p = dumps[i];
				streams.Add(p.DataStream);
				if (p.HasStartTime)
					snap.ByStartTime.Add(p);
				else
					snap.Untimed.Add(p);
				if (p.HasStartTime && p.HasEndTime && p.EndTime >= p.StartTime)
				{
					TimeSpan span = p.EndTime - p.StartTime;
					if (span > maxDur) maxDur = span;
				}
			}
			snap.ByStartTime.Sort(CompareStartTime);
			if (maxDur < TimeSpan.FromMinutes(1)) maxDur = TimeSpan.FromHours(12);
			snap.MaxDuration = maxDur;
			snap.ProfileCount = dumps.Count;
			snap.StreamCount = streams.Count;
			snap.ParseErrors = errors;
			snap.Status = "loaded";
			return snap;
		}

		static int CompareStartTime(ProfileDump a, ProfileDump b)
		{
			int c = a.StartTime.CompareTo(b.StartTime);
			if (c != 0) return c;
			c = a.DataStream.CompareTo(b.DataStream);
			if (c != 0) return c;
			return a.StartBar.CompareTo(b.StartBar);
		}

		static void StampProfileTimes(List<ProfileDump> dumps, Dictionary<long, DateTime> barTime)
		{
			for (int i = 0; i < dumps.Count; i++)
			{
				ProfileDump p = dumps[i];
				DateTime t;
				if (TryBarTime(barTime, p.DataStream, p.StartBar, out t))
				{
					p.StartTime = t;
					p.HasStartTime = true;
				}
				if (TryBarTime(barTime, p.DataStream, p.EndBar, out t))
				{
					p.EndTime = t;
					p.HasEndTime = true;
				}
				else if (p.NewTime != DateTime.MinValue && p.NewBar > p.EndBar)
				{
					// NEW time is the first bar after this profile. Keep it for culling.
					// Drawing pulls one chart bar back so a weekend gap is not treated
					// as a bar step (that left the last profile one bar short).
					p.EndTime = p.NewTime;
					p.HasEndTime = true;
				}
				if (!p.HasStartTime && p.HasEndTime)
				{
					// Still searchable: pin the sort key at the known end so the
					// untimed list is the only place a missing start can hide.
				}
			}
		}

		static bool TryBarTime(Dictionary<long, DateTime> barTime, int stream, int bar, out DateTime time)
		{
			time = DateTime.MinValue;
			if (stream < 1 || bar < 1) return false;
			return barTime.TryGetValue(BarKey(stream, bar), out time);
		}

		static long BarKey(int stream, int bar)
		{
			return ((long)stream << 32) | (uint)bar;
		}

		static ProfileDump ParseNew(string msg, Dictionary<long, DateTime> barTime)
		{
			// [VP-TEST] NEW profile Data1 bar=14535 time=2026-09-13 17:55:00 — dumping PRIOR once (priorStart=14522)
			int data = IntAfter(msg, "Data");
			int bar = IntAfter(msg, "bar=");
			int prior = IntAfter(msg, "priorStart=");
			DateTime when = TimeAfter(msg, "time=");
			if (data < 1 || bar < 1) return null;
			if (when != DateTime.MinValue)
				barTime[BarKey(data, bar)] = when;
			return new ProfileDump
			{
				DataStream = data,
				NewBar = bar,
				NewTime = when,
				StartBar = prior
			};
		}

		static void ParsePriorHeader(string msg, ProfileDump p)
		{
			int bar = IntAfter(msg, "bar=");
			int start = IntAfter(msg, "startBar=");
			int end = IntAfter(msg, "endBar=");
			int stream = IntAfter(msg, "streamOnExtra=");
			if (bar > 0) p.PriorBar = bar;
			if (start > 0) p.StartBar = start;
			if (end > 0) p.EndBar = end;
			if (stream > 0) p.DataStream = stream;
		}

		static void ParseOhlc(string msg, ProfileDump p)
		{
			p.Open = DblAfter(msg, "open=");
			p.High = DblAfter(msg, "high=");
			p.Low = DblAfter(msg, "low=");
			p.Close = DblAfter(msg, "close=");
		}

		static void ParsePoc(string msg, ProfileDump p)
		{
			p.Poc = DblAfter(msg, "POC=");
			p.PocVol = DblAfter(msg, "pocVol=");
			p.Vah = DblAfter(msg, "VAH=");
			p.Val = DblAfter(msg, "VAL=");
		}

		static void ParseNakedFlag(string msg, ProfileDump p)
		{
			p.NakedPoc = BoolAfter(msg, "nakedPOC=");
			p.Complete = BoolAfter(msg, "complete=");
			p.TouchedBar = IntAfter(msg, "touchedBar=");
		}

		static void ParseVwap(string msg, ProfileDump p)
		{
			// vwap=7615.2021(s1 σ=2.8335)  vwap2=0(s2 σ=0)  vwap3=0(s0 σ=0)
			p.Vwap1 = DblAfter(msg, "vwap=");
			p.Vwap1Stream = StreamAfter(msg, "vwap=");
			p.Vwap1Sigma = SigmaAfter(msg, "vwap=");
			p.Vwap2 = DblAfter(msg, "vwap2=");
			p.Vwap2Stream = StreamAfter(msg, "vwap2=");
			p.Vwap2Sigma = SigmaAfter(msg, "vwap2=");
			p.Vwap3 = DblAfter(msg, "vwap3=");
			p.Vwap3Stream = StreamAfter(msg, "vwap3=");
			p.Vwap3Sigma = SigmaAfter(msg, "vwap3=");
		}

		static PriceNode ParseNode(string msg, string kind)
		{
			int lb = msg.IndexOf(kind + "[", StringComparison.Ordinal);
			int index = 0;
			if (lb >= 0)
			{
				int rb = msg.IndexOf(']', lb);
				if (rb > lb)
					int.TryParse(msg.Substring(lb + kind.Length + 1, rb - lb - kind.Length - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
			}
			return new PriceNode
			{
				Index = index,
				Low = DblAfter(msg, "low="),
				High = DblAfter(msg, "high="),
				Peak = DblAfter(msg, "peak="),
				PeakVol = DblAfter(msg, "peakVol="),
				Strength = DblAfter(msg, "strength="),
				IsPoc = BoolAfter(msg, "isPoc=")
			};
		}

		static ConfZone ParseConf(string msg)
		{
			// [VP-TEST]   CONF[0] Magnet/InsideValue use=target anchor=7661 lo=7660.25 hi=7663 acc=2.6205 rej=0 ctx=0 src=3 [...]
			var zone = new ConfZone
			{
				Anchor = DblAfter(msg, "anchor="),
				Low = DblAfter(msg, "lo="),
				High = DblAfter(msg, "hi="),
				Acceptance = DblAfter(msg, "acc="),
				Rejection = DblAfter(msg, "rej="),
				Context = DblAfter(msg, "ctx="),
				Sources = IntAfter(msg, "src="),
				Use = WordAfter(msg, "use=")
			};
			int lb = msg.IndexOf("CONF[", StringComparison.Ordinal);
			if (lb >= 0)
			{
				int rb = msg.IndexOf(']', lb);
				if (rb > lb)
					int.TryParse(msg.Substring(lb + 5, rb - lb - 5), NumberStyles.Integer, CultureInfo.InvariantCulture, out zone.Index);
				int roleAt = rb + 1;
				while (roleAt < msg.Length && msg[roleAt] == ' ') roleAt++;
				int roleEnd = msg.IndexOf(' ', roleAt);
				if (roleEnd < 0) roleEnd = msg.Length;
				string roleLoc = msg.Substring(roleAt, roleEnd - roleAt);
				int slash = roleLoc.IndexOf('/');
				if (slash >= 0)
				{
					zone.Role = roleLoc.Substring(0, slash);
					zone.Location = roleLoc.Substring(slash + 1);
				}
				else zone.Role = roleLoc;
			}
			if (zone.High < zone.Low)
			{
				double swap = zone.Low;
				zone.Low = zone.High;
				zone.High = swap;
			}
			return zone;
		}

		static TargetHeader ParseTargetHeader(string msg)
		{
			return new TargetHeader
			{
				From = WordAfter(msg, "from="),
				Origin = DblAfter(msg, "ref=")
			};
		}

		static TargetMark ParseTarget(string msg, TargetHeader header)
		{
			string direction = "";
			int bull = msg.IndexOf(" Bullish ", StringComparison.Ordinal);
			int bear = msg.IndexOf(" Bearish ", StringComparison.Ordinal);
			if (bull >= 0 && (bear < 0 || bull < bear)) direction = "Bullish";
			else if (bear >= 0) direction = "Bearish";
			string from = WordAfter(msg, "from=");
			if (from.Length == 0 && header != null) from = header.From;
			double origin = header != null ? header.Origin : 0;
			int index = 0;
			int lb = msg.IndexOf("TGT[", StringComparison.Ordinal);
			if (lb >= 0)
			{
				int rb = msg.IndexOf(']', lb);
				if (rb > lb)
					int.TryParse(msg.Substring(lb + 4, rb - lb - 4), NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
			}
			return new TargetMark
			{
				Index = index,
				Direction = direction,
				From = from,
				Origin = origin,
				Touch = DblAfter(msg, "touch="),
				Price = DblAfter(msg, "price="),
				Distance = DblAfter(msg, "dist="),
				Z = DblAfter(msg, "z="),
				Raw = DblAfter(msg, "raw="),
				Hits = IntAfter(msg, "hits=")
			};
		}

		static NakedMark ParseNaked(string msg)
		{
			return new NakedMark
			{
				Age = IntAfter(msg, "age="),
				StartBar = IntAfter(msg, "start="),
				EndBar = IntAfter(msg, "end="),
				Poc = DblAfter(msg, "POC="),
				PocVol = DblAfter(msg, "pocVol="),
				Complete = BoolAfter(msg, "complete="),
				TouchedBar = IntAfter(msg, "touchedBar="),
				Current = BoolAfter(msg, "current=")
			};
		}

		static IEnumerable<ProfileDump> QueryVisible(LogSnapshot snap, int stream, DateTime viewStart, DateTime viewEnd)
		{
			if (snap == null) yield break;
			TimeSpan pad = snap.MaxDuration;
			if (pad < TimeSpan.FromMinutes(1)) pad = TimeSpan.FromHours(12);
			DateTime bound = viewStart == DateTime.MinValue ? DateTime.MinValue : viewStart - pad;

			List<ProfileDump> timed = snap.ByStartTime;
			int from = LowerBoundTime(timed, bound);
			for (int i = from; i < timed.Count; i++)
			{
				ProfileDump p = timed[i];
				if (viewEnd != DateTime.MinValue && p.StartTime > viewEnd) break;
				if (!StreamMatch(p.DataStream, stream)) continue;
				if (p.HasEndTime && viewStart != DateTime.MinValue && p.EndTime < viewStart) continue;
				yield return p;
			}
			for (int i = 0; i < snap.Untimed.Count; i++)
			{
				ProfileDump p = snap.Untimed[i];
				if (!StreamMatch(p.DataStream, stream)) continue;
				if (p.HasEndTime && viewStart != DateTime.MinValue && p.EndTime < viewStart) continue;
				if (p.HasEndTime && viewEnd != DateTime.MinValue && p.StartTime > viewEnd && p.HasStartTime) continue;
				yield return p;
			}
		}

		static int LowerBoundTime(List<ProfileDump> list, DateTime bound)
		{
			int lo = 0;
			int hi = list.Count;
			while (lo < hi)
			{
				int mid = (lo + hi) >> 1;
				if (list[mid].StartTime < bound) lo = mid + 1;
				else hi = mid;
			}
			return lo;
		}

		static bool StreamMatch(int profileStream, int wanted)
		{
			return wanted <= 0 || profileStream == wanted;
		}

		static bool PriceMayIntersect(ProfileDump p, double priceLo, double priceHi, double pad)
		{
			if (priceHi <= priceLo) return true;
			double lo = double.MaxValue;
			double hi = double.MinValue;
			Expand(ref lo, ref hi, p.Low, p.High);
			Expand(ref lo, ref hi, p.Poc, p.Poc);
			for (int i = 0; i < p.Hvns.Count; i++) Expand(ref lo, ref hi, p.Hvns[i].Low, p.Hvns[i].High);
			for (int i = 0; i < p.Lvns.Count; i++) Expand(ref lo, ref hi, p.Lvns[i].Low, p.Lvns[i].High);
			for (int i = 0; i < p.Confluence.Count; i++) Expand(ref lo, ref hi, p.Confluence[i].Low, p.Confluence[i].High);
			for (int i = 0; i < p.Targets.Count; i++)
			{
				Expand(ref lo, ref hi, p.Targets[i].Origin, p.Targets[i].Price);
				Expand(ref lo, ref hi, p.Targets[i].Touch, p.Targets[i].Touch);
			}
			for (int i = 0; i < p.Nakeds.Count; i++) Expand(ref lo, ref hi, p.Nakeds[i].Poc, p.Nakeds[i].Poc);
			if (lo == double.MaxValue) return false;
			return hi + pad >= priceLo && lo - pad <= priceHi;
		}

		static void Expand(ref double lo, ref double hi, double a, double b)
		{
			if (a > 0 && !double.IsNaN(a))
			{
				if (a < lo) lo = a;
				if (a > hi) hi = a;
			}
			if (b > 0 && !double.IsNaN(b) && b != a)
			{
				if (b < lo) lo = b;
				if (b > hi) hi = b;
			}
		}

		void DrawProfile(
			DrawContext context, Graphics g, RectangleF view, ProfileDump profile,
			double priceLo, double priceHi, double pad,
			Pen hvnPen, Brush hvnFill, Pen lvnPen, Brush lvnFill,
			Pen magnetPen, Brush magnetFill, Pen acceptPen, Brush acceptFill,
			Pen rejectPen, Brush rejectFill, Pen mixedPen, Brush mixedFill,
			Pen nakedPen, Brush nakedFill,
			Pen bullPen, Pen bearPen)
		{
			if (ShowHvn)
			{
				for (int i = 0; i < profile.Hvns.Count; i++)
				{
					PriceNode n = profile.Hvns[i];
					if (!BandVisible(n.Low, n.High, priceLo, priceHi, pad)) continue;
					DrawBand(context, g, view, profile, n.Low, n.High, pad, hvnPen, hvnFill);
				}
			}
			if (ShowLvn)
			{
				for (int i = 0; i < profile.Lvns.Count; i++)
				{
					PriceNode n = profile.Lvns[i];
					if (!BandVisible(n.Low, n.High, priceLo, priceHi, pad)) continue;
					DrawBand(context, g, view, profile, n.Low, n.High, pad, lvnPen, lvnFill);
				}
			}
			if (ShowConfluence)
			{
				for (int i = 0; i < profile.Confluence.Count; i++)
				{
					ConfZone z = profile.Confluence[i];
					if (!RoleEnabled(z.Role)) continue;
					if (!BandVisible(z.Low, z.High, priceLo, priceHi, pad)) continue;
					Pen pen;
					Brush fill;
					if (!RoleStyle(z.Role, magnetPen, magnetFill, acceptPen, acceptFill, rejectPen, rejectFill, mixedPen, mixedFill, out pen, out fill))
						continue;
					DrawBand(context, g, view, profile, z.Low, z.High, pad, pen, fill);
				}
			}
			if (ShowNakedPoc)
				DrawNaked(context, g, view, profile, priceLo, priceHi, pad, nakedPen, nakedFill);
			if (ShowBullishTargets || ShowBearishTargets)
				DrawTargets(context, g, view, profile, priceLo, priceHi, bullPen, bearPen);
		}

		bool RoleStyle(
			string role,
			Pen magnetPen, Brush magnetFill, Pen acceptPen, Brush acceptFill,
			Pen rejectPen, Brush rejectFill, Pen mixedPen, Brush mixedFill,
			out Pen pen, out Brush fill)
		{
			pen = null;
			fill = null;
			if (string.IsNullOrEmpty(role)) return false;
			if (role.Equals("Magnet", StringComparison.OrdinalIgnoreCase))
			{
				pen = magnetPen;
				fill = magnetFill;
				return true;
			}
			if (role.Equals("Acceptance", StringComparison.OrdinalIgnoreCase))
			{
				pen = acceptPen;
				fill = acceptFill;
				return true;
			}
			if (role.Equals("Rejection", StringComparison.OrdinalIgnoreCase))
			{
				pen = rejectPen;
				fill = rejectFill;
				return true;
			}
			if (role.Equals("Mixed", StringComparison.OrdinalIgnoreCase))
			{
				pen = mixedPen;
				fill = mixedFill;
				return true;
			}
			return false;
		}

		bool RoleEnabled(string role)
		{
			if (string.IsNullOrEmpty(role)) return ShowConfluence;
			if (role.Equals("Magnet", StringComparison.OrdinalIgnoreCase)) return ShowConfluenceMagnet;
			if (role.Equals("Acceptance", StringComparison.OrdinalIgnoreCase)) return ShowConfluenceAcceptance;
			if (role.Equals("Rejection", StringComparison.OrdinalIgnoreCase)) return ShowConfluenceRejection;
			if (role.Equals("Mixed", StringComparison.OrdinalIgnoreCase)) return ShowConfluenceMixed;
			return ShowConfluence;
		}

		void DrawNaked(DrawContext context, Graphics g, RectangleF view, ProfileDump profile, double priceLo, double priceHi, double pad, Pen pen, Brush fill)
		{
			var seen = new HashSet<int>();
			if (profile.NakedPoc && profile.Poc > 0 && profile.StartBar > 0)
			{
				seen.Add(profile.StartBar);
				if (BandVisible(profile.Poc, profile.Poc, priceLo, priceHi, pad))
					DrawBand(context, g, view, profile, profile.Poc, profile.Poc, pad, pen, fill);
			}
			for (int i = 0; i < profile.Nakeds.Count; i++)
			{
				NakedMark n = profile.Nakeds[i];
				if (n.Current || n.Poc <= 0 || n.StartBar < 1) continue;
				if (!seen.Add(n.StartBar)) continue;
				if (!BandVisible(n.Poc, n.Poc, priceLo, priceHi, pad)) continue;
				int start = n.StartBar;
				int end = n.EndBar > start ? n.EndBar : start;
				DrawBand(context, g, view, profile, n.Poc, n.Poc, pad, pen, fill, start, end);
			}
		}

		void DrawTargets(DrawContext context, Graphics g, RectangleF view, ProfileDump profile, double priceLo, double priceHi, Pen bullPen, Pen bearPen)
		{
			for (int i = 0; i < profile.Targets.Count; i++)
			{
				TargetMark t = profile.Targets[i];
				bool bull = t.Direction.Equals("Bullish", StringComparison.OrdinalIgnoreCase);
				bool bear = t.Direction.Equals("Bearish", StringComparison.OrdinalIgnoreCase);
				if (bull && !ShowBullishTargets) continue;
				if (bear && !ShowBearishTargets) continue;
				if (!bull && !bear) continue;
				bool fromHvn = t.From.StartsWith("HVN", StringComparison.OrdinalIgnoreCase);
				bool fromPoc = t.From.Equals("POC", StringComparison.OrdinalIgnoreCase);
				if (fromHvn && !ShowTargetsFromHvn) continue;
				if (fromPoc && !ShowTargetsFromPoc) continue;
				if (!fromHvn && !fromPoc) continue;
				double dest = t.Price > 0 ? t.Price : t.Touch;
				double origin = t.Origin;
				if (origin <= 0 || dest <= 0) continue;
				double lo = Math.Min(origin, dest);
				double hi = Math.Max(origin, dest);
				if (priceHi > priceLo && (hi < priceLo || lo > priceHi)) continue;
				DrawArrow(context, g, view, profile, origin, dest, bull ? bullPen : bearPen, t.Index);
			}
		}

		void DrawBand(DrawContext context, Graphics g, RectangleF view, ProfileDump profile, double low, double high, double pad, Pen pen, Brush fill)
		{
			DrawBand(context, g, view, profile, low, high, pad, pen, fill, profile.StartBar, profile.EndBar > 0 ? profile.EndBar : profile.NewBar);
		}

		void DrawBand(DrawContext context, Graphics g, RectangleF view, ProfileDump profile, double low, double high, double pad, Pen pen, Brush fill, int startBar, int endBar)
		{
			if (high < low)
			{
				double swap = low;
				low = high;
				high = swap;
			}
			if (high - low < pad * 0.5)
			{
				double mid = high > 0 ? high : low;
				low = mid - pad;
				high = mid + pad;
			}
			ChartPoint left = Anchor(profile, startBar, true, high);
			PointF a, b;
			RectangleF rect;
			if (profile.NewTime != DateTime.MinValue && profile.NewBar > startBar && startBar == profile.StartBar)
			{
				// ChartPoint is the bar center. NewTime is the next profile open,
				// so pull one bar back to the end-bar center, then grow both edges
				// by half a bar so the band covers the full start and end bars.
				ChartPoint exclusive = new ChartPoint(profile.NewTime, low);
				if (!TryPoints(context, left, exclusive, out a, out b)) return;
				float barPx = Math.Abs(b.X - a.X) / (profile.NewBar - startBar);
				if (b.X >= a.X) b.X -= barPx;
				else b.X += barPx;
				ExtendHalfBar(ref a, ref b, barPx);
				if (!TryRectFromPoints(a, b, out rect)) return;
			}
			else
			{
				ChartPoint right = Anchor(profile, endBar, false, low);
				if (!TryPoints(context, left, right, out a, out b)) return;
				float barPx = BarSpacingPx(context, profile);
				if (barPx > 0f) ExtendHalfBar(ref a, ref b, barPx);
				if (!TryRectFromPoints(a, b, out rect)) return;
			}
			if (!rect.IntersectsWith(view)) return;
			g.FillRectangle(fill, rect);
			g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
		}

		float BarSpacingPx(DrawContext context, ProfileDump profile)
		{
			if (profile.NewTime == DateTime.MinValue || !profile.HasStartTime || profile.NewBar <= profile.StartBar)
				return 0f;
			PointF a, b;
			if (!TryPoints(context, new ChartPoint(profile.StartTime, profile.Poc), new ChartPoint(profile.NewTime, profile.Poc), out a, out b))
				return 0f;
			return Math.Abs(b.X - a.X) / (profile.NewBar - profile.StartBar);
		}

		static void ExtendHalfBar(ref PointF left, ref PointF right, float barPx)
		{
			if (barPx <= 0f) return;
			float half = barPx * 0.5f;
			if (left.X <= right.X)
			{
				left.X -= half;
				right.X += half;
			}
			else
			{
				left.X += half;
				right.X -= half;
			}
		}

		void DrawArrow(DrawContext context, Graphics g, RectangleF view, ProfileDump profile, double origin, double dest, Pen pen, int index)
		{
			int bar = profile.EndBar > 0 ? profile.EndBar : profile.NewBar;
			// Fan each target a couple of bars so stacked arrowheads stay readable.
			int destBar = bar + 1 + index;
			ChartPoint from = Anchor(profile, bar, false, origin);
			ChartPoint to = Anchor(profile, destBar, false, dest);
			PointF a, b;
			if (!TryPoints(context, from, to, out a, out b)) return;
			if (!SegmentHits(a, b, view)) return;
			g.DrawLine(pen, a, b);
		}

		static ChartPoint Anchor(ProfileDump profile, int bar, bool startEdge, double price)
		{
			// Time anchors only for this dump's own window. Naked lookback rows and
			// fanned arrow tips keep the log bar number (1-based Status Bar #).
			if (startEdge && profile.HasStartTime && bar == profile.StartBar)
				return new ChartPoint(profile.StartTime, price);
			if (!startEdge && profile.HasEndTime && (bar == profile.EndBar || bar == profile.NewBar))
				return new ChartPoint(profile.EndTime, price);
			if (bar < 1) bar = profile.NewBar > 0 ? profile.NewBar : 1;
			return new ChartPoint(bar, price);
		}

		static bool TryRect(DrawContext context, ChartPoint left, ChartPoint right, out RectangleF rect)
		{
			PointF a, b;
			if (!TryPoints(context, left, right, out a, out b))
			{
				rect = RectangleF.Empty;
				return false;
			}
			return TryRectFromPoints(a, b, out rect);
		}

		static bool TryRectFromPoints(PointF a, PointF b, out RectangleF rect)
		{
			float x = Math.Min(a.X, b.X);
			float y = Math.Min(a.Y, b.Y);
			float w = Math.Abs(a.X - b.X);
			float h = Math.Abs(a.Y - b.Y);
			if (w < 1f) w = 1f;
			if (h < 1f) h = 1f;
			rect = new RectangleF(x, y, w, h);
			return true;
		}

		static bool TryPoints(DrawContext context, ChartPoint from, ChartPoint to, out PointF a, out PointF b)
		{
			a = context.Environment.ChartPoint2Point(from);
			b = context.Environment.ChartPoint2Point(to);
			return IsFinite(a) && IsFinite(b);
		}

		static bool IsFinite(PointF p)
		{
			return !float.IsNaN(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.X) && !float.IsInfinity(p.Y);
		}

		static bool SegmentHits(PointF a, PointF b, RectangleF view)
		{
			RectangleF inflated = view;
			inflated.Inflate(2f, 2f);
			if (inflated.Contains(a) || inflated.Contains(b)) return true;
			float loX = Math.Min(a.X, b.X);
			float hiX = Math.Max(a.X, b.X);
			float loY = Math.Min(a.Y, b.Y);
			float hiY = Math.Max(a.Y, b.Y);
			return hiX >= inflated.Left && loX <= inflated.Right && hiY >= inflated.Top && loY <= inflated.Bottom;
		}

		static bool BandVisible(double low, double high, double priceLo, double priceHi, double pad)
		{
			if (priceHi <= priceLo) return true;
			if (high < low)
			{
				double swap = low;
				low = high;
				high = swap;
			}
			if (low <= 0 && high <= 0) return false;
			return high + pad >= priceLo && low - pad <= priceHi;
		}

		static bool TryViewport(DrawContext context, out DateTime viewStart, out DateTime viewEnd, out double priceHi, out double priceLo)
		{
			viewStart = DateTime.MinValue;
			viewEnd = DateTime.MinValue;
			priceHi = 0;
			priceLo = 0;
			RectangleF r = context.DrawRect;
			ChartPoint left = context.Environment.Point2ChartPoint(new PointF(r.Left, r.Top + r.Height * 0.5f));
			ChartPoint right = context.Environment.Point2ChartPoint(new PointF(r.Right, r.Top + r.Height * 0.5f));
			ChartPoint top = context.Environment.Point2ChartPoint(new PointF(r.Left + r.Width * 0.5f, r.Top));
			ChartPoint bot = context.Environment.Point2ChartPoint(new PointF(r.Left + r.Width * 0.5f, r.Bottom));
			DateTime t1 = left.Time;
			DateTime t2 = right.Time;
			if (t1 != DateTime.MinValue && t2 != DateTime.MinValue)
			{
				viewStart = t1 <= t2 ? t1 : t2;
				viewEnd = t1 <= t2 ? t2 : t1;
			}
			double p1 = top.Price;
			double p2 = bot.Price;
			if (IsPrice(p1) && IsPrice(p2))
			{
				priceHi = Math.Max(p1, p2);
				priceLo = Math.Min(p1, p2);
			}
			return viewStart != DateTime.MinValue || (priceHi > priceLo);
		}

		static bool IsPrice(double p)
		{
			return !double.IsNaN(p) && !double.IsInfinity(p) && p != 0;
		}

		static string ExtractMessage(string line)
		{
			if (string.IsNullOrEmpty(line)) return "";
			int tag = line.IndexOf("[VP-TEST]", StringComparison.Ordinal);
			if (tag < 0) return "";
			string msg = line.Substring(tag).Trim();
			if (msg.Length > 0 && msg[msg.Length - 1] == '"')
				msg = msg.Substring(0, msg.Length - 1);
			return msg.Replace("\"\"", "\"");
		}

		static bool Starts(string msg, string prefix)
		{
			return msg.StartsWith(prefix, StringComparison.Ordinal);
		}

		static int IntAfter(string msg, string key)
		{
			int at = msg.IndexOf(key, StringComparison.Ordinal);
			if (at < 0) return 0;
			at += key.Length;
			int end = at;
			while (end < msg.Length && (char.IsDigit(msg[end]) || msg[end] == '-')) end++;
			int value;
			if (end > at && int.TryParse(msg.Substring(at, end - at), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
				return value;
			return 0;
		}

		static double DblAfter(string msg, string key)
		{
			int at = msg.IndexOf(key, StringComparison.Ordinal);
			if (at < 0) return 0;
			at += key.Length;
			int end = at;
			while (end < msg.Length)
			{
				char c = msg[end];
				if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') { end++; continue; }
				break;
			}
			if (end == at) return 0;
			string token = msg.Substring(at, end - at);
			if (token.Equals("NaN", StringComparison.OrdinalIgnoreCase)) return double.NaN;
			double value;
			if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
				return value;
			return 0;
		}

		static bool BoolAfter(string msg, string key)
		{
			int at = msg.IndexOf(key, StringComparison.Ordinal);
			if (at < 0) return false;
			at += key.Length;
			return at + 4 <= msg.Length && string.Compare(msg, at, "True", 0, 4, StringComparison.OrdinalIgnoreCase) == 0;
		}

		static string WordAfter(string msg, string key)
		{
			int at = msg.IndexOf(key, StringComparison.Ordinal);
			if (at < 0) return "";
			at += key.Length;
			int end = at;
			while (end < msg.Length && !char.IsWhiteSpace(msg[end])) end++;
			return msg.Substring(at, end - at);
		}

		static int StreamAfter(string msg, string valueKey)
		{
			int at = msg.IndexOf(valueKey, StringComparison.Ordinal);
			if (at < 0) return 0;
			int paren = msg.IndexOf("(s", at, StringComparison.Ordinal);
			if (paren < 0) return 0;
			return IntAfter(msg.Substring(paren), "s");
		}

		static double SigmaAfter(string msg, string valueKey)
		{
			int at = msg.IndexOf(valueKey, StringComparison.Ordinal);
			if (at < 0) return 0;
			int sig = msg.IndexOf("\u03c3=", at, StringComparison.Ordinal);
			if (sig < 0) sig = msg.IndexOf("sigma=", at, StringComparison.OrdinalIgnoreCase);
			if (sig < 0) return 0;
			int next = msg.IndexOf("vwap", sig + 2, StringComparison.OrdinalIgnoreCase);
			string slice = next > sig ? msg.Substring(sig, next - sig) : msg.Substring(sig);
			if (slice.StartsWith("\u03c3=", StringComparison.Ordinal)) return DblAfter(slice, "\u03c3=");
			return DblAfter(slice, "sigma=");
		}

		static DateTime TimeAfter(string msg, string key)
		{
			int at = msg.IndexOf(key, StringComparison.Ordinal);
			if (at < 0) return DateTime.MinValue;
			at += key.Length;
			int end = msg.IndexOf('\u2014', at);
			if (end < 0) end = msg.IndexOf(" - ", at, StringComparison.Ordinal);
			if (end < 0) end = msg.Length;
			string token = msg.Substring(at, end - at).Trim();
			DateTime when;
			if (DateTime.TryParseExact(token, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out when))
				return when;
			if (DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out when))
				return when;
			return DateTime.MinValue;
		}
	}
}
