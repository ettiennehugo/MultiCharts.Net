using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;
using RiskManagement;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Chart Type:
  /// 
  /// Hypothesis:
  /// 
  /// 
  /// 
  /// Ideal Instruments and Timeframes:
  /// 
  /// 
  /// 
  /// Indicators: 
  /// 
  /// 
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
  /// Notes:
  /// 	
  /// 
  /// </summary>
  public class _VWAPCrossover : SignalObject
  {
    //Inputs:

    //TODO: Define additional inputs for the strategy.
    [Input] public int FastLength { get; set; }
    [Input] public int SlowLength { get; set; }
    [Input] public double StopLossPct { get; set; }
    [Input] public double TrailingStopPct { get; set; }
    [Input] public bool EnableLong { get; set; }
    [Input] public bool EnableShort { get; set; }
    [Input] public bool Debug { get; set; }                     //flag to enable debug output to the output console

    //Constants:
    const int CROSS_LOOKBACK = 5;
    const int MINI_LOT_SIZE = 1000;                             //mini-lot size used in Forex

    //Attributes:
    private VWAP _fastVWAP;
    private VWAP _slowVWAP;
    private IOrderMarket _LE;
    private IOrderMarket _LX;
    private IOrderMarket _SE;
    private IOrderMarket _SX;
    protected double _PipValue;

    public _VWAPCrossover(object _ctx) : base(_ctx)
    {
      //initialize additional inputs for strategy
      FastLength = 21;
      SlowLength = 55;
      StopLossPct = 1.0;                //1% stop loss per default
      TrailingStopPct = 2.0;            //2% trailing stop per default
      EnableLong = true;
      EnableShort = true;

      //initialize default inputs for strategy
      Debug = false;
    }

    protected override void Create()
    {
      //create indicators used
      _fastVWAP = new VWAP(this);
      _slowVWAP = new VWAP(this);

      //create the order types used
      _LE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "LE", EOrderAction.Buy));
      _LX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "LX", EOrderAction.Sell));
      _SE = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "SE", EOrderAction.SellShort));
      _SX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, "SX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc()
    {
      //clear the debugging console
      if (Debug)
        Output.Clear();

      //initialize indicators and objects used
      _fastVWAP.Length = FastLength;
      _fastVWAP.Price = Bars.Close;
      _fastVWAP.Volume = Bars.Volume;
      _slowVWAP.Length = SlowLength;
      _slowVWAP.Price = Bars.Close;
      _slowVWAP.Volume = Bars.Volume;

      //standarize the buffer pip value for the price scale we're trading
      _PipValue = Bars.Info.PointValue * 10 * Bars.Info.BigPointValue;
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
      }
    }

    private void CalcBarDefault()
    {
      //process the entry/exit logic
      if (PositionSide == 0)
        calcBarEntry();
      else
        calcBarExit();
    }

    private void calcBarEntry()
    {
      if (EnableLong && _fastVWAP.CrossesOver(_slowVWAP, CROSS_LOOKBACK))
        _LE.Send();

      if (EnableShort && _fastVWAP.CrossesUnder(_slowVWAP, CROSS_LOOKBACK))
        _SE.Send();

    }

    private void calcBarExit()
    {
      //stop-loss/trailing-stop exit
      CurSpecOrdersMode = ESpecOrdersMode.PerContract;
      if (StopLossPct != 0)
        GenerateStopLoss(this.EntryPrice() * (StopLossPct / 100));
      if (TrailingStopPct != 0)
        GeneratePercentTrailing(this.EntryPrice() * (TrailingStopPct / 100), TrailingStopPct);
		
      if (PositionSide > 0 && _fastVWAP.CrossesUnder(_slowVWAP, CROSS_LOOKBACK)) {
        _SE.Send();
      }
      else if (PositionSide < 0 && _fastVWAP.CrossesOver(_slowVWAP, CROSS_LOOKBACK)) {
        _LE.Send();
      }
    }
  }
}
