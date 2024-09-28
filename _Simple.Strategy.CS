using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
  
  /// TODO: This strategy is WAY too simple, it only looks at one symbol's data to determine what to do. Test is "as-is" but if it does not work check
  ///       whether you can add additional symbols like comparing the symbol against the general market for strength.
  /// 
  ///Description:
  /// Based on the book "The Simple EntryExit" by Markus Heitkoetter. Signal uses the MACD, Bollinger Band and 
  /// RSI indicators to determine a confluence between these indicators to determine entry/exit.
  ///
  ///Entry Rules:
  ///Long: p.15
  /// 1. MACD is above the signal line and the zero line.
  /// 2. Bollinger Band must indicate an uptrend with upper band pointing upwards.
  /// 3. [Optional] RSI should rise above overbought.
  /// 4. Price breaks above the high of the bar where these criteria are met.
  ///
  ///Short: p.28
  /// 1. MACD is below the signal line and the zero line.
  /// 2. Bollinger Band must indicate a downtrend with lower band pointing down.
  /// 3. [Optional] RSI should rise below oversold limit.
  /// 4. Price breaks below the low of the bar where these criteria are met.
  ///
  ///Exit Rules:
  /// 1. 
  ///
  ///

  public class _Simple : SignalObject
  {

    [Input] public int MACDLength { get; set; }
    [Input] public int MACDFastLength { get; set; }
    [Input] public int MACDSlowLength { get; set; }
    [Input] public int BBLength { get; set; }
    [Input] public double BBStdDevs { get; set; }
    [Input] public int RSILength { get; set; }
    [Input] public int RSIOverbought { get; set; }
    [Input] public int RSIOversold { get; set; }

    public _Simple(object _ctx) : base(_ctx)
    {
      MACDLength = 9;
      MACDFastLength = 12;
      MACDSlowLength = 26;
      BBLength = 20;
      BBStdDevs = 2.0;
      RSILength = 20;
      RSIOverbought = 70;
      RSIOversold = 30;
    }
    private IOrderMarket buy_order;
    protected override void Create()
    {
      // create variable objects, function objects, order objects etc.
      buy_order = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Buy));
    }
    protected override void StartCalc()
    {
      // assign inputs 
    }
    protected override void CalcBar()
    {
      // strategy logic 
      buy_order.Send();
    }
  }
}