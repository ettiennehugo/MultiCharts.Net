using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;
using RiskManagement;
using PowerLanguage;
using StructualTargetPatterns;
using FileHelpers;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Chart Type: Candlestick
  /// 
  /// Hypothesis:
  /// Breaks down the movement of price as Structural Target (ST) Patterns that track the up/down movement of the market around fractal pivot points.
  /// A number of models are developed to control the entry into the market around these patterns and the unfolding up/down meandering of the market. 
  /// From the "Trading Code is Open" Kindle book by Vladimir Poltoratskiy.
  /// 
  /// This strategy is a counter trend strategy that looks for reversal patterns and do not continuation patterns.
  /// 
  /// Ideal Instruments and Timeframes:
  /// EUR/USD, GBP/USD, AUD/USD
  /// 
  /// Indicators:
  /// Price Action Pivots/Fractal Layers, Average Daily Range
  /// 
  /// Entry Rules - Long:
  ///  * We are more than 3 hours away from  significant economic release news, if we are closer we do not open trades but still keep track of the corridors that form so
  ///    that we can enter trades once the economic release is done.
  ///  * Market forms a corridor by forming a fractal high and reverses to form a fractal low.
  ///    - A fractal low point that is not completely formed, meaning a fractal low looks like it's forming but the last bar has not yet closed, can
  ///      be used as a stop-loss and corridor if the new corridor would be in the same direction as the previous corridor that formed. Continuation of
  ///      market momentum is assumed in this case.
  ///  * From the fractal low the market reverses back to break the price level of the fractal high point by a given number of pips.
  ///  * Initial risk is recorded as the size of the corridor, that is, the distance between the fractal high and fractal low point. 
  ///  * The size of the corridor is between prescribed minimum and maximum pips. We do not want to enter trades on insignificant corridors or corridors
  ///    that would include large risk.
  ///  * PARTIALLY FORMED PIVOT LOW AND CORRIDOR: The fractal low for the stop-loss level is partially formed when it looks like a pivot low is forming for the
  ///                                             stop-loss level but the last bar that would constitite the pivot low has not closed yet.
  ///    - A partially formed fractal low point can be used as a stop-loss and corridor if the new corridor would be in the SAME DIRECTION as the previous corridor
  ///      that formed. Continuation of market momentum is assumed in this case.
  ///    - A partially formed fractal low point can be used as a stop-loss and corridor in the OPPOSITE DIRECTION of the previous corridor if the stop-loss level would be 
  ///      in an area of resistance based on a number of bars. For the H1 chart this would include bars for a couple of days and for smaller timeframe charts (e.g. M5) this would
  ///      include about 12 to 30 bars.
  /// 
  /// Entry Rules - Short:
  ///  * We are more than 3 hours away from  significant economic release news, if we are closer we do not open trades but still keep track of the corridors that form so
  ///    that we can enter trades once the economic release is done.
  ///  * Market forms a corridor by forming a fractal low and reverses to form a fractal high.
  ///  * From the fractal high the market reverses back to break the price level of the fractal low point by a give number of pips.
  ///  * Initial risk is recorded as the size of the corridor, that is, the distance between the fractal low and fractal high point.
  ///  * The size of the corridor is between prescribed minimum and maximum pips. We do not want to enter trades on insignificant corridors or corridors
  ///    that would include large risk.
  ///  * PARTIALLY FORMED PIVOT HIGH AND CORRIDOR: The fractal high for the stop-loss level is partially formed when it looks like a pivot high is forming for the
  ///                                              stop-loss level but the last bar that would constitite the pivot high has not closed yet.
  ///    - A partially formed fractal high point can be used as a stop-loss and corridor if the new corridor would be in the SAME DIRECTION as the previous corridor
  ///      that formed. Continuation of market momentum is assumed in this case.
  ///    - A partially formed fractal high point can be used as a stop-loss and corridor in the OPPOSITE DIRECTION of the previous corridor if the stop-loss level would be 
  ///      in an area of support based on a number of bars. For the H1 chart this would include bars for a couple of days and for smaller timeframe charts (e.g. M5) this would
  ///      include about 12 to 30 bars.
  /// 
  /// Exit Rules - Long:
  ///  * Initial stop-loss is placed at the fractal low point minus a specific number of pips specified as parameter to the strategy.
  ///  * Exit profit target is set at 400% of the initial risk taken from the entry price.
  ///  * When market moves up past the 210% value of the initial risk taken the following must occur:
  ///    * The initial stop-loss is moved from the fractal low to the entry price of the trade for a break even stop-loss.
  ///    * When the entry rules for a short position is met the long position is closed and an appropriate short position is opened based on the risk of the new corridor. From this point on
  ///      we expect the market to show strong movement toward the 400% profit target and do not allow it to reverse on us.
  ///    * Large/small fractal corridors are taken into account for this exit.
  /// 
  /// Exit Rules - Short:
  ///  * Initial stop-loss is placed at the fractal high point plus a specific number of pips specified as parameter to the strategy.
  ///  * Exit profit target is set at 400% of the initial risk taken from the entry price.
  ///  * When market moves down past the 210% value of the initial risk taken the following must occur:
  ///    * The initial stop-loss is moved from the fractal high to the entry price of the trade for a break even stop-loss.
  ///    * When the entry rules for a long position is met the short position is closed and an appropriate long position is opened based on the risk of the new corridor. From this point on 
  ///      we expect the market to show strong movement toward the 400% profit target and do not allow it to reverse on us.
  ///    * Large/small fractal corridors are taken into account for this exit.
  ///    
  /// Money Management:
  /// 	Fixed ratio position sizing based on profits.
  /// 
  /// Notes:
  ///   * There are lots of complex combinations of Structural Target patterns that can form in around the given rules see the book for a full discussion around these.
  ///   * [Potential optimization] See how you can incorporate volume price analysis with this strategy, maybe make trades only on high volume price breakouts instead of just using the price breakout.
  ///   * For EURUSD M5 the following should hold:
  ///     - Time filter on 6:00 GMT to 18:00 GMT for EURUSD.
  ///     - Corridor range should be 2 to 13-pips.
  ///     - For EURUSD author advises a 3-pip enter buffer on entry and a 2-pip exit buffer on stop-loss.
  ///   * A similar indicator is discussed in Technical Analysis of Stocks and Commodities Magazine, June 2019 - The 3S Code, p. 40.
  /// 
  /// TODO:
  ///   * Make sure that the activeCorridor picked for processing is always the correct corridor for analysis based on the whole set of corridors found.
  ///     - The author mentions something about corridors that overlap should be ignored and only ones that are completely formed without overlap should be used.
  ///     
  ///   * Additional rules required:
  ///     * When 5 losing trades occur you need to take a break???  
  /// 
  ///     * On intraday timeframes keep track of the Forex calendar and do not trade when upcoming events might cause HIGH volatility -  the author prescribed not trading 3-hours before a significant
  ///       economic indicator is released.
  ///       * Can ignore low and medium volatility events but pay attention to high volatility events.
  ///       * Close any open positions some time before the high volatility events.
  ///       * [Optional] Skip some time window after the high volatility events and then start trading.
  ///       * You still have to keep track of the corridors when you're not trading due to economic events in order to enter trades after the economic event is released.
  ///     * On intraday timeframes stop trading for some time window when you have a specific number of unprofitable trades, e.g. 5 trades.
  /// 
  ///   * IMPORTANT: There seems to be a rule about not opening a trade when it's entry price would fall within the range of a recent corridor that formed. This seems to be important when working
  ///     on intraday timeframes but might be important for interday trading as well. 
  ///   * Avoid corridors that are too small or too large, on M5 for EURUSD 2 pips are too small and 13 pips are too large.
  /// 
  /// </summary>
  public class _StructuralTargetPatterns : SignalObject, IChartCustomDrawer
  {
    //Types:
    public enum EEventVolatility
    {
      None,
      Low,
      Medium,
      High
    }

    /// <summary>
    /// Expected record structure for the CSV file.
    /// </summary>
    [DelimitedRecord(","), IgnoreFirst(), IgnoreEmptyLines()]
    protected class FileEconomicEvent
    {
      public string startDateTime;
      public string currency;
      public string description;
      public string country;
      public string volatility;
      [FieldConverter(ConverterKind.Double), FieldNullValue(0.0)] public double actual;
      [FieldConverter(ConverterKind.Double), FieldNullValue(0.0)] public double previous;
      [FieldConverter(ConverterKind.Double), FieldNullValue(0.0)] public double consensus;
    }

    /// <summary>
    /// Run-time economic event data used for trading.
    /// </summary>
    protected class EconomicEvent
    {
      public DateTime startDateTime;
      public string currency;
      public string description;
      public string country;
      public EEventVolatility volatility;
      public double actual;
      public double previous;
      public double consensus;
    }

    //Constants:
    //NOTE: These defaults are based on the M5 EUR/USD chart.
    public const int DEFAULT_ENTRY_BUFFER_PIPS = 2;
    public const int DEFAULT_EXIT_BUFFER_PIPS = 0;
    public const int DEFAULT_MIN_CORRIDOR_PIPS = 2;
    public const int DEFAULT_MAX_CORRIDOR_PIPS = 13;
    public const string DEFAULT_EVENT_FILE = "C:\\Share\\Trading\\MultiCharts\\Structural Target Patterns\\EURUSD 2018.csv";
    public const EEventVolatility DEFAULT_EVENT_VOLATILITY = EEventVolatility.Medium;
    public const int DEFAULT_EVENT_TIME_WINDOW = 180;            //Based on 3-hour to stop trading before economic event.

    //Inputs:
    [Input] public int EntryBufferPips { get; set; }             //buffer pips at which the entry order is placed beyond a ST corridor
    [Input] public int ExitBufferPips { get; set; }              //buffer pips at which the exit order is placed beyond a ST corridor
    [Input] public int MinCorridorPips { get; set; }             //Minimum size to accept as a corridor
    [Input] public int MaxCorridorPips { get; set; }             //Maximum size to accept as a corridor
    [Input] public int TFStartTime { get; set; }          //time filter start time, e.g. 800 is 8am and 1300 is 1pm		
    [Input] public int TFWindowLength { get; set; }       //time filter window length in minutes, e.g. 60 is 1-hour and 240 is 4-hours
    [Input] public bool EnableEconomicEventFilter { get; set; }  //Enable the economic event filter, no trading before volatile economic news.
    [Input] public string EconomicEventFile { get; set; }        //File containing upcoming economic events.
    [Input] public EEventVolatility EconomicEventVolatility { get; set; } //Least event volatility before which trading should be stopped.
    [Input] public int EconomicEventTimeWindow { get; set; }     //Number of minutes before a crucial economic event to stop trading.
    [Input] public bool ShowCorridors { get; set; }              //Renders the corridors on the chart.
    [Input] public bool Debug { get; set; }                      //flag to enable debug output to the output console

    //Constants:
    const int CROSS_LOOKBACK = 5;
    const int MINI_LOT_SIZE = 1000;                             //mini-lot size used in Forex

    //Types:
    /// <summary>
    /// Storage structure for historical corridors that formed.
    /// </summary>
    protected struct HistoricalCorridor
    {
      public double high;
      public double low;
      public double entryPrice;
      public double stopLossPrice;
      public double target210;
      public double exitTargetPrice;
      public DateTime highBarTime;
      public DateTime lowBarTime;
      public CorridorDirection direction;
    }

    //Attributes:		
    private IOrderPriced _LE;
    private IOrderPriced _SE;
    private IOrderPriced _LE_REVERSAL;
    private IOrderPriced _SE_REVERSAL;
    private IOrderMarket _LX_EVENT_EXIT;
    private IOrderMarket _SX_EVENT_EXIT;
    private IOrderPriced _LX_SL;
    private IOrderPriced _SX_SL;
    private IOrderPriced _LX_TP;
    private IOrderPriced _SX_TP;
    private List<Corridor> _corridors;
    private List<HistoricalCorridor> _historicalCorridors;
    private bool _tradeProcessed;
    private int _corridorId;
    private Corridor _activeCorridor;
    private Corridor _reversalCorridor;
    private List<EconomicEvent> _economicEvents;
    private TimeSpan _economicEventTimeWindow;
    private bool _enableTrading;
    private int _previousPositionSide;
    private TimeSpan _StartTime;
    private TimeSpan _EndTime;

    private Color _entryColor { get; set; }
    private Color _exitColor { get; set; }
    private Color _target210Color { get; set; }
    private Color _exitTargetColor { get; set; }
    private Color _tradeColor { get; set; }
    private Color _eventTextColor { get; set; }
    private int _eventTextFontSize { get; set; }

    public _StructuralTargetPatterns(object _ctx) : base(_ctx)
    {
      //default input parameters
      EntryBufferPips = DEFAULT_ENTRY_BUFFER_PIPS;
      ExitBufferPips = DEFAULT_EXIT_BUFFER_PIPS;
      MinCorridorPips = DEFAULT_MIN_CORRIDOR_PIPS;
      MaxCorridorPips = DEFAULT_MAX_CORRIDOR_PIPS;
      TFStartTime = 0;        //12:00am
      TFWindowLength = 1439;  //minutes in a full 24-hour trading day
      EnableEconomicEventFilter = true;
      EconomicEventFile = DEFAULT_EVENT_FILE;
      EconomicEventVolatility = DEFAULT_EVENT_VOLATILITY;
      EconomicEventTimeWindow = DEFAULT_EVENT_TIME_WINDOW;
      ShowCorridors = true;
      Debug = false;

      //default internal variables used to render corridors
      _entryColor = Color.LightGreen;
      _exitColor = Color.LightSalmon;
      _target210Color = Color.LightGray;
      _exitTargetColor = Color.Green;
      _tradeColor = Color.LightGray;
      _eventTextColor = Color.AntiqueWhite;
      _eventTextFontSize = 8;
    }

    protected override void Create()
    {
      ChartCustomDraw.Register(this);

      //create the order types used
      _LE = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LE", EOrderAction.Buy));
      _SE = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SE", EOrderAction.SellShort));
      _LE_REVERSAL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LERV", EOrderAction.Buy));
      _SE_REVERSAL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SERV", EOrderAction.SellShort));
      _LX_EVENT_EXIT = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "EVLX", EOrderAction.Sell));
      _SX_EVENT_EXIT = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "EVSX", EOrderAction.BuyToCover));
      _LX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SLLX", EOrderAction.Sell));
      _SX_SL = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SLSX", EOrderAction.BuyToCover));
      _LX_TP = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "TPLX", EOrderAction.Sell));
      _SX_TP = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "TPSX", EOrderAction.BuyToCover));

      //create collections of objects used
      _corridors = new List<Corridor>();
      _historicalCorridors = new List<HistoricalCorridor>();
      _economicEvents = new List<EconomicEvent>();
    }

    protected override void Destroy()
    {
      ChartCustomDraw.Unregister(this);
    }

    protected override void StartCalc()
    {
      //clear the debugging console
      if (Debug)
        Output.Clear();

      //clear the collections used
      _corridors.Clear();
      _historicalCorridors.Clear();
      _economicEvents.Clear();

      FileHelperEngine<FileEconomicEvent> parser = new FileHelperEngine<FileEconomicEvent>();
      FileEconomicEvent[] lines = parser.ReadFile(EconomicEventFile);

      foreach (FileEconomicEvent line in lines)
      {
        EconomicEvent economicEvent = new EconomicEvent();
        economicEvent.volatility = (EEventVolatility)Enum.Parse(typeof(EEventVolatility), line.volatility);

        //filter events with less expected volatility than volatility used to disable trading
        if (economicEvent.volatility < EconomicEventVolatility) continue;

        economicEvent.startDateTime = DateTime.Parse(line.startDateTime);
        economicEvent.currency = line.currency;
        economicEvent.description = line.description;
        economicEvent.country = line.country;
        economicEvent.actual = line.actual;
        economicEvent.previous = line.previous;
        economicEvent.consensus = line.consensus;
        _economicEvents.Add(economicEvent);
      }

      //set the start/end times for the time filter
      _StartTime = new TimeSpan(TFStartTime / 100, TFStartTime % 100, 0);
      _EndTime = _StartTime + new TimeSpan(TFWindowLength / 60, TFWindowLength % 60, 0);

      //NOTE: If the start/end time goes over multiple days we remove the days part since we're only interested in the time of day
      //      trading should occur. Comparison of TimeSpan objects do not work correctly when days are added to the end time.
      if (_EndTime.Days > 0)
        _EndTime = _EndTime.Subtract(new TimeSpan(_EndTime.Days, 0, 0, 0));

      //reset general strategy state
      _corridorId = 1;
      _tradeProcessed = false;
      _economicEventTimeWindow = new TimeSpan(0, EconomicEventTimeWindow, 0);
      _enableTrading = true;
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

    private void CalcBarDefault()
    {


      //DEBUG: Breakpoint for debugging specific bars.
      //if (Bars.Time[0] == DateTime.Parse("4/9/2018 10:20:00"))
      //  Output.Write("Break");


      //update general indicators
      if (EnableEconomicEventFilter) updateEnabledState();

      //process the entry/exit logic
      if (PositionSide == 0)
        calcBarEntry();
      else
        calcBarExit();
    }

    private void calcBarEntry()
    {
      //record previous position side used in state changes
      _previousPositionSide = PositionSide;

      //perform normal processing
      //NOTE: If a trade was entered and closed and this is the first bar after the trade was closed we need to filter out corridors
      //      whose entry price is beyond the current price level. Additionally, the standard processing of the corridors will filter
      //      out the corridors that are not considered valid. Since the backtesting engine does not send "order filled" messages we
      //      need to capture this event implicitly and clear the corridor in this event!!!
      if (Bars.Status == EBarState.Close && _tradeProcessed)
      {
        _tradeProcessed = false;

        //output debug information
        if (Debug)
          Output.WriteLine("Trade complete: Date({0})", Bars.Time[0].ToString());

        List<Corridor> corridorsToKeep = new List<Corridor>(_corridors.Count);

        foreach (Corridor c in corridorsToKeep)
          if (c.Direction == CorridorDirection.Bullish && c.EntryPrice > Bars.Close[0])
            corridorsToKeep.Add(c);
          else if (c.Direction == CorridorDirection.Bearish && c.EntryPrice < Bars.Close[0])
            corridorsToKeep.Add(c);

        _corridors.Clear();
        _corridors.AddRange(corridorsToKeep);
      }

      //process the set of defined corridors 
      processCorridors();

      //add new corridor if required
      addNewCorridor();

      //process current active corridor
      if (Bars.Status == EBarState.Close && _corridors.Count > 0)
      {
        //always select the last found and valid corridor for processing as the current active corridor
        _activeCorridor = null;
        for (int i = _corridors.Count - 1; i > 0; i--)
        {
          Corridor c = _corridors[i];
          if (c.Found && c.IsValid)
          {
            _activeCorridor = c;
            break;    //terminate search loop for most recent active corridor
          }
        }
      }

      //apply time filter for entries
      //NOTE: Time filter is implemented in local time so backtests and live trading should occur in local time. Additionally,
      //      start and end time is inclusive in range so that start and end minutes are both included for processing.                
      DateTime currentTime = Bars.Time.Value;

      if (((_StartTime < _EndTime) && (_StartTime <= currentTime.TimeOfDay) && (currentTime.TimeOfDay <= _EndTime)) ||    //start/end time fall in the same trading day
          ((_StartTime > _EndTime) && ((_StartTime <= currentTime.TimeOfDay) || (currentTime.TimeOfDay <= _EndTime))))
      {  
        //send orders for selected active corridor
        if (_enableTrading && _activeCorridor != null)
          switch (_activeCorridor.Direction)
          {
            case CorridorDirection.Bullish:
              _LE.Send(_activeCorridor.EntryPrice);
              break;
            case CorridorDirection.Bearish:
              _SE.Send(_activeCorridor.EntryPrice);
              break;
          }
      }
    }

    private void calcBarExit()
    {
      //set flag that trade was processed and clear all corridors found so far
      //that were not filled
      if (_tradeProcessed == false)
      {
        _corridors.Clear();
        _corridors.Add(_activeCorridor);
        _tradeProcessed = true;

        //output debug information
        if (Debug)
          Output.WriteLine("Trade entered: Id({0}) Date({1}) Direction({2})", _activeCorridor.Id, Bars.Time[0].ToString(), _activeCorridor.Direction.ToString());
      }

      //process the current set of defined corridors
      processCorridors();

      //add new corridor if required
      addNewCorridor();

      //flatten position if we entered the timewindow before an economic event that could cause volatility
      if (!_enableTrading)
      {
        if (PositionSide > 0) _LX_EVENT_EXIT.Send();
        if (PositionSide < 0) _SX_EVENT_EXIT.Send();
      }
      else
      {
        //swap active corridor and reversal corridor if position direction changed
        if (_previousPositionSide != 0 && _previousPositionSide != PositionSide)
        {
          //reversal trade was entered, apply state change to send orders for reversal
          _activeCorridor = _reversalCorridor;

          if (Debug)
            Output.WriteLine("Trade reversal entered: Id({0}) Direction({1}) FirstPivot({2}, {3}) SecondPivot({4}, {5}) Entry({6}) StopLoss({7}) Risk({8}) 210%-Price({9}) Target({10})",
                              _reversalCorridor.Id,
                              _reversalCorridor.Direction.ToString(),
                              _reversalCorridor.FirstPivotBarTime.ToString(), _reversalCorridor.Direction == CorridorDirection.Bullish ? _reversalCorridor.HighPivotPrice : _reversalCorridor.LowPivotPrice,
                              _reversalCorridor.SecondPivotBarTime.ToString(), _reversalCorridor.Direction == CorridorDirection.Bullish ? _reversalCorridor.LowPivotPrice : _reversalCorridor.HighPivotPrice,
                              _reversalCorridor.EntryPrice,
                              _reversalCorridor.StopLossPrice,
                              _reversalCorridor.Risk,
                              _reversalCorridor.PriceLevel210,
                              _reversalCorridor.ExitTargetPrice);

          _reversalCorridor = null;
        }

        //record previous position side used in state changes
        _previousPositionSide = PositionSide;

        //manage orders for current active corridor
        if (PositionSide > 0)
        {
          _LX_SL.Send(_activeCorridor.StopLossPrice);
          _LX_TP.Send(_activeCorridor.ExitTargetPrice);
        }

        if (PositionSide < 0)
        {
          _SX_SL.Send(_activeCorridor.StopLossPrice);
          _SX_TP.Send(_activeCorridor.ExitTargetPrice);
        }

        //scan for corridors in the opposite direction if required
        if (Bars.Status == EBarState.Close && _activeCorridor.ScanForNextCorridor)
        {
          //remove other corridors that do not fall within the potential reversal zone of the active corridor        
          List<Corridor> corridorsToKeep = new List<Corridor>(_corridors.Count);
          corridorsToKeep.Add(_activeCorridor);    //always keep the active corridor being processed
          foreach (Corridor c in _corridors)
            if (_activeCorridor != c)
              if (c.Found && _activeCorridor.IsBeyond210PriceLevel(c))
                corridorsToKeep.Add(c);
              else if (!c.Found)
                corridorsToKeep.Add(c);

          _corridors.Clear();
          _corridors.AddRange(corridorsToKeep);

          //try to find a corridor in the opposite direction from the current active corridor
          Corridor oppositeCorridor = null;
          foreach (Corridor c in _corridors)
            if (c != _activeCorridor && c.Found && _activeCorridor.OppositeDirection == c.Direction)
            {
              oppositeCorridor = c;
              break;  //we process the first opposite corridor found, terminate search loop
            }

          //setup reversal corridor if required
          if (oppositeCorridor == null)
            _reversalCorridor = null;   //clear previous reversal corridor since no opposite direction corridor found
          else
          {
            if (_reversalCorridor == null)
            {
              _reversalCorridor = oppositeCorridor;

              //output debug information
              if (Debug)
                Output.WriteLine("Corridor reversal FOUND: Id({0}) Direction({1}) FirstPivot({2}, {3}) SecondPivot({4}, {5}) Entry({6}) StopLoss({7}) Risk({8}) 210%-Price({9}) Target({10})",
                                  oppositeCorridor.Id,
                                  oppositeCorridor.Direction.ToString(),
                                  oppositeCorridor.FirstPivotBarTime.ToString(), oppositeCorridor.Direction == CorridorDirection.Bullish ? oppositeCorridor.HighPivotPrice : oppositeCorridor.LowPivotPrice,
                                  oppositeCorridor.SecondPivotBarTime.ToString(), oppositeCorridor.Direction == CorridorDirection.Bullish ? oppositeCorridor.LowPivotPrice : oppositeCorridor.HighPivotPrice,
                                  oppositeCorridor.EntryPrice,
                                  oppositeCorridor.StopLossPrice,
                                  oppositeCorridor.Risk,
                                  oppositeCorridor.PriceLevel210,
                                  oppositeCorridor.ExitTargetPrice);

            }
            else if (_reversalCorridor.FirstPivotBarTime != oppositeCorridor.FirstPivotBarTime ||
                     _reversalCorridor.SecondPivotBarTime != oppositeCorridor.SecondPivotBarTime)
            {
              _reversalCorridor = oppositeCorridor;

              //output debug information
              if (Debug)
                Output.WriteLine("Corridor reversal ADJUSTED: Id({0}) Direction({1}) FirstPivot({2}, {3}) SecondPivot({4}, {5}) Entry({6}) StopLoss({7}) Risk({8}) 210%-Price({9}) Target({10})",
                                  oppositeCorridor.Id,
                                  oppositeCorridor.Direction.ToString(),
                                  oppositeCorridor.FirstPivotBarTime.ToString(), oppositeCorridor.Direction == CorridorDirection.Bullish ? oppositeCorridor.HighPivotPrice : oppositeCorridor.LowPivotPrice,
                                  oppositeCorridor.SecondPivotBarTime.ToString(), oppositeCorridor.Direction == CorridorDirection.Bullish ? oppositeCorridor.LowPivotPrice : oppositeCorridor.HighPivotPrice,
                                  oppositeCorridor.EntryPrice,
                                  oppositeCorridor.StopLossPrice,
                                  oppositeCorridor.Risk,
                                  oppositeCorridor.PriceLevel210,
                                  oppositeCorridor.ExitTargetPrice);
            }
          }

          //send orders for reversal orders if required
          if (_reversalCorridor != null)
          {
            //send orders for the position reveral
            int positionSize = 0;
            switch (_reversalCorridor.Direction)
            {
              case CorridorDirection.Bullish:
                positionSize = Positions[0].OpenLots * 2;      //TODO: Correct position sizing, for reversal you need to offset the current position AND setup new position size.
                _LE_REVERSAL.Send(_reversalCorridor.EntryPrice, positionSize);
                break;
              case CorridorDirection.Bearish:
                positionSize = Positions[0].OpenLots * 2;      //TODO: Correct position sizing, for reversal you need to offset the current position AND setup new position size.
                _SE_REVERSAL.Send(_reversalCorridor.EntryPrice, positionSize);
                break;
            }
          }
        }
      }
    }

    /// <summary>
    /// Finds the next upcoming economic event and disables trading when the event
    /// is within the timeframe for which trading should be disabled.
    /// </summary>
    private void updateEnabledState()
    {
      //per default assume that trading will be enabled
      _enableTrading = true;

      //find next economic event, that is the first event with a timestamp larger than the current bar time
      EconomicEvent nextEvent = null;
      for (int i = 0; i < _economicEvents.Count; i++)
      {
        EconomicEvent currentEvent = _economicEvents[i];

        if (Bars.Time[0] < currentEvent.startDateTime)
        {
          nextEvent = currentEvent;
          break;    //terminate search loop
        }
      }

      //enable/disable trading based on whether current bar lies beyond the time window used to 
      //disable trading before major economic event news
      if (nextEvent != null)
        _enableTrading = Bars.Time[0] < (nextEvent.startDateTime - _economicEventTimeWindow);
    }

    /// <summary>
    /// Adds a new corridor if required, a new corridor should be added when the corridor list is empty or the last
    /// defined corridor entered the "found"-state.
    /// </summary>
    /// <returns>True if new corridor was added, false otherwise.</returns>
    private bool addNewCorridor()
    {
      bool result = false;

      if (Bars.CurrentBar > 3 && Bars.Status == EBarState.Close)
        //handle special case where we don't have a corridor created yet
        if (_corridors.Count == 0)
          result = true;
        else
        {
          Corridor c = _corridors.Last();
          result = c.Found;

          //add the corridor to the set of historical corridors if we're displaying the corridors and the corridor is complete
          if (ShowCorridors && c.Found)
          {
            HistoricalCorridor h = new HistoricalCorridor();
            h.direction = c.Direction;
            h.low = c.LowPivotPrice;
            h.high = c.HighPivotPrice;
            h.entryPrice = c.EntryPrice;
            h.stopLossPrice = c.StopLossPrice;
            h.target210 = c.PriceLevel210;
            h.exitTargetPrice = c.ExitTargetPrice;

            if (h.direction == CorridorDirection.Bullish)
            {
              //bullish corridor has first pivot as the low and second pivot as the high
              h.highBarTime = c.FirstPivotBarTime;
              h.lowBarTime = c.SecondPivotBarTime;
            }
            else
            {
              //bearish corridor has first pivot as the high and second pivot as the low
              h.lowBarTime = c.FirstPivotBarTime;
              h.highBarTime = c.SecondPivotBarTime;
            }

            _historicalCorridors.Add(h);
          }
        }

      //create new corridor if required
      if (result)
      {
        Corridor newCorridor = new Corridor(_corridorId.ToString(), this, CorridorDirection.Any);
        _corridorId++;

        newCorridor.EntryBufferPips = EntryBufferPips;
        newCorridor.ExitBufferPips = ExitBufferPips;
        newCorridor.MinCorridorPips = MinCorridorPips;
        newCorridor.MaxCorridorPips = MaxCorridorPips;
        newCorridor.Debug = Debug;

        //always allow a bi-directional scan of the pivot points and corridors by starting a scan from the
        //resume-scan-bar of the previously formed corridor
        if (_corridors.Count == 0)
          newCorridor.LastScanBar = Bars.Time[0];
        else
        {
          newCorridor.LastScanBar = _corridors.Last().ResumeScanBar;
        }

        _corridors.Add(newCorridor);
      }

      return result;
    }

    /// <summary>
    /// Process the current set of corridors and remove corridors no longer valid.
    /// </summary>
    private void processCorridors()
    {
      //process current defined corridors
      if (Bars.Status == EBarState.Close)
      {
        //process the set of corridors
        foreach (Corridor c in _corridors)
          c.CalcBar();

        //remove the set of invalid corridors
        List<Corridor> corridorsToKeep = new List<Corridor>(_corridors.Count);
        foreach (Corridor c in _corridors)
          if (c.IsValid)
            corridorsToKeep.Add(c);

        _corridors.Clear();
        _corridors.AddRange(corridorsToKeep);
      }
    }

    void IChartCustomDrawer.Draw(DrawContext context, EDrawPhases phase)
    {
      if (ShowCorridors && phase == EDrawPhases.Final)
      {
        using (Pen entryPen = new Pen(_entryColor, 3))
        using (Pen exitPen = new Pen(_exitColor, 3))
        using (Pen entryPricePen = new Pen(_entryColor, 1))
        using (Pen stopLossPricePen = new Pen(_exitColor, 1))
        using (Pen target210Pen = new Pen(_target210Color, 1))
        using (Pen exitTargetPen = new Pen(_exitTargetColor, 3))
        using (Pen tradePen = new Pen(_tradeColor, 1))
        using (Font textFont = new Font("Arial", _eventTextFontSize, FontStyle.Regular))
        using (StringFormat textFormat = new StringFormat(StringFormatFlags.DirectionVertical | StringFormatFlags.NoWrap))
        using (Brush textBrush = new SolidBrush(_eventTextColor))
        {
          //draw entry/exit prices with dashed styles
          entryPricePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
          stopLossPricePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

          //render the necessary historical corridors
          TimeSpan ts = Bars.Info.Resolution.Duration();

          foreach (HistoricalCorridor h in _historicalCorridors)
          {
            //determine entry and exit points for historical bar lines
            ChartPoint entryStartCP;
            ChartPoint entryEndCP;
            ChartPoint entryPriceStartCP;
            ChartPoint entryPriceEndCP;
            ChartPoint exitStartCP;
            ChartPoint exitEndCP;
            ChartPoint stopLossPriceStartCP;
            ChartPoint stopLossPriceEndCP;
            ChartPoint target210PriceStartCP;
            ChartPoint target210PriceEndCP;
            ChartPoint exitTargetPriceStartCP;
            ChartPoint exitTargetPriceEndCP;

            DateTime startTime;
            DateTime endTime;

            if (h.direction == CorridorDirection.Bullish)
            {
              //high pivot bar formed first and then second pivot bar formed at the low
              //draw line from high bar date to low bar date
              startTime = h.highBarTime - ts;
              endTime = h.lowBarTime + ts;

              entryStartCP = new ChartPoint(startTime, h.high);
              entryEndCP = new ChartPoint(endTime, h.high);
              entryPriceStartCP = new ChartPoint(startTime, h.entryPrice);
              entryPriceEndCP = new ChartPoint(endTime, h.entryPrice);

              exitStartCP = new ChartPoint(startTime, h.low);
              exitEndCP = new ChartPoint(endTime, h.low);
              stopLossPriceStartCP = new ChartPoint(startTime, h.stopLossPrice);
              stopLossPriceEndCP = new ChartPoint(endTime, h.stopLossPrice);

            }
            else
            {
              //low pivot bar formed first and then the second pivot bar formed at the high
              //draw line from low bar date to high bar date
              startTime = h.lowBarTime - ts;
              endTime = h.highBarTime + ts;

              entryStartCP = new ChartPoint(startTime, h.low);
              entryEndCP = new ChartPoint(endTime, h.low);
              entryPriceStartCP = new ChartPoint(startTime, h.entryPrice);
              entryPriceEndCP = new ChartPoint(endTime, h.entryPrice);

              exitStartCP = new ChartPoint(startTime, h.high);
              exitEndCP = new ChartPoint(endTime, h.high);
              stopLossPriceStartCP = new ChartPoint(startTime, h.stopLossPrice);
              stopLossPriceEndCP = new ChartPoint(endTime, h.stopLossPrice);
            }

            //set target 210% and exit target prices
            target210PriceStartCP = new ChartPoint(startTime, h.target210);
            target210PriceEndCP = new ChartPoint(endTime, h.target210);
            exitTargetPriceStartCP = new ChartPoint(startTime, h.exitTargetPrice);
            exitTargetPriceEndCP = new ChartPoint(endTime, h.exitTargetPrice);

            //get points on the screen
            PointF entryStartP = context.Environment.ChartPoint2Point(entryStartCP);
            PointF entryEndP = context.Environment.ChartPoint2Point(entryEndCP);
            PointF exitStartP = context.Environment.ChartPoint2Point(exitStartCP);
            PointF exitEndP = context.Environment.ChartPoint2Point(exitEndCP);

            PointF entryPriceStartP = context.Environment.ChartPoint2Point(entryPriceStartCP);
            PointF entryPriceEndP = context.Environment.ChartPoint2Point(entryPriceEndCP);
            PointF stopLossPriceStartP = context.Environment.ChartPoint2Point(stopLossPriceStartCP);
            PointF stopLossPriceEndP = context.Environment.ChartPoint2Point(stopLossPriceEndCP);

            PointF target210PriceStartP = context.Environment.ChartPoint2Point(target210PriceStartCP);
            PointF target210PriceEndP = context.Environment.ChartPoint2Point(target210PriceEndCP);
            PointF exitTargetPriceStartP = context.Environment.ChartPoint2Point(exitTargetPriceStartCP);
            PointF exitTargetPriceEndP = context.Environment.ChartPoint2Point(exitTargetPriceEndCP);

            //render corridor lines if required
            if (context.DirtyRect.Contains(entryStartP) || context.DirtyRect.Contains(entryEndP))
              context.graphics.DrawLine(entryPen, entryStartP, entryEndP);

            if (context.DirtyRect.Contains(exitStartP) || context.DirtyRect.Contains(exitEndP))
              context.graphics.DrawLine(exitPen, exitStartP, exitEndP);

            //render target 210% and exit target lines if they are in the dirty rectangle
            if (context.DirtyRect.Contains(target210PriceStartP) || context.DirtyRect.Contains(target210PriceEndP))
              context.graphics.DrawLine(target210Pen, target210PriceStartP, target210PriceEndP);

            if (context.DirtyRect.Contains(exitTargetPriceStartP) || context.DirtyRect.Contains(exitTargetPriceEndP))
              context.graphics.DrawLine(exitTargetPen, exitTargetPriceStartP, exitTargetPriceEndP);

            //render entry/stop-loss price lines if they are not at the same level as the corridor price lines
            if ((entryStartP.Y != entryPriceStartP.Y) && (context.DirtyRect.Contains(entryPriceStartP) || context.DirtyRect.Contains(entryPriceEndP)))
              context.graphics.DrawLine(entryPricePen, entryPriceStartP, entryPriceEndP);

            if ((exitEndP.Y != stopLossPriceStartP.Y) && (context.DirtyRect.Contains(stopLossPriceStartP) || context.DirtyRect.Contains(stopLossPriceEndP)))
              context.graphics.DrawLine(stopLossPricePen, stopLossPriceStartP, stopLossPriceEndP);

            //render trade connector line to connect all levels together
            if (context.DirtyRect.Contains(stopLossPriceStartP) || context.DirtyRect.Contains(exitTargetPriceStartP))
              context.graphics.DrawLine(tradePen, stopLossPriceStartP, exitTargetPriceStartP);
          }

          //render economic event text
          string eventText = "";
          DateTime eventDateTime = _economicEvents.Count > 0 ? _economicEvents[0].startDateTime : DateTime.MinValue;
          foreach (EconomicEvent e in _economicEvents)
          {
            //render events if this new event no longer fall on the time of the previous event
            if (e.startDateTime != eventDateTime && eventText.Length > 0)
            {
              //find the bar where the event text should be rendered
              foreach (Bar b in context.Environment.Bars.All)
                if (b.Time == eventDateTime)
                {
                  //update the location of the text in the window client area
                  RectangleF textRect = new RectangleF();
                  ChartPoint startChartPoint = new ChartPoint(eventDateTime, b.Low);
                  PointF startPoint = context.Environment.ChartPoint2Point(startChartPoint);
                  SizeF textSize = context.graphics.MeasureString(eventText, textFont);
                  startPoint.X -= textSize.Height / 2;
                  textRect.Location = startPoint;

                  //NOTE: We render the text vertically so the width and height is swapped
                  textRect.Height = textSize.Width;
                  textRect.Width = textSize.Height;

                  //render the text if it overlaps the dirty rectangle
                  if (context.DirtyRect.IntersectsWith(textRect))
                    context.graphics.DrawString(eventText, textFont, textBrush, textRect.Location, textFormat);

                  break;  //terminate search loop for bar
                }

              //clear the rendered event text and start accumulating new values for text
              eventText = "";
              eventDateTime = e.startDateTime;
            }

            //accumulate events that fall on the same date/time
            if (e.startDateTime == eventDateTime)
            {
              if (eventText == "")
                eventText = e.description;
              else
                eventText += ", " + e.description;
            }
          }

        }
      }
    }

    private void orderFilled()
    {
      //set flag that trade was processed and clear all corridors found so far
      //that were not filled
      if (_tradeProcessed == false)
      {
        _corridors.Clear();
        _corridors.Add(_activeCorridor);
        _tradeProcessed = true;

        //output debug information
        if (Debug)
          Output.WriteLine("Trade entered: Id({0}) Date({1}) Direction({2})", _activeCorridor.Id, Bars.Time[0].ToString(), _activeCorridor.Direction.ToString());
      }

      //send initial stop-loss when an entry order is filled
      if (PositionSide > 0)
      {
        _LX_SL.Send(_activeCorridor.StopLossPrice);
        _LX_TP.Send(_activeCorridor.ExitTargetPrice);
      }

      if (PositionSide < 0)
      {
        _SX_SL.Send(_activeCorridor.StopLossPrice);
        _SX_TP.Send(_activeCorridor.ExitTargetPrice);
      }
    }

    protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price)
    {
      orderFilled();
    }

    private void CalcBarOrderFilled()
    {
      orderFilled();
    }
  }
}

