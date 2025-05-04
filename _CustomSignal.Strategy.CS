using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.Indicator;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Implements common utility functions used by signals and calculation of intraday high, low, open and close
	/// if the signal uses minute bars in conjunction with daily/weekly/monthly bars.
	/// </summary>
	public abstract class _CustomSignal : PortfolioSignalObject
	{
		//constants


		//types
		/// <summary>
		/// Trace level to use for debugging and logging.
		/// </summary>
		public enum ETraceLevel
		{
			None = 0,   //no trace
			Info,
			Warning,
			Error,
			Verbose,    //trace all messages
		}

		//inputs


		//properties
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

		[Input] public ETraceLevel TraceLevel { get; set; }

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
		private PowerLanguage.Function.USHolidays m_holidays;

		//interface methods
		public _CustomSignal(object _ctx) : base(_ctx) { }

		//methods
		public void trace(ETraceLevel traceLevel, string message)
		{
			if (TraceLevel >= traceLevel) Output.WriteLine(message);
		}

		protected override void Create()
		{
			base.Create();
			m_holidays = new PowerLanguage.Function.USHolidays(this);
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_currentDay = DateTime.Now.Date;
			m_intradayLowTime = DateTime.Now;
			m_intradayHighTime = DateTime.Now;
			m_intradayCloseTime = DateTime.Now;
			m_intradayOpenTime = DateTime.Now;
			m_intradayLow = double.MaxValue;
			m_intradayHigh = double.MinValue;
			m_intradayClose = 0.0d;
			m_intradayOpen = 0.0d;
			m_intradayVolume = 0.0d;
		}

		/// <summary>
		/// Assumes that Bars is a minute bar and that the first bar of the day is the open. The first bar of the day is used to set the
		/// </summary>
		protected override void CalcBar()
		{
			if (Bars.Time[0].Date != m_currentDay.Date)
			{
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
				m_intradayClose = Bars.Close[0];
				m_intradayCloseTime = Bars.Time[0];
				m_intradayVolume += Bars.Volume[0];
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

			//NOTE: This only works for single entries, need to see how this will work for multiple entries.
			switch (Environment.ApplicationCode)
			{
				case EApplicationCode.Charting:
					equityAvailable = maxCustomRiskEquityPerPosPercent > 0.0d ? (this.InitialCapital + this.NetProfit) * maxCustomRiskEquityPerPosPercent : this.InitialCapital + this.NetProfit;
					break;
				case EApplicationCode.Portfolio:
					double percentEquity = maxCustomRiskEquityPerPosPercent > 0.0d ? maxCustomRiskEquityPerPosPercent : this.Portfolio.MaxRiskEquityPerPosPercent / 100.0d;
					equityAvailable = (this.InitialCapital + this.Portfolio.NetProfit) * percentEquity;
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
			double positionCash = equityAvailable(maxRiskEquityPerPosPercent);
			result = (int)Math.Truncate(positionCash / risk);
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
				Output.WriteLine("WARNING - positionSize based on STOP-LOSS raised an overflow exception, signal ignored.");
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
		protected int volumeBasedPositionSize(IInstrument bars, int size, int volumePeriod, double maxAvgVolumePercent, bool useRoundLots = false)
		{
			int result = 0; //do not enter a position if we do not have enough data to compute the average volume

			if (maxAvgVolumePercent > 0.0d && bars.CurrentBar >= volumePeriod)
			{
				try
				{
					int clippedSize = (int)checked(Math.Round(bars.Volume.Average(volumePeriod) * maxAvgVolumePercent, 0));
					result = clippedSize < size ? clippedSize : size;
				}
				catch (OverflowException)
				{
					Output.WriteLine("WARNING - positionSize based on VOLUME raised an overflow exception, signal ignored.");
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
			TimeSpan days = currentDateTime - entryDate;
			return days.Days;
		}

		/// <summary>
		/// Calculates the number of trading days between two dates using the US stock market calendar.
		/// </summary>
		protected int tradingDayCount(DateTime entryDate, DateTime currentDateTime)
		{
			DateTime date = currentDateTime;
			int count = 0;

			if (Bars.Info.Category != ESymbolCategory.Stock)
				Output.WriteLine("WARNING - ExitDays used with non-stock object using TradingDays specification calculated around stock market calendar.");

			while (date.Date > entryDate.Date)
			{
				//NOTE: This will not work correctly for non-US markets and non-equities that trade over the weekends, e.g. Forex.
				if (!m_holidays.Holidays.Contains(date) && date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday) count++;
				date = date.AddDays(-1);
			}

			return count;
		}

	}
}