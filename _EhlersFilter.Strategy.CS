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
  /// Ehlers filter is less responsive to noise and is supposed to work better than a moving average.
  /// 
  /// Ideal Instruments and Timeframes:
  /// Forex 30min and longer
  /// 
  /// Indicators: 
  /// Ehlers fast and slow
  /// 
  /// Entry Rules - Long:
  /// Ehlers fast crosses over Ehlers slow.
  /// 
  /// Entry Rules - Short:
  /// Ehlers fast crosses under Ehlers slow.
  /// 
  /// Exit Rules - Long:
  /// Ehlers fast crosses under Ehlers slow.
  /// 
  /// Exit Rules - Short:
  /// Ehlers fast crosses over Ehlers slow.
  /// 
  /// Money Management:
  /// 	Fixed ratio position sizing based on profits.
  /// 
  /// Notes:
  /// 	
  /// 
  /// </summary>
  public class _EhlersFilter : SignalObject {
    //Inputs:
    [Input] public int FastLength { get; set; }         //Fast Ehlers length
    [Input] public int SlowLength { get; set; }					//Slow Ehlers length
    [Input] public int TFStartTime1 { get; set; }               //first time filter start time, e.g. 800 is 8am and 1350 is 1:30pm		
    [Input] public int TFWindowLength1 { get; set; }            //first time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int TFStartTime2 { get; set; }               //second time filter start time, e.g. 800 is 8am and 1350 is 1:30pm		
    [Input] public int TFWindowLength2 { get; set; }            //second time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int TFStartTime3 { get; set; }               //third time filter start time, e.g. 800 is 8am and 1350 is 1:30pm		
    [Input] public int TFWindowLength3 { get; set; }            //third time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int MMStartPosSize { get; set; }             //money management start position size (0 = disable MM), e.g. 10000 starts with one mini lot
    [Input] public double MMProfitDelta { get; set; }           //money management profit delta at which new positions are added, e.g. 500 adds a new position when 500-curreny unit profit is made
    [Input] public int MMStopLossPivotLayer { get; set; }       //Layer 1-3 to use for the pivot stop-loss
    [Input] public double MMPercentRiskPerTrade { get; set; }   //percentage allowable risk per trade
    [Input] public int MMStaticPositionSize { get; set; }       //default trade size for static position sizing
    [Input] public double MMStartNetProfit { get; set; }        //money management start profit if the strategy was shut down between runs
    [Input] public bool EnableLong { get; set; }                //enable long trades
    [Input] public bool EnableShort { get; set; }               //enable short trades
    [Input] public bool Debug { get; set; }                     //flag to enable debug output to the output console

    //Constants:
    const int DEFAULT_FAST_LENGTH = 10;
    const int DEFAULT_SLOW_LENGTH = 30;
    const int CROSS_LOOKBACK = 5;
    const int MINI_LOT_SIZE = 1000;                             //mini-lot size used in Forex

    //Attributes:		
    private IOrderMarket _LE;
    private IOrderMarket _SE;
    private IOrderMarket _LX;
    private IOrderMarket _SX;
    private EhlersFilter _fastFilterFC;
    private EhlersFilter _slowFilterFC;
    private VariableSeries<double> _fastFilter;
    private VariableSeries<double> _slowFilter;
    private TimeSpan _StartTime1;
    private TimeSpan _EndTime1;
    private TimeSpan _StartTime2;
    private TimeSpan _EndTime2;
    private TimeSpan _StartTime3;
    private TimeSpan _EndTime3;
    private double _PipValue;
    private RiskManager _RiskManager;

    public _EhlersFilter(object _ctx) : base(_ctx) {
      //initialize additional inputs for strategy
      FastLength = DEFAULT_FAST_LENGTH;
      SlowLength = DEFAULT_SLOW_LENGTH;

      //initialize default inputs for strategy
      TFStartTime1 = 0;                 //12:00am
      TFWindowLength1 = 1439;           //minutes in a full 24-hour trading day
      TFStartTime2 = -1;                //12:00am
      TFWindowLength2 = -1;             //minutes in a full 24-hour trading day
      TFStartTime3 = -1;                //12:00am
      TFWindowLength3 = -1;             //minutes in a full 24-hour trading day
      MMStartPosSize = 0;               //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;                //profit delta before another additional contract is added
      MMPercentRiskPerTrade = 0.01;     //percentage risk per trade to take
      MMStaticPositionSize = 100000;    //setup one lot size for static position sizing
      MMStartNetProfit = 0;             //start net profit between restarts of the strategy
      EnableLong = true;                //per default enable long trades
      EnableShort = true; 		          //per default enable short trades		
      Debug = false;
    }

    protected override void Create() {
      //create indicators used
      _fastFilterFC = new EhlersFilter(this);
      _slowFilterFC = new EhlersFilter(this);
      _fastFilter = new VariableSeries<double>(this);
      _slowFilter = new VariableSeries<double>(this);

      //create the order types used
      _LE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "LE", EOrderAction.Buy));
      _SE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "SE", EOrderAction.SellShort));
      _LX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "LX", EOrderAction.Sell));              //exit orders
      _SX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "SX", EOrderAction.BuyToCover));
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

      //initialize indicators and objects used
      _fastFilterFC.Instrument = Bars;
      _fastFilterFC.Length = FastLength;
      _slowFilterFC.Instrument = Bars;
      _slowFilterFC.Length = SlowLength;

      //standarize the buffer pip value for the price scale we're trading
      _PipValue = Bars.Info.PointValue * 10 * Bars.Info.BigPointValue;

      //set the start/end times for the time filters
      timeFrameToTimeStartEnd(TFStartTime1, TFWindowLength1, ref _StartTime1, ref _EndTime1);
      timeFrameToTimeStartEnd(TFStartTime2, TFWindowLength2, ref _StartTime2, ref _EndTime2);
      timeFrameToTimeStartEnd(TFStartTime3, TFWindowLength3, ref _StartTime3, ref _EndTime3);
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

    private void CalcBarDefault() {
      //process signal entries and adjust stop-losses
      //NOTE: We only update the signals when the bar is closed but we process the entry/exit signals
      //      so that intrabar order generation can be used to get exact back testing on smaller timeframe
      //      bars during back testing.
      if (Bars.Status == EBarState.Close) {
        _fastFilter.Value = _fastFilterFC.Value;
        _slowFilter.Value = _slowFilterFC.Value;
      }

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

      //apply time filters for entry
      if (timeFilterActive(currentTime, _StartTime1, _EndTime1) ||
          timeFilterActive(currentTime, _StartTime2, _EndTime2) ||
          timeFilterActive(currentTime, _StartTime3, _EndTime3)) {                                                                                                                          //determine whether we should enter a position
        bool enterLong = false;
        bool enterShort = false;

        //determine whether we can enter a long position if enabled
        if (EnableLong)
          enterLong = _fastFilter.CrossesOver(_slowFilter, CROSS_LOOKBACK);

        //determine whether we can enter a short position if enabled
        if (EnableShort)
          enterShort = _fastFilter.CrossesUnder(_slowFilter, CROSS_LOOKBACK);

        //enter trade if signal was generated and risk is allowed
        if (enterLong) {
          RiskAnalysis riskAnalysis = _RiskManager.Analyze();
          if (riskAnalysis.Allowed)
            _LE.Send(riskAnalysis.Size);
        }

        if (enterShort) {
          RiskAnalysis riskAnalysis = _RiskManager.Analyze();
          if (riskAnalysis.Allowed)
            _SE.Send(riskAnalysis.Size);
        }
      }
    }

    private void calcBarExit() {
      //manage long position
      if (PositionSide > 0) {
        if (_fastFilter.CrossesUnder(_slowFilter, CROSS_LOOKBACK))
          _LX.Send();
      }

      //manage short position
      if (PositionSide < 0) {
        if (_fastFilter.CrossesOver(_slowFilter, CROSS_LOOKBACK))
          _SX.Send();
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
      ////send initial stop-loss when an entry order is filled
      //if (PositionSide > 0)
      //  _LX_SL.Send(_SLPrice);

      //if (PositionSide < 0)
      //  _SX_SL.Send(_SLPrice);
    }

    protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price) {
      orderFilled();
    }

    private void CalcBarOrderFilled() {
      orderFilled();
    }
  }
}