///===================================================================================================================
///===================================================================================================================
///===================================================================================================================
///===================================================================================================================
/// <summary>
/// Common classes used for the Structural Target patterns.
/// </summary>
namespace StructualTargetPatterns
{
  /// <summary>
  /// Enumerates the direction a corridor would enter, also used to limit the entry direction for a corridor if required.
  /// </summary>
  public enum CorridorDirection
  {
    Any,      //enter in first detected direction
    Bullish,  //enter only in the busllish direction
    Bearish,  //enter only in the bearish direction
  }

  /// <summary>
  /// Generic implementation to handle processing of fractal corridors.
  /// </summary>
  public class Corridor
  {
    //Constants:
    public int MIN_CORRIDOR_PIPS = 5;
    public int MAX_CORRIDOR_PIPS = 20;

    //Properties:
    public int EntryBufferPips { get; set; }
    public int ExitBufferPips { get; set; }
    public int MinCorridorPips { get; set; }                                    //Sets the limit for the minimum corridor size
    public int MaxCorridorPips { get; set; }                                    //Sets the limit for the maximum corridor size
    public DateTime LastScanBar { get; set; }                                   //Last bar to scan for corridor
    public string Id { get { return _id; } }
    public bool Found { get { return _found; } }                                //Was a corridor found by scanner?
    public bool IsValid { get { return _isValid; } }                            //Given current price movement can this corridor still merit a trade entry?
    public CorridorDirection Direction { get { return _direction; } }           //Market direction in which the corridor would operate.

