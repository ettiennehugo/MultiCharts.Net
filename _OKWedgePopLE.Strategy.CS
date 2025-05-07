using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	
	//Oliver Kell wedge pop long entry.
	
	public class _OKWedgePopLE : _CustomSignal {
		//constants

		
		//types


		//properties


		//attribute
		private IOrderMarket m_le;

		//interface methods
		public _OKWedgePopLE(object _ctx):base(_ctx){}

		protected override void Create() {
			// create variable objects, function objects, order objects etc.
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Buy));
		}

		protected override void StartCalc() {
			// assign inputs 
		}

		protected override void CalcBar(){
			// strategy logic 
			m_le.Send();
		}

		//methods


	}
}