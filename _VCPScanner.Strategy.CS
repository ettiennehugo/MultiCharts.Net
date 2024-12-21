using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	public class _VCPScanner : SignalObject {
		public _VCPScanner(object _ctx):base(_ctx){}
		private IOrderMarket buy_order;
		protected override void Create() {
			// create variable objects, function objects, order objects etc.
			buy_order = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Buy));
		}
		protected override void StartCalc() {
			// assign inputs 
		}
		protected override void CalcBar(){
			// strategy logic 
			buy_order.Send();
		}
	}
}