    public CorridorDirection OppositeDirection {
      get {
        CorridorDirection result = CorridorDirection.Any;
        switch (_direction)
        {
          case CorridorDirection.Bullish:
            result = CorridorDirection.Bearish;
            break;
          case CorridorDirection.Bearish:
            result = CorridorDirection.Bullish;
            break;
        }

        return result;
      }
    }

    public DateTime FirstPivotBarTime { get { return _firstPivotBarTime; } }    //First pivot bar time for the corridor
    public DateTime SecondPivotBarTime { get { return _secondPivotBarTime; } }  //Second pivot bar time for the corridor
    public DateTime ResumeScanBar { get { return _resumeScanBar; } }            //Returns the date/time from which to scan for a subsequent corridor if this corridor is invalidated or no longer useable, see where this is used.
    public double LowPivotPrice { get { return _lowPivotPrice; } }
    public double HighPivotPrice { get { return _highPivotPrice; } }
    public double Height { get { return _height; } }                            //Returns the height of the corridor, that is, the distance between the pivot low and high.
    public double StopLossPrice { get { return _stopLossPrice; } }
    public double EntryPrice { get { return _entryPrice; } }
    public double Risk { get { return _risk; } }                               //Risk in terms of the price scale between entry price and stop-loss.
    public double PriceLevel210 { get { return _priceLevel210; } }
    public double ExitTargetPrice { get { return _exitTargetPrice; } }
    public bool ScanForNextCorridor { get { return _priceBroke210Level; } }  //Returns whether the strategy should scan for the next corridor
    public bool Debug { get; set; }

