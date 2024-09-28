using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using Ichimoku;

namespace PowerLanguage.Strategy {
    /// <summary>
	/// Chart Type: Candlestick
	/// 
    /// Hypothesis:
	/// Works like a MA crossover but uses the Tenkan Sen and Kijun Sen for the cross (p.151)
	///
	/// Ideal Instruments and Timeframes:
	/// Forex - TBD 
	/// 
    /// Entry Rules - Long:
	/// * Price is above Kumo
	/// * Tenkan Sen crosses over the Kijun Sen
	/// * Chikou Span has bullish open space
	/// * Price close, TenkanSen, KijunSen and Chikou Span should not be in the Kumo cloud, if they are it should be a thick Kumo cloud.
	/// * Optional: Future Senkou A is greater or equal to Future Senkou B.
	/// * Optional: Future Kumo cloud is not thick.
	/// 
    /// Entry Rules - Short:
	/// * Price is below Kumo
	/// * Tenkan Sen crosses under the Kijun Sen
	/// * Chikou Span has bearish open space
	/// * Price close, TenkanSen, KijunSen and Chikou Span should not be in the Kumo cloud, if they are it should be a thick Kumo cloud.
	/// * Optional: Future Senkou A is less or equal to Future Senkou B.
	/// * Optional: Future Kumo cloud is not thick.
	/// 
    /// Exit Rules - Long:
	/// 
    /// 
    /// Exit Rules - Short:
    /// 
    /// 
    /// Money Management:
    /// 
	/// 
    /// Notes:
	/// 
	/// 
	/// </summary>
	public class _IchimokuTSKSCross : SignalObject {
		//Inputs:
        [Input] public int TenkanSenLength { get; set; }
        [Input] public int KijinSenLength { get; set; }
        [Input] public int ChikouSpanShift { get; set; }
        [Input] public int SenkouSpanBLength { get; set; }
        [Input] public int KumoShift { get; set; }	
		[Input] public int StartTime { get; set; }
		[Input] public int WindowLength { get; set; }
		[Input] public int MMStartPosSize { get; set; }
		[Input] public double MMProfitDelta { get; set; }
		[Input] public double MMStartNetProfit { get; set; }		//Start net profit when strategy is restarted between system reboots.		
		[Input] public bool EnableLong { get; set; }				//Enable long trades
		[Input] public bool EnableShort { get; set; }				//Enable short trades
		
		//Attributes:
        private IOrderMarket m_LE_DMM;
        private IOrderMarket m_SE_DMM;
        private IOrderMarket m_LE_SMM;
        private IOrderMarket m_SE_SMM;
        private IOrderPriced m_LX;
        private IOrderPriced m_SX;
        private IchimokuAnalysis m_Ichimoku;
		private VariableSeries<double> m_TenkanSen;
		private VariableSeries<double> m_KijunSen;
		private TimeSpan m_StartTime;
		private TimeSpan m_EndTime;
		
		public _IchimokuTSKSCross(object _ctx):base(_ctx) {
			//initialize input parameters
            TenkanSenLength = 9;
            KijinSenLength = 26;
            ChikouSpanShift = 26;
            SenkouSpanBLength = 52;
            KumoShift = 26;
			StartTime = 0;           //12-AM starting time for trading session       
			WindowLength = 1439;     //minutes in full trading day
			MMStartPosSize = 0;
			MMProfitDelta = 0;
			MMStartNetProfit = 0;
			EnableLong = true;
			EnableShort = true;		
		}
		
