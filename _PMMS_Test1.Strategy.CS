using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {

	public class _PMMS_Test1 : PortfolioSignalObject {
    //constants
    public const double DEFAULT_ACCOUNT_EQUITY_TO_USE = 1.0;
    public const double DEFAULT_ACCOUNT_EQUITY_PER_POSITION = 0.1;
    public const int DEFAULT_MINIMUM_POSITION_SIZE = 1;
    public const int DEFAULT_MAXIMUM_POSITION_SIZE = 10000;

    //inputs
    [Input] public double AccountEquityToUse { get; set; }
    [Input] public double AccountEquityPerPosition { get; set; }
    [Input] public int MinimumPosSize { get; set; }
    [Input] public int MaximumPosSize { get; set; }

    //attributes
    public _PMMS_Test1(object _ctx):base(_ctx){}

	protected override void Create() {
      AccountEquityToUse = DEFAULT_ACCOUNT_EQUITY_TO_USE;
      AccountEquityPerPosition = DEFAULT_ACCOUNT_EQUITY_PER_POSITION;
      MinimumPosSize = DEFAULT_MINIMUM_POSITION_SIZE;
      MaximumPosSize = DEFAULT_MAXIMUM_POSITION_SIZE;
    }

	protected override void CalcBar(){
      for (int i = 0; i < PortfolioStrategies.Count; i++) {
        IPortfolioStrategy strategy = PortfolioStrategies[i];
        IStrategy signal = strategy.Signals[0];


        //TODO: This does not necessarily take into account current positions already in a strategy or other strategies.


        double equityPerPosition = this.PortfolioEquity() * AccountEquityToUse * AccountEquityPerPosition;
        strategy.EntryContracts = (int)Math.Truncate(equityPerPosition / signal.Bars.Close[0]);

        strategy.EntryContracts = strategy.EntryContracts < MinimumPosSize ? MinimumPosSize : strategy.EntryContracts;
        strategy.EntryContracts = strategy.EntryContracts > MaximumPosSize ? MaximumPosSize : strategy.EntryContracts;
      }
    }
	}
}