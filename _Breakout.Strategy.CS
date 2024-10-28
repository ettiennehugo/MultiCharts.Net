using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;
using RiskManagement;

namespace PowerLanguage.Strategy {
  /// <summary>
  /// Chart Type: Candlestick
  /// 
  /// Hypothesis:
  /// Setting the strategy to trade a breakout around the price when key economic indicators are released. A number of time windows can be specified to support 
  /// multiple entries on a specific day.
  /// 
  /// Ideal Instruments and Timeframes:
  /// Any
  /// 
  /// Indicators: 
  /// None
  /// 
  /// Entry Rules - Long:
  /// A breakout occurs to the upside beyond the highest high of a given number of bars and a specified entry buffer pips.
  /// 
  /// Entry Rules - Short:
  /// A breakout occurs to the downside beyond the lowest low of a given number of bars and a specified entry buffer pips.
  /// 
  /// Exit Rules - Long:
  /// TBD
  /// 
  /// Exit Rules - Short:
  /// TBD
  /// 
  /// Money Management:
  /// 	Fixed ratio position sizing based on profits.
  /// 
  /// Notes:
  /// 	
  /// 
  /// </summary>
  [IOGMode(IOGMode.Enabled)]                                    //order generation must be IOG to ensure that orders are properly replaced when an order is filled
  public class _Breakout : SignalObject {
    //Inputs:
    [Input] public int BreakoutBars { get; set; }               //number of bars to test for a breakout
    [Input] public int BufferPips { get; set; }					        //buffer distance beyond the high/low at which an entry will be placed
    [Input] public int TrailingStopActivationPips { get; set; } //number of pips at which point the trailing stop order starts to follow the market
    [Input] public int TrailingStopPips { get; set; }           //number of pips to use to track market high/low for exit
    [Input] public int TFStartTime1 { get; set; }               //first time filter start time, e.g. 800 is 8am and 1350 is 1:30pm		
    [Input] public int TFWindowLength1 { get; set; }            //first time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int TFStartTime2 { get; set; }               //second time filter start time, e.g. 800 is 8am and 1350 is 1:30pm		
    [Input] public int TFWindowLength2 { get; set; }            //second time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int TFStartTime3 { get; set; }               //third time filter start time, e.g. 800 is 8am and 1350 is 1:30pm		
    [Input] public int TFWindowLength3 { get; set; }            //third time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int TFStartTime4 { get; set; }               //fourth time filter start time, e.g. 800 is 8am and 1350 is 1:30pm		
    [Input] public int TFWindowLength4 { get; set; }            //fourth time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int TFStartTime5 { get; set; }               //fifth time filter start time, e.g. 800 is 8am and 1350 is 1:30pm		
    [Input] public int TFWindowLength5 { get; set; }            //fifth time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int MMStartPosSize { get; set; }             //money management start position size (0 = disable MM), e.g. 10000 starts with one mini lot
    [Input] public double MMProfitDelta { get; set; }           //money management profit delta at which new positions are added, e.g. 500 adds a new position when 500-curreny unit profit is made
    [Input] public double MMPercentRiskPerTrade { get; set; }   //percentage allowable risk per trade
    [Input] public int MMStaticPositionSize { get; set; }       //default trade size for static position sizing
    [Input] public double MMStartNetProfit { get; set; }        //money management start profit if the strategy was shut down between runs
    [Input] public bool Debug { get; set; }                     //flag to enable debug output to the output console

    //Constants:
    const int DEFAULT_BREAKOUT_BARS = 10;
    const int DEFAULT_BUFFER_PIPS = 5;
    const int DEFAULT_TRAILING_STOP_ACTIVATION_PIPS = 10;
    const int DEFAULT_TRAILING_STOP_PIPS = 5;
    const int CROSS_LOOKBACK = 5;
    const int MINI_LOT_SIZE = 1000;                             //mini-lot size used in Forex

    //Attributes:		
    private IOrderPriced _LE;
    private IOrderPriced _SE;
    private IOrderPriced _LX_TSP;
    private IOrderPriced _SX_TSP;
    private IOrderPriced _LX_SL;
    private IOrderPriced _SX_SL;
    private TimeSpan _StartTime1;
    private TimeSpan _EndTime1;
    private TimeSpan _StartTime2;
    private TimeSpan _EndTime2;
    private TimeSpan _StartTime3;
    private TimeSpan _EndTime3;
    private TimeSpan _StartTime4;
    private TimeSpan _EndTime4;
    private TimeSpan _StartTime5;
    private TimeSpan _EndTime5;
    private bool _ScanForBreakout;
    private double _LongBreakPrice;
    private double _ShortBreakPrice;
    private bool _TrailingStopActive;
    private double _TrailingStopPrice;
    private double _PipValue;
    private double _Risk;
    private RiskManager _RiskManager;
    private int _Decimals;

