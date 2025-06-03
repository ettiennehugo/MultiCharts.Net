using System;
using System.Collections;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Chart Type: Candle
  /// 
  /// Hypothesis:
  /// The market volatility ETF's moves in a range bound fashion as the market cycles between more volatile and less volatile
  /// thus when the market becomes less volatile it is a setup for more volatility later on.
  /// 
  /// Entries are managed on a short time frame, e.g. 1- or 5-minute timeframe while the daily timeframe is monitored to determine
  /// whether entries or exits should be made.
  /// 
  /// Implementation:
  /// Strategy uses the Ehler's sinewave indicator to isolate the cycles of the VIX instrument and enters the market in blocks
  /// when low volatility is entered and exits the positions when higher volatility is reached. 
  /// 
  /// Ideal Instruments and Timeframes:
  ///   VIX range bound instruments on daily timeframe - UVXY, VXX
  /// 
  /// Entry Rules - Long:
  ///   1. SineWave indicator is below the long level indicator.
  ///   2. Price movement is within a given percentage of the lowest historical prices. 
  /// 
  /// Exit Rules - Long:
  ///   1. Exit when the prices moves within a given percentage of the price highs.
  ///  
  /// Money Management:
  ///  * Trades only a specific percentage of the account.
  ///  * Entries are computed linearly based on available equity to trade.
  /// 
  /// Notes:
  ///  * Strategy MUST be run with intrabar order generation enabled (IOG) to make sure that entries and exits are properly
  ///    executed at the correct prices.
  /// 
  /// TODO:
  ///   * Determine how to enter multiple positions and exit in multiple positions.
  ///   * Determine a series of prices at which you need to enter positions and then send the limit orders removing the
  ///     price at which the order was send (OnFilled method)
  ///     - When the sinewave value crosses under the low level calculate this entry series based on the low lookback and 
  ///       given percentage.
  ///     - As orders are filled drop the value from the series and start sending the next lower value in the series till it's
  ///       filled
  ///     - Need to parameterize how this series of values will be computed??? You'd want this to series to automatically adjust
  ///       into multiple positions with increasing position sizes toward lower price values.
  /// 
  /// </summary>	

  public class _SineWave : SignalObject
  {
    //Constants:
    public const int DEFAULT_DURATION = 30;
    public const double DEFAULT_ENTRY_LEVEL = -0.85;
    public const double DEFAULT_EXIT_LEVEL = 0.85;
    public const int DEFAULT_LOW_LOOKBACK = 30;
    public const double DEFAULT_LOW_START_PERCENT = 0.20;   //start entering 20% above the lowest price encountered
    public const double DEFAULT_LOW_END_PERCENT = 0.13;     //end entering 13% above the lowest price encountered
    public const int DEFAULT_HIGH_LOOKBACK = 30;
    public const double DEFAULT_HIGH_START_PERCENT = 0.13;  //place first exiting trades 13% from highest high
    public const double DEFAULT_HIGH_END_PERCENT = 0.08;    //place last exiting trades 8% from highest high
    public const double DEFAULT_PROFIT_START_PERCENT = 0.1; //place first exiting trades at 10% profit
    public const double DEFAULT_PROFIT_END_PERCENT = 0.2;   //place first exiting trades at 20% profit
    public const double DEFAULT_PERCENT_ACCOUNT_TO_TRADE = 0.1;
    public const int MIN_REQUIRED_HOLD_TIME = 3;            //number of days to hold each trade
    public const int DEFAULT_MAX_DAYS_PER_TRADE = 30;       
    public const int MIN_TRADE_SIZE = 10;                   //minimum trade size to take per position
    public const int MAX_TRADE_SIZE = 15000;                //maximum trade size to take per position

    //Properties:
    [Input] public int Duration { get; set; }
    [Input] public double EntryLevel { get; set; }    //sinewave indicator level at which entries would be considered
    [Input] public double ExitLevel { get; set; }     //sinewave indicator level at which exits would be considered
    [Input] public int LowLookback { get; set; }      //number of bars to look back for low to determine entry
    [Input] public double LowStartPercent { get; set; }    //price movement percentage from low to start entries
    [Input] public double LowEndPercent { get; set; }  //price movement percentage from low to end entries (this would be lowest entry possible above the lowest price)
    [Input] public double ProfitStartPercent { get; set; }   //start percentage for profit taking
    [Input] public double ProfitEndPercent { get; set; }   //end percentage for profit taking
    [Input] public int MaxDaysPerTrade { get; set; }    //maximum number of days to hold a trade
    [Input] public double PercentAccountToTrade { get; set; } //percentage of the account to trade
    [Input] public bool Debug { get; set; }           //debug the strategy

    //Attributes:
    private PowerLanguage.Function._SineWave _sineWave;
    private VariableSeries<Double> _sineWaveSeries;
    private SortedList _entrySeries;
    private IOrderPriced _LE;
    private IOrderPriced _LX_TP;
    private IOrderMarket _LX_SL;

    public _SineWave(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      // create variable objects, function objects, order objects etc.
      Duration = DEFAULT_DURATION;
      EntryLevel = DEFAULT_ENTRY_LEVEL;
      ExitLevel = DEFAULT_EXIT_LEVEL;
      LowLookback = DEFAULT_LOW_LOOKBACK;
      LowStartPercent = DEFAULT_LOW_START_PERCENT;
      LowEndPercent = DEFAULT_LOW_END_PERCENT;
      ProfitStartPercent = DEFAULT_PROFIT_START_PERCENT;
      ProfitEndPercent = DEFAULT_PROFIT_END_PERCENT;
      PercentAccountToTrade = DEFAULT_PERCENT_ACCOUNT_TO_TRADE;
      MaxDaysPerTrade = DEFAULT_MAX_DAYS_PER_TRADE;
      _sineWave = new PowerLanguage.Function._SineWave(this);
      _sineWaveSeries = new VariableSeries<Double>(this);
      _entrySeries = new SortedList();

      //create the orders used
      _LE = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LE", EOrderAction.Buy));
      _LX_TP = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "TPLX", EOrderAction.Sell));
      _LX_SL = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "TPSL", EOrderAction.Sell)); ;
    }

    protected override void StartCalc()
    {
      //clear the debugging console
      if (Debug)
        Output.Clear();

      //input parameter checks
      if (Debug && (GetIOGMode() == IOGMode.Disabled))
        Output.WriteLine("WARNING: Intrabar order generation should be enabled to properly trade strategy.");
      if (LowStartPercent <= LowEndPercent) throw new Exception("LowStartPercentage must be larger than LowEndPercentage.");

      //setup the sinewave indicator
      _sineWave.Duration = Duration;
      _entrySeries.Clear();

      //adjust low/high percentages for quicker calculation
      LowStartPercent += 1;
      LowEndPercent += 1;
    }

    protected override void CalcBar()
    {
      switch (Environment.CalcReason)
      {
        case CalculationReason.Default:
          calcBarDefault();
          break;
      }
    }

    private void calcBarDefault()
    {
      //update the sine wave series if this is the end processing of the bar
      if (Bars.Status == EBarState.Close)
        _sineWaveSeries.Value = _sineWave.Value;

      updateEntries();
      calcBarEntry();
      calcBarExit();
    }

    private void updateEntries()
    {
      //update the entry price series if the sine wave price series moves under the entry level
      if (_sineWaveSeries.CrossesUnder(EntryLevel, 1))
      {
        //compute the amount of equity available to trade
        double equityAvailableToTrade = InitialCapital * PercentAccountToTrade;
        foreach (PowerLanguage.TradeManager.Account account in TradeManager.TradingData.Accounts.Items)
        {
          if (account.ID == Account)
          {
            equityAvailableToTrade = (double)account.AvailableToTrade * PercentAccountToTrade;
            break;
          }
        }

        equityAvailableToTrade -= CurrentPosition.Value;

        //only try to enter a trade if we can at least place a minimal trade
        double startEntryPrice = Bars.Low.Lowest(LowLookback) * LowStartPercent;
        double endEntryPrice = Bars.Low.Lowest(LowLookback) * LowEndPercent;
        if (equityAvailableToTrade > (startEntryPrice * MIN_TRADE_SIZE))
        {
          Random randomDelta = new Random();
          _entrySeries.Clear();
          double currentPrice = startEntryPrice;
          
          //create random set of prices to enter at if there is enough spread otherwise just enter on each sent
          if (startEntryPrice - endEntryPrice > 0.1) {
            while (currentPrice > endEntryPrice)
            {
              _entrySeries.Add(currentPrice, 0);
              currentPrice -= randomDelta.Next(2, 10) / (double)100.0;  //add random price delta between entries
            }
          } else {
            while (currentPrice > endEntryPrice)
            {
              _entrySeries.Add(currentPrice, 0);
              currentPrice -= 0.02; //add constant price delta
            }
          }

          //increment determined price blocks until we can not enter any more positions
          int index = 0;
          while (equityAvailableToTrade > 0) 
          {
            int size = (int)_entrySeries.GetByIndex(index);
            double price = (double)_entrySeries.GetKey(index);
            if (equityAvailableToTrade > (price * MIN_TRADE_SIZE))
            {
              size += MIN_TRADE_SIZE;
              _entrySeries.SetByIndex(index, size);
              equityAvailableToTrade -= price * MIN_TRADE_SIZE;
            }
            else equityAvailableToTrade = 0;

            if (index < (_entrySeries.Count - 1)) 
              index++; 
            else index = 0;
          }

          if (Debug) {
            for (index = 0; index < _entrySeries.Count; index++)
            {
              double price = (double)_entrySeries.GetKey(index);
              int size = (int)_entrySeries.GetByIndex(index);
              Output.WriteLine("EntrySeries(Index: {0}, Price: {1}, Size: {2})", index, price, size);
            }
          }

        }
        else if (Debug) Output.WriteLine("SkippingEntry(availableEquity: {0}, minRequiredEquity: {1}, barTime: {2}, barOpen: {3})", equityAvailableToTrade, Bars.Open[0] * MIN_TRADE_SIZE, Bars.Time[0], Bars.Open[0]);
      }

      //clear the entry price series if the sine wave price moved above entry level
      if (_sineWaveSeries.CrossesOver(EntryLevel, 1)) _entrySeries.Clear();
    }

    private void calcBarEntry()
    {    
      for (int index = 0; index < _entrySeries.Count; index++) 
      {
        double price = (double)_entrySeries.GetKey(index);
        int size = (int)_entrySeries.GetByIndex(index);
        if (Debug) Output.WriteLine("Entry(Time: {0}, Price: {1}, Contracts: {2})", Bars.Time[0], price, size);
        _LE.Send(price, size);
      }
    }

    private void calcBarExit()
    {
      if (PositionSide < 0)
      {
        Output.WriteLine("WARNING: SineWave strategy is in a short position, this should not occur.");
      }

      if (PositionSide > 0)
      {
        //exit trading positions that matches specific profit targets based around how long the trades have been held
        foreach (ITrade trade in CurrentPosition.OpenTrades)
        {
          if (Debug && (trade.EntryOrder.Price == 0 || trade.EntryOrder.Contracts == 0))
          {
            Output.WriteLine("ERROR: Trade has zero entry price/contracts. (Time: {0}, Price: {1}, Contracts: {2})", trade.EntryOrder.Time, trade.EntryOrder.Price, trade.EntryOrder.Contracts);
            continue;
          }

          double profitPercent = trade.Profit / (trade.EntryOrder.Price * trade.EntryOrder.Contracts);
          TimeSpan timeSinceEntry = Bars.Time[0] - trade.EntryOrder.Time;
          if (profitPercent > ProfitStartPercent && timeSinceEntry.Days > MIN_REQUIRED_HOLD_TIME)
          {


            // TODO: Need to refine the profit percentage based on the current price.
            double profitPrice = trade.EntryOrder.Price * (1 + ProfitStartPercent);


            if (Debug) Output.WriteLine("ProfitExit(exitTime: {0}, exitPrice: {1}, exitContracts: {2}, profit: {3}, daysHeld: {4})", Bars.Time[0], profitPrice, trade.EntryOrder.Contracts, trade.Profit, timeSinceEntry.Days);
            _LX_TP.Send(profitPrice, trade.EntryOrder.Contracts);
          }
          else if (timeSinceEntry.Days > MaxDaysPerTrade)
          {
            if (Debug) Output.WriteLine("StopLossExit(entryTime: {0}, entryPrice: {1}, entryContracts: {2}, profit: {3}, daysHeld: {4}, exitTime: {5})", trade.EntryOrder.Time, trade.EntryOrder.Price, trade.EntryOrder.Contracts, trade.Profit, timeSinceEntry.Days, Bars.Time[0]);
            _LX_SL.Send(trade.EntryOrder.Contracts);
          }
        }
      }
    }
  }
}