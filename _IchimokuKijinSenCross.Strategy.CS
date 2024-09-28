using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using Ichimoku;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Chart Type: Candle
  /// 
  /// Hypothesis:
  /// On shorter time frames a price cross of the Kijin Sen is a sign that the market it trying to break out in that direction (p.154)
  /// 
  /// Ideal Instruments and Timeframes:
  /// Forex - TBD but apparently it uses shorter time frames.
  /// 
  /// Entry Rules - Long:
  /// * Price crosses over the Kijin Sen
  /// * Tenkan Sen greater than the Kijin Sen OR the Tenkan Sen should have positive momentum.
  /// * Chikou Span has bullish open space.
  /// * Future Senkou Span B is flat or has positive momentum.
  /// * Future Senkou Span A is greater than Future Senkou Span B OR Future Senkou Span A has positive momentum.
  /// * Price close, Tenkan Sen, Kijin Sen and Chikou Span is outside the Kumo cloud, if they are inside the Kumo Cloud it should be a ???thick??? Kumo cloud.
  /// * Price is not far from the Tenkan Sen and Kijin Sen.
  /// * Optional: Future Kumo cloud is not thick.
  /// 
  /// Entry Rules - Short:
  /// * Price crosses under the Kijin Sen
  /// * Tenkan Sen less than the Kijin Sen OR the Tenkan Sen should have negative momentum.
  /// * Chikou Span has bearish open space.
  /// * Future Senkou Span B is flat or has negative momentum.
  /// * Future Senkou Span A is less than Future Senkou Span B OR Future Senkou Span A has negative momentum.
  /// * Price close, Tenkan Sen, Kijin Sen and Chikou Span is outside the Kumo cloud, if they are inside the Kumo Cloud it should be a ???thick??? Kumo cloud.
  /// * Price is not far from the Tenkan Sen and Kijin Sen.
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
  public class _IchimokuKijinSenCross : SignalObject
  {
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
    [Input] public double MMStartNetProfit { get; set; }    //Start net profit when strategy is restarted between system reboots.		
    [Input] public bool EnableLong { get; set; }        //Enable long trades
    [Input] public bool EnableShort { get; set; }       //Enable short trades

    //Attributes:
    private IOrderPriced m_LE_DMM;
    private IOrderPriced m_SE_DMM;
    private IOrderPriced m_LE_SMM;
    private IOrderPriced m_SE_SMM;
    private IOrderPriced m_LX;
    private IOrderPriced m_SX;
    private IchimokuAnalysis m_Ichimoku;
    private VariableSeries<double> m_KijunSen;
    private TimeSpan m_StartTime;
    private TimeSpan m_EndTime;

    public _IchimokuKijinSenCross(object _ctx) : base(_ctx)
    {
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

    protected override void Create()
    {
      m_LE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "KSCrsLE", EOrderAction.Buy));     //dynamic money management orders
      m_SE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "KSCrsSE", EOrderAction.SellShort));
      m_LE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KSCrsLE", EOrderAction.Buy));         //static money management orders
      m_SE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KSCrsSE", EOrderAction.SellShort));
      m_LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KSCrsLX", EOrderAction.Sell));          //exit orders
      m_SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "KSCrsSX", EOrderAction.BuyToCover));
      m_KijunSen = new VariableSeries<double>(this);
    }

    protected override void StartCalc()
    {
      //initialize indicators and objects used
      m_Ichimoku = new IchimokuAnalysis(10000, Bars.Info.Resolution.Duration());

      //set the start/end times for the time filter
      m_StartTime = new TimeSpan(StartTime / 100, StartTime % 100, 0);
      m_EndTime = m_StartTime + new TimeSpan(WindowLength / 60, WindowLength % 60, 0);

      //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
      //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
      if (m_EndTime.Days > 0) m_EndTime = m_EndTime.Subtract(new TimeSpan(m_EndTime.Days, 0, 0, 0));
    }

    //protected override void StopCalc() {
    //	//cleanup/deinitialize strategy components
    //	
    //}

    protected override void CalcBar()
    {
      switch (Environment.CalcReason)
      {
        case CalculationReason.Default:
          CalcBarDefault();
          break;
        case CalculationReason.OrderFilled:
          CalcBarOrderFilled();
          break;
      }
    }

    protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price)
    {
      positionManagement();
    }

    private void CalcBarOrderFilled()
    {
      positionManagement();
    }

    private void positionManagement()
    {
      //send initial stop-loss when an entry order is filled
      if (PositionSide > 0)
      {
        double stopPrice = m_Ichimoku.TenkanSen < m_Ichimoku.KijunSen ? m_Ichimoku.TenkanSen : m_Ichimoku.KijunSen;
        m_LX.Send(m_Ichimoku.KijunSen);
      }

      if (PositionSide < 0)
      {
        double stopPrice = m_Ichimoku.TenkanSen > m_Ichimoku.KijunSen ? m_Ichimoku.TenkanSen : m_Ichimoku.KijunSen;
        m_SX.Send(m_Ichimoku.KijunSen);
      }
    }

    private void CalcBarDefault()
    {
      //add the bar data to the Ichimoku analysis
      bool analysisReady = m_Ichimoku.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0]);
      m_KijunSen.Value = m_Ichimoku.KijunSen;

      if (analysisReady)
      {
        //perform position management if we're in a position
        positionManagement();

        //check for long/short entries
        if (PositionSide == 0)
        {
          //apply time filter to entry/exit
          //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
          //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
          DateTime currentTime = Bars.Time.Value;

          if (((m_StartTime < m_EndTime) && (m_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= m_EndTime)) ||    //start/end time fall in the same trading day
              ((m_StartTime > m_EndTime) && ((m_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= m_EndTime))))
          {  //start/end time span over different trading days	

            /// * Price crosses over the Kijin Sen
            /// * Tenkan Sen greater than the Kijin Sen OR the Tenkan Sen should have positive momentum.
            /// * Chikou Span has bullish open space.
            /// * Future Senkou Span B is flat or has positive momentum.
            /// * Future Senkou Span A is greater than Future Senkou Span B OR Future Senkou Span A has positive momentum.
            /// * Price close, Tenkan Sen, Kijin Sen and Chikou Span is outside the Kumo cloud, if they are inside the Kumo Cloud it should be a ???thick??? Kumo cloud.
            /// * Price is not far from the Tenkan Sen and Kijin Sen.
            /// * Optional: Future Kumo cloud is not thick.
            bool enterLong = EnableLong &&
                     Bars.Close.CrossesOver(m_KijunSen, 2) &&
                             (m_Ichimoku.TenkanSenAboveKijunSen || 0 < m_Ichimoku.TenkanSenMomentum) &&
                     m_Ichimoku.ChikouSpanBullishOpenSpace &&
                     m_Ichimoku.FutureSenkouBMomentum >= 0 &&
                       (m_Ichimoku.FutureKumoBullish || m_Ichimoku.FutureSenkouAMomentum > 0) &&
                     //(!m_Ichimoku.PriceInKumo || m_Ichimoku.CurrentKumoThick) &&
                     //(!m_Ichimoku.TenkanSenInKumo || m_Ichimoku.CurrentKumoThick) &&
                     //(!m_Ichimoku.KijunSenInKumo || m_Ichimoku.CurrentKumoThick) && 
                     //(!m_Ichimoku.ChikouSpanInKumo || m_Ichimoku.ChikouSpanKumoThick) &&
                     m_Ichimoku.PriceCloseToTenkanSen &&
                      m_Ichimoku.PriceCloseToKijunSen;

            /// * Price crosses under the Kijin Sen
            /// * Tenkan Sen less than the Kijin Sen OR the Tenkan Sen should have negative momentum.
            /// * Chikou Span has bearish open space.
            /// * Future Senkou Span B is flat or has negative momentum.
            /// * Future Senkou Span A is less than Future Senkou Span B OR Future Senkou Span A has negative momentum.
            /// * Price close, Tenkan Sen, Kijin Sen and Chikou Span is outside the Kumo cloud, if they are inside the Kumo Cloud it should be a ???thick??? Kumo cloud.
            /// * Price is not far from the Tenkan Sen and Kijin Sen.
            /// * Optional: Future Kumo cloud is not thick.
            bool enterShort = EnableShort &&
                      !enterLong &&
                      Bars.Close.CrossesUnder(m_KijunSen, 2) &&
                             (m_Ichimoku.TenkanSenBelowKijunSen || 0 > m_Ichimoku.TenkanSenMomentum) &&
                     m_Ichimoku.ChikouSpanBearishOpenSpace &&
                     m_Ichimoku.FutureSenkouBMomentum <= 0 &&
                       (m_Ichimoku.FutureKumoBearish || m_Ichimoku.FutureSenkouAMomentum < 0) &&
                     //(!m_Ichimoku.PriceInKumo || m_Ichimoku.CurrentKumoThick) &&
                     //(!m_Ichimoku.TenkanSenInKumo || m_Ichimoku.CurrentKumoThick) &&
                     //(!m_Ichimoku.KijunSenInKumo || m_Ichimoku.CurrentKumoThick) && 
                     //(!m_Ichimoku.ChikouSpanInKumo || m_Ichimoku.ChikouSpanKumoThick) &&
                     m_Ichimoku.PriceCloseToTenkanSen &&
                      m_Ichimoku.PriceCloseToKijunSen;


            //check whether we're using dynamic or static position sizing
            if (MMStartPosSize > 0 && MMProfitDelta > 0)
            {
              //dynamic position sizing, compute new trade size
              //compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
              double netProfit = MMStartNetProfit + NetProfit;
              netProfit = (netProfit > 0) ? netProfit : 0;    //net profit can not be negetive for square root calculation below
              int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));

              if (enterLong && EnableLong) m_LE_DMM.Send(Bars.High[0], numLots);
              if (enterShort && EnableShort) m_SE_DMM.Send(Bars.Low[0], numLots);
            }
            else
            {
              //static position sizing controlled by the user settings
              if (enterLong && EnableLong) m_LE_SMM.Send(Bars.High[0]);
              if (enterShort && EnableShort) m_SE_SMM.Send(Bars.Low[0]);
            }
          }
        }
      }
    }
  }
}