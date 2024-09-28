using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using Ichimoku;

namespace PowerLanguage.Strategy {
    /// <summary>
	/// Chart Type: Candle
	/// 
    /// Hypothesis:
	/// Conservative trend following strategy using the Ichimoku indicator (p.150)
	/// 
    /// Ideal Instruments and Timeframes:
	/// Forex - 1H, 2H, 4H and D
	/// 
    /// Entry Rules - Long:
	/// * Price is above Kumo cloud
	/// * Tenkan Sen is greater than the Kijin Sen
	/// * Chikou Span is greater than price.
	/// * Future Senkou A is greater than Future Senkou B.
	/// * Price is not far from the Tenkan Sen and the Kijin Sen.
	/// * Tenkan Sen, Kijin Sen and Chikou Span should not be in a thick Kumo cloud.
	/// 
    /// Entry Rules - Short:
	/// * Price is below the Kumo cloud.
	/// * Tenkan Sen is less than the Kijin Sen.
	/// * Chikou Span is less than price.
	/// * Future Senkou A is less than Future Senkou B.
	/// * Price is not far from the Tenkan Sen and the Kijin Sen.
	/// * Tenkan Sen, Kijin Sen and Chikou Span should not be in a thick Kumo cloud.
	/// 
    /// Exit Rules - Long:
	/// TBD
    /// 
    /// Exit Rules - Short:
    /// TBD
    /// 
    /// Money Management:
    /// TBD
	/// 
    /// Notes:
	/// 
	/// 
	/// </summary>
	public class _IchimokuIdeal : SignalObject {
		//Inputs:
        [Input] public int TenkanSenLength { get; set; }
        [Input] public int KijinSenLength { get; set; }
        [Input] public int ChikouSpanShift { get; set; }
        [Input] public int SenkouSpanBLength { get; set; }
        [Input] public int KumoShift { get; set; }	
		[Input] public int MMStartPosSize { get; set; }
		[Input] public double MMProfitDelta { get; set; }
		[Input] public double MMStartNetProfit { get; set; }		//Start net profit when strategy is restarted between system reboots.		
		[Input] public bool EnableLong { get; set; }				//Enable long trades
		[Input] public bool EnableShort { get; set; }				//Enable short trades
		
		//Attributes:
        private IOrderPriced m_LE_DMM;
        private IOrderPriced m_SE_DMM;
        private IOrderPriced m_LE_SMM;
        private IOrderPriced m_SE_SMM;
        private IOrderPriced m_LX;
        private IOrderPriced m_SX;
        private IchimokuAnalysis m_Ichimoku;
		
		private MMI _mmiFC;
		
		public _IchimokuIdeal(object _ctx):base(_ctx){
			//initialize input parameters			
            TenkanSenLength = 9;
            KijinSenLength = 26;
            ChikouSpanShift = 26;
            SenkouSpanBLength = 52;
            KumoShift = 26;
			MMStartPosSize = 0;
			MMProfitDelta = 0;
			MMStartNetProfit = 0;
			EnableLong = true;
			EnableShort = true;
		}
		
