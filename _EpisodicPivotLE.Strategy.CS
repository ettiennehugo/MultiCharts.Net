using System;

namespace PowerLanguage.Strategy
{
	/// Episodic Pivot entry for instruments that have gapped up on earnings or some other catalyst.
	/// Data Feeds:
	///		1 Something like the 5-minute chart for the entry
	///		2 Daily chart for determining the moving averages used
	///	Entry Rules
	///	• Larger cap stocks(>10B) tend to move sideways to up, so you might need different trade management for them.
	///		○ 84% of large cap stocks tend to move sideways to upward for at least 5 days - this strategy just works on the inputs, any partitioning on fundamental factors need to be made in the portfolio manager
	///		○ Maybe user a slightly less percentage since these stocks do not necessarily move as quickly as smaller cap stocks.
	///
	///		1. Scan - stocks with upcoming earnings and stocks that are up pre-market (around 8am) 8%
	///			a.Check 8 to 10% gap up (defined as previous day's close vs current day open).
	///		2. Big volume on open and bar closes in the upper 10% to 25% - this is if you enter around the opening bars
	///		3. If earnings gap preferable high double and triple digit returns
	///		4. Best if stock comes out of base without being overextended to the up side
	///		5. Initial risk
	///			a.If you're trading the opening range then it would be the day low + 0.01 buffer price
	///			b.If you're entering on the premarket initial risk is the entry bar low.
	///		Notes
	///		○ IMPORTANT: Make sure that you have enough lookback bars to allow entries to be made, that includes any exit strategies paired with this entry strategy otherwise MC will not evaluate this strategy
	///		             for entry.
	public class _EpisodicPivotLE : _CustomSignal
	{
		//constants
		public const string DEFAULT_START_TIME = "09:30"; //stock market regular open time
		public const string DEFAULT_END_TIME = "16:00";   //stock market regular close time
		public int DEFAULT_ENTRIES_PER_TIME_FILTER = 1; //number of entries per time filter - this is used to determine the number of entries per time filter
		public const double DEFAULT_STOP_LOSS_BUFFER = 0.01; //stop-loss buffer - this is used to determine the stop-loss based on the entry date
		public const double DEFAULT_MINIMUM_PERCENTAGE_CHANGE = 0.1;      //ignore stocks that fall below this minimum percentage change
		public const double DEFAULT_MAXIMUM_PERCENTAGE_CHANGE = 0.3;			//ignore stocks that have massive gap ups as to avoid parabolic moves that will just reverse
		public const double DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT = 0.3; //per default don't enter positions larger than 30% of available equity
		public const double DEFAULT_RISK_BASED_ENTRY_PERCENT = 0.01;    //use 1% of equity for risk based entry
		public const double DEFAULT_REQUIRED_CLOSE_PERCENTILE = 0.5; //required close percentile - this is used to determine the entry size based on the average trading volume 
		public const int DEFAULT_VOLUME_LENGTH = 20;        //length of the volume for the entry size
		public const double DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT = 0.2; //do not exceed 20% of the average trading volume for the entry size

		//types


		//properties
		[Input] public string StartTime { get; set; } //start time for the entry
		[Input] public string EndTime { get; set; } //end time for the entry
		[Input] public int EntriesPerTimeFilter { get; set; } //number of entries per time filter - this is used to determine the number of entries per time filter
		[Input] public double MinimumPercentageChange { get; set; } //gap up/down must be at least this minimum - this must always be a positive value, algoritm will flip it around if required
		[Input] public double MaximumPercentageChange { get; set; } //gap up/down clipped to this maximum to avoid pump-and-dump stocks - this must always be a positive value, algorithm will flip it around if required
		[Input] public double MaximumEquityPerPosPercent { get; set; } //maximum equity per position as a percentage of the available equity - this is used to determine the maximum position size for the entry
		[Input] public double RiskBasedEntryPercent { get; set; } //risk based entry percentage - this is used to base the position size as a percentage of the euity based on the risk taken, set to zero to disable risk based entries
		[Input] public double MaximumVolumeBasedPercent { get; set; } //maximum volume based percentage - this is used to determine the entry size based on the average trading volume\
		[Input] public double RequiredClosePercentile { get; set; } //required close percentile - this is used to determine the entry size based on the average trading volume
		[Input] public bool UseRoundLots { get; set; } //use round lots for the entry size - this is used to determine the entry size based on the average trading volume
		[Input] public double StopLossBuffer { get; set; } //stop-loss buffer - this is used to determine the stop-loss based on the entry date

		//attribute
		private IOrderPriced m_le;
		private DateTime m_startTime;
		private DateTime m_endTime;
		private DateTime m_previousDayFilter;
		private IInstrument m_minuteTimeFrame;
		private IInstrument m_dailyTimeFrame;
		private int m_entriesPerTimeFilter;
		private int m_previousPositionSide;

