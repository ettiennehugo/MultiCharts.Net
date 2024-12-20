using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using RiskManagement;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// 
  /// </summary>
  public class _Test5 : SignalObject
  {
    //types
    private enum TradeStage
    {
      Entry,
      FirstExit,
      SecondExit
    }

    //constants
    public const int DEFAULT_SS_LENGTH = 50;
    public const int DEFAULT_ER_LENGTH = 50;
    public const int DEFAULT_BREAKOUT_LENGTH = 50;
    public const double DEFAULT_PERCENT_GROWTH = 1.3;
    public const int DEFAULT_PERCENT_GROWTH_LENGTH = 125;
    public const int DEFAULT_EXIT_DAYS = 5;
    public const double DEFAULT_RISK_PERCENT = 0.01;

    //inputs
    [Input] public int SuperSmootherLength { get; set; }
    [Input] public int EfficiencyRatioLength { get; set; }
    [Input] public int BreakoutLength { get; set; }
    [Input] public int PercentGrowthLength { get; set; }
    [Input] public double PercentGrowth { get; set; }
    [Input] public double RiskPercentage { get; set; }
    [Input] public int ExitDays { get; set; }
    [Input] public bool EnableLong { get; set; }
    [Input] public bool EnableShort { get; set; }

    //attributes
    private PowerLanguage.Function._SuperSmoother m_superSmoother;
    private _EfficiencyRatio m_efficiencyRatio;
    private VolumeWithSD m_volumeWithSD;
    private IOrderMarket m_le;
    private IOrderMarket m_se;
    private IOrderMarket m_lx;
    private IOrderMarket m_sx;
    private double m_stopLoss;
    private TradeStage m_tradeStage;

    public _Test5(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      //set defaults for inputs
      SuperSmootherLength = DEFAULT_SS_LENGTH;
      EfficiencyRatioLength = DEFAULT_ER_LENGTH;
      BreakoutLength = DEFAULT_BREAKOUT_LENGTH;
      PercentGrowthLength = DEFAULT_PERCENT_GROWTH_LENGTH;
      PercentGrowth = DEFAULT_PERCENT_GROWTH;
      RiskPercentage = DEFAULT_RISK_PERCENT;
      ExitDays = DEFAULT_EXIT_DAYS;
      m_tradeStage = TradeStage.Entry;
      EnableLong = true;
      EnableShort = true;

      //create indicators used
      m_superSmoother = new PowerLanguage.Function._SuperSmoother(this);
      m_efficiencyRatio = new _EfficiencyRatio(this);
      m_volumeWithSD = new VolumeWithSD(this);

      //create the order types used
      m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LE", EOrderAction.Buy));
      m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SE", EOrderAction.SellShort));
      m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LX", EOrderAction.Sell));
      m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SX", EOrderAction.BuyToCover));
    }

    protected override void StartCalc()
    {
      m_superSmoother.Length = SuperSmootherLength;
      m_efficiencyRatio.Length = EfficiencyRatioLength;
      m_volumeWithSD.Length = BreakoutLength;
      m_volumeWithSD.Volume = Bars.Volume;
    }

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

    protected void CalcBarDefault()
    {
      if (PositionSide == 0)
        CalcBarEntry();
      else
        CalcBarExit();
    }

    protected void CalcBarEntry()
    {
      if (EnableLong &&
          Bars.Close[0] > (Bars.Close[PercentGrowthLength] * PercentGrowth) &&
          Bars.Close[0] > Bars.Close.Highest(BreakoutLength, 1) &&
          Bars.Volume[0] > m_volumeWithSD.AvgPlusLevel2[0])
      {
        double m_stopLoss = Bars.Close.Lowest(10, 1);
        double risk = 0.01 * Bars.Close[0] - m_stopLoss;    //1% risk over initial stop-loss position
        int size = (int)Math.Round(this.StrategyInfo.OpenEquity / risk, 0);
        m_le.Send(size);
      }
    }

    protected void CalcBarExit()
    {
      //Exit rules:
      // - Close third of position at 3-5 days after entry and move stop-loss to break even.
      // - Close position after break of super smoother.
      switch (m_tradeStage)
      {
        case TradeStage.Entry:
          m_tradeStage = TradeStage.FirstExit;
          break;
        case TradeStage.FirstExit:
          if (PositionSide != 0 && (this.EntryTime(0) - Bars.Time[0]).Days > ExitDays)
          {
            int size = (int)Math.Abs(Math.Round((double)this.StrategyInfo.MarketPosition / 3, 0));
            m_lx.Send(size);
          }
          break;
        case TradeStage.SecondExit:
          if (PositionSide > 0 && m_superSmoother.CrossesUnder(m_superSmoother[0], 1)) m_lx.Send(PositionSide);
          break;
      }
    }

    protected void CalcBarOrderFilled()
    {
      switch (m_tradeStage)
      {
        case TradeStage.Entry:
          m_tradeStage = TradeStage.FirstExit;
          break;
        case TradeStage.FirstExit:
          m_tradeStage = TradeStage.SecondExit;
          break;
        case TradeStage.SecondExit:
          m_tradeStage = TradeStage.Entry;
          break;
      }
    }

  }
}
