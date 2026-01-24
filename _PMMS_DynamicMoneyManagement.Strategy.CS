using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PowerLanguage.Indicator;
using System.Windows.Forms.VisualStyles;
using PowerLanguage.Strategy.implementation;
namespace PowerLanguage.Strategy
{
  public abstract class _PMMS_SignalObject : SignalObject {
    //signal type used for portfolio strategies
    public enum ESignalType {
      Entry,
      Exit,
      EntryExit,
    };

    //properties
    public ESignalType SignalType { get { return _signalType; } }
    public double RiskAmount { 
      get { return _riskAmount; } 
      set {
        if (value < 0) throw new Exception("RiskAmount can not be less than 0.");
        _riskAmount = value;
      }
    }

    //attribute
    private ESignalType _signalType;
    private double _riskAmount;

    //methods
    public _PMMS_SignalObject(object _ctx, ESignalType signalType) : base(_ctx) 
    {
      _signalType = signalType;
      _riskAmount = 0;
    }
  }

  public class _PMMS_DynamicMoneyManagement : PortfolioSignalObject
  {
    public const double DEFAULT_ACCOUNT_EQUITY_TO_USE = 1.0;
    public const double DEFAULT_ACCOUNT_EQUITY_PER_POSITION = 0.1;
    public const int DEFAULT_MINIMUM_POSITION_SIZE = 1;
    public const int DEFAULT_MAXIMUM_POSITION_SIZE = 10000;
    public const int DEFAULT_DELTA_POSITION_SIZE = 100;
    public const double DEFAULT_PROFIT_DELTA = 1000.0;

    public enum EDynamicMMStrategy {
      FixedFractional = 0,
      AllCapital,
    }

    [Input] public EDynamicMMStrategy DynamicMMStrategy { get; set; } 
    [Input] public double AccountEquityToUse { get; set; }
    [Input] public double AccountEquityPerPosition { get; set; }
    [Input] public int MinimumPosSize { get; set; }
    [Input] public int MaximumPosSize { get; set; }
    [Input] public int DeltaPosSize { get; set; }
    [Input] public double ProfitDelta { get; set; }

    [Input] public bool Debug { get; set; }

    public _PMMS_DynamicMoneyManagement(object _ctx) : base(_ctx) { }

    protected override void Create()
    {
      DynamicMMStrategy = EDynamicMMStrategy.FixedFractional;
      AccountEquityToUse = DEFAULT_ACCOUNT_EQUITY_TO_USE;
      AccountEquityPerPosition = DEFAULT_ACCOUNT_EQUITY_PER_POSITION;
      MinimumPosSize = DEFAULT_MINIMUM_POSITION_SIZE;
      MaximumPosSize = DEFAULT_MAXIMUM_POSITION_SIZE;
      DeltaPosSize = DEFAULT_DELTA_POSITION_SIZE;
      ProfitDelta = DEFAULT_PROFIT_DELTA;
    }
    protected override void StartCalc()
    {
      //require portfolio trader for money management signal
      if (Environment.ApplicationCode != EApplicationCode.Portfolio)
        ExecControl.Abort("Signal can be applied (as money management) in portfolio only.");

      //validate inputs
      if (AccountEquityToUse > 1.0) ExecControl.Abort("Can not use more than 100% account equity, configure margin as percentage of contract.");
      if (AccountEquityPerPosition > 1.0) ExecControl.Abort("Can not use more than 100% account equity per position.");

      if (Debug) Output.Clear();
    }
    protected override void CalcBar()
    {
      switch (DynamicMMStrategy) {
        case EDynamicMMStrategy.FixedFractional:
          fixedFractional();
          break;
        case EDynamicMMStrategy.AllCapital:
          allCapital();
          break;
        default:
          allCapital();
          break;
      }
    }

    protected void fixedFractional() {
      for (int i = 0; i < PortfolioStrategies.Count; i++)
      {
        IPortfolioStrategy strategy = PortfolioStrategies[i];

        double netProfit = 0;
        for (int j = 0; j < strategy.Signals.Length; j++)
          netProfit += strategy.Signals[j].GrossProfit;

        //TODO:
        // * Adjust size of entry based on the capital percentage assigned to the strategy.
        // * Maybe check to implement a number of different sizing strategies and add that as a parameter to the PMMS strategy.

        if (Debug) Output.WriteLine("Risk capital: {0}", strategy.RiskCapital);
        netProfit = strategy.RiskCapital;

        //netProfit = (netProfit > 0) ? netProfit : 0;    //net profit can not be negative for square root calculation below
        
        int sizeFactor = (int)(1 * Math.Floor(0.5 * (1 + Math.Sqrt(1 + (8 * (netProfit / ProfitDelta))))));
        strategy.EntryContracts = MinimumPosSize + DeltaPosSize * (sizeFactor - 1);
        strategy.EntryContracts = strategy.EntryContracts > MaximumPosSize ? MaximumPosSize : strategy.EntryContracts;
      }
    }

    protected void allCapital() {
      double equityToUse = this.PortfolioEquity() * AccountEquityToUse * AccountEquityPerPosition;

      for (int i = 0; i < PortfolioStrategies.Count; i++) {
        IPortfolioStrategy strategy = PortfolioStrategies[i];

        double riskAmount = 0;
        foreach (IStrategy signal in strategy.Signals) {
          _PMMS_SignalObject pmmsSignal = (_PMMS_SignalObject)signal;
          riskAmount += pmmsSignal.RiskAmount;
        }

        strategy.EntryContracts = 0;
        if (riskAmount > 0) strategy.EntryContracts = (int)Math.Truncate(equityToUse / riskAmount);

        //strategy.EntryContracts = this.CalcContractsForEntry(AccountEquityToUse * AccountEquityPerPosition * 100.0, i);
        //if (Debug) Output.WriteLine("Entry: {0}, {1}, {2}", strategy.Signals.);

        if (Debug) Output.WriteLine("Risk capital {0} - Index({1}), PortfolioEquity({2}), RiskAmount({3}), EntryContracts({4})", Bars.Time[0], i, equityToUse, riskAmount, strategy.EntryContracts);
      }
    }
  }
}