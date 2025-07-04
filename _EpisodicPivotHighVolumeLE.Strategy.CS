﻿using Microsoft.CodeAnalysis;
using System;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// High volume episodic pivot long entry, use this with stocks that are already in a strong uptrend it will wait for a high volume bullish DAILY bar to enter a long position. Pradeep Bond
	/// calls this a 9mil+EP since he uses 9 million as the above average volume to determine whether he'll enter a long position or not.
	/// Use the EpisodicPivotLX strategy to exit the long position.
	/// Data Feeds:
	///		1 Something like the 5-minute chart for the entry
	///		2 Daily chart for determining episodic pivot and the moving averages used
	///		3 Market daily time frame that is used to determine whether to enter a position or not, e.g. SPY, QQQ, IWM, etc.
	///	Entry Rules:
	///	• Larger cap stocks(>10B) tend to move sideways to up and can have a delayed reaction, so you might need different trade management for them.
	///		○ 84% of large cap stocks tend to move sideways to upward for at least 5 days - this strategy just works on the inputs, any partitioning on fundamental factors need to be made in the portfolio manager
	///		○ Maybe user a slightly less percentage since these stocks do not necessarily move as quickly as smaller cap stocks.
	///
	///		1. Scan - stocks that are up 30%+ over the last 1 to 3 months.
	///		2. Big volume move on the daily timeframe at least a specific minimum move on very high volume - this is considered an episodic pivot move with volume catalyst, this could be an earnings call or some other catalyst.
	///		3. Best if stock comes out of base without being overextended to the up side
	///		4. Initial risk is the entry day low.
	/// Notes:
	///	• Can use the standard EpisodicPivotLX with this strategy. 
	/// </summary>
	public class _EpisodicPivotHighVolumeLE : _CustomSignal {
		//constants
		public const string DEFAULT_START_TIME = "09:30"; //stock market regular open time
		public const string DEFAULT_END_TIME = "16:00";   //stock market regular close time
		public int DEFAULT_ENTRIES_PER_TIME_FILTER = 1; //number of entries per time filter - this is used to determine the number of entries per time filter
		public const double DEFAULT_MINIMUM_PERCENTAGE_CHANGE = 0.1;      //ignore stocks that fall below this minimum percentage change
		public const double DEFAULT_MAXIMUM_PERCENTAGE_CHANGE = 0.3;      //ignore stocks that have massive overextended moves as to avoid parabolic moves that will just reverse
		public const double DEFAULT_MINIMUM_ABOVE_AVERAGE_PCT_VOLUME = -1; //[disable == -1] minimum above average DAILY volume percentage for the entry - this is used for volume confirmation, e.g. 0.1 means 10% of the daily volume needs to be traded on the entry bar \
		public const int DEFAULT_ABSOLUTE_ABOVE_AVERAGE_VOLUME = 9000000; //[disable == -1] absolute above average volume for the entry - this is used for volume confirmation of the number of shares traded above the volume average
		public const double DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT = 0.3; //per default don't enter positions larger than 30% of available equity
		public const double DEFAULT_RISK_BASED_ENTRY_PERCENT = 0.01;    //use 1% of equity for risk based entry
		public const double DEFAULT_REQUIRED_CLOSE_PERCENTILE = 0.75; //required close percentile - this is used to determine the entry size based on the average trading volume 
		public const int DEFAULT_VOLUME_LENGTH = 20;        //length of the volume for the entry size
		public const int DEFAULT_BARS_ABOVE_MARKET_AVERAGE = 5; //number of bars above the market moving average to determine whether to enter a position or not, this is used to determine the general market direction
		public const int DEFAULT_MARKET_LENGTH = 50;				//length of the market moving average used to determine general market direction
		public const double DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT = 0.2; //do not exceed 20% of the average trading volume for the entry size

		//types


		//properties
		[Input] public string StartTime { get; set; } //start time for the entry
		[Input] public string EndTime { get; set; } //end time for the entry
		[Input] public int EntriesPerTimeFilter { get; set; } //number of entries per time filter - this is used to determine the number of entries per time filter
		[Input] public double MinimumPercentageChange { get; set; } //gap up/down must be at least this minimum - this must always be a positive value, algoritm will flip it around if required
		[Input] public double MaximumPercentageChange { get; set; } //gap up/down clipped to this maximum to avoid overextended stocks - this must always be a positive value, algorithm will flip it around if required
		[Input] public double MinimumAboveAveragePctVolume { get; set; } //[alternate] minimum above average volume for the entry - this is used for volume confirmation
		[Input] public int AbsoluteAboveAverageVolume { get; set; } //[alternate] absolute above average volume for the entry - this is used for volume confirmation of the number of shares traded above the volume average
		[Input] public double MaximumEquityPerPosPercent { get; set; } //maximum equity per position as a percentage of the available equity - this is used to determine the maximum position size for the entry
		[Input] public double RiskBasedEntryPercent { get; set; } //risk based entry percentage - this is used to base the position size as a percentage of the euity based on the risk taken, set to zero to disable risk based entries
		[Input] public double MaximumVolumeBasedPercent { get; set; } //maximum volume based percentage - this is used to determine the entry size based on the average trading volume\
		[Input] public int VolumeLength { get; set; } //length of the volume for the entry size - this is used to determine the entry size based on the average trading volume
		[Input] public double RequiredClosePercentile { get; set; } //required close percentile - this is used to determine the entry size based on the average trading volume
		[Input] public bool UseOpeningRangeBreak { get; set; } //use opening range break for entry - the entry will wait for a solid bullish bar that forms a new high for the day
		[Input] public int BarsAboveMarketAverage { get; set; } //number of bars above the market moving average to determine whether to enter a position or not, this is used to determine the general market direction, set to zero (0) to disable market filter
		[Input] public int MarketAverageLength {  get; set; } //length of the market moving average used to determine general market direction
		[Input] public bool UseRoundLots { get; set; } //use round lots for the entry size - this is used to determine the entry size based on the average trading volume

		//attribute
		private IOrderPriced m_le;
		private DateTime m_startTime;
		private DateTime m_endTime;
		private DateTime m_previousDayFilter;
		private IInstrument m_minuteTimeFrame;
		private IInstrument m_dailyTimeFrame;
		private IInstrument m_marketTimeFrame;
		private double m_minimumAboveAveragePctVolume;
		private int m_entriesPerTimeFilter;
		private int m_previousPositionSide;
		private PowerLanguage.Function._VolumeWithSD m_volumeWithSD;
		private PowerLanguage.Function._AverageSimple m_marketMovingAverage;

		//interface methods
		public _EpisodicPivotHighVolumeLE(object _ctx):base(_ctx){}

		protected override void Create() {
			base.Create();
			StartTime = DEFAULT_START_TIME;
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			EndTime = DEFAULT_END_TIME;
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			EntriesPerTimeFilter = DEFAULT_ENTRIES_PER_TIME_FILTER;
			m_previousDayFilter = DateTime.MinValue;
			MinimumPercentageChange = DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
			MaximumPercentageChange = DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;
			MinimumAboveAveragePctVolume = DEFAULT_MINIMUM_ABOVE_AVERAGE_PCT_VOLUME;
			MaximumEquityPerPosPercent = DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT;
			AbsoluteAboveAverageVolume = DEFAULT_ABSOLUTE_ABOVE_AVERAGE_VOLUME;
			RiskBasedEntryPercent = DEFAULT_RISK_BASED_ENTRY_PERCENT;
			MaximumVolumeBasedPercent = DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT;
			VolumeLength = DEFAULT_VOLUME_LENGTH;
			RequiredClosePercentile = DEFAULT_REQUIRED_CLOSE_PERCENTILE;
			UseOpeningRangeBreak = false;
			BarsAboveMarketAverage = DEFAULT_BARS_ABOVE_MARKET_AVERAGE;
			MarketAverageLength = DEFAULT_MARKET_LENGTH;
			UseRoundLots = false;
			m_le = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "Le", EOrderAction.Buy));
			m_volumeWithSD = new PowerLanguage.Function._VolumeWithSD(this);
			m_marketMovingAverage = new PowerLanguage.Function._AverageSimple(this);
		}

		protected override void StartCalc() {
			base.StartCalc();
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			m_entriesPerTimeFilter = 0;
			m_previousPositionSide = 0;
			if (EntriesPerTimeFilter < 1) throw new Exception("EntriesPerTimeFilter must be at least 1");
			if (MinimumPercentageChange <= 0) throw new Exception("MinimumPercentageChange must be larger than zero.");
			if (MaximumPercentageChange <= 0) throw new Exception("MaximumPercentageChange must be larger than zero.");
			if (MinimumAboveAveragePctVolume < 0 && AbsoluteAboveAverageVolume < 0) throw new Exception("MinimumAboveAveragePctVolume or AbsoluteAboveAverageVolume must be specified.");
			if (MaximumEquityPerPosPercent <= 0 || MaximumEquityPerPosPercent > 1) throw new Exception("MaximumEquityPerPosPercent must be larger than zero and less then one.");
			if (RiskBasedEntryPercent < 0 || RiskBasedEntryPercent > 1) throw new Exception("RiskBasedEntryPercent must be larger than or equal to zero and less than one.");
			if (MaximumVolumeBasedPercent <= 0 || MaximumVolumeBasedPercent > 1) throw new Exception("MaximumVolumeBasedPercent must be larger than zero and less than one.");
			if (VolumeLength < 1) throw new Exception("VolumeLength must be at least 1.");
			if (BarsAboveMarketAverage < 0) throw new Exception("BarsAboveMarketAverage must be larger than or equal to zero.");
			if (MarketAverageLength < 1) throw new Exception("MarketAverageLength must be at least 1.");
			if (RequiredClosePercentile < 0 || RequiredClosePercentile > 1) throw new Exception("RequiredClosePercentile must be larger than or equal to zero and less than one.");
			m_minimumAboveAveragePctVolume = MinimumAboveAveragePctVolume > 0 ? MinimumAboveAveragePctVolume + 1.0 : -1;
			m_minuteTimeFrame = BarsOfData(1); //get the minute time frame for the stock
			m_dailyTimeFrame = BarsOfData(2); //get the daily time frame for the stock
			m_marketTimeFrame = BarsOfData(3); //get the market time frame for the stock, e.g. SPY, QQQ, IWM, etc.
			m_volumeWithSD.Length = VolumeLength;
			m_volumeWithSD.Instrument = m_dailyTimeFrame;
			m_marketMovingAverage.Length = MarketAverageLength;
			m_marketMovingAverage.Instrument = m_marketTimeFrame;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Environment.CalcReason == CalculationReason.OrderFilled || (PositionSide > 0 && m_previousPositionSide == 0)) m_entriesPerTimeFilter++;	//backtester do not send order filled messages so we need to compare position sizes between bars
			m_previousPositionSide = PositionSide;
			if (Environment.CalcReason != CalculationReason.Default || m_minuteTimeFrame.Status != EBarState.Close) return;
			if (!marketBullish()) return;

			//check for enough data and implement strategy rules
			if (PositionSide != 0) return;
			if (m_dailyTimeFrame.CurrentBar < Math.Max(2, VolumeLength)) return;
			if (m_marketTimeFrame.CurrentBar < Math.Max(BarsAboveMarketAverage, MarketAverageLength)) return;
			if (m_minuteTimeFrame.Time[0].TimeOfDay < m_startTime.TimeOfDay || m_minuteTimeFrame.Time[0].TimeOfDay > m_endTime.TimeOfDay)
			{
				m_entriesPerTimeFilter = 0;
				return;
			}
			if (m_entriesPerTimeFilter >= EntriesPerTimeFilter) return; //do not enter if the number of entries per time filter is reached
			if (m_previousDayFilter.Date != m_minuteTimeFrame.Time[0].Date)
			{
				m_entriesPerTimeFilter = 0;
				m_previousDayFilter = m_minuteTimeFrame.Time[0].Date;
			}

			//check if PREVIOUS DAILY bar met the specific percentage change to be considered an episodic pivot
			if (m_dailyTimeFrame.Low[1] == 0) return; //avoid division by zero
			double closeLowDelta = m_dailyTimeFrame.Close[1] - m_dailyTimeFrame.Low[1];
			double percentageChange = closeLowDelta / m_dailyTimeFrame.Low[1];
			if (percentageChange < MinimumPercentageChange || percentageChange > MaximumPercentageChange) return; //ignore stocks that do not meet the percentage change criteria

			//check required volume traded on the PREVIOUS DAILY bar to be considered an episodic pivot
			double requiredMiniumAboveAverageVolume = -1;
			if (m_minimumAboveAveragePctVolume > 0)
				requiredMiniumAboveAverageVolume = m_minimumAboveAveragePctVolume * m_volumeWithSD.Average[1];
			else
				requiredMiniumAboveAverageVolume = AbsoluteAboveAverageVolume + m_volumeWithSD.Average[1];

			if (m_dailyTimeFrame.Volume[1] < requiredMiniumAboveAverageVolume) return; //ignore stocks that did not meet the volume criteria

			//required close percentile on minute time frame
			double range = m_minuteTimeFrame.High[0] - m_minuteTimeFrame.Low[0];
			closeLowDelta = m_minuteTimeFrame.Close[0] - m_minuteTimeFrame.Low[0];
			if (range == 0)
			{
				trace(ETraceLevel.Warning, $"{GetType().ToString()}, {Bars.Info.ASymbolInfo2.SymbolName}, {m_minuteTimeFrame.Time[0]} - Range is zero, cannot calculate close percentile - Close {m_minuteTimeFrame.Close[0]} - Low {m_minuteTimeFrame.Low[0]} - High {m_minuteTimeFrame.High[0]}");
				return;
			}
			double closePercentile = closeLowDelta / range;
			if (closePercentile < RequiredClosePercentile) return; //ignore stocks that do not meet the close percentile criteria

			//check for an opening range break
			if (UseOpeningRangeBreak && m_minuteTimeFrame.High[0] != IntradayHigh) return; //ignore stocks that do not meet the opening range break criteria
			
			//determine the risk based on the entry day low
			double risk = m_minuteTimeFrame.Close[0] - IntradayLow;

			//determine the entry size based on the available equity, risk and volume
			int equityBasedSize = positionSize(m_minuteTimeFrame.Close[0], MaximumEquityPerPosPercent, UseRoundLots);
			int riskBasedSize = riskBasedPositionSize(risk, RiskBasedEntryPercent, UseRoundLots);
			int size = volumeBasedPositionSize(m_dailyTimeFrame, equityBasedSize < riskBasedSize ? equityBasedSize : riskBasedSize, VolumeLength, MaximumVolumeBasedPercent, UseRoundLots);
			if (size == 0)
			{
				trace(ETraceLevel.Warning, $"{GetType().ToString()}, {Bars.Info.ASymbolInfo2.SymbolName}, {m_minuteTimeFrame.Time[0]} - Position size calculated as zero - EquityBasedSize {equityBasedSize} - RiskBasedSize {riskBasedSize} - VolumeBasedSize {size} - UseRoundLots {UseRoundLots}");
				return;
			}

			trace(ETraceLevel.Info, $"{GetType().ToString()}, {Bars.Info.ASymbolInfo2.SymbolName}, {m_minuteTimeFrame.Time[0]} - Entry order at Price {m_minuteTimeFrame.Close[0]} - Size {size} - PctChange {percentageChange} - RequiredMinAvgPctVolume {requiredMiniumAboveAverageVolume} - ClosePercentile {closePercentile}");
			m_le.Send(m_minuteTimeFrame.Close[0], size);
		}

		//methods
		/// <summary>
		/// Require a specific number of bars of the market to be above the moving average to determine whether to enter a position or not.
		/// </summary>
		private bool marketBullish()
		{
			for (int i = 0; i < BarsAboveMarketAverage; i++)
				if (m_marketTimeFrame.Close[i] < m_marketMovingAverage[i]) return false; 
			return true;
		}
	}
}