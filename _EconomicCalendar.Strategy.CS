using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using FileHelpers;
using RiskManagement;

namespace PowerLanguage.Strategy {
  /// <summary>
  /// Chart Type:
  /// 	Candlestick M1 with Intrabar Order Generation (IOG) Mode Enabled
  /// 
  /// Hypothesis:
  /// 	Trades the economic event calendar using specific entry and exit techiques. The strategy takes a snapshot of the market extreme high and low for a given length of bars and then uses the entry
  /// 	technique to enter the market. Once the market is entered a specific exit technique is used to follow the market for exit.
  /// 
  /// Ideal Instruments and Timeframes:
  ///		Event studies must be performed to determine which instruments should be traded for which events using which types of entries/exits. E.g. The US non-farm payroll causes a lot of volatility
  ///		in the EURUSD in which case a Fade entry with a trailing stop or hard profit exit should work best while on the USDJPY the US non-farm payroll seems to cause a much cleaner breakout and thus
  ///		a plain breakout with trailing stop or hard profit should suffice.
  /// 
  /// Input Parameters:
  ///   EventFile  - CSV file containing the market events, only upcoming market events will be processed (unless in backtesting mode). The CSV file must contain the following columns in this given order, 
  ///               this information is mainly based around the way the data is downloaded from an economic event calendar:
  ///                 start_date_time - Date and time of the market event.
  ///                 time_window - Minutes and seconds the entry should be active after the specified date and time of the market event.
  ///                 currency_symbol - Currency symbol to trade for the given market event.
  ///                 currency - Currency of country affected by the market event.
  ///                 event_description - Description of the market event.
  ///                 country - Country publishing the market event, not used but included for reference.
  ///                 volatility - Expected volatility around the market event, should be none, low, medium or high. Alternatively, numbers can be used for 0 = None, 1 = low, 2 = medium and 3 = high.
  ///                 actual - Actual value of the market event if it is quantified, can be blank if not quantified.
  ///                 previous - Previous value of the market event if it is quantified, can be blank if not quantified.
  ///                 consensus - Consensus value from analysts for the value of the market event, can be blank if not quantified.
  ///                 entry - Formula for the entry strategy to used to trade the market event.  
  ///                 exit1 - Required parameter, formula for the exit strategy used to trade the market event, required. 
  ///                 exit2 - Optional parameter, formula for additional exit strategy used to trade the market event. This is typically used when you want to place take_profit limit orders.
  ///   length - Length to determine the immediate market high and low.
  /// 
  /// Entry Stragtegies:
  ///   Each entry strategy will place a stop order in the market that works in conjunction with the exit strategies.
  ///     * Breakout(length, long_buffer_pips, short_buffer_pips) - Uses a specific number of buffer pips for the long and short breaks. The stop-loss will be placed at the opposite end of the entry direction, e.g. breakout(5, 5) would
  ///                                                               enter the market when the current market high or low is broken by 5 pips and would place the stop loss order 5 pips beyond the opposite market extreme.
  ///     * Fade(length, long_buffer_pips, short_buffer_pips) - Uses a specific number of buffer pips to fade the market at long and short extremes. The stop-loss is placed a given amount of
  ///                                                           pips beyond the entry price, e.g. fade(15, 5, 15, 5) would enter the market when it has moved 15 pips beyond the price extremes
  ///                                                           and would place a stop-loss at 5 pips beyond the entry price.
  ///     * FadeTrail(length, activationPips, trailPips) - Uses a specific number of trailing pips to trail the market when it breaks out of the high/low for the last length bars by a specific activationPip amount, e.g. FadeTrail(10, 5, 3) would trail the market by 
  ///                                                      3-pips with a stop order when the market breaks out of the high/low of the last 10 bars by at least 5 pips.
  /// 
  /// Exit Strategies:
  ///   Exit strategies will place stop/limit orders based on the market position to act as take profit exit strategies:
  ///     * BreakoutStopLoss() - Used in conjuction with the breakout strategy to place a stop-loss at the opposite extreme of the price level that was not hit.
  ///     * BreakEvenStopLoss(profitPips, bufferPips) - Sets up a break-even stop-loss when the given number of pips profit is reached with the given number of buffer pips added to the average entry price, 
  ///                                                   e.g. BreakEvenStopLoss(10, 2) would place a stop-loss 2-pips above the average entry price when a profit of 10-pips is reached. 
  ///     * StopLoss(pips) - Enter a stop order into the market by the number of pips above/below the average entry price.
  ///     * Trail(activation_profit_pips, trailing_pips) - When the activation profit pips are reached the market will trailed using the given trailing pips, e.g. trail(10, 5) will start to trail the market at 5 pips once the profit
  ///                                                      of the market reaches a 10 pip profit.
  ///     * TakeProfit(profit_pips) - Will enter a limit order into the market to exit the position when a specific profit in pips are reached, take_profit(10) will exit the position when a 10 pip profit is reached.
  ///     * HighLowTrail(length, bufferPips) - Computes the high/low of the last length number of bars and moves a trailing stop order in the market placing the order beyond the high/low by the specified bufferPips, e.g. 
  ///                                          HighLowTrailStop(10, 2) would move a trailing stop order with the market position placed at the high/low of the last 10 bars 2-pips beyond the high/low price level.
  /// 
  /// Money Management:
  ///   Fixed ratio money management is supported. 
  /// 
  /// Notes:
  ///     * You just want to trade around the leading indicators since they will forcast what it going to happen in the economy and trade them - see url https://www.focus-economics.com/countries
  /// 
  /// 
  /// TODO:
  ///   * Add entry using some of the John Ehlers indicators such as the super smoother this might be useful on very volatile economic events such as the US non-farm payroll. You can use the smoother to remove noise and determine
  ///     a general direction for the market based on the smoother direction. Study the US non-farm payroll before adding this entry.
  ///   * Add entry for a price action reversal, what would make this hard is to decide when the reversal is forming and taking place. It might be sufficient to measure the price movement and volume to determine this.
  ///     Look at recent movement in the market and volume.
  ///   
  ///   
  /// 
  /// </summary>
  [IOGMode(IOGMode.Enabled)]
  public class _EconomicCalendar : SignalObject {
    //Inputs:
    [Input] public string EventFile { get; set; }               //CSV format config file of the events to trade against for the different currencies.
    [Input] public bool LoadFutureEventsOnly { get; set; }      //set to true to load only future events, historic events are discarded
    [Input] public int MMStartPosSize { get; set; }             //money management start position size (0 = disable MM), e.g. 10000 starts with one mini lot
    [Input] public double MMProfitDelta { get; set; }           //money management profit delta at which new positions are added, e.g. 500 adds a new position when 500-curreny unit profit is made
    [Input] public double MMPercentRiskPerTrade { get; set; }   //percentage allowable risk per trade
    [Input] public int MMStaticPositionSize { get; set; }       //default trade size for static position sizing
    [Input] public double MMStartNetProfit { get; set; }        //money management start profit if the strategy was shut down between runs
    [Input] public bool Debug { get; set; }

    //Properties:
    public List<CEconomicEvent> Events { get; set; }

    public CEconomicEvent NextEvent {
      get {
        CEconomicEvent result = null;

        //try to find an economic event that has entered a trade but is not yet completed or an economic event that
        //falls within the current time window for processing
        foreach (CEconomicEvent economicEvent in Events) {
          if (economicEvent.TradeEntered && !economicEvent.TradeCompleted) {
            //economic event that has entered a trade but not yet completed, allow it to finish the trade
            result = economicEvent;
            break;  //live trade on economic event started earlier, terminate search
          } else {
            //try to find the next economic event within current time window
            DateTime endTime = economicEvent.StartDateTime + economicEvent.TimeWindow;
            if (economicEvent.StartDateTime <= Bars.Time[0] && Bars.Time[0] < endTime)
              result = result == null || result.Volatility < economicEvent.Volatility ? economicEvent : result;
            else if (economicEvent.StartDateTime > Bars.Time[0])
              break;    //first event after the current bar time so there can not be further events to analyse
          }
        }

        return result;
      }
    }          //returns the event that matches the current time window