    //Attributes:
    protected string _id;
    protected CStudyAbstract _parent;
    protected CorridorDirection _direction;
    protected bool _found;        //set when a corridor was found in scanning, below price values would be initialized
    protected bool _isValid;      //set when price movement is still within the bounds of the corridor and entry price it not yet broken
    protected double _pipValue;
    protected DateTime _firstPivotBarTime;
    protected DateTime _secondPivotBarTime;
    protected DateTime _resumeScanBar;
    protected double _lowPivotPrice;
    protected double _highPivotPrice;
    protected double _height;
    protected double _stopLossPrice;
    protected double _entryPrice;
    protected double _risk;
    protected double _priceLevel210;
    protected double _exitTargetPrice;
    protected bool _priceBroke210Level;   //flag set when price moved beyond the 210% level

    //Methods:
    public Corridor(string id, CStudyAbstract parent, CorridorDirection direction = CorridorDirection.Any)
    {
      _id = id;
      _parent = parent;
      _direction = direction;
      EntryBufferPips = 0;
      ExitBufferPips = 0;
      LastScanBar = DateTime.MinValue;    //per default we'll scan all bars 
      _resumeScanBar = DateTime.MinValue; //and resume for the first bar as well
      _found = false;
      _isValid = true;  //corridor starts out as "valid" even if it was not found
      MinCorridorPips = MIN_CORRIDOR_PIPS;
      MaxCorridorPips = MAX_CORRIDOR_PIPS;
      _lowPivotPrice = -1;
      _highPivotPrice = -1;
      _height = -1;
      _stopLossPrice = -1;
      _entryPrice = -1;
      _risk = 0;
      _priceLevel210 = -1;
      _exitTargetPrice = -1;
      _priceBroke210Level = false;
      Debug = false;

      //standarize the buffer pip value for the price scale we're trading
      //NOTE: We multiply by 10 since the point value is always a tenth of a PIP.
      _pipValue = _parent.Bars.Info.PointValue * 10 * _parent.Bars.Info.BigPointValue;
    }

