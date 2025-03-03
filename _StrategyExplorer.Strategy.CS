using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.TradeManager;
using Ichimoku;
using PriceAction;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Position sizing strategy.
  /// </summary>
  public enum SizingStrategy
  {
    Static,
    Dynamic,
  }

  /// <summary>
  /// Chart Type: Candlestick
  /// 
  /// Hypothesis:
  ///   Supports a scripting language called strategy script that can be used to explore strategies to try and find criteria under 
  ///   which different indicators works best.
  /// 
  /// Ideal Instruments and Timeframes:
  ///   Any instrument and timeframe.
  /// 
  /// Money Management:
  ///   Static position sizing or dynamic position sizing using a fixed ratio position sizing.
  /// 
  /// Notes:
  ///   * IMPORTANT: The BarsOfData method raises an exception that you have to let the calling framework catch, MultiCharts uses this exception to actually initialize that data feed.
  ///   
  ///     TODO:
  ///       * Add super smoother to moving average module.
  ///         - Sort of works but you do not get a lot of trades.
  ///       * Add support for stochastics and include the standard stochastic, smoothed stochastic and stochastic with fisher transform.
  ///       * Add support for an RSI with modified inverse Fisher transform.
  ///       * Check for the other John Ehlers components and whether you can add them as usefull functions.
  ///         - Voss Filter
  ///         - Decycler Oscillator
  ///         - BandPass Trend filter
  ///       * Test and fix the stop-loss/trailing-stop/profit-target methods.
  /// 
  /// 
  /// </summary>
  public class _StrategyExplorer : SignalObject
  {
    //Inputs:
    [Input]
    //NOTE: MC's MaxDataStream variable is not consistent between MC and Portfolio Trader so you need to set explicitly how many data streams are accessed.
    //      Portfolio Trader will tell you that there's 10 data streams assigned to the portfolio even though only the first data stream is configured with
    //      instruments. TBD: Maybe there's another variable that expresses the actual number of data streams configured but MaxDataStream is not it.
    public int DataStreamsUsed { get; set; }
    [Input]
    public string StrategyScript { get; set; }      //strategy script to run OR 
    [Input]
    public string ScriptPrefix { get; set; }        //script prefix, e.g. imports, position sizing etc.
    [Input]
    public string LEFile { get; set; }              //file containing the long entry strategies to use
    [Input]
    public int LEFileLine { get; set; }             //file line in the long entry strategies to use
    [Input]
    public string LXFile { get; set; }              //file containing the long exit strategies to use
    [Input]
    public int LXFileLine { get; set; }             //file line in the long exit strategies to use
    [Input]
    public string SEFile { get; set; }              //file containing the short entry strategies to use
    [Input]
    public int SEFileLine { get; set; }             //file line in the short entry strategies to use
    [Input]
    public string SXFile { get; set; }              //file containgin the short exit strategies to use
    [Input]
    public int SXFileLine { get; set; }             //file line in the short exit strategies to use
    [Input]
    public string LEStopLossFile { get; set; }      //file containing long entry stop-loss strategies to use
    [Input]
    public int LEStopLossFileLine { get; set; }     //file line in long entry stop-loss strategies to use
    [Input]
    public string LEProfitTargetFile { get; set; }  //file containing long entry profit target strategies to use
    [Input]
    public int LEProfitTargetFileLine { get; set; } //file line in long entry profit target strategies to use
    [Input]
    public string SEStopLossFile { get; set; }      //file containing short entry stop-loss strategies to use
    [Input]
    public int SEStopLossFileLine { get; set; }     //file line in short entry stop-loss strategies to use
    [Input]
    public string SEProfitTargetFile { get; set; }  //file containing short entry proft target strategies to use
    [Input]
    public int SEProfitTargetFileLine { get; set; } //file line in short entry profit target strategies to use

    //Constants:
    //IMPORTANT: These constants assume the Forex market for other markets you'd have to set them explicitly.
    public const int DefaultMaxDynamicOrderSize = 10000000;
    public const int DefaultDynamicSizeDelta = 10000;
    public const double DefaultDynamicProfitDelta = 500.0;

    //Properties:
    /// <summary>
    /// Properties associated with dynamic position sizing.
    /// </summary>
    public SizingStrategy SizingStrategy {
      get { return _sizingStrategy; }
      set { _sizingStrategy = value; }
    }

    public int SizeMax {
      get { return _sizeMax; }
      set {
        if (value <= 0)
          throw new Exception("Maximum size for orders must be a positive number.");
        _sizeDelta = value;
      }
    }

    public int SizeDelta {
      get { return _sizeDelta; }
      set {
        if (value <= 0)
          throw new Exception("Sizing delta for orders must be a positive number.");
        _sizeDelta = value;
      }
    }

    public double ProfitDelta {
      get { return _profitDelta; }
      set {
        if (value <= 0)
          throw new Exception("Profit delta must be a positive amount.");
        _profitDelta = value;
      }
    }

    public int PositionSize {
      get {
        int positionSize = 0;
        if (_sizingStrategy == SizingStrategy.Static)
        {
          //apply static position sizing from strategy settings
          //TBD: Not sure how you retrieve the static position sizing from the strategy settings window.
          switch (Bars.Info.Category)
          {
            case ESymbolCategory.Forex:
              positionSize = 100000;
              break;
            case ESymbolCategory.Future:
              positionSize = 1;
              break;
            case ESymbolCategory.Stock:
              positionSize = 100;
              break;
            default:
              throw new Exception("Unhandled instrument category for static position sizing.");
          }
        }
        else
        {
          //apply dynamic position sizing
          double netProfit = NetProfit + _startProfit;
          netProfit = (netProfit > 0) ? netProfit : 0;    //net profit can not be negetive for square root calculation below
          positionSize = (int)(_sizeDelta * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / _profitDelta))))));
          positionSize = positionSize <= _sizeMax ? positionSize : _sizeMax;
        }

        return positionSize;
      }
    }

    public double StartProfit {
      get { return _startProfit; }
      set { _profitDelta = value; }
    }

    //order management methods
    public void SendMarketLE()
    {
      if (SizingStrategy == SizingStrategy.Static)
        _marketSLE.Send();
      else
        _marketDLE.Send(PositionSize);
    }

    public void SendMarketLX()
    {
      _marketLX.Send();
    }

    public void SendMarketSE()
    {
      if (SizingStrategy == SizingStrategy.Static)
        _marketSSE.Send();
      else
        _marketDSE.Send(PositionSize);
    }

    public void SendMarketSX()
    {
      _marketSX.Send();
    }

    public void SendStopLE(double price)
    {
      if (SizingStrategy == SizingStrategy.Static)
        _stopSLE.Send(price);
      else
        _stopDLE.Send(price, PositionSize);
    }

    public void SendStopLX(double price)
    {
      _stopLX.Send(price);
    }

    public void SendStopSE(double price)
    {
      if (SizingStrategy == SizingStrategy.Static)
        _stopSSE.Send(price);
      else
        _stopDSE.Send(price, PositionSize);
    }

    public void SendStopSX(double price)
    {
      _stopSX.Send(price);
    }

    public void SendLimitLE(double price)
    {
      if (SizingStrategy == SizingStrategy.Static)
        _limitSLE.Send(price);
      else
        _limitDLE.Send(price, PositionSize);
    }

    public void SendLimitLX(double price)
    {
      _limitLX.Send(price);
    }

    public void SendLimitSE(double price)
    {
      if (SizingStrategy == SizingStrategy.Static)
        _limitSSE.Send(price);
      else
        _limitDSE.Send(price, PositionSize);
    }

    public void SendLimitSX(double price)
    {
      _limitSX.Send(price);
    }

    public void SendStopLimitLE(double stopPrice, double limitPrice)
    {
      if (SizingStrategy == SizingStrategy.Static)
        _stopLimitSLE.Send(stopPrice, limitPrice);
      else
        _stopLimitDLE.Send(stopPrice, limitPrice, PositionSize);
    }

    public void SendStopLimitLX(double stopPrice, double limitPrice)
    {
      _stopLimitLX.Send(stopPrice, limitPrice);
    }

    public void SendStopLimitSE(double stopPrice, double limitPrice)
    {
      if (SizingStrategy == SizingStrategy.Static)
        _stopLimitSSE.Send(stopPrice, limitPrice);
      else
        _stopLimitDSE.Send(stopPrice, limitPrice, PositionSize);
    }

    public void SendStopLimitSX(double stopPrice, double limitPrice)
    {
      _stopLimitSX.Send(stopPrice, limitPrice);
    }

    public void StopLossPercent(double percent)
    {

      //TODO: This code will not work right because you do not know what the cost basis is for holding a position.

      //if (PositionSide != 0) {
      //    //NOTE: This code assumes that we're holding only a single position at a time.
      //    IMarketPosition position = Positions[0];
      //    ITrade trade = position.OpenTrades[0];
      //    GenerateStopLoss(position.Value * (percent / 100));
      //}

      GenerateStopLoss(this.EntryPrice() * (percent / 100));

    }

    public void StopLossAmount(double amount)
    {
      GenerateStopLoss(amount);
    }

    public void TrailingStopPercent(double percent, double floorAmount)
    {
      GeneratePercentTrailing(floorAmount, percent);
    }

    public void TrailingStopAmount(double amount)
    {
      GenerateDollarTrailing(amount);
    }

    public void ProfitTargetPercent(double percent)
    {

      //NOT SURE WHETHER WE CAN COMPUTE/SUPPORT THIS SINCE YOU DON'T KNOW WHAT THE POSITION IS COSTING YOU!!!!

      GenerateProfitTarget(this.EntryPrice() * (percent / 100));
    }

    public void ProfitTargetAmount(double amount)
    {
      GenerateProfitTarget(amount);
    }


    // * Add a functions that returns the bar Bid/Ask prices.

    //Attributes:
    protected Interpreter _interpreter;
    protected SizingStrategy _sizingStrategy;
    protected int _sizeMax;
    protected int _sizeDelta;
    protected double _profitDelta;
    protected double _startProfit;
    protected IOrderMarket _marketSLE;
    protected IOrderMarket _marketDLE;
    protected IOrderMarket _marketLX;
    protected IOrderMarket _marketSSE;
    protected IOrderMarket _marketDSE;
    protected IOrderMarket _marketSX;
    protected IOrderPriced _stopSLE;
    protected IOrderPriced _stopDLE;
    protected IOrderPriced _stopLX;
    protected IOrderPriced _stopSSE;
    protected IOrderPriced _stopDSE;
    protected IOrderPriced _stopSX;
    protected IOrderPriced _limitSLE;
    protected IOrderPriced _limitDLE;
    protected IOrderPriced _limitLX;
    protected IOrderPriced _limitSSE;
    protected IOrderPriced _limitDSE;
    protected IOrderPriced _limitSX;
    protected IOrderStopLimit _stopLimitSLE;
    protected IOrderStopLimit _stopLimitDLE;
    protected IOrderStopLimit _stopLimitLX;
    protected IOrderStopLimit _stopLimitSSE;
    protected IOrderStopLimit _stopLimitDSE;
    protected IOrderStopLimit _stopLimitSX;
    protected List<string> _leBuffer;
    protected List<string> _lxBuffer;
    protected List<string> _seBuffer;
    protected List<string> _sxBuffer;
    protected List<string> _leStopLossBuffer;
    protected List<string> _leProfitTargetBuffer;
    protected List<string> _seStopLossBuffer;
    protected List<string> _seProfitTargetBuffer;

    public _StrategyExplorer(object _ctx) : base(_ctx)
    {
      DataStreamsUsed = 1;
      StrategyScript = "";
      ScriptPrefix = "";
      LEFile = "";
      LEFileLine = -1;
      LXFile = "";
      LXFileLine = -1;
      SEFile = "";
      SEFileLine = -1;
      SXFile = "";
      SXFileLine = -1;
      LEStopLossFile = "";
      LEStopLossFileLine = -1;
      LEProfitTargetFile = "";
      LEProfitTargetFileLine = -1;
      SEStopLossFile = "";
      SEStopLossFileLine = -1;
      SEProfitTargetFile = "";
      SEProfitTargetFileLine = -1;
      _sizingStrategy = SizingStrategy.Static;
      _sizeMax = DefaultMaxDynamicOrderSize;
      _sizeDelta = DefaultDynamicSizeDelta;
      _profitDelta = DefaultDynamicProfitDelta;
    }

    protected override void Create()
    {
      _marketSLE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "MtSLE", EOrderAction.Buy));
      _marketDLE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "MtDLE", EOrderAction.Buy));
      _marketLX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "MtLX", EOrderAction.Sell));
      _marketSSE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "MtSSE", EOrderAction.SellShort));
      _marketDSE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.UserSpecified, "MtDSE", EOrderAction.SellShort));
      _marketSX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "MtSX", EOrderAction.BuyToCover));
      _stopSLE = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SpSLE", EOrderAction.Buy));
      _stopDLE = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SpDLE", EOrderAction.Buy));
      _stopLX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SpLX", EOrderAction.Sell));
      _stopSSE = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SpSSE", EOrderAction.SellShort));
      _stopDSE = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SpDSE", EOrderAction.SellShort));
      _stopSX = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SpSX", EOrderAction.BuyToCover));
      _limitSLE = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "LtSLE", EOrderAction.Buy));
      _limitDLE = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LtDLE", EOrderAction.Buy));
      _limitLX = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "LtLX", EOrderAction.Sell));
      _limitSSE = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "LtSSE", EOrderAction.SellShort));
      _limitDSE = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "LtDSE", EOrderAction.SellShort));
      _limitSX = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "LtSX", EOrderAction.BuyToCover));
      _stopLimitSLE = OrderCreator.StopLimit(new SOrderParameters(Contracts.Default, "StLtSLE", EOrderAction.Buy));
      _stopLimitDLE = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "StLtDLE", EOrderAction.Buy));
      _stopLimitLX = OrderCreator.StopLimit(new SOrderParameters(Contracts.Default, "StLtLX", EOrderAction.Sell));
      _stopLimitSSE = OrderCreator.StopLimit(new SOrderParameters(Contracts.Default, "StLtSSE", EOrderAction.SellShort));
      _stopLimitDSE = OrderCreator.StopLimit(new SOrderParameters(Contracts.UserSpecified, "StLtDSE", EOrderAction.SellShort));
      _stopLimitSX = OrderCreator.StopLimit(new SOrderParameters(Contracts.Default, "StLtSX", EOrderAction.BuyToCover));

      _interpreter = new Interpreter(this);
      _interpreter.Create();
    }

    protected override void StartCalc()
    {
      base.StartCalc();

      //load strategy file data and raise exception if strategy line number if not in range
      string script = "";
      if (StrategyScript.Length > 0)
        script = StrategyScript;
      else
      {
        //initialize the file buffers if not yet setup
        initFileBuffer(LEFile, ref _leBuffer);
        initFileBuffer(LXFile, ref _lxBuffer);
        initFileBuffer(SEFile, ref _seBuffer);
        initFileBuffer(SXFile, ref _sxBuffer);
        initFileBuffer(LEStopLossFile, ref _leStopLossBuffer);
        initFileBuffer(LEProfitTargetFile, ref _leProfitTargetBuffer);
        initFileBuffer(SEStopLossFile, ref _seStopLossBuffer);
        initFileBuffer(SEProfitTargetFile, ref _seProfitTargetBuffer);

        //construct the script from the components specified
        //NOTE: Stop-loss/trailing-stop/profit-target components are added in a very specific order to give them precedence. The applied
        //      precendence is the exit, profit-target and lastly the stop-loss.
        script = ScriptPrefix;
        script = addScriptComponent(_leBuffer, LEFileLine, script);
        script = addScriptComponent(_lxBuffer, LXFileLine, script);
        script = addScriptComponent(_seBuffer, SEFileLine, script);
        script = addScriptComponent(_sxBuffer, SXFileLine, script);
        script = addScriptComponent(_leProfitTargetBuffer, LEProfitTargetFileLine, script);
        script = addScriptComponent(_seProfitTargetBuffer, SEProfitTargetFileLine, script);
        script = addScriptComponent(_leStopLossBuffer, LEStopLossFileLine, script);
        script = addScriptComponent(_seStopLossBuffer, SEStopLossFileLine, script);
      }

      //setup the script and initialize the interpreter
      _sizingStrategy = SizingStrategy.Static;    //default the sizing strategy to static
      _interpreter.Script = script;
      _interpreter.StartCalc();
    }

    protected override void StopCalc()
    {
      base.StopCalc();

      //allow the interpreter to process stop calculation
      _interpreter.StopCalc();
    }

    protected override void CalcBar()
    {
      //compute the calculation
      switch (Environment.CalcReason)
      {
        case CalculationReason.Default:
          CalcBarDefault();
          break;
        case CalculationReason.OrderFilled:
          CalcBarOrderFilled();
          break;
      }

      //update the custom fitness value if this is the last bar
      if (Bars.LastBarOnChart)
      {

        //TODO: Implement the RINA-index

        ////update the custom criteria as RINA-index
        //if (TotalTrades > 1) {
        //    //compute select total profit - we reject positions that made a profit/loss of more than 3 standard deviations from the average
        //    double variance = 0.0;
        //    double averageProfit = NetProfit / TotalTrades;

        //    foreach (IMarketPosition position in Positions) {
        //        double squareProfit = position.Profit - averageProfit;
        //        squareProfit = squareProfit * squareProfit;
        //        variance += squareProfit;
        //    }

        //    variance = variance / TotalTrades - 1;
        //    double stdDev3 = Math.Sqrt(variance) * 3;     //<=== GETTING AN Overflow/Underflow EXCEPTION HERE.
        //    double selectGrossProfit = 0.0;
        //    double selectGrossLoss = 0.0;

        //    foreach (IMarketPosition position in Positions) {
        //        if (position.Profit >= 0)
        //            selectGrossProfit += position.Profit < stdDev3 ? position.Profit : 0;
        //        else
        //            selectGrossLoss += -position.Profit < stdDev3 ? position.Profit : 0;
        //    }

        //    double selectTotalProfit = selectGrossProfit + selectGrossLoss;

        //    //compute percentage of time in the market
        //    double percentInMarket = (AvgBarsEvenTrade + AvgBarsLosTrade + AvgBarsWinTrade) / 3;
        //    percentInMarket = (percentInMarket * TotalTrades) / Bars.CurrentBar;

        //    //set fitness value to the RINA-index
        //    double avgDrawDown = MaxDrawDown / TotalTrades;
        //    double avgDDPctInMarket = avgDrawDown * percentInMarket;

        //    CustomFitnessValue = avgDDPctInMarket != 0 ? selectTotalProfit / avgDDPctInMarket : 0;
        //} else {
        //    //no trades so fitness value is zero
        //    CustomFitnessValue = 0;
        //}

      }
    }

    protected override void OnBrokerStategyOrderFilled(bool is_buy, int quantity, double avg_fill_price)
    {
      orderFilled();
    }

    private void CalcBarDefault()
    {
      //evaluate the loaded script
      _interpreter.Evaluate();
    }

    private void CalcBarOrderFilled()
    {
      orderFilled();
    }

    private void orderFilled()
    {
      //allow the script to evaluate the change in the market position
      _interpreter.Evaluate();
    }

    /// <summary>
    /// Initializes the file buffer and loads the file data from the given file if the file is specified. The file buffer is loaded only ONCE and then just
    /// reused after that, this helps to speed up the optimization but has the draw back that if the files change we need to REMOVE AND ADD THE SIGNAL AGAIN.
    /// </summary>
    private void initFileBuffer(string filename, ref List<string> buffer)
    {
      //only setup the file buffer if it was not created yet AND we have a file name to work with
      if (buffer == null && filename.Length != 0)
      {
        buffer = new List<string>();
        using (StreamReader sr = File.OpenText(filename))
        {
          string line = "";
          while ((line = sr.ReadLine()) != null)
          {
            //only add non-blank lines, no comments supported
            if (line.Length > 0)
              buffer.Add(line);
          }
        }
      }
    }

    /// <summary>
    /// Adds the line number from the file buffer to the script if buffer is set and line number is valid.
    /// </summary>
    private string addScriptComponent(List<string> buffer, int lineNo, string currentScript)
    {
      string result = currentScript;

      if (null != buffer)
      {
        //raise exception if script line number is out of range
        //NOTE: Line numbers are zero based.
        if (lineNo > buffer.Count)
          throw new Exception(string.Format("Line number {0} is out of range for file buffer, line count is {1}.", lineNo, buffer.Count));

        //add the script components IF it has a positive index, negative indices would disable the specific script components
        if (lineNo > 0)
          result = result + buffer[lineNo - 1];
      }

      return result;
    }
  }

  #region Strategy Script Tokenizer/Lexer
  /// <summary>
  /// Enumeration of the supported function and value result types.
  /// </summary>
  public enum ValueTypeType
  {
    Void,
    Boolean,
    Integer,
    Double,
    String,
    Enumeration,
  }

  /// <summary>
  /// Enumeration of the types of tokens supported by the parser.
  /// </summary>
  public enum TokenType
  {
    EntryExitStart,
    EntryExitEnd,
    ComplexCondition,
    Condition,
    UniaryLogicalOperator,
    BinaryLogicalOperator,
    ComparisonOperator,
    FunctionStart,
    FunctionEnd,
    Parameter,
    Value,
    TriggerAction,
  }

  //Types:
  /// <summary>
  /// Internal structure to store tokens processed.
  /// </summary>
  public class TokenString
  {
    public int startPosition;
    public string token;
  }

  /// <summary>
  /// Structure of the tokens parsed.
  /// </summary>
  public class Token
  {
    //Properties:
    public TokenType type;
    public ValueTypeType valueType;
    public string text;
    public int start;
  }

  /// <summary>
  /// Script tokenizer used to tokenize the script grammar.
  ///     Script = { Function | EntryExit }
  ///		EntryExit = EntryExit { EntryExit }
  /// 	EntryExit = '{', { ComplexCondition | Condition },'->', Function,'}'
  ///     ComplexCondition = Condition, BinaryLogicalOperator, Condition, {LogicalOperator, Condition}
  ///     Condition = ValueOrFunction, ComparisonOperator, ValueOrFunction | [UniaryLogicalOperator,] ValueOrFunction
  ///     UniaryLogicalOperator = '!'
  ///     BinaryLogicalOperator = '&&' | '||'
  ///     ComparisonOperator = '==' | '!=' | '<' | '>' | '<=' | '>='
  ///     ValueOrFunction = Function | Value
  ///		Function = Name, '(', [ParameterList] ')'
  ///		ParameterList = ValueOrFunction, {',', ValueOrFunction }
  ///		Value = BooleanValue | PositiveIntegerValue | NegativeIntegerValue | PositiveDoubleValue | NegativeDoubleValue | StringValue | SetValue | EnumerationValue
  ///		Name = Alpha | Digit | '_', { Alpha | Digit | '_' }
  ///		BooleanValue = "True" | "False"
  ///     Integer = Digit, { Digit }
  ///		NegativeInteger = '-', Integer
  ///		PositiveInteger = ['+',] Integer
  ///		PositiveDouble = ['+',] Integer, '.'[, Integer]
  ///		NegativeDouble = '-', Integer, '.' [, Integer]
  /// 	StringValue = '"', { Alpha }, '"'
  /// 	SetValue = '(' EnumerationValue, {',' EnumerationValue } ')'
  /// 	EnumerationValue = { Alpha }
  ///		Alpha = 'A' - 'Z' | 'a' - 'z'
  ///		Digit = '0' - '9'
  /// </summary>
  public class Tokenizer
  {
    //Constants:
    public const string tkn_entry_exit_start = "{";
    public const string tkn_entry_exit_end = "}";
    public const string tkn_parenthesis_open = "(";
    public const string tkn_parenthesis_close = ")";
    public const string tkn_parameter_separator = ",";
    public const string tkn_trigger_action = "->";
    public const string tkn_string_start_end = "\"";         //characters that can be used for string start and end
    public const string tkn_string_escape = "\\";            //string escape character
    public const string tkn_whitespace = " \t\n";            //whitespace characters ignored during tokenizing
    public const string tkn_logical_not = "!";
    public const string tkn_logical_and = "&&";
    public const string tkn_logical_or = "||";
    public const string tkn_comparison_equal = "==";
    public const string tkn_comparison_not_equal = "!=";
    public const string tkn_comparison_less = "<";
    public const string tkn_comparison_greater = ">";
    public const string tkn_comparison_less_equal = "<=";
    public const string tkn_comparison_greater_equal = ">=";
    public const string tkn_boolean_true = "true";
    public const string tkn_boolean_false = "false";
    public const string tkn_empty = "";                      //special token for empty string
    public const string tkn_positive = "+";
    public const string tkn_negative = "-";
    public const string tkn_set_open = "(";
    public const string tkn_set_close = ")";
    public const string set_alpha = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string set_digit = "0123456789";
    public const string set_alphanumeric = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string set_identifier = "_0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string set_double = "0123456789.";

    //Properties:
    public List<TokenString> StringTokens { get { return _stringTokens; } }
    public Stack<Token> Tokens { get { return _tokens; } }

    //Attributes:
    protected Stack<Token> _tokens;
    protected List<Token> _tokensList;
    protected string _script;
    protected int _parsingPosition;
    protected Regex _regexIdentifier;
    protected Regex _regexString;
    protected Regex _regexInteger;
    protected Regex _regexDouble;

    public Tokenizer(string script)
    {
      _script = script;
      _tokens = new Stack<Token>();
      _stringTokens = new List<TokenString>();
      _regexIdentifier = new Regex("^[_\\w\\d]+$");
      _regexString = new Regex("^\\\"[_\\w\\d\\s]+\\\"$");
      _regexInteger = new Regex("^[0-9]+$");
      _regexDouble = new Regex("^[0-9]+\\.([0-9]+)?$");
      loadScript();
    }

    protected Stack<TokenString> _stringTokenStack;
    protected List<TokenString> _stringTokens;

    /// <summary>
    /// Approach using a temporary stack for tokens.
    /// </summary>
    private TokenString nextToken()
    {
      TokenString result = null;
      string currentChar = "";
      string previousChar = "";
      int previousCharPos = 0;
      bool tokenComplete = false;

      //first return items from the token stack previously recorded else get next token
      if (_stringTokenStack.Count > 0)
        result = _stringTokenStack.Pop();
      else
      {
        //process next string token
        result = new TokenString();
        result.token = "";

        //extract the next token
        while (_parsingPosition < _script.Length && !tokenComplete)
        {
          //get next character in token to pricess
          currentChar = _script[_parsingPosition].ToString();

          //construct character sequence of previous and current character for lookback on certain tokens
          string previousAndCurrentChar = string.Concat(previousChar, currentChar);

          //process tokens based on current character or combined previous and current character
          if (currentChar == tkn_entry_exit_start ||
              currentChar == tkn_entry_exit_end ||
              currentChar == tkn_parenthesis_open ||
              currentChar == tkn_parenthesis_close ||
              currentChar == tkn_set_open ||
              currentChar == tkn_set_close ||
              currentChar == tkn_parameter_separator ||
              currentChar == tkn_logical_not ||
              currentChar == tkn_positive)
          {
            //if we already have a token we just return it otherwise we return this special character
            if (result.token.Length != 0)
            {
              TokenString token = new TokenString();
              token.token = currentChar;
              token.startPosition = _parsingPosition;
              _parsingPosition++;
              _stringTokenStack.Push(token);
            }
            else
            {
              result.token = currentChar;
              result.startPosition = _parsingPosition;
              _parsingPosition++;
            }

            //token is complete at this stage
            tokenComplete = true;

          }
          else if (result.token == tkn_boolean_true ||
                   result.token == tkn_boolean_false)
          {
            //boolean value token, terminate token extraction and move to next character for next token extraction
            previousCharPos = _parsingPosition;
            _parsingPosition++;
            tokenComplete = true;

          }
          else if (previousAndCurrentChar == tkn_logical_and ||
                   previousAndCurrentChar == tkn_logical_or ||
                   previousAndCurrentChar == tkn_comparison_equal ||
                   previousAndCurrentChar == tkn_comparison_not_equal ||
                   previousAndCurrentChar == tkn_comparison_less_equal ||
                   previousAndCurrentChar == tkn_comparison_greater_equal)
          {
            //handle the set of well defined tokens, terminate token extraction and move to next character for next token extraction
            if (result.token.Length > 0)
            {
              //return current token and push operator onto the stack for next call
              TokenString token = new TokenString();
              token.token = previousAndCurrentChar;
              token.startPosition = previousCharPos;
              _stringTokenStack.Push(token);

            }
            else
            {
              //simply return the token
              result.token = previousAndCurrentChar;
              result.startPosition = previousCharPos;
            }
            tokenComplete = true;
            _parsingPosition++;

          }
          else if (previousAndCurrentChar == tkn_trigger_action)
          {
            //check whether we need to return the current result that could contain an identifier, we need to step back
            //so that we can reprocess the trigger action token
            if (result.token.Length > 0)
            {
              //add the trigger action onto the stack and return current token
              TokenString token = new TokenString();
              token.token = tkn_trigger_action;
              token.startPosition = previousCharPos;
              _stringTokenStack.Push(token);
            }
            else
            {
              //simply return the trigger action
              result.token = tkn_trigger_action;
              result.startPosition = previousCharPos;
            }

            _parsingPosition++;
            tokenComplete = true;

          }
          else if (currentChar == tkn_comparison_less ||
                   currentChar == tkn_comparison_greater ||
                   currentChar == tkn_negative)
          {
            //move to next character since we need more context for these characters
            previousCharPos = _parsingPosition;
            _parsingPosition++;

          }
          else if (currentChar == tkn_string_start_end)
          {
            //extract string value, we allow string escape of " using \
            result.startPosition = _parsingPosition;
            result.token = string.Concat(result.token, currentChar);
            _parsingPosition++;

            while (_parsingPosition < _script.Length && !tokenComplete)
            {
              currentChar = _script[_parsingPosition].ToString();
              result.token = string.Concat(result.token, currentChar);
              tokenComplete = currentChar == tkn_string_start_end && previousChar != tkn_string_escape;
              previousChar = currentChar;
              previousCharPos = _parsingPosition;
              _parsingPosition++;
            }

            //raise exception if the string was not properly terminated
            if (!tokenComplete)
              throw new Exception(String.Format("Unterminated string found at position {0}.", result.startPosition));

            //NOTE: Complete string already processed so we do not have to move the parser position forward.

          }
          else if (tkn_whitespace.Contains(currentChar))
          {
            //return any value processed to far but also check for the comparison or negation of a number to process
            if (previousChar == tkn_comparison_less ||
                previousChar == tkn_comparison_greater ||
                previousChar == tkn_negative)
            {
              if (result.token.Length > 0)
              {
                //push comparison/sign onto the stack for next call
                TokenString token = new TokenString();
                token.token = previousChar;
                token.startPosition = previousCharPos;
                _stringTokenStack.Push(token);
              }
              else
              {
                //simply result the comparison/sign
                result.token = previousChar;  //return the previous character as token if it is set
                result.startPosition = previousCharPos;
              }
            }

            tokenComplete = result.token.Length != 0;
            _parsingPosition++;

          }
          else if (set_identifier.Contains(currentChar) ||
                   set_double.Contains(currentChar))
          {
            //if previous chracter is an inequality operator we first need to return it before processing the new identifier/number character
            if (previousChar == tkn_comparison_less ||
                previousChar == tkn_comparison_greater ||
                previousChar == tkn_negative)
            {
              //check whether we need to return result before processing the inequality symbol, in that case we need to back track
              //to the inequality symbol and first return the token constructed
              if (result.token.Length > 0)
              {
                TokenString token = new TokenString();
                token.token = previousChar;
                token.startPosition = previousCharPos;
                _stringTokenStack.Push(token);
              }
              else
              {
                result.token = previousChar;
                result.startPosition = previousCharPos;
              }

              tokenComplete = true;

            }
            else
            {
              //build the identifier or number value and move to next character, we'll return token on first whitespace
              //encountered or other language token
              result.token = string.Concat(result.token, currentChar);
              if (result.startPosition == 0)
                result.startPosition = _parsingPosition;
              previousCharPos = _parsingPosition;
              _parsingPosition++;
            }
          }
          else
          {
            //some other character, move parser forward to get next character
            previousCharPos = _parsingPosition;
            _parsingPosition++;
          }

          //keep back reference to the current character if it is a non-whitespace character
          if (!tkn_whitespace.Contains(currentChar))
            previousChar = currentChar;
        }
      }

      return result;
    }

    /// <summary>
    /// Reduces the script into it's constituent tokens for processing.
    /// </summary>
    private void loadScript()
    {
      tokenize();
      lexer();
    }

    /// <summary>
    /// Reduces the scrip to it's constituent tokens for processing.
    /// </summary>
    private void tokenize()
    {
      _parsingPosition = 0;
      _stringTokenStack = new Stack<TokenString>();

      //trim any leading/trailing spaces from script
      _script = _script.Trim(tkn_whitespace.ToCharArray());

      while (_parsingPosition < _script.Length || _stringTokenStack.Count > 0)
      {
        TokenString token = nextToken();
        _stringTokens.Add(token);
      }

      _stringTokenStack.Clear();
      _stringTokens.Reverse();
      _stringTokenStack = new Stack<TokenString>(_stringTokens);
      _stringTokens.Reverse();
    }

    /// <summary>
    /// Performs lexical analysis on the set of tokens in the script and loads them onto the token stack.
    /// </summary>
    private void lexer()
    {
      _tokensList = new List<Token>();

      while (_stringTokenStack.Count > 0)
      {
        TokenString token = _stringTokenStack.Peek();

        //process only certain expected start tokens
        if (token.token == tkn_entry_exit_start)
          lexEntryExit();
        else if (token.token == tkn_parenthesis_open)
          lexParenthesis();
        else if (token.token == tkn_positive ||
                 token.token == tkn_negative)
          lexSignedNumber();
        else if (token.token == tkn_boolean_true ||
                 token.token == tkn_boolean_false)
          lexBoolean();
        else if (token.token == tkn_logical_not)
          lexLogicalNot();
        else if (token.token == tkn_logical_and ||
                 token.token == tkn_logical_or)
          lexLogicalOperator();
        else if (token.token == tkn_comparison_equal ||
                   token.token == tkn_comparison_not_equal ||
                   token.token == tkn_comparison_less ||
                   token.token == tkn_comparison_greater ||
                   token.token == tkn_comparison_less_equal ||
                   token.token == tkn_comparison_greater_equal)
          lexComparisonOperator();
        else if (_regexInteger.IsMatch(token.token))
          lexInteger();
        else if (_regexDouble.IsMatch(token.token))
          lexDouble();
        else if (_regexString.IsMatch(token.token))
          lexString();
        else if (_regexIdentifier.IsMatch(token.token))
          lexIdentifier();
        else
          //unexected token found, raise exception
          throw new Exception(string.Format("Unexpected token \"{0}\" found at position {1}.", token.token, token.startPosition));
      }

      _tokensList.Reverse();
      _tokens = new Stack<Token>(_tokensList);
    }

    /// <summary>
    /// General lexer function to process the next token it encounters, used to process non-terminal tokens.
    /// </summary>
    private void lexToken()
    {
      //peek the next token
      TokenString token = _stringTokenStack.Peek();

      //process the token accordingly
      if (token.token == tkn_parenthesis_open)
        lexParenthesis();
      else if (token.token == tkn_positive ||
                  token.token == tkn_negative)
        lexSignedNumber();
      else if (token.token == tkn_boolean_true ||
               token.token == tkn_boolean_false)
        lexBoolean();
      else if (token.token == tkn_logical_not)
        lexLogicalNot();
      else if (token.token == tkn_trigger_action)
        lexTriggerAction();
      else if (token.token == tkn_logical_and ||
               token.token == tkn_logical_or)
        lexLogicalOperator();
      else if (token.token == tkn_comparison_equal ||
               token.token == tkn_comparison_not_equal ||
               token.token == tkn_comparison_less ||
               token.token == tkn_comparison_greater ||
               token.token == tkn_comparison_less_equal ||
               token.token == tkn_comparison_greater_equal)
        lexComparisonOperator();
      else if (_regexInteger.IsMatch(token.token))
        lexInteger();
      else if (_regexDouble.IsMatch(token.token))
        lexDouble();
      else if (_regexString.IsMatch(token.token))
        lexString();
      else if (_regexIdentifier.IsMatch(token.token))
        lexIdentifier();
      else
        //unexected token found, raise exception
        throw new Exception(string.Format("Unexpected token \"{0}\" found at position {1}.", token.token, token.startPosition));
    }

    /// <summary>
    /// Perform lexical analysis on tokens for a strategy entry/exit.
    /// </summary>
    private void lexEntryExit()
    {
      //if token is the start of the entry/exit add the designator to the stack, otherwise just pop the end
      TokenString startToken = _stringTokenStack.Pop();
      TokenString endToken = startToken;

      Token token = new Token();
      token.type = TokenType.EntryExitStart;
      token.start = startToken.startPosition;
      _tokensList.Add(token);

      //keep on lexing the inner tokens until the strategy end is reached
      while (_stringTokenStack.Count > 0 && endToken.token != tkn_entry_exit_end)
      {
        lexToken();
        endToken = _stringTokenStack.Peek();
      }

      //remove end if it was found, else raise exception
      if (endToken.token == tkn_entry_exit_end)
      {
        _stringTokenStack.Pop();

        token = new Token();
        token.type = TokenType.EntryExitEnd;
        token.start = startToken.startPosition;
        _tokensList.Add(token);
      }
      else
        throw new Exception(string.Format("Entry/exit starting at character {0} was not terminated.", startToken.startPosition));
    }

    /// <summary>
    /// Perform lexical analysis on tokens contained in parentheses, should be a condition or a complex condition.
    /// </summary>
    private void lexParenthesis()
    {
      //process start of complex condition
      TokenString startToken = _stringTokenStack.Pop();
      TokenString endToken = startToken;

      Token token = new Token();
      token.type = TokenType.ComplexCondition;
      token.text = startToken.token;
      token.start = startToken.startPosition;
      _tokensList.Add(token);

      //keep on lexing the inner tokens until the close parenthesis is found
      while (_stringTokenStack.Count > 0 && endToken.token != tkn_parenthesis_close)
      {
        lexToken();
        endToken = _stringTokenStack.Peek();
      }

      //remove end if it was found, else raise exception
      if (endToken.token == tkn_parenthesis_close)
        _stringTokenStack.Pop();
      else
        throw new Exception(string.Format("Missing closing of condition starting at position {0}.", startToken.startPosition));
    }

    /// <summary>
    /// Perform lexical analysis for positive/negative number
    /// </summary>
    private void lexSignedNumber()
    {
      TokenString sign = _stringTokenStack.Pop();
      TokenString value = _stringTokenStack.Peek();

      //process number value or raise exception
      if (_regexInteger.IsMatch(value.token))
        lexInteger();
      else if (_regexDouble.IsMatch(value.token))
        lexDouble();
      else
        throw new Exception(string.Format("Sign \"{0}\" at position {1} must be followed by a number value.", sign.token, sign.startPosition));

      //negate number of the sign is negative else just ignore it
      if (sign.token == tkn_negative)
      {
        Token token = _tokensList.ElementAt(_tokensList.Count - 1);
        _tokensList.RemoveAt(_tokensList.Count - 1);
        token.text = string.Concat(tkn_negative, token.text);
        _tokensList.Add(token);
      }
    }

    /// <summary>
    /// Perform lexical analysis for boolean value.
    /// </summary>
    private void lexBoolean()
    {
      TokenString tokenString = _stringTokenStack.Pop();
      Token token = new Token();

      token.type = TokenType.Value;
      token.valueType = ValueTypeType.Boolean;
      token.text = tokenString.token;
      token.start = tokenString.startPosition;
      _tokensList.Add(token);
    }

    /// <summary>
    /// Perform lexical analysis for negated boolean or condition.
    /// </summary>
    private void lexLogicalNot()
    {
      TokenString tokenString = _stringTokenStack.Pop();
      Token token = new Token();

      token.type = TokenType.UniaryLogicalOperator;
      token.text = tokenString.token;
      token.start = tokenString.startPosition;
      _tokensList.Add(token);
    }

    /// <summary>
    /// Perform trigger action lexical analysis.
    /// </summary>
    private void lexTriggerAction()
    {
      TokenString tokenString = _stringTokenStack.Pop();
      Token token = new Token();

      token.type = TokenType.TriggerAction;
      token.text = tokenString.token;
      token.start = tokenString.startPosition;
      _tokensList.Add(token);
    }

    /// <summary>
    /// Perform lexical analysis on logical operator.
    /// </summary>
    private void lexLogicalOperator()
    {
      TokenString tokenString = _stringTokenStack.Pop();

      Token token = new Token();
      token.type = TokenType.BinaryLogicalOperator;
      token.text = tokenString.token;
      token.start = tokenString.startPosition;
      _tokensList.Add(token);
    }

    /// <summary>
    /// Perform lexical analysis on comparison of function result or value.
    /// </summary>
    private void lexComparisonOperator()
    {
      //add the comparison operator
      TokenString tokenString = _stringTokenStack.Pop();

      Token token = new Token();
      token.type = TokenType.ComparisonOperator;
      token.text = tokenString.token;
      token.start = tokenString.startPosition;
      _tokensList.Add(token);
    }

    /// <summary>
    /// Perform lexical analysis of an identifier that can be a function call or an enumeration.
    /// </summary>
    private void lexIdentifier()
    {
      //get the identifier token
      TokenString tokenString = _stringTokenStack.Pop();
      Token token = new Token();

      //see whether we're working with a function call or an enumeration
      TokenString nextToken = _stringTokenStack.Peek();

      if (nextToken.token == tkn_parenthesis_open)
      {
        //identifier is for a function call
        token.start = tokenString.startPosition;
        string functionName = tokenString.token.ToLower();   //function names must be lower case to process in parser
        token.text = functionName;
        token.type = TokenType.FunctionStart;
        _tokensList.Add(token);

        //process the parameter list
        TokenString startParmToken = _stringTokenStack.Pop();

        if (startParmToken.token != tkn_parenthesis_open)
          throw new Exception(string.Format("Function call \"{0}\" should be followed by a parameter list.", tokenString.token));

        TokenString parameterToken = _stringTokenStack.Pop();   //get next token to process, should at least have a closing parenthesis
        while (_stringTokenStack.Count > 0 && parameterToken.token != tkn_parenthesis_close)
        {
          //check whether we're processing a parameter or not
          if (parameterToken.token != tkn_parameter_separator)
          {
            //add token for parameter designator
            token = new Token();
            token.start = parameterToken.startPosition;
            token.type = TokenType.Parameter;
            _tokensList.Add(token);

            //process the parameter value, add the parameter value and lex it
            _stringTokenStack.Push(parameterToken);
            lexToken();
          }

          //get next parameter to process
          parameterToken = _stringTokenStack.Pop();
        }

        //raise exception if parameter list it not terminated
        if (parameterToken.token != tkn_parenthesis_close)
          throw new Exception(string.Format("Function call \"{0}\" at position {1} parameter list not terminated.", tokenString.token, tokenString.startPosition));

        //add end token for the function call
        //NOTE: This is required so that we can differentiate between function calls where parameters of one function call is yet other function calls,
        //      e.g. function1(function2(param2_1, param2_2), param1_2) - this avoids parameter 1_2 being processed as part of function2's parameter list.
        token = new Token();
        token.start = parameterToken.startPosition;
        token.text = functionName;
        token.type = TokenType.FunctionEnd;
        _tokensList.Add(token);

      }
      else
      {
        //identifier is for an enumeration
        token.type = TokenType.Value;
        token.valueType = ValueTypeType.Enumeration;
        token.text = tokenString.token.ToLower();   //enumerated values are always lower case
        token.start = tokenString.startPosition;
        _tokensList.Add(token);
      }
    }

    /// <summary>
    /// Perform lexical analysis on string value.
    /// </summary>
    private void lexString()
    {
      TokenString tokenString = _stringTokenStack.Pop();
      Token token = new Token();

      token.type = TokenType.Value;
      token.valueType = ValueTypeType.String;
      token.text = tokenString.token;
      token.start = tokenString.startPosition;
      _tokensList.Add(token);
    }

    /// <summary>
    /// Perform lexical analysis on integer value.
    /// </summary>
    private void lexInteger()
    {
      TokenString tokenString = _stringTokenStack.Pop();
      Token token = new Token();

      token.type = TokenType.Value;
      token.valueType = ValueTypeType.Integer;
      token.text = tokenString.token;
      token.start = tokenString.startPosition;
      _tokensList.Add(token);
    }

    /// <summary>
    /// Perform lexical analysis on double value.
    /// </summary>
    private void lexDouble()
    {
      TokenString tokenString = _stringTokenStack.Pop();
      Token token = new Token();

      token.type = TokenType.Value;
      token.valueType = ValueTypeType.Double;
      token.text = tokenString.token;
      token.start = tokenString.startPosition;
      _tokensList.Add(token);
    }
  }
  #endregion

  #region Strategy Script Interpreter
  /// <summary>
  /// EntryExit script interpreter used to evaluate a strategy script.
  /// </summary>
  public class Interpreter
  {
    //constants:
    public const int MAX_DATA_STREAM = 3;   //maximum number of data streams supported for multiple time frame analysis

    //Properties:
    public string Script {
      get { return _script; }
      set {
        reset();
        _script = value;
        parseScript();
      }
    }

    public _StrategyExplorer Strategy { get { return _strategy; } }

    //Attributes:
    protected string _script;
    protected _StrategyExplorer _strategy;
    protected List<FunctionFactory> _functionFactories;     //set of supported function factories
    protected List<FunctionFactory> _runtimeFactories;      //set of function factories to load and update funtions from
    protected List<EntryExit> _runtimeEntryExit;            //set of run-time entry/exit's to evaluate for this run
    private Regex _regexInteger;
    private Regex _regexDouble;
    private Regex _regexString;

    /// <summary>
    /// Initializes the strategy script interpreter with the set of order types that can be used to manage the strategy.
    /// </summary>
    public Interpreter(_StrategyExplorer strategy)
    {
      //create basic attributes
      _strategy = strategy;
      _script = "";
      _functionFactories = new List<FunctionFactory>();
      _runtimeFactories = new List<FunctionFactory>();
      _runtimeEntryExit = new List<EntryExit>();
      _regexInteger = new Regex("[-]?[\\d]+");
      _regexDouble = new Regex("[-]?[\\d]+[\\.[\\d]*]?");
      _regexString = new Regex("[\"\'].*[\"\']");
    }

    /// <summary>
    /// Method creates the required components for the defined set of factories.
    /// </summary>
    public void Create()
    {
      //define the whole set of supported function factories
      defineFunctionFactories();

      //reset the interpreter state
      reset();

      //create the components for all the defined factories
      foreach (FunctionFactory ff in _functionFactories)
        ff.Create();
    }

    /// <summary>
    /// Method called before calculation is started.
    /// </summary>
    public void StartCalc()
    {
      //prepare the set of function factories for the script run
      foreach (FunctionFactory ff in _functionFactories)
        ff.StartCalc();
    }

    /// <summary>
    /// Method called when calculation is finished.
    /// </summary>
    public void StopCalc()
    {
      foreach (FunctionFactory ff in _functionFactories)
        ff.StopCalc();
    }

    /// <summary>
    /// Allows the set of function factories to update themselves and then evaluates the set of defined strategies.
    /// </summary>
    public void Evaluate()
    {
      foreach (FunctionFactory ff in _runtimeFactories)
        ff.Evaluate();
      foreach (EntryExit ee in _runtimeEntryExit)
        ee.Evaluate();
    }

    /// <summary>
    /// Adds a supported function factory to the set of function factories to evaluate at run-time.
    /// </summary>
    public void Import(string factoryName)
    {
      //try to find the factory to load
      FunctionFactory factory = null;
      foreach (FunctionFactory ff in _functionFactories)
      {
        if (ff.Name == factoryName)
        {
          factory = ff;
          break;      //found the factory, terminate search loop
        }
      }

      //raise error if user is trying to load an unsupported factory
      if (factory == null)
        throw new Exception(String.Format("Function factory with name \"{0}\" not supported.", factoryName));

      //load the function factory if not yet loaded
      if (!_runtimeFactories.Contains(factory))
        _runtimeFactories.Add(factory);
    }
    /// <summary>
    /// Extends the language capability by adding function factories that support functions that can be called.
    /// </summary>
    protected void defineFunctionFactories()
    {
      _functionFactories.Add(new CoreFunctions(this));
      _functionFactories.Add(new OrderFunctions(this));
      _functionFactories.Add(new VolumeFunctions(this));
      _functionFactories.Add(new MovingAverageFunctions(this));
      _functionFactories.Add(new IchimokuFunctions(this));
      _functionFactories.Add(new PriceActionFunctions(this));
      _functionFactories.Add(new KeltnerChannelFunctions(this));
      _functionFactories.Add(new RSIFunctions(this));
      _functionFactories.Add(new MACDFunctions(this));
      _functionFactories.Add(new ADXFunctions(this));
    }

    /// <summary>
    /// Resets the interpreter state to an initial state.
    /// </summary>
    protected void reset()
    {
      //clear the current run-time entry/exit and reparse the script
      _runtimeEntryExit.Clear();
      _runtimeFactories.Clear();

      //ensure the basic language functions are always loaded
      Import("core");
      Import("orders");
    }

    /// <summary>
    /// Implements parsing of the above grammar to construct the list of strategies that should be run.
    /// </summary>
    private void parseScript()
    {
      //tokenize the script and process it
      Tokenizer tokenizer = new Tokenizer(_script);
      Stack<Token> tokens = tokenizer.Tokens;

      //process the token stack until an error occurs or there's nothing left to process
      Token token = null;

      while (tokens.Count > 0)
      {
        //check that we have a strategy start
        token = tokenizer.Tokens.Peek();
        if (token.type == TokenType.EntryExitStart)
        {
          EntryExit entryExit = processEntryExit(tokens);
          _runtimeEntryExit.Add(entryExit);

        }
        else if (token.type == TokenType.FunctionStart)
        {
          //get non-strategy function to evaluate once-off to perform some operation
          Function f = processFunction(FunctionType.Factory, tokens);
          f.Evaluate();
        }
        else
          throw new Exception(String.Format("Unexpected token {0} encountered at position {1}.", token.text, token.start));
      }
    }

    /// <summary>
    /// Implements the logic to parse an entry/exit within the stategy.
    /// </summary>
    private EntryExit processEntryExit(Stack<Token> tokens)
    {
      //parse the set of tokens that make up the entry/exit
      bool parseComplete = false;
      EntryExit result = new EntryExit();
      bool parsingActions = false;
      Token startToken = null;
      IEvaluatable term = null;

      //process the set of tokens that make up the strategy
      while (!parseComplete && tokens.Count > 0)
      {
        Token token = tokens.Peek();
        if (startToken == null)
          startToken = token;

        switch (token.type)
        {
          case TokenType.EntryExitStart:
            tokens.Pop();
            break;
          case TokenType.EntryExitEnd:
            tokens.Pop();
            parseComplete = true;
            break;
          case TokenType.FunctionStart:
            //parse the function in question within the correct entry/exit context
            if (parsingActions)
            {
              Function function = processFunction(FunctionType.Action, tokens);
              result.AddAction(function);
            }
            else
            {
              Function function = processFunction(FunctionType.Trigger, tokens);
              result.AddTrigger(function);
            }
            break;
          case TokenType.Value:
            //explicit values can only be used on trigger side
            if (parsingActions)
              throw new Exception(string.Format("Value \"{0}\" at position {1} can not be used in action statement.", token.text, token.start));
            term = processValue(tokens);
            result.AddTrigger(term);
            break;
          case TokenType.ComparisonOperator:
            if (parsingActions)
              throw new Exception(string.Format("Comparison operator \"{0}\" at position {1} can not be used in action statement.", token.text, token.start));
            term = result.RemoveLastTrigger();
            Comparison comparison = processComparison(term, tokens);
            result.AddTrigger(comparison);
            break;
          case TokenType.BinaryLogicalOperator:
            if (parsingActions)
              throw new Exception(string.Format("Logical operator \"{0}\" at position {1} can not be used in action statement.", token.text, token.start));
            term = result.RemoveLastTrigger();
            BinaryLogicalOperator binaryLogicalOperator = processBinaryLogicalOperator(term, tokens);
            result.AddTrigger(binaryLogicalOperator);
            break;
          case TokenType.TriggerAction:
            //remove trigger action designator and start parsing actions
            tokens.Pop();
            parsingActions = true;
            break;
          default:
            throw new Exception(string.Format("Unexpeccted token \"{0}\" encountered in entry/exit statement at position {1}.", token.text, token.start));
        }
      }

      //check that the entry/exit contained the expected number of tokens
      if (!parseComplete)
        throw new Exception(string.Format("Entry/exit starting at position {0} does not terminate properly.", startToken.start));

      return result;
    }

    /// <summary>
    /// Implements the logic to parse a function call in the scripting language.
    /// </summary>
    private Function processFunction(FunctionType expectedType, Stack<Token> tokens)
    {
      List<Parameter> parameters = new List<Parameter>();
      FunctionFactory factory = null;
      Function result = null;

      //try to find function associated with the trigger
      Token function = tokens.Pop();

      //try to find a factory supporting the given function
      foreach (FunctionFactory f in _runtimeFactories)
      {
        if (f.Functions.Contains(function.text))
        {
          factory = f;
          break;
        }
      }

      //check that function could be found
      if (factory == null)
        throw new Exception(String.Format("Function call '{0}' at position {1} not loaded or not supported.", function.text, function.start));

      //parse any parameters to pass to the function
      Token parameterToken = tokens.Peek();
      Token valueToken = null;

      while (parameterToken.type == TokenType.Parameter)
      {
        //get the parameter value
        Parameter parameter = null;
        parameterToken = tokens.Pop();   //remove the parameter token
        valueToken = tokens.Peek();      //look at what token type to process for the value

        //ensure that the next token is a value
        if (valueToken.type == TokenType.Value)
        {
          //construct the parameter to pass to the function
          ValueType value = processValue(tokens);
          parameter = new Parameter(value);

        }
        else if (valueToken.type == TokenType.FunctionStart)
        {
          //try to parse a function call
          Function f = processFunction(FunctionType.Any, tokens);
          parameter = new Parameter(f);

        }
        else
          throw new Exception(string.Format("Unexpected token in parameter list as position {0}, value expected.", valueToken.start, valueToken.text));

        //add parameter to the list of parameters passed to function
        parameters.Add(parameter);

        //check whether there's another parameter to pass
        parameterToken = tokens.Peek();
      }

      //remove the function-end token from the token stack
      tokens.Pop();

      //retrieve the function to be called
      result = factory.CreateFunction(function.text, parameters);

      //check that the function is used in the correct context
      if (expectedType != FunctionType.Any && expectedType != result.Type)
        throw new Exception(string.Format("Function \"{0}\" called at position {1} is called in the incorrect context.", function.text, function.start));

      return result;
    }

    /// <summary>
    /// Processes a token containing an explicit value.
    /// </summary>
    private ValueType processValue(Stack<Token> tokens)
    {
      Token token = tokens.Pop();

      ValueType result = null;
      switch (token.valueType)
      {
        case ValueTypeType.Boolean:
          bool valueBool = false;
          if (token.text == "true")
            valueBool = true;
          result = new ValueType(valueBool);
          break;
        case ValueTypeType.String:
          string valueString = token.text;
          valueString = valueString.TrimStart(Tokenizer.tkn_string_start_end.ToCharArray());
          valueString = valueString.TrimEnd(Tokenizer.tkn_string_start_end.ToCharArray());
          result = new ValueType(valueString, false);
          break;
        case ValueTypeType.Enumeration:
          string enumString = token.text;
          enumString = enumString.TrimStart(Tokenizer.tkn_string_start_end.ToCharArray());
          enumString = enumString.TrimEnd(Tokenizer.tkn_string_start_end.ToCharArray());
          result = new ValueType(enumString, true);
          break;
        case ValueTypeType.Integer:
          int valueInt = Int32.Parse(token.text);
          result = new ValueType(valueInt);
          break;
        case ValueTypeType.Double:
          double valueDouble = Double.Parse(token.text);
          result = new ValueType(valueDouble);
          break;
        case ValueTypeType.Void:
          //this should not happen
          throw new Exception(string.Format("Unable to process void type value \"{0}\" at position {1}.", token.text, token.start));
      }

      return result;
    }

    /// <summary>
    /// Constructs a comparison with the given left term and tokens from the right term.
    /// </summary>
    private Comparison processComparison(IEvaluatable leftTerm, Stack<Token> tokens)
    {
      //get comparison token
      Token operatorToken = tokens.Pop();
      ComparisonType operatorType = ComparisonType.Equal;

      switch (operatorToken.text)
      {
        case Tokenizer.tkn_comparison_equal:
          operatorType = ComparisonType.Equal;
          break;
        case Tokenizer.tkn_comparison_not_equal:
          operatorType = ComparisonType.NotEqual;
          break;
        case Tokenizer.tkn_comparison_greater:
          operatorType = ComparisonType.Greater;
          break;
        case Tokenizer.tkn_comparison_less:
          operatorType = ComparisonType.Less;
          break;
        case Tokenizer.tkn_comparison_greater_equal:
          operatorType = ComparisonType.GreaterEqual;
          break;
        case Tokenizer.tkn_comparison_less_equal:
          operatorType = ComparisonType.LessEqual;
          break;
        default:
          throw new Exception(string.Format("Unexpected operator \'{0}\' found for comparison at position {1}.", operatorToken.text, operatorToken.start));
      }

      //get the right hand term of the comparison
      IEvaluatable rightTerm = null;
      Token rightToken = tokens.Peek();
      if (rightToken.type == TokenType.Value)
      {
        //construct the parameter to pass to the function
        rightTerm = processValue(tokens);

      }
      else if (rightToken.type == TokenType.FunctionStart)
      {
        //try to parse a function call
        rightTerm = processFunction(FunctionType.Any, tokens);

      }
      else
        throw new Exception(string.Format("Unexpected token in comparison as position {0}, right hand term expected.", rightToken.start, rightToken.text));

      Comparison result = new Comparison();
      result.Left = leftTerm;
      result.Type = operatorType;
      result.Right = rightTerm;
      return result;
    }

    /// <summary>
    /// Constructs a NOT logical operator from the token stack.
    /// </summary>
    private NotLogicalOperator processNotLogicalOperator(Stack<Token> tokens)
    {
      IEvaluatable term = null;

      //remove NOT operator token
      tokens.Pop();

      //parse the term associated with the operator
      Token token = tokens.Peek();

      //ENHANCEMENT: Implement more complex support for parenthesis with complex conditions, e.g. !(x > 10).
      if (token.type == TokenType.FunctionStart)
        term = processFunction(FunctionType.Trigger, tokens);
      else
        throw new Exception(string.Format("NOT-operator at postiion {0} should only be used in conjunction with a function call.", token.start));

      //construct logical NOT of term
      NotLogicalOperator result = new NotLogicalOperator();
      result.Term = term;
      return result;
    }

    /// <summary>
    /// Constructs a boolean operator to combine a set of conditions from the token stack adding the left term as the
    /// first term of the boolean operator.
    /// </summary>
    private BinaryLogicalOperator processBinaryLogicalOperator(IEvaluatable leftTerm, Stack<Token> tokens)
    {
      //get boolean operator to apply
      Token operatorToken = tokens.Pop();
      BinaryLogicalOperatorType operatorType = BinaryLogicalOperatorType.And;

      if (operatorToken.text == Tokenizer.tkn_logical_and)
        operatorType = BinaryLogicalOperatorType.And;
      else if (operatorToken.text == Tokenizer.tkn_logical_or)
        operatorType = BinaryLogicalOperatorType.Or;
      else
        throw new Exception(string.Format("Unexpected operator \"{0}\" found for boolean statement at position {1}", operatorToken.text, operatorToken.start));

      //get the right hand term for the boolean operator, this could be a boolean function, boolean value or another comparison
      IEvaluatable rightTerm = null;
      Token rightToken = tokens.Peek();
      if (rightToken.type == TokenType.Value)
      {
        //construct the value used for the right term, the term should most likely be combined into a comparison
        rightTerm = processValue(tokens);

      }
      else if (rightToken.type == TokenType.FunctionStart)
      {
        //try to parse a function call
        rightTerm = processFunction(FunctionType.Any, tokens);
      }
      else if (rightToken.type == TokenType.ComplexCondition)
      {
        //try to process complex condition
        rightTerm = processComplexCondition(tokens);
      }
      else if (rightToken.type == TokenType.UniaryLogicalOperator)
      {
        //try to process not logical operator
        rightTerm = processNotLogicalOperator(tokens);
      }
      else
        throw new Exception(string.Format("Unexpected token in comparison as position {0}, right hand term expected.", rightToken.start, rightToken.text));

      //see whether we need to process another comparison for the right term
      rightToken = tokens.Peek();
      if (rightToken.type == TokenType.ComparisonOperator)
        rightTerm = processComparison(rightTerm, tokens);

      //check that the user never combines a non-boolean right term value into a logical boolean operator
      ValueType value = rightTerm as ValueType;
      if (value != null && value.Type != ValueTypeType.Boolean)
        throw new Exception(string.Format("Can not combine non-boolean value into binary operation \"{0}\" at position {1}.", operatorToken.text, operatorToken.start));

      //check whether the left term is a binary logical operator of the same type and just add the right term to it, this will allow one operator to encapsulate all the
      //logical terms if they are combined using the same logical operator, e.g. a && b && c would all be added to one AND logical operator
      BinaryLogicalOperator result = leftTerm as BinaryLogicalOperator;
      if (result != null)
        if (result.Type == operatorType)
          result.AddTerm(rightTerm);
        else
          result = null;  //can not reuse the binary logical operator, we need to create another one since the operator is different

      if (result == null)
      {
        result = new BinaryLogicalOperator();
        result.Type = operatorType;
        result.AddTerm(leftTerm);
        result.AddTerm(rightTerm);
      }

      return result;
    }

    /// <summary>
    /// Constructs an evaluatable term from the set of tokens contained in a complex condition, e.g. ( term operator term )
    /// </summary>
    private IEvaluatable processComplexCondition(Stack<Token> tokens)
    {
      //parse the set of tokens that make up the entry/exit
      bool parseComplete = false;
      IEvaluatable result = null;
      Token startToken = null;

      //process the set of tokens that make up the complex condition/statement
      while (!parseComplete && tokens.Count > 0)
      {
        Token token = tokens.Peek();
        if (startToken == null)
          startToken = token;

        switch (token.type)
        {
          case TokenType.ComplexCondition:
            tokens.Pop();
            break;
          case TokenType.FunctionStart:
            result = processFunction(FunctionType.Trigger, tokens);
            break;
          case TokenType.Value:
            result = processValue(tokens);
            break;
          case TokenType.ComparisonOperator:
            result = processComparison(result, tokens);
            break;
          case TokenType.BinaryLogicalOperator:
            result = processBinaryLogicalOperator(result, tokens);
            break;
          case TokenType.UniaryLogicalOperator:
            result = processNotLogicalOperator(tokens);
            break;
          default:
            //some other token that must be processed by calling routine
            parseComplete = true;
            break;
        }
      }

      //check that the entry/exit contained the expected number of tokens
      if (!parseComplete)
        throw new Exception(string.Format("Complex statement starting at position {0} does not terminate properly.", startToken.start));

      return result;
    }
  }
  #endregion

  #region Base Functional Implementation Classes
  /// <summary>
  /// Interface defining components that can be evaluated in the script to yield a result of some type (including a void type implying "no result" to report).
  /// </summary>
  public interface IEvaluatable
  {
    ValueType Evaluate();
  }

  /// <summary>
  /// Class to enumerate concrete values in the interpreter.
  /// </summary>
  public class ValueType : IEvaluatable
  {
    public ValueTypeType Type { get { return _type; } }
    public bool BooleanValue { get { return _booleanValue; } }
    public int IntegerValue { get { return _integerValue; } }
    public double DoubleValue { get { return _doubleValue; } }
    public string StringValue { get { return _stringValue; } }
    public string EnumerationValue { get { return _enumerationValue; } }

    //Attributes:
    protected ValueTypeType _type;
    protected bool _booleanValue;
    protected int _integerValue;
    protected double _doubleValue;
    protected string _stringValue;
    protected string _enumerationValue;

    public ValueType()
    {
      _type = ValueTypeType.Void;
    }

    public ValueType(bool value)
    {
      _type = ValueTypeType.Boolean;
      _booleanValue = value;
    }

    public ValueType(int value)
    {
      _type = ValueTypeType.Integer;
      _integerValue = value;
    }

    public ValueType(double value)
    {
      _type = ValueTypeType.Double;
      _doubleValue = value;
    }

    public ValueType(string value, bool enumeration = false)
    {
      if (!enumeration)
      {
        _type = ValueTypeType.String;
        _stringValue = value;
      }
      else
      {
        _type = ValueTypeType.Enumeration;
        _enumerationValue = value;
      }
    }

    /// <summary>
    /// Makes the value type evaluatable within the scripting concept.
    /// </summary>
    public virtual ValueType Evaluate()
    {
      return this;
    }
  }

  /// <summary>
  /// Class used to encapsulate parameters passed to functions can be either a value type or a function to be evaluated.
  /// </summary>
  public class Parameter : IEvaluatable
  {
    //Attributes:
    protected ValueType _value;
    protected Function _function;

    public Parameter(bool value)
    {
      _value = new ValueType(value);
      _function = null;
    }

    public Parameter(int value)
    {
      _value = new ValueType(value);
      _function = null;
    }

    public Parameter(double value)
    {
      _value = new ValueType(value);
      _function = null;
    }

    public Parameter(string value, bool enumeration = false)
    {
      _value = new ValueType(value);
      _function = null;
    }

    public Parameter(ValueType value)
    {
      _value = value;
      _function = null;

      //raise exception if parameter can not evaluate to a value
      if (_value == null && _function == null)
        throw new ArgumentException("Parameter can not be evaluated.");
    }

    public Parameter(Function function)
    {
      _value = null;
      _function = function;

      //raise exception if parameter can not evaluate to a value
      if (_value == null && _function == null)
        throw new ArgumentException("Parameter can not be evaluated.");
    }

    /// <summary>
    /// Evaluates the parameter and returns the resulting value.
    /// </summary>
    public ValueType Evaluate()
    {
      //explicit value has precedence over function
      ValueType result = null;

      if (null != _value)
        result = _value;
      else if (null != _function)
        result = _function.Evaluate();

      return result;
    }
  }

  /// <summary>
  /// Enumration of the function types used in the strategy script.
  /// </summary>
  public enum FunctionType
  {
    Any,            //Any type of function can be processed in this context.
    Factory,        //Factory function used to configure factory settings.
    Trigger,        //Trigger function used in trigger conditions.
    Action,         //Action function used when trigger conditions evaluate to true.
  }

  /// <summary>
  /// EntryExit scripting function that can be evaluated to determine some value type. The function can take a set of parameters that can be used during evaluation.
  /// </summary>
  public abstract class Function : IEvaluatable
  {
    //Properties:
    public string Name { get { return _name; } }
    public List<Parameter> Parameters { get { return _parameters; } }
    public FunctionType Type { get { return _type; } }

    //Attributes:
    protected FunctionType _type;
    protected string _name;
    protected FunctionFactory _parent;
    protected List<Parameter> _parameters;
    protected IOutput Output;

    public Function(FunctionType type, string name, FunctionFactory parent, List<Parameter> parameters)
    {
      _type = type;
      _name = name;
      _parent = parent;
      _parameters = parameters;
      if (_parameters == null)
        _parameters = new List<Parameter>();   //ensure we can never access null object
      Output = _parent.Interpreter.Strategy.Output;
    }

    /// <summary>
    /// Sub-classes must implement this function to define the logic for the function evaluation.
    /// </summary>
    public abstract ValueType Evaluate();

    /// <summary>
    /// Tries to retrieve parameter at index as a boolean parameter, if parameter is not a boolean returns or is not present the default is returned.
    /// </summary>
    public bool GetBoolParameter(int index, bool defaultValue)
    {
      bool result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type != ValueTypeType.Boolean)
          result = defaultValue;
        else
          result = value.BooleanValue;
      }
      else
        result = defaultValue;

      return result;
    }

    /// <summary>
    /// Tries to retrieve parameter at index as a boolean parameter, if parameter is not a boolean returns the given error message.
    /// </summary>
    public bool GetBoolParameter(int index, string error)
    {
      bool result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type != ValueTypeType.Boolean)
          throw new Exception(error);
        result = value.BooleanValue;
      }
      else
        throw new Exception(string.Format("Function \"{0}\" expects parameter at index {1} to be a boolean.", _name, index + 1));

      return result;
    }

    /// <summary>
    /// Tries to retrieve parameter at index as an integer parameter, if parameter is not an integer or not present returns the default value.
    /// </summary>
    public int GetIntegerParameter(int index, int defaultValue)
    {
      int result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type != ValueTypeType.Integer)
          result = defaultValue;
        else
          result = value.IntegerValue;
      }
      else
        result = defaultValue;

      return result;
    }

    /// <summary>
    /// Tries to retrieve parameter at index as an integer parameter, if parameter is not an integer returns the given error message.
    /// </summary>
    public int GetIntegerParameter(int index, string error)
    {
      int result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type != ValueTypeType.Integer)
          throw new Exception(error);
        result = value.IntegerValue;
      }
      else
        throw new Exception(string.Format("Function \"{0}\" expects parameter at index {1} to be an integer.", _name, index + 1));

      return result;
    }

    /// <summary>
    /// Tries to retrieve parameter at index as an integer parameter, if parameter is not an integer or it is not within the required range it
    /// returns the given error message for the integer error or range error, respectively.
    /// </summary>
    public int GetIntegerParameter(int index, int min, int max, string int_error, string range_error)
    {
      int result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type != ValueTypeType.Integer)
          throw new Exception(int_error);
        if (value.IntegerValue < min || value.IntegerValue > max)
          throw new Exception(range_error);
        result = value.IntegerValue;
      }
      else
        throw new Exception(string.Format("Function \"{0}\" expects parameter at index {1} to be an integer.", _name, index + 1));

      return result;
    }


    /// <summary>
    /// Tries to retrieve parameter at index as a double parameter. Optionally the method will allow an integer if the allowInteger flag is set otherwise returns the given default value.
    /// </summary>
    public double GetDoubleParameter(int index, double defaultValue, bool allowInteger = false)
    {
      double result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type == ValueTypeType.Double)
          result = value.DoubleValue;
        else if (allowInteger && value.Type == ValueTypeType.Integer)
          result = value.IntegerValue;
        else
          result = defaultValue;
      }
      else
        result = defaultValue;

      return result;
    }

    /// <summary>
    /// Tries to retrieve parameter at index as a double parameter. Optionally the method will allow an integer if the allowInteger flag is set otherwise returns the given error message.
    /// </summary>
    public double GetDoubleParameter(int index, string error, bool allowInteger = false)
    {
      double result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type == ValueTypeType.Double)
          result = value.DoubleValue;
        else if (allowInteger && value.Type == ValueTypeType.Integer)
          result = value.IntegerValue;
        else
          throw new Exception(error);
      }
      else
        throw new Exception(string.Format("Function \"{0}\" expects parameter at index {1} to be a double.", _name, index + 1));

      return result;
    }

    /// <summary>
    /// Tries to retrieve parameter at index as a string parameter, if parameter is not a string or not present returns the default value.
    /// NOTE: Dummy is not used, it is purely to use method overloading.
    /// </summary>
    public string GetStringParameter(int index, string defaultValue, bool dummy)
    {
      string result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type != ValueTypeType.String)
          result = defaultValue;
        else
          result = value.StringValue;
      }
      else
        result = defaultValue;

      return result;
    }

    /// <summary>
    /// Tries to retrieve parameter at index as a string parameter, if parameter is not a string returns the given error message.
    /// </summary>
    public string GetStringParameter(int index, string error)
    {
      string result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type != ValueTypeType.String)
          throw new Exception(error);
        result = value.StringValue;
      }
      else
        throw new Exception(string.Format("Function \"{0}\" expects parameter at index {1} to be a string.", _name, index + 1));

      return result;
    }

    /// <summary>
    /// Tries to retrieve parameter at index as an enumeration parameter, if parameter is not an enumeration returns the given error message.
    /// </summary>
    public string GetEnumerationParameter(int index, string error)
    {
      string result;

      if (_parameters.Count > index)
      {
        ValueType value = _parameters[index].Evaluate();
        if (value.Type != ValueTypeType.Enumeration)
          throw new Exception(error);
        result = value.EnumerationValue;
      }
      else
        throw new Exception(string.Format("Function \"{0}\" expects parameter at index {1} to be an enumerated value.", _name, index + 1));

      return result;
    }
  }

  /// <summary>
  /// Implements the proxy wrapper instance for a function supported by a function factory.
  /// </summary>
  public class FunctionProxy : Function
  {

    public FunctionProxy(FunctionType type, string name, FunctionFactory parent, List<Parameter> parameters) : base(type, name, parent, parameters) { }

    public override ValueType Evaluate()
    {
      return _parent.Evaluate(this);
    }
  }

  /// <summary>
  /// Factory used to create function instances as they are required in the processing of the script.
  /// </summary>
  public abstract class FunctionFactory : IEvaluatable
  {
    //Properties:
    public string Name { get { return _name; } }
    public List<string> Functions { get { return _functions; } }
    public Interpreter Interpreter { get { return _interpreter; } }

    //Attributes:
    protected string _name;
    protected List<string> _functions;
    protected Interpreter _interpreter;

    public FunctionFactory(string name, Interpreter interpreter)
    {
      _name = name;
      _interpreter = interpreter;
      _functions = new List<string>();
    }

    public abstract Function CreateFunction(string name, List<Parameter> parameters);

    /// <summary>
    /// Called in the Create method of the strategy, MultiCharts indicators must be created in the Create method so factories using these indicators
    /// must implement this method to create indicators.
    /// </summary>
    public virtual void Create() { }

    /// <summary>
    /// Called when strategy calculation is starting.
    /// </summary>
    public virtual void StartCalc() { }

    /// <summary>
    /// Called when strategy calculation is stopped.
    /// </summary>
    public virtual void StopCalc() { }

    /// <summary>
    /// Called when before evalating any of the functions are evaluated, the factory must implement code to update any indicators used by the
    /// factory functions. Default implementation does nothing and simply retuns a void result.
    /// </summary>
    public abstract ValueType Evaluate();

    /// <summary>
    /// Evaluation of a function supported by the factory must occur, factories can implement this method if they want to support functions as part of themselves
    /// and use the FunctionProxy class as a proxy for the function data.
    /// </summary>
    public abstract ValueType Evaluate(FunctionProxy wrapper);
  }

  /// <summary>
  /// Encapsulation of the entry/exit with it's set of triggers and actions.
  /// </summary>
  public class EntryExit : IEvaluatable
  {
    //Attributes:
    protected List<IEvaluatable> _triggers;
    protected List<IEvaluatable> _actions;

    public EntryExit()
    {
      _triggers = new List<IEvaluatable>();
      _actions = new List<IEvaluatable>();
    }

    /// <summary>
    /// Adds a trigger function to evaluate for the strategy.
    /// </summary>
    public void AddTrigger(IEvaluatable function)
    {
      _triggers.Add(function);
    }

    /// <summary>
    /// Removes the last trigger added and returns it, is used to retrieve the left hand side of comparisons or binary boolean operations.
    /// </summary>
    public IEvaluatable RemoveLastTrigger()
    {
      IEvaluatable result = null;

      if (_triggers.Count == 0)
        throw new Exception("Entry/exit does not contain triggers.");

      int lastIndex = _triggers.Count - 1;
      result = _triggers[lastIndex];
      _triggers.RemoveAt(lastIndex);

      return result;
    }

    /// <summary>
    /// Adds an action to perform when all triggers fire (evaluate to true). 
    /// </summary>
    public void AddAction(IEvaluatable function)
    {
      _actions.Add(function);
    }

    /// <summary>
    /// Evaluates the set of triggers and when all of them evaluate to true the set of actions would be taken.
    /// </summary>
    public virtual ValueType Evaluate()
    {
      bool fireActions = false;

      //per default strategy will not evaluate actions if no triggers are defined
      if (_triggers.Count > 0)
      {
        //evaluate the set of triggers until one is evaluated as false
        fireActions = true;

        foreach (IEvaluatable trigger in _triggers)
        {
          ValueType result = trigger.Evaluate();

          //evaluate the trigger value based on it's return type
          switch (result.Type)
          {
            case ValueTypeType.Void:
              //nothing to do, none return types are ignored
              break;
            case ValueTypeType.String:
              //nothing to do, string return types are ignored 
              break;
            case ValueTypeType.Integer:
              //positive integer values are interpreted as "true"
              fireActions = result.IntegerValue > 0;
              break;
            case ValueTypeType.Double:
              //positive double values are interpreted as "true"
              fireActions = result.DoubleValue > 0;
              break;
            case ValueTypeType.Boolean:
              fireActions = result.BooleanValue;
              break;
          }

          //terminate loop as soon as the actions do not have to be fired
          if (!fireActions)
            break;
        }
      }

      //evaluate the set of actions if the triggers fired
      if (fireActions)
      {
        foreach (IEvaluatable action in _actions)
          action.Evaluate();
      }

      //simply return void result
      return new ValueType();
    }
  }

  //Supported comparison operators.
  public enum ComparisonType
  {
    Equal,
    NotEqual,
    Less,
    Greater,
    LessEqual,
    GreaterEqual,
  }

  /// <summary>
  /// Constructs a function that evaluates a comparison in order to give a boolean result.
  /// </summary>
  public class Comparison : IEvaluatable
  {
    //Properties:
    public IEvaluatable Left {
      get { return _left; }
      set {
        if (value == null)
          throw new Exception("Left side evaluation of condition can not be null.");
        _left = value;
      }
    }

    public IEvaluatable Right {
      get { return _right; }
      set {
        if (value == null)
          throw new Exception("Right side evaluation of condition can not be null.");
        _right = value;
      }
    }

    public ComparisonType Type {
      get { return _type; }
      set { _type = value; }
    }

    //Attributes:
    protected IEvaluatable _left;
    protected ComparisonType _type;
    protected IEvaluatable _right;

    public Comparison()
    {
      //left and right terms must be set by interpreter
      _left = null;
      _right = null;
      _type = ComparisonType.Equal;
    }

    /// <summary>
    /// Evaluates the condition and returns the result.
    /// </summary>
    public virtual ValueType Evaluate()
    {
      bool result = false;

      //check that we have valid left/right terms set, this is more of an internal error since it implies the interpreter
      //did not assemble the comparison correctly
      if (_left == null)
        throw new Exception("Left term for comparison is not set.");
      if (_right == null)
        throw new Exception("Right term for comparison is not set.");

      //evaluate the left and right hand sides of the comparison
      ValueType leftValueType = _left.Evaluate();
      ValueType rightValueType = _right.Evaluate();

      //get the values to compare
      double leftValue = 0;
      double rightValue = 0;

      if (leftValueType.Type == ValueTypeType.Integer)
        leftValue = leftValueType.IntegerValue;
      else if (leftValueType.Type == ValueTypeType.Double)
        leftValue = leftValueType.DoubleValue;
      else
        throw new Exception("Left term of comparison does not evaluate to a number.");

      if (rightValueType.Type == ValueTypeType.Integer)
        rightValue = rightValueType.IntegerValue;
      else if (rightValueType.Type == ValueTypeType.Double)
        rightValue = rightValueType.DoubleValue;
      else
        throw new Exception("Right term of comparison does not evaluate to a number.");

      //perform the comparison
      switch (_type)
      {
        case ComparisonType.Equal:
          result = PublicFunctions.DoubleEquals(leftValue, rightValue);
          break;
        case ComparisonType.NotEqual:
          result = !PublicFunctions.DoubleEquals(leftValue, rightValue);
          break;
        case ComparisonType.Less:
          result = PublicFunctions.DoubleLess(leftValue, rightValue);
          break;
        case ComparisonType.Greater:
          result = PublicFunctions.DoubleGreater(leftValue, rightValue);
          break;
        case ComparisonType.LessEqual:
          result = PublicFunctions.DoubleLessEquals(leftValue, rightValue);
          break;
        case ComparisonType.GreaterEqual:
          result = PublicFunctions.DoubleGreaterEquals(leftValue, rightValue);
          break;
      }

      return new ValueType(result);
    }
  }

  /// <summary>
  /// Implements logical NOT operator on boolean term.
  /// </summary>
  public class NotLogicalOperator : IEvaluatable
  {
    //Properties:
    public IEvaluatable Term {
      get { return _term; }
      set {
        if (value == null)
          throw new Exception("NOT operator requires a term to evaluate.");
        _term = value;
      }
    }

    //Attributes:
    IEvaluatable _term;

    //Evaluates the set of terms and return the result of the boolean operation.
    public virtual ValueType Evaluate()
    {
      bool result = false;

      if (_term == null)
        throw new Exception("NOT operator called on NULL term.");

      ValueType value = _term.Evaluate();

      if (value.Type == ValueTypeType.Boolean)
      {
        result = !value.BooleanValue;
      }
      else
        throw new Exception("NOT operator called on non-boolean term.");

      return new ValueType(result);
    }
  }

  /// <summary>
  /// Logical operator used for the boolean operator.
  /// </summary>
  public enum BinaryLogicalOperatorType
  {
    And,
    Or,
  }

  /// <summary>
  /// Implements binary logical AND or OR operation.
  /// </summary>
  public class BinaryLogicalOperator : IEvaluatable
  {
    //Properties:
    public BinaryLogicalOperatorType Type {
      get { return _type; }
      set { _type = value; }
    }

    //Attributes:
    protected List<IEvaluatable> _terms;
    protected BinaryLogicalOperatorType _type;

    public BinaryLogicalOperator()
    {
      _terms = new List<IEvaluatable>();
    }

    /// <summary>
    /// Adds an evaluatable term to the boolean operation.
    /// </summary>
    public void AddTerm(IEvaluatable term)
    {
      _terms.Add(term);
    }

    //Evaluates the set of terms and return the result of the boolean operation.
    public virtual ValueType Evaluate()
    {
      bool result = false;        //if there's no terms in the boolean operator it returns false

      if (_terms.Count > 0)
        foreach (IEvaluatable e in _terms)
        {
          ValueType value = e.Evaluate();
          if (value.Type != ValueTypeType.Boolean)
            throw new Exception("Term in logical operator did not evaluate to a boolean.");
          result = value.BooleanValue;

          //short circut termination of term evaluation for And/Or
          if (result && _type == BinaryLogicalOperatorType.Or)
            break;
          else if (!result && _type == BinaryLogicalOperatorType.And)
            break;
        }

      return new ValueType(result);
    }
  }
  #endregion

  #region Core Language Functions
  /// <summary>
  /// Factory class to create run-time functions that facilitate some core functions required by the language.
  /// </summary>
  public class CoreFunctions : FunctionFactory
  {
    //Factory name to import
    public const string tkn_factory_name = "core";

    //Set of supported function tokens
    public const string tkn_import = "import";
    public const string tkn_is_flat = "is_flat";
    public const string tkn_is_long = "is_long";
    public const string tkn_is_short = "is_short";
    public const string tkn_profit_loss = "profit_loss";
    public const string tkn_profit_loss_percent = "profit_loss_percent";
    public const string tkn_bar_count = "bar_count";
    public const string tkn_last_bar_open = "last_bar_open";
    public const string tkn_last_bar_high = "last_bar_high";
    public const string tkn_last_bar_low = "last_bar_low";
    public const string tkn_last_bar_close = "last_bar_close";
    public const string tkn_bar_open = "bar_open";
    public const string tkn_bar_high = "bar_high";
    public const string tkn_bar_low = "bar_low";
    public const string tkn_bar_close = "bar_close";
    public const string tkn_highest_open = "highest_open";
    public const string tkn_lowest_open = "lowest_open";
    public const string tkn_highest_high = "highest_high";
    public const string tkn_lowest_high = "lowest_high";
    public const string tkn_highest_low = "highest_low";
    public const string tkn_lowest_low = "lowest_low";
    public const string tkn_highest_close = "highest_close";
    public const string tkn_lowest_close = "lowest_close";

    public CoreFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      //add the set of supported order functions
      _functions.Add(tkn_import);
      _functions.Add(tkn_is_flat);
      _functions.Add(tkn_is_long);
      _functions.Add(tkn_is_short);
      _functions.Add(tkn_profit_loss);
      _functions.Add(tkn_profit_loss_percent);
      _functions.Add(tkn_bar_count);
      _functions.Add(tkn_last_bar_open);
      _functions.Add(tkn_last_bar_high);
      _functions.Add(tkn_last_bar_low);
      _functions.Add(tkn_last_bar_close);
      _functions.Add(tkn_bar_open);
      _functions.Add(tkn_bar_high);
      _functions.Add(tkn_bar_low);
      _functions.Add(tkn_bar_close);
      _functions.Add(tkn_highest_open);
      _functions.Add(tkn_lowest_open);
      _functions.Add(tkn_highest_high);
      _functions.Add(tkn_lowest_high);
      _functions.Add(tkn_highest_low);
      _functions.Add(tkn_lowest_low);
      _functions.Add(tkn_highest_close);
      _functions.Add(tkn_lowest_close);
    }

    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        //supported factory functions
        case tkn_import:
          result = new FunctionProxy(FunctionType.Factory, name, this, parameters);
          break;

        //supported trigger functions
        case tkn_is_flat:
        case tkn_is_long:
        case tkn_is_short:
        case tkn_profit_loss:
        case tkn_profit_loss_percent:
          result = new FunctionProxy(FunctionType.Trigger, name, this, parameters);
          break;

        //supported trigger and action functions
        case tkn_bar_count:
        case tkn_last_bar_open:
        case tkn_last_bar_high:
        case tkn_last_bar_low:
        case tkn_last_bar_close:
        case tkn_bar_open:
        case tkn_bar_high:
        case tkn_bar_low:
        case tkn_bar_close:
        case tkn_highest_open:
        case tkn_lowest_open:
        case tkn_highest_high:
        case tkn_lowest_high:
        case tkn_highest_low:
        case tkn_lowest_low:
        case tkn_highest_close:
        case tkn_lowest_close:
          result = new FunctionProxy(FunctionType.Any, name, this, parameters);
          break;

        default:
          throw new ArgumentException("Core function factory does not support function " + name + ".");
      }

      return result;
    }

    /// <summary>
    /// Core functions do not require any specific evaluation for the factory.
    /// </summary>
    public override ValueType Evaluate()
    {
      return new ValueType();
    }

    /// <summary>
    /// Implement the different supported functions.
    /// </summary>
    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = null;
      int timeframe = 0;
      int bars_ago = 0;
      int lookback = 0;
      IInstrument data = null;

      switch (wrapper.Name)
      {
        case tkn_import:
          string import = wrapper.GetStringParameter(0, "Import statement must have a string parameter value.");
          _interpreter.Import(import);
          result = new ValueType();
          break;
        case tkn_is_flat:
          result = new ValueType(_interpreter.Strategy.PositionSide == 0);
          break;
        case tkn_is_long:
          result = new ValueType(_interpreter.Strategy.PositionSide > 0);
          break;
        case tkn_is_short:
          result = new ValueType(_interpreter.Strategy.PositionSide < 0);
          break;
        case tkn_profit_loss:
          result = new ValueType(_interpreter.Strategy.NetProfit);
          break;
        case tkn_profit_loss_percent:
          result = new ValueType(_interpreter.Strategy.PercentProfit);
          break;
        case tkn_bar_count:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.CurrentBar);
          break;
        case tkn_last_bar_open:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Open[0]);
          break;
        case tkn_last_bar_high:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.High[0]);
          break;
        case tkn_last_bar_low:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Low[0]);
          break;
        case tkn_last_bar_close:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Low[0]);
          break;
        case tkn_bar_open:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          bars_ago = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter as index of bars-ago to retrieve price value for.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Open[bars_ago]);
          break;
        case tkn_bar_high:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          bars_ago = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter as index of bars-ago to retrieve price value for.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.High[bars_ago]);
          break;
        case tkn_bar_low:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          bars_ago = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter as index of bars-ago to retrieve price value for.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Low[bars_ago]);
          break;
        case tkn_bar_close:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          bars_ago = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter as index of bars-ago to retrieve price value for.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Close[bars_ago]);
          break;
        case tkn_highest_open:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          lookback = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter for look back period.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Open.Highest(lookback));
          break;
        case tkn_lowest_open:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          lookback = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter for look back period.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Open.Lowest(lookback));
          break;
        case tkn_highest_high:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          lookback = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter for look back period.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.High.Highest(lookback));
          break;
        case tkn_lowest_high:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          lookback = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter for look back period.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.High.Lowest(lookback));
          break;
        case tkn_highest_low:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          lookback = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter for look back period.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Low.Highest(lookback));
          break;
        case tkn_lowest_low:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          lookback = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter for look back period.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Low.Lowest(lookback));
          break;
        case tkn_highest_close:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          lookback = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter for look back period.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Close.Highest(lookback));
          break;
        case tkn_lowest_close:
          timeframe = wrapper.GetIntegerParameter(0, String.Format("Function {0} requires an integer parameter to designate the timeframe.", wrapper.Name));
          lookback = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter for look back period.", wrapper.Name));
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since timeframe is zero indexed while BarsOfData is 1-indexed
          result = new ValueType(data.Close.Lowest(lookback));
          break;

        default:
          throw new ArgumentException("Core function factory does not support function " + wrapper.Name + ".");
      }

      return result;
    }

  }
  #endregion

  #region Order Functions
  /// <summary>
  /// Factory class to create run-time instances of the order functions.
  /// </summary>
  public class OrderFunctions : FunctionFactory
  {
    //Factory name to import
    public const string tkn_factory_name = "orders";

    //Set of supported function tokens
    public const string tkn_size_static = "order_size_static";
    public const string tkn_size_dynamic = "order_size_dynamic";
    public const string tkn_size_max = "order_size_max";
    public const string tkn_size_delta = "order_size_delta";
    public const string tkn_profit_delta = "order_profit_delta";
    public const string tkn_start_profit = "order_start_profit";
    public const string tkn_market_le = "market_le";
    public const string tkn_market_lx = "market_lx";
    public const string tkn_market_se = "market_se";
    public const string tkn_market_sx = "market_sx";
    public const string tkn_market_flat = "market_flat";
    public const string tkn_stop_le = "stop_le";
    public const string tkn_stop_lx = "stop_lx";
    public const string tkn_stop_se = "stop_se";
    public const string tkn_stop_sx = "stop_sx";
    public const string tkn_limit_le = "limit_le";
    public const string tkn_limit_lx = "limit_lx";
    public const string tkn_limit_se = "limit_se";
    public const string tkn_limit_sx = "limit_sx";
    public const string tkn_stop_limit_le = "stop_limit_le";
    public const string tkn_stop_limit_lx = "stop_limit_lx";
    public const string tkn_stop_limit_se = "stop_limit_se";
    public const string tkn_stop_limit_sx = "stop_limit_sx";
    public const string tkn_stop_percent = "stop_loss_percent";
    public const string tkn_stop_amount = "stop_loss_amount";
    public const string tkn_trailing_percent = "trailing_stop_percent";
    public const string tkn_trailing_amount = "trailing_stop_amount";
    public const string tkn_target_percent = "profit_target_percent";
    public const string tkn_target_amount = "profit_target_amount";
    public const string tkn_oco_breakout = "oco_breakout";
    public const string tkn_oco_fade = "oco_fade";
    public const string tkn_entry_price = "entry_price";
    public const string tkn_entry_bar_open = "entry_bar_open";
    public const string tkn_entry_bar_high = "entry_bar_high";
    public const string tkn_entry_bar_low = "entry_bar_low";
    public const string tkn_entry_bar_close = "entry_bar_close";

    public OrderFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      //add the set of supported order functions
      _functions.Add(tkn_size_static);
      _functions.Add(tkn_size_dynamic);
      _functions.Add(tkn_size_max);
      _functions.Add(tkn_size_delta);
      _functions.Add(tkn_profit_delta);
      _functions.Add(tkn_start_profit);
      _functions.Add(tkn_market_le);
      _functions.Add(tkn_market_lx);
      _functions.Add(tkn_market_se);
      _functions.Add(tkn_market_sx);
      _functions.Add(tkn_market_flat);
      _functions.Add(tkn_stop_le);
      _functions.Add(tkn_stop_lx);
      _functions.Add(tkn_stop_se);
      _functions.Add(tkn_stop_sx);
      _functions.Add(tkn_limit_le);
      _functions.Add(tkn_limit_lx);
      _functions.Add(tkn_limit_se);
      _functions.Add(tkn_limit_sx);
      _functions.Add(tkn_stop_limit_le);
      _functions.Add(tkn_stop_limit_lx);
      _functions.Add(tkn_stop_limit_se);
      _functions.Add(tkn_stop_limit_sx);
      _functions.Add(tkn_stop_percent);
      _functions.Add(tkn_stop_amount);
      _functions.Add(tkn_trailing_percent);
      _functions.Add(tkn_trailing_amount);
      _functions.Add(tkn_target_percent);
      _functions.Add(tkn_target_amount);
      _functions.Add(tkn_oco_breakout);
      _functions.Add(tkn_oco_fade);
      _functions.Add(tkn_entry_price);
      _functions.Add(tkn_entry_bar_open);
      _functions.Add(tkn_entry_bar_high);
      _functions.Add(tkn_entry_bar_low);
      _functions.Add(tkn_entry_bar_close);
    }

    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        //supported factory functions            
        case tkn_size_static:
        case tkn_size_dynamic:
        case tkn_size_max:
        case tkn_size_delta:
        case tkn_profit_delta:
        case tkn_start_profit:
          result = new FunctionProxy(FunctionType.Factory, name, this, parameters);
          break;

        //supported action functions
        case tkn_market_le:
        case tkn_market_lx:
        case tkn_market_se:
        case tkn_market_sx:
        case tkn_market_flat:
        case tkn_stop_le:
        case tkn_stop_lx:
        case tkn_stop_se:
        case tkn_stop_sx:
        case tkn_limit_le:
        case tkn_limit_lx:
        case tkn_limit_se:
        case tkn_limit_sx:
        case tkn_stop_limit_le:
        case tkn_stop_limit_lx:
        case tkn_stop_limit_se:
        case tkn_stop_limit_sx:
        case tkn_stop_percent:
        case tkn_stop_amount:
        case tkn_trailing_percent:
        case tkn_trailing_amount:
        case tkn_target_percent:
        case tkn_target_amount:
        case tkn_oco_breakout:
        case tkn_oco_fade:
          result = new FunctionProxy(FunctionType.Action, name, this, parameters);
          break;

        //supported trigger/action functions
        case tkn_entry_price:
        case tkn_entry_bar_open:
        case tkn_entry_bar_high:
        case tkn_entry_bar_low:
        case tkn_entry_bar_close:
          result = new FunctionProxy(FunctionType.Any, name, this, parameters);
          break;

        default:
          throw new ArgumentException("Order functions does not support order of function " + name + ".");
      }

      return result;
    }

    /// <summary>
    /// Order functions do not require any specific evaluation for the factory.
    /// </summary>
    public override ValueType Evaluate()
    {
      return new ValueType();
    }

    /// <summary>
    /// Implement the behavior for the supported functions.
    /// </summary>
    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = new ValueType();   //per default assume void return value
      double stopPrice = 0.0;
      double limitPrice = 0.0;
      double percent = 0.0;
      double amount = 0.0;
      double floorAmount = 0.0;
      double price = 0.0;
      TradeManager.OrderParams[] orders;
      TradeManager.ITradingProfile tradingProfile = null;
      TimeSpan duration;
      double oco_low = 0.0;
      double oco_high = 0.0;
      int oco_bars_valid = 1;

      switch (wrapper.Name)
      {
        case tkn_size_static:
          _interpreter.Strategy.SizingStrategy = SizingStrategy.Static;
          break;
        case tkn_size_dynamic:
          _interpreter.Strategy.SizingStrategy = SizingStrategy.Dynamic;
          break;
        case tkn_size_max:
          _interpreter.Strategy.SizeMax = wrapper.GetIntegerParameter(0, "Size max function requires an integer parameter.");
          break;
        case tkn_size_delta:
          _interpreter.Strategy.SizeDelta = wrapper.GetIntegerParameter(0, "Size delta function requires an integer parameter.");
          break;
        case tkn_profit_delta:
          _interpreter.Strategy.ProfitDelta = wrapper.GetDoubleParameter(0, "Profit delta function requires an amount parameter.", true);
          break;
        case tkn_start_profit:
          _interpreter.Strategy.StartProfit = wrapper.GetDoubleParameter(0, "Start profit function requires an amount parameter.", true);
          break;
        case tkn_market_le:
          _interpreter.Strategy.SendMarketLE();
          break;
        case tkn_market_lx:
          _interpreter.Strategy.SendMarketLX();
          break;
        case tkn_market_se:
          _interpreter.Strategy.SendMarketSE();
          break;
        case tkn_market_sx:
          _interpreter.Strategy.SendMarketSX();
          break;
        case tkn_market_flat:
          if (_interpreter.Strategy.PositionSide > 0)
            _interpreter.Strategy.SendMarketLX();
          else if (_interpreter.Strategy.PositionSide < 0)
            _interpreter.Strategy.SendMarketSX();
          break;
        case tkn_stop_le:
          stopPrice = wrapper.GetDoubleParameter(0, "Stop order \"" + wrapper.Name + "\" parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendStopLE(stopPrice);
          break;
        case tkn_stop_lx:
          stopPrice = wrapper.GetDoubleParameter(0, "Stop order \"" + wrapper.Name + "\" parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendStopLX(stopPrice);
          break;
        case tkn_stop_se:
          stopPrice = wrapper.GetDoubleParameter(0, "Stop order \"" + wrapper.Name + "\" parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendStopSE(stopPrice);
          break;
        case tkn_stop_sx:
          stopPrice = wrapper.GetDoubleParameter(0, "Stop order \"" + wrapper.Name + "\" parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendStopSX(stopPrice);
          break;
        case tkn_limit_le:
          limitPrice = wrapper.GetDoubleParameter(0, "Limit order \"" + wrapper.Name + "\" parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendLimitLE(limitPrice);
          break;
        case tkn_limit_lx:
          limitPrice = wrapper.GetDoubleParameter(0, "Limit order \"" + wrapper.Name + "\" parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendLimitLX(limitPrice);
          break;
        case tkn_limit_se:
          limitPrice = wrapper.GetDoubleParameter(0, "Limit order \"" + wrapper.Name + "\" parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendLimitSE(limitPrice);
          break;
        case tkn_limit_sx:
          limitPrice = wrapper.GetDoubleParameter(0, "Limit order \"" + wrapper.Name + "\" parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendLimitSX(limitPrice);
          break;
        case tkn_stop_limit_le:
          stopPrice = wrapper.GetDoubleParameter(0, "Stop-limit order \"" + wrapper.Name + "\" stop price parameter does not evaluate to an integer or double.", true);
          limitPrice = wrapper.GetDoubleParameter(1, "Stop-limit order \"" + wrapper.Name + "\" limit price parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendStopLimitLE(stopPrice, limitPrice);
          break;
        case tkn_stop_limit_lx:
          stopPrice = wrapper.GetDoubleParameter(0, "Stop-limit order \"" + wrapper.Name + "\" stop price parameter does not evaluate to an integer or double.", true);
          limitPrice = wrapper.GetDoubleParameter(1, "Stop-limit order \"" + wrapper.Name + "\" limit price parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendStopLimitLX(stopPrice, limitPrice);
          break;
        case tkn_stop_limit_se:
          stopPrice = wrapper.GetDoubleParameter(0, "Stop-limit order \"" + wrapper.Name + "\" stop price parameter does not evaluate to an integer or double.", true);
          limitPrice = wrapper.GetDoubleParameter(1, "Stop-limit order \"" + wrapper.Name + "\" limit price parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendStopLimitSE(stopPrice, limitPrice);
          break;
        case tkn_stop_limit_sx:
          stopPrice = wrapper.GetDoubleParameter(0, "Stop-limit order \"" + wrapper.Name + "\" stop price parameter does not evaluate to an integer or double.", true);
          limitPrice = wrapper.GetDoubleParameter(1, "Stop-limit order \"" + wrapper.Name + "\" limit price parameter does not evaluate to an integer or double.", true);
          _interpreter.Strategy.SendStopLimitSX(stopPrice, limitPrice);
          break;
        case tkn_stop_percent:
          if (_interpreter.Strategy.PositionSide != 0)
          {
            percent = wrapper.GetDoubleParameter(0, "Stop-loss percent parameter does not evaluate to an integer or double.", true);
            if (percent != 0)
              _interpreter.Strategy.StopLossPercent(percent);
          }
          break;
        case tkn_stop_amount:
          if (_interpreter.Strategy.PositionSide != 0)
          {
            amount = wrapper.GetDoubleParameter(0, "Stop-loss amount parameter does not evaluate to an integer or double.", true);
            if (amount != 0)
              _interpreter.Strategy.StopLossAmount(amount);
          }
          break;
        case tkn_trailing_percent:
          if (_interpreter.Strategy.PositionSide != 0)
          {
            percent = wrapper.GetDoubleParameter(0, "Trailing-stop percent parameter does not evaluate to an integer or double.", true);
            floorAmount = wrapper.GetDoubleParameter(1, 0, true);
            if (percent != 0)
              _interpreter.Strategy.TrailingStopPercent(percent, floorAmount);
          }
          break;
        case tkn_trailing_amount:
          if (_interpreter.Strategy.PositionSide != 0)
          {
            amount = wrapper.GetDoubleParameter(0, "Trailing-stop amount parameter does not evaluate to an integer or double.", true);
            if (amount != 0)
              _interpreter.Strategy.TrailingStopAmount(amount);
          }
          break;

        case tkn_target_percent:
          if (_interpreter.Strategy.PositionSide != 0)
          {
            percent = wrapper.GetDoubleParameter(0, "Profit-target percent parameter does not evaluate to an integer or double.", true);
            if (percent != 0)
              _interpreter.Strategy.ProfitTargetPercent(percent);
          }
          break;
        case tkn_target_amount:
          if (_interpreter.Strategy.PositionSide != 0)
          {
            amount = wrapper.GetDoubleParameter(0, "Profit-target amount parameter does not evaluate to an integer or double.", true);
            if (amount != 0)
              _interpreter.Strategy.ProfitTargetAmount(amount);
          }
          break;

        case tkn_oco_breakout:
          oco_low = wrapper.GetDoubleParameter(0, String.Format("Function {0} requires a low-price value for the breakout.", wrapper.Name), true);
          oco_high = wrapper.GetDoubleParameter(1, String.Format("Function {0} requires a high-price value for the breakout.", wrapper.Name), true);
          oco_bars_valid = wrapper.GetIntegerParameter(2, 1);   //per default the order only stays valid for the next bar

          orders = new OrderParams[2];

          orders[0].action = MTPA_OrdrActn.eMTPA_OA_Buy;
          orders[0].category = MTPA_OrdrCtgry.eMTPA_OC_Limit;
          orders[0].contracts = _interpreter.Strategy.PositionSize;
          orders[0].limit_price = oco_high;
          orders[0].tif = MTPA_OrdrTimeInForce.eMTPA_TIF_GTD;

          duration = _interpreter.Strategy.Bars.Info.Resolution.Duration();
          duration = TimeSpan.FromTicks(duration.Ticks * oco_bars_valid);
          orders[0].gtd = _interpreter.Strategy.Bars.Time[0] + duration;

          orders[1].action = MTPA_OrdrActn.eMTPA_OA_Sell;
          orders[1].category = MTPA_OrdrCtgry.eMTPA_OC_Stop;
          orders[1].contracts = orders[0].contracts;
          orders[1].stop_price = oco_low;
          orders[1].tif = orders[0].tif;
          orders[1].gtd = orders[0].gtd;

          for (int i = 0; i < _interpreter.Strategy.TradeManager.TradingProfiles.Length; i++)
          {
            if (_interpreter.Strategy.TradeManager.TradingProfiles[i].Name == _interpreter.Strategy.Profile)
            {
              tradingProfile = _interpreter.Strategy.TradeManager.TradingProfiles[i];
              break;
            }
          }

          //raise exception if we could not determine the trading profile from the data feed
          if (tradingProfile == null)
            throw new Exception("Unable to determing correct trading profile for OCO-breakout order.");

          System.Diagnostics.Debug.Assert(false, "Backtesting does not support placement of OCO-order types yet.");

          //tradingProfile.CurrentAccount = tradingProfile.Accounts[0];
          //tradingProfile.CurrentSymbol = new MCSymbolInfo(_interpreter.Strategy.Bars.Info.ASymbolInfo2, _interpreter.Strategy.Bars.Info.DataFeed);
          //tradingProfile.PlaceOCOOrders(MTPA_OCOGroupType.eMTPA_OCOGT_CancellOthers, orders);

          break;

        case tkn_oco_fade:
          oco_low = wrapper.GetDoubleParameter(0, String.Format("Function {0} requires a low-price value for the fade.", wrapper.Name), true);
          oco_high = wrapper.GetDoubleParameter(1, String.Format("Function {0} requires a high-price value for the fade.", wrapper.Name), true);
          oco_bars_valid = wrapper.GetIntegerParameter(2, 1);   //per default the order only stays valid for the next bar

          orders = new OrderParams[2];

          orders[0].action = MTPA_OrdrActn.eMTPA_OA_Buy;
          orders[0].category = MTPA_OrdrCtgry.eMTPA_OC_Stop;
          orders[0].contracts = _interpreter.Strategy.PositionSize;
          orders[0].stop_price = oco_low;
          orders[0].tif = MTPA_OrdrTimeInForce.eMTPA_TIF_GTD;

          duration = _interpreter.Strategy.Bars.Info.Resolution.Duration();
          duration = TimeSpan.FromTicks(duration.Ticks * oco_bars_valid);
          orders[0].gtd = _interpreter.Strategy.Bars.Time[0] + duration;

          orders[1].action = MTPA_OrdrActn.eMTPA_OA_Sell;
          orders[1].category = MTPA_OrdrCtgry.eMTPA_OC_Limit;
          orders[1].contracts = orders[0].contracts;
          orders[1].stop_price = oco_high;
          orders[1].tif = orders[0].tif;
          orders[1].gtd = orders[0].gtd;

          for (int i = 0; i < _interpreter.Strategy.TradeManager.TradingProfiles.Length; i++)
          {
            if (_interpreter.Strategy.TradeManager.TradingProfiles[i].Name == _interpreter.Strategy.Profile)
            {
              tradingProfile = _interpreter.Strategy.TradeManager.TradingProfiles[i];
              break;
            }
          }

          //raise exception if we could not determine the trading profile from the data feed
          if (tradingProfile == null)
            throw new Exception("Unable to determing correct trading profile for OCO-breakout order.");

          System.Diagnostics.Debug.Assert(false, "Backtesting does not support placement of OCO-order types yet.");

          //tradingProfile.CurrentAccount = tradingProfile.Accounts[0];
          //tradingProfile.CurrentSymbol = new MCSymbolInfo(_interpreter.Strategy.Bars.Info.ASymbolInfo2, _interpreter.Strategy.Bars.Info.DataFeed);
          //tradingProfile.PlaceOCOOrders(MTPA_OCOGroupType.eMTPA_OCOGT_CancellOthers, orders);

          break;

        case tkn_entry_price:
          if (_interpreter.Strategy.PositionSide != 0)
            price = _interpreter.Strategy.EntryPrice(0);
          result = new ValueType(price);
          break;
        case tkn_entry_bar_open:
          if (_interpreter.Strategy.PositionSide != 0)
            price = _interpreter.Strategy.Bars.Open[_interpreter.Strategy.BarsSinceEntry()];
          result = new ValueType(price);
          break;
        case tkn_entry_bar_high:
          if (_interpreter.Strategy.PositionSide != 0)
            price = _interpreter.Strategy.Bars.High[_interpreter.Strategy.BarsSinceEntry()];
          result = new ValueType(price);
          break;
        case tkn_entry_bar_low:
          if (_interpreter.Strategy.PositionSide != 0)
            price = _interpreter.Strategy.Bars.Low[_interpreter.Strategy.BarsSinceEntry()];
          result = new ValueType(price);
          break;
        case tkn_entry_bar_close:
          if (_interpreter.Strategy.PositionSide != 0)
            price = _interpreter.Strategy.Bars.Close[_interpreter.Strategy.BarsSinceEntry()];
          result = new ValueType(price);
          break;
        default:
          throw new ArgumentException("Order factory does not support order of type " + wrapper.Name + ".");
      }

      //order functions do not return anything
      return result;
    }
  }

  #endregion

  #region Volume Functions
  public class VolumeFunctions : FunctionFactory
  {
    //general constants
    public const int DEFAULT_LENGTH = 10;
    public const double DEFAULT_LEVEL1 = 0.5;
    public const double DEFAULT_LEVEL2 = 1.0;
    public const double DEFAULT_LEVEL3 = 1.5;

    //general types
    protected class VolumeSettings
    {
      public int length;
      public double level1;
      public double level2;
      public double level3;
    }

    //Factory name to import
    public const string tkn_factory_name = "volume";

    //Set of supported function tokens
    public const string tkn_volume_length = "volume_length";
    public const string tkn_volume_level1 = "volume_level1";
    public const string tkn_volume_level2 = "volume_level2";
    public const string tkn_volume_level3 = "volume_level3";
    public const string tkn_volume = "volume";
    public const string tkn_volume_avg = "volume_avg";
    public const string tkn_volume_below_avg = "volume_below_avg";
    public const string tkn_volume_low = "volume_low";
    public const string tkn_volume_ultra_low = "volume_ultra_low";
    public const string tkn_volume_above_avg = "volume_above_avg";
    public const string tkn_volume_high = "volume_high";
    public const string tkn_volume_ultra_high = "volume_ultra_high";
    public const string tkn_volume_rising = "volume_rising";
    public const string tkn_volume_dropping = "volume_dropping";

    //Attributes:
    protected List<VolumeSettings> _volumeSettings;     //Volume settings for the different layers.
    protected List<_VolumeWithSD> _volumeWithSD;         //List of volume indicators for the defined timeframes.

    public VolumeFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      //add the set of supported order functions
      _functions.Add(tkn_volume_length);
      _functions.Add(tkn_volume_level1);
      _functions.Add(tkn_volume_level2);
      _functions.Add(tkn_volume_level3);
      _functions.Add(tkn_volume);
      _functions.Add(tkn_volume_avg);
      _functions.Add(tkn_volume_below_avg);
      _functions.Add(tkn_volume_low);
      _functions.Add(tkn_volume_ultra_low);
      _functions.Add(tkn_volume_above_avg);
      _functions.Add(tkn_volume_high);
      _functions.Add(tkn_volume_ultra_high);
      _functions.Add(tkn_volume_rising);
      _functions.Add(tkn_volume_dropping);
    }

    //Sets up the structures with the default volume settings to use when factory functions are called.
    public override void Create()
    {
      base.Create();

      //create volume settings for factory functions
      _volumeSettings = new List<VolumeSettings>(Interpreter.MAX_DATA_STREAM);

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        VolumeSettings settings = new VolumeSettings();
        settings.length = DEFAULT_LENGTH;
        settings.level1 = DEFAULT_LEVEL1;
        settings.level2 = DEFAULT_LEVEL2;
        settings.level3 = DEFAULT_LEVEL3;
        _volumeSettings.Add(settings);
      }

      //create the set of required volume indicators for the number of time frames required by the calling script
      _volumeWithSD = new List<_VolumeWithSD>(Interpreter.MAX_DATA_STREAM);

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        _VolumeWithSD volumeWithSD = new _VolumeWithSD(_interpreter.Strategy);
        _volumeWithSD.Add(volumeWithSD);
      }
    }

    /// <summary>
    /// Initializes the set of required volume inicators for the defined timeframes of the signal object.
    /// </summary>
    public override void StartCalc()
    {
      base.StartCalc();

      //set the parameters for the volume indicators
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        IInstrument dataFeed = null;

        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          dataFeed = _interpreter.Strategy.BarsOfData(i + 1);
        else
          dataFeed = _interpreter.Strategy.Bars;

        _volumeWithSD[i].Length = _volumeSettings[i].length;
        _volumeWithSD[i].Level1 = _volumeSettings[i].level1;
        _volumeWithSD[i].Level2 = _volumeSettings[i].level2;
        _volumeWithSD[i].Level3 = _volumeSettings[i].level3;
        _volumeWithSD[i].Instrument = dataFeed;
      }
    }

    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        case tkn_volume_length:
        case tkn_volume_level1:
        case tkn_volume_level2:
        case tkn_volume_level3:
          result = new FunctionProxy(FunctionType.Factory, name, this, parameters);
          break;

        case tkn_volume:
        case tkn_volume_avg:
        case tkn_volume_below_avg:
        case tkn_volume_low:
        case tkn_volume_ultra_low:
        case tkn_volume_above_avg:
        case tkn_volume_high:
        case tkn_volume_ultra_high:
        case tkn_volume_rising:
        case tkn_volume_dropping:
          result = new FunctionProxy(FunctionType.Trigger, name, this, parameters);
          break;

        default:
          throw new ArgumentException("Volume functions does not support order of function " + name + ".");
      }

      return result;
    }

    /// <summary>
    /// Volume functions do not require any specific evaluation for the factory.
    /// </summary>
    public override ValueType Evaluate()
    {
      return new ValueType();
    }

    /// <summary>
    /// Implement the behavior for the supported functions.
    /// </summary>
    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = new ValueType();

      //get timeframe for which function must be evaluated
      int timeframe = wrapper.GetIntegerParameter(0, 0);
      int bars_ago = 0;
      int lookback;
      IInstrument data;

      switch (wrapper.Name)
      {
        case tkn_volume_length:
          _volumeSettings[timeframe].length = wrapper.GetIntegerParameter(1, String.Format("Function {0} requires an integer parameter with the timeframe to designate the length of the average calculation.", tkn_volume_length));
          break;
        case tkn_volume_level1:
          _volumeSettings[timeframe].level1 = wrapper.GetDoubleParameter(1, String.Format("Function {0} requires a double parameter with the timeframe to designate the level factor for the standard deviation for level 1.", tkn_volume_level1));
          break;
        case tkn_volume_level2:
          _volumeSettings[timeframe].level2 = wrapper.GetDoubleParameter(1, String.Format("Function {0} requires a double parameter with the timeframe to designate the level factor for the standard deviation for level 2.", tkn_volume_level2));
          break;
        case tkn_volume_level3:
          _volumeSettings[timeframe].level3 = wrapper.GetDoubleParameter(1, String.Format("Function {0} requires a double parameter with the timeframe to designate the level factor for the standard deviation for level 3.", tkn_volume_level3));
          break;
        case tkn_volume:
          result = new ValueType(_volumeWithSD[timeframe].Instrument.Volume.Value);
          break;
        case tkn_volume_avg:
          result = new ValueType(_volumeWithSD[timeframe].Value);
          break;
        case tkn_volume_below_avg:
          //NOTE: Below average volume includes low and ultra-low volume.
          bars_ago = wrapper.GetIntegerParameter(1, 0);
          result = new ValueType(_volumeWithSD[timeframe].Instrument.Volume[bars_ago] < _volumeWithSD[timeframe][bars_ago]);
          break;
        case tkn_volume_low:
          //NOTE: Low volume also includes ultra-low volume.
          bars_ago = wrapper.GetIntegerParameter(1, 0);
          result = new ValueType(_volumeWithSD[timeframe].Instrument.Volume[bars_ago] < _volumeWithSD[timeframe].AvgMinusLevel1[bars_ago]);
          break;
        case tkn_volume_ultra_low:
          bars_ago = wrapper.GetIntegerParameter(1, 0);
          result = new ValueType(_volumeWithSD[timeframe].Instrument.Volume[bars_ago] < _volumeWithSD[timeframe].AvgMinusLevel2[bars_ago]);
          break;
        case tkn_volume_above_avg:
          //NOTE: Above average volume includes high and ultra-high volume.
          bars_ago = wrapper.GetIntegerParameter(1, 0);
          result = new ValueType(_volumeWithSD[timeframe].Instrument.Volume[bars_ago] > _volumeWithSD[timeframe][bars_ago]);
          break;
        case tkn_volume_high:
          //NOTE: High volume also includes ultra-high volume.
          bars_ago = wrapper.GetIntegerParameter(1, 0);
          result = new ValueType(_volumeWithSD[timeframe].Instrument.Volume[bars_ago] > _volumeWithSD[timeframe].AvgPlusLevel1[bars_ago]);
          break;
        case tkn_volume_ultra_high:
          bars_ago = wrapper.GetIntegerParameter(1, 0);
          result = new ValueType(_volumeWithSD[timeframe].Instrument.Volume[bars_ago] > _volumeWithSD[timeframe].AvgPlusLevel2[bars_ago]);
          break;
        case tkn_volume_rising:
          lookback = wrapper.GetIntegerParameter(1, 3);
          bool raising = false;

          //ensure we have enough volume information to evaluate the function
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since function is zero indexed while BarsOfData function is 1-indexed
          if (data.CurrentBar > lookback)
          {
            raising = true;       //per default assume that volume is raising and do a counter proof of invariant

            for (int i = 0; i < lookback; i++)
              if (_volumeWithSD[timeframe].Instrument.Volume[i] < _volumeWithSD[timeframe].Instrument.Volume[i + 1])
              {
                //volume is not strictly raising accross the lookback period, return false 
                raising = false;
                break;
              }
          }

          result = new ValueType(raising);
          break;
        case tkn_volume_dropping:
          lookback = wrapper.GetIntegerParameter(1, 3);
          bool dropping = false;

          //ensure we have enough volume information to evaluate function
          data = _interpreter.Strategy.BarsOfData(timeframe + 1);   //+1 since function is zero indexed while BarsOfData function is 1-indexed
          if (data.CurrentBar > lookback)
          {
            dropping = true;       //per default assume that volume is dropping and do a counter proof of invariant

            for (int i = 0; i < lookback; i++)
              if (_volumeWithSD[timeframe].Instrument.Volume[i] > _volumeWithSD[timeframe].Instrument.Volume[i + 1])
              {
                //volime is not strictly dropping accross the lookback period, return false
                dropping = false;
                break;
              }
          }

          result = new ValueType(dropping);
          break;
      }

      return result;
    }
  }
  #endregion

  #region Ichimoku Functions
  /// <summary>
  /// Factory to extend strategy scripting with Ichimoku capabilities.
  /// </summary>
  public class IchimokuFunctions : FunctionFactory
  {
    //Factory name to import
    public const string tkn_factory_name = "ichimoku";

    //Set of supported function tokens
    public const string tkn_ts = "ichi_ts";                 //Tenkan Sen
    public const string tkn_ks = "ichi_ks";                 //Kijun Sen
    public const string tkn_cs = "ichi_cs";                 //Chikou Span
    public const string tkn_ssa = "ichi_ssa";               //Senkou Span A
    public const string tkn_ssb = "ichi_ssb";               //Senkou Span B
    public const string tkn_curr_ssa = "ichi_curr_ssa";     //Current Senkou Span A
    public const string tkn_curr_ssb = "ichi_curr_ssb";     //Current Senkou Span B
    public const string tkn_cs_ssa = "ichi_cs_ssa";         //Chikou Span Senkou Span A
    public const string tkn_cs_ssb = "ichi_cs_ssb";         //Chikou Span Senkou Span B		
    public const string tkn_pr_gt_km = "ichi_pr_gt_km";     //Price Above Kumo
    public const string tkn_pr_lt_km = "ichi_pr_lt_km";     //Price Below Kumo
    public const string tkn_pr_gt_ts = "ichi_pr_gt_ts";     //Price Above Tenkan Sen
    public const string tkn_pr_lt_ts = "ichi_pr_lt_ts";     //Price Below Tenkan Sen
    public const string tkn_pr_gt_ks = "ichi_pr_gt_ks";     //Price Above Kijin Sen
    public const string tkn_pr_lt_ks = "ichi_pr_lt_ks";     //Price Below Kijin Sen
    public const string tkn_pr_cl_ts = "ichi_pr_cl_ts";     //Price Close to Tenkan Sen
    public const string tkn_pr_cl_ks = "ichi_pr_cl_ks";     //Price Close to Kijin Sen
    public const string tkn_ts_gt_ks = "ichi_ts_gt_ks";     //Tenkan Sen Above Kijin Sen
    public const string tkn_ts_lt_ks = "ichi_ts_lt_ks";     //Tenkan Sen Below Kijin Sen
    public const string tkn_ts_cl_ks = "ichi_ts_cl_ks";     //Tenkan Sen Close to Kijin Sen
    public const string tkn_ck_gt_pr = "ichi_ck_gt_pr";     //Chikou Above Price
    public const string tkn_ck_lt_pr = "ichi_ck_lt_pr";     //Chikou Below Price
    public const string tkn_ck_bl_sp = "ichi_ck_bl_sp";     //Chikou Bullish Open Space
    public const string tkn_ck_br_sp = "ichi_ck_br_sp";     //Chikou Bearish Open Space
    public const string tkn_ts_mt_pt = "ichi_ts_mt_pt";     //Tenkan Sen Momentum Positive
    public const string tkn_ts_mt_ft = "ichi_ts_mt_ft";     //Tenkan Sen Momentum Flat
    public const string tkn_ts_mt_nt = "ichi_ts_mt_nt";     //Tenkan Sen Momentum Negative
    public const string tkn_ks_mt_pt = "ichi_ks_mt_pt";     //Kijin Sen Momentum Positive
    public const string tkn_ks_mt_ft = "ichi_ks_mt_ft";     //Kijin Sen Momentum Flat
    public const string tkn_ks_mt_nt = "ichi_ks_mt_nt";     //Kijin Sen Momentum Negative
    public const string tkn_ck_mt_pt = "ichi_ck_mt_pt";     //Chikou Momentum Positive
    public const string tkn_ck_mt_ft = "ichi_ck_mt_ft";     //Chikou Momentum Flat
    public const string tkn_ck_mt_nt = "ichi_ck_mt_nt";     //Chikou Momentum Negative
    public const string tkn_csa_mt_pt = "ichi_csa_mt_pt";   //Current Senkou A Momentum Positive
    public const string tkn_csa_mt_ft = "ichi_csa_mt_ft";   //Current Senkou A Momentum Flat
    public const string tkn_csa_mt_nt = "ichi_csa_mt_nt";   //Current Senkou A Momentum Negative
    public const string tkn_fsa_mt_pt = "ichi_fsa_mt_pt";   //Future Senkou A Momentum Positive
    public const string tkn_fsa_mt_ft = "ichi_fsa_mt_ft";   //Future Senkou A Momentum Flat
    public const string tkn_fsa_mt_nt = "ichi_fsa_mt_nt";   //Future Senkou A Momentum Negative
    public const string tkn_csb_mt_pt = "ichi_csb_mt_pt";   //Current Senkou B Momentum Positive
    public const string tkn_csb_mt_ft = "ichi_csb_mt_ft";   //Current Senkou B Momentum Flat
    public const string tkn_csb_mt_nt = "ichi_csb_mt_nt";   //Current Senkou B Momentum Negative
    public const string tkn_fsb_mt_pt = "ichi_fsb_mt_pt";   //Future Senkou B Momentum Positive
    public const string tkn_fsb_mt_ft = "ichi_fsb_mt_ft";   //Future Senkou B Momentum Flat
    public const string tkn_fsb_mt_nt = "ichi_fsb_mt_nt";   //Future Senkou B Momentum Negative
    public const string tkn_ckkm_bl = "ichi_ckkm_bl";       //Chikou Kumo Bullish
    public const string tkn_ckkm_br = "ichi_ckkm_br";       //Chikou Kumo Bearish
    public const string tkn_ckm_bl = "ichi_ckm_bl";         //Current Kumo Bullish
    public const string tkn_ckm_br = "ichi_ckm_br";         //Current Kumo Bearish
    public const string tkn_fkm_bl = "ichi_fkm_bl";         //Future Kumo Bullish
    public const string tkn_fkm_br = "ichi_fkm_br";         //Future Kumo Bearish
    public const string tkn_ckkm_tk = "ichi_ckkm_tk";       //Chikou Kumo Thick
    public const string tkn_ckkm_tn = "ichi_ckkm_tn";       //Chikou Kumo Thin
    public const string tkn_ckm_tk = "ichi_ckm_tk";         //Current Kumo Thick
    public const string tkn_ckm_tn = "ichi_ckm_tn";         //Current Kumo Thin
    public const string tkn_fkm_tk = "ichi_fkm_tk";         //Future Kumo Thick
    public const string tkn_fkm_tn = "ichi_fkm_tn";         //Future Kumo Thin
    public const string tkn_pr_in_km = "ichi_pr_in_km";     //Price In Kumo
    public const string tkn_pr_in_kmsh = "ichi_pr_in_kmsh"; //Price In Kumo Shadow
    public const string tkn_ts_gt_km = "ichi_ts_gt_km";     //Tenkan Sen Above Kumo
    public const string tkn_ts_lt_km = "ichi_ts_lt_km";     //Tenkan Sen Below Kumo
    public const string tkn_ts_in_km = "ichi_ts_in_km";     //Tenkan Sen In Kumo
    public const string tkn_ks_gt_km = "ichi_ks_gt_km";     //Kijin Sen Above Kumo
    public const string tkn_ks_lt_km = "ichi_ks_lt_km";     //Kijin Sen Below Kumo
    public const string tkn_ks_in_km = "ichi_ks_in_km";     //Kijin Sen In Kumo
    public const string tkn_ck_gt_km = "ichi_ck_gt_km";     //Chikou Above Kumo
    public const string tkn_ck_lt_km = "ichi_ck_lt_km";     //Chikou Below Kumo
    public const string tkn_ck_in_km = "ichi_ck_in_km";     //Chikou In Kumo

    //Constants:
    public int IchimokuBarsToKeep = 5000;               //Number of bars to keep for the Ichimoku analysis		

    //Properties:
    public List<IchimokuAnalysis> Ichimoku { get { return _ichimoku; } }

    //Attributes:
    protected List<IchimokuAnalysis> _ichimoku;         //List of Ichimoku indicators associated with the defined set of timeframes.

    public IchimokuFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      //add the set of functions supported by the function factory
      _functions.Add(tkn_ts);
      _functions.Add(tkn_ks);
      _functions.Add(tkn_cs);
      _functions.Add(tkn_ssa);
      _functions.Add(tkn_ssb);
      _functions.Add(tkn_curr_ssa);
      _functions.Add(tkn_curr_ssb);
      _functions.Add(tkn_cs_ssa);
      _functions.Add(tkn_cs_ssb);
      _functions.Add(tkn_pr_gt_km);
      _functions.Add(tkn_pr_lt_km);
      _functions.Add(tkn_pr_gt_ts);
      _functions.Add(tkn_pr_lt_ts);
      _functions.Add(tkn_pr_gt_ks);
      _functions.Add(tkn_pr_lt_ks);
      _functions.Add(tkn_pr_cl_ts);
      _functions.Add(tkn_pr_cl_ks);
      _functions.Add(tkn_ts_gt_ks);
      _functions.Add(tkn_ts_lt_ks);
      _functions.Add(tkn_ts_cl_ks);
      _functions.Add(tkn_ck_gt_pr);
      _functions.Add(tkn_ck_lt_pr);
      _functions.Add(tkn_ck_bl_sp);
      _functions.Add(tkn_ck_br_sp);
      _functions.Add(tkn_ts_mt_pt);
      _functions.Add(tkn_ts_mt_ft);
      _functions.Add(tkn_ts_mt_nt);
      _functions.Add(tkn_ks_mt_pt);
      _functions.Add(tkn_ks_mt_ft);
      _functions.Add(tkn_ks_mt_nt);
      _functions.Add(tkn_ck_mt_pt);
      _functions.Add(tkn_ck_mt_ft);
      _functions.Add(tkn_ck_mt_nt);
      _functions.Add(tkn_csa_mt_pt);
      _functions.Add(tkn_csa_mt_ft);
      _functions.Add(tkn_csa_mt_nt);
      _functions.Add(tkn_fsa_mt_pt);
      _functions.Add(tkn_fsa_mt_ft);
      _functions.Add(tkn_fsa_mt_nt);
      _functions.Add(tkn_csb_mt_pt);
      _functions.Add(tkn_csb_mt_ft);
      _functions.Add(tkn_csb_mt_nt);
      _functions.Add(tkn_fsb_mt_pt);
      _functions.Add(tkn_fsb_mt_ft);
      _functions.Add(tkn_fsb_mt_nt);
      _functions.Add(tkn_ckkm_bl);
      _functions.Add(tkn_ckkm_br);
      _functions.Add(tkn_ckm_bl);
      _functions.Add(tkn_ckm_br);
      _functions.Add(tkn_fkm_bl);
      _functions.Add(tkn_fkm_br);
      _functions.Add(tkn_ckkm_tk);
      _functions.Add(tkn_ckkm_tn);
      _functions.Add(tkn_ckm_tk);
      _functions.Add(tkn_ckm_tn);
      _functions.Add(tkn_fkm_tk);
      _functions.Add(tkn_fkm_tn);
      _functions.Add(tkn_pr_in_km);
      _functions.Add(tkn_pr_in_kmsh);
      _functions.Add(tkn_ts_gt_km);
      _functions.Add(tkn_ts_lt_km);
      _functions.Add(tkn_ts_in_km);
      _functions.Add(tkn_ks_gt_km);
      _functions.Add(tkn_ks_lt_km);
      _functions.Add(tkn_ks_in_km);
      _functions.Add(tkn_ck_gt_km);
      _functions.Add(tkn_ck_lt_km);
      _functions.Add(tkn_ck_in_km);
    }

    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        //supported factory functions

        //supported trigger functions
        case tkn_ts:
        case tkn_ks:
        case tkn_cs:
        case tkn_ssa:
        case tkn_ssb:
        case tkn_curr_ssa:
        case tkn_curr_ssb:
        case tkn_cs_ssa:
        case tkn_cs_ssb:
        case tkn_pr_gt_km:
        case tkn_pr_lt_km:
        case tkn_pr_gt_ts:
        case tkn_pr_lt_ts:
        case tkn_pr_gt_ks:
        case tkn_pr_lt_ks:
        case tkn_pr_cl_ts:
        case tkn_pr_cl_ks:
        case tkn_ts_gt_ks:
        case tkn_ts_lt_ks:
        case tkn_ts_cl_ks:
        case tkn_ck_gt_pr:
        case tkn_ck_lt_pr:
        case tkn_ck_bl_sp:
        case tkn_ck_br_sp:
        case tkn_ts_mt_pt:
        case tkn_ts_mt_ft:
        case tkn_ts_mt_nt:
        case tkn_ks_mt_pt:
        case tkn_ks_mt_ft:
        case tkn_ks_mt_nt:
        case tkn_ck_mt_pt:
        case tkn_ck_mt_ft:
        case tkn_ck_mt_nt:
        case tkn_csa_mt_pt:
        case tkn_csa_mt_ft:
        case tkn_csa_mt_nt:
        case tkn_fsa_mt_pt:
        case tkn_fsa_mt_ft:
        case tkn_fsa_mt_nt:
        case tkn_csb_mt_pt:
        case tkn_csb_mt_ft:
        case tkn_csb_mt_nt:
        case tkn_fsb_mt_pt:
        case tkn_fsb_mt_ft:
        case tkn_fsb_mt_nt:
        case tkn_ckkm_bl:
        case tkn_ckkm_br:
        case tkn_ckm_bl:
        case tkn_ckm_br:
        case tkn_fkm_bl:
        case tkn_fkm_br:
        case tkn_ckkm_tk:
        case tkn_ckkm_tn:
        case tkn_ckm_tk:
        case tkn_ckm_tn:
        case tkn_fkm_tk:
        case tkn_fkm_tn:
        case tkn_pr_in_km:
        case tkn_pr_in_kmsh:
        case tkn_ts_gt_km:
        case tkn_ts_lt_km:
        case tkn_ts_in_km:
        case tkn_ks_gt_km:
        case tkn_ks_lt_km:
        case tkn_ks_in_km:
        case tkn_ck_gt_km:
        case tkn_ck_lt_km:
        case tkn_ck_in_km:
          result = new FunctionProxy(FunctionType.Trigger, name, this, parameters);
          break;
        default:
          throw new ArgumentException("Ichimoku factory does not support function " + name + ".");
      }

      return result;
    }

    /// <summary>
    /// Initializes the set of required Ichimoku inicators for the defined timeframes of the signal object.
    /// </summary>
    public override void StartCalc()
    {
      base.StartCalc();

      //create the set of required ichimoku Indicators for the number of time frames required by the calling script
      _ichimoku = new List<IchimokuAnalysis>(Interpreter.MAX_DATA_STREAM);
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        IInstrument dataFeed = null;

        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          dataFeed = _interpreter.Strategy.BarsOfData(i + 1);
        else
          dataFeed = _interpreter.Strategy.Bars;

        IchimokuAnalysis ichimokuTimeFrame = new IchimokuAnalysis(IchimokuBarsToKeep, dataFeed.Info.Resolution.Duration());
        _ichimoku.Add(ichimokuTimeFrame);
      }
    }

    /// <summary>
    /// Updates the set of Ichimoku indicators associated with the different timeframes used by the supported functions.
    /// </summary>
    public override ValueType Evaluate()
    {
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        IInstrument dataFeed = null;

        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          dataFeed = _interpreter.Strategy.BarsOfData(i + 1);
        else
          dataFeed = _interpreter.Strategy.Bars;

        _ichimoku[i].Analyse(dataFeed.Time[0], dataFeed.Open[0], dataFeed.High[0], dataFeed.Low[0], dataFeed.Close[0]);
      }

      return new ValueType();
    }


    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = null;

      //get timeframe for which function must be evaluated
      int timeframe = wrapper.GetIntegerParameter(0, 0);

      switch (wrapper.Name)
      {
        case tkn_ts:
          result = new ValueType(_ichimoku[timeframe].TenkanSen);
          break;
        case tkn_ks:
          result = new ValueType(_ichimoku[timeframe].KijunSen);
          break;
        case tkn_cs:
          result = new ValueType(_ichimoku[timeframe].ChikouSpan);
          break;
        case tkn_ssa:
          result = new ValueType(_ichimoku[timeframe].SenkouSpanA);
          break;
        case tkn_ssb:
          result = new ValueType(_ichimoku[timeframe].SenkouSpanB);
          break;
        case tkn_curr_ssa:
          result = new ValueType(_ichimoku[timeframe].CurrentSenkouSpanA);
          break;
        case tkn_curr_ssb:
          result = new ValueType(_ichimoku[timeframe].CurrentSenkouSpanB);
          break;
        case tkn_cs_ssa:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanSenkouSpanA);
          break;
        case tkn_cs_ssb:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanSenkouSpanB);
          break;
        case tkn_pr_gt_km:
          result = new ValueType(_ichimoku[timeframe].PriceAboveKumo);
          break;
        case tkn_pr_lt_km:
          result = new ValueType(_ichimoku[timeframe].PriceBelowKumo);
          break;
        case tkn_pr_gt_ts:
          result = new ValueType(_ichimoku[timeframe].PriceAboveTenkanSen);
          break;
        case tkn_pr_lt_ts:
          result = new ValueType(_ichimoku[timeframe].PriceBelowTenkanSen);
          break;
        case tkn_pr_gt_ks:
          result = new ValueType(_ichimoku[timeframe].PriceAboveKijunSen);
          break;
        case tkn_pr_lt_ks:
          result = new ValueType(_ichimoku[timeframe].PriceBelowKijunSen);
          break;
        case tkn_pr_cl_ts:
          result = new ValueType(_ichimoku[timeframe].PriceCloseToTenkanSen);
          break;
        case tkn_pr_cl_ks:
          result = new ValueType(_ichimoku[timeframe].PriceCloseToKijunSen);
          break;
        case tkn_ts_gt_ks:
          result = new ValueType(_ichimoku[timeframe].TenkanSenAboveKijunSen);
          break;
        case tkn_ts_lt_ks:
          result = new ValueType(_ichimoku[timeframe].TenkanSenBelowKijunSen);
          break;
        case tkn_ts_cl_ks:
          result = new ValueType(_ichimoku[timeframe].TenkanSenCloseToKijunSen);
          break;
        case tkn_ck_gt_pr:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanAbovePrice);
          break;
        case tkn_ck_lt_pr:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanBelowPrice);
          break;
        case tkn_ck_bl_sp:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanBullishOpenSpace);
          break;
        case tkn_ck_br_sp:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanBearishOpenSpace);
          break;
        case tkn_ts_mt_pt:
          result = new ValueType(_ichimoku[timeframe].TenkanSenMomentum > 0);
          break;
        case tkn_ts_mt_ft:
          result = new ValueType(_ichimoku[timeframe].TenkanSenMomentum == 0);
          break;
        case tkn_ts_mt_nt:
          result = new ValueType(_ichimoku[timeframe].TenkanSenMomentum < 0);
          break;
        case tkn_ks_mt_pt:
          result = new ValueType(_ichimoku[timeframe].KijunSenMomentum > 0);
          break;
        case tkn_ks_mt_ft:
          result = new ValueType(_ichimoku[timeframe].KijunSenMomentum == 0);
          break;
        case tkn_ks_mt_nt:
          result = new ValueType(_ichimoku[timeframe].KijunSenMomentum < 0);
          break;
        case tkn_ck_mt_pt:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanMomentum > 0);
          break;
        case tkn_ck_mt_ft:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanMomentum == 0);
          break;
        case tkn_ck_mt_nt:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanMomentum < 0);
          break;
        case tkn_csa_mt_pt:
          result = new ValueType(_ichimoku[timeframe].CurrentSenkouAMomentum > 0);
          break;
        case tkn_csa_mt_ft:
          result = new ValueType(_ichimoku[timeframe].CurrentSenkouAMomentum == 0);
          break;
        case tkn_csa_mt_nt:
          result = new ValueType(_ichimoku[timeframe].CurrentSenkouAMomentum < 0);
          break;
        case tkn_fsa_mt_pt:
          result = new ValueType(_ichimoku[timeframe].FutureSenkouAMomentum > 0);
          break;
        case tkn_fsa_mt_ft:
          result = new ValueType(_ichimoku[timeframe].FutureSenkouAMomentum == 0);
          break;
        case tkn_fsa_mt_nt:
          result = new ValueType(_ichimoku[timeframe].FutureSenkouAMomentum < 0);
          break;
        case tkn_csb_mt_pt:
          result = new ValueType(_ichimoku[timeframe].CurrentSenkouBMomentum > 0);
          break;
        case tkn_csb_mt_ft:
          result = new ValueType(_ichimoku[timeframe].CurrentSenkouBMomentum == 0);
          break;
        case tkn_csb_mt_nt:
          result = new ValueType(_ichimoku[timeframe].CurrentSenkouBMomentum < 0);
          break;
        case tkn_fsb_mt_pt:
          result = new ValueType(_ichimoku[timeframe].FutureSenkouBMomentum > 0);
          break;
        case tkn_fsb_mt_ft:
          result = new ValueType(_ichimoku[timeframe].FutureSenkouBMomentum == 0);
          break;
        case tkn_fsb_mt_nt:
          result = new ValueType(_ichimoku[timeframe].FutureSenkouBMomentum < 0);
          break;
        case tkn_ckkm_bl:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanKumoBullish);
          break;
        case tkn_ckkm_br:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanKumoBearish);
          break;
        case tkn_ckm_bl:
          result = new ValueType(_ichimoku[timeframe].CurrentKumoBullish);
          break;
        case tkn_ckm_br:
          result = new ValueType(_ichimoku[timeframe].CurrentKumoBearish);
          break;
        case tkn_fkm_bl:
          result = new ValueType(_ichimoku[timeframe].FutureKumoBullish);
          break;
        case tkn_fkm_br:
          result = new ValueType(_ichimoku[timeframe].FutureKumoBearish);
          break;
        case tkn_ckkm_tk:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanKumoThick);
          break;
        case tkn_ckkm_tn:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanKumoThin);
          break;
        case tkn_ckm_tk:
          result = new ValueType(_ichimoku[timeframe].CurrentKumoThick);
          break;
        case tkn_ckm_tn:
          result = new ValueType(_ichimoku[timeframe].CurrentKumoThin);
          break;
        case tkn_fkm_tk:
          result = new ValueType(_ichimoku[timeframe].FutureKumoThick);
          break;
        case tkn_fkm_tn:
          result = new ValueType(_ichimoku[timeframe].FutureKumoThin);
          break;
        case tkn_pr_in_km:
          result = new ValueType(_ichimoku[timeframe].PriceInKumo);
          break;
        case tkn_pr_in_kmsh:
          result = new ValueType(_ichimoku[timeframe].PriceInKumoShadow);
          break;
        case tkn_ts_gt_km:
          result = new ValueType(_ichimoku[timeframe].TenkanSenAboveKumo);
          break;
        case tkn_ts_lt_km:
          result = new ValueType(_ichimoku[timeframe].TenkanSenBelowKumo);
          break;
        case tkn_ts_in_km:
          result = new ValueType(_ichimoku[timeframe].TenkanSenInKumo);
          break;
        case tkn_ks_gt_km:
          result = new ValueType(_ichimoku[timeframe].KijunSenAboveKumo);
          break;
        case tkn_ks_lt_km:
          result = new ValueType(_ichimoku[timeframe].KijunSenBelowKumo);
          break;
        case tkn_ks_in_km:
          result = new ValueType(_ichimoku[timeframe].KijunSenInKumo);
          break;
        case tkn_ck_gt_km:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanAboveKumo);
          break;
        case tkn_ck_lt_km:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanBelowKumo);
          break;
        case tkn_ck_in_km:
          result = new ValueType(_ichimoku[timeframe].ChikouSpanInKumo);
          break;
        default:
          throw new ArgumentException("Ichimoku factory does not support function " + wrapper.Name + ".");
      }

      return result;
    }
  }
  #endregion

  #region Price Action Functions	
  /// <summary>
  /// Function factory to extend strategy scripting with price action analysis.
  /// </summary>
  public class PriceActionFunctions : FunctionFactory
  {
    //Factory name to import
    public const string tkn_factory_name = "price_action";


    //TBD: Add functions to evaluate the bar size relative to the ATR
    // * pa_bar_size_very_small
    // * pa_bar_size_small
    // * pa_bar_size_average
    // * pa_bar_size_large
    // * pa_bar_size_very_large


    //Set of supported function tokens
    public const string tkn_bars_to_keep = "pa_bars_to_keep";
    public const string tkn_fractal_layers = "pa_fractal_layers";
    public const string tkn_has_bar = "pa_has_bar";
    public const string tkn_bar_at_index = "pa_bar_at_index";
    public const string tkn_bar_open = "pa_bar_open";
    public const string tkn_bar_high = "pa_bar_high";
    public const string tkn_bar_low = "pa_bar_low";
    public const string tkn_bar_close = "pa_bar_close";

    //Set of supported criteria for the pa_has_bar function
    public string tkn_criteria_bull = "bullish";
    public string tkn_criteria_bear = "bearish";
    public string tkn_criteria_double_bottom = "double_bottom";
    public string tkn_criteria_double_top = "double_top";
    public string tkn_criteria_lower_low = "lower_low";
    public string tkn_criteria_higher_low = "higher_low";
    public string tkn_criteria_higher_high = "higher_high";
    public string tkn_criteria_lower_high = "lower_high";
    public string tkn_criteria_pivot_high = "pivot_high";
    public string tkn_criteria_pivot_low = "pivot_low";
    public string tkn_criteria_breakout = "breakout";
    public string tkn_criteria_strong = "strong";
    public string tkn_criteria_outside = "outside";
    public string tkn_criteria_inside = "inside";
    public string tkn_criteria_pin_low = "pin_low";                 //pin bar with body at the low end of the bar
    public string tkn_criteria_pin_high = "pin_high";               //pin bar with body at the high end of the bar
    public string tkn_criteria_doji = "doji";
    public string tkn_criteria_engulfing = "engulfing";
    public string tkn_criteria_reversal = "reversal";

    //Constants:
    public const int DefaultFractalLayerCount = 3;                  //number of fractal layers to use for the price action analysis 
    public const int DefaultBarsToKeep = 500;                       //number of bars to keep for the price action analysis		

    //Attributes:
    protected int _barsToKeep;
    protected int _fractalLayers;
    protected List<PriceActionAnalysis> _priceAction;               //list of price action indicators associated with the defined set of timeframes
    protected PriceActionBar _lastHasBar;

    public PriceActionFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      //set the defaults for the price action analysis attributes
      _fractalLayers = DefaultFractalLayerCount;
      _barsToKeep = DefaultBarsToKeep;

      //add the set of functions supported by the function factory
      _functions.Add(tkn_bars_to_keep);
      _functions.Add(tkn_fractal_layers);
      _functions.Add(tkn_has_bar);
      _functions.Add(tkn_bar_at_index);
      _functions.Add(tkn_bar_open);
      _functions.Add(tkn_bar_high);
      _functions.Add(tkn_bar_low);
      _functions.Add(tkn_bar_close);
    }

    /// <summary>
    /// Initializes the set of required price action analysis inicators for the defined timeframes of the signal object.
    /// </summary>
    public override void StartCalc()
    {
      base.StartCalc();

      //create the set of required ichimoku Indicators for the number of time frames required by the calling script
      _priceAction = new List<PriceActionAnalysis>(Interpreter.MAX_DATA_STREAM);
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        PriceActionAnalysis priceActionTimeFrame = new PriceActionAnalysis(_fractalLayers);
        priceActionTimeFrame.Size = _barsToKeep;
        _priceAction.Add(priceActionTimeFrame);
      }
    }

    /// <summary>
    /// Updates the set of price action analysis indicators associated with the different timeframes used by the supported functions.
    /// </summary>
    public override ValueType Evaluate()
    {
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        IInstrument dataFeed = null;

        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          dataFeed = _interpreter.Strategy.BarsOfData(i + 1);
        else
          dataFeed = _interpreter.Strategy.Bars;

        _priceAction[i].Analyse(dataFeed.Time[0], dataFeed.Open[0], dataFeed.High[0], dataFeed.Low[0], dataFeed.Close[0], dataFeed.Volume[0]);
      }

      return new ValueType();
    }

    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        //supported factory functions
        case tkn_bars_to_keep:
        case tkn_fractal_layers:
          result = new FunctionProxy(FunctionType.Factory, name, this, parameters);
          break;

        //supported trigger functions
        case tkn_has_bar:
        case tkn_bar_at_index:
        case tkn_bar_open:
        case tkn_bar_high:
        case tkn_bar_low:
        case tkn_bar_close:
          result = new FunctionProxy(FunctionType.Trigger, name, this, parameters);
          break;

        default:
          throw new Exception("Price action factory does not support function with name \"" + name + "\".");
      }

      return result;
    }

    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = null;

      //we need to handle factory functions differently than the trigger functions
      if (wrapper.Type == FunctionType.Factory)
      {
        //factory function evaluation does not return anything
        result = new ValueType();

        switch (wrapper.Name)
        {
          case tkn_bars_to_keep:
            _barsToKeep = wrapper.GetIntegerParameter(0, "Price action bars to keep function requires a single integer parameter.");
            break;
          case tkn_fractal_layers:
            _fractalLayers = wrapper.GetIntegerParameter(0, "Price action fractal layer count function requires a single integer parameter.");
            break;
          default:
            throw new Exception("Price action factory does not support function with name \"" + wrapper.Name + "\".");
        }
      }
      else
      {
        //process the trigger function
        switch (wrapper.Name)
        {
          case tkn_has_bar:
            result = evaluateHasBar(wrapper);
            break;
          case tkn_bar_at_index:
            result = evaluateBarAtIndex(wrapper);
            break;
          case tkn_bar_open:
            if (null != _lastHasBar)
              result = new ValueType(_lastHasBar.open);
            else
              throw new Exception("Price action last bar is not set, no open price available.");
            break;
          case tkn_bar_high:
            if (null != _lastHasBar)
              result = new ValueType(_lastHasBar.high);
            else
              throw new Exception("Price action last bar is not set, no high price available.");
            break;
          case tkn_bar_low:
            if (null != _lastHasBar)
              result = new ValueType(_lastHasBar.low);
            else
              throw new Exception("Price action last bar is not set, no low price available.");
            break;
          case tkn_bar_close:
            if (null != _lastHasBar)
              result = new ValueType(_lastHasBar.close);
            else
              throw new Exception("Price action last bar is not set, no close price available.");
            break;
          default:
            throw new Exception("Price action factory does not support function with name \"" + wrapper.Name + "\".");
        }
      }

      return result;
    }

    /// <summary>
    /// Implements the logic of the has-bar function that will search back a specific length of bars and return true if a bar is found with a specific set of
    /// attributes.
    /// </summary>
    private ValueType evaluateHasBar(FunctionProxy wrapper)
    {
      bool result = false;
      int timeframe = wrapper.GetIntegerParameter(0, string.Format("{0} function requires a timeframe parameter.", wrapper.Name));
      int barsToSearch = wrapper.GetIntegerParameter(1, string.Format("{0} function requires a parameter for the number of bars to search.", wrapper.Name));
      int fractalLayer = wrapper.GetIntegerParameter(2, string.Format("{0} function requires a parameter for the fractal layer to analyze.", wrapper.Name));

      //get the price action instance to analyse according to timeframe
      PriceActionAnalysis priceAction = _priceAction[timeframe];
      SortedList<DateTime, PriceActionBar> bars = priceAction.Bars;

      //clip the bars to search parameter if we have not yet accumulated enough bars
      barsToSearch = barsToSearch <= bars.Count ? barsToSearch : bars.Count;

      //check that the user specified the enumeration criteria for the bar
      if (wrapper.Parameters.Count <= 3)
        throw new Exception(string.Format("{0} function requires as enumerated set of parameters for the bars to search for.", wrapper.Name));

      //search the number of bars for the given enumerated criteria specified by parameters 4 to N
      _lastHasBar = null;
      int barIndex = 0;
      for (int i = 0; i < barsToSearch; i++)
      {
        barIndex = bars.Values.Count - i - 1;   //-1 since bar indices are zero based
        PriceActionBar bar = bars.Values[barIndex];

        result = true;  //assume criteria will be matched for this bar

        for (int parameterIndex = 3; parameterIndex < wrapper.Parameters.Count; parameterIndex++)
        {
          string criteria = wrapper.GetEnumerationParameter(parameterIndex, string.Format("Failed to find criteria parameter at index {0} for function \"{1}\".", parameterIndex, wrapper.Name));

          if (criteria == tkn_criteria_bear)
          {
            result &= bar.state.bear;
          }
          else if (criteria == tkn_criteria_bull)
          {
            result &= bar.state.bull;
          }
          else if (criteria == tkn_criteria_double_bottom)
          {
            result &= bar.state.doubleBottom;
          }
          else if (criteria == tkn_criteria_double_top)
          {
            result &= bar.state.doubleTop;
          }
          else if (criteria == tkn_criteria_lower_low)
          {
            result &= bar.state.lowerLow;
          }
          else if (criteria == tkn_criteria_higher_low)
          {
            result &= bar.state.higherLow;
          }
          else if (criteria == tkn_criteria_higher_high)
          {
            result &= bar.state.higherHigh;
          }
          else if (criteria == tkn_criteria_lower_high)
          {
            result &= bar.state.lowerHigh;
          }
          else if (criteria == tkn_criteria_pivot_high)
          {
            result &= bar.state.pivotState[fractalLayer].pivotHigh;
          }
          else if (criteria == tkn_criteria_pivot_low)
          {
            result &= bar.state.pivotState[fractalLayer].pivotLow;
          }
          else if (criteria == tkn_criteria_breakout)
          {
            result &= bar.state.breakout;
          }
          else if (criteria == tkn_criteria_strong)
          {
            result &= bar.state.strong;
          }
          else if (criteria == tkn_criteria_outside)
          {
            result &= bar.state.outside;
          }
          else if (criteria == tkn_criteria_inside)
          {
            result &= bar.state.inside;
          }
          else if (criteria == tkn_criteria_pin_low)
          {
            result &= bar.state.lowPin;
          }
          else if (criteria == tkn_criteria_pin_high)
          {
            result &= bar.state.highPin;
          }
          else if (criteria == tkn_criteria_doji)
          {
            result &= bar.state.doji;
          }
          else if (criteria == tkn_criteria_engulfing)
          {
            result &= bar.state.engulfing;
          }
          else if (criteria == tkn_criteria_reversal)
          {
            result &= bar.state.reversal;
          }
          else
            throw new Exception(string.Format("Function \"{0}\" does not support bar criteria \"{1}\".", wrapper.Name, criteria));

          //terminate criteria loop as soon as bar can no longer match the specified criteria
          if (!result)
            break;
        }

        //terminate search loop on first bar found matching the described criteria
        if (result)
        {
          _lastHasBar = bar;
          break;
        }
      }

      return new ValueType(result);
    }

    /// <summary>
    /// Implements the logic of the bar-at-index function that would look at a specific bar back by a certain index to determine
    /// whether it meets certain criteria.
    /// </summary>
    private ValueType evaluateBarAtIndex(FunctionProxy wrapper)
    {
      bool result = false;
      int timeframe = wrapper.GetIntegerParameter(0, string.Format("{0} function requires a timeframe parameter.", wrapper.Name));
      int barIndex = wrapper.GetIntegerParameter(1, string.Format("{0} function requires a parameter for the bar index.", wrapper.Name));
      int fractalLayer = wrapper.GetIntegerParameter(2, string.Format("{0} function requires a parameter for the fractal layer to analyze.", wrapper.Name));

      //get the price action instance to analyse according to timeframe
      PriceActionAnalysis priceAction = _priceAction[timeframe];
      SortedList<DateTime, PriceActionBar> bars = priceAction.Bars;

      //check that the user specified the enumeration criteria for the bar
      if (wrapper.Parameters.Count <= 3)
        throw new Exception(string.Format("{0} function requires as enumerated set of parameters for the bars to search for.", wrapper.Name));

      //function only evaluates to true if we have enough bars to reference back by the specified index
      if (bars.Count >= barIndex)
      {
        //get the bar to analyze and look through the specific set of criteria
        PriceActionBar bar = bars.Values[barIndex];

        result = true;  //assume criteria will be matched for this bar

        for (int parameterIndex = 3; parameterIndex < wrapper.Parameters.Count; parameterIndex++)
        {
          string criteria = wrapper.GetEnumerationParameter(parameterIndex, string.Format("Failed to find criteria parameter at index {0} for function \"{1}\".", parameterIndex, wrapper.Name));

          if (criteria == tkn_criteria_bear)
          {
            result &= bar.state.bear;
          }
          else if (criteria == tkn_criteria_bull)
          {
            result &= bar.state.bull;
          }
          else if (criteria == tkn_criteria_double_bottom)
          {
            result &= bar.state.doubleBottom;
          }
          else if (criteria == tkn_criteria_double_top)
          {
            result &= bar.state.doubleTop;
          }
          else if (criteria == tkn_criteria_lower_low)
          {
            result &= bar.state.lowerLow;
          }
          else if (criteria == tkn_criteria_higher_low)
          {
            result &= bar.state.higherLow;
          }
          else if (criteria == tkn_criteria_higher_high)
          {
            result &= bar.state.higherHigh;
          }
          else if (criteria == tkn_criteria_lower_high)
          {
            result &= bar.state.lowerHigh;
          }
          else if (criteria == tkn_criteria_pivot_high)
          {
            result &= bar.state.pivotState[fractalLayer].pivotHigh;
          }
          else if (criteria == tkn_criteria_pivot_low)
          {
            result &= bar.state.pivotState[fractalLayer].pivotLow;
          }
          else if (criteria == tkn_criteria_breakout)
          {
            result &= bar.state.breakout;
          }
          else if (criteria == tkn_criteria_strong)
          {
            result &= bar.state.strong;
          }
          else if (criteria == tkn_criteria_outside)
          {
            result &= bar.state.outside;
          }
          else if (criteria == tkn_criteria_inside)
          {
            result &= bar.state.inside;
          }
          else if (criteria == tkn_criteria_pin_low)
          {
            result &= bar.state.lowPin;
          }
          else if (criteria == tkn_criteria_pin_high)
          {
            result &= bar.state.highPin;
          }
          else if (criteria == tkn_criteria_doji)
          {
            result &= bar.state.doji;
          }
          else if (criteria == tkn_criteria_engulfing)
          {
            result &= bar.state.engulfing;
          }
          else if (criteria == tkn_criteria_reversal)
          {
            result &= bar.state.reversal;
          }
          else
            throw new Exception(string.Format("Function \"{0}\" does not support bar criteria \"{1}\".", wrapper.Name, criteria));

          //terminate criteria loop as soon as bar can no longer match the specified criteria
          if (!result)
            break;
        }
      }

      return new ValueType(result);
    }
  }
  #endregion

  #region Moving Average Functions
  /// <summary>
  /// Function factory to extend strategy script with moving average functionality.
  /// </summary>
  public class MovingAverageFunctions : FunctionFactory
  {
    //Constants:
    public const int DefaultMAFastLength = 20;
    public const int DefaultMASlowLength = 50;
    public const int DefaultLinRegFastLength = 100;
    public const int DefaultLinRegSlowLength = 200;
    public const int DefaultCrossOverLength = 5;

    //Factory name to import
    public const string tkn_factory_name = "ma";

    //Set of supported function tokens
    public const string tkn_sma_fast_len = "ma_sma_fast_len";
    public const string tkn_sma_slow_len = "ma_sma_slow_len";
    public const string tkn_ema_fast_len = "ma_ema_fast_len";
    public const string tkn_ema_slow_len = "ma_ema_slow_len";
    public const string tkn_ssma_fast_len = "ma_ssma_fast_len";
    public const string tkn_ssma_slow_len = "ma_ssma_slow_len";
    public const string tkn_lin_reg_fast_len = "ma_lin_reg_fast_len";
    public const string tkn_lin_reg_slow_len = "ma_lin_reg_slow_len";
    public const string tkn_ma_cross_over_len = "ma_cross_over_len";
    public const string tkn_sma_cross_over = "sma_cross_over";
    public const string tkn_sma_cross_under = "sma_cross_under";
    public const string tkn_ema_cross_over = "ema_cross_over";
    public const string tkn_ema_cross_under = "ema_cross_under";
    public const string tkn_linreg_cross_over = "linreg_cross_over";
    public const string tkn_linreg_cross_under = "linreg_cross_under";
    public const string tkn_ssma_cross_over = "ssma_cross_over";
    public const string tkn_ssma_cross_under = "ssma_cross_under";
    public const string tkn_sma_fast_lt_slow = "sma_fast_lt_slow";
    public const string tkn_sma_fast_gt_slow = "sma_fast_gt_slow";
    public const string tkn_ema_fast_lt_slow = "ema_fast_lt_slow";
    public const string tkn_ema_fast_gt_slow = "ema_fast_gt_slow";
    public const string tkn_linreg_fast_lt_slow = "linreg_fast_lt_slow";
    public const string tkn_linreg_fast_gt_slow = "linreg_fast_gt_slow";
    public const string tkn_ssma_fast_lt_slow = "ssma_fast_lt_slow";
    public const string tkn_ssma_fast_gt_slow = "ssma_fast_gt_slow";

    //Attributes:
    protected int _smaFastLength;
    protected int _smaSlowLength;
    protected int _emaFastLength;
    protected int _emaSlowLength;
    protected int _linRegFastLength;
    protected int _linRegSlowLength;
    protected int _ssmaFastLength;
    protected int _ssmaSlowLength;
    protected int _crossOverLength;
    protected List<_AverageSimple> _smaFastAvgFC;
    protected List<_AverageSimple> _smaSlowAvgFC;
    protected List<VariableSeries<Double>> _smaFastAvg;
    protected List<VariableSeries<Double>> _smaSlowAvg;
    protected List<_AverageExponential> _emaFastAvgFC;
    protected List<_AverageExponential> _emaSlowAvgFC;
    protected List<VariableSeries<Double>> _emaFastAvg;
    protected List<VariableSeries<Double>> _emaSlowAvg;
    protected List<LinearRegValue> _linRegFastFC;
    protected List<LinearRegValue> _linRegSlowFC;
    protected List<VariableSeries<Double>> _linRegFast;
    protected List<VariableSeries<Double>> _linRegSlow;
    protected List<PowerLanguage.Function._SuperSmoother> _ssmaFastAvgFC;
    protected List<PowerLanguage.Function._SuperSmoother> _ssmaSlowAvgFC;
    protected List<VariableSeries<Double>> _ssmaFastAvg;
    protected List<VariableSeries<Double>> _ssmaSlowAvg;

    public MovingAverageFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      //setup instance attributes
      _smaFastLength = DefaultMAFastLength;
      _smaSlowLength = DefaultMASlowLength;
      _emaFastLength = DefaultMAFastLength;
      _emaSlowLength = DefaultMASlowLength;
      _linRegFastLength = DefaultLinRegFastLength;
      _linRegSlowLength = DefaultLinRegSlowLength;
      _ssmaFastLength = DefaultMAFastLength;
      _ssmaSlowLength = DefaultMASlowLength;
      _crossOverLength = DefaultCrossOverLength;

      //add the set of functions supported by the function factory
      _functions.Add(tkn_sma_fast_len);
      _functions.Add(tkn_sma_slow_len);
      _functions.Add(tkn_ema_fast_len);
      _functions.Add(tkn_ema_slow_len);
      _functions.Add(tkn_lin_reg_fast_len);
      _functions.Add(tkn_lin_reg_slow_len);
      _functions.Add(tkn_ssma_fast_len);
      _functions.Add(tkn_ssma_slow_len);
      _functions.Add(tkn_ma_cross_over_len);
      _functions.Add(tkn_sma_cross_over);
      _functions.Add(tkn_sma_cross_under);
      _functions.Add(tkn_ema_cross_over);
      _functions.Add(tkn_ema_cross_under);
      _functions.Add(tkn_linreg_cross_over);
      _functions.Add(tkn_linreg_cross_under);
      _functions.Add(tkn_ssma_cross_over);
      _functions.Add(tkn_ssma_cross_under);
      _functions.Add(tkn_sma_fast_lt_slow);
      _functions.Add(tkn_sma_fast_gt_slow);
      _functions.Add(tkn_ema_fast_lt_slow);
      _functions.Add(tkn_ema_fast_gt_slow);
      _functions.Add(tkn_linreg_fast_lt_slow);
      _functions.Add(tkn_linreg_fast_gt_slow);
      _functions.Add(tkn_ssma_fast_lt_slow);
      _functions.Add(tkn_ssma_fast_gt_slow);
    }

    /// <summary>
    /// Create the set of moving average calculations, this must occur in the Create method.
    /// </summary>
    public override void Create()
    {
      base.Create();

      // create the set of required moving average indicators
      _smaFastAvgFC = new List<_AverageSimple>(Interpreter.MAX_DATA_STREAM);
      _smaSlowAvgFC = new List<_AverageSimple>(Interpreter.MAX_DATA_STREAM);
      _smaFastAvg = new List<VariableSeries<Double>>(Interpreter.MAX_DATA_STREAM);
      _smaSlowAvg = new List<VariableSeries<Double>>(Interpreter.MAX_DATA_STREAM);
      _emaFastAvgFC = new List<_AverageExponential>(Interpreter.MAX_DATA_STREAM);
      _emaSlowAvgFC = new List<_AverageExponential>(Interpreter.MAX_DATA_STREAM);
      _emaFastAvg = new List<VariableSeries<Double>>(Interpreter.MAX_DATA_STREAM);
      _emaSlowAvg = new List<VariableSeries<Double>>(Interpreter.MAX_DATA_STREAM);
      _linRegFastFC = new List<LinearRegValue>(Interpreter.MAX_DATA_STREAM);
      _linRegSlowFC = new List<LinearRegValue>(Interpreter.MAX_DATA_STREAM);
      _linRegFast = new List<VariableSeries<Double>>(Interpreter.MAX_DATA_STREAM);
      _linRegSlow = new List<VariableSeries<Double>>(Interpreter.MAX_DATA_STREAM);
      _ssmaFastAvgFC = new List<PowerLanguage.Function._SuperSmoother>(Interpreter.MAX_DATA_STREAM);
      _ssmaSlowAvgFC = new List<PowerLanguage.Function._SuperSmoother>(Interpreter.MAX_DATA_STREAM);
      _ssmaFastAvg = new List<VariableSeries<Double>>(Interpreter.MAX_DATA_STREAM);
      _ssmaSlowAvg = new List<VariableSeries<Double>>(Interpreter.MAX_DATA_STREAM);

      //NOTE: The indicators must be created before the script is processed so we need to create the maximum number of time frames.
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        _smaFastAvgFC.Add(new _AverageSimple(_interpreter.Strategy));
        _smaSlowAvgFC.Add(new _AverageSimple(_interpreter.Strategy));
        _smaFastAvg.Add(new VariableSeries<Double>(_interpreter.Strategy));
        _smaSlowAvg.Add(new VariableSeries<Double>(_interpreter.Strategy));
        _emaFastAvgFC.Add(new _AverageExponential(_interpreter.Strategy));
        _emaSlowAvgFC.Add(new _AverageExponential(_interpreter.Strategy));
        _emaFastAvg.Add(new VariableSeries<Double>(_interpreter.Strategy));
        _emaSlowAvg.Add(new VariableSeries<Double>(_interpreter.Strategy));
        _linRegFastFC.Add(new LinearRegValue(_interpreter.Strategy));
        _linRegSlowFC.Add(new LinearRegValue(_interpreter.Strategy));
        _linRegFast.Add(new VariableSeries<Double>(_interpreter.Strategy));
        _linRegSlow.Add(new VariableSeries<Double>(_interpreter.Strategy));
        _ssmaFastAvgFC.Add(new PowerLanguage.Function._SuperSmoother(_interpreter.Strategy));
        _ssmaSlowAvgFC.Add(new PowerLanguage.Function._SuperSmoother(_interpreter.Strategy));
        _ssmaFastAvg.Add(new VariableSeries<Double>(_interpreter.Strategy));
        _ssmaSlowAvg.Add(new VariableSeries<Double>(_interpreter.Strategy));
      }
    }

    /// <summary>
    /// Initializes the moving average components to use the correct price data.
    /// </summary>
    public override void StartCalc()
    {
      base.StartCalc();

      //NOTE: The indicators must be created before the script is processed so we need to create the maximum number of time frames.
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        IInstrument dataFeed = null;

        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          dataFeed = _interpreter.Strategy.BarsOfData(i + 1);
        else
          dataFeed = _interpreter.Strategy.Bars;

        _smaFastAvgFC[i].Instrument = dataFeed;
        _smaFastAvgFC[i].Length = _smaFastLength;
        _smaSlowAvgFC[i].Instrument = dataFeed;
        _smaSlowAvgFC[i].Length = _smaSlowLength;
        _emaFastAvgFC[i].Instrument = dataFeed;
        _emaFastAvgFC[i].Length = _emaFastLength;
        _emaSlowAvgFC[i].Instrument = dataFeed;
        _emaSlowAvgFC[i].Length = _emaSlowLength;
        _linRegFastFC[i].price = dataFeed.Close;    //TODO - this most likely does not work correctly on multiple data feeds
        _linRegFastFC[i].length = _linRegFastLength;
        _linRegSlowFC[i].price = dataFeed.Close;    //TODO - this most likely does not work correctly on multiple data feeds
				_linRegSlowFC[i].length = _linRegSlowLength;
        _ssmaFastAvgFC[i].Instrument = dataFeed;
        _ssmaFastAvgFC[i].Length = _ssmaFastLength;
        _ssmaSlowAvgFC[i].Instrument = dataFeed;
        _ssmaSlowAvgFC[i].Length = _ssmaSlowLength;
      }
    }

    /// <summary>
    /// Updates the set of moving average indicators associated with the different timeframes used by the supported functions.
    /// </summary>
    public override ValueType Evaluate()
    {
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        _smaFastAvg[i].Value = _smaFastAvgFC[i][0];
        _smaSlowAvg[i].Value = _smaSlowAvgFC[i][0];
        _emaFastAvg[i].Value = _emaFastAvgFC[i][0];
        _emaSlowAvg[i].Value = _emaSlowAvgFC[i][0];
        _linRegFast[i].Value = _linRegFastFC[i][0];
        _linRegSlow[i].Value = _linRegSlowFC[i][0];
        _ssmaFastAvg[i].Value = _ssmaFastAvgFC[i][0];
        _ssmaSlowAvg[i].Value = _ssmaSlowAvgFC[i][0];
      }

      return new ValueType();
    }

    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        //supported factory functions
        case tkn_sma_fast_len:
        case tkn_sma_slow_len:
        case tkn_ema_fast_len:
        case tkn_ema_slow_len:
        case tkn_lin_reg_fast_len:
        case tkn_lin_reg_slow_len:
        case tkn_ssma_fast_len:
        case tkn_ssma_slow_len:
        case tkn_ma_cross_over_len:
          result = new FunctionProxy(FunctionType.Factory, name, this, parameters);
          break;

        //supported trigger functions
        case tkn_sma_cross_over:
        case tkn_sma_cross_under:
        case tkn_ema_cross_over:
        case tkn_ema_cross_under:
        case tkn_linreg_cross_over:
        case tkn_linreg_cross_under:
        case tkn_ssma_cross_over:
        case tkn_ssma_cross_under:
        case tkn_sma_fast_lt_slow:
        case tkn_sma_fast_gt_slow:
        case tkn_ema_fast_lt_slow:
        case tkn_ema_fast_gt_slow:
        case tkn_linreg_fast_lt_slow:
        case tkn_linreg_fast_gt_slow:
        case tkn_ssma_fast_lt_slow:
        case tkn_ssma_fast_gt_slow:
          result = new FunctionProxy(FunctionType.Trigger, name, this, parameters);
          break;

        default:
          throw new Exception("Moving Average factory does not support function with name \"" + name + "\".");
      }

      return result;
    }

    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = null;

      if (wrapper.Type == FunctionType.Factory)
      {
        int length = wrapper.GetIntegerParameter(0, "Moving average function \"" + wrapper.Name + "\" requires an integer parameter.");

        switch (wrapper.Name)
        {
          case tkn_sma_fast_len:
            _smaFastLength = length;
            break;
          case tkn_sma_slow_len:
            _smaSlowLength = length;
            break;
          case tkn_ema_fast_len:
            _emaFastLength = length;
            break;
          case tkn_ema_slow_len:
            _emaSlowLength = length;
            break;
          case tkn_lin_reg_fast_len:
            _linRegFastLength = length;
            break;
          case tkn_lin_reg_slow_len:
            _linRegSlowLength = length;
            break;
          case tkn_ma_cross_over_len:
            _crossOverLength = length;
            break;
          case tkn_ssma_fast_len:
            _ssmaFastLength = length;
            break;
          case tkn_ssma_slow_len:
            _ssmaSlowLength = length;
            break;
          default:
            throw new Exception("Moving Average factory does not support function with name \"" + wrapper.Name + "\".");
        }

      }
      else
      {
        //get the timeframe to analyse
        int timeframe = wrapper.GetIntegerParameter(0, 0);

        switch (wrapper.Name)
        {
          case tkn_sma_cross_over:
            result = new ValueType(_smaFastAvg[timeframe].CrossesOver(_smaSlowAvg[timeframe], _crossOverLength));
            break;
          case tkn_sma_cross_under:
            result = new ValueType(_smaFastAvg[timeframe].CrossesUnder(_smaSlowAvg[timeframe], _crossOverLength));
            break;
          case tkn_ema_cross_over:
            result = new ValueType(_emaFastAvg[timeframe].CrossesOver(_emaSlowAvg[timeframe], _crossOverLength));
            break;
          case tkn_ema_cross_under:
            result = new ValueType(_emaFastAvg[timeframe].CrossesUnder(_emaSlowAvg[timeframe], _crossOverLength));
            break;
          case tkn_linreg_cross_over:
            result = new ValueType(_linRegFast[timeframe].CrossesOver(_linRegSlow[timeframe], _crossOverLength));
            break;
          case tkn_linreg_cross_under:
            result = new ValueType(_linRegFast[timeframe].CrossesUnder(_linRegSlow[timeframe], _crossOverLength));
            break;
          case tkn_ssma_cross_over:
            result = new ValueType(_ssmaFastAvg[timeframe].CrossesOver(_ssmaSlowAvg[timeframe], _crossOverLength));
            break;
          case tkn_ssma_cross_under:
            result = new ValueType(_ssmaFastAvg[timeframe].CrossesUnder(_ssmaSlowAvg[timeframe], _crossOverLength));
            break;
          case tkn_sma_fast_lt_slow:
            result = new ValueType(_smaFastAvg[timeframe].Value < _smaSlowAvg[timeframe].Value);
            break;
          case tkn_sma_fast_gt_slow:
            result = new ValueType(_smaFastAvg[timeframe].Value > _smaSlowAvg[timeframe].Value);
            break;
          case tkn_ema_fast_lt_slow:
            result = new ValueType(_emaFastAvg[timeframe].Value < _emaSlowAvg[timeframe].Value);
            break;
          case tkn_ema_fast_gt_slow:
            result = new ValueType(_emaFastAvg[timeframe].Value > _emaSlowAvg[timeframe].Value);
            break;
          case tkn_linreg_fast_lt_slow:
            result = new ValueType(_linRegFast[timeframe].Value < _linRegSlow[timeframe].Value);
            break;
          case tkn_linreg_fast_gt_slow:
            result = new ValueType(_linRegFast[timeframe].Value > _linRegSlow[timeframe].Value);
            break;
          case tkn_ssma_fast_lt_slow:
            result = new ValueType(_ssmaFastAvg[timeframe].Value < _ssmaSlowAvg[timeframe].Value);
            break;
          case tkn_ssma_fast_gt_slow:
            result = new ValueType(_ssmaFastAvg[timeframe].Value > _ssmaSlowAvg[timeframe].Value);
            break;
          default:
            throw new Exception("Moving Average factory does not support function with name \"" + wrapper.Name + "\".");
        }
      }

      return result;
    }
  }
  #endregion

  #region Keltner Channel Functions
  /// <summary>
  /// Implements the functions that can be used to perform Keltner Channel analysis on price movement. Implementation uses a linear regression for the midline.
  /// </summary>
  public class KeltnerChannelFunctions : FunctionFactory
  {
    //Constants:
    public const int DefaultLength = 20;
    public const int DefaultATRLength = 20;
    public const double DefaultChannelFactor = 1.5;
    public const int DefaultCrossLength = 5;

    //Factory name to import
    public const string tkn_factory_name = "kc";

    //Set of supported function tokens
    public const string tkn_length = "ktch_length";
    public const string tkn_atr_length = "ktch_atr_length";
    public const string tkn_channel_factor = "ktch_channel_factor";
    public const string tkn_cross_length = "ktch_cross_length";
    public const string tkn_pr_crossover_midline = "ktch_pr_crossover_midline";
    public const string tkn_pr_crossunder_midline = "ktch_pr_crossunder_midline";
    public const string tkn_pr_crossover_upper = "ktch_pr_crossover_upper";
    public const string tkn_pr_crossunder_upper = "ktch_pr_crossunder_upper";
    public const string tkn_pr_crossover_lower = "ktch_pr_crossover_lower";
    public const string tkn_pr_crossunder_lower = "ktch_pr_crossunder_lower";
    public const string tkn_pr_gt_midline = "ktch_pr_gt_midline";
    public const string tkn_pr_lt_midline = "ktch_pr_lt_midline";
    public const string tkn_pr_gt_upper = "ktch_pr_gt_upper";
    public const string tkn_pr_lt_upper = "ktch_pr_lt_upper";
    public const string tkn_pr_gt_lower = "ktch_pr_gt_lower";
    public const string tkn_pr_lt_lower = "ktch_pr_lt_lower";

    //Attributes:
    protected int _length;
    protected int _atrLength;
    protected double _channelFactor;
    protected int _crossLength;
    private List<AverageFC> _centerLineFC;
    private List<VariableSeries<Double>> _centerLine;
    private List<VariableSeries<Double>> _upperBand;
    private List<VariableSeries<Double>> _lowerBand;

    public KeltnerChannelFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      _length = DefaultLength;
      _atrLength = DefaultATRLength;
      _channelFactor = DefaultChannelFactor;
      _crossLength = DefaultCrossLength;

      //add the set of functions supported by the function factory
      _functions.Add(tkn_length);
      _functions.Add(tkn_atr_length);
      _functions.Add(tkn_channel_factor);
      _functions.Add(tkn_cross_length);
      _functions.Add(tkn_pr_crossover_midline);
      _functions.Add(tkn_pr_crossunder_midline);
      _functions.Add(tkn_pr_crossover_upper);
      _functions.Add(tkn_pr_crossunder_upper);
      _functions.Add(tkn_pr_crossover_lower);
      _functions.Add(tkn_pr_crossunder_lower);
      _functions.Add(tkn_pr_gt_midline);
      _functions.Add(tkn_pr_lt_midline);
      _functions.Add(tkn_pr_gt_upper);
      _functions.Add(tkn_pr_lt_upper);
      _functions.Add(tkn_pr_gt_lower);
      _functions.Add(tkn_pr_lt_lower);
    }

    public override void Create()
    {
      base.Create();

      _centerLineFC = new List<AverageFC>(Interpreter.MAX_DATA_STREAM);
      _centerLine = new List<VariableSeries<double>>(Interpreter.MAX_DATA_STREAM);
      _upperBand = new List<VariableSeries<double>>(Interpreter.MAX_DATA_STREAM);
      _lowerBand = new List<VariableSeries<double>>(Interpreter.MAX_DATA_STREAM);

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        _centerLineFC.Add(new AverageFC(_interpreter.Strategy));
        _centerLine.Add(new VariableSeries<double>(_interpreter.Strategy));
        _upperBand.Add(new VariableSeries<double>(_interpreter.Strategy));
        _lowerBand.Add(new VariableSeries<double>(_interpreter.Strategy));
      }
    }

    public override void StartCalc()
    {
      base.StartCalc();

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        IInstrument dataFeed = null;

        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          dataFeed = _interpreter.Strategy.BarsOfData(i + 1);
        else
          dataFeed = _interpreter.Strategy.Bars;

        _centerLineFC[i].price = dataFeed.Close;
        _centerLineFC[i].length = _length;
      }
    }

    public override ValueType Evaluate()
    {

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        double scaledATR = 0.0;

        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          scaledATR = _channelFactor * _interpreter.Strategy.AverageTrueRange(_atrLength, 0, i + 1);
        else
          scaledATR = _channelFactor * _interpreter.Strategy.AverageTrueRange(_atrLength, 0, 1);

        _centerLine[i].Value = _centerLineFC[i][0];
        _upperBand[i].Value = (_centerLine[i].Value + scaledATR);
        _lowerBand[i].Value = (_centerLine[i].Value - scaledATR);
      }

      return new ValueType();
    }

    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        //supported factory functions
        case tkn_length:
        case tkn_atr_length:
        case tkn_channel_factor:
        case tkn_cross_length:
          result = new FunctionProxy(FunctionType.Factory, name, this, parameters);
          break;

        //supported trigger functions
        case tkn_pr_crossover_midline:
        case tkn_pr_crossunder_midline:
        case tkn_pr_crossover_upper:
        case tkn_pr_crossunder_upper:
        case tkn_pr_crossover_lower:
        case tkn_pr_crossunder_lower:
        case tkn_pr_gt_midline:
        case tkn_pr_lt_midline:
        case tkn_pr_gt_upper:
        case tkn_pr_lt_upper:
        case tkn_pr_gt_lower:
        case tkn_pr_lt_lower:
          result = new FunctionProxy(FunctionType.Trigger, name, this, parameters);
          break;

        default:
          throw new Exception("Keltner Channel factory does not support function with name \"" + name + "\".");
      }

      return result;
    }

    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = null;

      if (wrapper.Type == FunctionType.Factory)
      {
        //factory function evaluation does not return anything
        result = new ValueType();

        switch (wrapper.Name)
        {
          case tkn_length:
            _length = wrapper.GetIntegerParameter(0, "Keltner channel length function requires an integer parameter.");
            break;
          case tkn_atr_length:
            _length = wrapper.GetIntegerParameter(0, "Keltner channel average true range length function requires an integer parameter.");
            break;
          case tkn_channel_factor:
            _channelFactor = wrapper.GetDoubleParameter(0, "Keltner channel average true range factor function requires a double or integer parameter.", true);
            break;
          case tkn_cross_length:
            _crossLength = wrapper.GetIntegerParameter(0, "Keltner channel cross length function requires an integer parameter.");
            break;
          default:
            throw new Exception("Keltner Channel factory does not support function with name \"" + wrapper.Name + "\".");
        }
      }
      else
      {
        //get the timeframe we need to evaluate against
        int timeframe = wrapper.GetIntegerParameter(0, 0);
        IInstrument data = _interpreter.Strategy.BarsOfData(timeframe + 1);     //+1 since bars of data is indexed from 1 and not zero

        switch (wrapper.Name)
        {
          case tkn_pr_crossover_midline:
            result = new ValueType(data.Close.CrossesOver(_centerLine[timeframe], _crossLength));
            break;
          case tkn_pr_crossunder_midline:
            result = new ValueType(data.Close.CrossesUnder(_centerLine[timeframe], _crossLength));
            break;
          case tkn_pr_crossover_upper:
            result = new ValueType(data.Close.CrossesOver(_upperBand[timeframe], _crossLength));
            break;
          case tkn_pr_crossunder_upper:
            result = new ValueType(data.Close.CrossesUnder(_upperBand[timeframe], _crossLength));
            break;
          case tkn_pr_crossover_lower:
            result = new ValueType(data.Close.CrossesOver(_lowerBand[timeframe], _crossLength));
            break;
          case tkn_pr_crossunder_lower:
            result = new ValueType(data.Close.CrossesUnder(_lowerBand[timeframe], _crossLength));
            break;
          case tkn_pr_gt_midline:
            result = new ValueType(data.Close[0] > _centerLine[timeframe].Value);
            break;
          case tkn_pr_lt_midline:
            result = new ValueType(data.Close[0] < _centerLine[timeframe].Value);
            break;
          case tkn_pr_gt_upper:
            result = new ValueType(data.Close[0] > _upperBand[timeframe].Value);
            break;
          case tkn_pr_lt_upper:
            result = new ValueType(data.Close[0] < _upperBand[timeframe].Value);
            break;
          case tkn_pr_gt_lower:
            result = new ValueType(data.Close[0] > _lowerBand[timeframe].Value);
            break;
          case tkn_pr_lt_lower:
            result = new ValueType(data.Close[0] < _lowerBand[timeframe].Value);
            break;
          default:
            throw new Exception("Keltner Channel factory does not support function with name \"" + wrapper.Name + "\".");
        }
      }

      return result;
    }
  }
  #endregion

  #region RSI Functions
  /// <summary>
  /// Implements the functions that can be used to perfrom RSI analysis on price data.
  /// </summary>
  public class RSIFunctions : FunctionFactory
  {
    //Constants:
    public const int DefaultOverboughtLevel = 70;
    public const int DefaultOversoldLevel = 30;
    public const int DefaultLength = 14;
    public const int DefaultCrossLength = 5;
    public const int DefaultMomentumLength = 1;

    //Factory name for import
    public const string tkn_factory_name = "rsi";

    //Set of supported function tokens
    public const string tkn_overbought_level = "rsi_overbought_level";
    public const string tkn_oversold_level = "rsi_oversold_level";
    public const string tkn_length = "rsi_length";
    public const string tkn_cross_length = "rsi_cross_length";
    public const string tkn_momentum_length = "rsi_mom_length";
    public const string tkn_value = "rsi_value";
    public const string tkn_momentum = "rsi_mom";
    public const string tkn_momentum_positive = "rsi_mom_pt";
    public const string tkn_momentum_negative = "rsi_mom_nt";
    public const string tkn_gt_overbought = "rsi_gt_overbought";
    public const string tkn_lt_overbought = "rsi_lt_overbought";
    public const string tkn_gt_oversold = "rsi_gt_oversold";
    public const string tkn_lt_oversold = "rsi_lt_oversold";
    public const string tkn_crossover_overbought = "rsi_crossover_overbought";
    public const string tkn_crossunder_overbought = "rsi_crossunder_overbought";
    public const string tkn_crossover_oversold = "rsi_crossover_oversold";
    public const string tkn_crossunder_oversold = "rsi_crossunder_oversold";

    //Attributes:
    protected int _overboughtLevel;
    protected int _oversoldLevel;
    protected int _length;
    protected int _crossLength;
    protected int _momentumLength;
    protected List<RSI> _rsiFC;
    protected List<VariableSeries<double>> _rsi;

    public RSIFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      _overboughtLevel = DefaultOverboughtLevel;
      _oversoldLevel = DefaultOversoldLevel;
      _length = DefaultLength;
      _crossLength = DefaultCrossLength;
      _momentumLength = DefaultMomentumLength;

      //add the set of functions supported by the factory
      _functions.Add(tkn_overbought_level);
      _functions.Add(tkn_oversold_level);
      _functions.Add(tkn_length);
      _functions.Add(tkn_cross_length);
      _functions.Add(tkn_momentum_length);
      _functions.Add(tkn_value);
      _functions.Add(tkn_momentum);
      _functions.Add(tkn_momentum_positive);
      _functions.Add(tkn_momentum_negative);
      _functions.Add(tkn_gt_overbought);
      _functions.Add(tkn_lt_overbought);
      _functions.Add(tkn_gt_oversold);
      _functions.Add(tkn_lt_oversold);
      _functions.Add(tkn_crossover_overbought);
      _functions.Add(tkn_crossunder_overbought);
      _functions.Add(tkn_crossover_oversold);
      _functions.Add(tkn_crossunder_oversold);
    }

    public override void Create()
    {
      base.Create();

      _rsiFC = new List<RSI>(Interpreter.MAX_DATA_STREAM);
      _rsi = new List<VariableSeries<double>>(Interpreter.MAX_DATA_STREAM);

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        _rsiFC.Add(new RSI(_interpreter.Strategy));
        _rsi.Add(new VariableSeries<double>(_interpreter.Strategy));
      }
    }

    public override void StartCalc()
    {
      base.StartCalc();

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        IInstrument dataFeed = null;

        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          dataFeed = _interpreter.Strategy.BarsOfData(i + 1);
        else
          dataFeed = _interpreter.Strategy.Bars;

        _rsiFC[i].price = dataFeed.Close;
        _rsiFC[i].length = _length;
      }
    }

    public override ValueType Evaluate()
    {
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
        _rsi[i].Value = _rsiFC[i][0];
      return new ValueType();
    }


    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        //supported factory functions
        case tkn_overbought_level:
        case tkn_oversold_level:
        case tkn_length:
        case tkn_cross_length:
        case tkn_momentum_length:
          result = new FunctionProxy(FunctionType.Factory, name, this, parameters);
          break;

        //supported trigger functions
        case tkn_value:
        case tkn_momentum:
        case tkn_momentum_positive:
        case tkn_momentum_negative:
        case tkn_gt_overbought:
        case tkn_lt_overbought:
        case tkn_gt_oversold:
        case tkn_lt_oversold:
        case tkn_crossover_overbought:
        case tkn_crossunder_overbought:
        case tkn_crossover_oversold:
        case tkn_crossunder_oversold:
          result = new FunctionProxy(FunctionType.Trigger, name, this, parameters);
          break;

        default:
          throw new Exception("RSI factory does not support function with name \"" + name + "\".");
      }

      return result;
    }


    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = null;

      if (wrapper.Type == FunctionType.Factory)
      {
        //factory functions do not return a value
        result = new ValueType();

        switch (wrapper.Name)
        {
          case tkn_overbought_level:
            _overboughtLevel = wrapper.GetIntegerParameter(0, "RSI overbought function requires an integer parameter.");
            break;
          case tkn_oversold_level:
            _oversoldLevel = wrapper.GetIntegerParameter(0, "RSI oversold function requires an integer parameter.");
            break;
          case tkn_length:
            _length = wrapper.GetIntegerParameter(0, "RSI length function requires an integer parameter.");
            break;
          case tkn_cross_length:
            _crossLength = wrapper.GetIntegerParameter(0, "RSI cross length function requires an integer parameter.");
            break;
          case tkn_momentum_length:
            _momentumLength = wrapper.GetIntegerParameter(0, "RSI momentum length function requires an integer parameter.");
            break;
          default:
            throw new Exception("RSI factory does not support function with name \"" + wrapper.Name + "\".");
        }
      }
      else
      {
        //get the timeframe for functions to evaluate
        int timeframe = wrapper.GetIntegerParameter(0, 0);
        VariableSeries<double> rsi = _rsi[timeframe];

        switch (wrapper.Name)
        {
          case tkn_value:
            result = new ValueType(rsi.Value);
            break;
          case tkn_momentum:
            result = new ValueType(rsi.Momentum(_momentumLength));
            break;
          case tkn_momentum_positive:
            result = new ValueType(0 < _rsi[timeframe].Momentum(_momentumLength));
            break;
          case tkn_momentum_negative:
            result = new ValueType(0 > _rsi[timeframe].Momentum(_momentumLength));
            break;
          case tkn_gt_overbought:
            result = new ValueType(_overboughtLevel < rsi.Value);
            break;
          case tkn_lt_overbought:
            result = new ValueType(_overboughtLevel > rsi.Value);
            break;
          case tkn_gt_oversold:
            result = new ValueType(_oversoldLevel < rsi.Value);
            break;
          case tkn_lt_oversold:
            result = new ValueType(_oversoldLevel > rsi.Value);
            break;
          case tkn_crossover_overbought:
            result = new ValueType(rsi.CrossesOver(_overboughtLevel, _crossLength));
            break;
          case tkn_crossunder_overbought:
            result = new ValueType(rsi.CrossesUnder(_overboughtLevel, _crossLength));
            break;
          case tkn_crossover_oversold:
            result = new ValueType(rsi.CrossesOver(_oversoldLevel, _crossLength));
            break;
          case tkn_crossunder_oversold:
            result = new ValueType(rsi.CrossesUnder(_oversoldLevel, _crossLength));
            break;
          default:
            throw new Exception("RSI factory does not support function with name \"" + wrapper.Name + "\".");
        }
      }

      return result;
    }
  }
  #endregion

  #region MACD Functions
  /// <summary>
  /// Factory to support the MACD functions.
  /// </summary>
  public class MACDFunctions : FunctionFactory
  {
    //Constants:
    public const int DefaultFastEMA = 12;
    public const int DefaultSlowEMA = 26;
    public const int DefaultSignalLineEMA = 9;
    public const int DefaultMomentumLength = 1;
    public const int DefaultCrossLength = 5;

    //Factory name for import
    public const string tkn_factory_name = "macd";

    //Set of supported function tokens
    public const string tkn_fast_ema = "macd_fast_ema";
    public const string tkn_slow_ema = "macd_slow_ema";
    public const string tkn_sl_ema = "macd_sl_ema";
    public const string tkn_mom_length = "macd_mom_length";
    public const string tkn_cross_length = "macd_cross_length";
    public const string tkn_macd = "macd_macd";
    public const string tkn_sl = "macd_sl";
    public const string tkn_hst = "macd_hst";
    public const string tkn_hst_pt = "macd_hst_pt";
    public const string tkn_hst_nt = "macd_hst_nt";
    public const string tkn_macd_mom = "macd_macd_mom";
    public const string tkn_macd_mom_pt = "macd_macd_mom_pt";
    public const string tkn_macd_mom_nt = "macd_macd_mom_nt";
    public const string tkn_sl_mom = "macd_sl_mom";
    public const string tkn_sl_mom_pt = "macd_sl_mom_pt";
    public const string tkn_sl_mom_nt = "macd_sl_mom_nt";
    public const string tkn_hst_mom = "macd_hst_mom";
    public const string tkn_hst_mom_pt = "macd_hst_mom_pt";
    public const string tkn_hst_mom_nt = "macd_hst_mom_nt";
    public const string tkn_macd_crossover_sl = "macd_crossover_sl";
    public const string tkn_macd_crossunder_sl = "macd_crossunder_sl";
    public const string tkn_macd_crossover_zero = "macd_crossover_zero";
    public const string tkn_macd_crossunder_zero = "macd_crossunder_zero";
    public const string tkn_sl_crossover_zero = "macd_sl_crossover_zero";
    public const string tkn_sl_crossunder_zero = "macd_sl_crossunder_zero";

    //Attributes:
    protected int _fastEMA;
    protected int _slowEMA;
    protected int _signalLineEMA;
    protected int _momentumLength;
    protected int _crossLength;
    protected List<MACD> _macdFC;
    protected List<VariableSeries<double>> _macd;
    protected List<VariableSeries<double>> _histogram;
    protected List<XAverage> _signalLineFC;
    protected List<VariableSeries<double>> _signalLine;

    public MACDFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      _fastEMA = DefaultFastEMA;
      _slowEMA = DefaultSlowEMA;
      _signalLineEMA = DefaultSignalLineEMA;
      _momentumLength = DefaultMomentumLength;
      _crossLength = DefaultCrossLength;

      //add the set of functions supported by the factory
      _functions.Add(tkn_fast_ema);
      _functions.Add(tkn_slow_ema);
      _functions.Add(tkn_sl_ema);
      _functions.Add(tkn_mom_length);
      _functions.Add(tkn_cross_length);
      _functions.Add(tkn_macd);
      _functions.Add(tkn_sl);
      _functions.Add(tkn_hst);
      _functions.Add(tkn_hst_pt);
      _functions.Add(tkn_hst_nt);
      _functions.Add(tkn_macd_mom);
      _functions.Add(tkn_macd_mom_pt);
      _functions.Add(tkn_macd_mom_nt);
      _functions.Add(tkn_sl_mom);
      _functions.Add(tkn_sl_mom_pt);
      _functions.Add(tkn_sl_mom_nt);
      _functions.Add(tkn_hst_mom);
      _functions.Add(tkn_hst_mom_pt);
      _functions.Add(tkn_hst_mom_nt);
      _functions.Add(tkn_macd_crossover_sl);
      _functions.Add(tkn_macd_crossunder_sl);
      _functions.Add(tkn_macd_crossover_zero);
      _functions.Add(tkn_macd_crossunder_zero);
      _functions.Add(tkn_sl_crossover_zero);
      _functions.Add(tkn_sl_crossunder_zero);
    }

    public override void Create()
    {
      base.Create();

      _macdFC = new List<MACD>(Interpreter.MAX_DATA_STREAM);
      _macd = new List<VariableSeries<double>>(Interpreter.MAX_DATA_STREAM);
      _histogram = new List<VariableSeries<double>>(Interpreter.MAX_DATA_STREAM);
      _signalLineFC = new List<XAverage>(Interpreter.MAX_DATA_STREAM);
      _signalLine = new List<VariableSeries<double>>(Interpreter.MAX_DATA_STREAM);

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        _macdFC.Add(new MACD(_interpreter.Strategy));
        _macd.Add(new VariableSeries<double>(_interpreter.Strategy));
        _histogram.Add(new VariableSeries<double>(_interpreter.Strategy));
        _signalLineFC.Add(new XAverage(_interpreter.Strategy));
        _signalLine.Add(new VariableSeries<double>(_interpreter.Strategy));
      }
    }

    public override void StartCalc()
    {
      base.StartCalc();

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        IInstrument dataFeed = null;

        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          dataFeed = _interpreter.Strategy.BarsOfData(i + 1);
        else
          dataFeed = _interpreter.Strategy.Bars;

        _macdFC[i].Price = dataFeed.Close;
        _macdFC[i].FastLength = _fastEMA;
        _macdFC[i].SlowLength = _slowEMA;
        _signalLineFC[i].Price = _macd[i];
        _signalLineFC[i].Length = _signalLineEMA;
        _macd[i].DefaultValue = 0;
        _histogram[i].DefaultValue = 0;
      }
    }

    public override ValueType Evaluate()
    {
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        _macd[i].Value = _macdFC[i][0];
        _signalLine[i].Value = _signalLineFC[i][0];
        _histogram[i].Value = _macd[i].Value - _signalLine[i].Value;
      }

      return new ValueType();
    }

    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        //supported factory functions
        case tkn_fast_ema:
        case tkn_slow_ema:
        case tkn_sl_ema:
        case tkn_mom_length:
        case tkn_cross_length:
          result = new FunctionProxy(FunctionType.Factory, name, this, parameters);
          break;

        //supported trigger functions
        case tkn_macd:
        case tkn_sl:
        case tkn_hst:
        case tkn_hst_pt:
        case tkn_hst_nt:
        case tkn_macd_mom:
        case tkn_macd_mom_pt:
        case tkn_macd_mom_nt:
        case tkn_sl_mom:
        case tkn_sl_mom_pt:
        case tkn_sl_mom_nt:
        case tkn_hst_mom:
        case tkn_hst_mom_pt:
        case tkn_hst_mom_nt:
        case tkn_macd_crossover_sl:
        case tkn_macd_crossunder_sl:
        case tkn_macd_crossover_zero:
        case tkn_macd_crossunder_zero:
        case tkn_sl_crossover_zero:
        case tkn_sl_crossunder_zero:
          result = new FunctionProxy(FunctionType.Trigger, name, this, parameters);
          break;

        default:
          throw new Exception("MACD function \"" + name + "\" is not supported.");
      }

      return result;
    }

    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = null;

      if (wrapper.Type == FunctionType.Factory)
      {
        //factory functions to not return a result
        result = new ValueType();

        switch (wrapper.Name)
        {
          case tkn_fast_ema:
            _fastEMA = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          case tkn_slow_ema:
            _slowEMA = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          case tkn_sl_ema:
            _signalLineEMA = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          case tkn_mom_length:
            _momentumLength = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          case tkn_cross_length:
            _crossLength = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          default:
            throw new Exception("MACD function \"" + wrapper.Name + "\" is not supported.");
        }
      }
      else
      {
        //get the timeframe to evaluate against, we default to the first timeframe
        int timeframe = wrapper.GetIntegerParameter(0, 0);

        switch (wrapper.Name)
        {
          case tkn_macd:
            result = new ValueType(_macd[timeframe].Value);
            break;
          case tkn_sl:
            result = new ValueType(_signalLine[timeframe].Value);
            break;
          case tkn_hst:
            result = new ValueType(_histogram[timeframe].Value);
            break;
          case tkn_hst_pt:
            result = new ValueType(0 < _histogram[timeframe].Value);
            break;
          case tkn_hst_nt:
            result = new ValueType(0 > _histogram[timeframe].Value);
            break;
          case tkn_macd_mom:
            result = new ValueType(_macd[timeframe].Momentum(_momentumLength));
            break;
          case tkn_macd_mom_pt:
            result = new ValueType(0 < _macd[timeframe].Momentum(_momentumLength));
            break;
          case tkn_macd_mom_nt:
            result = new ValueType(0 > _macd[timeframe].Momentum(_momentumLength));
            break;
          case tkn_sl_mom:
            result = new ValueType(_signalLine[timeframe].Momentum(_momentumLength));
            break;
          case tkn_sl_mom_pt:
            result = new ValueType(0 < _signalLine[timeframe].Momentum(_momentumLength));
            break;
          case tkn_sl_mom_nt:
            result = new ValueType(0 > _signalLine[timeframe].Momentum(_momentumLength));
            break;
          case tkn_hst_mom:
            result = new ValueType(_histogram[timeframe].Momentum(_momentumLength));
            break;
          case tkn_hst_mom_pt:
            result = new ValueType(0 < _histogram[timeframe].Momentum(_momentumLength));
            break;
          case tkn_hst_mom_nt:
            result = new ValueType(0 > _histogram[timeframe].Momentum(_momentumLength));
            break;
          case tkn_macd_crossover_sl:
            result = new ValueType(_macd[timeframe].CrossesOver(_signalLine[timeframe], _crossLength));
            break;
          case tkn_macd_crossunder_sl:
            result = new ValueType(_macd[timeframe].CrossesUnder(_signalLine[timeframe], _crossLength));
            break;
          case tkn_macd_crossover_zero:
            result = new ValueType(_macd[timeframe].CrossesOver(0, _crossLength));
            break;
          case tkn_macd_crossunder_zero:
            result = new ValueType(_macd[timeframe].CrossesUnder(0, _crossLength));
            break;
          case tkn_sl_crossover_zero:
            result = new ValueType(_signalLine[timeframe].CrossesOver(0, _crossLength));
            break;
          case tkn_sl_crossunder_zero:
            result = new ValueType(_signalLine[timeframe].CrossesUnder(0, _crossLength));
            break;
          default:
            throw new Exception("MACD function \"" + wrapper.Name + "\" is not supported.");
        }
      }

      return result;
    }
  }
  #endregion

  #region ADX Functions
  /// <summary>
  /// Factory to support the Average Directional Index and Average Directional Index Rating functions.
  /// See https://www.investopedia.com/articles/trading/07/adx-trend-indicator.asp  and http://forex-indicators.net/adx and
  /// http://forex-indicators.net/trend-indicators/adxr for more information.
  /// NOTE: This factory supports both ADX and ADXR functions.
  /// </summary>
  public class ADXFunctions : FunctionFactory
  {
    //Constants:
    public const int DefaultCrossLength = 10;
    public const int DefaultADXLength = 14;
    public const int DefaultADXTrendLevel = 20;
    public const int DefaultADXOverboughtLevel = 40;
    public const int DefaultADXRLength = 14;
    public const int DefaultADXRTrendLevel = 25;
    public const int DefaultADXROscillatorLevel = 20;

    //Factory name for import:
    public const string tkn_factory_name = "adx";

    //Set of supported function tokens
    public const string tkn_adx_cross_length = "adx_cross_length";
    public const string tkn_adx_length = "adx_length";
    public const string tkn_adx_trend_level = "adx_trend_level";
    public const string tkn_adx_overbought_level = "adx_overbought_level";
    public const string tkn_adxr_trend_level = "adxr_trend_level";
    public const string tkn_adxr_oscillator_level = "adxr_oscillator_level";
    public const string tkn_adx = "adx_adx";
    public const string tkn_adxr = "adx_adxr";
    public const string tkn_adx_dmi_plus = "adx_dmi_plus";
    public const string tkn_adx_dmi_minus = "adx_dmi_minus";
    public const string tkn_adx_trending = "adx_trending";
    public const string tkn_adx_overbought = "adx_overbought";
    public const string tkn_adx_dmi_crossover = "adx_dmi_crossover";
    public const string tkn_adx_dmi_crossunder = "adx_dmi_crossunder";
    public const string tkn_adxr_trending = "adxr_trending";
    public const string tkn_adxr_oscillating = "adxr_oscillating";

    //Attributes:
    protected int _crossLength;
    protected int _adxLength;
    protected int _adxrLength;
    protected int _adxTrendLevel;
    protected int _adxOverboughtLevel;
    protected int _adxrTrendLevel;
    protected int _adxrOscillatorLevel;
    protected List<DirMovement> _dirMovementFC;

    public ADXFunctions(Interpreter interpreter) : base(tkn_factory_name, interpreter)
    {
      //set factory parameter defaults
      _crossLength = DefaultCrossLength;
      _adxLength = DefaultADXLength;
      _adxTrendLevel = DefaultADXTrendLevel;
      _adxOverboughtLevel = DefaultADXOverboughtLevel;
      _adxrTrendLevel = DefaultADXRTrendLevel;
      _adxrOscillatorLevel = DefaultADXROscillatorLevel;

      //add the set of functions supported by the factory
      _functions.Add(tkn_adx_cross_length);
      _functions.Add(tkn_adx_length);
      _functions.Add(tkn_adx_trend_level);
      _functions.Add(tkn_adx_overbought_level);
      _functions.Add(tkn_adxr_trend_level);
      _functions.Add(tkn_adxr_oscillator_level);
      _functions.Add(tkn_adx);
      _functions.Add(tkn_adxr);
      _functions.Add(tkn_adx_dmi_plus);
      _functions.Add(tkn_adx_dmi_minus);
      _functions.Add(tkn_adx_trending);
      _functions.Add(tkn_adx_overbought);
      _functions.Add(tkn_adx_dmi_crossover);
      _functions.Add(tkn_adx_dmi_crossunder);
      _functions.Add(tkn_adxr_trending);
      _functions.Add(tkn_adxr_oscillating);
    }

    public override void Create()
    {
      base.Create();

      _dirMovementFC = new List<DirMovement>(Interpreter.MAX_DATA_STREAM);

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        _dirMovementFC.Add(new DirMovement(_interpreter.Strategy));
      }
    }

    public override void StartCalc()
    {
      base.StartCalc();

      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        IInstrument dataFeed = null;
        if (i + 1 <= _interpreter.Strategy.DataStreamsUsed)
          dataFeed = _interpreter.Strategy.BarsOfData(i + 1);
        else
          dataFeed = _interpreter.Strategy.Bars;

        _dirMovementFC[i].PriceH = dataFeed.High;
        _dirMovementFC[i].PriceL = dataFeed.Low;
        _dirMovementFC[i].PriceC = dataFeed.Close;
        _dirMovementFC[i].Length = _adxLength;
      }
    }

    public override ValueType Evaluate()
    {
      for (int i = 0; i < Interpreter.MAX_DATA_STREAM; i++)
      {
        _dirMovementFC[i].Call();
      }

      return new ValueType();
    }

    public override Function CreateFunction(string name, List<Parameter> parameters)
    {
      Function result = null;

      switch (name)
      {
        //supported factory functions
        case tkn_adx_cross_length:
        case tkn_adx_length:
        case tkn_adx_trend_level:
        case tkn_adx_overbought_level:
        case tkn_adxr_trend_level:
        case tkn_adxr_oscillator_level:
          result = new FunctionProxy(FunctionType.Factory, name, this, parameters);
          break;

        //supported trigger functions
        case tkn_adx:
        case tkn_adxr:
        case tkn_adx_dmi_plus:
        case tkn_adx_dmi_minus:
        case tkn_adx_trending:
        case tkn_adx_overbought:
        case tkn_adx_dmi_crossover:
        case tkn_adx_dmi_crossunder:
        case tkn_adxr_trending:
        case tkn_adxr_oscillating:
          result = new FunctionProxy(FunctionType.Trigger, name, this, parameters);
          break;

        default:
          throw new Exception("ADX function \"" + name + "\" is not supported.");
      }

      return result;
    }

    public override ValueType Evaluate(FunctionProxy wrapper)
    {
      ValueType result = null;

      if (wrapper.Type == FunctionType.Factory)
      {
        //factory functions do not return a result
        result = new ValueType();

        switch (wrapper.Name)
        {
          case tkn_adx_cross_length:
            _crossLength = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          case tkn_adx_length:
            _adxLength = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          case tkn_adx_trend_level:
            _adxTrendLevel = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          case tkn_adx_overbought_level:
            _adxOverboughtLevel = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          case tkn_adxr_trend_level:
            _adxrTrendLevel = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          case tkn_adxr_oscillator_level:
            _adxrOscillatorLevel = wrapper.GetIntegerParameter(0, string.Format("Function \"{0}\" requires an integer parameter.", wrapper.Name));
            break;
          default:
            throw new Exception("ADX function \"" + wrapper.Name + "\" is not supported.");
        }
      }
      else
      {
        //get the timeframe to evaluate against, we default to the first timeframe
        int timeframe = wrapper.GetIntegerParameter(0, 0);

        switch (wrapper.Name)
        {
          case tkn_adx:
            result = new ValueType(_dirMovementFC[timeframe].ADX.Value);
            break;
          case tkn_adxr:
            result = new ValueType(_dirMovementFC[timeframe].ADXR.Value);
            break;
          case tkn_adx_dmi_plus:
            result = new ValueType(_dirMovementFC[timeframe].DMIPlus.Value);
            break;
          case tkn_adx_dmi_minus:
            result = new ValueType(_dirMovementFC[timeframe].DMIMinus.Value);
            break;
          case tkn_adx_trending:
            result = new ValueType(_dirMovementFC[timeframe].ADX.Value > _adxTrendLevel && _dirMovementFC[timeframe].ADX.Value < _adxOverboughtLevel);
            break;
          case tkn_adx_overbought:
            result = new ValueType(_dirMovementFC[timeframe].ADX.Value > _adxOverboughtLevel);
            break;
          case tkn_adx_dmi_crossover:
            result = new ValueType(_dirMovementFC[timeframe].DMIPlus.CrossesOver(_dirMovementFC[timeframe].DMIMinus, _crossLength));
            break;
          case tkn_adx_dmi_crossunder:
            result = new ValueType(_dirMovementFC[timeframe].DMIPlus.CrossesUnder(_dirMovementFC[timeframe].DMIMinus, _crossLength));
            break;
          case tkn_adxr_trending:
            result = new ValueType(_dirMovementFC[timeframe].ADXR.Value > _adxrTrendLevel);
            break;
          case tkn_adxr_oscillating:
            result = new ValueType(_dirMovementFC[timeframe].ADXR.Value < _adxrOscillatorLevel);
            break;
          default:
            throw new Exception("ADX function \"" + wrapper.Name + "\" is not supported.");
        }
      }

      return result;
    }
  }
  #endregion



  //ENHANCEMENTS: The following additional factories could be added:
  // * Order book analysis, not sure how this would work but it might merit further investigation.

  //Support/Resistance
  // TODO: Add a module for stochastics.
  // TBD: Determine a good way to look at support resistance, dochian channels might be it but there might be better ways.


  // Dochian Channels
  // * Factory: Length for upper level to check
  // * Factory: Length for lower level to check
  // * Price cross over upper
  // * Price cross under upper
  // * Price cross over lower
  // * Price cross under lower



}