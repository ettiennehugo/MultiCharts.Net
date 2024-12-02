using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {

	public class _PMMS_Test1 : _CustomSignal {

	//constants

		
    //inputs
	[Input] public int BuyBestX {get; set; }
			
    //attributes
	private Dictionary<int, double> m_bestStrategies = new Dictionary<int, double>();

	//interface methods
    public _PMMS_Test1(object _ctx):base(_ctx){}

	protected override void Create() {
		BuyBestX = 10;
		m_bestStrategies = new Dictionary<int, double>();
	}

	protected override void CalcBar(){
	   this.StrategiesDenyEntriesAll();

	   m_bestStrategies.Clear();
	   for (int i = 0; i < PortfolioStrategies.Count; i++) {
        IPortfolioStrategy strategy = PortfolioStrategies[i];
		strategy.AllowExits = true;
		strategy.AllowExitsLong = true;
		strategy.AllowExitsShort = true;
        IStrategy signal = strategy.Signals[0];
        double value = strategy.PortfolioData["RotationalValue"].safe_cast2double();
		m_bestStrategies.Add(i, value);
      }
	
	  var inLongStrategies = new List<int>();
	  this.StrategiesInLong(ref inLongStrategies);
	
	  m_bestStrategies = m_bestStrategies.OrderByDescending(elem => elem.Value).ToDictionary(x => x.Key, y => y.Value);
  	  for (int i = 0; i < BuyBestX; i++)
		  if (!inLongStrategies.Contains(m_bestStrategies.Keys.ElementAt(i)))
		{
          IPortfolioStrategy strategy = PortfolioStrategies[i];
		  strategy.AllowEntries = true;
		  strategy.AllowEntriesLong = true;
		}
    }
  }
}