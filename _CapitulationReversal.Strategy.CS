using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Buys the pullback on the strongest stocks (reversal trade) in the market when the market is overextended to the down side.
	/// https://www.youtube.com/watch?v=WY_5BV3BmkA
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
	///		* Market is currently down x-percent from the high in the past y-days and the stock is also down at least x-percent from the high in the past y-days.
	///		* Optionally - wait for an x-percent gap down on the open - 0 switch it off.
	///		* Waits for the opening range bar to form on the entry data feed.
	///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _CapitulationReversal : PortfolioSignalObject
	{
		//constants


		//inputs
		[Input] double MinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
		[Input] int GrowthDays { get; set; }                //number of days to look back for the growth

		//TBD: add more inputs



		//properties


		//attributes
		private IOrderMarket buy_order;

		//interface methods
		public _CapitulationReversal(object _ctx) : base(_ctx) { }


		//methods
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
	}
}