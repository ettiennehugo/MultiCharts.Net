using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Cup and handle pattern strategy.
	///	Required Feeds:
	///		1. Entry data feed - typically some minute data feed.
	///		2. Scanner data feed - typically daily data feed for the same instrument.
	/// </summary>
	public class _CupAndHandle : _TimeRangeSignal
	{
		//constants


		//inputs
		[Input] public int Length { get; set; } //scan length for the cup-and-handle pattern

		//properties


		//attributes
		private IOrderMarket buy_order;

		//interface methods
		public _CupAndHandle(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			// create variable objects, function objects, order objects etc.
			buy_order = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Buy));
		}

		protected override void StartCalc()
		{
			// assign inputs 
		}

		protected override void CalcBar()
		{
			// strategy logic 
			buy_order.Send();
		}

		//methods



	}
}