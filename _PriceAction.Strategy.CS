using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;

namespace PowerLanguage.Strategy {
  /// <summary>
  /// Chart Type: Candlestick
  /// 
  /// Hypothesis:
  ///   Implements a number of generic entries and exits that are used to enter the market to make a profit.
  /// 
  /// Ideal Instruments and Timeframes:
  ///   
  ///   TBD
  /// 
  /// Entry Rules - Long:
  /// 
  /// 
  /// 
  /// Entry Rules - Short:
  /// 
  /// 
  /// 
  /// Exit Rules - Long:
  /// 
  /// 
  /// Exit Rules - Short:
  /// 
  /// 
  /// Money Management:
  ///   Fixed ratio position sizing. Disable dynamic money management by setting the money management start size input to zero (0).
  /// 
  /// Notes:
  /// 
  /// 
  /// 
  /// 
  /// </summary>


  //IMPORTANT: NONE of the strategies below will really work unless you have some convolution of different factors, e.g. a double top/bottom is worthless unless there's some other
  //           aspects working in it's favor like a Fib retracement and zero level price being defended (1.9700 or 1.9000) and support/resistance.
	

  // * Entry strategy 1 - Double top/bottom formed, this would require getting the pivots working and double top/bottom detection working. This would entail entering the
  //   trade as soon as the double top/bottom is detected and then entering at the market putting a stop-loss right above the pivot that caused the double top/bottom to be detected
  //   and moving the stop-loss up/down according to the pivots that form.
  // * Entry strategy 2 - Enter at a market order as soon as a higher low or lower high is detected. Enter long on the higher low and enter short on the lower high, this indicates that
  //   the market is strengething or weakening.
  // * Entry strategy 3 - Enter at a stop order on the close of an engulfing bar, place stop loss at the opposite end of the engulfing bar with maybe some buffer value. 
  // * Entry strategy 4 - Enter a stop order on the close of a breakout bar, there seems to be a tendency to test the close of a breakout bar and then the market moves in the opposite direction,
  //   you might need to add some distance for risk since the market seems to test the area quite a bit before moving in favor of such a trade, maybe use the ATR as a measure for the risk.
  // * Entry strategy 5 - Enter at the market on a stong reversal bar within the direction of the strong reversal bar, enter a stop order at the end of the bar with a stop loss at the
  //   opposite end of the bar, e.g. on a strong reversal bullish bar set a stop entry order at the top of the bar and if it is hit set the stop-loss at the bottom of the strong reversal
  //   bar.
  // * Entry strategy 6 - Doji followed by a reversal bar, set stop entry in direction of reversal bar with stop-loss above the bar. Doji indicates indicision while the reversal bar
  //   indicates the market came to a conclusion.
  // * Entry strategy 7 - Breakout of one or more inside bars, add a number of bars to wait for the breakout since it might take a few bars to break out. 


  //TODO: Need to add exit processing when signal is being processed.
  // * Exit strategy 1 - Static take profit: Exit the position with a profit at least twice the risk taken.
  // * Exit strategy 2 - Dynamic take profit: 
  // * Exit strategy 3 - Move the stop loss all the way down with the pivots forming in the direction of the trade, e.g. with a bullish trade move the stop loss
  //   up on pivots that are higher than the current stop loss so either the current stop loss will be hit for a loss of the bar size while as the stop moves with
  //   the pivots higher the trade profit would improve in your favour as the market fails to break previous lows.

  public enum EntryStrategy {
    InsideBarBreakout,
  }

  public enum ExitStrategy {
    StaticTakeProfit,
    DynamicTakeProfit,
  }



  public class _PriceAction : SignalObject {
    public _PriceAction(object _ctx) : base(_ctx) {
      Size = PriceActionAnalysis.DefaultSize;
      AtrLength = PriceActionAnalysis.DefaultAtrLength;
      FactorDoji = PriceActionAnalysis.DefaultFactorDoji;
      FactorClose = PriceActionAnalysis.DefaultFactorClose;
      FactorStrong = PriceActionAnalysis.DefaultFactorStrong;
      FactorEpsilon = PriceActionAnalysis.DefaultFactorEpsilon;
      StartTime = 0;          //12:00 AM starting time for trading session       
      WindowLength = 1439;    //minutes for a full 24-hour trading day
      MMStartPosSize = 0;   //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;    //profit delta before another additional contract is added (0 - disable MM)				
      MMStartNetProfit = 0; //start net profit between restarts of the strategy			
    }

