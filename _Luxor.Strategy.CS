using System;
using System.Globalization;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {

  //NOTE:
  // * When adding the time filter you'll have to look at the IInstrumentSettings Info object TimeZone attribute
  //   to determine in which time zone the price data is coming from, it can be: Local, Exchange or GMT. See p.21
  //   of the development guide.		

  //
  //Description:
  // Basic moving average crossover strategy with the addition of a breakout filter.
  //
  //Entry Rules:
  //Long:
  // 1. Local time is between StartTime and StartTime + WindowLength.
  // 2. Fast MA crosses over slow MA.
  // 3. Price breaks out above the high of the bar where the fast/slow cross over is first detected.
  //
  //Short:
  // 1. Local time is between StartTime and StartTime + WindowLength.
  // 2. Fast MA crosses under slow MA.
  // 3. Price breaks out below the low of the bar where the fast/slow cross under is first detected.
  //
  //Exit Rules:
  // 1. Closes a previous long/short position as soon as a cross over/cross under is detected.
  //
  //Money Management:
  // * Fixed ratio position sizing based around the starting position and profit delta's to add additional
  //   positions to the trade size.
  //

  public class _Luxor : SignalObject {

    //Inputs:
    [Input] public int FastLength { get; set; }
    [Input] public int SlowLength { get; set; }
    [Input] public int StartTime { get; set; }
    [Input] public int WindowLength { get; set; }
    [Input] public double StopLossPct { get; set; }
    [Input] public double TrailingStopPct { get; set; }
    [Input] public int MMStartPosSize { get; set; }
    [Input] public double MMProfitDelta { get; set; }
    [Input] public double MMStartNetProfit { get; set; }    //Start net profit when strategy is restarted between system reboots.

    //Attributes:
    private AverageFC m_FastAvgFC;
    private AverageFC m_SlowAvgFC;
    private VariableSeries<Double> m_FastAvg;
    private VariableSeries<Double> m_SlowAvg;
    private IOrderPriced m_LE_DMM;
    private IOrderPriced m_SE_DMM;
    private IOrderPriced m_LE_SMM;
    private IOrderPriced m_SE_SMM;
    private IOrderMarket m_LX;
    private IOrderMarket m_SX;
    private TimeSpan m_StartTime;
    private TimeSpan m_EndTime;

    public _Luxor(object _ctx) : base(_ctx) {
      FastLength = 10;
      SlowLength = 30;
      StartTime = 700;        //7-AM starting time for trading session       
      WindowLength = 540;     //minutes for a full 9-hour trading day
      StopLossPct = 1.0;      //1% stop loss per default
      TrailingStopPct = 2.0;  //2% trailing stop per default
      MMStartPosSize = 0;   //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;    //profit delta before another additional contract is added (0 - disable MM)
    }

    protected override void Create() {
      //create the indicators for signal
      m_FastAvgFC = new AverageFC(this);
      m_SlowAvgFC = new AverageFC(this);
      m_FastAvg = new VariableSeries<Double>(this);
      m_SlowAvg = new VariableSeries<Double>(this);

      //create the orders for signal
      m_LE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "LxrLE", EOrderAction.Buy));     //dynamic money management orders
      m_SE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "LxrSE", EOrderAction.SellShort));
      m_LE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LxrLE", EOrderAction.Buy));       //static money management orders
      m_SE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LxrSE", EOrderAction.SellShort));
      m_LX = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LxrLX", EOrderAction.Sell));     //exit orders
      m_SX = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LxrSX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc() {
      //set the start/end times for the time filter
      m_StartTime = new TimeSpan(StartTime / 100, StartTime % 100, 0);
      m_EndTime = m_StartTime + new TimeSpan(WindowLength / 60, WindowLength % 60, 0);

      //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
      //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
      if (m_EndTime.Days > 0)
        m_EndTime = m_EndTime.Subtract(new TimeSpan(m_EndTime.Days, 0, 0, 0));

      //update the moving averages
      m_FastAvgFC.price = Bars.Close;
      m_FastAvgFC.length = FastLength;
      m_SlowAvgFC.price = Bars.Close;
      m_SlowAvgFC.length = SlowLength;
    }

    protected override void CalcBar() {
      //update moving averages
      m_FastAvg.Value = m_FastAvgFC[0];
      m_SlowAvg.Value = m_SlowAvgFC[0];

      //apply rules only when enough data is available
      if (Bars.CurrentBar >= SlowLength) {
        //stop-loss/trailing-stop exit
        if (CurrentPosition.Side != EMarketPositionSide.Flat) {
          CurSpecOrdersMode = ESpecOrdersMode.PerContract;
          if (StopLossPct != 0)
            GenerateStopLoss(this.EntryPrice() * (StopLossPct / 100));
          if (TrailingStopPct != 0)
            GeneratePercentTrailing(this.EntryPrice() * (TrailingStopPct / 100), TrailingStopPct);
        }

        //apply time filter to entry/exit
        //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
        //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
        DateTime currentTime = Bars.Time.Value;

        if (((m_StartTime < m_EndTime) && (m_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= m_EndTime)) ||    //start/end time fall in the same trading day
            ((m_StartTime > m_EndTime) && ((m_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= m_EndTime)))) {  //start/end time span over different trading days	
                                                                                                                                //compute entry/exit criteria
          bool crossesOver = m_FastAvg.CrossesOver(m_SlowAvg, ExecInfo.MaxBarsBack);
          bool crossesUnder = m_FastAvg.CrossesUnder(m_SlowAvg, ExecInfo.MaxBarsBack);

          if (CurrentPosition.Side != EMarketPositionSide.Flat) {
            //general exit criteria
            if (crossesUnder || crossesOver) {
              m_LX.Send();
              m_SX.Send();
            }
          } else {
            //entry rules
            if (crossesOver || crossesUnder) {
              if (MMStartPosSize > 0 && MMProfitDelta > 0) {
                //dynamic position sizeing
                //compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
                double netProfit = MMStartNetProfit + NetProfit;
                netProfit = (netProfit > 0) ? netProfit : 0;    //net profit can not be negetive for square root calculation below
                int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));

                if (crossesOver)
                  m_LE_DMM.Send(Bars.High[0], numLots);
                if (crossesUnder)
                  m_SE_DMM.Send(Bars.Low[0], numLots);
              } else {
                if (crossesOver)
                  m_LE_SMM.Send(Bars.High[0]);
                if (crossesUnder)
                  m_SE_SMM.Send(Bars.Low[0]);
              }
            }
          }
        }
      }
    }
  }
}