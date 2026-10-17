using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// https://www.youtube.com/watch?v=uDvT4uDWgB4
	/// Timeframes:
	///   * 10-minute bars with VP over daily sessions (span = 138 bars for full futures day)
	///   * [Optional] Additional datastreams for confluence for things like VWAP, Price Action, Fibonacci
	/// Entry:
	/// Rules:
	///   1. Daily profile is fully formed (entry will be next day so this day is described as previous day)
	///   2. Price moves away from POC - builds an HVN next day ABOVE the previous day POC
	///   3. Pullback to the previous day POC
	///   4. When price hits POC from above go long
	///   5. Trade only the first test of the previous day POC
	/// Extensions/Improvements:
	///   6. Look for confluence with other factors like VWAP/price action/Fibonacci levels
	///
	/// Exit:
	///   1. Initially just exit with same take profit as stop-loss 
	///   2. Take profit/stop loss must not be more than 10-20% of average daily range.
	/// </summary>
	public class _POCPullbackLELX : _VolumeProfileFuturesBase
	{
		//constants


		//types


		//properties


		//attribute


		//interface methods
		public _POCPullbackLELX(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
		}

		protected override void StartCalc()
		{
			base.StartCalc();

		}

		protected override void CalcBar()
		{
			base.CalcBar();
		}

		//methods


	}
}