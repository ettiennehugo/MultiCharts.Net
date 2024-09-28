using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using RiskManagement;

namespace PowerLanguage.Strategy {
  /// <summary>
  /// Chart Type: Candlestick
  /// 
  /// Hypothesis:
  ///	Determines entry point on a failed test of the moving average on a pullback using a dual time frame to determine the entry direction
  ///   off the higher time frame. Enters an on a breakout of the extreme price level after the test.
  /// 
  /// Ideal Instruments and Timeframes:
  ///   Forex using 5-minute/15-minute for entry and 1-hour for direction filter.
  /// 
  /// Entry Rules - Long:
  ///   1. Longer time frame double moving average fast moving average is above the slow moving average, default uses a 8-ema and 21-ema.
  ///   2. Longer time frame price bar is completely above the fast moving average.
  ///   3. Shorter time frame triple moving average has the fast moving average above the medium moving average and the medium moving average
  ///      is above the long moving average, default uses a 8-ema, 13-ema and 21-ema.
  ///   4. Bar low/close touches or penetrates the 8-ema and 13-ema region on the shorter time frame for a test of the moving average
  ///      on a trend pullback - this is the signal bar.
  ///   5. Determine the highest point of the last x-number of bars and add a buffer pip value for a breakout high entry at the price level 
  ///      - this is the breakout bar.
  /// 
  /// Entry Rules - Short:
  ///   1. Longer time frame double moving average fast moving average is below the slow moving average, default uses a 8-ema and 21-ema.
  ///   2. Longer time frame price bar is completely below the fast moving average.
  ///   3. Shorter time frame triple moving average has the fast moving average below the medium moving average and the medium moving average
  ///      is below the long moving average, default uses a 8-ema, 13-ema and 21-ema.
  ///   4. Bar high/close touches or penetrates the 8-ema and 13-ema region on the shorter time frame for a test of the moving average
  ///      on a trend pullback - this is the signal bar.
  ///   5. Determine the lowest point of the last "entry breakout"-number of bars and subtract a buffer pip value for a breakout low entry at the price level 
  ///      - this is the breakout bar.
  /// 
  /// Exit Rules - Long:
  ///   * Initial risk exit: We place an initial risk exit stop order at the low of the signal bar price level with a buffer pip value subtracted. Thus our initial
  ///     risk (R) is between the low of the signal bar and the high of the breakout bar plus twice the buffer pip value.
  ///   * Take profit exit 1: We place a limit exit order for half our position at the 1R above the entry price level. 
  ///   * Take profit exit 2: When the first take profit target exit is hit we move the initial stop loss order up to break even and start trailing the market at the
  ///                         low of the last "trailing count"-number of bars.
  /// 
  /// Exit Rules - Short:
  ///   * Initial risk exit: We place an initial risk exit stop order at the high of the signal bar price level with a buffer pip value added. Thus our initial
  ///     risk (R) is between the high of the signal bar and the low of the breakout bar plus twice the buffer pip value.
  ///   * Take profit exit 1: We place a limit exit order for half our position at the 1R below the entry price level.
  ///   * Take profit exit 2: When the first take profit target exit is hit we move the initial stop loss order up to break even and start trailing the market at the
  ///                         high of the last "trailing count"-number of bars.
  /// 
  /// Money Management:
  ///   Fixed ratio position sizing. Disable dynamic money management by setting the money management start size input to zero (0).
  /// 
  /// Notes:
  ///   * Strategy must only be run on the close of the bar, intrabar computation would cause incorrect results.
  ///   * Basic rules and examples are available at https://www.youtube.com/watch?v=0hUJTsjYPcQ  
  ///   * Check that the moving averages for the longer timeframe is computed correctly since they are updated on the shorter timeframe. 
  ///   * Currently rule 2 completely kicks out all trades since it expects the H1 bars to be completely above or below the fast moving average BUT the given that
  ///     you use an 8EMA and 21EMA the bars are essentially strung onto the 8EMA like a kebab and are never completely above/below the fast moving average.
  ///   * Implement the profit target exits, from preliminary tests with some of the kinks resolved it might be quite a profitable strategy.
  /// 
  /// 
  /// TODO:
  ///   * Implement check against the economic calendar and disable trading/exit positions when we get close to volatile economic news.
  ///   
  /// 
  ///   TBD: 
  ///     * The first take profit exit might be problematic due to the fact that we enter with a large risk and only really offset half this risk with
  ///       the first exit. Try to just apply a trailing stop exit to follow the market and make the profits run as far as possible.
  ///       - Try different trailing techniques like a volatility based trailing stop.
  /// 
  /// </summary>
  public class _TrimaScalper : SignalObject {
    //Inputs:
    [Input] public int TrimaFastLength { get; set; }      //tripe moving average fast length
    [Input] public int TrimaMediumLength { get; set; }    //tripe moving average medium length
    [Input] public int TrimaSlowLength { get; set; }      //tripe moving average slow length
    [Input] public int DumaFastLength { get; set; }       //dual moving average fast length
    [Input] public int DumaSlowLength { get; set; }       //dual moving average slow length
    [Input] public int TFStartTime { get; set; }          //time filter start time, e.g. 800 is 8am and 1300 is 1pm		
    [Input] public int TFWindowLength { get; set; }       //time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public double MMPercentRiskPerTrade { get; set; } //percentage allowable risk per trade
    [Input] public int MMStaticPositionSize { get; set; } //default trade size for static position sizing
    [Input] public int MMStartPosSize { get; set; }       //money management start position size (0 = disable MM), e.g. 10000 starts with one mini lot
    [Input] public double MMProfitDelta { get; set; }     //money management profit delta at which new positions are added, e.g. 500 adds a new position when 500-curreny unit profit is made
    [Input] public double MMStartNetProfit { get; set; }  //money management start profit if the strategy was shut down between runs
    [Input] public bool EnableLong { get; set; }          //enable long trades
    [Input] public bool EnableShort { get; set; }         //enable short trades
    [Input] public bool Debug { get; set; }               //flag to enable debug output to the output console