    public _Breakout(object _ctx) : base(_ctx) {
      //initialize additional inputs for strategy
      BreakoutBars = DEFAULT_BREAKOUT_BARS;
      BufferPips = DEFAULT_BUFFER_PIPS;
      TrailingStopPips = DEFAULT_TRAILING_STOP_PIPS;
      TrailingStopActivationPips = DEFAULT_TRAILING_STOP_ACTIVATION_PIPS;

      //initialize default inputs for strategy
      TFStartTime1 = 0;                 //12:00am
      TFWindowLength1 = 1439;           //minutes in a full 24-hour trading day
      TFStartTime2 = -1;                //12:00am
      TFWindowLength2 = -1;             //minutes in a full 24-hour trading day
      TFStartTime3 = -1;                //12:00am
      TFWindowLength3 = -1;             //minutes in a full 24-hour trading day
      TFStartTime4 = -1;                //12:00am
      TFWindowLength4 = -1;             //minutes in a full 24-hour trading day
      TFStartTime5 = -1;                //12:00am
      TFWindowLength5 = -1;             //minutes in a full 24-hour trading day
      MMStartPosSize = 0;               //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;                //profit delta before another additional contract is added
      MMPercentRiskPerTrade = 0.01;     //percentage risk per trade to take
      MMStaticPositionSize = 100000;    //setup one lot size for static position sizing
      MMStartNetProfit = 0;             //start net profit between restarts of the strategy
      Debug = false;
    }

