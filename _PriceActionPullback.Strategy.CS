using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;

namespace PowerLanguage.Strategy {
	
	/// <summary>
	/// Hypothesis:
	/// Buying/selling on pullback (lower-low or higher-higher) should be a reliable signal.
	/// 
	/// Ideal Instruments and Timeframes:
	/// TBD
	/// 
	/// Entry Rules - Long:
	/// 1. Fast MA is above Slow MA
	/// 2. Lower-low bar occurs that's not an outside bar. Enter long with stop-loss under low of lower-low bar.
	/// 
	/// Entry Rules - Short:
	/// 1. Fast MA is below Slow MA
	/// 2. Higher-high occurs that's not an outside bar. Enter short with stop-loss above high of higher-high bar.
	/// 
	/// Exit Rules - Long:
	/// 1. Move stop-loss up when pivot-lows occur that are higher than the current stop-loss.
	/// 
	/// Exit Rules - Short:
	/// 1. Move stop-loss down when pivot-highs occur that are lower than the current stop-loss. 
	/// 
	/// Money Management:
	/// Position size based on strategy properties - ENHANCEMENT: Add fixed ratio position sizing if signal is reliable.
	/// 
	/// Notes:
	/// * There's a lag on the lower-low/higher-high detection that introduces additional risk.
	/// </summary>
	public class _PriceActionPullback : SignalObject {
		[Input] public int FastMALength { get; set; }
		[Input] public int SlowMALength { get; set; }
		[Input] public int ATRLength { get; set; }		
		[Input] public int StartTime { get; set; }
		[Input] public int WindowLength { get; set; }
		[Input] public int MMStartPosSize { get; set; }
		[Input] public double MMProfitDelta { get; set; }
		[Input] public double MMStartNetProfit { get; set; }		//Start net profit when strategy is restarted between system reboots.
		[Input] public bool EnableLong { get; set; }
		[Input] public bool EnableShort { get; set; } 
		
        private IOrderMarket m_LE_DMM;
        private IOrderMarket m_SE_DMM;
        private IOrderMarket m_LE_SMM;
        private IOrderMarket m_SE_SMM;
        private IOrderPriced m_LX;
        private IOrderPriced m_SX;
        private XAverage m_FastXAverage;		
        private XAverage m_SlowXAverage;		
        private VariableSeries<Double> m_FastMA;		
        private VariableSeries<Double> m_SlowMA;
		private PriceActionAnalysis m_PriceActionAnalysis;
        private double m_StopLossPrice;
		private TimeSpan m_StartTime;
		private TimeSpan m_EndTime;
		

		public _PriceActionPullback(object _ctx):base(_ctx){
			FastMALength = 21;
			SlowMALength = 55;
			ATRLength = PriceActionAnalysis.DefaultAtrLength;
			EnableLong = true;
			EnableShort = true;		
		}

