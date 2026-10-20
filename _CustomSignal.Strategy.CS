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
				string logFilename = $"MCSignal{DateTime.Now:yyyyMMdd-HHmm}.csv";   //log files are csv's for analysis in Excel
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

			public void WriteTrace(ETraceLevel level, DateTime timestamp, string signalName, string symbol, string runId, string message)
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
						m_writer.WriteLine($"{level},{timestamp:yyyy-MM-dd HH:mm:ss zzz},{signalName},{symbol},{runId},{csvMessage}");
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

		public double IntradayVolume
		{
			get { return m_intradayVolume; }
		}

		public Function._Holidays Holidays { get { return m_holidays; } }

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
				m_logger.WriteTrace(traceLevel, Bars.Time[0], this.GetType().Name, this.Bars.Info.Name, RunId, message);
			}
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
			m_logger = null;
			base.Destroy();
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_currentDay = DateTime.MinValue; // make sure that current day is always set on the first CalcBar iteration
			m_intradayLowTime = DateTime.Now;
			m_intradayHighTime = DateTime.Now;
			m_intradayCloseTime = DateTime.Now;
			m_intradayOpenTime = DateTime.Now;
			m_intradayBarCount = 0;
			m_intradayLow = double.MaxValue;
			m_intradayHigh = double.MinValue;
			m_intradayClose = 0.0d;
			m_intradayOpen = 0.0d;
			m_intradayVolume = 0.0d;
			m_loadAccounts = true;
			m_unknownAppWarned = false;
			m_volumeMbbWarned = false;
			m_tradingDayWarned = false;
			m_entryCacheBar = -1;
			m_entryCacheSnapshot = 0;
			m_runId = Guid.NewGuid().ToString("N").Substring(0, 8);   // CS-3
			m_runLogged = false;
			m_runEndLogged = false;
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
			return $"RUN id({RunId}) build({BuildTag}) strategy({GetType().Name}) symbol({info.Name}) category({info.Category}) " +
				$"resolution({info.Resolution.ChartType} {info.Resolution.Type} {info.Resolution.Size}) timeZone({info.TimeZone}) " +
				$"firstBar({Bars.Time[0]:yyyy-MM-dd HH:mm:ss}) barNumber({Bars.FullSymbolData.Current}) app({Environment.ApplicationCode}) " +
				$"inputs[{inputSnapshot()}]";
		}

		/// <summary>CS-3: "Name=value; ..." for every [Input] property of the concrete strategy, invariant culture.</summary>
		protected string inputSnapshot()
		{
			var sb = new StringBuilder();
			System.Reflection.PropertyInfo[] props = GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
		protected virtual DateTime tradingDayKey(DateTime barTime) { return barTime.Date; }

		/// <summary>
		/// Determines the equity available for trading a position based on some custom risk equity per position percentage. If
		/// the strategy is run on a chart the whole equity is used if no custom equity percentage is given. If the strategy is
		/// is run in the portfolio manager the portfolio percentage per position is used if no custom equity percentage is given.
		/// </summary>
		protected double equityAvailable(double maxCustomRiskEquityPerPosPercent = 0.0d)
		{
			double equityAvailable = 0.0d;

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
		protected double averageTrueRange(IInstrument bars, int length)
		{
			if (bars.CurrentBar < length) return 0.0d;

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
				if (!m_holidays.isHoliday(date) && date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday) count++;
				date = date.AddDays(-1);
			}

			return count;
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
			//add special handling for backtesting just returning entry order time of last open trade
			if (!Environment.IsRealTimeCalc)
			{
				var position = Positions[0];
				if (position.OpenTrades.Count > 0)
				{
					var lastOpenTrade = position.OpenTrades
							.Where(t => t.EntryOrder != null &&
													entryOrderNames.Any(name =>
															!string.IsNullOrEmpty(name) && t.EntryOrder.Name.Contains(name)))
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
																		 && entryOrderNames.Any(name => !String.IsNullOrEmpty(name) && o.Name.Contains(name))
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
					if (x < first) first = x;
					if (x > last) last = x;
				}

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
						)
						.Select(o => o.FinalDT ?? o.GeneratedDT)  //FinalDT = actual broker fill time
						.Where(dt => dt > DateTime.MinValue)
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