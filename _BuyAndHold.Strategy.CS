using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	public class _BuyAndHold : SignalObject {
		public _BuyAndHold(object _ctx):base(_ctx){}
		private IOrderMarket _LE;
		private IOrderMarket _LX;

		protected override void Create() {
			_LE = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
			_LX = OrderCreator.MarketThisBar(new SOrderParameters(Contracts.Default, EOrderAction.Sell));
		}
		protected override void StartCalc() {

		}
		protected override void CalcBar(){
			if (Bars.CurrentBar == 1) _LE.Send((int)Math.Floor(InitialCapital / Bars.Close[0]));
			if (Bars.LastBarOnChart) _LX.Send();
		}
	}
}