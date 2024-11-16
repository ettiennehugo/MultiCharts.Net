using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// 
	/// </summary>
	public class _AutocorrelationReversals : SignalObject {

		//constants


		//inputs


		//properties


		//attributes
		private IOrderMarket m_le;
		private IOrderMarket m_lx;
		private IOrderMarket m_se;
		private IOrderMarket m_sx;
		
		//interface methods
		public _AutocorrelationReversals(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			// create variable objects, function objects, order objects etc.
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Sell));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.SellShort));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.BuyToCover));
		}

		protected override void StartCalc()
		{
			// assign inputs 
		}

		protected override void CalcBar()
		{
			// strategy logic 
			m_le.Send();
		}

		//methods



	}
}