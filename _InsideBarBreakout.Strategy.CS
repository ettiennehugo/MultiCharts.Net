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
  /// Scalping of a breakout of one or more inside bars. Risk is the height of the largest bar making out the inside bar combination with
  /// some buffer pip-value.
  /// 
  /// Ideal Instruments and Timeframes:
  /// Forex
  /// 
  /// Entry Rules - Long:
  /// 1. x-number of inside bars detected.
  /// 2. Price moves beyond the high of the largest of the inside bars within a specified number of bars. 
  /// 
  /// Entry Rules - Short:
  /// 1. x-number of inside bars detected.
  /// 2. Price moves beyond the low of the largest of the inside bars within a specified number of bars. 
  /// 
  /// Exit Rules - Long:
  /// * Moves the stop-loss along pivot low points on the price action.
  /// 
  /// Exit Rules - Short:
  /// * Moves the stop-loss along pivot high points on the price action.
  /// 
  /// Money Management:
  /// 	Fixed ratio position sizing based on profits.
  /// 
  /// Notes:
  /// 
  ///  TODO: Does not look very profitable with a very low success rate might require additional confirmation signal like checking a 
	/// moving average on the higher timeframe to filter signals.
  /// 
  /// 
  /// </summary>
  public class _InsideBarBreakout : SignalObject {
    //Inputs:
    [Input] public int InsideBarCount { get; set; }             //number of inside bars to detect before looking for breakout
    [Input] public int BarsToWait { get; set; }                 //number of bars to wait after inside bar(s) detected for breakout
    [Input] public int StopLossPivotLayer { get; set; }         //pivot layer to use low/high's for stop-loss
    [Input] public int TFStartTime { get; set; }                //time filter start time, e.g. 800 is 8am and 1300 is 1pm		
    [Input] public int TFWindowLength { get; set; }             //time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public int MMStartPosSize { get; set; }             //money management start position size (0 = disable MM), e.g. 10000 starts with one mini lot
    [Input] public double MMProfitDelta { get; set; }           //money management profit delta at which new positions are added, e.g. 500 adds a new position when 500-curreny unit profit is made
    [Input] public double MMStartNetProfit { get; set; }        //money management start profit if the strategy was shut down between runs
    [Input] public bool Debug { get; set; }                     //flag to enable debug output to the output console

    //Constants:
    const int BUFFER_PIP_VALUE = 1;               //buffer pip-value to add/substract from entries/exits		
    const int FRACTAL_LAYERS = 3;                 //number of fractal layers to analyze for price action

    //Attributes:
    private PriceActionAnalysis _priceActionAnalysis;
    private IOrderPriced _LE_DMM;
    private IOrderPriced _SE_DMM;
    private IOrderPriced _LE_SMM;
    private IOrderPriced _SE_SMM;
    private IOrderPriced _LX_SL;
    private IOrderPriced _SX_SL;
    private TimeSpan _StartTime;
    private TimeSpan _EndTime;
    private double _BufferPipValue;
    private double _SLPrice;
    private double _iiBarsLow;                    //low of the consecutive inside bars
    private double _iiBarsHigh;                   //high of the consecutive inside bars
    private double _iiLowEntry;                   //low of consecutive inside bars with buffer pip values
    private double _iiHighEntry;                  //high of consecutive inside bars with buffer pip value
    private double _initialRisk;                  //initial risk for entry
    private DateTime _firstInsideBar;             //timestamp of the first inside bar

    public _InsideBarBreakout(object _ctx) : base(_ctx) {
      //initialize additional inputs for strategy
      InsideBarCount = 2;
      BarsToWait = 1;
      StopLossPivotLayer = 2;

      //initialize default inputs for strategy
      TFStartTime = 0;        //12:00am
      TFWindowLength = 1439;  //minutes in a full 24-hour trading day
      MMStartPosSize = 0;     //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;      //profit delta before another additional contract is added
      MMStartNetProfit = 0;   //start net profit between restarts of the strategy
      Debug = false;
    }

    protected override void Create() {
      //create the order types used
      _LE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "DLE", EOrderAction.Buy));         //dynamic money management orders
      _SE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "DSE", EOrderAction.SellShort));
      _LE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SLE", EOrderAction.Buy));               //static money management orders
      _SE_SMM = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SSE", EOrderAction.SellShort));
      _LX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SLLX", EOrderAction.Sell));              //exit orders
      _SX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SLSX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc() {
      //clear the debugging console
      if (Debug)
        Output.Clear();

      //initialize indicators and objects used
      _priceActionAnalysis = new PriceActionAnalysis(FRACTAL_LAYERS);

      //standarize the buffer pip value for the price scale we're trading
      //NOTE: We multiple by 10 since the point value is always a tenth of a PIP.
      _BufferPipValue = BUFFER_PIP_VALUE * Bars.Info.PointValue * 10 * Bars.Info.BigPointValue;

      //set the start/end times for the time filter
      _StartTime = new TimeSpan(TFStartTime / 100, TFStartTime % 100, 0);
      _EndTime = _StartTime + new TimeSpan(TFWindowLength / 60, TFWindowLength % 60, 0);

      //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
      //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
      if (_EndTime.Days > 0)
        _EndTime = _EndTime.Subtract(new TimeSpan(_EndTime.Days, 0, 0, 0));
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
      //update the price action analysis if we have enough bars to analyse
      if (Bars.Status == EBarState.Close)
        _priceActionAnalysis.Analyse(Bars.Time[0], Bars.CurrentBar, Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);

      //check market position and process bar accordingly
      if (_priceActionAnalysis.Bars.Count > 0 && PositionSide == 0) {
        //apply time filter for entries
        //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
        //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
        DateTime currentTime = Bars.Time.Value;

        if (((_StartTime < _EndTime) && (_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= _EndTime)) ||    //start/end time fall in the same trading day
            ((_StartTime > _EndTime) && ((_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= _EndTime)))) {  //start/end time span over different trading days
          //analyze the price action bars to determine whether a setup occurred
          bool setupDetected = false;
          int barsToAnalyze = InsideBarCount + BarsToWait;

          //reset low/high inside bar state used for breakout
          _iiBarsHigh = -1;
          _iiBarsLow = -1;
          _iiLowEntry = -1;
          _iiHighEntry = -1;
          _initialRisk = -1;
          _SLPrice = -1;

          if (Bars.CurrentBar >= barsToAnalyze && _priceActionAnalysis.Bars.Count >= barsToAnalyze) {
            int consecutiveInsideBars = 0;
            for (int i = barsToAnalyze - 1; i >= 0; i--) {
              PriceActionBar bar = _priceActionAnalysis.Bars[Bars.Time[i]];
              if (bar.state.inside) {
                //increase the consecutive inside bars and compute low/high values
                consecutiveInsideBars++;
                _iiBarsLow = _iiBarsLow == -1 || _iiBarsLow > bar.low ? bar.low : _iiBarsLow;
                _iiBarsHigh = _iiBarsHigh == -1 || _iiBarsHigh < bar.high ? bar.high : _iiBarsHigh;
                _iiLowEntry = _iiBarsLow - _BufferPipValue;
                _iiHighEntry = _iiBarsHigh + _BufferPipValue;
                _initialRisk = _iiHighEntry - _iiLowEntry;

                //record timestamp of the first inside bar detected for stop-loss management
                if (consecutiveInsideBars == 1)
                  _firstInsideBar = bar.timestamp;

              } else {
                //reset consecutive inside bar state and low/high values for breakout
                consecutiveInsideBars = 0;
                _iiBarsHigh = -1;
                _iiBarsLow = -1;
                _iiLowEntry = -1;
                _iiHighEntry = -1;
                _initialRisk = -1;
              }

              if (consecutiveInsideBars == InsideBarCount) {
                setupDetected = true;
                break;  //break out of the inside bar search loop
              }
            }
          }

          //check whether we're using dynamic or static position sizing
          if (MMStartPosSize > 0 && MMProfitDelta > 0) {
            //dynamic position sizing, compute new trade size
            //compute the number of lots to trade using fixed ratio position sizing taking into account the starting net profit
            double netProfit = MMStartNetProfit + NetProfit;
            netProfit = (netProfit > 0) ? netProfit : 0;        //net profit can not be negetive for square root calculation below
            int numLots = (int)(MMStartPosSize * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / MMProfitDelta))))));

            if (setupDetected) {
              _LE_DMM.Send(_iiHighEntry, numLots);
              _SE_DMM.Send(_iiLowEntry, numLots);
            }
          } else {
            //static position sizing controlled by the user settings
            if (setupDetected) {
              _LE_SMM.Send(_iiHighEntry);
              _SE_SMM.Send(_iiLowEntry);
            }
          }
        }
      } else if (PositionSide > 0) {
        //adjust exit for long position
        //setup the stop-loss if not yet set from entry
        if (_SLPrice == -1)
          _SLPrice = _iiLowEntry;

        //try to find a pivot bar on the price action with a higher low
        //price than the current stop-loss
        double slPrice = _iiLowEntry;
        PriceActionBar bar = null;
        int i = 0;
        DateTime timestamp = Bars.Time[i];
        while (timestamp > _firstInsideBar && i < ExecInfo.MaxBarsBack) {
          bar = _priceActionAnalysis.Bars[timestamp];
          if (bar.state.pivotState[StopLossPivotLayer - 1].pivotLow && bar.low > slPrice)
            slPrice = bar.low;
          i++;
          timestamp = Bars.Time[i];
        }

        _SLPrice = _SLPrice < slPrice ? slPrice : _SLPrice;

        //send stop-loss for position
        _LX_SL.Send(_SLPrice);
      } else if (PositionSide < 0) {
        //adjust exit for short position
        //setup the stop-loss if not yet set from entry
        if (_SLPrice == -1)
          _SLPrice = _iiHighEntry;

        //try to find a pivot bar on the price action with a lower high
        //price than the current stop-loss
        double slPrice = _iiHighEntry;
        PriceActionBar bar = null;
        int i = 0;
        DateTime timestamp = Bars.Time[i];
        while (timestamp > _firstInsideBar && i < ExecInfo.MaxBarsBack) {
          bar = _priceActionAnalysis.Bars[timestamp];
          if (bar.state.pivotState[StopLossPivotLayer - 1].pivotHigh && bar.high < slPrice)
            slPrice = bar.high;
          i++;
          timestamp = Bars.Time[i];
        }

        _SLPrice = _SLPrice > slPrice ? slPrice : _SLPrice;

        //send stop-loss for position
        _SX_SL.Send(_SLPrice);
      }
    }

    private void CalcBarOrderFilled() {
      orderFilled();
    }

    private void orderFilled() {
      //send initial stop-loss when an entry order is filled
      if (PositionSide > 0) {
        _SLPrice = _iiLowEntry;
        _LX_SL.Send(_SLPrice);
      }

      if (PositionSide < 0) {
        _SLPrice = _iiHighEntry;
        _SX_SL.Send(_SLPrice);
      }
    }
  }
}