		protected override void Create() {
			m_LE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "TSKSCrsLE", EOrderAction.Buy));			//dynamic money management orders
			m_SE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "TSKSCrsSE", EOrderAction.SellShort));			
			m_LE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "TSKSCrsLE", EOrderAction.Buy));					//static money management orders
			m_SE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "TSKSCrsSE", EOrderAction.SellShort));			
			m_LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TSKSCrsLX", EOrderAction.Sell));					//exit orders
			m_SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TSKSCrsSX", EOrderAction.BuyToCover));												
			m_TenkanSen = new VariableSeries<double>(this);			
			m_KijunSen = new VariableSeries<double>(this);			
		}
		
		protected override void StartCalc() {
			//initialize indicators and objects used
            m_Ichimoku = new IchimokuAnalysis(10000, Bars.Info.Resolution.Duration());

			//set the start/end times for the time filter
            m_StartTime = new TimeSpan(StartTime / 100, StartTime % 100, 0);
            m_EndTime = m_StartTime + new TimeSpan(WindowLength / 60, WindowLength % 60, 0);

            //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
            //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
            if (m_EndTime.Days > 0) m_EndTime = m_EndTime.Subtract(new TimeSpan(m_EndTime.Days, 0, 0, 0));
		}

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

        private void CalcBarOrderFilled() {
            orderFilled();
        }

		private void orderFilled() {
			if (PositionSide > 0) m_LX.Send(m_KijunSen[0]);
			if (PositionSide < 0) m_SX.Send(m_KijunSen[0]); 
		}
				
        private void positionManagement() {
			//send initial stop-loss when an entry order is filled
			//if (PositionSide > 0 && m_TenkanSen.CrossesUnder(m_KijunSen, 10)) m_LX.Send();
            //if (PositionSide < 0 && m_TenkanSen.CrossesOver(m_KijunSen, 10)) m_SX.Send();
			if (PositionSide > 0) m_LX.Send(m_KijunSen[0]);
			if (PositionSide < 0) m_SX.Send(m_KijunSen[0]); 

		}
		
		private void CalcBarDefault() {
			//add the bar data to the Ichimoku analysis
			bool analysisReady = m_Ichimoku.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0]);
			m_TenkanSen.Value = m_Ichimoku.TenkanSen;
			m_KijunSen.Value = m_Ichimoku.KijunSen;
			
			if (analysisReady) {
				//perform position management if we're in a position
				positionManagement();
				
				//check for long/short entries
				if (PositionSide == 0) {
	                //apply time filter to entry/exit
	                //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
	                //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
	                DateTime currentTime = Bars.Time.Value;

	                if (((m_StartTime < m_EndTime) && (m_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= m_EndTime)) ||  	//start/end time fall in the same trading day
	                    ((m_StartTime > m_EndTime) && ((m_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= m_EndTime)))) {  //start/end time span over different trading days
						/// * Price is above Kumo
						/// * Tenkan Sen crosses over the Kijun Sen
						/// * Chikou Span has bullish open space
						/// * Price close, TenkanSen, KijunSen and Chikou Span should not be in the Kumo cloud, if they are it should be a thick Kumo cloud.
						/// * Optional: Future Senkou A is greater or equal to Future Senkou B.
						/// * Optional: Future Kumo cloud is not thick.
						bool enterLong = EnableLong &&
										 m_Ichimoku.PriceAboveKumo &&
										 m_TenkanSen.CrossesOver(m_KijunSen, 10); //&&
										 //m_Ichimoku.ChikouSpanBullishOpenSpace &&
										 //(!m_Ichimoku.PriceInKumo || m_Ichimoku.CurrentKumoThick) &&
										 //(!m_Ichimoku.TenkanSenInKumo || m_Ichimoku.CurrentKumoThick) &&
										 //(!m_Ichimoku.KijunSenInKumo || m_Ichimoku.CurrentKumoThick) && 
										 //(!m_Ichimoku.ChikouSpanInKumo || m_Ichimoku.ChikouSpanKumoThick) &&
										 //m_Ichimoku.FutureKumoBullish &&
							             //m_Ichimoku.FutureKumoThin;
															
						/// * Price is below Kumo
						/// * Tenkan Sen crosses under the Kijun Sen
						/// * Chikou Span has bearish open space
						/// * Price close, TenkanSen, KijunSen and Chikou Span should not be in the Kumo cloud, if they are it should be a thick Kumo cloud.
						/// * Optional: Future Senkou A is less or equal to Future Senkou B.
						/// * Optional: Future Kumo cloud is not thick.
						bool enterShort = EnableShort &&
										  !enterLong &&
										  m_Ichimoku.PriceBelowKumo &&
										  m_TenkanSen.CrossesUnder(m_KijunSen, 10); // &&
										  //m_Ichimoku.ChikouSpanBearishOpenSpace &&
										  //(!m_Ichimoku.PriceInKumo || m_Ichimoku.CurrentKumoThick) &&
										  //(!m_Ichimoku.TenkanSenInKumo || m_Ichimoku.CurrentKumoThick) &&
										  //(!m_Ichimoku.KijunSenInKumo || m_Ichimoku.CurrentKumoThick) && 
										  //(!m_Ichimoku.ChikouSpanInKumo || m_Ichimoku.ChikouSpanKumoThick) &&						
										  //m_Ichimoku.FutureKumoBearish &&
							              //m_Ichimoku.FutureKumoThin;
						
						//check whether we're using dynamic or static position sizing
						if (MMStartPosSize > 0 && MMProfitDelta > 0) {
							//dynamic position sizing, compute new trade size
							//compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
							double netProfit = MMStartNetProfit + NetProfit;
							netProfit = (netProfit > 0) ? netProfit : 0;		//net profit can not be negetive for square root calculation below
							int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));
							
							if (enterLong) m_LE_DMM.Send(numLots);
							if (enterShort) m_SE_DMM.Send(numLots);							
						} else {
							//static position sizing controlled by the user settings
							if (enterLong) m_LE_SMM.Send();
							if (enterShort) m_SE_SMM.Send();
						}
					}
				}					
			}			
		}		
	}
}