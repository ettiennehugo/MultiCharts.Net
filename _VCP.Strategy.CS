using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Volatility contraction pattern strategy the price action is contracting into narrower bar ranges and then breaks out of that range.
	/// This is typically the pattern traded by Mark Minervini.
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
	///		* Stock must be contracting into a narrower range in the past x-days.
	///		* Waits for a high volume opening range bar to form on the entry data feed.
	///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range
	/// </summary>
	public class _VCP : PortfolioSignalObject {
		//constants


		//inputs
		[Input] double MinimumGrowthPercent { get; set; }    //minimum required growth in the past x-days
		[Input] int GrowthDays { get; set; }          //number of days to look back for the growth

		//properties


		//attributes
		private IOrderMarket buy_order;

		//interface methods
		public _VCP(object _ctx) : base(_ctx) { }


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
			
			// TODO - develop a VCP detection FUNCTION.
			
			// strategy logic 
			buy_order.Send();
		}
	}
}