    public IOrderPriced Stop1LE { get { return _Stop1LE; } }
    public IOrderPriced Stop1SE { get { return _Stop1SE; } }
    public IOrderPriced Limit1LE { get { return _Limit1LE; } }
    public IOrderPriced Limit1SE { get { return _Limit1SE; } }
    public IOrderPriced Stop1LX { get { return _Stop1LX; } }
    public IOrderPriced Stop1SX { get { return _Stop1SX; } }
    public IOrderPriced Limit1LX { get { return _Limit1LX; } }
    public IOrderPriced Limit1SX { get { return _Limit1SX; } }
    public IOrderPriced Stop2LX { get { return _Stop2LX; } }
    public IOrderPriced Stop2SX { get { return _Stop2SX; } }
    public IOrderPriced Limit2LX { get { return _Limit2LX; } }
    public IOrderPriced Limit2SX { get { return _Limit2SX; } }
    public IOrderPriced Stop3LX { get { return _Stop3LX; } }
    public IOrderPriced Stop3SX { get { return _Stop3SX; } }
    public IOrderPriced Limit3LX { get { return _Limit3LX; } }
    public IOrderPriced Limit3SX { get { return _Limit3SX; } }


    public RiskManager RiskManager { get { return _RiskManager; } }
    public double PipValue { get { return _PipValue; } }
    public int CurrencyDecimals { get { return _CurrencyDecimals; } }

    //Attributes:
    protected CCSVCalendarParser _Parser;
    private IOrderPriced _Stop1LE;
    private IOrderPriced _Stop1SE;
    private IOrderPriced _Limit1LE;
    private IOrderPriced _Limit1SE;
    private IOrderPriced _Stop1LX;
    private IOrderPriced _Stop1SX;
    private IOrderPriced _Limit1LX;
    private IOrderPriced _Limit1SX;
    private IOrderPriced _Stop2LX;
    private IOrderPriced _Stop2SX;
    private IOrderPriced _Limit2LX;
    private IOrderPriced _Limit2SX;
    private IOrderPriced _Stop3LX;
    private IOrderPriced _Stop3SX;
    private IOrderPriced _Limit3LX;
    private IOrderPriced _Limit3SX;
    private CEconomicEvent _Event;
    private double _PipValue;
    private int _CurrencyDecimals;
    private RiskManager _RiskManager;

    public _EconomicCalendar(object Ctx) : base(Ctx) { }