		protected override void Create() {
			//create indicators used for signal
			m_PriceActionAnalysis = new PriceActionAnalysis(3);			
            m_FastXAverage = new XAverage(this);
            m_SlowXAverage = new XAverage(this);
            m_FastMA = new VariableSeries<Double>(this);
            m_SlowMA = new VariableSeries<Double>(this);
			m_LE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "PAPBLE", EOrderAction.Buy));			//dynamic money management orders
			m_SE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "PAPBSE", EOrderAction.SellShort));			
			m_LE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "PAPBLE", EOrderAction.Buy));				    //static money management orders
			m_SE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "PAPBSE", EOrderAction.SellShort));			
			m_LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PAPBLX", EOrderAction.Sell));         					//exit orders
			m_SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PAPBSX", EOrderAction.BuyToCover));
            m_StopLossPrice = 0;
		}
		
		protected override void StartCalc() {
            //set the start/end times for the time filter
            m_StartTime = new TimeSpan(StartTime / 100, StartTime % 100, 0);
            m_EndTime = m_StartTime + new TimeSpan(WindowLength / 60, WindowLength % 60, 0);

            //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
            //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
            if (m_EndTime.Days > 0) m_EndTime = m_EndTime.Subtract(new TimeSpan(m_EndTime.Days, 0, 0, 0));
			
			//setup indicators used
			m_PriceActionAnalysis.Clear();			
			m_PriceActionAnalysis.AtrLength = ATRLength;			
            m_FastXAverage.Price = Bars.Close;
            m_FastXAverage.Length = FastMALength;
            m_SlowXAverage.Price = Bars.Close;
            m_SlowXAverage.Length = SlowMALength;			
		}

		protected override void CalcBar(){
			switch (Environment.CalcReason) {
				case CalculationReason.Default:
					CalcBarDefault();
					break;
				case CalculationReason.OrderFilled:
					CalcBarOrderFilled();
					break;
				
			}
		}
		
		protected void CalcBarDefault() {
            bool priceActionChanged = false;
						
            //add bar to price action data
            priceActionChanged = m_PriceActionAnalysis.Analyse(Bars.Time[0], Bars.CurrentBar, Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);
						
			//update stop-loss price if required
			if (priceActionChanged && (m_PriceActionAnalysis.AnalysisState.pivotLow || m_PriceActionAnalysis.AnalysisState.pivotHigh)) {
				//get the pivot bar to adjust stop-loss
                PriceActionBar pivotBar = m_PriceActionAnalysis.CurrentMin1;
				
				if (PositionSide > 0 && pivotBar.state.pivotState[0].pivotLow && m_StopLossPrice < pivotBar.low) m_StopLossPrice = pivotBar.low;								
				if (PositionSide < 0 && pivotBar.state.pivotState[0].pivotHigh && m_StopLossPrice > pivotBar.high) m_StopLossPrice = pivotBar.high;
			}
						
			//send stop-losses order if required
			if (PositionSide != 0) {
				if (PositionSide > 0)m_LX.Send(m_StopLossPrice);									
				if (PositionSide < 0) m_SX.Send(m_StopLossPrice);
			}
			
            if (Bars.CurrentBar > SlowMALength) {
				//update moving averages from new price data
				m_FastMA.Value = m_FastXAverage[0];					
				m_SlowMA.Value = m_SlowXAverage[0];
				
                //process entries if we're flat and we have a pivot point
                if (PositionSide == 0 && priceActionChanged && (m_PriceActionAnalysis.AnalysisState.pivotLow || m_PriceActionAnalysis.AnalysisState.pivotHigh)) {
					//apply time filter for entries
	                //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
	                //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
	                DateTime currentTime = Bars.Time.Value;

	                if (((m_StartTime < m_EndTime) && (m_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= m_EndTime)) ||  	//start/end time fall in the same trading day
	                    ((m_StartTime > m_EndTime) && ((m_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= m_EndTime)))) {  //start/end time span over different trading days					
						//get the bar around which the pivot low/high was formed
	                    //NOTE: We always work with the first fractal layer so currentMin1 bar will be the detected pivot point.
	                    PriceActionBar pivotBar = m_PriceActionAnalysis.CurrentMin1;
						
	                    //determine entry for long/short trades
	                    bool openLongTrade = false;
	                    bool openShortTrade = false;
						
						/// Entry Rules - Long:
						/// 1. Fast MA is above Slow MA
						/// 2. Lower-low bar occurs that's not an outside bar. Enter long with stop-loss under low of lower-low bar.
						//openLongTrade = (m_FastMA.Value > m_SlowMA.Value) && pivotBar.state.lowerLow;// && !pivotBar.state.outside;
						openLongTrade = (m_FastMA.Value < m_SlowMA.Value) && pivotBar.state.higherHigh;
												
						/// Entry Rules - Short:
						/// 1. Fast MA is below Slow MA
						/// 2. Higher-high occurs that's not an outside bar. Enter short with stop-loss above high of higher-high bar.
						//if (!openLongTrade) openShortTrade = (m_FastMA.Value < m_SlowMA.Value) && pivotBar.state.higherHigh;// && !pivotBar.state.outside;
						if (!openLongTrade) openShortTrade = (m_FastMA.Value > m_SlowMA.Value) && pivotBar.state.lowerLow;
						
						
						//check whether we're using dynamic or static position sizing
						if (MMStartPosSize > 0 && MMProfitDelta > 0) {
							//dynamic position sizing, compute new trade size
							//compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
							double netProfit = MMStartNetProfit + NetProfit;
							netProfit = (netProfit > 0) ? netProfit : 0;		//net profit can not be negetive for square root calculation below
							int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));
							
		                    if (EnableLong && openLongTrade) {
		                        m_LE_DMM.Send(numLots);
		                        m_StopLossPrice = pivotBar.low;
							}

		                    if (EnableShort && openShortTrade) {
								m_SE_DMM.Send(numLots);
		                        m_StopLossPrice = pivotBar.high;
							}
						} else {
							//static position sizing controlled by the user settings
		                    if (EnableLong && openLongTrade) {
		                        m_LE_SMM.Send();
		                        m_StopLossPrice = pivotBar.low;
							}

		                    if (EnableShort && openShortTrade) {
								m_SE_SMM.Send();
		                        m_StopLossPrice = pivotBar.high;
							}
						}						
					}
				}								
			}
		}
		
		/// <summary>
		/// Catches recalulation if order filled reason is received.
		/// </summary>
		protected void CalcBarOrderFilled() {
			if (PositionSide > 0) m_LX.Send(m_StopLossPrice);
			if (PositionSide < 0) m_SX.Send(m_StopLossPrice);
		}
		
		/// <summary>
		/// Live trading handling of broker strategy order filled.
		/// </summary>
		protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price) {
			if (PositionSide > 0) m_LX.Send(m_StopLossPrice);
			if (PositionSide < 0) m_SX.Send(m_StopLossPrice);
		}
	}
}