    //Constants:
    const int BUFFER_PIP_VALUE = 3;           //buffer pip-value to add/substract from entries/exits
    const int ENTRY_BREAKOUT_LENGTH = 5;      //breakout length used to scan for highest/lowest entry bar extreme - see entry long/short rule 4
    const int MINI_LOT_SIZE = 1000;           //Mini-lot size used in Forex

    //Attributes:
    private IOrderPriced _LE_DMM;
    private IOrderPriced _SE_DMM;
    private IOrderPriced _LX_SL;
    private IOrderPriced _SX_SL;
    private IOrderPriced _LX_TP1;
    private IOrderPriced _SX_TP1;
    private IOrderPriced _LX_TP2;
    private IOrderPriced _SX_TP2;
    private XAverage _TrimaFastAvgFC;
    private XAverage _TrimaMediumAvgFC;
    private XAverage _TrimaSlowAvgFC;
    private VariableSeries<Double> _TrimaFastAvg;
    private VariableSeries<Double> _TrimaMediumAvg;
    private VariableSeries<Double> _TrimaSlowAvg;
    private XAverage _DumaFastAvgFC;
    private XAverage _DumaSlowAvgFC;
    private VariableSeries<Double> _DumaFastAvg;
    private VariableSeries<Double> _DumaSlowAvg;
    private TimeSpan _StartTime;
    private TimeSpan _EndTime;
    private double _PipValue;
    private double _BufferPipValue;
    private double _EntryBarHigh;
    private double _EntryBarLow;
    private double _LEEntryStopPrice;
    private double _SEEntryStopPrice;
    private double _InitialRisk;
    private int _TP1TargetSize;
    private int _TP2TargetSize;
    private RiskManager _RiskManager;

