using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Based off the narrow range day breakout strategy by Toby Crable - https://www.youtube.com/watch?v=Hym5yqy1QGY
	/// A X-bar narrow range is defined as the narrowest X-day range relative to any X-day range within the previous Y-market days, typical values for X is
	/// 2, 4 and 7 and the default for Y is 20.
	/// Requires two data feeds, the entry data feed based off the minute timeframe since it uses an opening bar range to determine the risk and a secondary data
	/// feed on the daily timeframe used to determine the narrow range days.
	/// Entry:
	///   * Stock must have shown some minimal momentum in the past, typically 30 to 100% up in the past 6 months.
	///		* Current X-bar lookback we have a range tightening and it is the smallest range in the last narraw range period.
	///		* Waits for a high volume opening range bar to form on the entry data feed.
	///		* IF the opening bar is a high volume bullish bar an entry is placed at the high of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _CrableNRX : PortfolioSignalObject 
	{
		//constants


		//inputs
		[Input] double MinimumGrowthPercent { get; set; }    //minimum required growth in the past x-days
		[Input] int GrowthDays { get; set; }								 //number of days to look back for the growth
		[Input] public int NarrowRangeDays { get; set; }     //X-days is the narrowest X-day range relative to any X-day range within the previous narrow range period-market days
		[Input] public int NarrowRangePeriod { get; set; }   //period to scan for narrow range days

		//properties


		//attributes
		private IOrderMarket buy_order;

		//interface methods
		public _CrableNRX(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			NarrowRangeDays = 4;
			NarrowRangePeriod = 20;
			buy_order = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Buy));
		}

		protected override void StartCalc()
		{
			// assign inputs 
		}

		protected override void CalcBar()
		{

			//TODO - develop narrow range bar FUNCTION.
			
			
			// strategy logic 
			buy_order.Send();
		}

		//methods



	}
}