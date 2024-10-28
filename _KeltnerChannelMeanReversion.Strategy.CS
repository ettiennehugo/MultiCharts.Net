using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
    /// <summary>
	/// Chart Type:
	/// 	Candletick
	/// 
    /// Hypothesis:
	/// 	Assumes that the price would return to the mean when it crosses outside the upper/lower bounds to the Keltner Channel.
	/// 	Strategy was discovered using the StrategyExplorer.
	/// 
    /// Ideal Instruments and Timeframes:
	/// 	Backtesting has shown that strategy works good on all Forex instruments on a 5 minute timeframe.
	/// 
    /// Entry Rules - Long:
	/// 	Price crosses under the lower line of the Keltner Channel.
	/// 
    /// Entry Rules - Short:
	/// 	Price crosses over the upper line of the Keltner Channel.
	/// 
    /// Exit Rules - Long:
	/// 	Price crosses over the midline of the Keltner Channel.
    /// 
    /// Exit Rules - Short:
    /// 	Price crosses under the midline of the Keltner Channel.
    /// 
    /// Money Management:
    /// 	Fixed ratio money management.
	/// 
    /// Notes:
	///		* Strategy loses lots of money in forward testing on lower timeframes even though backtesting has shown that it's quite profitable. The reason it loses
	/// 	  money is due to the bid/ask spread, you might have to work with it on higher timeframes or work in some code to try an minimize the effect of the bid/ask
	/// 	  spread.
	/// 	* Another potential solution would be to use the explicit bid/ask price feeds and compute a Keltner Channel for the bid and the ask and then use the correct
	/// 	  channel for entries and exits. E.g. The bid Keltner Channel would be used to enter long positions and the ask Keltner Channel would be used to exit long
	/// 	  positions.
	/// 
	/// </summary>
	public class _KeltnerChannelMeanReversion : SignalObject {
		//Inputs:
		[Input] public int Length { get; set; }
        [Input] public int ATRLength {  get; set; }
		[Input] public double ScalingFactor { get; set; }
		[Input] public int CrossLookBack { get; set; }
		[Input] public int StartTime { get; set; }
		[Input] public int WindowLength { get; set; }
		[Input] public double StopLossPct { get; set; }
		[Input] public double TrailingStopPct { get; set; }
		[Input] public int MMStartPosSize { get; set; }
		[Input] public double MMProfitDelta { get; set; }
		[Input] public double MMStartNetProfit { get; set; }		//Start net profit when strategy is restarted between system reboots.
		[Input] public int MMMaxPositionSize { get; set; }			//Maximum number of lots/position size to use.
		[Input] public bool EnableLong { get; set; }				//Enable long trades
		[Input] public bool EnableShort { get; set; }				//Enable short trades

		//Attributes:
        private IOrderPriced m_LE_DMM;
        private IOrderPriced m_SE_DMM;
        private IOrderPriced m_LE_SMM;
        private IOrderPriced m_SE_SMM;
        private IOrderPriced m_LX;
        private IOrderPriced m_SX;
        //private LinearRegValue m_LinRegFC;
        private AverageFC m_SMAFC;
		private VariableSeries<Double> m_CenterLine;
        private VariableSeries<Double> m_UpperBand;
        private VariableSeries<Double> m_LowerBand;
		private TimeSpan m_StartTime;
		private TimeSpan m_EndTime;
		
		public _KeltnerChannelMeanReversion(object _ctx):base(_ctx){
            Length = 20;
            ATRLength = 20;
			ScalingFactor = 1.5;
			CrossLookBack = 5;
			StartTime = 0;          		//12:00 AM starting time for trading session       
			WindowLength = 1439;    		//minutes for a full 24-hour trading day
			StopLossPct = 0;        		//stop loss (0 = disable)
			TrailingStopPct = 0;    		//trailing stop (0 = disable)
			MMStartPosSize = 0; 			//start contracts and additional contracts to add (0 - disable dynamic MM)
			MMProfitDelta = 0;				//profit delta before another additional contract is added (0 - disable MM)	
			MMStartNetProfit = 0;			//start net profit between restarts of the strategy
			MMMaxPositionSize = 10000000;	//maximum position size for Forex
			EnableLong = true;				//per default enable long trades
			EnableShort = true;				//per default enable short trades
		}
		
		protected override void Create() {
            //m_LinRegFC = new LinearRegValue(this);
			m_SMAFC = new AverageFC(this);
            m_CenterLine = new VariableSeries<Double>(this);
			m_UpperBand = new VariableSeries<Double>(this);
            m_LowerBand = new VariableSeries<Double>(this);
		
			m_LE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "KTCLE", EOrderAction.Buy));			//dynamic money management orders
			m_SE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "KTCSE", EOrderAction.SellShort));			
			m_LE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KTCLE", EOrderAction.Buy));				//static money management orders
			m_SE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KTCSE", EOrderAction.SellShort));			
			m_LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KTCLX", EOrderAction.Sell));			//exit orders
			m_SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KTCSX", EOrderAction.BuyToCover));			
		}
		
		protected override void StartCalc() {
            //set the start/end times for the time filter
            m_StartTime = new TimeSpan(StartTime / 100, StartTime % 100, 0);
            m_EndTime = m_StartTime + new TimeSpan(WindowLength / 60, WindowLength % 60, 0);

            //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
            //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
            if (m_EndTime.Days > 0) m_EndTime = m_EndTime.Subtract(new TimeSpan(m_EndTime.Days, 0, 0, 0));
			
            //update linear regression and moving average
            //m_LinRegFC.price = Bars.Close;
            //m_LinRegFC.length = LinRegLength;
            //m_LinRegFC.tgtbar = 0;
			m_SMAFC.price = Bars.Close;
            m_SMAFC.length = Length;

		}

        //protected override void StopCalc() {
		//	//cleanup/deinitialize strategy components
		//	
        //}
			
        protected override void CalcBar() {
            switch (Environment.CalcReason) {
                case CalculationReason.Default:
                    CalcBarDefault();
                    break;
                case CalculationReason.OrderFilled:
                    CalcBarOrderFilled();
                    break;
            }
		}
		
        protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price) {
            orderFilled();
        }
		
		private void CalcBarDefault() {
            //update indicator values
          	m_CenterLine.Value = m_SMAFC[0];		//m_LinRegFC[0];

            double scaledAtr = ScalingFactor * this.AverageTrueRange(ATRLength);
			m_LowerBand.Value = m_CenterLine.Value - scaledAtr;
			m_UpperBand.Value = m_CenterLine.Value + scaledAtr;

            //manage signal once we have enough bars to compute indicators
            int longestLength = Length > ATRLength ? Length : ATRLength;
			if (Bars.CurrentBar >= longestLength) {
				//stop-loss/trailing-stop exit
				if (PositionSide != 0) {
                    CurSpecOrdersMode = ESpecOrdersMode.PerContract;					
					if (StopLossPct != 0) GenerateStopLoss(this.EntryPrice() * (StopLossPct / 100));
                    if (TrailingStopPct != 0) GeneratePercentTrailing(this.EntryPrice() * (TrailingStopPct / 100), TrailingStopPct);
					
					//always have an exit order at the moving average if no stop loss is specified
					bool exitLong = PositionSide > 0 && Bars.Close.CrossesOver(m_CenterLine, CrossLookBack);
					bool exitShort = PositionSide < 0 && Bars.Close.CrossesUnder(m_CenterLine, CrossLookBack);
					if (exitLong) m_LX.Send(m_CenterLine[0]);
					if (exitShort) m_SX.Send(m_CenterLine[0]);
					//bool exitLong = PositionSide > 0 && Bars.Close.CrossesOver(m_LowerBand, CrossLookBack);
					//bool exitShort = PositionSide < 0 && Bars.Close.CrossesUnder(m_UpperBand, CrossLookBack);
					//if (exitLong) m_LX.Send(m_LowerBand[0]);
					//if (exitShort) m_SX.Send(m_UpperBand[0]);
					
				} else {
	                //apply time filter for entries
	                //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
	                //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
	                DateTime currentTime = Bars.Time.Value;

	                if (((m_StartTime < m_EndTime) && (m_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= m_EndTime)) ||  	//start/end time fall in the same trading day
	                    ((m_StartTime > m_EndTime) && ((m_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= m_EndTime)))) {  //start/end time span over different trading days
						//determine flags used for entry position
						bool entryLong = Bars.Close.CrossesUnder(m_LowerBand, CrossLookBack);
						bool entryShort = Bars.Close.CrossesOver(m_UpperBand, CrossLookBack);
						//bool entryLong = Bars.Close.CrossesOver(m_LowerBand, CrossLookBack);
						//bool entryShort = Bars.Close.CrossesUnder(m_UpperBand, CrossLookBack);
						
						//check whether we're using dynamic or static position sizing
						if (MMStartPosSize > 0 && MMProfitDelta > 0) {
							//dynamic position sizing, compute new trade size
							//compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
							double netProfit = MMStartNetProfit + NetProfit;
							netProfit = (netProfit > 0) ? netProfit : 0;		//net profit can not be negetive for square root calculation below
							int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));
							numLots = numLots <= MMMaxPositionSize ? numLots : MMMaxPositionSize;
							
							if (entryLong && EnableLong) m_LE_DMM.Send(Bars.High[0], numLots);
							if (entryShort && EnableShort) m_SE_DMM.Send(Bars.Low[0], numLots);							
						} else {
							//static position sizing controlled by the user settings
							if (entryLong && EnableLong) m_LE_SMM.Send(Bars.High[0]);
							if (entryShort && EnableShort) m_SE_SMM.Send(Bars.Low[0]);
						}
					}
				}								
			}
		}

        private void CalcBarOrderFilled() {
            orderFilled();
        }

        private void orderFilled() {
			//send initial stop-loss when an entry order is filled
		
			//if (PositionSide > 0) {
			//
            //}

            //if (PositionSide < 0) {
			//
            //}
        }
		
		
		
	}
}