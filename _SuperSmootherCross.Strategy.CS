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
  public class _SuperSmootherCross : _CustomSignal
  {
    //Constants:
    public const int DEFAULT_FAST_PERIOD = 40;
    public const int DEFAULT_SLOW_PERIOD = 120;
    private const int CROSS_LOOKBACK = 1;
		public const double DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT = 0.01d;

		//Properties:
		[Input] public int FastPeriod { get; set; }
    [Input] public int SlowPeriod { get; set; }
		[Input] public bool RiskBasedPositionSizing { get; set; }
		[Input] public double MaxRiskEquityPerPosPercent { get; set; }
		[Input] public bool EnableLong { get; set; }
    [Input] public bool EnableShort { get; set; }

    //Attributes:
    private PowerLanguage.Function._SuperSmoother m_fastSuperSmoother;
    private PowerLanguage.Function._SuperSmoother m_slowSuperSmoother;
    private VariableSeries<double> m_fast;
    private VariableSeries<double> m_slow;
    private IOrderMarket m_le;
    private IOrderMarket m_se;
    private IOrderMarket m_lx;
    private IOrderMarket m_sx;
		private double m_stopLossPercent;

		public _SuperSmootherCross(object _ctx) : base(_ctx)
    {
      //initialize additional inputs for strategy
      FastPeriod = DEFAULT_FAST_PERIOD;
      SlowPeriod = DEFAULT_SLOW_PERIOD;
			RiskBasedPositionSizing = false;
			MaxRiskEquityPerPosPercent = DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT;
			EnableLong = true;
      EnableShort = true;
    }

    protected override void Create()
    {		
      //create indicators used
      m_fastSuperSmoother = new PowerLanguage.Function._SuperSmoother(this);
      m_slowSuperSmoother = new PowerLanguage.Function._SuperSmoother(this);
      m_fast = new VariableSeries<double>(this);
      m_slow = new VariableSeries<double>(this);

      //create the order types used
      m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "LE", EOrderAction.Buy));
      m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SE", EOrderAction.SellShort));
      m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LX", EOrderAction.Sell));
      m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc()
    {
      //initialize indicators and objects used
      m_fastSuperSmoother.Length = FastPeriod;
      m_fastSuperSmoother.Price = Bars.Close;
      m_slowSuperSmoother.Length = SlowPeriod;
      m_slowSuperSmoother.Price = Bars.Close;
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
        m_fast.Value = m_fastSuperSmoother[0];
        m_slow.Value = m_slowSuperSmoother[0];
      }

      //process the entry/exit logic
      calcBarEntry();
      calcBarExit();
    }

    private void calcBarEntry()
    {
			//determine whether we should send an entry order

			//TODO - calculating the stop-loss off the delta of the fast/slow SuperSmoother results in HUGE positions for the risk based entries.

			if (EnableLong && m_fast.CrossesOver(m_slow, CROSS_LOOKBACK))
      {
		    double ssDelta = m_fastSuperSmoother[0] - m_slowSuperSmoother[0];
				int size = RiskBasedPositionSizing ? positionSize(Bars, Bars.Close[0] - ssDelta, MaxRiskEquityPerPosPercent) : positionSize(Bars, MaxRiskEquityPerPosPercent);
				m_le.Send(size);
			}
			else if (EnableShort && m_fast.CrossesUnder(m_slow, CROSS_LOOKBACK))
      {
		    double ssDelta = m_fastSuperSmoother[0] - m_slowSuperSmoother[0];
				int size = RiskBasedPositionSizing ? positionSize(Bars, Bars.Close[0] - ssDelta, MaxRiskEquityPerPosPercent) : positionSize(Bars, MaxRiskEquityPerPosPercent);
				m_se.Send(size);
			}
		}

    private void calcBarExit()
    {
		if (PositionSide > 0 && m_fast.CrossesUnder(m_slow, CROSS_LOOKBACK))
			m_lx.Send();
		if (PositionSide < 0 && m_fast.CrossesOver(m_slow, CROSS_LOOKBACK))
			m_sx.Send();
	}
}
}