    /// <summary>
    /// Performs the bar calculation to scan for a corridor.
    /// </summary>
    public void CalcBar()
    {
      //scan for possible pivots for corridor
      if (!_found && _parent.Bars.CurrentBar > 3)
      {
        int firstPivotBarIndex = -1;
        int secondPivotBarIndex = -1;
        CorridorDirection detectedDirection = CorridorDirection.Any;

        //we start ONE bar back to determine the pivots
        for (int i = 1; i < _parent.Bars.CurrentBar; i++)
        {
          //terminate scan if this is the last scan bar OR we have found
          //the two pivot points for the corridor
          if ((_parent.Bars.Time[i] < LastScanBar) ||
              (firstPivotBarIndex != -1 && secondPivotBarIndex != -1))
            break;

          //detect pivots based on the detection direction
          switch (_direction)
          {
            case CorridorDirection.Any:
              //detect second pivot if not yet set
              if (secondPivotBarIndex == -1)
              {
                if (isPivotLow(_parent.Bars, i))
                {
                  secondPivotBarIndex = i;
                  //since second pivot is a low the first pivot must be a high for a bullish break
                  detectedDirection = CorridorDirection.Bullish;
                }
                else if (isPivotHigh(_parent.Bars, i))
                {
                  secondPivotBarIndex = i;
                  //since second pivot is a high so the first pivot must be a low for a bearish break
                  detectedDirection = CorridorDirection.Bearish;
                }
              }

              //detect first pivot if second pivot was detected
              //NOTE: Detection bar index used for pivot must NOT be the same as second pivot bar index to ensure we
              //      do not create a corridor using the extremes of a single bar.
              if (secondPivotBarIndex != -1 && firstPivotBarIndex == -1)
              {
                //determine whether the current bar is a pivot low OR high, the following applies:
                // 1. if we detect a pivot of the opposite type of the second pivot we have a first pivot index
                // 2. if rule 1 hold then pivot extreme value of first index bar must be beyond the low/high of the second pivot index bar as well, this
                //    ensures that we do not use engulfing bars are pivot points
                // 3. if we determine a pivot of the type of the second pivot index we simply adjust the second pivot index to whichever is the most extreme
                if (detectedDirection == CorridorDirection.Bullish)
                {
                  //perform first index pivot bar detection for a bullish corridor
                  if (isPivotHigh(_parent.Bars, i))
                  {
                    //rule 1 holds - we have a pivot in the opposite direction of the second index pivot which is a pivot low 
                    //check whether rule 2 holds as well and set first index if that is the case - the LOW of the second index
                    //pivot bar should be lower than the LOW of the first index bar so that the corridor is properly formed
                    if (i != secondPivotBarIndex && _parent.Bars.Low[secondPivotBarIndex] < _parent.Bars.Low[i])
                      firstPivotBarIndex = i;

                  }
                  else if (isPivotLow(_parent.Bars, i) &&
                           _parent.Bars.Low[i] < _parent.Bars.Low[secondPivotBarIndex])
                  {
                    //rule 3 holds - we have a pivot in the same direction of the second index pivot simply adjust the second pivot back
                    secondPivotBarIndex = i;
                  }

                }
                else
                {
                  //perfrom first index pivot bar detection for a bearish corridor
                  if (isPivotLow(_parent.Bars, i))
                  {
                    //rule 1 holds - we have a pivot in the opposite direction of the second index pivot which is a pivot high
                    //check whether rule 2 holds as well as set first index if that is the case - the HIGH of the second index
                    //pivot bar should be higher than the HIGH of the first index bar so that the corridor is properly formed
                    if (i != secondPivotBarIndex && _parent.Bars.High[secondPivotBarIndex] > _parent.Bars.High[i])
                      firstPivotBarIndex = i;
                  }
                  else if (isPivotHigh(_parent.Bars, i) &&
                           _parent.Bars.High[i] > _parent.Bars.High[secondPivotBarIndex])
                  {
                    //rule 3 holds - we have a pivot in the same direction of the second index pivot simply adjust the second pivot back
                    secondPivotBarIndex = i;
                  }
                }
              }

              break;
            case CorridorDirection.Bullish:
              //keep detected corridor direction the same as explicitly set value
              detectedDirection = CorridorDirection.Bullish;

              //second pivot must be a low for bullish break corridor
              if (secondPivotBarIndex == -1 &&
                  isPivotLow(_parent.Bars, i))
                secondPivotBarIndex = i;

              //first pivot must be a high for bullish break corridor
              if (secondPivotBarIndex != -1 && firstPivotBarIndex == -1)
              {
                //determine whether the current bar is a pivot low OR high, the following applies:
                // 1. if we detect a pivot of the opposite type of the second pivot we have a first pivot index
                // 2. if rule 1 hold then pivot extreme value of first index bar must be beyond the low/high of the second pivot index bar as well, this
                //    ensures that we do not use engulfing bars are pivot points
                // 3. if we determine a pivot of the type of the second pivot index we simply adjust the second pivot index
                //perform first index pivot bar detection for a bullish corridor
                if (isPivotHigh(_parent.Bars, i))
                {
                  //rule 1 holds - we have a pivot in the opposite direction of the second index pivot which is a pivot low 
                  //check whether rule 2 holds as well and set first index if that is the case - the LOW of the second index
                  //pivot bar should be lower than the LOW of the first index bar so that the corridor is properly formed
                  if (i != secondPivotBarIndex && _parent.Bars.Low[secondPivotBarIndex] < _parent.Bars.Low[i])
                    firstPivotBarIndex = i;
                }
                else if (isPivotLow(_parent.Bars, i))
                {
                  //rule 3 holds - we have a pivot in the same direction of the second index pivot simply adjust the second pivot back
                  secondPivotBarIndex = i;
                }
              }

              break;
            case CorridorDirection.Bearish:
              //keep detected corridor direction the same as explicitly set value
              detectedDirection = CorridorDirection.Bearish;

              //second pivot must be a high for a bearish corridor
              if (secondPivotBarIndex == -1 &&
                  isPivotHigh(_parent.Bars, i))
                secondPivotBarIndex = i;

              //first pivot must be a low for a bearish corridor
              if (secondPivotBarIndex != -1 && firstPivotBarIndex == -1)
              {
                //determine whether the current bar is a pivot low OR high, the following applies:
                // 1. if we detect a pivot of the opposite type of the second pivot we have a first pivot index
                // 2. if rule 1 hold then pivot extreme value of first index bar must be beyond the low/high of the second pivot index bar as well, this
                //    ensures that we do not use engulfing bars are pivot points
                // 3. if we determine a pivot of the type of the second pivot index we simply adjust the second pivot index
                //perfrom first index pivot bar detection for a bearish corridor
                if (isPivotLow(_parent.Bars, i))
                {
                  //rule 1 holds - we have a pivot in the opposite direction of the second index pivot which is a pivot high
                  //check whether rule 2 holds as well as set first index if that is the case - the HIGH of the second index
                  //pivot bar should be higher than the HIGH of the first index bar so that the corridor is properly formed
                  if (i != secondPivotBarIndex && _parent.Bars.High[secondPivotBarIndex] > _parent.Bars.High[i])
                    firstPivotBarIndex = i;
                }
                else if (isPivotHigh(_parent.Bars, i))
                {
                  //rule 3 holds - we have a pivot in the same direction of the second index pivot simply adjust the second pivot back
                  secondPivotBarIndex = i;
                }
              }

              break;
          }
        }

        //we found a corridor if both first and second pivot bar index, set the state information for the corridor
        if (firstPivotBarIndex != -1 && secondPivotBarIndex != -1)
        {
          _lowPivotPrice = _parent.Bars.Low[firstPivotBarIndex] < _parent.Bars.Low[secondPivotBarIndex] ? _parent.Bars.Low[firstPivotBarIndex] : _parent.Bars.Low[secondPivotBarIndex];
          _highPivotPrice = _parent.Bars.High[firstPivotBarIndex] > _parent.Bars.High[secondPivotBarIndex] ? _parent.Bars.High[firstPivotBarIndex] : _parent.Bars.High[secondPivotBarIndex];
          _height = _highPivotPrice - _lowPivotPrice;
          double minCorridorSize = MinCorridorPips * _pipValue;
          double maxCorridorSize = MaxCorridorPips * _pipValue;

          //only use corridor if it's within the minimum and maximum corridor size
          if (_height >= minCorridorSize && _height <= maxCorridorSize)
          {
            _found = true;
            _isValid = true;
            _direction = detectedDirection;
            _firstPivotBarTime = _parent.Bars.Time[firstPivotBarIndex];
            _secondPivotBarTime = _parent.Bars.Time[secondPivotBarIndex];
            _resumeScanBar = _secondPivotBarTime - _parent.Bars.Info.Resolution.Duration();

            //set price levels based on the break direction of the corridor
            if (_direction == CorridorDirection.Bullish)
            {
              //set price levels for bullish break of corridor
              _stopLossPrice = _lowPivotPrice - ExitBufferPips * _pipValue;
              _entryPrice = _highPivotPrice + EntryBufferPips * _pipValue;
              _risk = _entryPrice - _stopLossPrice;

              //NOTE: The 210%-price level and the exit target is based on the HEIGHT of the corridor and NOT the risk according to the book.
              _priceLevel210 = _highPivotPrice + 2.1 * _height;
              _exitTargetPrice = _highPivotPrice + 4.0 * _height;
            }
            else
            {
              //set price levels for bearish break of corridor
              _stopLossPrice = _highPivotPrice + ExitBufferPips * _pipValue;
              _entryPrice = _lowPivotPrice - EntryBufferPips * _pipValue;
              _risk = _stopLossPrice - _entryPrice;

              //NOTE: The 210%-price level and the exit target is based on the HEIGHT of the corridor and NOT the risk according to the book.
              _priceLevel210 = _lowPivotPrice - 2.1 * _height;
              _exitTargetPrice = _lowPivotPrice - 4.0 * _height;

            }

            //output debug information for corridor
            if (Debug)
              _parent.Output.WriteLine("Corridor found: Id({0}) Direction({1}) FirstPivot({2}, {3}) SecondPivot({4}, {5}) Entry({6}) StopLoss({7}) Height({8}) Risk({9}) 210%-Price({10}) Target({11})",
                                       _id,
                                       _direction.ToString(),
                                       _firstPivotBarTime.ToString(), _direction == CorridorDirection.Bullish ? _highPivotPrice : _lowPivotPrice,
                                       _secondPivotBarTime.ToString(), _direction == CorridorDirection.Bullish ? _lowPivotPrice : _highPivotPrice,
                                       _entryPrice,
                                       _stopLossPrice,
                                       _height,
                                       _risk,
                                       _priceLevel210,
                                       _exitTargetPrice);
          }
        }
      }

      //check whether corridor can still be considered valid
      if (_found && _isValid)
      {
        //the following criteria must hold for the corridor to be considered valid:
        // * entry price must be below/above the 210%-price level, depending on the entry buffer pips this might not be the case
        // * price closed above the stop-loss level of the corridor
        if (_direction == CorridorDirection.Bullish)
          _isValid = _entryPrice < _priceLevel210 && _parent.Bars.Close[0] > _stopLossPrice;
        else
          _isValid = _entryPrice > _priceLevel210 && _parent.Bars.Close[0] < _stopLossPrice;
      }

      //if corridor was found, it's still valid then keep checking whether the 210%-price level was broken
      //NOTE: We test the 210%-price break against the bar extreme high/low values since the bars might still
      //      close below the 210%-level.
      if (_found && IsValid && !_priceBroke210Level)
      {
        switch (_direction)
        {
          case CorridorDirection.Bullish:
            _priceBroke210Level = _parent.Bars.High[0] > _priceLevel210;
            break;
          case CorridorDirection.Bearish:
            _priceBroke210Level = _parent.Bars.Low[0] < _priceLevel210;
            break;
        }

        //output debug information IF the corridor 210%-level was broken
        if (Debug && _priceBroke210Level)
          _parent.Output.WriteLine("Corridor 210%-level broke: Id({0}) Direction({1}) Date({2}) 210%-Price({3}) Close({4})",
                                   _id,
                                   _direction.ToString(),
                                   _parent.Bars.Time[0].ToString(),
                                   _priceLevel210,
                                   _parent.Bars.Close[0]);
      }

      //move stop-loss to breakeven if the 210% level was broken
      if (_priceBroke210Level)
        _stopLossPrice = _entryPrice;
    }