    protected override void Create() {
      //create the order types used
      _LE = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "LE", EOrderAction.Buy));
      _SE = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SE", EOrderAction.SellShort));
      _LX_TSP = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TSPLX", EOrderAction.Sell));            //trailing stop-orders
      _SX_TSP = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TSPSX", EOrderAction.BuyToCover));
      _LX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SLLX", EOrderAction.Sell));              //exit orders
      _SX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SLSX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc() {
      //clear the debugging console
      if (Debug)
        Output.Clear();

      //initialize the risk manager
      _RiskManager = new RiskManager(this);
      _RiskManager.RiskPercentage = MMPercentRiskPerTrade;

      if (MMStartPosSize > 0 && MMProfitDelta > 0) {
        FixedRatioSizing fixedRatioSizing = new FixedRatioSizing(this);
        fixedRatioSizing.StartNetProfit = MMStartPosSize;
        fixedRatioSizing.BasePositionSize = MMStartPosSize;
        fixedRatioSizing.ProfitDelta = MMProfitDelta;
        _RiskManager.PositionSizing = fixedRatioSizing;
      } else {
        StaticSizing staticSizing = new StaticSizing(this);
        staticSizing.PositionSize = MMStaticPositionSize;
        _RiskManager.PositionSizing = staticSizing;
      }

      //standarize the buffer pip value for the price scale we're trading
      _PipValue = Bars.Info.PointValue * 10 * Bars.Info.BigPointValue;

      //set the start/end times for the time filters
      timeFrameToTimeStartEnd(TFStartTime1, TFWindowLength1, ref _StartTime1, ref _EndTime1);
      timeFrameToTimeStartEnd(TFStartTime2, TFWindowLength2, ref _StartTime2, ref _EndTime2);
      timeFrameToTimeStartEnd(TFStartTime3, TFWindowLength3, ref _StartTime3, ref _EndTime3);
      timeFrameToTimeStartEnd(TFStartTime4, TFWindowLength4, ref _StartTime4, ref _EndTime4);
      timeFrameToTimeStartEnd(TFStartTime5, TFWindowLength5, ref _StartTime5, ref _EndTime5);

      //per default start computing breakout
      _ScanForBreakout = true;

      //HACK: To get the decimals correct for the currency price values sent to OandA.
      if (Bars.Info.Description == "USD/JPY")
        _Decimals = 3;
      else
        _Decimals = 5;
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

    private void calcBarEntry() {
      //apply time filter for entries
      //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
      //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
      DateTime currentTime = Bars.Time.Value;

      //reset strategy position state variables if breakout computation should
      if (_ScanForBreakout) {
        _LongBreakPrice = -1;
        _ShortBreakPrice = -1;
        _TrailingStopActive = false;
        _TrailingStopPrice = -1;
        _Risk = -1;
      }

      //apply time filters for entry
      if (timeFilterActive(currentTime, _StartTime1, _EndTime1) ||
          timeFilterActive(currentTime, _StartTime2, _EndTime2) ||
          timeFilterActive(currentTime, _StartTime3, _EndTime3) ||
          timeFilterActive(currentTime, _StartTime4, _EndTime4) ||
          timeFilterActive(currentTime, _StartTime5, _EndTime5)) {
        //NOTE: We need to compute the breakout only once when the time filter starts to fire and not continuously since we
        //      want to get a snapshot of the extremes as we approach the trading time window and then enter when these extremes
        //      are broken out of.
        if (_ScanForBreakout) {
          //compute the long/short price values to use for the breakout
          _LongBreakPrice = Bars.High.Highest(BreakoutBars) + (BufferPips * _PipValue);
          _ShortBreakPrice = Bars.Low.Lowest(BreakoutBars) - (BufferPips * _PipValue);
          //NOTE: Decimals MUST be correct otherwise the broker rejects the orders.
          _LongBreakPrice = Math.Round(_LongBreakPrice, _Decimals);
          _ShortBreakPrice = Math.Round(_ShortBreakPrice, _Decimals);

          //compute the projected risk that we will take
          _Risk = _LongBreakPrice - _ShortBreakPrice;

          //stop monitoring the breakout
          _ScanForBreakout = false;
        }

        //enter trade if signal was generated and risk is allowed
        RiskAnalysis riskAnalysis = _RiskManager.Analyze(_Risk);
        if (riskAnalysis.Allowed) {
          _LE.Send(_LongBreakPrice, riskAnalysis.Size);
          _SE.Send(_ShortBreakPrice, riskAnalysis.Size);

          if (Debug)
            Output.WriteLine("{0} Waiting for breakout {1}: LongBreakPrice({2}) ShortBreakPrice({3}) Risk({4})", Bars.Info.Description, Bars.Time[0], _LongBreakPrice, _ShortBreakPrice, _Risk);

        } else if (Debug)
          Output.WriteLine("{0} Risk not allowed {1}: LongBreakPrice({2}) ShortBreakPrice({3}) Risk({4})", Bars.Info.Description, Bars.Time[0], _LongBreakPrice, _ShortBreakPrice, _Risk);

      } else
        //time filter no longer holding, compute breakout next time the time filter becomes valid
        _ScanForBreakout = true;
    }

    private void calcBarExit() {
      //compute the breakout again next time we go flat
      _ScanForBreakout = true;

      //manage long position
      if (PositionSide > 0) {
        //compute the actual risk for the position based on the actual entry price
        _Risk = StrategyInfo.AvgEntryPrice - _ShortBreakPrice;

        //send stop-loss order
        _LX_SL.Send(_ShortBreakPrice);

        //determine activation state for trailing stop if not yet switched on, it gets switched on as soon as the difference
        //between the current high and the entry price exceeds the trailing stop activation pips
        if (!_TrailingStopActive)
          _TrailingStopActive = (TrailingStopActivationPips * _PipValue) < (Bars.High[0] - StrategyInfo.AvgEntryPrice);

        //send trailing stop order if trailing stop activated
        if (_TrailingStopActive) {
          double currentTrailPrice = Bars.High[0] - (TrailingStopPips * _PipValue);
          currentTrailPrice = Math.Round(currentTrailPrice, _Decimals);

          _TrailingStopPrice = (_TrailingStopPrice == -1) || (currentTrailPrice > _TrailingStopPrice) ? currentTrailPrice : _TrailingStopPrice;
          _TrailingStopPrice = Math.Round(_TrailingStopPrice, _Decimals);

          _LX_TSP.Send(_TrailingStopPrice);
        }

        if (Debug)
          Output.WriteLine("{0} Long {1}: EntryPrice({2}) StopLoss({3}) TrailingStop({4}) ActualRisk({5}))", Bars.Info.Description, Bars.Time[0], StrategyInfo.AvgEntryPrice, _ShortBreakPrice, _TrailingStopPrice, _Risk);
      }

      //manage short position
      if (PositionSide < 0) {
        //compute the actual risk for the position based on the actual entry price
        _Risk = _LongBreakPrice - StrategyInfo.AvgEntryPrice;

        //send the stop-loss order
        _SX_SL.Send(_LongBreakPrice);

        //determine activation state for trailing stop if not yet switched on, it gets switched on as soon as the difference
        //between the current low and the entry price exceeds the trailing stop activation pips
        if (!_TrailingStopActive)
          _TrailingStopActive = (TrailingStopActivationPips * _PipValue) < (StrategyInfo.AvgEntryPrice - Bars.Low[0]);

        //send trailing stop order if trailing stop activated
        if (_TrailingStopActive) {
          double currentTrailPrice = Bars.Low[0] + (TrailingStopPips * _PipValue);
          currentTrailPrice = Math.Round(currentTrailPrice, _Decimals);

          _TrailingStopPrice = (_TrailingStopPrice == -1) || (currentTrailPrice < _TrailingStopPrice) ? currentTrailPrice : _TrailingStopPrice;
          _TrailingStopPrice = Math.Round(_TrailingStopPrice, _Decimals);

          _SX_TSP.Send(_TrailingStopPrice);
        }

        if (Debug)
          Output.WriteLine("{0} Short {1}: EntryPrice({2}) StopLoss({3}) TrailingStop({4}) ActualRisk({5})", Bars.Info.Description, Bars.Time[0], StrategyInfo.AvgEntryPrice, _LongBreakPrice, _TrailingStopPrice, _Risk);
      }
    }

    //===================================================================================================
    //===================================================================================================
    //===================================================================================================
    //Standard startegy methods that do not require modification.
    //===================================================================================================
    //===================================================================================================
    //===================================================================================================
    private void timeFrameToTimeStartEnd(int tfStartTime, int tfWindowLength, ref TimeSpan startTime, ref TimeSpan endTime) {
      //set time spans only if valid time frame set
      if (tfStartTime >= 0 && tfWindowLength > 0) {
        //set the start/end times for the time filter
        startTime = new TimeSpan(tfStartTime / 100, tfStartTime % 100, 0);
        endTime = startTime + new TimeSpan(tfWindowLength / 60, tfWindowLength % 60, 0);

        //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
        //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
        if (endTime.Days > 0)
          endTime = endTime.Subtract(new TimeSpan(endTime.Days, 0, 0, 0));
      } else {
        startTime = TimeSpan.MinValue;
        endTime = TimeSpan.MinValue;
      }
    }

    private bool timeFilterActive(DateTime currentTime, TimeSpan startTime, TimeSpan endTime) {
      bool result = false;

      if (startTime != TimeSpan.MinValue && endTime != TimeSpan.MinValue)
        result = (((startTime < endTime) && (startTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= endTime)) ||    //start/end time fall in the same trading day
                  ((startTime > endTime) && ((startTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= endTime))));   //start/end time span over different trading days
                                                                                                                              //determine whether we should enter a position

      return result;
    }

    private void orderFilled() {
      //send initial stop-loss when an entry order is filled
      if (PositionSide > 0) {
        //send initial stop-loss order
        _LX_SL.Send(_ShortBreakPrice);

        //compute the actual risk for the position based on the actual entry price
        _Risk = StrategyInfo.AvgEntryPrice - _ShortBreakPrice;

        if (Debug)
          Output.WriteLine("{0} Long position breakout {1}: EntryPrice({2}) StopLoss({3}) ActualRisk({4})", Bars.Info.Description, Bars.Time[0], _LongBreakPrice, _ShortBreakPrice, _Risk);        
      }

      if (PositionSide < 0) {
        //send initial stop-loss order
        _SX_SL.Send(_LongBreakPrice);

        //compute the actual risk for the position based on the actual entry price
        _Risk = _LongBreakPrice - StrategyInfo.AvgEntryPrice;

        if (Debug)
          Output.WriteLine("{0} Short position breakout {1}: EntryPrice({2}) StopLoss({3}) ActualRisk({4})", Bars.Info.Description, Bars.Time[0], _ShortBreakPrice, _LongBreakPrice, _Risk);
      }
    }

    protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price) {
      orderFilled();
    }

    private void CalcBarOrderFilled() {
      orderFilled();
    }
  }
}
