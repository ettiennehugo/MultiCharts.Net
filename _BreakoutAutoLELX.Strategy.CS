using System;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Enter a long position and manage it around a range expansion breakout (e.g. %change of today > %change of last X days).
	/// Strategy exit is controlled to look for a specific 
	/// 
	/// Timeframes:
	///		1 - Minute timeframe - you'd like to keep this to short timeframes as breakouts occur quickly and early entry is imperative.
	///		2 - Daily - Daily timeframe used to measure %change of previous days to decide entry for current day.
	///	
	/// Entry:
	///		Measures the average percentage change over the last X days to determine average % change.
	///		Current day % change is larger than last X-days average % change with an additional factor to control detection sensitivity (defaults to 1.0).
	///		Uses an entry time filter to control when entries can be taken.
	///		
	///	Risk Management:
	///		Can enter an absolute size to trade really small or large positions irrespective of account/risk size, overrides all other risk controls so use with caution.
	///		Maximum percentage equity to allocate to position taken.
	///		Maximum risk based percentage to allocate position taken, works with max percentage equity to use and stop-loss placement.
	///		Stop loss placement determines where to place the stop-loss, ideally for a breakout the current day low is the ideal stop-loss but other options are given. An
	///		additional buffer price is added to the predetermined stop-loss to offset the stop-loss by (could be both positive or negative). 
	///		
	///	Exit:
	///		First and second exits are supported with a max profit exit for whole postiion.
	///		Break even stop can be sent after a specific profit target is reached.
	///		Full exit if the position does not move beyond the entry day high/low after a specific number of days.
	///		Complete flattening of position if not profitable after a specific number of days.
	/// 
	/// Notes:
	///		Entries and exits use market orders so while risk is computed there might be slippage that play into position sizing leading to excess risk taken.
	///		Note does not cater for pre-/post-market entries/exits would need to add limit orders for those.
	///		
	/// </summary>
	public class _BreakoutAutoLELX : _BreakoutAutoBase
	{
		//types


		//constants
		public const string DEFAULT_START_TIME = "09:30";
		public const string DEFAULT_END_TIME = "10:00";
		public const double DEFAULT_AVG_VOLUME_FILTER = 150000.0;

		//properties
		[Input] public string StartTime { get; set; }           // Start time for the entries to be taken, default to 09:30 (assuming stock exchange local time)
		[Input] public string EndTime { get; set; }             // End time for entries to be taken, defaul to 10:00 (assuming stock exchange local time)
		[Input] public double AvgVolumeFilter { get; set; }			// Required average volume over the last three days, this is to make sure we stay out of low float stocks that can be all over the place on low volume. 

		//attribute
		private DateTime m_startTime;
		private DateTime m_endTime;

		//interface methods
		public _BreakoutAutoLELX(object _ctx) : base(_ctx, true) { }

		protected override void Create()
		{
			base.Create();
			StartTime = DEFAULT_START_TIME;
			EndTime = DEFAULT_END_TIME;
			AvgVolumeFilter = DEFAULT_AVG_VOLUME_FILTER;
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (!DateTime.TryParse(StartTime, out m_startTime)) throw new ArgumentException($"Failed to parse start time {StartTime}.");
			if (!DateTime.TryParse(EndTime, out m_endTime)) throw new ArgumentException($"Failed to parse end time {EndTime}.");
			if (AvgVolumeFilter < 0.0) throw new ArgumentException("Average volume filter value must be greater or equal to zero.");
			if (m_startTime.TimeOfDay > m_endTime.TimeOfDay) throw new ArgumentException($"Start time {StartTime} must be less than end time {EndTime}.");
		}


		//methods
		protected override void sendEntry()
		{
			if (MarketMode != EMarketMode.Unknown && MarketMode != EMarketMode.Bullish)
			{
				trace(ETraceLevel.Debug, $"sendEntry - market mode {MarketMode} is not bullish");
				return;
			}

			//pre - existing momentum check
			double avgVolume = m_dailyTimeframe.Volume.Average(3, 1);
			double low21 = m_dailyTimeframe.Close.Lowest(21, 1);
			double low63 = m_dailyTimeframe.Close.Lowest(63, 1);
			double low126 = m_dailyTimeframe.Close.Lowest(126, 1);
			if (avgVolume > AvgVolumeFilter &&
			    ((m_dailyTimeframe.CurrentBar > 21 && low21 != 0.0 && m_dailyTimeframe.Close[1] / low21 > 1.3) ||
					 (m_dailyTimeframe.CurrentBar > 63 && low63 != 0.0 && m_dailyTimeframe.Close[1] / low63 > 1.3) ||
					 (m_dailyTimeframe.CurrentBar > 126 && low126 != 0.0 && m_dailyTimeframe.Close[1] / low126 > 1.3)))
			{
				if (m_tradingTimeframe.Time[0].TimeOfDay >= m_startTime.TimeOfDay &&
						m_tradingTimeframe.Time[0].TimeOfDay <= m_endTime.TimeOfDay)
				{
					double range = IntradayClose - IntradayOpen;  //bar must be bullish to yield a positive range expansion
					double rangePct = (IntradayOpen != 0.0) ? range / IntradayOpen : 0.0;

					if (m_averageRange.Value != double.MaxValue) // trace test if we have a valid average range
						trace(ETraceLevel.Debug, $"sendEntry - testing for bullish range - Range({range}) RangePct({rangePct}) AvgRange({m_averageRange.Value}) DailyRange({IntradayClose - IntradayOpen}) IC({IntradayClose}) IO({IntradayOpen}) DC({m_dailyTimeframe.Close[DailyOffset]}) DO({m_dailyTimeframe.Open[DailyOffset]})");

					if (range > 0.0 && m_averageRange.Value < rangePct)
					{
						trace(ETraceLevel.Info, $"sendEntry - bullish range expansion detected - Range({range}) RangePct({rangePct}) AvgRange({m_averageRange.Value})");
						m_entry.Send(positionSize());
					}
				}
			}
		}
	}
}