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
	/// Chart Type: Candle
  /// 
  /// Hypothesis: Trend following strategy using the super smoother to filter out price noise.
  /// 
  /// Ideal Instruments and Timeframes:
  /// Instruments that trend such as currencies, interest rates and certain commodities such as oil using the dialy timeframe.
  ///
  /// Indicators: 
  /// Super smoother 
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
  /// 
  /// 
  /// 
  /// </summary>
  public class _SuperSmootherCross : SignalObject
  {
    //Constants:
    public const int DEFAULT_FAST_PERIOD = 40;
    public const int DEFAULT_SLOW_PERIOD = 120;
    private const int CROSS_LOOKBACK = 1;

    //Properties:
    [Input] public int FastPeriod { get; set; }
    [Input] public int SlowPeriod { get; set; }
    [Input] public bool EnableLong { get; set; }
    [Input] public bool EnableShort { get; set; }

    //Attributes:
    private SuperSmoother _FastSuperSmoother;
    private SuperSmoother _SlowSuperSmoother;
    private VariableSeries<double> _Fast;
    private VariableSeries<double> _Slow;
    private IOrderMarket _LE;
    private IOrderMarket _SE;
    private IOrderMarket _LX;
    private IOrderMarket _SX;

    public _SuperSmootherCross(object _ctx) : base(_ctx)
    {
      //initialize additional inputs for strategy
      FastPeriod = DEFAULT_FAST_PERIOD;
      SlowPeriod = DEFAULT_SLOW_PERIOD;
      EnableLong = true;
      EnableShort = true;
    }

    protected override void Create()
    {		
      //create indicators used
      _FastSuperSmoother = new SuperSmoother(this);
      _SlowSuperSmoother = new SuperSmoother(this);
      _Fast = new VariableSeries<double>(this);
      _Slow = new VariableSeries<double>(this);

      //create the order types used
      _LE = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LE", EOrderAction.Buy));
      _SE = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SE", EOrderAction.SellShort));
      _LX = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LX", EOrderAction.Sell));
      _SX = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc()
    {
      //initialize indicators and objects used
      _FastSuperSmoother.Period = FastPeriod;
      _FastSuperSmoother.Price = Bars.Close;
      _SlowSuperSmoother.Period = SlowPeriod;
      _SlowSuperSmoother.Price = Bars.Close;
    }

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
      //update series indicators if this is the bar close
      if (Bars.Status == EBarState.Close)
      {
        _Fast.Value = _FastSuperSmoother[0];
        _Slow.Value = _SlowSuperSmoother[0];
      }

      //process the entry/exit logic
      calcBarEntry();
      calcBarExit();
    }

    private void calcBarEntry()
    {
      //determine whether we should send an entry order
      if (EnableLong && _Fast.CrossesOver(_Slow, CROSS_LOOKBACK))
        _LE.Send(positionSize());
      else if (EnableShort && _Fast.CrossesUnder(_Slow, CROSS_LOOKBACK))
        _SE.Send(positionSize());
    }

    private void calcBarExit()
    {
      //send standard exit orders
      if (PositionSide > 0 && _Fast.CrossesUnder(_Slow, CROSS_LOOKBACK))
        _LX.Send();

      if (PositionSide < 0 && _Fast.CrossesOver(_Slow, CROSS_LOOKBACK))
        _SX.Send();
    }

		private int positionSize()
		{
			double equityAvailable = 0.0d;
			int result = 0;
			if (Bars.Close[0] == 0.0d) return result;

			//NOTE: This only works for single entries, need to see how this will work for multiple entries.
			switch (Environment.ApplicationCode)
			{
				case EApplicationCode.Charting:
					equityAvailable = this.InitialCapital + this.NetProfit;
					break;
				case EApplicationCode.Portfolio:
					equityAvailable = (this.InitialCapital + this.Portfolio.NetProfit) * (this.Portfolio.MaxRiskEquityPerPosPercent / 100.0d);
					break;
			}

			if (equityAvailable > 0) result = (int)Math.Truncate(equityAvailable / Bars.Close[0]);

			return result;
		}
	}
}
