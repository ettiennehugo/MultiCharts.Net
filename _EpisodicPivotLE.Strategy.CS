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
	///			a.Check 8 to 10% gap up
	///		2. Big volume on open and bar closes in the upper 10% to 25% - this is if you enter around the opening bars
	///		3. If earnings gap preferable high double and triple digit returns
	///		4. Best if stock comes out of base without being overextended to the up side
	///		5. Initial risk
	///			a.If you're trading the opening range then it would be the day low + 0.01 buffer price
	///			b.If you're entering on the premarket initial risk is the entry bar low.
	public class _EpisodicPivotLE : _CustomSignal
	{
		//constants
		public const string DEFAULT_START_TIME = "09:30"; //stock market regular open time
		public const string DEFAULT_END_TIME = "16:00";   //stock market regular close time
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
		[Input] public double MinimumPercentageChange { get; set; } //gap up/down must be at least this minimum - this must always be a positive value, algoritm will flip it around if required
		[Input] public double MaximumPercentageChange { get; set; } //gap up/down clipped to this maximum to avoid pump-and-dump stocks - this must always be a positive value, algorithm will flip it around if required
		[Input] public double MaximumEquityPerPosPercent { get; set; } //maximum equity per position as a percentage of the available equity - this is used to determine the maximum position size for the entry
		[Input] public double RiskBasedEntryPercent { get; set; } //risk based entry percentage - this is used to base the position size as a percentage of the euity based on the risk taken, set to zero to disable risk based entries
		[Input] public double MaximumVolumeBasedPercent { get; set; } //maximum volume based percentage - this is used to determine the entry size based on the average trading volume\
		[Input] public double RequiredClosePercentile { get; set; } //required close percentile - this is used to determine the entry size based on the average trading volume
		[Input] public bool UseRoundLots { get; set; } //use round lots for the entry size - this is used to determine the entry size based on the average trading volume

		//attribute
		private IOrderPriced m_le;
		private DateTime m_startTime;
		private DateTime m_endTime;
		private IInstrument m_minuteTimeFrame;
		private IInstrument m_dailyTimeFrame;

		//interface methods
		public _EpisodicPivotLE(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			StartTime = DEFAULT_START_TIME;
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			EndTime = DEFAULT_END_TIME;
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			MinimumPercentageChange = DEFAULT_MINIMUM_PERCENTAGE_CHANGE;
			MaximumPercentageChange = DEFAULT_MAXIMUM_PERCENTAGE_CHANGE;
			MaximumEquityPerPosPercent = DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT;
			RiskBasedEntryPercent = DEFAULT_RISK_BASED_ENTRY_PERCENT;
			MaximumVolumeBasedPercent = DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT;
			RequiredClosePercentile = DEFAULT_REQUIRED_CLOSE_PERCENTILE;
			UseRoundLots = false;
			m_le = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "Le", EOrderAction.Buy));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			if (MinimumPercentageChange <= 0) throw new Exception("MinimumPercentageChange must be larger than zero.");
			if (MaximumPercentageChange <= 0) throw new Exception("MaximumPercentageChange must be larger than zero.");
			if (MaximumEquityPerPosPercent <= 0 || MaximumEquityPerPosPercent > 1) throw new Exception("MaximumEquityPerPosPercent must be larger than zero and less then one.");
			if (RiskBasedEntryPercent < 0 || RiskBasedEntryPercent > 1) throw new Exception("RiskBasedEntryPercent must be larger than or equal to zero and less than one.");
			if (MaximumVolumeBasedPercent <= 0 || MaximumVolumeBasedPercent > 1) throw new Exception("MaximumVolumeBasedPercent must be larger than zero and less than one.");
			if (RequiredClosePercentile < 0 || RequiredClosePercentile > 1) throw new Exception("RequiredClosePercentile must be larger than or equal to zero and less than one.");
			m_minuteTimeFrame = BarsOfData(1); //get the minute time frame for the stock
			m_dailyTimeFrame = BarsOfData(2); //get the daily time frame for the stock
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Environment.CalcReason != CalculationReason.Default || m_minuteTimeFrame.Status != EBarState.Close) return;

			//Debug - if (m_minuteTimeFrame.Time[0].Month != 2 || m_minuteTimeFrame.Time[0].Year != 2025 || m_minuteTimeFrame.Time[0].Day != 14) return;

			//check for enough data and implement time filter
			if (PositionSide != 0 || m_dailyTimeFrame.CurrentBar < 2) return; //do not enter if there is already a position or not enough data
			if (m_minuteTimeFrame.Time[0].TimeOfDay < m_startTime.TimeOfDay || m_minuteTimeFrame.Time[0].TimeOfDay > m_endTime.TimeOfDay) return;

			//check if the stock has gapped up
			if (m_dailyTimeFrame.High[0] == 0) return; //avoid division by zero
			double gap = (IntradayLow - m_dailyTimeFrame.High[0]) / m_dailyTimeFrame.High[0];
			if (gap < MinimumPercentageChange || gap > MaximumPercentageChange) return; //ignore stocks that do not meet the gap up criteria

			//required close percentile
			double range = m_minuteTimeFrame.High[0] - m_minuteTimeFrame.Low[0];
			if (range == 0) return; //avoid division by zero
			double closeLowDelta = m_minuteTimeFrame.Close[0] - m_minuteTimeFrame.Low[0];
			double closePercentile = closeLowDelta / range;
			if (closePercentile < RequiredClosePercentile) return; //ignore stocks that do not meet the close percentile criteria

			//determine the risk based on the entry day low
			double risk = m_minuteTimeFrame.Close[0] - IntradayLow;
			
			//determine the entry size based on the available equity, risk and volume
			int equityBasedSize = positionSize(m_minuteTimeFrame.Close[0], MaximumEquityPerPosPercent, UseRoundLots);
			int riskBasedSize = riskBasedPositionSize(risk, RiskBasedEntryPercent, UseRoundLots);
			int size = volumeBasedPositionSize(m_dailyTimeFrame, equityBasedSize < riskBasedSize ? equityBasedSize : riskBasedSize, DEFAULT_VOLUME_LENGTH, MaximumVolumeBasedPercent, UseRoundLots);
			if (size == 0) return; //do not enter if the size is zero

			trace(ETraceLevel.Info, $"{GetType().ToString()}, {Bars.Info.ASymbolInfo2.SymbolName}, {Bars.Time[0]} - Entry order at Price({m_minuteTimeFrame.Close[0]}), Size({size}), Gap({gap}), ClosePercentile({closePercentile})");
			m_le.Send(m_minuteTimeFrame.Close[0], size);
		}

		//methods


	}
}