    //Inputs:
    [Input] public int Size { get; set; }
    [Input] public int AtrLength { get; set; }
    [Input] public double FactorDoji { get; set; }
    [Input] public double FactorClose { get; set; }
    [Input] public double FactorStrong { get; set; }
    [Input] public double FactorEpsilon { get; set; }
    [Input] public int StartTime { get; set; }
    [Input] public int WindowLength { get; set; }
    [Input] public int MMStartPosSize { get; set; }
    [Input] public double MMProfitDelta { get; set; }
    [Input] public double MMStartNetProfit { get; set; }

    //Attributes:
    private IOrderPriced m_LE_DMM;
    private IOrderPriced m_SE_DMM;
    private IOrderPriced m_LE_SMM;
    private IOrderPriced m_SE_SMM;
    private IOrderPriced m_LX;
    private IOrderPriced m_SX;
    private TimeSpan m_StartTime;
    private TimeSpan m_EndTime;
    private PriceActionAnalysis m_PriceActionAnalysis;

    protected override void Create() {
      m_PriceActionAnalysis = new PriceActionAnalysis(3);
      m_LE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "PALE", EOrderAction.Buy));      //dynamic money management orders
      m_SE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "PASE", EOrderAction.SellShort));
      m_LE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PALE", EOrderAction.Buy));        //static money management orders
      m_SE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PASE", EOrderAction.SellShort));
      m_LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PALX", EOrderAction.Sell));         //exit orders
      m_SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "PASX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc() {
      //set price action attributes
      m_PriceActionAnalysis.Size = Size;
      m_PriceActionAnalysis.AtrLength = AtrLength;
      m_PriceActionAnalysis.FactorDoji = FactorDoji;
      m_PriceActionAnalysis.FactorClose = FactorClose;
      m_PriceActionAnalysis.FactorStrong = FactorStrong;
      m_PriceActionAnalysis.FactorEpsilon = FactorEpsilon;

      //set the start/end times for the time filter
      m_StartTime = new TimeSpan(StartTime / 100, StartTime % 100, 0);
      m_EndTime = m_StartTime + new TimeSpan(WindowLength / 60, WindowLength % 60, 0);

      //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
      //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
      if (m_EndTime.Days > 0)
        m_EndTime = m_EndTime.Subtract(new TimeSpan(m_EndTime.Days, 0, 0, 0));
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

    private void CalcBarDefault() {
      //process the entry/exit logic
      if (PositionSide == 0)
        calcBarEntry();
      else
        calcBarExit();
    }

    private void CalcBarOrderFilled() {
      calcBarExit();
    }


    private void calcBarEntry() {
      
      


    }


    private void calcBarExit() {


    }
















    protected void OldCalcBar() {
      bool priceActionChanged = false;

      //add bar to price action data
      //NOTE: Price action always wait for complete bars before analysis so we always compute the analysis on the bar's close.
      if (Bars.Status == EBarState.Close)
        priceActionChanged = m_PriceActionAnalysis.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);

      //we only process entries when we detected price action
      if (true == priceActionChanged) {
        //get the price action of the bar we need to analyze
        PriceActionBar bar = m_PriceActionAnalysis.Bars[Bars.Time[0]];

        //apply time filter for entries
        //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
        //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
        DateTime currentTime = Bars.Time.Value;

        if (((m_StartTime < m_EndTime) && (m_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= m_EndTime)) ||    //start/end time fall in the same trading day
            ((m_StartTime > m_EndTime) && ((m_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= m_EndTime)))) {  //start/end time span over different trading days
                                                                                                                                //determine flags used for entry position						


          bool openLongTrade = false;     //TODO: Complete long trade criteria.
          bool openShortTrade = false;    //TODO: Complete short trade criteria.


          //check whether we're using dynamic or static position sizing
          if (MMStartPosSize > 0 && MMProfitDelta > 0) {
            //dynamic position sizing, compute new trade size
            //compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
            double netProfit = MMStartNetProfit + NetProfit;
            netProfit = (netProfit > 0) ? netProfit : 0;    //net profit can not be negetive for square root calculation below
            int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));

            if (openLongTrade)
              m_LE_DMM.Send(Bars.High[0], numLots);
            if (openShortTrade)
              m_SE_DMM.Send(Bars.Low[0], numLots);
          } else {
            //static position sizing controlled by the user settings
            if (openLongTrade)
              m_LE_SMM.Send(Bars.High[0]);
            if (openShortTrade)
              m_SE_SMM.Send(Bars.Low[0]);
          }
        }
      }
    }
  }
}