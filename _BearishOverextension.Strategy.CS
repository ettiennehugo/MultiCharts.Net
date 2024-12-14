using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Counter trend strategy that looks for a bearish overextension in the price, typically a stock that has been going down for a while
	/// and is becoming overextended in the price. The strategy will look for an entry on a high volume opening range bar as buying enters
	/// the market to cover short positions.
	///	Required Feeds:
	///		1. Entry data feed - typically some minute data feed.
	///		2. Scanner data feed - typically daily data feed for the same instrument.
	///	Entry:
	///	  * Stock must have shown some minimum decline in the past, typically 30 to 100% down in the past 6 months.
	///	  * Stock is gradually declining in price without much consolidation over a specified period.
	///	  * Stock price is becoming overextended to the downside, typically 3x the ATR or more.
	///	  * IF the opening bar is a high volume bullish bar an entry is placed at the high/close of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _BearishOverextension : _OpeningRangeBarSignal
	{
		//constants


		//inputs
		[Input] public double MinimumDropPercent { get; set; }
		[Input] public int DropDays { get; set; }
		[Input] public int ATRLength { get; set; }
		[Input] public double MinimumOverextensionFactor { get; set; }
		[Input] public int FastSmoothingLength { get; set; }
		[Input] public int SlowSmoothingLength { get; set; }
		[Input] public int GradualDeclineLength { get; set; }

		//properties


		//attributes
		private PowerLanguage.Function._BearishOverextension m_bearishOverextension;

		//interface methods
		public _BearishOverextension(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			MinimumDropPercent = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_DROP_PERCENT;
			DropDays = PowerLanguage.Function._BearishOverextension.DEFAULT_DROP_DAYS;
			ATRLength = PowerLanguage.Function._BearishOverextension.DEFAULT_ATR_LENGTH;
			MinimumOverextensionFactor = PowerLanguage.Function._BearishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			FastSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			SlowSmoothingLength = PowerLanguage.Function._BearishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			GradualDeclineLength = PowerLanguage.Function._BearishOverextension.DEFAULT_GRADUAL_DECLINE_LENGTH;
			m_bearishOverextension = new PowerLanguage.Function._BearishOverextension(this);
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_bearishOverextension.Instrument = BarsOfData(2);
			m_bearishOverextension.MinimumDropPercent = MinimumDropPercent;
			m_bearishOverextension.DropDays = DropDays;
			m_bearishOverextension.ATRLength = ATRLength;
			m_bearishOverextension.MinimumOverextensionFactor = MinimumOverextensionFactor;
			m_bearishOverextension.FastSmoothingLength = FastSmoothingLength;
			m_bearishOverextension.SlowSmoothingLength = SlowSmoothingLength;
			m_bearishOverextension.GradualDeclineLength = GradualDeclineLength;
		}

		protected override void CalcBar()
		{
			if (!m_bearishOverextension[0]) return;
			base.CalcBar();
		}

		//methods


	}
}