    public _TrimaScalper(object _ctx) : base(_ctx) {
      TrimaFastLength = 8;
      TrimaMediumLength = 13;
      TrimaSlowLength = 21;
      DumaFastLength = 8;
      DumaSlowLength = 21;
      TFStartTime = 0;    //12:00am
      TFWindowLength = 1439;  //minutes in a full 24-hour trading day
      MMPercentRiskPerTrade = 0.01;  //percent risk per trade
      MMStaticPositionSize = 100000; //default size for static position sizing
      MMStartPosSize = 0;   //start contracts and additional contracts to add (0 - disable dynamic MM)
      MMProfitDelta = 0;    //profit delta before another additional contract is added
      MMStartNetProfit = 0; //start net profit between restarts of the strategy
      EnableLong = true;    //per default enable long trades
      EnableShort = true;   //per default enable short trades		
      Debug = false;
    }

    protected override void Create() {
      //create the indicators used
      _TrimaFastAvgFC = new XAverage(this);
      _TrimaMediumAvgFC = new XAverage(this);
      _TrimaSlowAvgFC = new XAverage(this);
      _TrimaFastAvg = new VariableSeries<Double>(this);
      _TrimaMediumAvg = new VariableSeries<Double>(this);
      _TrimaSlowAvg = new VariableSeries<Double>(this);
      _DumaFastAvgFC = new XAverage(this);
      _DumaSlowAvgFC = new XAverage(this);
      _DumaFastAvg = new VariableSeries<Double>(this);
      _DumaSlowAvg = new VariableSeries<Double>(this);

      //create the order types used
      _LE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "TMALE", EOrderAction.Buy));     //dynamic money management orders
      _SE_DMM = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "TMASE", EOrderAction.SellShort));
      _LX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TMALXSL", EOrderAction.Sell));        //exit orders
      _SX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TMASXSL", EOrderAction.BuyToCover));
      _LX_TP1 = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "TMALXTP1", EOrderAction.Sell));
      _LX_TP2 = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "TMALXTP2", EOrderAction.Sell));
      _SX_TP1 = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "TMASXTP1", EOrderAction.BuyToCover));
      _SX_TP2 = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "TMASXTP2", EOrderAction.BuyToCover));
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
      //NOTE: We multiply by 10 since the point value is always a tenth of a PIP.
      _PipValue = Bars.Info.PointValue * 10 * Bars.Info.BigPointValue;
      _BufferPipValue = BUFFER_PIP_VALUE * _PipValue;

      //set the start/end times for the time filter
      _StartTime = new TimeSpan(TFStartTime / 100, TFStartTime % 100, 0);
      _EndTime = _StartTime + new TimeSpan(TFWindowLength / 60, TFWindowLength % 60, 0);

      //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
      //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
      if (_EndTime.Days > 0)
        _EndTime = _EndTime.Subtract(new TimeSpan(_EndTime.Days, 0, 0, 0));

      //setup indicator parameters
      _TrimaFastAvgFC.Price = Bars.Close;
      _TrimaFastAvgFC.Length = TrimaFastLength;
      _TrimaMediumAvgFC.Price = Bars.Close;
      _TrimaMediumAvgFC.Length = TrimaMediumLength;
      _TrimaSlowAvgFC.Price = Bars.Close;
      _TrimaSlowAvgFC.Length = TrimaSlowLength;

      IInstrument directionFeed = BarsOfData(2);
      _DumaFastAvgFC.Price = directionFeed.Close;
      _DumaFastAvgFC.Length = DumaFastLength;
      _DumaSlowAvgFC.Price = directionFeed.Close;
      _DumaSlowAvgFC.Length = DumaSlowLength;
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
      calcBarExit();
    }

    private void CalcBarDefault() {
      //record the indicator values
      _TrimaFastAvg.Value = _TrimaFastAvgFC[0];
      _TrimaMediumAvg.Value = _TrimaMediumAvgFC[0];
      _TrimaSlowAvg.Value = _TrimaSlowAvgFC[0];


      //TBD: Will this compute correctly given that the CalcBarDefault will be called on the shorter timeframe while these moving averages are based on a longer timeframe?????
      //if (LongerTimeframe.Status == EBarState.Close) {
      //  _DumaFastAvg.Value = _DumaFastAvgFC[0];
      //  _DumaSlowAvg.Value = _DumaSlowAvgFC[0];
      //}

      _DumaFastAvg.Value = _DumaFastAvgFC[0];
      _DumaSlowAvg.Value = _DumaSlowAvgFC[0];

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
      //apply time filter for entries
      //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
      //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
      DateTime currentTime = Bars.Time.Value;

      //reset strategy state variables
      _EntryBarHigh = -1;
      _EntryBarLow = -1;
      _LEEntryStopPrice = -1;
      _SEEntryStopPrice = -1;
      _InitialRisk = -1;
      _TP1TargetSize = -1;
      _TP2TargetSize = -1;

      if (((_StartTime < _EndTime) && (_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= _EndTime)) ||    //start/end time fall in the same trading day
          ((_StartTime > _EndTime) && ((_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= _EndTime)))) {  //start/end time span over different trading days
                                                                                                                          //determine whether we should enter a position
        bool enterLong = false;
        bool enterShort = false;
        IInstrument directionFeed = BarsOfData(2);


        //TODO: When a signal bar is detected we need to place the stop-entry order as dictated by rule 5 BUT we also need to wait for this order to be filled for a period
        //      of time until the order is either filled or some of the conditions are broken:
        // 1. Wait x-number of bars for the order to be filled and then cancel it - maybe default this to 3-bars
        // 2. The moving average conditions for the fast timeframe still holds, cancel the order once these special conditions do not hold.
        // 3. No additional entry detection is performed while rules 1 and 2 are not broken. 

     
        //determine whether we can enter a long position if enabled
        if (EnableLong) {
          // 1. Longer time frame double moving average fast moving average is above the slow moving average, default uses a 8-ema and 21-ema.
          enterLong = _DumaFastAvg.Value > _DumaSlowAvg.Value;

          // 2. Longer time frame price bar is completely above the fast moving average.
          enterLong = enterLong && directionFeed.Open[0] > _DumaFastAvg.Value;
          enterLong = enterLong && directionFeed.Close[0] > _DumaFastAvg.Value;
          enterLong = enterLong && directionFeed.Low[0] > _DumaFastAvg.Value;

          // 3. Shorter time frame triple moving average has the fast moving average above the medium moving average and the medium moving average
          //    is above the long moving average, default uses a 8-ema, 13-ema and 21-ema.
          enterLong = enterLong && _TrimaFastAvg.Value > _TrimaMediumAvg.Value && _TrimaMediumAvg.Value > _TrimaSlowAvg.Value;

          // 4. Bar low/close touches or penetrates the 8-ema and 13-ema region on the shorter time frame for a test of the moving average
          //    on a trend pullback - this is the signal bar.

          // 4.1 Test whether a pullback occurred for the price relative to the fast moving average.

          //TODO: Implement the trend pullback logic:
          // * Compute the absolute distance between the LOW of the bar and the fast MA.
          // * If the absolute distance increases and then decreases you have a pullback.
          // * Last bar being analyzed must penetrate the 8-ema and 13-ema zone, this becomes the signal bar.
          bool trendPullback = true;
          enterLong = enterLong && trendPullback;


          // 4.2 Test whether the bar open/close penetrated the fast ema/medium ema region
          bool penetrateTrimaFast = ((Bars.Close[0] > Bars.Open[0]) && (Bars.Open[0] < _TrimaFastAvg.Value) && (Bars.Open[0] > _TrimaMediumAvg.Value)) ||
                                    ((Bars.Close[0] < Bars.Open[0]) && (Bars.Close[0] < _TrimaFastAvg.Value) && (Bars.Close[0] > _TrimaMediumAvg.Value));
          enterLong = enterLong && penetrateTrimaFast;

          // 5. Determine the highest point of the last x-number of bars and add a buffer pip value for a breakout high entry at the price level 
          //    - this is the breakout bar.
          _LEEntryStopPrice = Bars.High.Highest(ENTRY_BREAKOUT_LENGTH) + _BufferPipValue;

          //record the entry bar low with buffer pip value subtracted for initial risk stop-loss order
          _EntryBarLow = enterLong ? Bars.Low[0] - _BufferPipValue : _EntryBarLow;

          //setup the initial risk value if we have a long entry potentially coming up
          _InitialRisk = enterLong ? _LEEntryStopPrice - _EntryBarLow : _InitialRisk;
        }

        //determine whether we can enter a short position if enabled
        if (EnableShort) {
          // 1. Longer time frame double moving average fast moving average is below the slow moving average, default uses a 8-ema and 21-ema.
          enterShort = _DumaFastAvg.Value < _DumaSlowAvg.Value;

          // 2. Longer time frame price bar is completely below the fast moving average.
          enterShort = enterShort && directionFeed.Open[0] < _DumaFastAvg.Value;
          enterShort = enterShort && directionFeed.Close[0] < _DumaFastAvg.Value;
          enterShort = enterShort && directionFeed.High[0] < _DumaFastAvg.Value;

          // 3. Shorter time frame triple moving average has the fast moving average below the medium moving average and the medium moving average
          //    is below the long moving average, default uses a 8-ema, 13-ema and 21-ema.
          enterShort = enterShort && _TrimaFastAvg.Value < _TrimaMediumAvg.Value && _TrimaMediumAvg.Value < _TrimaSlowAvg.Value;

          // 4. Bar high/close touches or penetrates the 8-ema and 13-ema region on the shorter time frame for a test of the moving average
          //    on a trend pullback - this is the signal bar.

          // 4.1 Test whether a pullback occurred for the price relative to the fast moving average.

          //TODO: Implement the trend pullback logic:
          // * Compute the absolute distance between the HIGH of the bar and the fast MA.
          // * If the absolute distance increases and then decreases you have a pullback.
          // * Last bar being analyzed must penetrate the 8-ema and 13-ema zone, this becomes the signal bar.
          bool trendPullback = true;
          enterShort = enterShort && trendPullback;


          // 4.2 Test whether the bar open/close penetrated the fast ema/medium ema region
          bool penetrateTrimaFast = ((Bars.Close[0] > Bars.Open[0]) && (Bars.Close[0] > _TrimaFastAvg.Value) && (Bars.Close[0] < _TrimaMediumAvg.Value)) ||
                                    ((Bars.Close[0] < Bars.Open[0]) && (Bars.Open[0] > _TrimaFastAvg.Value) && (Bars.Open[0] < _TrimaMediumAvg.Value));
          enterShort = enterShort && penetrateTrimaFast;



          // 5. Determine the lowest point of the last "entry breakout"-number of bars and subtract a buffer pip value for a breakout low entry at the price level 
          //    - this is the breakout bar.
          _SEEntryStopPrice = Bars.Low.Lowest(ENTRY_BREAKOUT_LENGTH) - _BufferPipValue;

          //record the entry bar high with buffer pip value added for initial risk stop-loss order
          _EntryBarHigh = enterShort ? Bars.High[0] + _BufferPipValue : _EntryBarHigh;

          //setup the initial risk value if we have a short entry potentially coming up
          _InitialRisk = enterShort ? _EntryBarHigh - _SEEntryStopPrice : _InitialRisk;
        }

        //enter trade if signal was generated and risk is allowed
        if (enterLong) {
          RiskAnalysis riskAnalysis = _RiskManager.Analyze(_InitialRisk);
          if (riskAnalysis.Allowed)
            _LE_DMM.Send(_LEEntryStopPrice, riskAnalysis.Size);
        }

        if (enterShort) {
          RiskAnalysis riskAnalysis = _RiskManager.Analyze(_InitialRisk);
          if (riskAnalysis.Allowed)
            _SE_DMM.Send(_SEEntryStopPrice, riskAnalysis.Size);
        }
      }
    }

    private void calcBarExit() {


      /// Exit Rules - Long:
      ///   * Initial risk exit: We place an initial risk exit stop order at the low of the signal bar price level with a buffer pip value subtracted. Thus our initial
      ///     risk (R) is between the low of the signal bar and the high of the breakout bar plus twice the buffer pip value.
      ///   * Take profit exit 1: We place a limit exit order for half our position at the 1R above the entry price level. 
      ///   * Take profit exit 2: When the first take profit target exit is hit we move the initial stop loss order up to break even and start trailing the market at the
      ///                         low of the last "trailing count"-number of bars.
      /// 
      /// Exit Rules - Short:
      ///   * Initial risk exit: We place an initial risk exit stop order at the high of the signal bar price level with a buffer pip value added. Thus our initial
      ///     risk (R) is between the high of the signal bar and the low of the breakout bar plus twice the buffer pip value.
      ///   * Take profit exit 1: We place a limit exit order for half our position at the 1R below the entry price level.
      ///   * Take profit exit 2: When the first take profit target exit is hit we move the initial stop loss order up to break even and start trailing the market at the
      ///                         high of the last "trailing count"-number of bars.


      //set size-per-target if not yet computed for this position
      if (_TP1TargetSize == -1) {
        //we have to ensure that the lot allocations are in valid increments of the smallest trade size
        int sizePerTarget = CurrentPosition.OpenLots / 2;
        int remainder = sizePerTarget % MINI_LOT_SIZE;
        if (remainder == 0) {
          //lots are even in size
          _TP1TargetSize = sizePerTarget;
          _TP2TargetSize = sizePerTarget;
        } else {
          //lots are unequal in size with take-profit 1 being the smallest
          _TP1TargetSize = sizePerTarget - remainder;
          _TP2TargetSize = sizePerTarget + remainder;
        }
      }

      if (PositionSide > 0) {
        //send/update the take-profit orders
        double tp1 = _LEEntryStopPrice + _InitialRisk;
        double tp2 = _LEEntryStopPrice + (2 * _InitialRisk);
        _LX_TP2.Send(tp2, _TP2TargetSize);

        //send first take profit if position was not yet closed
        if (StrategyInfo.MarketPosition > _TP2TargetSize) {
          _LX_TP1.Send(tp1, _TP1TargetSize);
          _LX_SL.Send(_EntryBarLow);
        } else {

          _LX_SL.Send(_LEEntryStopPrice);   //Break even stop as suggested
                                            //_LX_SL.Send(_TrimaMediumAvg.Value); //Stop at the medium average.


        }

        if (Debug)
          Output.WriteLine("Long {0}: EntrySp({1}), ExitSp({2}), TP1({3}), TP2({4}), R({5})", Bars.Time[0].ToString(), _LEEntryStopPrice, _EntryBarLow, tp1, tp2, _InitialRisk);

      }

      if (PositionSide < 0) {
        //send/update the take-profit orders
        double tp1 = _SEEntryStopPrice - _InitialRisk;
        double tp2 = _SEEntryStopPrice - (2 * _InitialRisk);
        _SX_TP2.Send(tp2, _TP2TargetSize);

        //send first take profit if position was not yet closed
        if (Math.Abs(StrategyInfo.MarketPosition) > _TP2TargetSize) {
          _SX_TP1.Send(tp1, _TP1TargetSize);
          _SX_SL.Send(_EntryBarHigh);
        } else {

          _SX_SL.Send(_SEEntryStopPrice);   //Break even stop as suggested                    
                                            //_SX_SL.Send(_TrimaMediumAvg.Value); //Stop at the medium average.
        }

        if (Debug)
          Output.WriteLine("Short {0}: EntrySp({1}), ExitSp({2}), TP1({3}), TP2({4}), R({5})", Bars.Time[0].ToString(), _SEEntryStopPrice, _EntryBarLow, tp1, tp2, _InitialRisk);

      }
    }
  }
}