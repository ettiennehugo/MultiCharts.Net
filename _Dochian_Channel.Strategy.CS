using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Chart Type: Candlestick
  /// 
  /// Hypothesis: 
  ///   Uses a channel breakout to enter trades into the market.
  /// 
  /// Ideal Instruments and Timeframes:
  /// 
  /// 	TBD
  /// 
  /// Entry Rules - Long:
  /// 
  /// 
  /// Entry Rules - Short:
  /// 
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
  public class _Dochian_Channel : SignalObject
  {
    //Inputs:
    [Input] public int LowLengthEntry { get; set; }
    [Input] public int HighLengthEntry { get; set; }
    [Input] public int LowLengthExit { get; set; }
    [Input] public int HighLengthExit { get; set; }
    [Input] public int TFStartTime { get; set; }                //time filter start time, e.g. 800 is 8am and 1300 is 1pm		
    [Input] public int TFWindowLength { get; set; }             //time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int MMStartPosSize { get; set; }             //money management start position size (0 = disable MM), e.g. 10000 starts with one mini lot
    [Input] public double MMProfitDelta { get; set; }           //money management profit delta at which new positions are added, e.g. 500 adds a new position when 500-curreny unit profit is made
    [Input] public double MMStartNetProfit { get; set; }        //money management start profit if the strategy was shut down between runs
    [Input] public double StopLossPct { get; set; }             //stop-loss percentage (0 = disable)
    [Input] public double TrailingStopPct { get; set; }         //trailing-stop percentage (0 = disable)
    [Input] public bool EnableLong { get; set; }                //enable long trades
    [Input] public bool EnableShort { get; set; }       //enable short trades
    [Input] public bool Debug { get; set; }                     //flag to enable debug output to the output console

    //Attributes:
    private IOrderMarket _LE_DMM;
    private IOrderMarket _SE_DMM;
    private IOrderMarket _LE_SMM;
    private IOrderMarket _SE_SMM;
    private IOrderPriced _LX;
    private IOrderPriced _SX;
    private double _lowestEntry;
    private double _highestEntry;
    private double _lowestExit;
    private double _highestExit;
    private TimeSpan _StartTime;
    private TimeSpan _EndTime;

    public _Dochian_Channel(object _ctx) : base(_ctx)
    {
      LowLengthEntry = 20;
      HighLengthEntry = 20;
      LowLengthExit = 20;
      HighLengthExit = 20;
      _lowestEntry = 0;
      _highestEntry = 0;
      TFStartTime = 0;        //12:00am
      TFWindowLength = 1439;  //minutes in a full 24-hour trading day
      MMStartPosSize = 0;     //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;      //profit delta before another additional contract is added
      MMStartNetProfit = 0;   //start net profit between restarts of the strategy
      StopLossPct = 0;        //stop loss (0 = disable)
      TrailingStopPct = 0;    //trailing stop (0 = disable)
      EnableLong = true;      //per default enable long trades
      EnableShort = true;   //per default enable short trades		
      Debug = false;
    }

    protected override void Create()
    {
      //create the orders used for strategy
      _LE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "DCDLE", EOrderAction.Buy));         //dynamic money management orders
      _SE_DMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "DCDSE", EOrderAction.SellShort));
      _LE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "DCSLE", EOrderAction.Buy));               //static money management orders
      _SE_SMM = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "DCSSE", EOrderAction.SellShort));
      _LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "DCLX", EOrderAction.Sell));                    //exit orders
      _SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "DCSX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc()
    {
      //set the start/end times for the time filter
      _StartTime = new TimeSpan(TFStartTime / 100, TFStartTime % 100, 0);
      _EndTime = _StartTime + new TimeSpan(TFWindowLength / 60, TFWindowLength % 60, 0);

      //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
      //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
      if (_EndTime.Days > 0) _EndTime = _EndTime.Subtract(new TimeSpan(_EndTime.Days, 0, 0, 0));
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
      orderFilled();
    }

    private void CalcBarDefault()
    {
      //handle entry based on previous values
      if (_highestEntry != 0 && _lowestEntry != 0)
      {
        if (PositionSide == 0)
        {
          //apply time filter for entries
          //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
          //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
          DateTime currentTime = Bars.Time.Value;

          if (((_StartTime < _EndTime) && (_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= _EndTime)) ||    //start/end time fall in the same trading day
              ((_StartTime > _EndTime) && ((_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= _EndTime))))
          {  //start/end time span over different trading days
             //determine entry signals	
            bool enterLong = false;
            bool enterShort = false;

            if (EnableLong) enterLong = _highestEntry < Bars.Close[0];
            if (EnableShort) enterShort = _lowestEntry > Bars.Close[0];

            //check whether we're using dynamic or static position sizing
            if (MMStartPosSize > 0 && MMProfitDelta > 0)
            {
              //dynamic position sizing, compute new trade size
              //compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
              double netProfit = MMStartNetProfit + NetProfit;
              netProfit = (netProfit > 0) ? netProfit : 0;        //net profit can not be negetive for square root calculation below
              int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));

              if (enterLong) _LE_DMM.Send(numLots);
              if (enterShort) _SE_DMM.Send(numLots);
            }
            else
            {
              //static position sizing controlled by the user settings
              if (enterLong) _LE_SMM.Send();
              if (enterShort) _SE_SMM.Send();
            }
          }
        }
        else
        {
          //stop-loss/trailing-stop exit
          CurSpecOrdersMode = ESpecOrdersMode.PerContract;
          if (StopLossPct != 0)
            GenerateStopLoss(this.EntryPrice() * (StopLossPct / 100));

          if (TrailingStopPct != 0)
            GeneratePercentTrailing(this.EntryPrice() * (TrailingStopPct / 100), TrailingStopPct);

          if (PositionSide > 0)
            _LX.Send(_lowestExit);
          if (PositionSide < 0)
            _SX.Send(_highestExit);
        }
      }

      //update Dochian Channel values
      _lowestEntry = Bars.Low.Lowest(LowLengthEntry);
      _highestEntry = Bars.High.Highest(HighLengthEntry);
      _lowestExit = Bars.Low.Lowest(LowLengthExit);
      _highestExit = Bars.High.Highest(HighLengthExit);
    }

    private void CalcBarOrderFilled()
    {
      orderFilled();
    }

    private void orderFilled()
    {
      //send initial stop-loss when an entry order is filled
      if (PositionSide > 0) _LX.Send(_lowestExit);
      if (PositionSide < 0) _SX.Send(_highestExit);
    }



  }
}