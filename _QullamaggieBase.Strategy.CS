﻿using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Base class for strategies based on the Kristjan Qullamaggie's trading style, provides basic filters and functionality to enter the market
	/// and sub-classes can refine the specific calculations for the entry conditions. Adds concepts to measure that the stock is moving enough
	/// to be considered for trading and that the stock has enough volume to be considered for trading.
	/// Functions are also provided to scan for multiple lookback periods to find the best stocks to trade.
	/// Trade management is provided to exit the trade after a specific number of days or when the stock closes below a moving average.
	/// Requires the following data feeds:
	/// 1. Entry data feed - typically some minute data feed.
	/// 2. Volume data feed - typically daily data feed for the same instrument.
	/// </summary>
	public abstract class _QullamaggieBase : _OpeningRangeBarSignal {
		//constants
		public const double DEFAULT_MINIMUM_ADR = 0.02;
		public const double DEFAULT_MINIMUM_PRICE = 1.0;
		public int DEFAULT_MINIMUM_VOLUME = 4000000;
		public int DEFAULT_MINIMUM_DOLLAR_VOLUME = 15000000;
		public const int DEFAULT_EXIT_FIRST_EXIT_DAYS = 5;
		public const int DEFAULT_EXIT_MOVING_AVERAGE_LENGTH = 10;
		public const int DEFAULT_EXIT_SECOND_MINUTES_BEFORE_SESSION_END = 10;

		//types


		//properties
		[Input] public double EntryVolumePercent { get; set; }  //entry volume percent of the average volume that would be considered a high volume bar, e.g. 0.25 means we want to see about 25% of the average volume to consider the bar a high volume bar
		[Input] public int EntryADRLength { get; set; }  //average daily range length
		[Input] public double EntryMinimumADR { get; set; }  //minimum ADR to consider a stock for trading
		[Input] public double EntryMinimumPrice { get; set; }  //minimum price to consider a stock for trading
		[Input] public int EntryVolumeLength { get; set; }  //volume length to use for opening bar volume detection
		[Input] public int EntryMinimumVolume { get; set; }  //minimum volume to consider a stock for trading
		[Input] public int EntryMinimumDollarVolume { get; set; }  //minimum dollar volume to consider a stock for trading
		[Input] public double EntryClosePercentile { get; set; } //close percentile in which directional trade must be made
		[Input] public double EntryMaxVolumeBasedPosPercent { get; set; }  //maximum volume based position size as a percentage of equity
		[Input] public double EntryStopLossBufferPrice { get; set; }	//buffer price to add to the entry price to avoid slippage
		[Input] public int ExitMovingAverageLength { get; set; }    //moving average length used for exits
		[Input] public double ExitFirstExitPercent { get; set; }    //percentage of position to exit after below n-days (0 to disable)
		[Input] public int ExitFirstExitDays { get; set; }          //number of days after which we should exit the first part of the 
		[Input] public int ExitSecondMinutesBeforeSessionEnd { get; set; }  //minutes before the session end to start sending exit market orders
		[Input] public bool Debug { get; set; }                 //debug flag to print out debug information

		//attribute
		protected PowerLanguage.Function._ADR m_adr;
		protected PowerLanguage.Function._VolumeWithSD m_volumeWithSD;
		protected PowerLanguage.Function._SuperSmoother m_exitMovingAverage;
		private IOrderMarket m_firstLx;
		private IOrderMarket m_firstSx;
		private IOrderMarket m_secondLx;
		private IOrderMarket m_secondSx;
		private DateTime m_entryDateTime;
		private int m_entrySize;
		private TimeSpan m_exitSecondTimeSpan;

		//interface methods
		public _QullamaggieBase(object _ctx):base(_ctx){}

		protected override void Create() {
			base.Create();
			EntryVolumePercent = DEFAULT_ENTRY_VOLUME_PERCENT;
			EntryADRLength = PowerLanguage.Function._ADR.DEFAULT_LENGTH;
			EntryMinimumADR = DEFAULT_MINIMUM_ADR;
			EntryMinimumPrice = DEFAULT_MINIMUM_PRICE;
			EntryVolumeLength = PowerLanguage.Function._VolumeWithSD.DEFAULT_LENGTH;
			EntryMinimumVolume = DEFAULT_MINIMUM_VOLUME;
			EntryMinimumDollarVolume = DEFAULT_MINIMUM_DOLLAR_VOLUME;
			EntryClosePercentile = DEFAULT_CLOSE_PERCENTILE;
			EntryMaxVolumeBasedPosPercent = DEFAULT_MAX_VOLUME_BASED_POS_PERCENT;
			EntryStopLossBufferPrice = DEFAULT_ENTRY_STOP_LOSS_BUFFER_PRICE;
			ExitFirstExitPercent = DEFAULT_EXIT_FIRST_EXIT_DAYS;
			ExitFirstExitDays = DEFAULT_EXIT_FIRST_EXIT_DAYS;
			ExitMovingAverageLength = DEFAULT_EXIT_MOVING_AVERAGE_LENGTH;
			ExitSecondMinutesBeforeSessionEnd = DEFAULT_EXIT_SECOND_MINUTES_BEFORE_SESSION_END;

			Debug = false;

			m_firstLx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "scs1LX", EOrderAction.Sell));
			m_firstSx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "scs1SX", EOrderAction.BuyToCover));
			m_secondLx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "scs2LX", EOrderAction.Sell));
			m_secondSx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "scs2SX", EOrderAction.BuyToCover));

			m_adr = new PowerLanguage.Function._ADR(this);
			m_volumeWithSD = new PowerLanguage.Function._VolumeWithSD(this);
			m_exitMovingAverage = new PowerLanguage.Function._SuperSmoother(this);
		}

		protected override void StartCalc() {
			_EntryDataFeed = 1;
			_VolumeDataFeed = 2;
			_EntryVolumePercent = EntryVolumePercent;
			_VolumeLength = EntryVolumeLength;
			_ClosePercentile = EntryClosePercentile;
			_MaxVolumeBasedPosPercent = EntryMaxVolumeBasedPosPercent;

			base.StartCalc();

			if (EntryADRLength <= 0) throw new ArgumentException("ADRLength must be positive");
			if (EntryMinimumADR <= 0) throw new ArgumentException("MinimumADR must be positive");
			if (EntryMinimumPrice <= 0) throw new ArgumentException("MinimumPrice must be positive");
			if (EntryVolumeLength <= 0) throw new ArgumentException("VolumeLength must be positive");
			if (EntryMinimumVolume <= 0) throw new ArgumentException("MinimumVolume must be positive");
			if (EntryMinimumDollarVolume <= 0) throw new ArgumentException("MinimumDollarVolume must be positive");


			m_adr.Length = EntryADRLength;
			m_adr.Instrument = BarsOfData(2);
			m_volumeWithSD.Length = EntryVolumeLength;
			m_volumeWithSD.Instrument = BarsOfData(2);
			m_exitMovingAverage.Instrument = m_volumeInstrument;
			m_exitMovingAverage.Length = ExitMovingAverageLength;
		}

		protected override void CalcBar()
		{
			if (PositionSide == 0)
			{
				m_entryDateTime = DateTime.MinValue;
				m_riskAmountPerContract = 0;
				if (calcEntry())
					base.CalcBar();
			}
			else
				calcExit();
		}

		/// <summary>
		/// Handle the common entry rules for the Qullamaggie strategy.
		/// </summary>
		protected virtual bool calcEntry()
		{
			if (m_adr[0] < EntryMinimumADR) return false;
			if (Bars.Close[0] < EntryMinimumPrice) return false;
			if (m_volumeWithSD[0] < EntryMinimumVolume) return false;
			if (m_volumeWithSD.AverageDollarVolume[0] < EntryMinimumDollarVolume) return false;
			return true;
		}

		/// <summary>
		/// Implement exit rules around the first and second exits.
		/// </summary>
		protected void calcExit()
		{
			//record first day of trade to determine n-day exit
			if (m_entryDateTime == DateTime.MinValue) m_entryDateTime = m_volumeInstrument.Time[0];

			//STOP-LOSS - keep stop-loss in place based on entry bar
			CurSpecOrdersMode = ESpecOrdersMode.PerContract;

			//FIRST EXIT - if we're still holding the full entry position exit half position into the n-day exit
			int currentPositionSize = (int)Math.Abs(StrategyInfo.MarketPosition);
			bool scanForSecondExit = m_entrySize != currentPositionSize;    //second exit scan will only be done once the first exit was performed
			if (!scanForSecondExit)
			{
				TimeSpan entryToCurrentBar = m_volumeInstrument.Time[0] - m_entryDateTime;
				if (entryToCurrentBar.Days >= ExitFirstExitDays)
				{
					int sizeToClose = (int)Math.Truncate((double)m_entrySize / 2);
					if (PositionSide > 0)
					{
						if (Debug) Output.WriteLine("{0} - First long exit on {1}, selling {2} at {3}, {4}", GetType().Name, m_volumeInstrument.Info.Name, sizeToClose, m_volumeInstrument.Time[0], Bars.Close[0]);
						m_firstLx.Send(sizeToClose);
					}
					else
					{
						if (Debug) Output.WriteLine("{0} - First short exit on {1}, buying {2} at {3}, {4}", GetType().Name, m_volumeInstrument.Info.Name, sizeToClose, m_volumeInstrument.Time[0], Bars.Close[0]);
						m_firstSx.Send(sizeToClose);
					}
				}
			}
			else if (StrategyInfo.AvgEntryPrice != m_riskAmountPerContract)
				m_riskAmountPerContract = StrategyInfo.AvgEntryPrice;   //move stop-loss to break even

			GenerateStopLoss(m_riskAmountPerContract);

			//SECOND EXIT - determine whether we're getting within the n-minute window of the session exit to start
			//exit when the bar closes above/below the moving average based off associated entry and
			//we're getting close (n-minutes) from the session close
			if (scanForSecondExit)
			{
				scanForSecondExit = false;  //only scan for exit once we get into the n-minute window from the close
				TimeSpan currentBarTime = Bars.Time[0].TimeOfDay;

				foreach (var session in Bars.Sessions)
				{
					if (session.EndDay == Bars.Time[0].DayOfWeek &&
							session.EndTime - currentBarTime <= m_exitSecondTimeSpan)
					{
						scanForSecondExit = true;
						break;
					}
				}

				if (PositionSide > 0)
				{
					if (Bars.Close[0] < m_exitMovingAverage[0])
					{
						if (Debug) Output.WriteLine("{0} - Second long exit on {1}, selling {2} at {3}, {4}", GetType().Name, m_volumeInstrument.Info.Name, currentPositionSize, m_volumeInstrument.Time[0], Bars.Close[0]);
						m_secondLx.Send();
					}
				}

				if (PositionSide < 0)
				{
					if (Bars.Close[0] > m_exitMovingAverage[0])
					{
						if (Debug) Output.WriteLine("{0} - Second short exit on {1}, buying {2} at {3}, {4}", GetType().Name, m_volumeInstrument.Info.Name, currentPositionSize, m_volumeInstrument.Time[0], Bars.Close[0]);
						m_secondSx.Send();
					}
				}
			}
		}

		//methods
		/// <summary>
		/// Returns true if the stock passes the Qullamaggie filter, false otherwise.
		/// </summary>
		protected bool qullamaggieFilter()
		{
			if (m_adr[0] < EntryMinimumADR) return false;
			if (Bars.Close[0] < EntryMinimumPrice) return false;
			if (m_volumeWithSD[0] < EntryMinimumVolume) return false;
			if (m_volumeWithSD.AverageDollarVolume[0] < EntryMinimumDollarVolume) return false;
			return true;
		}

		/// <summary>
		/// Parse a comma separated list of lookback periods and return an array of integers for the lookbacks..
		/// </summary>
		protected int[] parseLookbackList(string _lookupList)
		{
			string[] symbols = _lookupList.Split(',');
			int[] result = new int[symbols.Length];
			for (int i = 0; i < symbols.Length; i++)
				result[i] = int.Parse(symbols[i]);
			return result;
		}

		/// <summary>
		/// Computes the growth percentage over a specific lookback period. 
		/// </summary>
		protected double growthPercent(IInstrument bars, int lookback)
		{
			if (bars.CurrentBar <= lookback) return 0;  //not enough data yet
			return bars.Close[0] / bars.Close[lookback];
		}
	}
}