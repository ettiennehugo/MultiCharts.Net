using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Implements common utility functions used by signals and calculation of intraday high, low, open and close
	/// if the signal uses minute bars in conjunction with daily/weekly/monthly bars.
	/// </summary>
	public abstract class _CustomSignal : PortfolioSignalObject
	{
		//constants
		public double DEFAULT_ACCOUNT_RECALC_INTERVAL = 5;	//number of seconds after which account info would be recalculated if accounts are not yet loaded

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
			Verbose,    //trace all messages
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

		/// <summary>
		/// Singleton log file manager for internal use only.
		/// </summary>
		private sealed class LogFileSingleton
		{
			private static LogFileSingleton s_instance;
			private static readonly object s_lock = new object();

			private string m_logFilePath;
			private IOutput m_output; //MC main output window

			private LogFileSingleton(IOutput output)
			{
				m_output = output;
				string logFilename = $"MCSignal{DateTime.Now:yyyyMMdd-HHmm}.csv";   //log files are csv's for analysis in Excel
				try
				{
					m_logFilePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "MCSignalLogs", logFilename);
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
					}

					return s_instance;
				}
			}

			public void WriteTrace(ETraceLevel level, DateTime timestamp, string signalName, string symbol, string message)
			{
				lock (s_lock)
				{
					try
					{
						string csvMessage = escapeCsv(message);
						File.AppendAllText(m_logFilePath, $"{level},{timestamp:yyyy-MM-dd HH:mm:ss.fff},{signalName},{symbol},{csvMessage}{System.Environment.NewLine}");
					}
					catch (Exception ex)
					{
						m_output.WriteLine($"Error writing to log file: {ex.Message}");
					}
				}
			}

			private string escapeCsv(string input)
			{
				if (string.IsNullOrEmpty(input))
					return input;

				//check if escaping is needed
				bool needsQuotes = input.IndexOfAny(new char[] { ',', '"', '\n', '\r' }) >= 0;

				if (!needsQuotes)
					return input;

				//build escaped version
				StringBuilder sb = new StringBuilder();
				sb.Append('"'); //start quote

				foreach (char c in input)
				{
					if (c == '"')
						sb.Append("\"\""); //escape quote
					else
						sb.Append(c);
				}

				sb.Append('"'); //end quote
				return sb.ToString();
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

		public List<DateTime> Holidays { get { return m_holidays.Holidays; } }

		[Input] public virtual ETraceLevel TraceLevel { get; set; }

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
		private PowerLanguage.Function.USHolidays m_holidays;
		private LogFileSingleton m_logger;

		//interface methods
		public _CustomSignal(object _ctx) : base(_ctx) { }

		//methods
		public void trace(ETraceLevel traceLevel, string message)
		{
			if (TraceLevel >= traceLevel)
			{
				if (m_logger == null) m_logger = LogFileSingleton.GetInstance(Output);
				m_logger.WriteTrace(traceLevel, Bars.Time[0].ToLocalTime(), this.GetType().Name, this.Bars.Info.Name, message);
			}
		}

		protected override void Create()
		{
			base.Create();
			m_holidays = new PowerLanguage.Function.USHolidays(this);
			TraceLevel = ETraceLevel.None;
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_currentDay = DateTime.Now.Date;
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
		}

		/// <summary>
		/// Assumes that Bars is a minute bar and that the first bar of the day is the open. The first bar of the day is used to set the
		/// </summary>
		protected override void CalcBar()
		{
			//ensure latest account equity is available, this method seems to kick off async internal processes
			//to update interface elements like the Accounts etc, this can be deduced from how the interface looks
			//with callbacks like OnRefreshComplete etc. If you call it right before accessing the account data
			//those accounts would not be set and your results would be incorrect so initially we keep on calling
			//the CalcBar method until we have accounts loaded
			if (Environment.IsAutoTradingMode) {
				TradeManager.ProcessEvents();

				//force recalculation after a specific interval if account information is not yet loaded from the above ProcessEvents call
				if (m_loadAccounts)
				{
					if (TradeManager.TradingData.Accounts.Items.Length == 0) {
						trace(ETraceLevel.Warning,"ProcessEvents failed to load account queuing recalculation.");
						ExecControl.RecalcLastBarAfter(TimeSpan.FromSeconds(DEFAULT_ACCOUNT_RECALC_INTERVAL));
					} else {
						trace(ETraceLevel.Info,$"Loaded {TradeManager.TradingData.Accounts.Items.Length} accounts from broker.");
						m_loadAccounts = false;
					}
				}
			}
			
			if (Environment.CalcReason == CalculationReason.Default) {
				if (Bars.Time[0].Date != m_currentDay.Date)
				{
					m_intradayBarCount = 0;
					m_intradayOpen = Bars.Open[0];
					m_intradayClose = Bars.Close[0];
					m_intradayLow = Bars.Low[0];
					m_intradayHigh = Bars.High[0];
					m_currentDay = Bars.Time[0].Date;
					m_intradayVolume = Bars.Volume[0];
					m_currentDay = Bars.Time[0];
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
		}

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
				trace(ETraceLevel.Verbose, $"Checking {this.TradeManager.TradingData.Accounts.Items.Length} accounts to find available equity on signal account {this.Account}.");
				foreach (var account in this.TradeManager.TradingData.Accounts.Items)
				{
					trace(ETraceLevel.Verbose, $"Found account {account.Name} with equity {account.Equity ?? 0}.");
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
						trace(ETraceLevel.Verbose, $"Equity determined from account - {equityAvailable}");
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
						trace(ETraceLevel.Verbose, $"Equity determined from account - {equityAvailable}");
						equityAvailable *= percentEquity;
					}
					else
					{
						equityAvailable = this.InitialCapital + this.Portfolio.NetProfit;
						equityAvailable *= percentEquity;
					}
					break;
			}

			return equityAvailable;
		}

		/// <summary>
		/// Determine the position size based on a given entry price and a maximum equity per position as a percentage of the available equity.
		/// </summary>
		protected int positionSize(double entryPrice, double maxEquityPerPosPercent = 0.0d, bool useRoundLots = false)
		{
			double positionCash = equityAvailable(maxEquityPerPosPercent);
			int result = (int)Math.Truncate(positionCash / entryPrice);
			if (useRoundLots) result = (int)(Math.Truncate(result / 100.0d) * 100.0d); //round to the nearest 100 shares for round lot orders
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
		protected int positionSize(IInstrument bars, double maxRiskEquityPerPosPercent = 0.0d, bool useRoundLots = false)
		{
			int result = 0;
			if (bars.Close[0] == 0.0d) return result;

			double positionCash = equityAvailable(maxRiskEquityPerPosPercent);
			result = (int)Math.Truncate(positionCash / bars.Close[0]);
			if (useRoundLots) result = (int)(Math.Truncate(result / 100.0d) * 100.0d); //round to the nearest 100 shares for round lot orders

			return result;
		}

		/// <summary>
		/// Position size based on the maximum risk equity per position as a percentage of equity per position.
		/// </summary>
		protected int riskBasedPositionSize(double risk, double maxRiskEquityPerPosPercent, bool useRoundLots = false)
		{
			int result = 0;
			if (risk == 0.0d) return result;

			double positionCash = 0.0;
			try
			{
				positionCash = equityAvailable(maxRiskEquityPerPosPercent);
				result = (int)Math.Truncate(positionCash / risk);
			}
			catch (Exception e)
			{
				trace(ETraceLevel.Error, $"Exception raised computing risk based position size risk {risk} positionCash {positionCash} - message {e.Message}");
			}

			if (useRoundLots) result = (int)(Math.Truncate(result / 100.0d) * 100.0d); //round to the nearest 100 shares for round lot orders
			return result;
		}

		/// <summary>
		/// Position size based on the maximum risk equity per position as a percentage of equity and a given stop loss price.
		/// </summary>
		protected int stopLossBasedPositionSize(IInstrument bars, double stopLossPrice, double maxRiskEquityPerPosPercent = 0.0d, bool useRoundLots = false)
		{
			int result = 0;

			double risk;
			try
			{
				risk = checked(Math.Round(Math.Abs(bars.Close[0] - stopLossPrice), 2));
				result = riskBasedPositionSize(risk, maxRiskEquityPerPosPercent);
			}
			catch (OverflowException)
			{
				trace(ETraceLevel.Warning, "WARNING - positionSize based on STOP-LOSS raised an overflow exception, signal ignored.");
				return 0;   //risk overflow exception occurred, ignore signal
			}

			if (useRoundLots) result = (int)(Math.Truncate(result / 100.0d) * 100.0d); //round to the nearest 100 shares for round lot orders
			return result;
		}

		/// <summary>
		/// Postion size based on a ideal size position to take based on risk/equity but the position size is clipped to a specific
		/// maximum average volume percentage. The ideal size can be computed with one of the above positionSize overloads and then
		/// fed into this method to clip the size as a percentage of average volume.
		/// </summary>
		protected int volumeBasedPositionSize(IInstrument bars, int volumePeriod, double maxAvgVolumePercent, bool useRoundLots = false)
		{
			int result = 0; //do not enter a position if we do not have enough data to compute the average volume

			if (maxAvgVolumePercent > 0.0d && bars.CurrentBar >= volumePeriod)
			{
				try
				{
					result = (int)checked(Math.Round(bars.Volume.Average(volumePeriod) * maxAvgVolumePercent, 0));
				}
				catch (OverflowException)
				{
					trace(ETraceLevel.Warning, "WARNING - positionSize based on VOLUME raised an overflow exception, signal ignored.");
					return 0;   //risk overflow exception occurred, ignore signal
				}
			}

			if (useRoundLots) result = (int)(Math.Truncate(result / 100.0d) * 100.0d); //round to the nearest 100 shares for round lot orders
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
			if (entryDate.Date > currentDateTime.Date) throw new ArgumentException("entryDate must always smaller than currentDateTime");
			TimeSpan days = currentDateTime - entryDate;
			return days.Days;
		}

		/// <summary>
		/// Calculates the number of trading days between two dates using the US stock market calendar.
		/// </summary>
		protected int tradingDayCount(DateTime entryDate, DateTime currentDateTime)
		{
			if (entryDate.Date > currentDateTime.Date) throw new ArgumentException("entryDate must always smaller than currentDateTime");
			DateTime date = currentDateTime;
			int count = 0;

			if (Bars.Info.Category != ESymbolCategory.Stock)
				trace(ETraceLevel.Warning, "WARNING - ExitDays used with non-stock object using TradingDays specification calculated around stock market calendar.");

			while (date.Date > entryDate.Date)
			{
				//NOTE: This will not work correctly for non-US markets and non-equities that trade over the weekends, e.g. Forex.
				if (!m_holidays.Holidays.Contains(date) && date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday) count++;
				date = date.AddDays(-1);
			}

			return count;
		}

		/// <summary>
		/// Scans back from the current bar the given number of days back and returns the lowest low found.
		/// E.g. 1 day back would be the previous trading day, if days back is set to 0 it will return the current day low.
		/// </summary>
		protected Tuple<DateTime, double> recentLow(int daysBack, DateTime? earliestDateTime = null)
		{
			if (daysBack == 0) return new Tuple<DateTime, double>(IntradayLowTime, IntradayLow);
			double low = IntradayLow;
			DateTime lowBarTime = IntradayLowTime;
			DateTime lowDate = IntradayLowTime;
			int daysProcessed = 0;
			DateTime startDate = Bars.Time[0].Date;
			DateTime lastDate = startDate;

			for (int i = 0; i < Bars.CurrentBar; i++)
			{
				if (Bars.Time[i].Date != lastDate)
				{
					daysProcessed++;
					lastDate = Bars.Time[i].Date;
				}

				if (daysProcessed > daysBack || earliestDateTime?.CompareTo(Bars.Time[i]) > 0)
					break;

				if (Bars.Time[i].Date != startDate && low > Bars.Low[i])
				{
					low = Bars.Low[i];
					lowBarTime = Bars.Time[i];
				}

				if (i == ExecInfo.MaxBarsBack - 1)
				{
					trace(ETraceLevel.Warning, $"recentLow: Reached MaxBarsBack looking {daysBack} days back at index {i} with low {low} that might be incorrect.");
					break;
				}
			}

			return new Tuple<DateTime, double>(lowBarTime, low);
		}

		/// <summary>
		/// Scans back from the current bar the given number of days back and returns the highest high found.
		/// E.g. 1 day back would be the previous trading day, if days back is set to 0 it will return the current day high.
		/// </summary>
		protected Tuple<DateTime, double> recentHigh(int daysBack, DateTime? earliestDateTime = null)
		{
			if (daysBack == 0) return new Tuple<DateTime, double>(IntradayHighTime, IntradayHigh);
			double high = IntradayHigh;
			DateTime highBarTime = IntradayHighTime;
			int daysProcessed = 0;
			DateTime startDate = Bars.Time[0].Date;
			DateTime lastDate = startDate;

			for (int i = 0; i < Bars.CurrentBar; i++)
			{
				if (Bars.Time[i].Date != lastDate)
				{
					daysProcessed++;
					lastDate = Bars.Time[i].Date;
				}

				if (daysProcessed > daysBack || earliestDateTime?.CompareTo(Bars.Time[i]) > 0)
					break;

				if (Bars.Time[i].Date != startDate && high < Bars.High[i])
				{
					high = Bars.High[i];
					highBarTime = Bars.Time[i];
				}

				if (i == ExecInfo.MaxBarsBack - 1)
				{
					trace(ETraceLevel.Warning, $"recentHigh: Reached MaxBarsBack looking {daysBack} days back at index {i} with high {high} that might be incorrect.");
					break;
				}
			}

			return new Tuple<DateTime, double>(highBarTime, high);
		}

		public DateTime EntryTime(int posAgo, EUseFill useFill)
		{
			if (useFill == EUseFill.Last) return this.EntryTime(posAgo);

			//useFill == EUseFill.First
			DateTime entryDate = DateTime.MaxValue;
			if (Positions[0].OpenTrades.Count > 0 && Positions[0].ClosedTrades.Count > 0)
			{
				DateTime openTradesEarliestDate = Positions[0].OpenTrades.Min(trade => trade.EntryOrder.Time);
				DateTime closedTradesEarliestDate = Positions[0].ClosedTrades.Min(trade => trade.EntryOrder.Time);
				entryDate = openTradesEarliestDate < closedTradesEarliestDate ? openTradesEarliestDate : closedTradesEarliestDate;
			}
			else if (Positions[0].OpenTrades.Count > 0)
				entryDate = Positions[0].OpenTrades.Min(trade => trade.EntryOrder.Time);
			else if (Positions[0].ClosedTrades.Count > 0)
				entryDate = Positions[0].ClosedTrades.Min(trade => trade.EntryOrder.Time);

			if (entryDate == DateTime.MaxValue)
				trace(ETraceLevel.Verbose, $"Unable to compute position entry date with {Positions[0].OpenTrades.Count} open trades and {Positions[0].ClosedTrades.Count} closed trades");
			else
				trace(ETraceLevel.Verbose, $"Position entry date {entryDate.ToString("yyyyMMdd hh:mm:ss")} with {Positions[0].OpenTrades.Count} open trades and {Positions[0].ClosedTrades.Count} closed trades");
			return entryDate;
		}
	}
}