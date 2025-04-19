using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using PriceAction;

namespace PowerLanguage.Strategy {
	public class _ParabolicShortSE : SignalObject {
		//constants


		//types


		//properties
		[Input] public int FractalLayers { get; set; } //number of fractal layers to use for the price action analysis

		//attribute
		private PriceActionAnalysis m_priceAction;
		private IOrderMarket m_se;

		//interface methods
		public _ParabolicShortSE(object _ctx):base(_ctx){}

		protected override void Create() {
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.SellShort));
		}

		protected override void StartCalc() {
			m_priceAction = new PriceActionAnalysis(FractalLayers);
		}

		protected override void CalcBar(){
			m_se.Send();
		}

		//methods


	}
}