		protected override void Create() {
			m_LE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "IIdlLE", EOrderAction.Buy));			//dynamic money management orders
			m_SE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "IIdlSE", EOrderAction.SellShort));			
			m_LE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "IIdlLE", EOrderAction.Buy));				//static money management orders
			m_SE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "IIdlSE", EOrderAction.SellShort));			
			m_LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "IIdlLX", EOrderAction.Sell));					//exit orders
			m_SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "IIdlSX", EOrderAction.BuyToCover));					
			
			_mmiFC = new MMI(this);			
		}
		
		protected override void StartCalc() {
			//initialize indicators and objects used
            m_Ichimoku = new IchimokuAnalysis(10000, Bars.Info.Resolution.Duration());
			_mmiFC.Length = KijinSenLength;
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
			//add the bar data to the Ichimoku analysis
			bool analysisReady = false;
			if (Bars.Status == EBarState.Close)
				analysisReady = m_Ichimoku.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0]);
			
			if (analysisReady) {
				//adjust stop-loss if we're in a trade
				if (PositionSide > 0) m_LX.Send(m_Ichimoku.KijunSen);
	            if (PositionSide < 0) m_SX.Send(m_Ichimoku.KijunSen);
											
				//check for long/short entries
				if (PositionSide == 0) {
					/// * Price is above Kumo cloud
					/// * Tenkan Sen is greater than the Kijin Sen
					/// * Chikou Span is greater than price.
					/// * Future Senkou A is greater than Future Senkou B.
					/// * Price is not far from the Tenkan Sen and the Kijin Sen.
					/// * Tenkan Sen, Kijin Sen and Chikou Span should not be in a thick Kumo cloud.
					bool enterLong = _mmiFC[0] < 55 &&
						             m_Ichimoku.PriceAboveKumo && 
					                 m_Ichimoku.TenkanSenAboveKijunSen &&
									 m_Ichimoku.TenkanSenMomentum > 0 &&		//*
									 m_Ichimoku.KijunSenMomentum > 0 &&		//*
									 //m_Ichimoku.PriceCloseToTenkanSen &&	    //*
									 //m_Ichimoku.PriceCloseToKijinSen &&		//*
									 m_Ichimoku.ChikouSpanAbovePrice &&
									 m_Ichimoku.ChikouSpanBullishOpenSpace &&	//*
									 m_Ichimoku.FutureKumoBullish &&
									 !m_Ichimoku.FutureKumoThick &&			//*
									 //m_Ichimoku.FutureSenkouAMomentum > 0 &&		//*
									 //m_Ichimoku.FutureSenkouBMomentum > 0 &&		//*	
								  	 //!m_Ichimoku.PriceInKumoShadow &&				//*
									 (!m_Ichimoku.TenkanSenInKumo || m_Ichimoku.CurrentKumoThin) &&
									 (!m_Ichimoku.KijunSenInKumo || m_Ichimoku.CurrentKumoThin) &&
									 (!m_Ichimoku.ChikouSpanInKumo || m_Ichimoku.CurrentKumoThin);

					/// * Price is below the Kumo cloud.
					/// * Tenkan Sen is less than the Kijin Sen.
					/// * Chikou Span is less than price.
					/// * Future Senkou A is less than Future Senkou B.
					/// * Price is not far from the Tenkan Sen and the Kijin Sen.
					/// * Tenkan Sen, Kijin Sen and Chikou Span should not be in a thick Kumo cloud.
					bool enterShort = !enterLong &&
						               _mmiFC[0] < 55 &&
									  m_Ichimoku.PriceBelowKumo && 
					                  m_Ichimoku.TenkanSenBelowKijunSen &&
									  m_Ichimoku.TenkanSenMomentum < 0 &&		//*
									  m_Ichimoku.KijunSenMomentum < 0 &&		//*
									  //m_Ichimoku.PriceCloseToTenkanSen &&	    //*
									  //m_Ichimoku.PriceCloseToKijinSen &&		//*
									  m_Ichimoku.ChikouSpanBelowPrice &&
									  m_Ichimoku.ChikouSpanBearishOpenSpace &&	//*
									  m_Ichimoku.FutureKumoBearish &&
									  !m_Ichimoku.FutureKumoThick &&			//*
									  //m_Ichimoku.FutureSenkouAMomentum < 0 &&		//*
									  //m_Ichimoku.FutureSenkouBMomentum < 0 &&		//*
									  //!m_Ichimoku.PriceInKumoShadow &&			//*
									  (!m_Ichimoku.TenkanSenInKumo || m_Ichimoku.CurrentKumoThin) &&
									  (!m_Ichimoku.KijunSenInKumo || m_Ichimoku.CurrentKumoThin) &&
									  (!m_Ichimoku.ChikouSpanInKumo || m_Ichimoku.CurrentKumoThin);
					
					//check whether we're using dynamic or static position sizing
					if (MMStartPosSize > 0 && MMProfitDelta > 0) {
						//dynamic position sizing, compute new trade size
						//compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
						double netProfit = MMStartNetProfit + NetProfit;
						netProfit = (netProfit > 0) ? netProfit : 0;		//net profit can not be negetive for square root calculation below
						int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));
						
						if (enterLong && EnableLong) m_LE_DMM.Send(Bars.High[0], numLots);
						if (enterShort && EnableShort) m_SE_DMM.Send(Bars.Low[0], numLots);							
					} else {
						//static position sizing controlled by the user settings
						if (enterLong && EnableLong) m_LE_SMM.Send(Bars.High[0]);
						if (enterShort && EnableShort) m_SE_SMM.Send(Bars.Low[0]);
					}					
				}					
			}			
		}

        private void CalcBarOrderFilled() {
            orderFilled();
        }

        private void orderFilled() {
			//send initial stop-loss when an entry order is filled
			if (PositionSide > 0) m_LX.Send(m_Ichimoku.KijunSen);
            if (PositionSide < 0) m_SX.Send(m_Ichimoku.KijunSen);
        }
		
		
		
	}
}