    /// <summary>
    /// Determines whether bar 2 is a pivot low point.
    /// </summary>
    /// <returns>True if bar 2 is a pivot low.</returns>
    public static bool isPivotLow(double bar1Low, double bar2Low, double bar3Low)
    {
      return bar1Low > bar2Low && bar3Low > bar2Low;
    }

    /// <summary>
    /// Determines whether bar at given index is a pivot low point.
    /// </summary>
    public static bool isPivotLow(IInstrument bars, int index)
    {
      //NOTE: Doji bars are not considered pivot points.
      return bars.Low[index - 1] > bars.Low[index] && bars.Low[index + 1] > bars.Low[index] && bars.Open[index] != bars.Close[index];
    }

    /// <summary>
    /// Determines whether bar 2 is a pivot high point.
    /// </summary>
    /// <returns>True if bar 2 is a pivot high.</returns>
    public static bool isPivotHigh(double bar1High, double bar2High, double bar3High)
    {
      return bar1High < bar2High && bar3High < bar2High;
    }

    /// <summary>
    /// Determines whether bar at given index is a pivot high point. 
    /// </summary>
    public static bool isPivotHigh(IInstrument bars, int index)
    {
      //NOTE: Doji bars are not considered pivot points.
      return bars.High[index - 1] < bars.High[index] && bars.High[index + 1] < bars.High[index] && bars.Open[index] != bars.Close[index];
    }

    /// <summary>
    /// Determines whether given corridor is beyond the 210%-price level of this corridor. 
    /// </summary>
    /// <returns>True if given corridor is beyond the 210%-reversal point.</returns>
    public bool IsBeyond210PriceLevel(Corridor c)
    {
      bool result = false;

      switch (_direction)
      {
        case CorridorDirection.Bullish:
          result = c._lowPivotPrice >= _priceLevel210;
          break;
        case CorridorDirection.Bearish:
          result = c._highPivotPrice <= _priceLevel210;
          break;
      }

      return result;
    }
  }
}