    protected override void Create() {
      //initialize input parameters
      EventFile = "";
      LoadFutureEventsOnly = true;
      Debug = false;
      MMStartNetProfit = 0.0;
      MMStaticPositionSize = 100000;
      MMStartPosSize = 0;
      MMPercentRiskPerTrade = 1.0;
      MMProfitDelta = 0.0;
      MMStartNetProfit = 0.0;

      //create the orders used to enter/exit positions
      _Stop1LE = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "S1LE", EOrderAction.Buy));
      _Stop1SE = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "S1SE", EOrderAction.SellShort));
      _Limit1LE = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "L1LE", EOrderAction.Buy));
      _Limit1SE = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "L1SE", EOrderAction.SellShort));
      //NOTE: All exit orders are configured to exit the whole position.
      _Stop1LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "S1LX", EOrderAction.Sell));
      _Stop1SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "S1SX", EOrderAction.BuyToCover));
      _Limit1LX = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "L1LX", EOrderAction.Sell));
      _Limit1SX = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "L1SX", EOrderAction.BuyToCover));
      _Stop2LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "S2LX", EOrderAction.Sell));
      _Stop2SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "S2SX", EOrderAction.BuyToCover));
      _Limit2LX = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "L2LX", EOrderAction.Sell));
      _Limit2SX = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "L2SX", EOrderAction.BuyToCover));
      _Stop3LX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "S3LX", EOrderAction.Sell));
      _Stop3SX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "S3SX", EOrderAction.BuyToCover));
      _Limit3LX = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "L3LX", EOrderAction.Sell));
      _Limit3SX = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "L3SX", EOrderAction.BuyToCover));

      //initialize rest of the attributes
      _Event = null;
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

      //create general components
      Events = new List<CEconomicEvent>();
      _Parser = new CCSVCalendarParser(this);
      _Parser.Filename = EventFile;

      //standarize the buffer pip value for the price scale we're trading
      _PipValue = Bars.Info.PointValue * 10 * Bars.Info.BigPointValue;

      //compute the currency decimals for the instrument being traded
      double pointValue = Bars.Info.PointValue;
      _CurrencyDecimals = 0;
      while (pointValue != 1) {
        _CurrencyDecimals++;
        pointValue *= 10;
      }

      //output debug info if required
      if (Debug)
        Output.WriteLine("{0} {1} Loaded {2} economic events to process.", Bars.Info.Description, Bars.Time[0], Events.Count);
    }

    //protected override void StopCalc() {
    //	//cleanup/deinitialize strategy components
    //	
    //}

    protected override void CalcBar() {
      switch (Environment.CalcReason) {
        case CalculationReason.Default:
          _Event = NextEvent;
          if (_Event != null) {
            _Event.CalcBar();
          }
          break;
        case CalculationReason.OrderFilled:
          _Event.OrderFilled();
          break;
      }
    }

    protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price) {
      _Event.OrderFilled();
    }
  }

  /// <summary>
  /// Encapsulates the trading behaviour around an ecomonic event.
  /// </summary>
  public class CEconomicEvent {
    //Constants:
    public const string TKN_BREAKOUT = "BREAKOUT";
    public const string TKN_FADE = "FADE";
    public const string TKN_FADE_TRAIL = "FADETRAIL";
    public const string TKN_NONE = "NONE";
    public const string TKN_BREAKOUT_STOP_LOSS = "BREAKOUTSTOPLOSS";
    public const string TKN_BREAKEVEN_STOP_LOSS = "BREAKEVENSTOPLOSS";
    public const string TKN_STOP_LOSS = "STOPLOSS";
    public const string TKN_TRAIL = "TRAIL";
    public const string TKN_HIGH_LOW_TRAIL = "HIGHLOWTRAIL";
    public const string TKN_TAKE_PROFIT = "TAKEPROFIT";

    //Types:
    public enum EEventVolatility {
      None,
      Low,
      Medium,
      High
    }

    //Properties:
    public DateTime StartDateTime { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public bool TradeEntered { get { return _TradeEntered; } }
    public bool TradeCompleted {  get { return _TradeCompleted; } }
    public string CurrencySymbol { get; set; }
    public string Currency { get; set; }
    public string Description { get; set; }
    public string Country { get; set; }
    public EEventVolatility Volatility { get; set; }
    public double Actual { get; set; }
    public double Previous { get; set; }
    public double Consensus { get; set; }
    public string Entry { get { return _Entry; } set { _EntryStrategy = parseEntryStrategy(value); } }
    public CEntryStrategy EntryStrategy { get { return _EntryStrategy; } }
    public string Exit1 { get { return _Exit1; } set { _Exit1Strategy = parseExitStrategy(value); } }
    public CExitStrategy Exit1Strategy { get { return _Exit1Strategy; } }
    public string Exit2 { get { return _Exit2; } set { _Exit2Strategy = parseExitStrategy(value); } }
    public CExitStrategy Exit2Strategy { get { return _Exit2Strategy; } }
    public string Exit3 { get { return _Exit3; } set { _Exit3Strategy = parseExitStrategy(value); } }
    public CExitStrategy Exit3Strategy { get { return _Exit3Strategy; } }

    //Attributes:
    protected static Regex _FuncParam0;   //function with 0 parameter
    protected static Regex _FuncParam1;   //function with 1 parameter
    protected static Regex _FuncParam2;   //function with 2 parameters
    protected static Regex _FuncParam3;   //function with 3 parameters
    protected _EconomicCalendar _Calendar;
    protected bool _TradeEntered;         //flag to indicate whether economic event entered a trade
    protected bool _TradeCompleted;       //flag to indicate whether economic event completed it's trade, it needs to stay alive until trade is completed
    protected string _Entry;
    protected CEntryStrategy _EntryStrategy;
    protected string _Exit1;
    protected CExitStrategy _Exit1Strategy;
    protected string _Exit2;
    protected CExitStrategy _Exit2Strategy;
    protected string _Exit3;
    protected CExitStrategy _Exit3Strategy;

    static CEconomicEvent() {
      _FuncParam0 = new Regex(@"(\w+)\s*\(\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
      _FuncParam1 = new Regex(@"(\w+)\s*\(\s*(\d+)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
      _FuncParam2 = new Regex(@"(\w+)\s*\(\s*(\d+)\s*\,\s*(\d+)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
      _FuncParam3 = new Regex(@"(\w+)\s*\(\s*(\d+)\s*\,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public CEconomicEvent(_EconomicCalendar calendar) {
      _Calendar = calendar;
      _TradeEntered = false;
      _TradeCompleted = false;
    }

    /// <summary>
    /// Called when we're not in a position and the entry orders should be processed.
    /// </summary>
    public void CalcBar() {
      //compute the entry/exit based on current position
      if (_Calendar.PositionSide == 0) {
        //check whether the trade bar calculation should be allowed, based on whether a trade was entered
        if (_TradeEntered)
          //trade was entered and completed if we reach this state
          _TradeCompleted = true;
        else 
          //trade was not yet entered so allow entry strategy to process for possible entry
          _EntryStrategy.CalcBar();
      } else {
        //flag that trade was entered
        _TradeEntered = true;     //NOTE: This has to be done due to the fact that the backtesting engine does not fire the OrderFilled event.

        //process exit strategies
        if (_Exit1Strategy != null)
          _Exit1Strategy.CalcBar();
        if (_Exit2Strategy != null)
          _Exit2Strategy.CalcBar();
        if (_Exit3Strategy != null)
          _Exit3Strategy.CalcBar();
      }
    }

    /// <summary>
    /// Called when an entry order is filled.
    /// </summary>
    public void OrderFilled() {
      //flag that trade was entered
      _TradeEntered = true;

      //process exit strategies
      if (_Exit1Strategy != null)
        _Exit1Strategy.OrderFilled();
      if (_Exit2Strategy != null)
        _Exit2Strategy.OrderFilled();
      if (_Exit3Strategy != null)
        _Exit3Strategy.OrderFilled();
    }

    /// <summary>
    /// Returns the entry order from the parent calendar strategy for the given entry strategy. 
    /// </summary>
    public void OrdersForEntry(CEntryStrategy strategy, out IOrderPriced le, out IOrderPriced se) {
      le = null;
      se = null;

      switch (strategy.EntryStrategy) {
        case CStrategy.EEntryStrategy.Breakout:
          le = _Calendar.Stop1LE;
          se = _Calendar.Stop1SE;
          break;
        case CStrategy.EEntryStrategy.Fade:
          le = _Calendar.Limit1LE;
          se = _Calendar.Limit1SE;
          break;
        case CStrategy.EEntryStrategy.FadeTrail:
          le = _Calendar.Stop1LE;
          se = _Calendar.Stop1SE;
          break;
      }
    }

    /// <summary>
    /// Returns the exit order from the parent calendar strategy for the given exit strategy.
    /// </summary>
    public void OrdersForExit(CExitStrategy strategy, out IOrderPriced lx, out IOrderPriced sx) {
      lx = null;
      sx = null;

      //determine the exit strategy to analyse for the exit orders
      if (strategy == _Exit1Strategy) {
        switch (_Exit1Strategy.ExitStrategy) {
          case CExitStrategy.EExitStrategy.StopLoss:
            lx = _Calendar.Stop1LX;
            sx = _Calendar.Stop1SX;
            break;
          case CExitStrategy.EExitStrategy.BreakEvenStopLoss:
            lx = _Calendar.Stop1LX;
            sx = _Calendar.Stop1SX;
            break;
          case CStrategy.EExitStrategy.TakeProfit:
            lx = _Calendar.Limit1LX;
            sx = _Calendar.Limit1SX;
            break;
          case CStrategy.EExitStrategy.Trail:
            lx = _Calendar.Stop1LX;
            sx = _Calendar.Stop1SX;
            break;
          case CStrategy.EExitStrategy.BreakoutStopLoss:
            lx = _Calendar.Stop1LX;
            sx = _Calendar.Stop1SX;
            break;
          case CStrategy.EExitStrategy.HighLowTrail:
            lx = _Calendar.Stop1LX;
            sx = _Calendar.Stop1SX;
            break;
        }
      } else if (strategy == _Exit2Strategy) {
        switch (_Exit2Strategy.ExitStrategy) {
          case CExitStrategy.EExitStrategy.StopLoss:
            lx = _Calendar.Stop2LX;
            sx = _Calendar.Stop2SX;
            break;
          case CExitStrategy.EExitStrategy.BreakEvenStopLoss:
            lx = _Calendar.Stop2LX;
            sx = _Calendar.Stop2SX;
            break;
          case CStrategy.EExitStrategy.TakeProfit:
            lx = _Calendar.Limit2LX;
            sx = _Calendar.Limit2SX;
            break;
          case CStrategy.EExitStrategy.Trail:
            lx = _Calendar.Stop2LX;
            sx = _Calendar.Stop2SX;
            break;
          case CStrategy.EExitStrategy.BreakoutStopLoss:
            lx = _Calendar.Stop2LX;
            sx = _Calendar.Stop2SX;
            break;
          case CStrategy.EExitStrategy.HighLowTrail:
            lx = _Calendar.Stop2LX;
            sx = _Calendar.Stop2SX;
            break;
        }
      } else if (strategy == _Exit3Strategy) {
        switch (_Exit3Strategy.ExitStrategy) {
          case CExitStrategy.EExitStrategy.StopLoss:
            lx = _Calendar.Stop3LX;
            sx = _Calendar.Stop3SX;
            break;
          case CExitStrategy.EExitStrategy.BreakEvenStopLoss:
            lx = _Calendar.Stop3LX;
            sx = _Calendar.Stop3SX;
            break;
          case CStrategy.EExitStrategy.TakeProfit:
            lx = _Calendar.Limit3LX;
            sx = _Calendar.Limit3SX;
            break;
          case CStrategy.EExitStrategy.Trail:
            lx = _Calendar.Stop3LX;
            sx = _Calendar.Stop3SX;
            break;
          case CStrategy.EExitStrategy.BreakoutStopLoss:
            lx = _Calendar.Stop3LX;
            sx = _Calendar.Stop3SX;
            break;
          case CStrategy.EExitStrategy.HighLowTrail:
            lx = _Calendar.Stop3LX;
            sx = _Calendar.Stop3SX;
            break;
        }
      }
    }

    /// <summary>
    /// Parses given entry strategy and returns an initialized class instance of the strategy.
    /// </summary>
    protected CEntryStrategy parseEntryStrategy(string entry) {
      CEntryStrategy result = null;

      if (_FuncParam3.IsMatch(entry)) {
        Match match = _FuncParam3.Match(entry);

        switch (match.Groups[1].Value.ToUpper()) {
          case TKN_BREAKOUT:
            CEntryBreakout breakout = new CEntryBreakout(this, _Calendar);
            breakout.Length = Int32.Parse(match.Groups[2].Value);
            breakout.LongBufferPips = Int32.Parse(match.Groups[3].Value);
            breakout.ShortBufferPips = Int32.Parse(match.Groups[4].Value);
            result = breakout;
            break;

          case TKN_FADE:
            CEntryFade fade = new CEntryFade(this, _Calendar);
            fade.Length = Int32.Parse(match.Groups[2].Value);
            fade.LongBufferPips = Int32.Parse(match.Groups[3].Value);
            fade.ShortBufferPips = Int32.Parse(match.Groups[4].Value);
            result = fade;
            break;

          case TKN_FADE_TRAIL:
            CEntryFadeTrail fadeTrail = new CEntryFadeTrail(this, _Calendar);
            fadeTrail.Length = Int32.Parse(match.Groups[2].Value);
            fadeTrail.ActivationPips = Int32.Parse(match.Groups[3].Value);
            fadeTrail.TrailPips = Int32.Parse(match.Groups[4].Value);
            result = fadeTrail;
            break;

          default:
            throw new Exception(String.Format("Entry function \"{0}\" is not a valid entry function with three parameters.", match.Groups[0].Value));
        }
      } else
        throw new Exception(String.Format("Entry \"{0}\" does not match any entry function.", entry));

      return result;
    }

    /// <summary>
    /// Parses given exit strategy and returns an initialized strategy class instance.
    /// </summary>
    protected CExitStrategy parseExitStrategy(string exit) {
      CExitStrategy result = null;

      if (_FuncParam0.IsMatch(exit)) {
        Match match = _FuncParam0.Match(exit);

        switch (match.Groups[1].Value.ToUpper()) {
          case TKN_BREAKOUT_STOP_LOSS:
            CExitBreakoutStopLoss breakoutStopLoss = new CExitBreakoutStopLoss(this, _Calendar);
            breakoutStopLoss.Entry = (CEntryBreakout)EntryStrategy;
            result = breakoutStopLoss;
            break;

          default:
            throw new Exception(String.Format("Exit function \"{0}\" is not a valid exit function with no parameters.", match.Groups[0].Value));
        }
      } else if (_FuncParam1.IsMatch(exit)) {
        Match match = _FuncParam1.Match(exit);

        switch (match.Groups[1].Value.ToUpper()) {
          case TKN_STOP_LOSS:
            CExitStopLoss stopLoss = new CExitStopLoss(this, _Calendar);
            stopLoss.Pips = Int32.Parse(match.Groups[2].Value);
            result = stopLoss;
            break;

          case TKN_TAKE_PROFIT:
            CExitTakeProfit takeProfit = new CExitTakeProfit(this, _Calendar);
            takeProfit.Pips = Int32.Parse(match.Groups[2].Value);
            result = takeProfit;
            break;

          default:
            throw new Exception(String.Format("Exit function \"{0}\" is not a valid exit function with one parameter.", match.Groups[0].Value));
        }
      } else if (_FuncParam2.IsMatch(exit)) {
        Match match = _FuncParam2.Match(exit);

        switch (match.Groups[1].Value.ToUpper()) {
          case TKN_BREAKEVEN_STOP_LOSS:
            CExitBreakEvenStopLoss breakEvenStopLoss = new CExitBreakEvenStopLoss(this, _Calendar);
            breakEvenStopLoss.ProfitPips = Int32.Parse(match.Groups[2].Value);
            breakEvenStopLoss.BufferPips = Int32.Parse(match.Groups[3].Value);
            result = breakEvenStopLoss;
            break;

          case TKN_TRAIL:
            CExitTrail trail = new CExitTrail(this, _Calendar);
            trail.ActivationPips = Int32.Parse(match.Groups[2].Value);
            trail.TrailPips = Int32.Parse(match.Groups[3].Value);
            result = trail;
            break;

          case TKN_HIGH_LOW_TRAIL:
            CExitHighLowTrail highLowTrail = new CExitHighLowTrail(this, _Calendar);
            highLowTrail.Length = Int32.Parse(match.Groups[2].Value);
            highLowTrail.BufferPips = Int32.Parse(match.Groups[3].Value);
            result = highLowTrail;
            break;

          default:
            throw new Exception(String.Format("Exit function \"{0}\" is not a valid exit function with two parameters.", match.Groups[0].Value));
        }
      } //else - we assume this is an optional exit and thus it can be blank

      return result;
    }
  }

  /// <summary>
  /// CSV parser for the economic calendar.
  /// </summary>
  public class CCSVCalendarParser {
    //Types:
    /// <summary>
    /// Expected record structure for the CSV file.
    /// </summary>
    [DelimitedRecord(","), IgnoreFirst(), IgnoreEmptyLines()]
    protected class EconomicEventRecord {
      public string startDateTime;
      public string timeWindow;
      public string currencySymbol;
      public string currency;
      public string description;
      public string country;
      public string volatility;
      [FieldConverter(ConverterKind.Double), FieldNullValue(0.0)] public double actual;
      [FieldConverter(ConverterKind.Double), FieldNullValue(0.0)] public double previous;
      [FieldConverter(ConverterKind.Double), FieldNullValue(0.0)] public double consensus;
      [FieldQuoted()] public string entryStrategy;
      [FieldQuoted()] public string exit1Strategy;
      [FieldOptional(), FieldQuoted()] public string exit2Strategy;
      [FieldOptional(), FieldQuoted()] public string exit3Strategy;
    }

    //Properties:
    public bool LoadFutureEventsOnly { get; set; }
    public string Filename {
      get { return _Filename; }
      set { parse(value); _Filename = value; }
    }

    //Attributes:
    protected _EconomicCalendar Calendar;
    protected string _Filename;

    public CCSVCalendarParser(_EconomicCalendar calendar) {
      Calendar = calendar;
    }

    /// <summary>
    /// Parses the given filename into a new economic event list and sets it for the parent economic calendar if successfull.
    /// </summary>
    protected void parse(string filename) {
      FileHelperEngine<EconomicEventRecord> parser = new FileHelperEngine<EconomicEventRecord>();
      EconomicEventRecord[] lines = parser.ReadFile(filename);

      List<CEconomicEvent> economicEvents = new List<CEconomicEvent>();

      foreach (EconomicEventRecord line in lines) {
        //if only future events are required discard lines that are in the past
        DateTime dateTime = DateTime.Parse(line.startDateTime);
        if (LoadFutureEventsOnly && dateTime < DateTime.Now)
          continue;

        //discard events that would not be applicable to trading this symbol
        line.currencySymbol = line.currencySymbol.ToUpper();
        line.currency = line.currency.ToUpper();

        if (Calendar.Bars.Info.Description != line.currencySymbol)
          continue;

        //event should be kept, parse and set the rest of the attributes
        CEconomicEvent economicEvent = new CEconomicEvent(Calendar);
        economicEvent.StartDateTime = dateTime;
        economicEvent.TimeWindow = TimeSpan.Parse(line.timeWindow);
        economicEvent.CurrencySymbol = line.currencySymbol;
        economicEvent.Currency = line.currency;
        economicEvent.Description = line.description;
        economicEvent.Country = line.country;
        economicEvent.Volatility = (CEconomicEvent.EEventVolatility)Enum.Parse(typeof(CEconomicEvent.EEventVolatility), line.volatility);
        economicEvent.Actual = line.actual;
        economicEvent.Previous = line.previous;
        economicEvent.Consensus = line.consensus;
        economicEvent.Entry = line.entryStrategy;
        economicEvent.Exit1 = line.exit1Strategy;
        economicEvent.Exit2 = line.exit2Strategy;
        economicEvent.Exit3 = line.exit3Strategy;

        economicEvents.Add(economicEvent);
      }

      //set economic events for processing by the strategy
      Calendar.Events = economicEvents;
    }
  }

  /// <summary>
  /// Base class for entry/exit strategies.
  /// </summary>
  public abstract class CStrategy {
    //Types:
    public enum EEntryStrategy {
      None,
      Breakout,
      Fade,
      FadeTrail,
    }

    public enum EExitStrategy {
      None,
      BreakoutStopLoss,
      BreakEvenStopLoss,
      StopLoss,
      Trail,
      HighLowTrail,
      TakeProfit,
    }

    //Properties:
    public EEntryStrategy EntryStrategy { get { return _EntryStrategy; } }
    public EExitStrategy ExitStrategy { get { return _ExitStrategy; } }
    public int PositionSide { get { return _Calendar.PositionSide; } }
    public abstract double Risk { get; }

    //Attributes:
    protected _EconomicCalendar _Calendar;
    protected EEntryStrategy _EntryStrategy;
    protected EExitStrategy _ExitStrategy;
    protected CEconomicEvent _EconomicEvent;
    protected IOutput _Output;
    protected IInstrument _Bars;
    protected bool _Debug;

    public CStrategy(EEntryStrategy entryStrategy, EExitStrategy exitStrategy, CEconomicEvent economicEvent, _EconomicCalendar calendar) {
      _EntryStrategy = entryStrategy;
      _ExitStrategy = exitStrategy;
      _EconomicEvent = economicEvent;
      _Calendar = calendar;
      _Output = _Calendar.Output;
      _Bars = _Calendar.Bars;
      _Debug = _Calendar.Debug;
    }

    /// <summary>
    /// Implements the calculation logic for the strategy to entry/exit the market.
    /// </summary>
    public abstract void CalcBar();
  }

  /// <summary>
  /// Base class for entry strategies.
  /// </summary>
  public abstract class CEntryStrategy : CStrategy {
    //Properties:
    public override double Risk {
      get {
        //currently we simply return the largest possible risk of the position based on the configured exit strategies
        double result = 0.0;

        result = _EconomicEvent.Exit1Strategy != null && _EconomicEvent.Exit1Strategy.Risk > result ? _EconomicEvent.Exit1Strategy.Risk : result;
        result = _EconomicEvent.Exit2Strategy != null && _EconomicEvent.Exit2Strategy.Risk > result ? _EconomicEvent.Exit2Strategy.Risk : result;
        result = _EconomicEvent.Exit3Strategy != null && _EconomicEvent.Exit3Strategy.Risk > result ? _EconomicEvent.Exit3Strategy.Risk : result;

        return result;
      }
    }

    public CEntryStrategy(EEntryStrategy entryStrategy, CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(entryStrategy, EExitStrategy.None, economicEvent, calendar) { }
  }

  /// <summary>
  /// 
  /// </summary>
  public abstract class CExitStrategy : CStrategy {

    public CExitStrategy(EExitStrategy exitStrategy, CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(EEntryStrategy.None, exitStrategy, economicEvent, calendar) { }

    /// <summary>
    /// Implements the logic for the strategy when an order is filled. Default implementation just calls the CalcBar to handle the standard exit calculations.
    /// </summary>
    public virtual void OrderFilled() {
      CalcBar();
    }
  }

  /// <summary>
  /// Implements a breakout entry strategy using the last length number of bars to compute the market extremes for the entry.
  /// </summary>
  public class CEntryBreakout : CEntryStrategy {
    //Properties:
    public int Length { get; set; }
    public int LongBufferPips { get; set; }
    public int ShortBufferPips { get; set; }
    public double LongPrice { get { return _LongPrice; } }
    public double ShortPrice { get { return _ShortPrice; } }

    //Attributes:
    protected double _LongPrice;
    protected double _ShortPrice;
    protected bool _OutputDebugMessage;

    public CEntryBreakout(CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(CStrategy.EEntryStrategy.Breakout, economicEvent, calendar) {
      Length = 10;
      LongBufferPips = 3;
      ShortBufferPips = 3;
      _LongPrice = double.MinValue;
      _ShortPrice = double.MinValue;
      _OutputDebugMessage = true;
    }

    public override void CalcBar() {
      RiskAnalysis riskAnalysis = null;

      //only compute the entry prices once-off if not yet set
      if (_LongPrice == double.MinValue) {
        //compute the entry prices
        _LongPrice = _Calendar.Bars.High.Highest(Length) + _Calendar.PipValue * LongBufferPips;
        _ShortPrice = _Calendar.Bars.Low.Lowest(Length) - _Calendar.PipValue * ShortBufferPips;
        _LongPrice = Math.Round(_LongPrice, _Calendar.CurrencyDecimals);
        _ShortPrice = Math.Round(_ShortPrice, _Calendar.CurrencyDecimals);
      }

      //only send orders when trade has not yet been entered, each economic event only allows a single entry for the specific event
      if (!_EconomicEvent.TradeEntered) {
        //send the breakout orders if risk allowed
        IOrderPriced le = null;
        IOrderPriced se = null;
        _EconomicEvent.OrdersForEntry(this, out le, out se);

        riskAnalysis = _Calendar.RiskManager.Analyze(Risk);
        if (riskAnalysis.Allowed) {
          le.Send(_LongPrice, riskAnalysis.Size);
          se.Send(_ShortPrice, riskAnalysis.Size);

          if (_Debug && _OutputDebugMessage) {
            _Output.WriteLine("{0} Waiting for BREAKOUT {1}: LongBreakPrice({2}) ShortBreakPrice({3}) Risk({4})", _Bars.Info.Description, _Bars.Time[0], _LongPrice, _ShortPrice, Risk);
            _OutputDebugMessage = false;    //log debug message only once
          }

        } else if (_Debug && _OutputDebugMessage) {
          _Output.WriteLine("{0} BREAKOUT risk not allowed {1}: LongBreakPrice({2}) ShortBreakPrice({3}) Risk({4})", _Bars.Info.Description, _Bars.Time[0], _LongPrice, _ShortPrice, Risk);
          _OutputDebugMessage = false;    //log debug message only once
        }
      }
    }
  }

  /// <summary>
  /// Implements a fade entry strategy using the last length number of bars to compute the market extremes and puts a fade entry beyond those levels by a number of pips.
  /// </summary>
  public class CEntryFade : CEntryStrategy {
    //Properties:
    public int Length { get; set; }
    public int LongBufferPips { get; set; }
    public int ShortBufferPips { get; set; }
    public double LongPrice { get { return _LongPrice; } }
    public double ShortPrice { get { return _ShortPrice; } }

    //Attribute:
    protected double _LongPrice;
    protected double _ShortPrice;

    public CEntryFade(CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(CStrategy.EEntryStrategy.Fade, economicEvent, calendar) {
      Length = 10;
      LongBufferPips = 15;
      ShortBufferPips = 15;
      _LongPrice = double.MinValue;
      _ShortPrice = double.MinValue;
    }

    public override void CalcBar() {
      RiskAnalysis riskAnalysis = null;

      //only compute the entry prices once-off if not yet set
      if (_LongPrice == double.MinValue) {
        _LongPrice = _Calendar.Bars.Low.Lowest(Length) - _Calendar.PipValue * LongBufferPips;
        _ShortPrice = _Calendar.Bars.High.Highest(Length) + _Calendar.PipValue * ShortBufferPips;
        _LongPrice = Math.Round(_LongPrice, _Calendar.CurrencyDecimals);
        _ShortPrice = Math.Round(_ShortPrice, _Calendar.CurrencyDecimals);
      }

      //only send orders when trade has not yet been entered, each economic event only allows a single entry for the specific event
      if (!_EconomicEvent.TradeEntered) {
        //send the fade orders if risk allowed
        IOrderPriced le = null;
        IOrderPriced se = null;
        _EconomicEvent.OrdersForEntry(this, out le, out se);

        riskAnalysis = _Calendar.RiskManager.Analyze(Risk);
        if (riskAnalysis.Allowed) {
          le.Send(_LongPrice, riskAnalysis.Size);
          se.Send(_ShortPrice, riskAnalysis.Size);

          if (_Debug)
            _Output.WriteLine("{0} Waiting for FADE {1}: LongFadePrice({2}) ShortFadePrice({3}) Risk({4})", _Bars.Info.Description, _Bars.Time[0], _LongPrice, _ShortPrice, Risk);

        } else if (_Debug)
          _Output.WriteLine("{0} FADE risk not allowed {1}: LongFadePrice({2}) ShortFadePrice({3}) Risk({4})", _Bars.Info.Description, _Bars.Time[0], _LongPrice, _ShortPrice, Risk);
      }
    }
  }

  /// <summary>
  /// Implements a fade entry strategy using a trailing stop order. The order is inserted into the market once it has moved beyond the market extremes of the last length number of bars.
  /// </summary>
  public class CEntryFadeTrail : CEntryStrategy {
    //Properties:
    public int Length { get; set; }
    public int ActivationPips { get; set; }
    public int TrailPips { get; set; }
    public double LongPrice { get { return _LongPrice; } }
    public double ShortPrice { get { return _ShortPrice; } }
    public double LongActivationPrice {  get { return _LongActivationPrice;  } }
    public double ShortActivationPrice { get { return _ShortActivationPrice; } }
    public bool TrailActive {  get { return _TrailLong || _TrailShort; } }
    public bool TrailLongActive {  get { return _TrailLong; } }
    public bool TrailShortActive { get { return _TrailShort; } }
    public double TrailPrice { get { return _TrailPrice; } }

    //Attributes:
    protected double _LongPrice;
    protected double _ShortPrice;
    protected double _LongActivationPrice;
    protected double _ShortActivationPrice;
    protected double _TrailPrice;
    protected bool _TrailLong;
    protected bool _TrailShort;

    public CEntryFadeTrail(CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(CStrategy.EEntryStrategy.FadeTrail, economicEvent, calendar) {
      Length = 10;
      ActivationPips = 5;
      TrailPips = 3;
      _LongPrice = double.MinValue;
      _ShortPrice = double.MinValue;
      _LongActivationPrice = double.MinValue;
      _ShortActivationPrice = double.MinValue;
      _TrailPrice = double.MinValue;
      _TrailLong = false;
      _TrailShort = false;
    }

    public override void CalcBar() {
      RiskAnalysis riskAnalysis = null;

      //only compute the entry prices once-off if not yet set
      if (_LongPrice == double.MinValue) {
        //NOTE: For the trailing fade we have to enter a long position when the price breakout to the LOW of the recent price range and we should enter
        //      a short position when the price breakout to the HIGH of the recent price range. The code below might look incorrect but it is correct.
        _LongPrice = _Bars.Low.Lowest(Length);
        _ShortPrice = _Bars.High.Highest(Length);
        _LongActivationPrice = _LongPrice - ActivationPips * _Calendar.PipValue;
        _ShortActivationPrice = _ShortPrice + ActivationPips * _Calendar.PipValue;
        _LongPrice = Math.Round(_LongPrice, _Calendar.CurrencyDecimals);
        _ShortPrice = Math.Round(_ShortPrice, _Calendar.CurrencyDecimals);
        _LongActivationPrice = Math.Round(_LongActivationPrice, _Calendar.CurrencyDecimals);
        _ShortActivationPrice = Math.Round(_ShortActivationPrice, _Calendar.CurrencyDecimals);

        if (_Debug)
          _Output.WriteLine("{0} Trailing FADE started on {1}: LongBreakoutPrice({2}) ShortBreakoutPrice({3}) LongActivationPrice({4}) ShortActivationPrice({5})", _Bars.Info.Description, _Bars.Time[0], _LongPrice, _ShortPrice, _LongActivationPrice, _ShortActivationPrice);
      }

      //update the trailing activation flags if neither is set, as soon as one is set we're going to trail the market for an entry in the opposite direction
      if (!_TrailLong && !_TrailShort) {
        //see whether we have a breakout of the long/short price extremes of the recent price values
        _TrailLong = _Bars.Low[0] < _LongActivationPrice;
        _TrailShort = _Bars.High[0] > _ShortActivationPrice;
        
        if (_Debug) {
          if (_TrailLong)
            _Output.WriteLine("{0} FADE TRAIL LONG trailing activated {1}: LongActivationPrice({2}) Low({3})", _Bars.Info.Description, _Bars.Time[0], _LongActivationPrice, _Bars.Low[0]);

          if (_TrailShort)
            _Output.WriteLine("{0} FADE TRAIL SHORT trailing activated {1}: ShortActivationPrice({2}) High({3})", _Bars.Info.Description, _Bars.Time[0], _ShortActivationPrice, _Bars.High[0]);
        }
      }

      //process the trailing level for the entry
      double previousTrailPrice = _TrailPrice;
      double currentTrail = double.MinValue;
      if (_TrailLong) {
        currentTrail = _Bars.Low[0] + _Calendar.PipValue * TrailPips;
        _TrailPrice = _TrailPrice == double.MinValue || _TrailPrice > currentTrail ? currentTrail : _TrailPrice;
        _TrailPrice = Math.Round(_TrailPrice, _Calendar.CurrencyDecimals);
      }

      if (_TrailShort) {
        currentTrail = _Bars.High[0] - _Calendar.PipValue * TrailPips;
        _TrailPrice = _TrailPrice == double.MinValue || _TrailPrice < currentTrail ? currentTrail : _TrailPrice;
        _TrailPrice = Math.Round(_TrailPrice, _Calendar.CurrencyDecimals);
      }

      //only send orders when trade has not yet been entered, each economic event only allows a single entry for the specific event
      if (!_EconomicEvent.TradeEntered && _TrailPrice != double.MinValue) {
        //send the fade orders if risk allowed
        riskAnalysis = _Calendar.RiskManager.Analyze(Risk);
        if (riskAnalysis.Allowed) {
          IOrderPriced le = null;
          IOrderPriced se = null;
          _EconomicEvent.OrdersForEntry(this, out le, out se);

          if (_TrailLong) {
            le.Send(_TrailPrice, riskAnalysis.Size);

            if (_Debug && previousTrailPrice != _TrailPrice)
              _Output.WriteLine("{0} FADE TRAIL Trail change for LONG entry {1}: TrailPrice({2})", _Bars.Info.Description, _Bars.Time[0], _TrailPrice);
          }

          if (_TrailShort) {
            se.Send(_TrailPrice, riskAnalysis.Size);

            if (_Debug && previousTrailPrice != _TrailPrice)
              _Output.WriteLine("{0} FADE TRAIL Trail change for SHORT entry {1}: TrailPrice({2})", _Bars.Info.Description, _Bars.Time[0], _TrailPrice);
          }
        } else if (_Debug)
          _Output.WriteLine("{0} FADE TRAIL risk not allowed {1}: LongFadeTrailPrice({2}) ShortFadeTrailPrice({3}) LongFadeActive({4}) ShortFadeActive({5}) TrailPrice({6}) Risk({7})", _Bars.Info.Description, _Bars.Time[0], _LongPrice, _ShortPrice, _TrailLong.ToString(), _TrailShort.ToString(), _TrailPrice, Risk);
      }
    }
  }

  /// <summary>
  /// Chains a exit with a breakout entry to place a stop-loss order at the opposite end of the breakout.
  /// </summary>
  public class CExitBreakoutStopLoss : CExitStrategy {
    //Properties:
    public CEntryStrategy Entry {
      get { return _Entry; }
      set {
        _Entry = (CEntryBreakout)value;
      }
    }

    public override double Risk {
      get {
        //risk for breakout is distance between the long and short entry points
        return Math.Abs(_Entry.LongPrice - _Entry.ShortPrice);
      }
    }

    //Attributes:
    protected CEntryBreakout _Entry;
    protected bool _OutputDebugMessage;

    public CExitBreakoutStopLoss(CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(CStrategy.EExitStrategy.BreakoutStopLoss, economicEvent, calendar) {
      _OutputDebugMessage = true;
    }

    public override void CalcBar() {
      IOrderPriced lx = null;
      IOrderPriced sx = null;
      _EconomicEvent.OrdersForExit(this, out lx, out sx);

      if (PositionSide > 0) {
        lx.Send(_Entry.ShortPrice);

        if (_Debug && !_OutputDebugMessage) {
          _Output.WriteLine("{0} Long {1}: BreakoutStopLoss({2})", _Bars.Info.Description, _Bars.Time[0], _Entry.ShortPrice);
          _OutputDebugMessage = false;    //output debug information only once, else we flood the debug output with useless info
        }

      } else if (PositionSide < 0) {
        sx.Send(_Entry.LongPrice);

        if (_Debug && !_OutputDebugMessage) {
          _Output.WriteLine("{0} Short {1}: BreakoutStopLoss({2})", _Bars.Info.Description, _Bars.Time[0], _Entry.LongPrice);
          _OutputDebugMessage = false;    //output debug information only once, else we flood the debug output with useless info
        }
      }
    }
  }

  /// <summary>
  /// Place a stop-loss at the given number of pips above/below the average entry price when a given number pips profits are reached.
  /// </summary>
  public class CExitBreakEvenStopLoss : CExitStrategy {
    //Properties:
    public int ProfitPips { get; set; }
    public int BufferPips { get; set; }
    public bool Active {  get { return _Active; } }

    public override double Risk {
      get {
        return 0.0;
      }
    }

    //Attributes:
    protected double _ActivationPrice;
    protected double _BreakEvenPrice;
    protected bool _Active;

    public CExitBreakEvenStopLoss(CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(CStrategy.EExitStrategy.BreakEvenStopLoss, economicEvent, calendar) {
      ProfitPips = 10;
      BufferPips = 1;
      _ActivationPrice = double.MinValue;
      _BreakEvenPrice = double.MinValue;
    }

    public override void CalcBar() {
      IOrderPriced lx = null;
      IOrderPriced sx = null;
      _EconomicEvent.OrdersForExit(this, out lx, out sx);
      
      //initialize the price level to start sending break-even stop order when this price level is broken
      if (_ActivationPrice == double.MinValue) {
        if (PositionSide > 0) {
          _ActivationPrice = _Calendar.StrategyInfo.AvgEntryPrice + ProfitPips * _Calendar.PipValue;
          _BreakEvenPrice = _Calendar.StrategyInfo.AvgEntryPrice + BufferPips * _Calendar.PipValue;

          if (_Debug)
            _Output.WriteLine("{0} LONG Break-even stop-Loss {1}: ActivationPrice({2} BreakEvenPrice({3}))", _Bars.Info.Description, _Bars.Time[0], _ActivationPrice, _BreakEvenPrice);

        } else if (PositionSide < 0) {
          _ActivationPrice = _Calendar.StrategyInfo.AvgEntryPrice - ProfitPips * _Calendar.PipValue;
          _BreakEvenPrice = _Calendar.StrategyInfo.AvgEntryPrice - BufferPips * _Calendar.PipValue;

          if (_Debug)
            _Output.WriteLine("{0} SHORT Break-even stop-loss {1}: ActivationPrice({2} BreakEvenPrice({3}))", _Bars.Info.Description, _Bars.Time[0], _ActivationPrice, _BreakEvenPrice);
        }
      }

      //determine to activation status if not yet active
      if (!_Active) {
        if (PositionSide > 0) {
          _Active = _Bars.High[0] > _ActivationPrice;

          if (_Debug)
            _Output.WriteLine("{0} LONG Break-even stop-loss Activated {1}: High({2})", _Bars.Info.Description, _Bars.Time[0], _Bars.High[0]);

        } else if (PositionSide < 0) {
          _Active = _Bars.Low[0] < _ActivationPrice;

          if (_Debug)
            _Output.WriteLine("{0} SHORT Break-even stop-loss Activated {1}: Low({2})", _Bars.Info.Description, _Bars.Time[0], _Bars.Low[0]);
        }
      }

      //send stop-loss order when break-even stop has been activated
      if (_Active) {
        if (PositionSide > 0)
          lx.Send(_BreakEvenPrice);
        else if (PositionSide < 0)
          sx.Send(_BreakEvenPrice);
      }
    }
  }

  /// <summary>
  /// Places a hard stop-loss at the given number of pips from the average entry price.
  /// </summary>
  public class CExitStopLoss : CExitStrategy {
    //Properties:
    public int Pips { get; set; }

    public override double Risk {
      get {
        return Pips;
      }
    }

    //Attributes:
    protected double _Price;

    public CExitStopLoss(CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(CStrategy.EExitStrategy.StopLoss, economicEvent, calendar) {
      Pips = 5;
      _Price = double.MinValue;
    }

    public override void CalcBar() {
      IOrderPriced lx = null;
      IOrderPriced sx = null;
      _EconomicEvent.OrdersForExit(this, out lx, out sx);

      if (PositionSide > 0) {
        if (_Price == double.MinValue) {
          _Price = _Calendar.StrategyInfo.AvgEntryPrice - Pips * _Calendar.PipValue;
          _Price = Math.Round(_Price, _Calendar.CurrencyDecimals);

          if (_Debug)
            _Output.WriteLine("{0} Long {1}: StopLossPrice({2})", _Bars.Info.Description, _Bars.Time[0], _Price);
        }

        lx.Send(_Price);

      } else if (PositionSide < 0) {
        if (_Price == double.MinValue) {
          _Price = _Calendar.StrategyInfo.AvgEntryPrice + Pips * _Calendar.PipValue;
          _Price = Math.Round(_Price, _Calendar.CurrencyDecimals);

          if (_Debug)
            _Output.WriteLine("{0} Short {1}: StopLossPrice({2})", _Bars.Info.Description, _Bars.Time[0], _Price);
        }

        sx.Send(_Price);
      }
    }
  }

  /// <summary>
  /// Trails the market price movement by the a specific number of pips after the market has moved by a number of pips in the direction of the trade.
  /// </summary>
  public class CExitTrail : CExitStrategy {
    //Properties:
    public int ActivationPips { get; set; }
    public int TrailPips { get; set; }

    public override double Risk {
      get {
        return TrailPips;
      }
    }

    //Attributes:
    protected double _Price;
    protected double _ActivationPrice;
    protected bool _Active;

    public CExitTrail(CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(CStrategy.EExitStrategy.Trail, economicEvent, calendar) {
      ActivationPips = 10;
      TrailPips = 5;
      _Price = double.MinValue;
      _ActivationPrice = double.MinValue;
      _Active = false;
    }

    public override void CalcBar() {
      IOrderPriced lx = null;
      IOrderPriced sx = null;
      _EconomicEvent.OrdersForExit(this, out lx, out sx);

      if (PositionSide > 0) {
        //compute activation price level 
        if (_ActivationPrice == double.MinValue) {
          _ActivationPrice = _Calendar.StrategyInfo.AvgEntryPrice + ActivationPips * _Calendar.PipValue;
          _ActivationPrice = Math.Round(_ActivationPrice, _Calendar.CurrencyDecimals);

          if (_Debug)
            _Output.WriteLine("{0} Long {1}: TrailingActivationPrice({2})", _Bars.Info.Description, _Bars.Time[0], _ActivationPrice);
        }

        //process activation if not yet done
        if (!_Active) {
          _Active = _Bars.High[0] > _ActivationPrice;

          if (_Active && _Debug)
            _Output.WriteLine("{0} Long {1}: TrailingActivated High({2})", _Bars.Info.Description, _Bars.Time[0], _Bars.High[0]);
        }

        //send trailing stop if trailing is activated
        if (_Active) {
          double currentTrailPrice = _Bars.High[0] - TrailPips * _Calendar.PipValue;
          currentTrailPrice = Math.Round(currentTrailPrice, _Calendar.CurrencyDecimals);

          double previousTrailingPrice = _Price;
          _Price = _Price == double.MinValue || _Price < currentTrailPrice ? currentTrailPrice : _Price;
          _Price = Math.Round(_Price, _Calendar.CurrencyDecimals);

          if (previousTrailingPrice != _Price && _Debug)
            _Output.WriteLine("{0} Long {1}: TrailingPrice({2})", _Bars.Info.Description, _Bars.Time[0], _Price);

          lx.Send(_Price);
        }

      } else if (PositionSide < 0) {
        //compute activation price level 
        if (_ActivationPrice == double.MinValue) {
          _ActivationPrice = _Calendar.StrategyInfo.AvgEntryPrice - ActivationPips * _Calendar.PipValue;
          _ActivationPrice = Math.Round(_ActivationPrice, _Calendar.CurrencyDecimals);

          if (_Debug)
            _Output.WriteLine("{0} Short {1}: TrailingActivationPrice({2})", _Bars.Info.Description, _Bars.Time[0], _ActivationPrice);
        }

        //process activation if not yet done
        if (!_Active) {
          _Active = _Bars.Low[0] < _ActivationPrice;

          if (_Active && _Debug)
            _Output.WriteLine("{0} Short {1}: TrailingActivated Low({2})", _Bars.Info.Description, _Bars.Time[0], _Bars.Low[0]);
        }

        //send trailing stop if trailing is activated
        if (_Active) {
          double currentTrailPrice = _Bars.Low[0] + TrailPips * _Calendar.PipValue;
          currentTrailPrice = Math.Round(currentTrailPrice, _Calendar.CurrencyDecimals);

          double previousTrailingPrice = _Price;
          _Price = _Price == double.MinValue || _Price > currentTrailPrice ? currentTrailPrice : _Price;

          if (previousTrailingPrice != _Price && _Debug)
            _Output.WriteLine("{0} Short {1}: TrailingPrice({2})", _Bars.Info.Description, _Bars.Time[0], _Price);

          sx.Send(_Price);
        }
      }
    }
  }

  /// <summary>
  /// Trails the market by the most recent high/low extreme depending on trade direction. 
  /// </summary>
  public class CExitHighLowTrail : CExitStrategy {
    //Properties:
    public int Length { get; set; }
    public int BufferPips { get; set; }

    public override double Risk {
      get {
        return BufferPips;
      }
    }

    //Attributes:
    protected double _TrailPrice;

    public CExitHighLowTrail(CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(CStrategy.EExitStrategy.HighLowTrail, economicEvent, calendar) {
      Length = 10;
      BufferPips = 0;
      _TrailPrice = double.MinValue;
    }

    public override void CalcBar() {
      IOrderPriced lx = null;
      IOrderPriced sx = null;
      _EconomicEvent.OrdersForExit(this, out lx, out sx);

      double currentTrailPrice = double.MinValue;
      double previousHighLowTrailPrice = _TrailPrice;

      if (PositionSide > 0) {
        currentTrailPrice = _Bars.Low.Lowest(Length) - _Calendar.PipValue * BufferPips;
        _TrailPrice = _TrailPrice == double.MinValue || _TrailPrice < currentTrailPrice ? currentTrailPrice : _TrailPrice;
        _TrailPrice = Math.Round(_TrailPrice, _Calendar.CurrencyDecimals);

        lx.Send(_TrailPrice);

        if (_Debug && previousHighLowTrailPrice != _TrailPrice)
          _Output.WriteLine("{0} Long {1}: HighLowTrailPrice({2})", _Bars.Info.Description, _Bars.Time[0], _TrailPrice);

      } else if (PositionSide < 0) {
        currentTrailPrice = _Bars.High.Highest(Length) + _Calendar.PipValue * BufferPips;
        _TrailPrice = _TrailPrice == double.MinValue || _TrailPrice > currentTrailPrice ? currentTrailPrice : _TrailPrice;
        _TrailPrice = Math.Round(_TrailPrice, _Calendar.CurrencyDecimals);

        sx.Send(_TrailPrice);

        if (_Debug && previousHighLowTrailPrice != _TrailPrice)
          _Output.WriteLine("{0} Short {1}: HighLowTrailPrice({2})", _Bars.Info.Description, _Bars.Time[0], _TrailPrice);
      }
    }
  }

  /// <summary>
  /// Places a hard take profit exit order by a number of pips beyond the average entry price.
  /// </summary>
  public class CExitTakeProfit : CExitStrategy {
    //Properties:
    public int Pips { get; set; }

    public override double Risk {
      get {
        //take-profit does not add to the risk of the position
        return 0.0;
      }
    }

    //Attributes:
    protected double _Price;

    public CExitTakeProfit(CEconomicEvent economicEvent, _EconomicCalendar calendar) : base(CStrategy.EExitStrategy.TakeProfit, economicEvent, calendar) {
      _Price = double.MinValue;
    }

    public override void CalcBar() {
      IOrderPriced lx = null;
      IOrderPriced sx = null;
      _EconomicEvent.OrdersForExit(this, out lx, out sx);

      if (PositionSide > 0) {
        if (_Price == double.MinValue) {
          _Price = _Calendar.StrategyInfo.AvgEntryPrice + Pips * _Calendar.PipValue;
          _Price = Math.Round(_Price, _Calendar.CurrencyDecimals);
          if (_Debug)
            _Output.WriteLine("{0} Long {1}: TakeProfit({2})", _Bars.Info.Description, _Bars.Time[0], _Price);
        }

        lx.Send(_Price);

      } else if (PositionSide < 0) {
        if (_Price == double.MinValue) {
          _Price = _Calendar.StrategyInfo.AvgEntryPrice - Pips * _Calendar.PipValue;
          _Price = Math.Round(_Price, _Calendar.CurrencyDecimals);
          if (_Debug)
            _Output.WriteLine("{0} Short {1}: TakeProfit({2})", _Bars.Info.Description, _Bars.Time[0], _Price);
        }

        sx.Send(_Price);
      }
    }
  }
}