		//interface methods
		public _EpisodicPivotLE(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			StartTime = DEFAULT_START_TIME;
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			EndTime = DEFAULT_END_TIME;
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			EntriesPerTimeFilter = DEFAULT_ENTRIES_PER_TIME_FILTER;
			m_previousDayFilter = DateTime.MinValue;
			MinimumPercentageChange = DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
			MaximumPercentageChange = DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;
			MaximumEquityPerPosPercent = DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT;
			RiskBasedEntryPercent = DEFAULT_RISK_BASED_ENTRY_PERCENT;
			MaximumVolumeBasedPercent = DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT;
			RequiredClosePercentile = DEFAULT_REQUIRED_CLOSE_PERCENTILE;
			UseRoundLots = false;
			StopLossBuffer = DEFAULT_STOP_LOSS_BUFFER;
			m_le = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "Le", EOrderAction.Buy));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			m_entriesPerTimeFilter = 0;
			m_previousPositionSide = 0;
			if (MinimumPercentageChange <= 0) throw new Exception("MinimumPercentageChange must be larger than zero.");
			if (MaximumPercentageChange <= 0) throw new Exception("MaximumPercentageChange must be larger than zero.");
			if (MaximumEquityPerPosPercent <= 0 || MaximumEquityPerPosPercent > 1) throw new Exception("MaximumEquityPerPosPercent must be larger than zero and less then one.");
			if (RiskBasedEntryPercent < 0 || RiskBasedEntryPercent > 1) throw new Exception("RiskBasedEntryPercent must be larger than or equal to zero and less than one.");
			if (MaximumVolumeBasedPercent <= 0 || MaximumVolumeBasedPercent > 1) throw new Exception("MaximumVolumeBasedPercent must be larger than zero and less than one.");
			if (RequiredClosePercentile < 0 || RequiredClosePercentile > 1) throw new Exception("RequiredClosePercentile must be larger than or equal to zero and less than one.");
			if (StopLossBuffer < 0) throw new Exception("StopLossBuffer must be larger than or equal to zero.");
			m_minuteTimeFrame = BarsOfData(1); //get the minute time frame for the stock
			m_dailyTimeFrame = BarsOfData(2); //get the daily time frame for the stock
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Environment.CalcReason == CalculationReason.OrderFilled || (PositionSide != 0 && m_previousPositionSide == 0)) m_entriesPerTimeFilter++;  //backtester do not send order filled messages to we need to compare position sizes between bars
			m_previousPositionSide = PositionSide;
			if (Environment.CalcReason != CalculationReason.Default || m_minuteTimeFrame.Status != EBarState.Close) return;

			//check for enough data and implement time filter
			if (m_minuteTimeFrame.Time[0].TimeOfDay < m_startTime.TimeOfDay || m_minuteTimeFrame.Time[0].TimeOfDay > m_endTime.TimeOfDay)
			{
				m_entriesPerTimeFilter = 0;
				return;
			}
			if (m_previousDayFilter.Date != m_minuteTimeFrame.Time[0].Date) m_entriesPerTimeFilter = 0;
			m_previousDayFilter = m_minuteTimeFrame.Time[0].Date;
			if (PositionSide != 0 || m_dailyTimeFrame.CurrentBar < 2) return; //do not enter if there is already a position or not enough data
			if (m_entriesPerTimeFilter >= EntriesPerTimeFilter)
            {
				trace(ETraceLevel.Verbose, $"Entries per time filter reached, not taking entry.");                
                return; //do not enter if the number of entries per time filter is reached
            }

			//check if the stock has gapped up
			if (m_dailyTimeFrame.Close[0] == 0) return; //avoid division by zero
			double gap = (IntradayOpen - m_dailyTimeFrame.Close[0]) / m_dailyTimeFrame.Close[0]; //NOTE: We're using the close instead of the high as the close is considered the previous days sentiment (not the high).
			if (gap < MinimumPercentageChange || gap > MaximumPercentageChange) return; //ignore stocks that do not meet the gap up criteria

            //current bar low must be above the open, we don't want to enter on a down day as the stock is moving lower
            if (m_minuteTimeFrame.Close[0] < IntradayOpen)
            {
				trace(ETraceLevel.Verbose, $"Current bar close ({m_minuteTimeFrame.Close[0]}) is below intraday open ({IntradayOpen}), don't take entry on a down day.");
				return;
            }
                        
			//required close percentile
			double range = m_minuteTimeFrame.High[0] - m_minuteTimeFrame.Low[0];
			if (range == 0)
			{
				trace(ETraceLevel.Verbose, "Unable to compute close percentile due to zero bar range.");
				return; //avoid division by zero
			}

			double closeLowDelta = m_minuteTimeFrame.Close[0] - m_minuteTimeFrame.Low[0];
			double closePercentile = closeLowDelta / range; 
			if (closePercentile < RequiredClosePercentile)
			{
				trace(ETraceLevel.Info, $"Entry close percentile {closePercentile} is less than required percentile {RequiredClosePercentile}, skipping entry.");
				return; //ignore stocks that do not meet the close percentile criteria
			}

			//determine the risk based on the entry day low
			double risk = m_minuteTimeFrame.Close[0] - IntradayLow - StopLossBuffer;    //NOTE: Here we use the LOW and not the open as this might be a reentry.
			
			//determine the entry size based on the available equity, risk and volume
			int equityBasedSize = positionSize(m_minuteTimeFrame.Close[0], MaximumEquityPerPosPercent, UseRoundLots);
			int riskBasedSize = riskBasedPositionSize(risk, RiskBasedEntryPercent, UseRoundLots);
			int volumeBasedSize = volumeBasedPositionSize(m_dailyTimeFrame, DEFAULT_VOLUME_LENGTH, MaximumVolumeBasedPercent, UseRoundLots);
			int size = equityBasedSize < riskBasedSize ? equityBasedSize : riskBasedSize;
			size = size < volumeBasedSize ? size : volumeBasedSize;
			if (size == 0)
			{
				trace(ETraceLevel.Info,$"Entry size is zero so no entry would be take, EquityBasedSize({equityBasedSize}), RiskBasedSize({riskBasedSize}), VolumeBasedSize({volumeBasedSize})");
				return; //do not enter if the size is zero
			}

			m_le.Send(m_minuteTimeFrame.Close[0], size);
		}

		//methods


	}
}