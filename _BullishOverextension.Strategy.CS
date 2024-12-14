using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Counter trend strategy that looks for a bullish overextension in the price, typically a stock that has been rising for a while
	/// and is becoming overextended in the price. The strategy will look for an entry on a high volume opening range bar as selling enters
	/// the market for take profit.
	///	Required Feeds:
	///		1. Entry data feed - typically some minute data feed.
	///		2. Scanner data feed - typically daily data feed for the same instrument.
	///	Entry:
	///	  * Stock must have shown some minimum incline in the past, typically 30 to 100% rise in the past 6 months.
	///	  * Stock is gradually inclining in price without much consolidation over a specified period.
	///	  * Stock price is becoming overextended to the upside, typically 3x the ATR or more.
	///	  * IF the opening bar is a high volume bearish bar an entry is placed at the low/close of the opening bar for the breakout entry with risk based on the opening bar range.
	/// </summary>
	public class _BullishOverextension : _OpeningRangeBarSignal
	{
		//constants


		//inputs
		[Input] public double MinimumRisePercent { get; set; }
		[Input] public int RiseDays { get; set; }
		[Input] public int ATRLength { get; set; }
		[Input] public double MinimumOverextensionFactor { get; set; }
		[Input] public int FastSmoothingLength { get; set; }
		[Input] public int SlowSmoothingLength { get; set; }
		[Input] public int GradualInclineLength { get; set; }

		//properties


		//attributes
		private PowerLanguage.Function._BullishOverextension m_bullishOverextension;

		//interface methods
		public _BullishOverextension(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			MinimumRisePercent = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_RISE_PERCENT;
			RiseDays = PowerLanguage.Function._BullishOverextension.DEFAULT_RISE_DAYS;
			ATRLength = PowerLanguage.Function._BullishOverextension.DEFAULT_ATR_LENGTH;
			MinimumOverextensionFactor = PowerLanguage.Function._BullishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			FastSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			SlowSmoothingLength = PowerLanguage.Function._BullishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			GradualInclineLength = PowerLanguage.Function._BullishOverextension.DEFAULT_GRADUAL_INCLINE_LENGTH;
			m_bullishOverextension = new PowerLanguage.Function._BullishOverextension(this);
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_bullishOverextension.Instrument = BarsOfData(2);
			m_bullishOverextension.MinimumRisePercent = MinimumRisePercent;
			m_bullishOverextension.RiseDays = RiseDays;
			m_bullishOverextension.ATRLength = ATRLength;
			m_bullishOverextension.MinimumOverextensionFactor = MinimumOverextensionFactor;
			m_bullishOverextension.FastSmoothingLength = FastSmoothingLength;
			m_bullishOverextension.SlowSmoothingLength = SlowSmoothingLength;
			m_bullishOverextension.GradualInclineLength = GradualInclineLength;
		}

		protected override void CalcBar()
		{
			if (!m_bullishOverextension[0]) return;
			base.CalcBar();
		}

		//methods



	}
}