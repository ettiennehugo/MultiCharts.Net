using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Episodic pivot (gap up on earnings) strategy, it typically looks for stocks that have gapped up on earnings (or anything) and enters on good momentum on the opening bar.
	/// https://www.youtube.com/watch?v=YyNOhw2OLuI
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
	///		* Stock must have a gap up day on the open after earnings.
	///		* Optionally - stock must be breaking out of a base and must not be overly extended.
	///		* Waits for a high volume opening range bar to form on the entry data feed.
	///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _EpisodicPivot : PortfolioSignalObject {
		//constants


		//inputs
		[Input] double MinimumGrowthPercent { get; set; }    //minimum required growth in the past x-days
		[Input] int GrowthDays { get; set; }          //number of days to look back for the growth


		//TBD: add more inputs



		//properties


		//attributes
		private IOrderMarket buy_order;

		//interface methods
		public _EpisodicPivot(object _ctx) : base(_ctx) { }


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