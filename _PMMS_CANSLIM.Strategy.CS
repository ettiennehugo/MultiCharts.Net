using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {

	public class _PMMS_CANSLIM : PortfolioSignalObject
  {
		//Constants
		public const string KEY_RISK_PERCENTAGE_PER_POSITION = "RiskPercentagePerPosition";
		public const string KEY_RISK_AMOUNT_PER_POSITION = "RiskAmountPerPosition";
		public const double DEFAULT_RISK_PER_POSITION = 0.05;

		//Inputs
		[Input] public double RiskPerPosition { get; set; } 
		[Input] public bool Debug { get; set; }

		public _PMMS_CANSLIM(object _ctx):base(_ctx) {}

		protected override void Create() {
			RiskPerPosition = DEFAULT_RISK_PER_POSITION;
			Debug = false;
		}

		protected override void StartCalc() {
      //set the portfolio keys for the strategies
      for (int i = 0; i < PortfolioStrategies.Count; i++)
      {
        IPortfolioStrategy strategy = PortfolioStrategies[i];
        strategy.PortfolioData[KEY_RISK_PERCENTAGE_PER_POSITION] = RiskPerPosition;
        strategy.PortfolioData[KEY_RISK_AMOUNT_PER_POSITION] = this.PortfolioEquity() * RiskPerPosition;
      }
    }

    protected override void CalcBar(){
      //TBD: Not sure whether this will give all the capital or just capital available.

      //TODO: Test position sizing with smaller set of stocks and see whether you can get a good portfolio going.

//      double equity = this.PortfolioEquity();
//
//      for (int i = 0; i < PortfolioStrategies.Count; i++)
//      {
//        IPortfolioStrategy strategy = PortfolioStrategies[i];
//        strategy.EntryContracts = 0;
//        _CANSLIM canslimSignal = (_CANSLIM)strategy.Signals[0];
//
//        if (canslimSignal.RiskPerContract > 0)
//        {
//          strategy.EntryContracts = (int)Math.Floor((equity * RiskPerPosition) / canslimSignal.RiskPerContract);
//        }
//      }
    }
  }
}