using System;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// High volume episodic pivot long entry, use this with stocks that are already in a strong uptrend it will wait for a high volume bullish DAILY bar to enter a long position. Pradeep Bond
	/// calls this a 9mil+EP since he uses 9 million as the above average volume to determine whether he'll enter a long position or not.
	/// Use the EpisodicPivotLX strategy to exit the long position.
	/// Data Feeds:
	///		1 Something like the 5-minute chart for the entry
	///		2 Daily chart for determining episodic pivot and the moving averages used
	///	Entry Rules:
	///	• Larger cap stocks(>10B) tend to move sideways to up and can have a delayed reaction, so you might need different trade management for them.
	///		○ 84% of large cap stocks tend to move sideways to upward for at least 5 days - this strategy just works on the inputs, any partitioning on fundamental factors need to be made in the portfolio manager
	///		○ Maybe user a slightly less percentage since these stocks do not necessarily move as quickly as smaller cap stocks.
	///
	///		1. Scan - stocks that are up 30%+ over the last 1 to 3 months.
	///		2. Big volume move on the daily timeframe at least a specific minimum move on very high volume - this is considered an episodic pivot move with volume catalyst, this could be an earnings call or some other catalyst.
	///			2.1 Stocks under 10 Billion (nano, micro, small, meduim cap) market cap use an 8% move as the minimum move.
	///			2.2 Stocks above 10 billion (large and mega) market cap use a 3-5% move as the minimum move.
	///		3. Best if stock comes out of base without being overextended to the up side
	///		4. Wait for solid volume bullish bar on the minute time frame that is above the previous day high, this is the entry bar.
	///			4.1 IMPORTANT - ensure the market is bullish (e.g. above the 50EMA) as well since an episodic pivot would most likely fade quicker under bearish market conditions.
	///		5. Initial risk is the entry day low.
	/// Notes:
	///	• Can use the standard EpisodicPivotLX with this strategy. 
	/// </summary>
	public class _EpisodicPivotHighVolumeLE : _CustomSignal {
		//constants
		public const string DEFAULT_START_TIME = "09:30"; //stock market regular open time
		public const string DEFAULT_END_TIME = "16:00";   //stock market regular close time
		public int DEFAULT_ENTRIES_PER_TIME_FILTER = 1; //number of entries per time filter - this is used to determine the number of entries per time filter
		public const double DEFAULT_STOP_LOSS_BUFFER = 0.01; //stop-loss buffer - this is used to determine the stop-loss based on the entry date
		public const double DEFAULT_MINIMUM_PERCENTAGE_CHANGE = 0.1;      //ignore stocks that fall below this minimum percentage change
		public const double DEFAULT_MAXIMUM_PERCENTAGE_CHANGE = 0.3;      //ignore stocks that have massive overextended moves as to avoid parabolic moves that will just reverse
		public const double DEFAULT_MINIMUM_DAILY_VOLUME_PERCENTILE = -1; //[disable == -1] minimum above average DAILY volume percentage for the entry - this is used for volume confirmation, e.g. 0.1 means 10% of the daily volume needs to be traded on the entry bar \
		public const int DEFAULT_ABSOLUTE_ABOVE_AVERAGE_VOLUME = 9000000; //[disable == -1] absolute above average volume for the entry - this is used for volume confirmation of the number of shares traded above the volume average
		public const double DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT = 0.3; //per default don't enter positions larger than 30% of available equity
		public const double DEFAULT_RISK_BASED_ENTRY_PERCENT = 0.01;    //use 1% of equity for risk based entry
		public const double DEFAULT_REQUIRED_CLOSE_PERCENTILE = 0.75;
		public const double DEFAULT_REQUIRED_VOLUME_PERCENTILE = 0.8;
		public const int DEFAULT_VOLUME_LENGTH = 20;        //length of the volume for the entry size
		public const double DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT = 0.2; //do not exceed 20% of the average trading volume for the entry size

		//types


		//properties
		[Input] public string StartTime { get; set; } //start time for the entry
		[Input] public string EndTime { get; set; } //end time for the entry
		[Input] public int EntriesPerTimeFilter { get; set; } //number of entries per time filter - this is used to determine the number of entries per time filter
		[Input] public double MinimumPercentageChange { get; set; } //gap up/down must be at least this minimum - this must always be a positive value, algoritm will flip it around if required
		[Input] public double MaximumPercentageChange { get; set; } //gap up/down clipped to this maximum to avoid overextended stocks - this must always be a positive value, algorithm will flip it around if required
		[Input] public double MinimumDailyVolumePercentile { get; set; } //[alternate] minimum volume percentile before considering an entry - this is used for volume confirmation
		[Input] public int AbsoluteAboveAverageVolume { get; set; } //[alternate] absolute above average volume for the entry - this is used for volume confirmation of the number of shares traded above the volume average
		[Input] public double MaximumEquityPerPosPercent { get; set; } //maximum equity per position as a percentage of the available equity - this is used to determine the maximum position size for the entry
		[Input] public double RiskBasedEntryPercent { get; set; } //risk based entry percentage - this is used to base the position size as a percentage of the euity based on the risk taken, set to zero to disable risk based entries
		[Input] public double MaximumVolumeBasedPercent { get; set; } //maximum volume based percentage - this is used to determine the entry size based on the average trading volume\
		[Input] public int VolumeLength { get; set; } //length of the volume for the entry size - this is used to determine the entry size based on the average trading volume
		[Input] public double RequiredClosePercentile { get; set; } //required close percentile on the minute timeframe for entry - bar needs to close above this percentile for an entry to be considered, set to zero (0) to disable close percentile filter
		[Input] public double RequiredVolumePercentile { get; set; } //required volume percentile on the minute timeframe for entry - volume on bar need to be above this percentile for an entry to be considered, set to zero (0) to disable volume percentile filter
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
		private PowerLanguage.Function._VolumePercentile m_dailyVolumePercentile;
		private PowerLanguage.Function._VolumePercentile m_minuteVolumePercentile;

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
			MinimumDailyVolumePercentile = DEFAULT_MINIMUM_DAILY_VOLUME_PERCENTILE;
			MaximumEquityPerPosPercent = DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT;
			AbsoluteAboveAverageVolume = DEFAULT_ABSOLUTE_ABOVE_AVERAGE_VOLUME;
			RiskBasedEntryPercent = DEFAULT_RISK_BASED_ENTRY_PERCENT;
			MaximumVolumeBasedPercent = DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT;
			VolumeLength = DEFAULT_VOLUME_LENGTH;
			RequiredClosePercentile = DEFAULT_REQUIRED_CLOSE_PERCENTILE;
			RequiredVolumePercentile = DEFAULT_REQUIRED_VOLUME_PERCENTILE;
			UseRoundLots = false;
			StopLossBuffer = DEFAULT_STOP_LOSS_BUFFER;
			m_le = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "Le", EOrderAction.Buy));
			m_dailyVolumePercentile = new PowerLanguage.Function._VolumePercentile(this);
			m_minuteVolumePercentile = new PowerLanguage.Function._VolumePercentile(this);
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
			if (MinimumDailyVolumePercentile < 0 && AbsoluteAboveAverageVolume < 0) throw new Exception("MinimumVolumePercentile or AbsoluteAboveAverageVolume must be specified.");
			if (MaximumEquityPerPosPercent <= 0 || MaximumEquityPerPosPercent > 1) throw new Exception("MaximumEquityPerPosPercent must be larger than zero and less then one.");
			if (RiskBasedEntryPercent < 0 || RiskBasedEntryPercent > 1) throw new Exception("RiskBasedEntryPercent must be larger than or equal to zero and less than one.");
			if (MaximumVolumeBasedPercent <= 0 || MaximumVolumeBasedPercent > 1) throw new Exception("MaximumVolumeBasedPercent must be larger than zero and less than one.");
			if (VolumeLength < 1) throw new Exception("VolumeLength must be at least 1.");
			if (RequiredClosePercentile < 0 || RequiredClosePercentile > 1) throw new Exception("RequiredClosePercentile must be larger than or equal to zero and less than one.");
			if (RequiredVolumePercentile < 0 || RequiredVolumePercentile > 1) throw new Exception("RequiredVolumePercentile must be larger than or equal to zero and less than one.");
			if (StopLossBuffer < 0) throw new Exception("StopLossBuffer must be larger than or equal to zero.");
			m_minuteTimeFrame = BarsOfData(1); //get the minute time frame for the stock
			m_dailyTimeFrame = BarsOfData(2); //get the daily time frame for the stock
			m_dailyVolumePercentile.Instrument = m_dailyTimeFrame;
			m_minuteVolumePercentile.Instrument = m_minuteTimeFrame;
		}

		static DateTime testDate = DateTime.Parse("11/29/2023");

		protected override void CalcBar()
		{
			base.CalcBar();

			if (Bars.Time[0].Date == testDate.Date)
				Output.WriteLine("Test");

			if (Environment.CalcReason == CalculationReason.OrderFilled || (PositionSide > 0 && m_previousPositionSide == 0)) m_entriesPerTimeFilter++;	//backtester do not send order filled messages so we need to compare position sizes between bars
			m_previousPositionSide = PositionSide;
			if (Environment.CalcReason != CalculationReason.Default || m_minuteTimeFrame.Status != EBarState.Close) return;

			//check for enough data and implement strategy rules
			if (m_minuteTimeFrame.Time[0].TimeOfDay < m_startTime.TimeOfDay || m_minuteTimeFrame.Time[0].TimeOfDay > m_endTime.TimeOfDay)
			{
				m_entriesPerTimeFilter = 0;
				return;
			}
			if (m_previousDayFilter.Date != m_minuteTimeFrame.Time[0].Date)
			{
				m_entriesPerTimeFilter = 0;
				m_previousDayFilter = m_minuteTimeFrame.Time[0].Date;
			}
			if (PositionSide != 0) return;
			if (m_dailyTimeFrame.CurrentBar < Math.Max(2, VolumeLength)) return;
			if (m_entriesPerTimeFilter >= EntriesPerTimeFilter) return; //do not enter if the number of entries per time filter is reached
			if (m_minuteTimeFrame.Close[0] < m_dailyTimeFrame.High[0]) return;	//only enter if the stock still has momentum from the previous day otherwise you get entries on sideways to down moving stocks

			//check if PREVIOUS DAILY bar met the specific percentage change to be considered an episodic pivot
			if (m_dailyTimeFrame.Low[1] == 0) return; //avoid division by zero
			double percentageChange = (m_dailyTimeFrame.Close[0] / m_dailyTimeFrame.Low[0]) - 1.0;  //on daily index 0 is the previous day's values, we also need to ensure the previous day was a bullish day
			if (m_dailyTimeFrame.Open[0] > m_dailyTimeFrame.Close[0]) return;	//we only take bullish entries to ensure there are preexisting momentum
			if (percentageChange < MinimumPercentageChange || percentageChange > MaximumPercentageChange) return; //ignore stocks that do not meet the percentage change criteria

			//check required volume traded on the PREVIOUS DAILY bar to be considered an episodic pivot
			if (MinimumDailyVolumePercentile > 0 && m_dailyVolumePercentile[0] < MinimumDailyVolumePercentile) return;
			if (AbsoluteAboveAverageVolume > 0 && m_dailyTimeFrame.Volume[0] < (m_dailyTimeFrame.Volume.Average(VolumeLength) + AbsoluteAboveAverageVolume)) return;

			//required volume percentile on minute time frame
			if (RequiredVolumePercentile > 0 && m_minuteVolumePercentile[0] < RequiredVolumePercentile) return; //ignore stocks that do not meet the volume percentile criteria

			//required close percentile on minute time frame
			double range = m_minuteTimeFrame.High[0] - m_minuteTimeFrame.Low[0];
			double closeLowDelta = m_minuteTimeFrame.Close[0] - m_minuteTimeFrame.Low[0];
			if (range == 0)
			{
				trace(ETraceLevel.Warning, $"{m_minuteTimeFrame.Time[0]} - Range is zero, cannot calculate close percentile - Close {m_minuteTimeFrame.Close[0]} - Low {m_minuteTimeFrame.Low[0]} - High {m_minuteTimeFrame.High[0]}");
				return;
			}
			double closePercentile = closeLowDelta / range;
			if (closePercentile < RequiredClosePercentile) return; //ignore stocks that do not meet the close percentile criteria

			//determine the risk based on the entry day low
			double risk = m_minuteTimeFrame.Close[0] - IntradayLow - StopLossBuffer;

			//determine the entry size based on the available equity, risk and volume
			int equityBasedSize = positionSize(m_minuteTimeFrame.Close[0], MaximumEquityPerPosPercent, UseRoundLots);
			int riskBasedSize = riskBasedPositionSize(risk, RiskBasedEntryPercent, UseRoundLots);
			int volumeBasedSize = volumeBasedPositionSize(m_dailyTimeFrame, VolumeLength, MaximumVolumeBasedPercent, UseRoundLots);
			int size = equityBasedSize < riskBasedSize ? equityBasedSize : riskBasedSize;
			size = size < volumeBasedSize ? size : volumeBasedSize;

			if (size == 0)
			{
				trace(ETraceLevel.Warning, $"{m_minuteTimeFrame.Time[0]} - Position size calculated as zero - EquityBasedSize {equityBasedSize} - RiskBasedSize {riskBasedSize} - VolumeBasedSize {size} - UseRoundLots {UseRoundLots}");
				return;
			}

			trace(ETraceLevel.Info, $"{m_minuteTimeFrame.Time[0]} - Entry order at Price {m_minuteTimeFrame.Close[0]} - Size {size} - PctChange {percentageChange} - ClosePercentile {closePercentile}");
			m_le.Send(m_minuteTimeFrame.Close[0], size);
		}
	}
}