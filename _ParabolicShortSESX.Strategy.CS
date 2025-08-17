using ATCenterProxy.interop;
using PowerLanguage.Function;
using PriceAction;
using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Parabolic Short SE - this strategy is used to enter a short position when a lower high is formed after a parabolic move up.
	/// The strategy uses the price action analysis to determine the entry point and the exit point.
	/// Timeframes used:
	///		Primary: 1/5 minute chart for the entry
	///		Secondary: 15-minute/30-minutes/hourly - used for price action analysis
	///		Dialy timeframe
	///	Entry:
	///	
	/// 
	/// Exit:
	/// 
	/// </summary>
	public class _ParabolicShortSESX : _CustomSignal
	{
		//constants
		public const string DEFAULT_START_TIME = "09:30"; //stock market regular open time
		public const string DEFAULT_END_TIME = "16:00";   //stock market regular close time
		public int DEFAULT_FRACTAL_LAYERS = 3; //number of fractal layers to use for the price action analysis
		public const double DEFAULT_FACTOR_CLOSE = 0.05; //factor close - user a more finely tuned factor close to determine lower highs, this uses 5% of the ATR as parabolic shorts can have huge bar moves
		public const double DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT = 0.15; //per default don't enter positions larger than 15% of available equity
		public const double DEFAULT_RISK_BASED_ENTRY_PERCENT = 0.0075; //use 0.75% of equity for risk based entry
		public const double DEFAULT_CLOSE_PERCENTILE = 0.5; //required close percentile - this is used to determine the entry size based on the average trading volume 
		public const double DEFAULT_VOLUME_PERCENTILE = 0.5; //required volume percentile - this is used to determine the entry size based on the average trading volume
		public const int DEFAULT_VOLUME_LENGTH = 20; //length of the volume period to use for the volume based entry size
		public const double DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT = 0.2; //maximum volume based percentage - this is used to determine the entry size based on the average trading volume
		public const double DEFAULT_RETRACEMENT_EXIT_PERCENT = 0.5; //exit percentage - this is used to determine the exit percentage based on the entry date
		public const int DEFAULT_EXIT_DAYS_COUNT = 3; //number of days to exit the full position
		public const string DEFAULT_EXIT_TIME = "15:35"; //exit time for the full position

		//types
		public enum EExitDaysCountStrategy
		{
			CalendarDays,
			TradingDays
		};

		//properties
		[Input] public string StartTime { get; set; } //start time for the entry
		[Input] public string EndTime { get; set; } //end time for the entry
		[Input] public double MaximumEquityPerPosPercent { get; set; } //maximum equity per position as a percentage of the available equity - this is used to determine the maximum position size for the entry
		[Input] public double RiskBasedEntryPercent { get; set; } //risk based entry percentage - this is used to base the position size as a percentage of the euity based on the risk taken, set to zero to disable risk based entries
		[Input] public double MaximumVolumeBasedPercent { get; set; } //maximum volume based percentage - this is used to determine the entry size based on the average trading volume
		[Input] public double ClosePercentile { get; set; } //required close percentile - this is used to determine the entry size based on the average trading volume
		[Input] public double VolumePercentile { get; set; } //required volume percentile - this is used to determine the entry size based on the average trading volume
		[Input] public bool AddToPosition { get; set; } //add to position - this will add to the position if the price action analysis returns a lower high
		[Input] public bool UseRoundLots { get; set; } //use round lots for the entry size - this is used to determine the entry size based on the average trading volume
		[Input] public double RetracementExitPercent { get; set; } //exit percentage - this is used to determine the exit percentage based on the entry date
		[Input] public EExitDaysCountStrategy ExitDaysCountStrategy { get; set; } //exit days count strategy - this is used to determine the exit days count based on the entry date
		[Input] public int ExitDaysCount { get; set; } //full exit days count - this is used to determine the full exit days count based on the entry date
		[Input] public bool ExitFriday { get; set; } //exit on Fridays - this will exit the position on Fridays if required
		[Input] public string ExitTime { get; set; } //exit time - this will exit the position at the specified time

		//attribute
		private PriceActionAnalysis m_priceAction;
		private PowerLanguage.Function._VolumePercentile m_volumePercentile;
		private DateTime m_startTime;
		private DateTime m_endTime;
		private IOrderPriced m_se;
		private IInstrument m_primaryTimeFrame;
		private IInstrument m_secondaryTimeFrame;
		private IInstrument m_dailyTimeFrame;
		private DateTime m_exitTime;
		private DateTime m_entryDate;
		private double m_stopLoss;
		private IOrderMarket m_sxPartial;
		private IOrderMarket m_sxAll;
		private IOrderPriced m_sxStopLoss;
		private bool m_retracementExitFilled;
		private bool m_priceActionChanged;
		private bool m_lowerHighDetected;

		//interface methods
		public _ParabolicShortSESX(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			StartTime = DEFAULT_START_TIME;
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			EndTime = DEFAULT_END_TIME;
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			MaximumEquityPerPosPercent = DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT;
			RiskBasedEntryPercent = DEFAULT_RISK_BASED_ENTRY_PERCENT;
			MaximumVolumeBasedPercent = DEFAULT_MAXIMUM_VOLUME_BASED_PERCENT;
			ClosePercentile = DEFAULT_CLOSE_PERCENTILE;
			VolumePercentile = DEFAULT_VOLUME_PERCENTILE;
			AddToPosition = false;
			UseRoundLots = false;
			m_volumePercentile = new PowerLanguage.Function._VolumePercentile(this);
			m_se = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "Se", EOrderAction.SellShort));

			RetracementExitPercent = DEFAULT_RETRACEMENT_EXIT_PERCENT;
			ExitDaysCountStrategy = EExitDaysCountStrategy.CalendarDays;
			ExitDaysCount = DEFAULT_EXIT_DAYS_COUNT;
			ExitTime = DEFAULT_EXIT_TIME;
			m_exitTime = DateTime.ParseExact(DEFAULT_EXIT_TIME, "HH:mm", null);
			ExitFriday = true;
			m_sxPartial = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LxP", EOrderAction.BuyToCover));
			m_sxAll = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LxA", EOrderAction.BuyToCover));
			m_sxStopLoss = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LxSL", EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_startTime = DateTime.ParseExact(StartTime, "HH:mm", null);
			m_endTime = DateTime.ParseExact(EndTime, "HH:mm", null);
			if (MaximumEquityPerPosPercent <= 0 || MaximumEquityPerPosPercent > 1) throw new Exception("MaximumEquityPerPosPercent must be larger than zero and less then one.");
			if (RiskBasedEntryPercent < 0 || RiskBasedEntryPercent > 1) throw new Exception("RiskBasedEntryPercent must be larger than or equal to zero and less than one.");
			if (MaximumVolumeBasedPercent <= 0 || MaximumVolumeBasedPercent > 1) throw new Exception("MaximumVolumeBasedPercent must be larger than zero and less than one.");
			if (ClosePercentile < 0 || ClosePercentile > 1) throw new Exception("RequiredClosePercentile must be larger than or equal to zero and less than one.");
			if (RetracementExitPercent <= 0 || RetracementExitPercent > 1) throw new Exception("RetracementExitPercent must be larger than zero and less/equal to one.");
			if (ExitDaysCount <= 0) throw new Exception("ExitDaysCount must be larger than zero.");
			m_exitTime = DateTime.ParseExact(ExitTime, "HH:mm", null);

			m_priceAction = new PriceActionAnalysis(DEFAULT_FRACTAL_LAYERS);
			m_priceAction.FactorClose = DEFAULT_FACTOR_CLOSE;
			m_primaryTimeFrame = BarsOfData(1);
			m_secondaryTimeFrame = BarsOfData(2);
			m_dailyTimeFrame = BarsOfData(3);
			m_volumePercentile.Instrument = m_primaryTimeFrame;
			m_entryDate = DateTime.MinValue;
			m_stopLoss = -1;

			m_retracementExitFilled = false;
			m_priceActionChanged = false;
			m_lowerHighDetected = false;
		}

		protected override void CalcBar()
		{
			base.CalcBar();

			if (m_secondaryTimeFrame.Status == EBarState.Close)
			{
				m_priceActionChanged = m_priceAction.Analyse(m_secondaryTimeFrame.Time[0], m_secondaryTimeFrame.Open[0], m_secondaryTimeFrame.High[0], m_secondaryTimeFrame.Low[0], m_secondaryTimeFrame.Close[0], m_secondaryTimeFrame.Volume[0]);
				m_lowerHighDetected = m_priceAction.AnalysisState.lowerHigh;
			}

			if (PositionSide == 0)
			{
				m_retracementExitFilled = false;
				m_entryDate = DateTime.MinValue;
				m_stopLoss = -1;
				entry();
			}
			else
				exit();
		}

		//methods

		static DateTime testDate = new DateTime(2025, 5, 12, 0, 0, 0);

		protected void entry() {
			if (m_primaryTimeFrame.Status != EBarState.Close || m_primaryTimeFrame.CurrentBar < 2) return;

			//daily timeframe filter - TBD determine whether this is truly required
			
			if (m_primaryTimeFrame.Time[0].Date == testDate.Date)
				Output.WriteLine($"Test date detected: {testDate.ToString("yyyy-MM-dd")}, skipping daily timeframe filter check.");

			if (m_dailyTimeFrame.CurrentBar < 10) return; //ensure we have enough daily bars to calculate the volume percentile
			if (m_primaryTimeFrame.Close[0] / m_dailyTimeFrame.Close[5] < 2.0 || m_primaryTimeFrame.Close[0] / m_dailyTimeFrame.Close[10] < 2.0) return; //ensure that the daily timeframe has a significant move up, this is used to filter out stocks that are not moving enough

			if (PositionSide != 0 && !AddToPosition) return; //do not enter if there is already a position
			if (m_primaryTimeFrame.Time[0].TimeOfDay < m_startTime.TimeOfDay || m_primaryTimeFrame.Time[0].TimeOfDay > m_endTime.TimeOfDay) return; //exit if time filter not met

			//perform price action analysis on secondary timeframe close 
			if (!m_lowerHighDetected) return; //only enter when lower high is formed on secondary timeframe

			//find the lower high bar - we need to reverse iterate to find the most recent lower high for analysis
			PriceActionBar lowerHighBar = null;
			foreach (var bar in m_priceAction.ChangedBars.Reverse())
				if (bar.Value.state.lowerHigh)
				{
					lowerHighBar = bar.Value;
					break;
				}

			if (lowerHighBar == null)
			{
				trace(ETraceLevel.Error, $"PriceAction analysis returned that a lower high was found but could find the lower high bar");
				return; //do not enter if a lower high did not form yet
			}

			//exit if bar does not have significant volume
			if (VolumePercentile > 0 && m_volumePercentile[0] < VolumePercentile)
			{
				trace(ETraceLevel.Verbose, $"Volume percentile {m_volumePercentile[0]} is below the required volume percentile {VolumePercentile} not entering.");
				return;
			}

			//compare the current entry timeframe bar against the secondary timeframe used for price action analysis
			if (m_primaryTimeFrame.High[0] > lowerHighBar.high)
			{
				trace(ETraceLevel.Verbose, $"Entry timeframe high {m_primaryTimeFrame.High[0]} breaking above lower high {lowerHighBar.high} at {m_primaryTimeFrame.Time[0].ToString("yyyyMMdd-HHmmss.fff")} skipping short entry");
				return;
			}

			//determine the close percentile of the entry bar
			double range = m_primaryTimeFrame.High[0] - m_primaryTimeFrame.Low[0];
			if (range == 0) return; //avoid division by zero
			double closeHighDelta = m_primaryTimeFrame.High[0] - m_primaryTimeFrame.Close[0]; //we need bearish bars to close "far" from the high to get a solid closing percentile
			double closePercentile = closeHighDelta / range;
			if (closePercentile < ClosePercentile) return; //ignore stocks that do not meet the close percentile criteria

			//determine the risk based on the entry bar close and the day high
			double risk = IntradayHigh - m_primaryTimeFrame.Close[0];

			//determine the entry size based on the available equity, risk and volume
			int equityBasedSize = positionSize(m_primaryTimeFrame.Close[0], MaximumEquityPerPosPercent, UseRoundLots);
			int riskBasedSize = riskBasedPositionSize(risk, RiskBasedEntryPercent, UseRoundLots);
			int size = volumeBasedPositionSize(m_dailyTimeFrame, equityBasedSize < riskBasedSize ? equityBasedSize : riskBasedSize, DEFAULT_VOLUME_LENGTH, MaximumVolumeBasedPercent, UseRoundLots);
			if (size == 0) return; //do not enter if the size is zero

			trace(ETraceLevel.Info, $"Short entry at {m_primaryTimeFrame.Close[0]} Size {size} Risk {risk} IntradayHigh {IntradayHigh} EntryBarClose {m_primaryTimeFrame.Close[0]}");
			m_se.Send(m_primaryTimeFrame.Close[0], size);
		}

		protected void exit() {
			//exit on Fridays if required
			if (ExitFriday && m_primaryTimeFrame.Time[0].DayOfWeek == DayOfWeek.Friday && m_primaryTimeFrame.Time[0].TimeOfDay >= m_exitTime.TimeOfDay)
			{
				trace(ETraceLevel.Info, $"Exit Friday {m_exitTime.ToString("HH:mm")}");
				m_sxAll.Send();
				return;
			}

			//determine the entry date of the trade
			if (m_entryDate == DateTime.MinValue)
			{
				DateTime openEntryDate = DateTime.MaxValue;
				DateTime closeEntryDate = DateTime.MaxValue;

				if (CurrentPosition.OpenTrades.Count > 0) openEntryDate = CurrentPosition.OpenTrades.Min(trade => trade.EntryOrder.Time);
				if (CurrentPosition.ClosedTrades.Count > 0) closeEntryDate = CurrentPosition.ClosedTrades.Min(trade => trade.EntryOrder.Time);

				if (openEntryDate != DateTime.MaxValue && openEntryDate < closeEntryDate)
					m_entryDate = openEntryDate;
				else if (closeEntryDate != DateTime.MaxValue && closeEntryDate < openEntryDate)
					m_entryDate = closeEntryDate;
			}
			int daysCount = ExitDaysCountStrategy == EExitDaysCountStrategy.CalendarDays ? calendarDayCount(m_entryDate, m_primaryTimeFrame.Time[0]) : tradingDayCount(m_entryDate, m_primaryTimeFrame.Time[0]);
			trace(ETraceLevel.Info, $"Entry date {m_entryDate.ToString("yyyy-MM-dd")} DaysCount {daysCount}");

			//exit after a specific number of days
			if (ExitDaysCount > 0 && m_primaryTimeFrame.Time[0].TimeOfDay >= m_exitTime.TimeOfDay && daysCount >= ExitDaysCount)
			{
				trace(ETraceLevel.Info, $"Exit after {ExitDaysCount} days");
				m_sxAll.Send();
				return;
			}

			//maintain stop-loss based on the entry day and the formation of new lower highs
			double entryDayHigh = daysCount == 0 ? IntradayHigh : m_dailyTimeFrame.High[daysCount - 1];
			if (m_stopLoss == -1) m_stopLoss = entryDayHigh;
			if (m_priceActionChanged)
				foreach (var bar in m_priceAction.ChangedBars.Reverse())  //we need to reverse iterate the changed bars to find the most recent higher low
				{
					if (bar.Value.state.lowerHigh)
					{
						m_stopLoss = Math.Min(m_stopLoss, bar.Value.high);
						break;
					}
				}

			m_sxStopLoss.Send(m_stopLoss);

			//exit partial position if we reach the specific retracement of the spike
			if (!m_retracementExitFilled && entryDayHigh * RetracementExitPercent > m_primaryTimeFrame.Close[0])
			{
				trace(ETraceLevel.Info, $"Partial exit {m_primaryTimeFrame.Close[0]} EntryDayHigh {entryDayHigh} RetracementExitPercent {RetracementExitPercent}");
				m_sxPartial.Send((int)Math.Ceiling(Math.Abs(StrategyInfo.MarketPosition) / (double)2));
				return;
			}
		}
	}
}