using System;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	///Episodic Pivot trade management for instruments that have gapped up on earnings or some other catalyst.
	///Data feeds:
	/// 1 1/5/15-minute chart for the exit calculations
	///	2 Daily chart for the exit
	///Exit Rules
	///The first 5 days are crucial for a successful trade, chickens can turn against you while ducks can turn down after 5-days.EP's also tend to fizzle out over time so most of the profit is made in the first 5 days - if the stock is not moving or stalling out exit the trade.
	///	• Don't worry about the ADR here
	///	• Larger cap stocks(>10B) tend to move sideways to up, so you might need different trade management for them.
	///		○ 84% of large cap stocks tend to move sideways to upward for at least 5 days - this strategy just works on the inputs, any partitioning on fundamental factors need to be made in the portfolio manager
	///		○ Maybe user a slightly less percentage since these stocks do not necessarily move as quickly as smaller cap stocks.
	///	1. Exit half the position if it's up and move SL to breakeven on 5-days
	///		a. Roll the rest of the position to exit on an overextension or a close under the 10MA, 20MA or even the 50MA - if you've made a good risk to reward ratio (e.g. 10x or more then start following on the 10MA as to not allow the market to turn against you).
	///	2. Exit full position
	///		a.If the stock did not break the high of the EP entry day after 5-days (EP day is day 0, next day 1 etc.)
	///		b.If the stock is in a bullish overextension - use an ATR factor above the 10MA
	///		c.If the stock shoot up on the EP day tend to reverse course, e.g. 20%+ exit full position on EP day
	///		d.Once the position is up 10x/20x of the risk tighten the trailing stop to the 10MA to avoid the market turn against you
	///If the stock shoot up on the EP day tend to reverse course, e.g. 20%+ exit full position on EP day
	public class _EpisodicPivotLX : _CustomSignal
	{
		//constants
		public const int DEFAULT_FAST_MA = 10;
		public const int DEFAULT_MEDIUM_MA = 20;
		public const int DEFAULT_SLOW_MA = 50;
		public const int DEFAULT_ATR_LENGTH = 14;
		public const double DEFAULT_ATR_FACTOR = 4.0;
		public const int DEFAULT_FIRST_EXIT_DAYS_COUNT = 5; //first exit days count - this is used to determine the first exit days count based on the entry date
		public const double DEFAULT_EP_DAY_PERCENT_LIMIT = 0.3; //exit full position on EP day if the stock is up more than this percentage - this is used to determine the exit days count based on the entry date
		public const double DEFAULT_FASTMA_TIGHT_STOP_RISK_REWARD = 10.0; //activate tight stop at Fast MA once the risk reward ratio exceeds this ratio

		//types
		public enum EExitDaysCountStrategy
		{
			CalendarDays,
			TradingDays
		};

		//properties
		[Input] public int FastMA { get; set; }       //fast moving average for the exit calculations
		[Input] public int MediumMA { get; set; }     //medium moving average for the exit calculations
		[Input] public int SlowMA { get; set; }       //slow moving average for the exit calculations
		[Input] public int ATRLength { get; set; }    //length of the ATR for the exit calculations
		[Input] public double ATRFactor { get; set; } //factor for the ATR for the exit calculations
		[Input] public EExitDaysCountStrategy ExitDaysCountStrategy { get; set; } //exit days count strategy - this is used to determine the exit days count based on the entry date
		[Input] public int FirstExitDaysCount { get; set; } //first exit days count - this is used to determine the first exit days count based on the entry date
		[Input] public double EPDayPercentLimit { get; set; } //exit full position on EP day if the stock is up more than this percentage - this is used to determine the exit days count based on the entry date
		[Input] public double FastMATightStopRiskReward { get; set; } //activate tight stop at Fast MA once the risk reward ratio exceeds this ratio

		//attribute
		private IOrderMarket m_lxPartial;
		private IOrderMarket m_lxAll;
		private IOrderPriced m_lxStopLoss;
		private IOrderPriced m_lxTightStopAll;
		private PowerLanguage.Function._AverageSimple m_fastMA;
		private PowerLanguage.Function._AverageSimple m_mediumMA;
		private PowerLanguage.Function._AverageSimple m_slowMA;
		private _ATR m_atr;
		private IInstrument m_minuteTimeFrame;
		private IInstrument m_dailyTimeFrame;
		private double m_epDayPercentLimit;

		//interface methods
		public _EpisodicPivotLX(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			m_fastMA = new PowerLanguage.Function._AverageSimple(this);
			m_mediumMA = new PowerLanguage.Function._AverageSimple(this);
			m_slowMA = new PowerLanguage.Function._AverageSimple(this);
			m_atr = new _ATR(this);
			FastMA = DEFAULT_FAST_MA;
			MediumMA = DEFAULT_MEDIUM_MA;
			SlowMA = DEFAULT_SLOW_MA;
			ATRLength = DEFAULT_ATR_LENGTH;
			ATRFactor = DEFAULT_ATR_FACTOR;
			ExitDaysCountStrategy = EExitDaysCountStrategy.TradingDays;
			FirstExitDaysCount = DEFAULT_FIRST_EXIT_DAYS_COUNT;
			EPDayPercentLimit = DEFAULT_EP_DAY_PERCENT_LIMIT;
			FastMATightStopRiskReward = DEFAULT_FASTMA_TIGHT_STOP_RISK_REWARD;
			m_lxPartial = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LxP", EOrderAction.Sell, OrderExit.Total));
			m_lxAll = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LxA", EOrderAction.Sell, OrderExit.Total));
			m_lxStopLoss = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LxSL", EOrderAction.Sell, OrderExit.Total));
			m_lxTightStopAll = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LxTSL", EOrderAction.Sell, OrderExit.Total));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (FastMA <= 0) throw new Exception("FastMA must be larger than zero.");
			if (MediumMA <= 0) throw new Exception("MediumMA must be larger than zero.");
			if (FastMA > MediumMA) throw new Exception("FastMA must be less than MediumMA.");
			if (SlowMA <= 0) throw new Exception("SlowMA must be larger than zero.");
			if (MediumMA > SlowMA) throw new Exception("MediumMA must be less than SlowMA.");
			if (ATRLength <= 0) throw new Exception("ATRLength must be larger than zero.");
			if (FirstExitDaysCount <= 0) throw new Exception("FirstExitDaysCount must be larger than zero.");
			if (EPDayPercentLimit <= 0) throw new Exception("EPDayPercentLimit must be larger than zero.");
			if (FastMATightStopRiskReward <= 1) throw new Exception("FastMATightStopRiskReward must be larger than one.");
			m_minuteTimeFrame = BarsOfData(1);
			m_dailyTimeFrame = BarsOfData(2);
			m_fastMA.Instrument = m_dailyTimeFrame;
			m_fastMA.Length = FastMA;
			m_mediumMA.Instrument = m_dailyTimeFrame;
			m_mediumMA.Length = MediumMA;
			m_slowMA.Instrument = m_dailyTimeFrame;
			m_slowMA.Length = SlowMA;
			m_atr.Instrument = m_dailyTimeFrame;
			m_atr.Length = ATRLength;
			m_epDayPercentLimit = 1 + EPDayPercentLimit;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (PositionSide == 0) return; //do not exit if there is no position  

			//determine the entry date of the trade  
			DateTime entryDate = CurrentPosition.OpenTrades.Min(trade => trade.EntryOrder.Time);
			if (CurrentPosition.ClosedTrades.Count > 0) entryDate = CurrentPosition.ClosedTrades.Min(trade => trade.EntryOrder.Time);
			int daysCount = ExitDaysCountStrategy == EExitDaysCountStrategy.CalendarDays ? calendarDayCount(entryDate, Bars.Time[0]) : tradingDayCount(entryDate, Bars.Time[0]);
			trace(ETraceLevel.Info, $"Entry date({entryDate.ToString("yyyy-MM-dd")}), DaysCount({daysCount})");


			//determine the entry day risk that was taken
			double entryRisk = StrategyInfo.AvgEntryPrice - (daysCount == 0 ? IntradayLow : m_dailyTimeFrame.Low[daysCount - 1]); //-1 since daily timeframe would be the previous day

			//maintain stop-loss based on the current price and the average entry price
			CurSpecOrdersMode = ESpecOrdersMode.PerContract;
			//GenerateStopLoss(entryRisk);
			m_lxStopLoss.Send(daysCount == 0 ? IntradayLow : m_dailyTimeFrame.Low[daysCount - 1]);
			
			//exit half position on first exit days after the entry and we have not exited anything out of the current position
			if (daysCount >= FirstExitDaysCount && CurrentPosition.ClosedTrades.Count == 0)
				m_lxPartial.Send((int)Math.Truncate((double)this.StrategyInfo.MarketPosition / 2));

			//move stop-loss to breakeven after the first exit days
			if (daysCount >= FirstExitDaysCount)
			{
				trace(ETraceLevel.Info, $"Moving stop-loss to breakeven after first exit days.");
				GenerateBreakEven(0); //0 to force the stop-loss to breakeven
			}

			//exit the rest of the position based on general exit rules
			///	2. Exit full position
			///		a.If the stock did not break the HIGH of the EP entry day after 5-days (EP day is day 0, next day 1 etc.)
			///		b.If the stock is in a bullish overextension - use an ATR factor above the 10MA
			///		c.If the stock shoot up on the EP day they tend to reverse course, e.g. 20%+ exit full position on EP day
			///		d.Once the position is up 10x/20x of the risk tighten the trailing stop to the 10MA to avoid the market turn against you
			if (daysCount == FirstExitDaysCount && m_dailyTimeFrame.Status == EBarState.Close && m_dailyTimeFrame.High[daysCount] > m_dailyTimeFrame.Close[0])	//a.
			{
				trace(ETraceLevel.Info, $"Exiting position since close after {FirstExitDaysCount} days did not break the high of the EP day.");
				m_lxAll.Send();
			}

			//NOTE: We need to disable the bullish overextension initially since on an episodic pivot the stock will almost certainly be overextended for the first
			//      few days of the trade.
			if (daysCount >= FastMA / 2 && m_minuteTimeFrame.Close[0] > m_fastMA[0] + (m_atr[0] * ATRFactor) && m_minuteTimeFrame.Status == EBarState.Close)	//b.
			{
				trace(ETraceLevel.Info, $"Exiting position based on bullish overextension.");
				m_lxAll.Send();
			}

			if (daysCount == 0 && m_minuteTimeFrame.Status == EBarState.Close && m_dailyTimeFrame.Open[0] != 0 && (m_minuteTimeFrame.Close[0] / m_dailyTimeFrame.Open[0]) > m_epDayPercentLimit) //c.
			{
				trace(ETraceLevel.Info, $"Exiting position based on EP day percentage limit.");
				m_lxAll.Send();
			}

			if (entryRisk != 0 && CurrentPosition.Profit / entryRisk > FastMATightStopRiskReward && m_dailyTimeFrame.Status == EBarState.Close) //d.
			{
				trace(ETraceLevel.Info, $"Exiting position based on risk reward ratio.");
				m_lxTightStopAll.Send(m_fastMA[0]);
			}

			//close position when we're back testing and this is the last bar
			if (!Environment.IsRealTimeCalc && m_dailyTimeFrame.LastBarOnChart)
			{
				trace(ETraceLevel.Info, $"Exiting position on last bar during back testing");
				m_lxAll.Send();
			}
		}

		//methods

	}
}