using ATCenterProxy.interop;
using PowerLanguage.Function;
using PowerLanguage.TradeManager;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Implements common utility functions used by signals and calculation of intraday high, low, open and close
	/// if the signal uses minute bars in conjunction with daily/weekly/monthly bars.
	/// </summary>
	public abstract class _CustomSignal : PortfolioSignalObject
	{
		//constants
		// Spec CS-0: compile-time only reference (nameof emits a string literal, no run-time use) proving a study
		// resolves a plain helper class from a separate *.Function.CS file. Remove once CS-1 uses the helper for real.
		private const string HELPER_FILE_PROBE = nameof(_SignalJournal);
		public const double DEFAULT_ACCOUNT_RECALC_INTERVAL = 5;  //number of seconds after which account info would be recalculated if accounts are not yet loaded
		public const int DEFAULT_ENTRY_EXIT_LOOKBACK = 63;        //look back 3-months of trading orders to discover entry/exit orders
		private static readonly TimeSpan DEFAULT_TRADE_MANAGER_REFRESH_INTERVAL = TimeSpan.FromMinutes(1);  //Refresh time for the trade manager to process events.
		/// <summary>Spec CS-3: build tag written on the RUN line. Bump by hand (or paste a git hash) when the base classes change.</summary>
		public const string BUILD_TAG = "base-class-rev2";
		private const double TICK_FALLBACK = 0.01;   // tickSize() when the symbol reports no usable MinMove/PriceScale

		//types
		/// <summary>
		/// Trace level to use for debugging and logging.
		/// </summary>
		public enum ETraceLevel
		{
			None = 0,   //no trace
			Error,
			Warning,
			Info,
			Journal,    //spec CS-1: Info + per-trade journal (MCJournal_<runId>.csv); also on at Verbose/Debug
			Verbose,
			Debug,      //extensive logs for debugging purposes
		}

		/// <summary>
		///	Fill to use when querying the entry time for a position, this is especially important when
		///	scaling into and out of positions.
		/// </summary>
		public enum EUseFill
		{
			First,
			Last,
		}

		// Input parameters
		[Input] public virtual ETraceLevel TraceLevel { get; set; }

		/// <summary>
		/// Singleton log file manager for internal use only.
		/// </summary>
		private sealed class LogFileSingleton
		{
			private static LogFileSingleton s_instance;
			private static readonly object s_lock = new object();

			private string m_logFilePath;
			private IOutput m_output; //MC main output window
			private StreamWriter m_writer;   // guarded by s_lock, opened lazily and kept open (CS-16)

			private LogFileSingleton(IOutput output)
			{
				m_output = output;
				// CS-9: seconds + process id so two MultiCharts processes started in the same minute do not share a file. The run id
				// is per run, but this file is process-wide (every run of every signal), so it is a column, not part of the name.
				string logFilename = $"MCSignal{DateTime.Now:yyyyMMdd-HHmmss}-{System.Diagnostics.Process.GetCurrentProcess().Id}.csv";   //log files are csv's for analysis in Excel
				try
				{
					string dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "MCSignalLogs");
					Directory.CreateDirectory(dir);                       // no-op when it exists
					m_logFilePath = Path.Combine(dir, logFilename);
					File.AppendAllText(m_logFilePath, "", Encoding.UTF8);  // prove we can write here
					m_output.WriteLine($"Log file created {m_logFilePath}");
				}
				catch (Exception ex)
				{
					//fallback to temp directory if AppData is not accessible
					m_logFilePath = Path.Combine(Path.GetTempPath(), logFilename);
					m_output.WriteLine($"Fallback log file created {m_logFilePath} - Exception: {ex.Message}");
				}
			}

			public static LogFileSingleton GetInstance(IOutput output)
			{
				lock (s_lock)
				{
					if (s_instance == null)
					{
						s_instance = new LogFileSingleton(output);
						LogFileSingleton instance = s_instance;
						AppDomain.CurrentDomain.ProcessExit += (s, e) => instance.Close();
						AppDomain.CurrentDomain.DomainUnload += (s, e) => instance.Close();   // study DLL reload
					}

					return s_instance;
				}
			}

			public void WriteTrace(ETraceLevel level, DateTime timestamp, string zoneLabel, string signalName, string symbol, string runId, string message)
			{
				lock (s_lock)
				{
					try
					{
						if (m_writer == null)
						{
							var fs = new FileStream(m_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
							m_writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };   // no BOM mid-file, every line on disk
						}
						string csvMessage = escapeCsv(message);
						// CS-3: runId column after the symbol (parsers that search the message text, e.g. _VolumeProfileBaseLog, are unaffected)
						// CS-9: the bar time's zone (Bars.Info.TimeZone: Local / GMT / Exchange), not the PC offset that "zzz" printed
						m_writer.WriteLine($"{level},{timestamp:yyyy-MM-dd HH:mm:ss} {zoneLabel},{signalName},{symbol},{runId},{csvMessage}");
					}
					catch (Exception ex)
					{
						try { if (m_writer != null) m_writer.Dispose(); } catch { }
						m_writer = null;   // reopen on next write
						m_output.WriteLine($"Error writing to log file: {ex.Message}");
					}
				}
			}

			public void Close()
			{
				lock (s_lock)
				{
					try { if (m_writer != null) m_writer.Dispose(); } catch { }
					m_writer = null;
				}
			}

			private static readonly char[] s_csvSpecial = { ',', '"', '\n', '\r' };
			private string escapeCsv(string input)
			{
				if (string.IsNullOrEmpty(input))
					return input;

				//check if escaping is needed
				bool needsQuotes = input.IndexOfAny(s_csvSpecial) >= 0;

				if (!needsQuotes)
					return input;

				//return escaped version
				return "\"" + input.Replace("\"", "\"\"") + "\"";
			}
		}

		/// <summary>
		/// Spec CS-1: per-trade journal of what MultiCharts does not record (initial stop, R, MFE/MAE in R/ticks, planned vs
		/// fill, tag, context). One CSV per run (MCJournal_&lt;runId&gt;.csv) next to the trace log. Everything else (P&amp;L,
		/// commission, exit order name, bar numbers, run-up/drawdown in $) is copied from MC's own trade objects.
		/// </summary>
		private sealed class TradeJournal
		{
			public const string HEADER = "runId,strategy,symbol,tag,side,qty,entryTime,entryPrice,exitTime,exitPrice,exitOrder,barsHeld," +
				"profit,commission,armed,plannedEntry,entrySlipTicks,initialStop,target,riskPerUnit,riskTicks,riskDollars,rMultiple," +
				"mfeDollars,maeDollars,mfeTicks,maeTicks,mfeR,maeR,excursionScope,context";
			private readonly object m_lock = new object();
			private readonly string m_path;
			private readonly string m_header;
			private StreamWriter m_writer;

			public TradeJournal(string runId) : this(runId, "MCJournal_", HEADER) { }

			/// <summary>CS-8 reuses the writer for MCEvents_&lt;runId&gt;.csv with its own header.</summary>
			public TradeJournal(string runId, string prefix, string header)
			{
				m_header = header;
				string name = prefix + runId + ".csv";
				try
				{
					string dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "MCSignalLogs");
					Directory.CreateDirectory(dir);
					m_path = Path.Combine(dir, name);
				}
				catch
				{
					m_path = Path.Combine(Path.GetTempPath(), name);
				}
			}

			public string Path_ { get { return m_path; } }

			public void Write(string csvLine)
			{
				lock (m_lock)
				{
					if (m_writer == null)
					{
						bool fresh = !File.Exists(m_path);
						var fs = new FileStream(m_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
						m_writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };
						if (fresh) m_writer.WriteLine(m_header);
					}
					m_writer.WriteLine(csvLine);
				}
			}

			public void Close()
			{
				lock (m_lock)
				{
					try { if (m_writer != null) m_writer.Dispose(); } catch { }
					m_writer = null;
				}
			}
		}

		/// <summary>CS-1: what the strategy knew at order send, keyed by entry order name.</summary>
		private sealed class JournalArm
		{
			public string tag;
			public double plannedEntry;
			public double initialStop;
			public double target;
			public string context;
		}

		//inputs

		//properties
		public int IntradayBarCount
		{
			get { return m_intradayBarCount; }
		}

		public double IntradayLow
		{
			get { return m_intradayLow; }
		}

		public DateTime IntradayLowTime
		{
			get { return m_intradayLowTime; }
		}

		public double IntradayHigh
		{
			get { return m_intradayHigh; }
		}

		public DateTime IntradayHighTime
		{
			get { return m_intradayHighTime; }
		}

		/// <summary>CS-9: close of the trading day's last closed bar; the forming bar is not included, except on the day's first bar, which seeds it as it forms.</summary>
		public double IntradayClose
		{
			get { return m_intradayClose; }
		}

		public DateTime IntradayCloseTime
		{
			get { return m_intradayCloseTime; }
		}

		public double IntradayOpen
		{
			get { return m_intradayOpen; }
		}

		public DateTime IntradayOpenTime
		{
			get { return m_intradayOpenTime; }
		}

		/// <summary>CS-9: volume of the trading day's closed bars; the forming bar is never included.</summary>
		public double IntradayVolume
		{
			get { return m_intradayVolume; }
		}

		public Function._Holidays Holidays { get { return m_holidays; } }

		/// <summary>CS-9: zone label of the bar timestamps for the trace file (Bars.Info.TimeZone).</summary>
		private string barZoneLabel()
		{
			try { return Bars.Info.TimeZone.ToString(); } catch { return "?"; }
		}

		/// <summary>
		/// Spec CS-3: short id of this calculation run (new in every StartCalc, so every optimisation pass / recalculation
		/// gets its own id). Written as a column on every trace line and on the RUN line.
		/// </summary>
		public string RunId { get { return m_runId ?? ""; } }

		/// <summary>Spec CS-3: build tag on the RUN line; override in a derived strategy to tag its own version.</summary>
		protected virtual string BuildTag { get { return BUILD_TAG; } }

		protected ITradingData tradingData
		{
			get
			{
				refreshTradeManagerIfNeeded();
				return s_tradingData;
			}
		}

		//static attributes
		private static ITradingData s_tradingData;
		private static long s_lastTradeManagerRefreshTicks = DateTime.MinValue.Ticks;   // Interlocked access only
		private static readonly object s_tradingDataLock = new object();

		//attributes
		private double m_intradayLow;
		private double m_intradayHigh;
		private double m_intradayClose;
		private double m_intradayOpen;
		private DateTime m_intradayLowTime;
		private DateTime m_intradayHighTime;
		private DateTime m_intradayCloseTime;
		private DateTime m_intradayOpenTime;
		private double m_intradayVolume;
		private DateTime m_currentDay;
		private int m_intradayBarCount;
		private bool m_loadAccounts;
		private bool m_unknownAppWarned;
		private bool m_fractionWarned;          // CS-4
		private bool m_lastEquityFromAccount;   // CS-4: last equityAvailable came from the broker account
		private bool m_volumeMbbWarned;
		private bool m_tradingDayWarned;
		private int m_entryCacheBar;
		private bool m_entryCacheClosed;
		private string m_entryCacheKey;
		private DateTime m_entryCacheValue;
		private long m_entryCacheSnapshot;
		private PowerLanguage.Function._NyseHolidays m_holidays;
		private LogFileSingleton m_logger;
		private string m_runId;          // CS-3
		private bool m_runLogged;        // CS-3: RUN line written for this run
		private bool m_runEndLogged;     // CS-3: RUN-END line written for this run
		private double m_tick;           // tickSize() cache, reset in StartCalc (VPB-20, moved here by CS-1)
		private TradeJournal m_journal;  // CS-1, created lazily when ETraceLevel.Journal is on
		private Dictionary<string, JournalArm> m_journalArms;   // CS-1: by entry order name
		private HashSet<string> m_journalWritten;              // CS-1: trades already written (identity key)

		//interface methods
		public _CustomSignal(object _ctx) : base(_ctx) { }


		//methods
		/// <summary>
		/// Checks whether tracing is enabled, use to guard expensive string formatting etc since the $"..." on traces
		/// would be evaluated before any tracing checks are actually done.
		/// Hot-path guard: if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"...");
		/// </summary>
		protected bool traceOn(ETraceLevel level) 
		{ 
			return TraceLevel >= level;
		}

		/// <summary>
		/// Traces the given message string to the log file if the trace level is set higher or equal than the given trace level.
		/// </summary>
		/// <param name="traceLevel">Trace level under which to log the message</param>
		/// <param name="message">Message to log to the file (use above trace guard if this message if complicated to format and it's in a hot path)</param>
		public void trace(ETraceLevel traceLevel, string message)
		{
			if (TraceLevel >= traceLevel)
			{
				if (m_logger == null) m_logger = LogFileSingleton.GetInstance(Output);
				m_logger.WriteTrace(traceLevel, Bars.Time[0], barZoneLabel(), this.GetType().Name, this.Bars.Info.Name, RunId, message);
			}
		}

		// ---- Missing-functionality engines (spec missing-functionality-specs.md rev 4, §0.1). ----
		// Every engine is a plain internal sealed class, created in StartCalc only when a derived strategy enabled it through a
		// protected configuration property (never an [Input], D7), and null otherwise. Defaults keep the current behaviour.

		/// <summary>PRICEACTION (§1): price-action engine configuration. enabled = false (engine not created). A derived strategy sets
		/// PriceActionConfig.enabled = true (plus any per-field override) in its Create() or ctor; futures bases apply the Futures preset.</summary>
		protected PriceActionConfig PriceActionConfig { get; } = new PriceActionConfig();
		private _PriceActionEngine m_pa;
		private bool m_paQueryWarned;

		/// <summary>PRICEACTION engine on PriceActionConfig.dataStream, or null when not enabled (and not required by a lazy dependent).</summary>
		internal _PriceActionEngine PriceActionEngine
		{
			get
			{
				if (m_pa == null && !m_paQueryWarned) { m_paQueryWarned = true; if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "PriceActionEngine queried but PriceActionConfig.enabled is false (engine not created)"); }
				return m_pa;
			}
		}

		/// <summary>§0.1 rule 3: lazy dependents (M-AVWAP swing/breakout anchors, M-XMKT leader swing) force the engine on.</summary>
		protected virtual bool priceActionRequired() { return false; }

		/// <summary>M-AVWAP (§2): event anchors to maintain. None (default) = manager not created. Set in a derived Create()/ctor.</summary>
		protected EAvwapAnchor AvwapEvents { get; set; } = EAvwapAnchor.None;
		/// <summary>M-AVWAP limits (maxAnchors, maxAgeSessions, bands, slope, stream, gap/climax thresholds).</summary>
		protected AvwapLimits AvwapLimits { get; } = new AvwapLimits();
		private _AvwapManager m_avwap;
		private _PriceActionEngine m_avwapPa;   // PRICEACTION instance feeding the anchors (shared with m_pa when on the same stream)
		private bool m_avwapQueryWarned;

		/// <summary>M-AVWAP manager, or null when AvwapEvents == None.</summary>
		internal _AvwapManager AvwapManager
		{
			get
			{
				if (m_avwap == null && !m_avwapQueryWarned) { m_avwapQueryWarned = true; if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "AvwapManager queried but AvwapEvents is None (manager not created)"); }
				return m_avwap;
			}
		}

		/// <summary>M-AVWAP GapOpen: is the bar in regular trading hours? Default true (every bar); the futures base uses the CME RTH window.</summary>
		protected virtual bool isRthBar(DateTime barTime) { return true; }

		// ---- M-TOD (§4): time-of-day normalisers on Data1 closed bars. ----

		/// <summary>M-TOD metrics to normalise (None = engine not created, unless an M-AVWAP VolumeClimax anchor needs Volume).</summary>
		protected ETodMetric TodMetrics { get; set; } = ETodMetric.None;
		/// <summary>M-TOD: completed sessions kept per bucket (20).</summary>
		protected int TodSessions { get; set; } = 20;
		/// <summary>M-TOD: sessions needed before a bucket norm is valid (10).</summary>
		protected int TodMinSessions { get; set; } = 10;
		/// <summary>M-TOD bucket size in minutes; 0 (default) = the Data1 bar size for minute charts, else 1 (clock minute).</summary>
		protected int TodBucketMinutes { get; set; } = 0;
		private _TodNormaliser m_tod;
		private bool m_todQueryWarned;
		private double m_todPrevClose = double.NaN;

		/// <summary>M-TOD normaliser, or null when not enabled.</summary>
		internal _TodNormaliser TodNormaliser
		{
			get
			{
				if (m_tod == null && !m_todQueryWarned) { m_todQueryWarned = true; if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "TodNormaliser queried but TodMetrics is None (engine not created)"); }
				return m_tod;
			}
		}

		/// <summary>M-TOD: minutes since the exchange session open for a bar (end) time. Default: the sessionClock() time of day;
		/// the futures base counts from the 18:00 ET Globex open.</summary>
		protected virtual int todMinuteOfSession(DateTime barTime) { return (int)sessionClock(barTime).TotalMinutes; }
		/// <summary>M-TOD: session excluded from the norms (half-day / holiday session). Default false; the futures base uses the CME table.</summary>
		protected virtual bool todHalfDay(DateTime sessionDate) { return false; }
		/// <summary>M-TOD: delta of the current closed Data1 bar and its FlowQuality tier (lower = better); NaN when unavailable.
		/// Default: no order flow. The futures base reads the VPF-5 engine (OrderFlowEnabled) and never uses BarUpDown.</summary>
		protected virtual double todDelta(out int tier) { tier = -1; return double.NaN; }

		private void startTod()
		{
			m_todQueryWarned = false;
			m_tod = null;
			m_todPrevClose = double.NaN;
			ETodMetric metrics = TodMetrics;
			if ((AvwapEvents & EAvwapAnchor.VolumeClimax) != 0 && AvwapLimits.dataStream == 1) metrics |= ETodMetric.Volume;   // §0.1 rule 3
			if (metrics == ETodMetric.None) return;
			int bucket = TodBucketMinutes;
			if (bucket <= 0) { Resolution r = Bars.Info.Resolution; bucket = r.Type == EResolution.Minute && r.Size > 0 ? (int)r.Size : 1; }   // Resolution.Size is uint
			var t = new _TodNormaliser { metrics = metrics, sessions = TodSessions, minSessions = TodMinSessions, bucketMinutes = bucket, label = "TOD",
				journal = engineSink(ETraceLevel.Journal), warnOnce = engineSink(ETraceLevel.Warning) };
			string err = t.Configure();
			if (err != null) throw new ArgumentException(err);
			m_tod = t;
		}

		private void stepTod()
		{
			if (Bars.Status != EBarState.Close) return;
			DateTime time = Bars.Time[0], day = tradingDayKey(time);
			double h = Bars.High[0], l = Bars.Low[0], c = Bars.Close[0];
			bool splice = rollOn(1);
			double tr = double.IsNaN(m_todPrevClose) ? h - l : Math.Max(h, m_todPrevClose) - Math.Min(l, m_todPrevClose);
			int tier = -1;
			double delta = (TodMetrics & ETodMetric.Delta) != 0 ? todDelta(out tier) : double.NaN;
			if (m_tod.Add(Bars.CurrentBar, new TodSample { sessionDate = day, minuteOfSession = todMinuteOfSession(time), halfDay = todHalfDay(day), splice = splice,
				volume = Bars.Volume[0], delta = delta, range = h - l, trueRange = tr, deltaTier = tier }))
				m_todPrevClose = c;
		}

		// ---- M-DAILY (§5): daily / weekly context. ----

		/// <summary>M-DAILY: set true by a consumer (derived strategy) to create the daily context. Default false = engine not created.</summary>
		protected bool NeedsDaily { get; set; } = false;
		/// <summary>M-DAILY configuration (dataStream 0 = aggregate Data1 per trading day; weeklyStream; ATR/SMA/RSI/TSMOM lengths).</summary>
		protected DailyConfig DailyConfig { get; } = new DailyConfig();
		private _DailyContext m_daily;
		private bool m_dailyQueryWarned;
		private int m_dailyLastStreamBar, m_dailyLastWeekBar;
		private DateTime m_dailyAggDay; private double m_dailyAggO, m_dailyAggH, m_dailyAggL, m_dailyAggC;
		private double m_dailyTodayOpen = double.NaN; private DateTime m_dailyTodayKey = DateTime.MinValue;

		/// <summary>M-DAILY context, or null when NeedsDaily is false.</summary>
		internal _DailyContext DailyContext
		{
			get
			{
				if (m_daily == null && !m_dailyQueryWarned) { m_dailyQueryWarned = true; if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, "DailyContext queried but NeedsDaily is false (engine not created)"); }
				return m_daily;
			}
		}

		/// <summary>M-DAILY: today's gap vs the (roll-adjusted) previous daily close; NaN when not available.</summary>
		protected double DailyGapPct { get { return m_daily == null ? double.NaN : m_daily.GapPct(m_dailyTodayOpen); } }

		private void startDaily()
		{
			m_dailyQueryWarned = false;
			m_daily = null;
			m_dailyLastStreamBar = m_dailyLastWeekBar = int.MinValue;
			m_dailyAggDay = DateTime.MinValue; m_dailyTodayOpen = double.NaN; m_dailyTodayKey = DateTime.MinValue;
			if (!NeedsDaily) return;
			if (DailyConfig.dataStream > 0) requireDataStream("DailyConfig.dataStream", DailyConfig.dataStream);
			if (DailyConfig.weeklyStream > 0) requireDataStream("DailyConfig.weeklyStream", DailyConfig.weeklyStream);
			var d = new _DailyContext { config = DailyConfig, label = "DAILY", journal = engineSink(ETraceLevel.Journal), warnOnce = engineSink(ETraceLevel.Warning) };
			string err = d.Configure();
			if (err != null) throw new ArgumentException(err);
			m_daily = d;
		}

		/// <summary>Data1 closed bar: today's open, roll flag, and (fallback) the per-trading-day aggregate; or the daily stream's closed bar.</summary>
		private void stepDaily()
		{
			if (Bars.Status != EBarState.Close) return;
			DateTime t = Bars.Time[0], key = tradingDayKey(t);
			bool roll = rollOn(1);
			if (key != m_dailyTodayKey) { m_dailyTodayKey = key; m_dailyTodayOpen = Bars.Open[0]; }
			if (DailyConfig.dataStream <= 0)
			{
				if (key != m_dailyAggDay)
				{
					if (m_dailyAggDay != DateTime.MinValue) m_daily.AddDay(m_dailyAggDay, m_dailyAggO, m_dailyAggH, m_dailyAggL, m_dailyAggC);
					m_dailyAggDay = key; m_dailyAggO = Bars.Open[0]; m_dailyAggH = Bars.High[0]; m_dailyAggL = Bars.Low[0];
				}
				else { m_dailyAggH = Math.Max(m_dailyAggH, Bars.High[0]); m_dailyAggL = Math.Min(m_dailyAggL, Bars.Low[0]); }
				m_dailyAggC = Bars.Close[0];
			}
			else
			{
				// a daily bar completed before this Data1 bar is added first, so the roll adjusts the last old-contract day
				IInstrument pb = BarsOfData(DailyConfig.dataStream);
				int pi = HtfClosedIndex(DailyConfig.dataStream), pno = pb.CurrentBar - pi;
				if (pno > m_dailyLastStreamBar && tradingDayKey(pb.Time[pi]) < key) { m_dailyLastStreamBar = pno; m_daily.AddDay(tradingDayKey(pb.Time[pi]), pb.Open[pi], pb.High[pi], pb.Low[pi], pb.Close[pi]); }
			}
			// §0.3: roll-gap removal as soon as the roll bar is seen (offset = roll-bar open - previous day close)
			if (roll && m_daily.Days > 0) m_daily.OnRoll(key, Bars.Open[0] - m_daily.PrevClose);
			if (DailyConfig.dataStream > 0)
			{
				IInstrument db = BarsOfData(DailyConfig.dataStream);
				int idx = HtfClosedIndex(DailyConfig.dataStream), barNo = db.CurrentBar - idx;
				if (barNo > m_dailyLastStreamBar)
				{
					if (m_dailyLastStreamBar != int.MinValue && barNo > m_dailyLastStreamBar + 1 && traceOn(ETraceLevel.Warning))
						trace(ETraceLevel.Warning, $"DAILY Data{DailyConfig.dataStream} advanced {barNo - m_dailyLastStreamBar} bars between Data1 bars; only the latest is used");
					m_dailyLastStreamBar = barNo;
					// already roll-adjusted history; a daily bar of the roll day itself is in the new contract
					m_daily.AddDay(tradingDayKey(db.Time[idx]), db.Open[idx], db.High[idx], db.Low[idx], db.Close[idx]);
				}
			}
			if (DailyConfig.weeklyStream > 0)
			{
				IInstrument wb = BarsOfData(DailyConfig.weeklyStream);
				int wi = HtfClosedIndex(DailyConfig.weeklyStream), wno = wb.CurrentBar - wi;
				if (wno != m_dailyLastWeekBar) { m_dailyLastWeekBar = wno; m_daily.SetPrevWeek(wb.High[wi], wb.Low[wi]); }
			}
		}

		// ---- M-XMKT (§6): cross-market legs on extra chart streams (bar resolutions). Orders stay on Data1. ----

		/// <summary>M-XMKT legs (role -> stream, staleness, windows, leader swing). Empty (default) = no legs created.
		/// Add in a derived Create()/ctor, e.g. XmktLegs.Add(new XmktLegConfig { role = "NQ", dataStream = 2 }).</summary>
		protected List<XmktLegConfig> XmktLegs { get; } = new List<XmktLegConfig>();
		private _XmktLeg[] m_xmkt;
		private _PriceActionEngine[] m_xmktPa;
		private bool m_xmktQueryWarned;

		/// <summary>M-XMKT leg by role, or null (not configured). Check Available before using a signal from it.</summary>
		internal _XmktLeg XmktLeg(string role)
		{
			if (m_xmkt != null) foreach (var l in m_xmkt) if (l.config.role == role) return l;
			if (!m_xmktQueryWarned) { m_xmktQueryWarned = true; if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, $"XmktLeg({role}) queried but no such leg is configured"); }
			return null;
		}

		/// <summary>M-XMKT leader swing engine of a leg (XmktLegConfig.leaderSwing), or null.</summary>
		internal _PriceActionEngine XmktLeaderSwing(string role)
		{
			if (m_xmkt != null) for (int i = 0; i < m_xmkt.Length; i++) if (m_xmkt[i].config.role == role) return m_xmktPa[i];
			return null;
		}

		/// <summary>2 x the bar size of a time-based resolution; zero (staleness off) for tick/volume-based bars.</summary>
		private static TimeSpan twoBars(Resolution r)
		{
			double s;
			switch (r.Type.ToString())
			{
				case "Second": s = r.Size; break;
				case "Minute": s = r.Size * 60.0; break;
				case "Hour": s = r.Size * 3600.0; break;
				case "Day": s = r.Size * 86400.0; break;
				case "Week": s = r.Size * 7 * 86400.0; break;
				default: return TimeSpan.Zero;
			}
			return TimeSpan.FromSeconds(2 * s);
		}

		private void startXmkt()
		{
			m_xmktQueryWarned = false;
			m_xmkt = null; m_xmktPa = null;
			if (XmktLegs.Count == 0) return;
			m_xmkt = new _XmktLeg[XmktLegs.Count]; m_xmktPa = new _PriceActionEngine[XmktLegs.Count];
			for (int i = 0; i < XmktLegs.Count; i++)
			{
				XmktLegConfig c = XmktLegs[i];
				requireDataStream("XmktLegs[" + c.role + "].dataStream", c.dataStream);
				IInstrumentSettings info = BarsOfData(c.dataStream).Info;
				var leg = new _XmktLeg { config = c, journal = engineSink(ETraceLevel.Journal),
					maxStale = c.maxStaleSec > 0 ? TimeSpan.FromSeconds(c.maxStaleSec) : twoBars(info.Resolution) };
				string err = leg.Configure();
				if (err != null) throw new ArgumentException(err);
				if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, $"XMKT leg {c.role} Data{c.dataStream} symbol({info.Name}) resolution({info.Resolution.Type} {info.Resolution.Size}) dataFeed({info.DataFeed}) maxStale({leg.maxStale})");
				if (leg.maxStale == TimeSpan.Zero && traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, $"XMKT leg {c.role}: non-time bars, staleness check off (set maxStaleSec)");
				if (c.leaderSwing)
				{
					var pc = new PriceActionConfig { enabled = true, dataStream = c.dataStream, tieRule = PriceActionConfig.tieRule, volumeIncludesCurrent = PriceActionConfig.volumeIncludesCurrent,
						strength = PriceActionConfig.strength, layers = PriceActionConfig.layers, candles = false };
					var pa = new _PriceActionEngine { config = pc, label = "PA" + c.dataStream + "-" + c.role, journal = engineSink(ETraceLevel.Journal), warnOnce = engineSink(ETraceLevel.Warning) };
					string perr = pa.Configure();
					if (perr != null) throw new ArgumentException(perr);
					m_xmktPa[i] = pa;
				}
				m_xmkt[i] = leg;
			}
		}

		private void stepXmkt()
		{
			for (int i = 0; i < m_xmkt.Length; i++)
			{
				int ds = m_xmkt[i].config.dataStream;
				IInstrument b = BarsOfData(ds);
				if (m_xmktPa[i] != null) m_xmktPa[i].Step(b);   // leader swing: the leg's own closed bars
				if (Bars.Status != EBarState.Close) continue;
				int idx = htfMapped(ds);
				if (idx >= 0) m_xmkt[i].Step(Bars.Time[0], Bars.Close[0], b.CurrentBar - idx, b.Time[idx], b.Close[idx]);
				else m_xmkt[i].Step(Bars.Time[0], Bars.Close[0], -1, DateTime.MinValue, double.NaN);
			}
		}

		// ---- M-TICK (§7): lazy DataLoader 1-tick Trade stream for the Data1 symbol; no tick chart stream (§6). ----

		/// <summary>M-TICK configuration; enabled=false (default) = no DataLoader request, TickStats null.</summary>
		protected TickConfig TickConfig { get; } = new TickConfig();
		private _TickStream m_tickStream;
		private IDataLoaderResult m_tickReq;
		private int m_tickLastBar = -1;

		/// <summary>M-TICK aggregate of the last closed Data1 bar, or null when M-TICK is off. Check valid; size fields stay
		/// parked (NaN / null) until TickConfig.sizeOutputsVerified (rev 2.7 §E6).</summary>
		internal TickBarStats TickStats { get { return m_tickStream != null ? m_tickStream.Last : null; } }

		private void startTick()
		{
			m_tickStream = null; m_tickReq = null; m_tickLastBar = -1;
			if (!TickConfig.enabled) return;
			var t = new _TickStream { config = TickConfig, journal = engineSink(ETraceLevel.Journal) };
			string err = t.Configure();
			if (err != null) throw new ArgumentException(err);
			m_tickStream = t;
			InstrumentDataRequest req = Bars.Request;   // same symbol / feed / session as Data1 (PG p72-73)
			req.Resolution = new Resolution(EResolution.Tick, 1);
			req.QuoteField = RequestQuoteField.Trade;
			req.Subscribe2RT = TickConfig.realTime;
			req.Range = new DataRequest { RequestType = DataRequestType.BarsBack, Count = TickConfig.historyTicks };
			m_tickReq = DataLoader.BeginLoadData(req, onTickData, null);
			if (traceOn(ETraceLevel.Journal)) trace(ETraceLevel.Journal, $"TICK DataLoader 1-tick Trade request for {Bars.Info.Name}, {TickConfig.historyTicks} ticks back, RT={TickConfig.realTime}");
		}

		/// <summary>DataLoader callback (separate thread, PG p72): buffer only.</summary>
		private void onTickData(IDataLoaderResult r)
		{
			_TickStream t = m_tickStream;
			if (t == null) return;
			switch (r.Event)
			{
				case DataLoadedEvent.History:
					if (r.Data != null) foreach (Bar b in r.Data) enqueueTick(t, b);
					if (r.IsCompleted) t.MarkHistoryComplete();
					break;
				case DataLoadedEvent.RTNewBar:
					if (r.RTData.HasValue) enqueueTick(t, r.RTData.Value);
					break;
			}
		}

		// Bar members are not named in the Programming Guide: read by reflection like VPF-5's flowTick (verify in the editor).
		private System.Reflection.MemberInfo m_tickBarTime, m_tickBarClose, m_tickBarSize;
		private void enqueueTick(_TickStream t, Bar b)
		{
			object o = b;
			t.Enqueue((DateTime)tickBarMember(o, ref m_tickBarTime, "Time"), Convert.ToDouble(tickBarMember(o, ref m_tickBarClose, "Close")),
				Convert.ToDouble(tickBarMember(o, ref m_tickBarSize, "TotalVolume", "Volume", "Ticks")));
		}
		private static object tickBarMember(object o, ref System.Reflection.MemberInfo cache, params string[] names)
		{
			if (cache == null)
				foreach (string n in names)
				{
					cache = (System.Reflection.MemberInfo)o.GetType().GetProperty(n) ?? o.GetType().GetField(n);
					if (cache != null) break;
				}
			if (cache is System.Reflection.PropertyInfo) return ((System.Reflection.PropertyInfo)cache).GetValue(o, null);
			if (cache is System.Reflection.FieldInfo) return ((System.Reflection.FieldInfo)cache).GetValue(o);
			throw new InvalidOperationException("M-TICK: Bar has none of " + string.Join("/", names));
		}

		private void stepTick()
		{
			if (Bars.Status != EBarState.Close || Bars.CurrentBar == m_tickLastBar) return;
			m_tickLastBar = Bars.CurrentBar;
			DateTime start = Bars.CurrentBar > 1 ? Bars.Time[1] : Bars.Time[0] - Bars.Info.Resolution.Duration();
			m_tickStream.Aggregate(start, Bars.Time[0]);
		}

		private void stopTick()
		{
			if (m_tickReq != null) { try { DataLoader.EndLoadData(m_tickReq); } catch (Exception) { } }
			m_tickReq = null; m_tickStream = null;
		}

		// ---- M-MTF (§3): higher timeframes from extra chart series. Default for every HTF rule: closed bars only (VPB-8). ----

		/// <summary>M-MTF: HTF streams this strategy reads (e.g. { 2 } for Data2 = ES 60-min). Empty (default) = no checks.
		/// StartCalc fails loudly if a stream is missing or carries another instrument root (§3 rule 6).</summary>
		protected int[] HtfStreams { get; set; } = new int[0];
		private DateTime[] m_htfMappedSession;
		private bool m_htfFormingWarned;

		/// <summary>M-MTF rule 1: the bar of stream ds that is the ago-th CLOSED bar (bar 0 when it is closed, else bar 1), as a bars-ago index.</summary>
		protected int HtfClosedIndex(int ds, int ago = 0) { return _MtfMath.ClosedIndex(BarsOfData(ds).Status == EBarState.Close, ago); }
		/// <summary>M-MTF: close of the ago-th closed bar of stream ds.</summary>
		protected double HtfClosed(int ds, int ago = 0) { return BarsOfData(ds).Close[HtfClosedIndex(ds, ago)]; }
		/// <summary>M-MTF: the ago-th closed bar of stream ds (time, OHLCV).</summary>
		protected void HtfClosedBar(int ds, int ago, out DateTime time, out double open, out double high, out double low, out double close, out double volume)
		{
			IInstrument b = BarsOfData(ds); int i = _MtfMath.ClosedIndex(b.Status == EBarState.Close, ago);
			time = b.Time[i]; open = b.Open[i]; high = b.High[i]; low = b.Low[i]; close = b.Close[i]; volume = b.Volume[i];
		}

		/// <summary>M-MTF: explicit access to the FORMING bar 0 of stream ds (values change intrabar). Journaled once per run.</summary>
		protected double HtfForming(int ds)
		{
			if (!m_htfFormingWarned) { m_htfFormingWarned = true; if (traceOn(ETraceLevel.Journal)) trace(ETraceLevel.Journal, $"MTF forming-bar access on Data{ds} (not closedBarsOnly)"); }
			return BarsOfData(ds).Close[0];
		}

		/// <summary>
		/// M-MTF rule 3: bars-ago index of the latest closed bar of stream ds with Time &lt;= Data1 time, or -1 if none within maxBack.
		/// The first mapping per session is a Journal fact. Whether MC stamps HTF bars with their end time is Probe P2.
		/// </summary>
		protected int htfMapped(int ds, int maxBack = 2)
		{
			IInstrument b = BarsOfData(ds); DateTime t = Bars.Time[0];
			int i = _MtfMath.MapIndex(k => b.Time[k], _MtfMath.ClosedIndex(b.Status == EBarState.Close, 0), t, maxBack);
			if (m_htfMappedSession != null && ds < m_htfMappedSession.Length && Bars.Status == EBarState.Close)
			{
				DateTime day = tradingDayKey(t);
				if (m_htfMappedSession[ds] != day)
				{
					m_htfMappedSession[ds] = day;
					if (traceOn(ETraceLevel.Journal)) trace(ETraceLevel.Journal, i < 0 ? $"MTF Data{ds} first mapping {t:yyyy-MM-dd HH:mm:ss}: no closed bar <= Data1 time"
						: $"MTF Data{ds} first mapping {t:yyyy-MM-dd HH:mm:ss} -> {b.Time[i]:yyyy-MM-dd HH:mm:ss} (ago {i}, status {b.Status})");
				}
			}
			return i;
		}

		private void startMtf()
		{
			m_htfFormingWarned = false;
			m_htfMappedSession = new DateTime[MaxDataStream + 1];
			if (HtfStreams == null) return;
			string root = _MtfMath.SymbolRoot(Bars.Info.Name);
			foreach (int ds in HtfStreams)
			{
				requireDataStream("HtfStreams", ds);
				IInstrumentSettings info = BarsOfData(ds).Info;
				if (_MtfMath.SymbolRoot(info.Name) != root)
					throw new ArgumentException($"HtfStreams: Data{ds} is {info.Name}, Data1 is {Bars.Info.Name}; an HTF stream must carry the Data1 instrument (use XmktLegs for other markets)");
			}
		}

		/// <summary>§0.3: true on the first bar of a new contract on stream ds (unadjusted continuous futures). The base never
		/// detects rolls; _VolumeProfileFuturesBase overrides it with the VPF-4 detector (Data1 only).</summary>
		protected virtual bool rollOn(int ds) { return false; }

		/// <summary>Sink for engine Journal facts: null when the level is filtered, so the engine formats no strings (§0.2).</summary>
		protected Action<string> engineSink(ETraceLevel level)
		{
			if (!traceOn(level)) return null;
			return s => trace(level, s);
		}

		private void startEngines()
		{
			m_paQueryWarned = false;
			m_pa = null;
			m_avwapQueryWarned = false;
			m_avwap = null;
			m_avwapPa = null;
			bool avwapPa = (AvwapEvents & (EAvwapAnchor.Swing | EAvwapAnchor.Breakout)) != 0;
			if (PriceActionConfig.enabled || priceActionRequired() || (avwapPa && AvwapLimits.dataStream == PriceActionConfig.dataStream))
			{
				if (!PriceActionConfig.enabled) PriceActionConfig.enabled = true;   // lazy dependent: candles/settings as configured
				requireDataStream("PriceActionConfig.dataStream", PriceActionConfig.dataStream);
				var pa = new _PriceActionEngine { config = PriceActionConfig, label = "PA" + PriceActionConfig.dataStream,
					trace = engineSink(ETraceLevel.Debug), journal = engineSink(ETraceLevel.Journal), warnOnce = engineSink(ETraceLevel.Warning) };
				string err = pa.Configure();   // validate once; report, never clamp
				if (err != null) throw new ArgumentException(err);
				m_pa = pa;
			}
			startTod();
			startDaily();
			startXmkt();
			startTick();
			if (AvwapEvents != EAvwapAnchor.None) startAvwap(avwapPa);
			startMtf();
		}

		private void startAvwap(bool needsPa)
		{
			requireDataStream("AvwapLimits.dataStream", AvwapLimits.dataStream);
			if (needsPa)
			{
				if (m_pa != null && PriceActionConfig.dataStream == AvwapLimits.dataStream) m_avwapPa = m_pa;
				else
				{
					// §0.1 rule 3: lazy dependency on the manager's stream; candles off unless the strategy enabled them on that stream
					var cfg = new PriceActionConfig { enabled = true, dataStream = AvwapLimits.dataStream, tieRule = PriceActionConfig.tieRule,
						volumeIncludesCurrent = PriceActionConfig.volumeIncludesCurrent, strength = PriceActionConfig.strength, candles = (AvwapEvents & EAvwapAnchor.Breakout) != 0 };
					m_avwapPa = new _PriceActionEngine { config = cfg, label = "PA" + cfg.dataStream + "-AV", journal = engineSink(ETraceLevel.Journal), warnOnce = engineSink(ETraceLevel.Warning) };
					string perr = m_avwapPa.Configure();
					if (perr != null) throw new ArgumentException(perr);
				}
				if (m_avwapPa.config.pivotMode == EPivotMode.Fractal) AvwapLimits.backfillBars = Math.Max(AvwapLimits.backfillBars, m_avwapPa.config.strength + 1);
			}
			var m = new _AvwapManager { limits = AvwapLimits, events = AvwapEvents, tickSize = tickSize(), label = "AV" + AvwapLimits.dataStream,
				journal = engineSink(ETraceLevel.Journal), trace = engineSink(ETraceLevel.Debug), warnOnce = engineSink(ETraceLevel.Warning) };
			if (m_tod != null && AvwapLimits.dataStream == 1) { _TodNormaliser tod = m_tod; m.volumeZ = () => tod.Z(ETodMetric.Volume); }   // NaN while not valid -> 20-bar fallback
			if (m_daily != null) { _DailyContext daily = m_daily; m.dailyAtr = () => daily.Atr20d; }   // GapOpen: 0.5 x ATR20d when valid, else the SMA-TR fallback
			string err = m.Configure();
			if (err != null) throw new ArgumentException(err);
			m_avwap = m;
		}

		private void stepAvwap()
		{
			IInstrument bars = BarsOfData(AvwapLimits.dataStream);
			if (bars.Status != EBarState.Close || bars.CurrentBar == m_avwap.LastBar) return;
			bool roll = rollOn(AvwapLimits.dataStream);
			var ev = new AvwapPaEvents();
			if (m_avwapPa != null)
			{
				if (m_avwapPa != m_pa) { if (roll) m_avwapPa.OnRoll(bars.Time[0]); m_avwapPa.Step(bars); }
				if (m_avwapPa.LastBarTime == bars.Time[0])
				{
					_Pivot ph = m_avwapPa.NewPivotHigh ? m_avwapPa.LastHigh(0) : null, pl = m_avwapPa.NewPivotLow ? m_avwapPa.LastLow(0) : null;
					if (ph != null) { ev.swingHigh = true; ev.swingHighBar = ph.extremeBar; ev.swingHighTime = ph.extremeTime; }
					if (pl != null) { ev.swingLow = true; ev.swingLowBar = pl.extremeBar; ev.swingLowTime = pl.extremeTime; }
					ev.breakoutUp = m_avwapPa.BosUp || m_avwapPa.Has(ECandle.BreakoutUp);
					ev.breakoutDown = m_avwapPa.BosDown || m_avwapPa.Has(ECandle.BreakoutDown);
				}
			}
			if (roll) m_avwap.OnRoll(bars.Time[0]);
			m_avwap.Add(new AvwapBar { bar = bars.CurrentBar, time = bars.Time[0], o = bars.Open[0], h = bars.High[0], l = bars.Low[0], c = bars.Close[0], v = bars.Volume[0],
				sessionDate = tradingDayKey(bars.Time[0]), rth = isRthBar(bars.Time[0]) }, ev);
		}

		/// <summary>Steps the enabled engines once per CalcBar; each engine commits on its own stream's closed bar only.</summary>
		private void stepEngines()
		{
			if (m_pa != null)
			{
				IInstrument bars = BarsOfData(PriceActionConfig.dataStream);
				if (bars.Status == EBarState.Close && rollOn(PriceActionConfig.dataStream)) m_pa.OnRoll(bars.Time[0]);
				m_pa.Step(bars);
			}
			if (m_tickStream != null) stepTick();
			if (m_xmkt != null) stepXmkt();
			if (m_daily != null) stepDaily();   // before AVWAP: GapOpen reads the previous day's ATR
			if (m_tod != null) stepTod();       // before AVWAP: VolumeClimax reads this bar's clock-minute z
			if (m_avwap != null) stepAvwap();   // after PRICEACTION so the shared instance has this bar's pivots
		}

		private void stopEngines()
		{
			m_pa = null;
			m_avwap = null;
			m_avwapPa = null;
			m_tod = null;
			m_daily = null;
			m_xmkt = null;
			m_xmktPa = null;
			stopTick();
		}

		protected override void StopCalc()
		{
			stopEngines();   // §0.1 rule 6: re-calc starts clean
			base.StopCalc();
		}

		protected override void Create()
		{
			base.Create();
			m_holidays = new PowerLanguage.Function._NyseHolidays(this);
			TraceLevel = ETraceLevel.None;
		}

		/// <summary>
		/// Static state (log file, TradeManager snapshot) is process-wide and shared by every signal instance.
		/// The log file handle is closed on process exit/domain unload.
		/// </summary>
		protected override void Destroy()
		{
			if (m_journal != null) m_journal.Close();
			m_journal = null;
			if (m_eventFile != null) m_eventFile.Close();   // CS-8
			m_eventFile = null;
			m_logger = null;
			base.Destroy();
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_currentDay = DateTime.MinValue; // make sure that current day is always set on the first CalcBar iteration
			m_intradayLowTime = DateTime.MinValue;    // CS-9: no PC-clock seed; MinValue = not set yet
			m_intradayHighTime = DateTime.MinValue;
			m_intradayCloseTime = DateTime.MinValue;
			m_intradayOpenTime = DateTime.MinValue;
			m_intradayBarCount = 0;
			m_intradayLow = double.MaxValue;
			m_intradayHigh = double.MinValue;
			m_intradayClose = 0.0d;
			m_intradayOpen = 0.0d;
			m_intradayVolume = 0.0d;
			m_loadAccounts = true;
			m_unknownAppWarned = false;
			m_fractionWarned = false;
			m_barTz = null;   // CS-6
			m_tmDay = DateTime.MinValue; m_tmEntrySeen.Clear(); m_tmExitSeen.Clear(); m_tmEntries = m_tmLosers = 0; m_tmNet = 0;   // CS-7
			m_volumeMbbWarned = false;
			m_tradingDayWarned = false;
			m_entryCacheBar = -1;
			m_entryCacheSnapshot = 0;
			m_runId = Guid.NewGuid().ToString("N").Substring(0, 8);   // CS-3
			m_runLogged = false;
			m_runEndLogged = false;
			m_tick = 0;   // tickSize() re-resolved on first use
			if (m_journal != null) m_journal.Close();
			m_journal = null;                                   // CS-1: one file per run
			m_journalArms = new Dictionary<string, JournalArm>(StringComparer.Ordinal);
			m_journalWritten = new HashSet<string>(StringComparer.Ordinal);
			if (m_eventFile != null) m_eventFile.Close();      // CS-8: one events file per run
			m_eventFile = null;
			m_events.Clear();
			m_eventsSteppedBar = -1;
			//NOTE: Based on real-time history matching the zero index in the bars can change. When real-time history matching is
			//      enabled 0-index is the previous fully formed day and when it is disabled 0-index is the current forming day.

			//force load the data streams up front so we don't get into exception breaks somewhere down the line
			for (int i = 1; i <= MaxDataStream; ++i)
			{
				if (IsExist(i))
				{
					_ = BarsOfData(i);
				}
			}
			startEngines();   // lazy engines (§0.1); null unless enabled
		}

		/// <summary>
		/// Assumes that Bars is a minute bar and that the first bar of the day is the open. The first bar of the day is used to set the
		/// </summary>
		protected override void CalcBar()
		{
			// CS-3: one RUN line per run on the first calculated bar (Info level and above)
			if (!m_runLogged)
			{
				m_runLogged = true;
				if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, runLine());
			}

			//ensure latest account equity is available, this method seems to kick off async internal processes
			//to update interface elements like the Accounts etc, this can be deduced from how the interface looks
			//with callbacks like OnRefreshComplete etc. If you call it right before accessing the account data
			//those accounts would not be set and your results would be incorrect so initially we keep on calling
			//the CalcBar method until we have accounts loaded
			if (Environment.IsAutoTradingMode)
			{
				refreshTradeManagerIfNeeded();

				//force recalculation after a specific interval if account information is not yet loaded from the above ProcessEvents call
				if (m_loadAccounts)
				{
					if (tradingData.Accounts.Items.Length == 0)
					{
						trace(ETraceLevel.Warning, "CalcBar - ProcessEvents failed to load account queuing recalculation.");
						ExecControl.RecalcLastBarAfter(TimeSpan.FromSeconds(DEFAULT_ACCOUNT_RECALC_INTERVAL));
					}
					else
					{
						trace(ETraceLevel.Info, $"CalcBar - Loaded {tradingData.Accounts.Items.Length} accounts from broker.");
						m_loadAccounts = false;
					}
				}
			}

			// FunctionSimple.Value executes _NyseHolidays.CalcBar for this bar; it rebuilds the holiday list on a year change.
			// Must run every bar before isHoliday() is used.
			int _ = m_holidays.Value;

			if (Environment.CalcReason == CalculationReason.Default)
			{
				DateTime dayKey = tradingDayKey(Bars.Time[0]);
				if (dayKey != m_currentDay.Date)
				{
					m_intradayBarCount = 0;
					m_intradayOpen = Bars.Open[0];
					m_intradayClose = Bars.Close[0];
					m_intradayLow = Bars.Low[0];
					m_intradayHigh = Bars.High[0];
					m_intradayVolume = 0.0; // accumulated only on bar close below
					m_currentDay = dayKey;
					m_intradayLowTime = Bars.Time[0];
					m_intradayHighTime = Bars.Time[0];
					m_intradayCloseTime = Bars.Time[0];
					m_intradayOpenTime = Bars.Time[0];
				}
				else
				{
					if (m_intradayLow > Bars.Low[0])
					{
						m_intradayLow = Bars.Low[0];
						m_intradayLowTime = Bars.Time[0];
					}

					if (m_intradayHigh < Bars.High[0])
					{
						m_intradayHighTime = Bars.Time[0];
						m_intradayHigh = Bars.High[0];
					}
				}

				if (Bars.Status == EBarState.Close)
				{
					m_intradayBarCount++;
					m_intradayClose = Bars.Close[0];
					m_intradayCloseTime = Bars.Time[0];
					m_intradayVolume += Bars.Volume[0];
				}
			}

			stepEngines();   // lazy engines (§0.1): no-op unless enabled

			// CS-1: journal closed trades once per closed bar (only at ETraceLevel.Journal and above)
			if (Bars.Status == EBarState.Close && traceOn(ETraceLevel.Journal))
				journalScan();
			signalEventsStep();   // CS-8 (no pending events unless logSignal was called at Journal level)

			// CS-3: RUN-END once, on the last historical bar (end of the backtest data range)
			if (!m_runEndLogged && Bars.LastBarOnChart && Bars.Status == EBarState.Close)
			{
				m_runEndLogged = true;
				if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, $"RUN-END id({RunId}) lastBar({Bars.Time[0]:yyyy-MM-dd HH:mm:ss}) barNumber({Bars.FullSymbolData.Current}) realTime({Environment.IsRealTimeCalc})");
			}
		}

		/// <summary>
		/// CS-3: RUN line - run id, build tag, strategy type, symbol, resolution, first calculated bar and every [Input] value
		/// (reflection over public instance properties carrying an attribute named InputAttribute, sorted by name).
		/// </summary>
		protected string runLine()
		{
			IInstrumentSettings info = Bars.Info;
			return $"RUN id({RunId}) build({BuildTag}) strategy({this.GetType().Name}) symbol({info.Name}) category({info.Category}) " +
				$"resolution({info.Resolution.ChartType} {info.Resolution.Type} {info.Resolution.Size}) timeZone({info.TimeZone}) " +
				$"firstBar({Bars.Time[0]:yyyy-MM-dd HH:mm:ss}) barNumber({Bars.FullSymbolData.Current}) app({Environment.ApplicationCode}) " +
				$"inputs[{inputSnapshot()}]";
		}

		/// <summary>CS-3: "Name=value; ..." for every [Input] property of the concrete strategy, invariant culture.</summary>
		protected string inputSnapshot()
		{
			var sb = new StringBuilder();
			System.Reflection.PropertyInfo[] props = this.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			Array.Sort(props, (a, b) => string.CompareOrdinal(a.Name, b.Name));
			string last = null;
			foreach (System.Reflection.PropertyInfo p in props)
			{
				if (p.Name == last || !p.CanRead || p.GetIndexParameters().Length != 0 || !isInputProperty(p)) continue;
				last = p.Name;
				object value;
				try { value = p.GetValue(this, null); }
				catch (Exception e) { value = "<" + e.GetType().Name + ">"; }
				if (sb.Length > 0) sb.Append("; ");
				sb.Append(p.Name).Append('=').Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
			}
			return sb.ToString();
		}

		// Matched by name so the check does not depend on the attribute's namespace; Attribute.GetCustomAttributes honours
		// inherit for properties (PropertyInfo.GetCustomAttributes ignores it), so overridden inputs are still found.
		private static bool isInputProperty(System.Reflection.PropertyInfo p)
		{
			foreach (Attribute a in Attribute.GetCustomAttributes(p, true))
				if (a.GetType().Name == "InputAttribute") return true;
			return false;
		}

		/// <summary>
		/// Minimum price move of Data1 (VPB-20, moved from _VolumeProfileBase by CS-1). Resolved once per StartCalc, lazily.
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
			return TICK_FALLBACK;
		}

		// ---- CS-1: per-trade journal (gap-only) ----

		/// <summary>True when the per-trade journal is on (TraceLevel Journal, Verbose or Debug).</summary>
		protected bool journalOn() { return traceOn(ETraceLevel.Journal); }

		/// <summary>
		/// CS-1: record what the strategy knows at order send for the entry order named <paramref name="entryOrderName"/>
		/// (exact name match when the trade is written). Call it next to Send(); a later call for the same name replaces it.
		/// Cheap no-op when the journal is off. Context values are written as key=value pairs separated by '|'.
		/// </summary>
		protected void journalArm(string entryOrderName, double plannedEntry, double initialStop, double target, IDictionary<string, string> context = null)
		{
			if (!journalOn() || string.IsNullOrEmpty(entryOrderName) || m_journalArms == null) return;
			string ctx = "";
			if (context != null && context.Count > 0)
			{
				var sb = new StringBuilder();
				foreach (KeyValuePair<string, string> kv in context)
				{
					if (sb.Length > 0) sb.Append('|');
					sb.Append(kv.Key).Append('=').Append(kv.Value);
				}
				ctx = sb.ToString();
			}
			m_journalArms[entryOrderName] = new JournalArm { tag = entryOrderName, plannedEntry = plannedEntry, initialStop = initialStop, target = target, context = ctx };
		}

		// Positions[0] is the current position; [1].. are earlier ones. Scanning the newest three on every closed bar and
		// de-duplicating by trade identity does not depend on whether a flat CurrentPosition is the closed one or a new empty one.
		private void journalScan()
		{
			if (m_journalWritten == null) return;
			int n = Math.Min(Positions.Count, 3);
			for (int i = n - 1; i >= 0; i--)
			{
				IMarketPosition pos = Positions[i];
				if (pos == null || pos.ClosedTrades == null || pos.ClosedTrades.Count == 0) continue;
				if (pos.OpenTrades != null && pos.OpenTrades.Count > 0) continue;   // write a position once it is flat (scale-outs included)
				int tradeCount = pos.ClosedTrades.Count;
				foreach (var t in pos.ClosedTrades)
				{
					if (t == null || t.EntryOrder == null || t.ExitOrder == null) continue;
					string key = t.EntryOrder.Name + "\u001f" + t.EntryOrder.BarNumber + "\u001f" + t.EntryOrder.Time.Ticks + "\u001f" +
						t.ExitOrder.Name + "\u001f" + t.ExitOrder.BarNumber + "\u001f" + t.ExitOrder.Time.Ticks + "\u001f" + t.ExitOrder.Contracts;
					if (!m_journalWritten.Add(key)) continue;
					journalWriteTrade(pos, t.IsLong, t.EntryOrder.Name, t.EntryOrder.Time, t.EntryOrder.Price, t.EntryOrder.BarNumber,
						t.ExitOrder.Name, t.ExitOrder.Time, t.ExitOrder.Price, t.ExitOrder.BarNumber, t.ExitOrder.Contracts,
						t.Profit, t.CommissionValue, tradeCount);
				}
			}
		}

		private void journalWriteTrade(IMarketPosition pos, bool isLong, string entryName, DateTime entryTime, double entryPrice, int entryBar,
			string exitName, DateTime exitTime, double exitPrice, int exitBar, int qty, double profit, double commission, int tradesInPosition)
		{
			System.Globalization.CultureInfo ic = System.Globalization.CultureInfo.InvariantCulture;
			double bpv = Bars.Info.BigPointValue;
			double tick = tickSize();
			JournalArm arm;
			bool armed = m_journalArms.TryGetValue(entryName ?? "", out arm);

			// What MC lacks: initial risk from the armed stop, R, excursions in ticks/R, planned-vs-fill.
			double riskPerUnit = armed ? Math.Abs(entryPrice - arm.initialStop) : double.NaN;
			double riskDollars = armed && bpv > 0 ? riskPerUnit * bpv * qty : double.NaN;
			double rMultiple = riskDollars > 0 ? profit / riskDollars : double.NaN;
			double slipTicks = armed && tick > 0 ? (isLong ? entryPrice - arm.plannedEntry : arm.plannedEntry - entryPrice) / tick : double.NaN;   // > 0 = adverse

			// MC exposes run-up/drawdown per position, not per trade: with several trades in one position the excursion is the position's.
			double mfeDollars = Math.Abs(pos.MaxRunUp);
			double maeDollars = -Math.Abs(pos.MaxDrawDown);
			double perTick = bpv > 0 && tick > 0 && qty > 0 ? bpv * tick * qty : double.NaN;
			double mfeTicks = mfeDollars / perTick, maeTicks = maeDollars / perTick;
			double mfeR = riskDollars > 0 ? mfeDollars / riskDollars : double.NaN;
			double maeR = riskDollars > 0 ? maeDollars / riskDollars : double.NaN;
			string scope = tradesInPosition > 1 ? "position(" + tradesInPosition + " trades)" : "trade";

			var sb = new StringBuilder();
			Action<string> col = s => { if (sb.Length > 0) sb.Append(','); sb.Append(csvField(s)); };
			Func<double, string> num = v => double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString("0.########", ic);
			col(RunId); col(this.GetType().Name); col(Bars.Info.Name); col(armed ? arm.tag : entryName); col(isLong ? "Long" : "Short");
			col(qty.ToString(ic));
			col(entryTime.ToString("yyyy-MM-dd HH:mm:ss", ic)); col(num(entryPrice));
			col(exitTime.ToString("yyyy-MM-dd HH:mm:ss", ic)); col(num(exitPrice)); col(exitName);
			col((exitBar - entryBar).ToString(ic));
			col(num(profit)); col(num(commission)); col(armed ? "1" : "0");
			col(armed ? num(arm.plannedEntry) : ""); col(num(slipTicks)); col(armed ? num(arm.initialStop) : ""); col(armed ? num(arm.target) : "");
			col(num(riskPerUnit)); col(num(tick > 0 ? riskPerUnit / tick : double.NaN)); col(num(riskDollars)); col(num(rMultiple));
			col(num(mfeDollars)); col(num(maeDollars)); col(num(mfeTicks)); col(num(maeTicks)); col(num(mfeR)); col(num(maeR));
			col(scope); col(armed ? arm.context : "");

			try
			{
				if (m_journal == null)
				{
					m_journal = new TradeJournal(RunId);
					trace(ETraceLevel.Journal, $"JOURNAL file({m_journal.Path_})");
				}
				m_journal.Write(sb.ToString());
			}
			catch (Exception e)
			{
				trace(ETraceLevel.Warning, $"JOURNAL write failed - {e.Message}");
			}
		}

		private static string csvField(string s)
		{
			if (string.IsNullOrEmpty(s)) return "";
			if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
			return "\"" + s.Replace("\"", "\"\"") + "\"";
		}

		// ---- CS-3: input validation helpers (moved from _VolumeProfileBase; generic parameter hygiene) ----
		protected static void requireUnitInterval(string name, double value)
		{
			if (value < 0.0 || value > 1.0)
				throw new ArgumentException(name + " must be between 0.0 and 1.0.");
		}

		protected static void requireAtLeast(string name, int value, int min)
		{
			if (value < min)
				throw new ArgumentException(name + " must be " + min + " or greater.");
		}

		protected static void requireNonNegative(string name, double value)
		{
			if (value < 0.0)
				throw new ArgumentException(name + " must be 0.0 or greater.");
		}

		protected static void requireNonNegative(string name, int value)
		{
			if (value < 0)
				throw new ArgumentException(name + " must be 0 or greater.");
		}

		protected void requireDataStream(string name, int stream)
		{
			if (stream < 1)
				throw new ArgumentException(name + " must be a chart data stream index >= 1 (1 = Data1, 2 = Data2, …).");
			if (MaxDataStream >= 1 && stream > MaxDataStream)
				throw new ArgumentException(name + " = " + stream + " is not on this chart (MaxDataStream = " + MaxDataStream + ").");
		}

		/// <summary>
		/// Trading-day key for a bar. Equities: calendar date. Futures override with GlobexDate.
		/// </summary>
		protected virtual DateTime tradingDayKey(DateTime barTime)
		{
			if (TradingDayRoll == ETradingDayRoll.Auto) return autoTradingDay(barTime, Bars.Info.Category.ToString());   // CS-6
			return barTime.Date;
		}

		/// <summary>
		/// Determines the equity available for trading a position based on some custom risk equity per position percentage. If
		/// the strategy is run on a chart the whole equity is used if no custom equity percentage is given. If the strategy is
		/// is run in the portfolio manager the portfolio percentage per position is used if no custom equity percentage is given.
		/// </summary>
		protected double equityAvailable(double maxCustomRiskEquityPerPosPercent = 0.0d)
		{
			double equityAvailable = 0.0d;
			m_lastEquityFromAccount = false;
			// CS-4: the *Percent parameters are fractions (0.01 = 1%). Warn once on a value that looks like a percentage; no behaviour change.
			if (maxCustomRiskEquityPerPosPercent > 1.0 && !m_fractionWarned)
			{
				m_fractionWarned = true;
				trace(ETraceLevel.Warning, $"equityAvailable - fraction {maxCustomRiskEquityPerPosPercent} > 1: the *Percent sizing parameters are fractions (0.01 = 1%)");
			}

			//get equity available from account for trading if we are auto trading
			if (Environment.IsAutoTradingMode)
			{
				if (traceOn(ETraceLevel.Verbose)) trace(ETraceLevel.Verbose, $"equityAvailable - Checking {tradingData.Accounts.Items.Length} accounts to find available equity on signal account {this.Account}.");
				foreach (var account in tradingData.Accounts.Items)
				{
					if (traceOn(ETraceLevel.Verbose)) trace(ETraceLevel.Verbose, $"equityAvailable - Found account {account.Name} with equity {account.Equity ?? 0}.");
					if (account.Name == this.Account)
					{
						equityAvailable = account.Equity ?? 0;
						m_lastEquityFromAccount = equityAvailable != 0.0d;
						break;
					}
				}
			}

			//NOTE: This only works for single entries, need to see how this will work for multiple entries.
			switch (Environment.ApplicationCode)
			{
				case EApplicationCode.Charting:
					if (equityAvailable != 0.0d)
					{
						trace(ETraceLevel.Verbose, $"equityAvailable - Equity determined from account - {equityAvailable}");
						equityAvailable = maxCustomRiskEquityPerPosPercent > 0.0d ? equityAvailable * maxCustomRiskEquityPerPosPercent : equityAvailable;
					}
					else
					{
						equityAvailable = this.InitialCapital + this.NetProfit;
						equityAvailable = maxCustomRiskEquityPerPosPercent > 0.0d ? equityAvailable * maxCustomRiskEquityPerPosPercent : equityAvailable;
					}
					break;
				case EApplicationCode.Portfolio:
					double percentEquity = maxCustomRiskEquityPerPosPercent > 0.0d ? maxCustomRiskEquityPerPosPercent : this.Portfolio.MaxRiskEquityPerPosPercent / 100.0d;
					if (equityAvailable != 0.0d)
					{
						trace(ETraceLevel.Verbose, $"equityAvailable - Equity determined from account - {equityAvailable}");
						equityAvailable *= percentEquity;
					}
					else
					{
						equityAvailable = this.InitialCapital + this.Portfolio.NetProfit;
						equityAvailable *= percentEquity;
					}
					break;
				default:
					if (equityAvailable == 0.0d) equityAvailable = this.InitialCapital + this.NetProfit;
					equityAvailable = maxCustomRiskEquityPerPosPercent > 0.0d ? equityAvailable * maxCustomRiskEquityPerPosPercent : equityAvailable;
					if (!m_unknownAppWarned)
					{
						m_unknownAppWarned = true;
						trace(ETraceLevel.Warning, $"equityAvailable - unhandled ApplicationCode {Environment.ApplicationCode}, using chart equity");
					}
					break;
			}

			return equityAvailable;
		}

		// ---- CS-6: trading-day roll by asset class and generic time-zone helpers ----

		/// <summary>CS-6: how tradingDayKey rolls the day. Calendar (default) = bar date, today's behaviour. Auto = by Bars.Info.Category.</summary>
		public enum ETradingDayRoll { Calendar = 0, Auto = 1 }

		/// <summary>
		/// CS-6: Calendar (default, no change) or Auto: Forex = date of (New York time + 7h), i.e. the 17:00 NY roll; Crypto = UTC
		/// date; anything else (Stock, ...) = bar date. Futures bases override tradingDayKey (Globex date). Not an [Input].
		/// </summary>
		protected ETradingDayRoll TradingDayRoll { get; set; } = ETradingDayRoll.Calendar;

		private TimeZoneInfo m_barTz;

		/// <summary>CS-6: zone of this chart's bar times (resolved once per run from Bars.Info.TimeZone).</summary>
		protected TimeZoneInfo BarTimeZone
		{
			get { if (m_barTz == null) m_barTz = resolveBarTimeZone(Bars.Info); return m_barTz; }
		}

		/// <summary>CS-6: bar time (chart zone) converted to the zone with Windows or IANA id <paramref name="tzId"/>. DST-aware.</summary>
		protected DateTime ToZone(DateTime barTime, string tzId)
		{
			TimeZoneInfo dst = findTz(tzId);
			if (barTime.Kind == DateTimeKind.Utc) return TimeZoneInfo.ConvertTimeFromUtc(barTime, dst);
			DateTime t = DateTime.SpecifyKind(barTime, DateTimeKind.Unspecified);
			TimeZoneInfo src = BarTimeZone ?? TimeZoneInfo.Local;
			if (src.Id == dst.Id) return t;
			if (src.IsInvalidTime(t)) t = t.AddHours(1);   // spring-forward gap
			return TimeZoneInfo.ConvertTime(t, src, dst);
		}

		/// <summary>CS-6: current bar time in <paramref name="tzId"/> inside [start, end); a window with end &lt;= start wraps midnight.</summary>
		protected bool inTimeWindow(TimeSpan start, TimeSpan end, string tzId)
		{
			TimeSpan t = ToZone(Bars.Time[0], tzId).TimeOfDay;
			return end > start ? t >= start && t < end : t >= start || t < end;
		}

		/// <summary>CS-6: tradingDayKey under ETradingDayRoll.Auto for a category name (Forex / Crypto*; else calendar date).</summary>
		protected DateTime autoTradingDay(DateTime barTime, string category)
		{
			string c = (category ?? "").ToUpperInvariant();
			if (c.Contains("FOREX") || (c.Contains("CURRENCY") && !c.Contains("CRYPTO"))) return ToZone(barTime, "Eastern Standard Time").AddHours(7).Date;
			if (c.Contains("CRYPTO")) return ToZone(barTime, "UTC").Date;
			return barTime.Date;
		}

		/// <summary>
		/// VPF-01 / CS-6 (moved from _VolumeProfileFuturesBase). Zone of the bar timestamps from the instrument settings (PR #1 review: no input parameter).
		/// </summary>
		protected TimeZoneInfo resolveBarTimeZone(IInstrumentSettings info)
		{
			switch (info.TimeZone)
			{
				case RequestTimeZone.Local:
					return TimeZoneInfo.Local;
				case RequestTimeZone.GMT:
					return TimeZoneInfo.Utc;
				case RequestTimeZone.Exchange:
					string id = exchangeTimeZoneId(info.Exchange);
					if (id == null)
						throw new Exception("Exchange '" + info.Exchange + "' has no known time zone: set the chart to Local time or override exchangeTimeZoneId.");
					return findTz(id);
				default:
					return TimeZoneInfo.Local;
			}
		}

		/// <summary>
		/// Windows time zone id of an exchange's Exchange-time bars, or null when unknown. Matching is on the exchange name as
		/// the data feed reports it (case-insensitive, substring). Override to add feeds/exchanges.
		/// </summary>
		protected virtual string exchangeTimeZoneId(string exchange)
		{
			if (string.IsNullOrWhiteSpace(exchange)) return null;
			string e = exchange.ToUpperInvariant();
			// Chicago: CME Group's CME/CBOT (incl. GLOBEX, ECBOT, CBT) exchange time
			if (e.Contains("CME") || e.Contains("CBOT") || e.Contains("CBT") || e.Contains("GLOBEX") || e.Contains("CBOE")) return "Central Standard Time";
			// New York: NYMEX/COMEX and the US equity exchanges
			if (e.Contains("NYMEX") || e.Contains("COMEX") || e.Contains("NYSE") || e.Contains("NASDAQ") || e.Contains("ARCA")) return "Eastern Standard Time";
			return null;
		}

		protected static TimeZoneInfo findTz(string windowsId)
		{
			try { return TimeZoneInfo.FindSystemTimeZoneById(windowsId); }
			catch (TimeZoneNotFoundException) { }
			if (windowsId == "Central Standard Time") return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
			return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
		}

		// ---- CS-8: signal-event logging and controls (event study / placebo layer). Off unless TraceLevel >= Journal. ----

		/// <summary>CS-8 control modes. None (default) leaves every helper a no-op.</summary>
		public enum EControlMode { None = 0, RandomEntry = 1, ShiftLevels = 2, DelayEntryBars = 3 }

		/// <summary>CS-8: forward horizons in bars for logSignal MFE/MAE. Default 5/15/30/60. Not an [Input].</summary>
		protected int[] SignalHorizons { get; set; } = { 5, 15, 30, 60 };
		/// <summary>CS-8 control layer (protected properties, D7-d). Default None.</summary>
		protected EControlMode ControlMode { get; set; } = EControlMode.None;
		protected int ControlSeed { get; set; } = 12345;
		/// <summary>CS-8 RandomEntry: probability that controlRandomEntry() returns true on a bar.</summary>
		protected double ControlRandomProbability { get; set; } = 0.5;
		/// <summary>CS-8 ShiftLevels: ticks added to a level (negative shifts down).</summary>
		protected int ControlShiftTicks { get; set; }
		/// <summary>CS-8 DelayEntryBars: bars to delay an entry (the strategy applies it).</summary>
		protected int ControlDelayBars { get; set; }

		private sealed class SignalEvent
		{
			public string tag, context;
			public int direction, bar, next;
			public double price, stop, best, worst;
			public DateTime time, day;
			public bool taken;
			public double[] mfeR, maeR;
		}

		private const string EVENTS_HEADER_BASE = "runId,strategy,symbol,tag,direction,time,bar,price,stop,riskPerUnit,taken,controlMode";
		private readonly List<SignalEvent> m_events = new List<SignalEvent>();
		private TradeJournal m_eventFile;
		private int m_eventsSteppedBar = -1;

		/// <summary>
		/// CS-8: record a signal (taken or not). The base tracks forward MFE/MAE in R (R = |price - stop|) over the bars after
		/// the signal bar at SignalHorizons and writes one MCEvents_&lt;runId&gt;.csv row when the last horizon completes, the
		/// trading day ends, or the run ends. direction: +1 long, -1 short. No-op unless TraceLevel >= Journal.
		/// </summary>
		protected void logSignal(string tag, int direction, double price, double stop, IDictionary<string, string> context, bool taken)
		{
			if (!journalOn() || direction == 0 || price <= 0) return;
			int[] h = SignalHorizons ?? new int[0];
			var ev = new SignalEvent
			{
				tag = tag ?? "", direction = direction > 0 ? 1 : -1, bar = Bars.FullSymbolData.Current, price = price, stop = stop,
				time = Bars.Time[0], day = tradingDayKey(Bars.Time[0]), taken = taken, best = 0, worst = 0,
				mfeR = new double[h.Length], maeR = new double[h.Length]
			};
			for (int i = 0; i < h.Length; i++) ev.mfeR[i] = ev.maeR[i] = double.NaN;
			if (context != null)
			{
				var sb = new StringBuilder();
				foreach (var kv in context) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
				ev.context = sb.ToString();
			}
			m_events.Add(ev);
		}

		/// <summary>CS-8: step pending events on each closed bar (called from CalcBar).</summary>
		private void signalEventsStep()
		{
			if (m_events.Count == 0 || Bars.Status != EBarState.Close) return;
			int cur = Bars.FullSymbolData.Current;
			if (cur == m_eventsSteppedBar) return;
			m_eventsSteppedBar = cur;
			int[] h = SignalHorizons ?? new int[0];
			DateTime day = tradingDayKey(Bars.Time[0]);
			bool runEnd = Bars.LastBarOnChart;
			for (int i = m_events.Count - 1; i >= 0; i--)
			{
				SignalEvent ev = m_events[i];
				int elapsed = cur - ev.bar;
				if (day != ev.day) { writeEvent(ev, "session"); m_events.RemoveAt(i); continue; }
				if (elapsed >= 1)
				{
					double fav = ev.direction > 0 ? Bars.High[0] - ev.price : ev.price - Bars.Low[0];
					double adv = ev.direction > 0 ? Bars.Low[0] - ev.price : ev.price - Bars.High[0];
					if (fav > ev.best) ev.best = fav;
					if (adv < ev.worst) ev.worst = adv;
					double r = Math.Abs(ev.price - ev.stop);
					while (ev.next < h.Length && elapsed >= h[ev.next])
					{
						ev.mfeR[ev.next] = r > 0 ? ev.best / r : double.NaN;
						ev.maeR[ev.next] = r > 0 ? ev.worst / r : double.NaN;
						ev.next++;
					}
				}
				if (ev.next >= h.Length) { writeEvent(ev, "horizon"); m_events.RemoveAt(i); }
				else if (runEnd) { writeEvent(ev, "runEnd"); m_events.RemoveAt(i); }
			}
		}

		private void writeEvent(SignalEvent ev, string completedBy)
		{
			System.Globalization.CultureInfo ic = System.Globalization.CultureInfo.InvariantCulture;
			int[] h = SignalHorizons ?? new int[0];
			if (m_eventFile == null)
			{
				var hdr = new StringBuilder(EVENTS_HEADER_BASE);
				for (int i = 0; i < h.Length; i++) hdr.Append(",mfeR_").Append(h[i]).Append(",maeR_").Append(h[i]);
				hdr.Append(",completedBy,context");
				m_eventFile = new TradeJournal(RunId, "MCEvents_", hdr.ToString());
				trace(ETraceLevel.Journal, $"EVENTS file({m_eventFile.Path_})");
			}
			var sb = new StringBuilder();
			sb.Append(RunId).Append(',').Append(csvField(this.GetType().Name)).Append(',').Append(csvField(Bars.Info.Name)).Append(',').Append(csvField(ev.tag)).Append(',')
				.Append(ev.direction).Append(',').Append(ev.time.ToString("yyyy-MM-dd HH:mm:ss", ic)).Append(',').Append(ev.bar).Append(',')
				.Append(ev.price.ToString(ic)).Append(',').Append(ev.stop.ToString(ic)).Append(',').Append(Math.Abs(ev.price - ev.stop).ToString(ic)).Append(',')
				.Append(ev.taken ? "1" : "0").Append(',').Append(ControlMode);
			for (int i = 0; i < h.Length; i++) sb.Append(',').Append(ev.mfeR[i].ToString("0.####", ic)).Append(',').Append(ev.maeR[i].ToString("0.####", ic));
			sb.Append(',').Append(completedBy).Append(',').Append(csvField(ev.context ?? ""));
			m_eventFile.Write(sb.ToString());
		}

		/// <summary>CS-8: deterministic uniform [0,1) from (ControlSeed, bar) — reproducible for a fixed seed, independent of call count.</summary>
		protected double controlRandom(int bar)
		{
			unchecked
			{
				ulong z = (ulong)(uint)ControlSeed * 0x9E3779B97F4A7C15UL + (ulong)(uint)bar;
				z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
				z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
				z ^= z >> 31;
				return (z >> 11) * (1.0 / 9007199254740992.0);
			}
		}

		/// <summary>CS-8 RandomEntry: true with ControlRandomProbability on this bar (always false in other modes).</summary>
		protected bool controlRandomEntry()
		{
			return ControlMode == EControlMode.RandomEntry && controlRandom(Bars.FullSymbolData.Current) < ControlRandomProbability;
		}

		/// <summary>CS-8 ShiftLevels: level + ControlShiftTicks ticks; unchanged in other modes.</summary>
		protected double controlAdjustLevel(double level)
		{
			return ControlMode == EControlMode.ShiftLevels ? level + ControlShiftTicks * tickSize() : level;
		}

		/// <summary>CS-8 DelayEntryBars: bars to delay an entry; 0 in other modes.</summary>
		protected int controlAdjustDelayBars()
		{
			return ControlMode == EControlMode.DelayEntryBars ? ControlDelayBars : 0;
		}

		// ---- CS-7: trade-management primitives (protected properties, not [Input]s - D7-c). The base sends no orders. ----

		/// <summary>CS-7: no new entries before this session-clock time (null = off).</summary>
		protected TimeSpan? NoEntryBefore { get; set; }
		/// <summary>CS-7: no new entries at or after this session-clock time (null = off).</summary>
		protected TimeSpan? NoEntryAfter { get; set; }
		/// <summary>CS-7: mustFlatten() from this intra-session time (null = off). For the session end use ExitOnClose.</summary>
		protected TimeSpan? FlatBy { get; set; }
		/// <summary>CS-7: max entries per trading day (tradingDayKey). 0 = off.</summary>
		protected int MaxTradesPerDay { get; set; }
		/// <summary>CS-7: block entries after this many losing closed trades in the trading day. 0 = off.</summary>
		protected int DailyLossStopCount { get; set; }
		/// <summary>CS-7: block entries once the day's closed net loss reaches this many R (RiskUnitDollars). 0 = off.</summary>
		protected double DailyLossStopR { get; set; }
		/// <summary>CS-7: $ value of 1 R for DailyLossStopR.</summary>
		protected double RiskUnitDollars { get; set; }
		/// <summary>CS-7: mustFlatten() once the open position is this many bars old. 0 = off.</summary>
		protected int TimeStopBars { get; set; }

		private DateTime m_tmDay = DateTime.MinValue;
		private readonly HashSet<string> m_tmEntrySeen = new HashSet<string>();
		private readonly HashSet<string> m_tmExitSeen = new HashSet<string>();
		private int m_tmEntries, m_tmLosers;
		private double m_tmNet;

		/// <summary>CS-7: time of day the entry window / FlatBy compare against. Default the bar time; the futures base uses ET.</summary>
		protected virtual TimeSpan sessionClock(DateTime barTime) { return barTime.TimeOfDay; }

		/// <summary>CS-7: entries, losing trades and closed net P&amp;L of the current trading day.</summary>
		protected int TradesToday { get { tmRefresh(); return m_tmEntries; } }
		protected int LosersToday { get { tmRefresh(); return m_tmLosers; } }
		protected double NetProfitToday { get { tmRefresh(); return m_tmNet; } }

		private void tmRefresh()
		{
			// No cache: fills can arrive on any tick, and the scan is cheap (newest 10 positions, de-duplicated by key).
			DateTime day = tradingDayKey(Bars.Time[0]);
			if (day != m_tmDay)
			{
				m_tmDay = day; m_tmEntries = m_tmLosers = 0; m_tmNet = 0;
				m_tmEntrySeen.Clear(); m_tmExitSeen.Clear();
			}
			int n = Math.Min(Positions.Count, 10);
			for (int i = n - 1; i >= 0; i--)
			{
				IMarketPosition pos = Positions[i];
				if (pos == null) continue;
				if (pos.ClosedTrades != null)
					foreach (var t in pos.ClosedTrades)
					{
						if (t == null || t.EntryOrder == null) continue;
						tmEntry(t.EntryOrder.Name, t.EntryOrder.BarNumber, t.EntryOrder.Time);
						if (t.ExitOrder == null || tradingDayKey(t.ExitOrder.Time) != m_tmDay) continue;
						string k = t.EntryOrder.Name + "\u001f" + t.EntryOrder.Time.Ticks + "\u001f" + t.ExitOrder.Name + "\u001f" + t.ExitOrder.Time.Ticks + "\u001f" + t.ExitOrder.Contracts;
						if (!m_tmExitSeen.Add(k)) continue;
						m_tmNet += t.Profit;
						if (t.Profit < 0) m_tmLosers++;
					}
				if (pos.OpenTrades != null)
					foreach (var t in pos.OpenTrades)
						if (t != null && t.EntryOrder != null) tmEntry(t.EntryOrder.Name, t.EntryOrder.BarNumber, t.EntryOrder.Time);
			}
		}

		private void tmEntry(string name, int bar, DateTime time)
		{
			if (tradingDayKey(time) != m_tmDay) return;
			if (m_tmEntrySeen.Add(name + "\u001f" + bar + "\u001f" + time.Ticks)) m_tmEntries++;
		}

		/// <summary>CS-7: may the strategy enter now? reason names the first rule that blocks (traced at Info).</summary>
		protected bool entryAllowed(out string reason)
		{
			reason = null;
			TimeSpan clock = sessionClock(Bars.Time[0]);
			if (NoEntryBefore.HasValue && clock < NoEntryBefore.Value) reason = $"before NoEntryBefore({NoEntryBefore.Value})";
			else if (NoEntryAfter.HasValue && clock >= NoEntryAfter.Value) reason = $"at/after NoEntryAfter({NoEntryAfter.Value})";
			else if (FlatBy.HasValue && clock >= FlatBy.Value) reason = $"at/after FlatBy({FlatBy.Value})";
			else
			{
				tmRefresh();
				if (MaxTradesPerDay > 0 && m_tmEntries >= MaxTradesPerDay) reason = $"MaxTradesPerDay({MaxTradesPerDay}) reached";
				else if (DailyLossStopCount > 0 && m_tmLosers >= DailyLossStopCount) reason = $"DailyLossStopCount({DailyLossStopCount}) reached: {m_tmLosers} losers";
				else if (DailyLossStopR > 0 && RiskUnitDollars > 0 && -m_tmNet / RiskUnitDollars >= DailyLossStopR) reason = $"DailyLossStopR({DailyLossStopR}) reached: {m_tmNet / RiskUnitDollars:0.00} R";
			}
			if (reason != null && traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, "entry blocked: " + reason);
			return reason == null;
		}

		/// <summary>CS-7: should the open position be closed now (FlatBy reached or TimeStopBars elapsed)?</summary>
		protected bool mustFlatten()
		{
			if (StrategyInfo.MarketPosition == 0) return false;
			if (FlatBy.HasValue && sessionClock(Bars.Time[0]) >= FlatBy.Value) return true;
			if (TimeStopBars > 0)
			{
				try
				{
					if (CurrentPosition.OpenTrades != null)
						foreach (var t in CurrentPosition.OpenTrades)
						{
							// oldest open entry decides; BarNumber is on the CurrentBar scale
							if (t != null && t.EntryOrder != null) return Bars.CurrentBar - t.EntryOrder.BarNumber >= TimeStopBars;
						}
				}
				catch { }
			}
			return false;
		}

		// ---- CS-4: sizing API consolidation ----

		/// <summary>
		/// CS-4 limits for sizeForRisk. All optional (0 / false = no limit).
		/// maxQty: hard cap. maxNotionalFraction: qty x unitNotional <= equityAvailable(maxNotionalFraction).
		/// unitNotional: $ per unit for the notional cap; 0 = full contract value entry x BigPointValue (D1: ES by full
		/// contract value; a stock is its price). minStopTicks: refuse stops closer than this. roundLots: floor to 100s.
		/// </summary>
		public struct SizingLimits
		{
			public int maxQty;
			public double maxNotionalFraction;
			public double unitNotional;
			public double minStopTicks;
			public bool roundLots;
		}

		/// <summary>
		/// CS-4: units so that |entry - stop| x BigPointValue x units ≈ equity x riskFraction, then the limits. riskFraction is a
		/// fraction in (0, 1] (0.01 = 1%). Returns 0 with a traced reason (bad BPV, stop too close, zero equity, notional cap).
		/// With default(SizingLimits) it equals stopLossBasedPositionSize(bars, entry, stop, riskFraction).
		/// </summary>
		protected int sizeForRisk(IInstrument bars, double entry, double stop, double riskFraction, SizingLimits limits)
		{
			if (bars == null || entry <= 0 || riskFraction <= 0 || riskFraction > 1)
			{
				trace(ETraceLevel.Debug, $"sizeForRisk - 0: entry({entry}) riskFraction({riskFraction}) must be > 0 and riskFraction <= 1");
				return 0;
			}
			double bpv = bars.Info.BigPointValue;
			if (double.IsNaN(bpv) || double.IsInfinity(bpv) || bpv <= 0)
			{
				trace(ETraceLevel.Warning, $"sizeForRisk - 0: BigPointValue({bpv}) invalid");
				return 0;
			}
			double distance = Math.Abs(entry - stop);
			if (limits.minStopTicks > 0 && distance < limits.minStopTicks * tickSize() - tickSize() * 1e-6)
			{
				trace(ETraceLevel.Debug, $"sizeForRisk - 0: stop distance {distance / tickSize():0.##} ticks < minStopTicks({limits.minStopTicks})");
				return 0;
			}
			int qty = stopLossBasedPositionSize(bars, entry, stop, riskFraction, false);
			if (qty <= 0) { trace(ETraceLevel.Debug, "sizeForRisk - 0: risk sizing returned 0 (zero equity or zero risk)"); return 0; }
			if (limits.maxQty > 0 && qty > limits.maxQty) { trace(ETraceLevel.Debug, $"sizeForRisk - qty({qty}) capped to maxQty({limits.maxQty})"); qty = limits.maxQty; }
			if (limits.maxNotionalFraction > 0)
			{
				double unit = limits.unitNotional > 0 ? limits.unitNotional : entry * bpv;
				qty = capByNotional(qty, unit, limits.maxNotionalFraction);
				if (qty <= 0) { trace(ETraceLevel.Debug, $"sizeForRisk - 0: notional cap unit({unit}) fraction({limits.maxNotionalFraction})"); return 0; }
			}
			qty = applyRoundLots(qty, limits.roundLots);
			if (qty <= 0) trace(ETraceLevel.Debug, "sizeForRisk - 0 after round lots");
			return qty;
		}

		/// <summary>
		/// CS-4: equityAvailable plus, when includeOpenPnl, the current position's open P&amp;L (scaled by the same fraction) for
		/// non-account equity (chart / portfolio backtest). Account equity already includes open P&amp;L.
		/// </summary>
		protected double equityAvailable(double fraction, bool includeOpenPnl)
		{
			double e = equityAvailable(fraction);
			if (!includeOpenPnl || (Environment.IsAutoTradingMode && m_lastEquityFromAccount)) return e;
			double open = 0;
			try { open = CurrentPosition.OpenProfit; } catch { }
			double scale = fraction > 0 ? fraction
				: Environment.ApplicationCode == EApplicationCode.Portfolio ? this.Portfolio.MaxRiskEquityPerPosPercent / 100.0d : 1.0;
			return e + open * scale;
		}

		// ---- Q1: optional pre-trade cost filter (off by default) ----

		/// <summary>
		/// Q1: skip a trade when MultiCharts Commission + Slippage for the round trip exceeds this fraction of the planned risk
		/// (R, $ per contract). 0 = off (default). Not an [Input]; set it in the derived strategy.
		/// </summary>
		protected double CostFilterMaxFractionOfR { get; set; } = 0.0;

		/// <summary>
		/// Q1: MultiCharts Strategy Properties commission and slippage for one contract round trip, in $: 2 x (Commission +
		/// Slippage), both read as $ per contract per side [PG p17, p45]. Override if the commission rule is per trade.
		/// </summary>
		protected virtual double roundTripCostPerContract()
		{
			return 2.0 * (this.Commission + this.Slippage);
		}

		/// <summary>Q1: true when the filter is off or the cost is within CostFilterMaxFractionOfR of R. Reason set when false.</summary>
		protected bool costFilterPasses(IInstrument bars, double entry, double stop, out string reason)
		{
			reason = null;
			if (CostFilterMaxFractionOfR <= 0 || bars == null) return true;
			double r = Math.Abs(entry - stop) * bars.Info.BigPointValue;
			double cost = roundTripCostPerContract();
			if (r <= 0) { reason = "cost filter: zero risk"; return false; }
			if (cost / r > CostFilterMaxFractionOfR)
			{
				reason = $"cost filter: round-trip cost {cost:0.00} = {cost / r:0.000} R > {CostFilterMaxFractionOfR:0.000} R";
				if (traceOn(ETraceLevel.Info)) trace(ETraceLevel.Info, reason);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Round-lot policy: floor to a multiple of 100 when qty >= 100; below 100 keep the odd lot.
		/// </summary>
		protected static int applyRoundLots(int qty, bool useRoundLots)
		{
			if (!useRoundLots || qty < 100) return qty;
			return (qty / 100) * 100;
		}

		/// <summary>
		/// Takes a desired qty and reduces it if that quantity would consume more than maxEquityFraction of available equity at the given unitNotional.
		/// Caps qty so qty * unitNotional <= equityAvailable(maxEquityFraction). Stocks: unitNotional = price; futures: margin per contract.
		/// Example: equity available for this trade = $50,000, unit notional = $150 → max qty = floor(50,000 / 150) = 333. If your signal or
		///          risk model wanted 500 shares, this method forces it down to 333.
		/// Place in call chain or position entry:
		/// → signal / entry condition
		/// → risk-based qty(e.g.risk $ / stop distance)
		/// → volatility or ATR adjustment
		/// → capByNotional(...)          ← this method
		/// → min lot size / rounding / broker constraints
		/// → send order
		/// </summary>
		protected int capByNotional(int qty, double unitNotional, double maxEquityFraction)
		{
			if (qty <= 0 || unitNotional <= 0 || maxEquityFraction <= 0) return qty;
			double equity = equityAvailable(maxEquityFraction);
			int cap = Math.Max(0, (int)Math.Floor(equity / unitNotional));
			if (cap < qty) trace(ETraceLevel.Debug, $"capByNotional - qty({qty}) capped to {cap} unitNotional({unitNotional}) equity({equity})");
			return Math.Min(qty, cap);
		}

		/// <summary>
		/// Determine the position size based on a given entry price and a maximum equity per position as a percentage of the available equity.
		/// </summary>
		protected int equityBasedPositionSize(double entryPrice, double maxEquityPerPosPercent = 0.0d, bool useRoundLots = false)
		{
			int result = 0;
			if (entryPrice == 0.0d)
			{
				trace(ETraceLevel.Debug, $"equityBasedPositionSize - entryPrice is zero returning result {result}");
				return result;
			}

			double positionCash = equityAvailable(maxEquityPerPosPercent);
			result = (int)Math.Truncate(positionCash / entryPrice);
			int positionBeforeRoundLots = result;
			result = applyRoundLots(result, useRoundLots);
			if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"equityBasedPositionSize - Equity({positionCash}) EntryPrice({entryPrice}) MaxEquityPercent({maxEquityPerPosPercent}) BeforeRoundLots({positionBeforeRoundLots}) Result/AfterRoundLots({result})");
			return result;
		}

		/// <summary>
		/// Position size based on the maximum risk equity per position as a percentage of equity,
		/// if given maxRiskEquityPerPosPercent is zero the risk based entry is ignored (default) and
		/// the full equity is used to compute the position size.
		/// ROUND LOT ORDERS - In order to facilitate good market fills for larger accounts you should use round lot orders
		///                    (multiples of 100) to align with the instritutional traders and market makers. Odd lot orders
		///                    can create slippage and poor fills.
		/// </summary>
		protected int equityBasedPositionSize(IInstrument bars, double maxEquityPerPosPercent = 0.0d, bool useRoundLots = false)
		{
			int result = 0;
			if (bars.Close[0] == 0.0d)
			{
				trace(ETraceLevel.Debug, $"equityBasedPositionSize - bars close value is zero at time {bars.Time[0]:MM/dd/yyyy HH:mm:ss} returning result {result}");
				return result;
			}

			double positionCash = equityAvailable(maxEquityPerPosPercent);
			result = (int)Math.Truncate(positionCash / bars.Close[0]);
			int positionBeforeRoundLots = result;
			result = applyRoundLots(result, useRoundLots);
			if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"equityBasedPositionSize - Equity({positionCash}) BarsClose({bars.Close[0]}) MaxEquityPercent({maxEquityPerPosPercent}) BeforeRoundLots({positionBeforeRoundLots}) Result/AfterRoundLots({result})");
			return result;
		}

		/// <summary>
		/// Position size based on the maximum risk equity per position as a percentage of available equity.
		/// <paramref name="risk"/> is DOLLAR risk per unit (price distance × BigPointValue), not a price distance.
		/// </summary>
		protected int riskBasedPositionSize(double risk, double maxRiskEquityPerPosPercent, bool useRoundLots = false)
		{
			if (risk <= 0 || maxRiskEquityPerPosPercent <= 0)
			{
				trace(ETraceLevel.Debug, $"riskBasedPositionSize risk or maxRiskEquityPerPosPercent not correct - Risk({risk}) MaxRiskEquityPerPosPercent({maxRiskEquityPerPosPercent})");
				return 0;
			}

			double positionCash = equityAvailable(maxRiskEquityPerPosPercent);
			if (positionCash <= 0.0)
			{
				trace(ETraceLevel.Debug, $"riskBasedPositionSize zero equity available returning zero size");
				return 0;
			}

			double shares = positionCash / risk;
			if (double.IsInfinity(shares) || double.IsNaN(shares) || shares > int.MaxValue || shares < 0)
			{
				trace(ETraceLevel.Warning, $"riskBasedPositionSize would result in overflow (possible data issue or very small risk) or negative shares returning zero - Risk({risk}) PositionCash({positionCash}) Shares({shares})");
				return 0;
			}

			int result = (int)Math.Floor(shares);
			result = applyRoundLots(result, useRoundLots);
			if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"riskBasedPositionSize - Risk({risk}) PositionCash({positionCash}) SharesBeforeRoundLots({(int)Math.Floor(shares)}) FinalSize({result})");
			return result;
		}

		/// <summary>
		/// Position size based on the maximum risk equity per position as a percentage of equity and a given stop loss price,
		/// using the instrument's last close (bars.Close[0]) as the entry price.
		/// </summary>
		protected int stopLossBasedPositionSize(IInstrument bars, double stopLossPrice, double maxRiskEquityPerPosPercent = 0.0d, bool useRoundLots = false)
		{
			return stopLossBasedPositionSize(bars, bars.Close[0], stopLossPrice, maxRiskEquityPerPosPercent, useRoundLots);
		}

		/// <summary>Units such that |entry - stop| * BigPointValue * units ≈ equity * riskFraction. BPV = 1 for stocks.</summary>
		protected int stopLossBasedPositionSize(IInstrument bars, double entryPrice, double stopLossPrice, double maxRiskEquityPerPosPercent, bool useRoundLots = false)
		{
			double bpv = bars.Info.BigPointValue;
			if (double.IsNaN(bpv) || double.IsInfinity(bpv) || bpv <= 0)
			{
				trace(ETraceLevel.Warning, $"stopLossBasedPositionSize - BigPointValue({bpv}) invalid, returning 0");
				return 0;
			}
			double dollarRiskPerUnit = Math.Abs(entryPrice - stopLossPrice) * bpv;
			return riskBasedPositionSize(dollarRiskPerUnit, maxRiskEquityPerPosPercent, useRoundLots);
		}

		/// <summary>
		/// Position size based on a specific percentage of the average volume traded over a given period and offset on the given instrument. The useRoundLots
		/// can be used if larger position sizes are expected and entries need to be taken in round lots with floor to the nearest lower 100 shares
		/// (round-lot friendly).
		/// </summary>
		protected int volumeBasedPositionSize(IInstrument bars, int volumePeriod, int volumePeriodOffset, double maxAvgVolumePercent, bool useRoundLots = false)
		{
			int result = 0; //do not enter a position if we do not have enough data to compute the average volume
			
			// CurrentBar is 1-based and MaxBarsBack-relative; the real limit is MaxBarsBack >= period + offset - 1.
			int deepest = volumePeriod + volumePeriodOffset - 1;
			if (deepest > ExecInfo.MaxBarsBack && !m_volumeMbbWarned)
			{
				m_volumeMbbWarned = true;   // field, reset in StartCalc
				trace(ETraceLevel.Warning, $"volumeBasedPositionSize - needs {deepest} bars back but MaxBarsBack is {ExecInfo.MaxBarsBack}");
			}

			double volumeAverage = 0.0;
			if (maxAvgVolumePercent > 0.0d && bars.CurrentBar >= 1 && deepest <= ExecInfo.MaxBarsBack)
			{
				volumeAverage = bars.Volume.Average(volumePeriod, volumePeriodOffset);
				result = (int)checked(Math.Round(volumeAverage * maxAvgVolumePercent, 0));
			}

			int beforeRoundLots = result;
			result = applyRoundLots(result, useRoundLots);
			if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"volumeBasedPositionSize - CurrentBar({bars.CurrentBar}) VolumeLength({volumePeriod}) VolumePeriodOffset({volumePeriodOffset}) Average({volumeAverage}) AvgVolumeWithMaxPercentage({volumeAverage * maxAvgVolumePercent}) SizeBeforeRoundLots({beforeRoundLots}) RoundedSize({result})");
			return result;
		}

		/// <summary>
		/// Computes the average true range of the instrument over a given length.
		/// NOTE: This function is not optimized for performance and should be used cautiosly.
		/// </summary>
		/// <remarks>CS-9: reads Close[length], so it needs CurrentBar > length and MaxBarsBack >= length + 1.</remarks>
		protected double averageTrueRange(IInstrument bars, int length)
		{
			if (bars.CurrentBar <= length) return 0.0d;

			double sum = 0.0;
			for (int i = 0; i < length; i++)
			{
				double tr = Math.Max(bars.High[i] - bars.Low[i], Math.Max(Math.Abs(bars.High[i] - bars.Close[i + 1]), Math.Abs(bars.Low[i] - bars.Close[i + 1])));
				sum += tr;
			}

			return sum / length;
		}

		/// <summary>
		/// Checks whether a price is within range from the moving average of given length and a specific factor of ATR's.
		/// </summary>
		protected bool isWithinAverageRange(IInstrument bars, int maLength, int atrLength, double atrFactor)
		{
			if (bars.CurrentBar < maLength || bars.CurrentBar < atrLength) return false;
			return Math.Abs(bars.Close[0] - bars.Close.Average(maLength)) < averageTrueRange(bars, atrLength) * atrFactor;
		}

		/// <summary>
		/// Calculates the number of calendar days between two dates.
		/// </summary>
		protected int calendarDayCount(DateTime entryDate, DateTime currentDateTime)
		{
			if (entryDate.Date > currentDateTime.Date) throw new ArgumentException("entryDate must always be smaller than currentDateTime");
			//NOTE: We use only the dates and not times as TimeSpan will count complete 24-hour periods when time is involved leading to off-by-1 errors,
			//      we are only interested in the days without too much consideration for the time. E.g. 2024-05-01 14:00 to 2024-05-02 10:00 would
			//      return 0 instead of 1 when time is involved since there has not elapsed a 24-hour period between the two while 2024-05-01 to 2024-05-02
			//      would return 1 as we expect irrespective of time.
			return (currentDateTime.Date - entryDate.Date).Days;
		}

		/// <summary>
		/// Calculates the number of trading days between two dates using the US stock market calendar.
		/// NOTE: This will not work correctly for non-US markets and non-equities that trade over the weekends, e.g. Forex.
		/// </summary>
		protected int tradingDayCount(DateTime entryDate, DateTime currentDateTime)
		{
			if (entryDate.Date > currentDateTime.Date) throw new ArgumentException("entryDate must always be smaller than currentDateTime");
			DateTime date = currentDateTime.Date; //IMPORTANT: Holidays below ignore the Time component so we must use DateOnly component.
			int count = 0;

			if (!m_tradingDayWarned && Bars.Info.Category != ESymbolCategory.Stock)
			{
				m_tradingDayWarned = true;
				trace(ETraceLevel.Warning, "tradingDayCount - WARNING - ExitDays used with non-stock object using TradingDays specification calculated around stock market calendar.");
			}

			while (date.Date > entryDate.Date)
			{
				if (!isTradingHoliday(date) && date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday) count++;
				date = date.AddDays(-1);
			}

			return count;
		}

		/// <summary>
		/// Spec VPF-6 (Rev 2.1, calendar by asset class): non-trading weekday for tradingDayCount. Default: the NYSE calendar
		/// (unchanged). _VolumeProfileFuturesBase overrides it with the CME Globex closures.
		/// </summary>
		protected virtual bool isTradingHoliday(DateTime date)
		{
			return m_holidays.isHoliday(date);
		}

		/// <summary>
		/// Lowest low of today plus the previous <paramref name="daysBack"/> trading days (tradingDayKey, i.e. Globex days for futures).
		/// daysBack = 0 → today's low; daysBack = 1 → min(today, previous day). includeToday = false excludes today.
		/// E.g. 1 day back would be the previous trading day, if days back is set to 0 it will return the current day low.
		/// </summary>
		protected Tuple<DateTime, double> recentLow(int daysBack, DateTime? earliestDateTime = null, bool includeToday = true)
		{
			if (daysBack == 0) return new Tuple<DateTime, double>(IntradayLowTime, IntradayLow);
			double low = includeToday ? IntradayLow : double.MaxValue;
			DateTime lowBarTime = includeToday ? IntradayLowTime : DateTime.MinValue;
			int daysProcessed = 0;
			DateTime startDate = tradingDayKey(Bars.Time[0]);
			DateTime lastDate = startDate;

			for (int i = 0; i < Bars.CurrentBar; i++)
			{
				DateTime day = tradingDayKey(Bars.Time[i]);
				if (day != lastDate)
				{
					daysProcessed++;
					lastDate = day;
				}

				if (daysProcessed > daysBack || earliestDateTime?.CompareTo(Bars.Time[i]) > 0)
					break;

				if (day != startDate && low > Bars.Low[i])
				{
					low = Bars.Low[i];
					lowBarTime = Bars.Time[i];
				}

				if (i == ExecInfo.MaxBarsBack - 1)
				{
					if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, $"recentLow - Reached MaxBarsBack looking {daysBack} days back at index {i} with low {low} that might be incorrect.");
					break;
				}
			}

			return new Tuple<DateTime, double>(lowBarTime, low);
		}

		/// <summary>
		/// Highest high of today plus the previous <paramref name="daysBack"/> trading days (tradingDayKey, i.e. Globex days for futures).
		/// daysBack = 0 → today's high; daysBack = 1 → max(today, previous day). includeToday = false excludes today.
		/// E.g. 1 day back would be the previous trading day, if days back is set to 0 it will return the current day high.
		/// </summary>
		protected Tuple<DateTime, double> recentHigh(int daysBack, DateTime? earliestDateTime = null, bool includeToday = true)
		{
			if (daysBack == 0) return new Tuple<DateTime, double>(IntradayHighTime, IntradayHigh);
			double high = includeToday ? IntradayHigh : double.MinValue;
			DateTime highBarTime = includeToday ? IntradayHighTime : DateTime.MinValue;
			int daysProcessed = 0;
			DateTime startDate = tradingDayKey(Bars.Time[0]);
			DateTime lastDate = startDate;

			for (int i = 0; i < Bars.CurrentBar; i++)
			{
				DateTime day = tradingDayKey(Bars.Time[i]);
				if (day != lastDate)
				{
					daysProcessed++;
					lastDate = day;
				}

				if (daysProcessed > daysBack || earliestDateTime?.CompareTo(Bars.Time[i]) > 0)
					break;

				if (day != startDate && high < Bars.High[i])
				{
					high = Bars.High[i];
					highBarTime = Bars.Time[i];
				}

				if (i == ExecInfo.MaxBarsBack - 1)
				{
					if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, $"recentHigh - Reached MaxBarsBack looking {daysBack} days back at index {i} with high {high} that might be incorrect.");
					break;
				}
			}

			return new Tuple<DateTime, double>(highBarTime, high);
		}

		/// <summary>
		/// Finds the most recent entry order that completed the entry for the given name, so if there are multiple fills
		/// this would return the time of the last order the completed the entry.
		/// NOTE: This method performs a cached read, so if you run into issues try the non-cached version below.
		/// </summary>
		/// <param name="entryOrderNames">List of entry orders to determine the entry time for</param>
		/// <returns></returns>
		public DateTime orderEntryTime(params String[] entryOrderNames)
		{
			int bar = Bars.FullSymbolData.Current;
			bool closed = Bars.Status == EBarState.Close;
			string key = entryOrderNames == null ? "" : string.Join("\u001f", entryOrderNames);
			//real-time: the result comes from the shared TradeManager snapshot, so it can only change when the snapshot is
			//refreshed; key the cache on the refresh stamp (CS-19). Backtest: Positions only change per bar/state.
			long snapshot = 0;
			if (Environment.IsRealTimeCalc)
			{
				refreshTradeManagerIfNeeded();
				snapshot = System.Threading.Interlocked.Read(ref s_lastTradeManagerRefreshTicks);
			}
			if (bar == m_entryCacheBar && closed == m_entryCacheClosed && key == m_entryCacheKey && snapshot == m_entryCacheSnapshot)
				return m_entryCacheValue;
			DateTime result = orderEntryTimeUncached(entryOrderNames);
			m_entryCacheBar = bar; m_entryCacheClosed = closed; m_entryCacheKey = key; m_entryCacheValue = result; m_entryCacheSnapshot = snapshot;
			return result;
		}

		/// <summary>
		/// Finds the most recent entry order that completed the entry for the given name, so if there are multiple fills
		/// this would return the time of the last order the completed the entry.
		/// NOTE: This method does a hard by-pass of the above caching mechnism and always reads from the TradeManager.
		/// </summary>
		/// <param name="entryOrderNames">List of entry orders to determine the entry time for</param>
		/// <returns></returns>
		public DateTime orderEntryTimeUncached(params String[] entryOrderNames)
		{
			return entryTimeCore(EOrderNameMatch.Contains, entryOrderNames);
		}

		/// <summary>CS-5: how order names are matched. Contains is today's behaviour ("LE" also matches "LE2").</summary>
		public enum EOrderNameMatch { Contains = 0, Exact = 1, Prefix = 2 }

		/// <summary>CS-5: order-name match under <paramref name="mode"/> (ordinal, case-sensitive like the existing Contains).</summary>
		protected static bool orderNameMatches(string orderName, string wanted, EOrderNameMatch mode)
		{
			if (string.IsNullOrEmpty(wanted) || orderName == null) return false;
			switch (mode)
			{
				case EOrderNameMatch.Exact: return string.Equals(orderName, wanted, StringComparison.Ordinal);
				case EOrderNameMatch.Prefix: return orderName.StartsWith(wanted, StringComparison.Ordinal);
				default: return orderName.Contains(wanted);
			}
		}

		/// <summary>CS-5: orderEntryTime with an explicit name match mode (not cached).</summary>
		public DateTime orderEntryTime(EOrderNameMatch matchMode, params String[] entryOrderNames)
		{
			return entryTimeCore(matchMode, entryOrderNames);
		}

		private DateTime entryTimeCore(EOrderNameMatch matchMode, String[] entryOrderNames)
		{
			if (entryOrderNames == null) entryOrderNames = new string[0];
			//add special handling for backtesting just returning entry order time of last open trade
			if (!Environment.IsRealTimeCalc)
			{
				var position = Positions[0];
				if (position.OpenTrades.Count > 0)
				{
					var lastOpenTrade = position.OpenTrades
							.Where(t => t.EntryOrder != null &&
													entryOrderNames.Any(name => orderNameMatches(t.EntryOrder.Name, name, matchMode)))
							.OrderByDescending(t => t.EntryOrder?.Time ?? DateTime.MinValue)
							.FirstOrDefault();

					if (lastOpenTrade?.EntryOrder != null)
					{
						DateTime btEntryTime = lastOpenTrade.EntryOrder.Time;
						if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"EntryTime backtest {btEntryTime:yyyy-MM-dd HH:mm:ss} (matched order(s): {string.Join("; ", entryOrderNames)})");
						return btEntryTime;
					}
				}
				return DateTime.MinValue;
			}

			//refresh orders based on symbol and timeframe
			refreshTradeManagerIfNeeded();
			DateTime? entryLastFill;
			string window;
			lock (s_tradingDataLock)
			{
				ITradingData td = tradingData;   // refreshes if needed (re-entrant lock)
				if (td == null) return DateTime.MinValue;   // TradeManager not loaded (e.g. real-time without auto trading)
				td.Orders.IntervalFltr.Set(DateTime.Now.AddDays(-DEFAULT_ENTRY_EXIT_LOOKBACK), DateTime.Now.AddHours(1)); // negative value is correct, we want to look backward
				td.Orders.SymbolFltr.CurrentValue = this.Bars.Info.Name;
				entryLastFill = td.Orders.Items
														.Where(o => o.SymbolStr == Bars.Info.Name
																		 && entryOrderNames.Any(name => orderNameMatches(o.Name, name, matchMode))
																		 && o.State == ETM_OrderState.eTM_OS_Filled
																		 && o.LeftContracts == 0)
														.Select(o => o.FinalDT ?? o.GeneratedDT)
														.Where(dt => dt > DateTime.MinValue)
														.OrderByDescending(dt => dt)
														.FirstOrDefault();             // materialized inside the lock
				window = $"{td.Orders.IntervalFltr.From},{td.Orders.IntervalFltr.To}";
			}

			if (entryLastFill == null || entryLastFill.Value == DateTime.MinValue)
			{
				if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"EntryTime - no fills found for order names: [{String.Join("; ", entryOrderNames)}] DateInterval({window}).");
				return DateTime.MinValue;
			}

			return entryLastFill.Value;
		}

		public DateTime orderExitTime(EMarketPositionSide side, EUseFill useFill = EUseFill.Last)
		{
			return exitTimeCore(side, useFill, DateTime.MinValue, null);
		}

		/// <summary>
		/// CS-5: exit time of the current round trip only. Exits before <paramref name="notBefore"/> are ignored (DateTime.MinValue
		/// = the entry time of the position the exits belong to); when names are given only exits whose name contains one of
		/// them count. Use this so a real-time query does not return a previous trade's exit.
		/// Real time: the default notBefore is the position's entry time in bar time; TradeManager fill times (FinalDT) are PC
		/// local time. On a chart whose time zone is not the PC zone pass notBefore in PC local time.
		/// </summary>
		public DateTime orderExitTime(EMarketPositionSide side, EUseFill useFill, DateTime notBefore, params string[] exitOrderNames)
		{
			if (notBefore == DateTime.MinValue) notBefore = exitPositionEntryTime();
			return exitTimeCore(side, useFill, notBefore, exitOrderNames != null && exitOrderNames.Length > 0 ? exitOrderNames : null);
		}

		/// <summary>CS-5: entry time of the position whose exits orderExitTime reads (open position with partial exits, else the last closed one).</summary>
		private DateTime exitPositionEntryTime()
		{
			IMarketPosition pos = null;
			try
			{
				if (CurrentPosition.ClosedTrades.Count > 0 || CurrentPosition.OpenTrades.Count > 0) pos = CurrentPosition;
				if ((pos == null || pos.ClosedTrades.Count == 0) && Positions.Count > 1 && CurrentPosition.OpenTrades.Count == 0) pos = Positions[1];
			}
			catch { }
			if (pos == null) return DateTime.MinValue;
			DateTime first = DateTime.MaxValue;
			if (pos.OpenTrades != null) foreach (var t in pos.OpenTrades) if (t.EntryOrder != null && t.EntryOrder.Time < first) first = t.EntryOrder.Time;
			if (pos.ClosedTrades != null) foreach (var t in pos.ClosedTrades) if (t.EntryOrder != null && t.EntryOrder.Time < first) first = t.EntryOrder.Time;
			return first == DateTime.MaxValue ? DateTime.MinValue : first;
		}

		private DateTime exitTimeCore(EMarketPositionSide side, EUseFill useFill, DateTime notBefore, string[] exitOrderNames)
		{
			//add special handling for backtesting just returning entry order time of last open trade
			if (!Environment.IsRealTimeCalc)
			{
				IMarketPosition pos = null;
				if (CurrentPosition.ClosedTrades.Count > 0) pos = CurrentPosition;   // partial exits of the open position
				else if (Positions.Count > 1) pos = Positions[1];                    // last closed position (Positions[0] == CurrentPosition)

				if (pos == null || pos.ClosedTrades.Count == 0) return DateTime.MinValue;
				if (side != EMarketPositionSide.Flat && pos.Side != side) return DateTime.MinValue;

				DateTime first = DateTime.MaxValue, last = DateTime.MinValue;

				foreach (var t in pos.ClosedTrades)
				{
					if (t.ExitOrder == null) continue;
					DateTime x = t.ExitOrder.Time;
					if (x < notBefore) continue;   // CS-5
					if (exitOrderNames != null && !exitOrderNames.Any(n => orderNameMatches(t.ExitOrder.Name, n, EOrderNameMatch.Contains))) continue;
					if (x < first) first = x;
					if (x > last) last = x;
				}

				if (last == DateTime.MinValue && (notBefore != DateTime.MinValue || exitOrderNames != null)) return DateTime.MinValue;   // CS-5: every exit filtered out
				return useFill == EUseFill.First ? first : last;
			}

			//nothing to do for flat side, needs to be long or short
			if (side == EMarketPositionSide.Flat)
			{
				if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, $"ExitTime - position side set to flat, nothing to do.");
				return DateTime.MinValue;
			}

			//determine exit order type (opposite of entry action)
			ETM_OrderAction orderAction = side == EMarketPositionSide.Long ? ETM_OrderAction.eTM_OA_Sell : ETM_OrderAction.eTM_OA_Buy;

			//refresh orders based on symbol and timeframe (same pattern as EntryTime): the filters live on the shared static
			//TradeManager snapshot so set them and materialize the query under the same lock (CS-06)
			refreshTradeManagerIfNeeded();
			List<DateTime> exitFillTimes;
			string window;
			lock (s_tradingDataLock)
			{
				ITradingData td = tradingData;   // refreshes if needed (re-entrant lock)
				if (td == null) return DateTime.MinValue;   // TradeManager not loaded (e.g. real-time without auto trading)
				td.Orders.IntervalFltr.Set(DateTime.Now.AddDays(-DEFAULT_ENTRY_EXIT_LOOKBACK), DateTime.Now); // negative value is correct, we want to look backward
				td.Orders.SymbolFltr.CurrentValue = this.Bars.Info.Name;

				//get all filled exit orders for this instrument
				exitFillTimes = td.Orders.Items
						.Where(o =>
								(o.SymbolStr == Bars.Info.Name)
								&& o.State == ETM_OrderState.eTM_OS_Filled
								&& o.Action == orderAction
								&& o.LeftContracts == 0
								&& (exitOrderNames == null || exitOrderNames.Any(n => orderNameMatches(o.Name, n, EOrderNameMatch.Contains)))   // CS-5
						)
						.Select(o => o.FinalDT ?? o.GeneratedDT)  //FinalDT = actual broker fill time
						.Where(dt => dt > DateTime.MinValue && dt >= notBefore)
						.OrderBy(dt => dt)                        //sort chronological to find first/last exit
						.ToList();                                // materialized inside the lock
				window = $"{td.Orders.IntervalFltr.From},{td.Orders.IntervalFltr.To}";
			}

			//make sure we found filled orders
			if (exitFillTimes.Count == 0)
			{
				if (traceOn(ETraceLevel.Warning)) trace(ETraceLevel.Warning, $"ExitTime - no fills found: OrderAction({orderAction}) DateInterval({window}).");
				return DateTime.MinValue;
			}

			return useFill == EUseFill.First ? exitFillTimes.First() : exitFillTimes.Last();
		}

		protected void refreshTradeManagerIfNeeded()
		{
			if (!Environment.IsAutoTradingMode)
				return;

			//avoid lock contention if refresh not required
			long last = System.Threading.Interlocked.Read(ref s_lastTradeManagerRefreshTicks);
			if (s_tradingData != null && DateTime.Now.Ticks - last <= DEFAULT_TRADE_MANAGER_REFRESH_INTERVAL.Ticks)
				return;

			lock (s_tradingDataLock)
			{
				if (s_tradingData == null || DateTime.Now.Ticks - System.Threading.Interlocked.Read(ref s_lastTradeManagerRefreshTicks) > DEFAULT_TRADE_MANAGER_REFRESH_INTERVAL.Ticks)
				{
					if (traceOn(ETraceLevel.Debug)) trace(ETraceLevel.Debug, $"refreshTradeManagerIfNeeded - refreshing trade manager LastRefresh({new DateTime(System.Threading.Interlocked.Read(ref s_lastTradeManagerRefreshTicks))}) CurrentTime({DateTime.Now})");
					TradeManager.ProcessEvents();
					s_tradingData = TradeManager.TradingData;
					System.Threading.Interlocked.Exchange(ref s_lastTradeManagerRefreshTicks, DateTime.Now.Ticks);
				}
			}
		}
	}
}