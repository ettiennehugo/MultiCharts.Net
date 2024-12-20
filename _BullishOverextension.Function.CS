using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Determines whether a stock is in a bullish overextension with a gradual incline in price, we don't want to see massive
		/// volatility in the price that would make it hard to determine where the price is going.
		/// Notes:
		/// * This indicator would need to be run most likely based on industry group or sector to work effectively since the notion of
		///   bearish overextension would differ between sectors, e.g. percentage down and number of down days etc.
		/// </summary>
		public sealed class _BullishOverextension : FunctionSimple<System.Boolean>
		{
			//constants
			public const double DEFAULT_MINIMUM_RISE_PERCENT = 0.3; //30% increase required
			public const int DEFAULT_RISE_DAYS = 60;            //six months increase
			public const int DEFAULT_ATR_LENGTH = 14;               //14-day ATR
			public const double DEFAULT_MINIMUM_OVEREXTENSION_FACTOR = 5.0d; //factor of the ATR to determine overextension
			public const int DEFAULT_GRADUAL_INCLINE_LENGTH = 20;     //scan length to check gradual incline of the price without much consolidation (needs to be shorter than what is used for bearish overextension)
			public const int DEFAULT_FAST_SMOOTHER_LENGTH = 10;       //smoothing length for price move
			public const int DEFAULT_SLOW_SMOOTHER_LENGTH = 100;      //smoothing length to measure gradual decline against

			//inputs
			public IInstrument Instrument { get; set; }      //larger timeframe instrument bar data, e.g. daily, to determine pullback state
			public double MinimumRisePercent { get; set; }   //minimum required increase in the past x-days
			public int RiseDays { get; set; }                //number of days to look back for the drop
			public int ATRLength { get; set; }               //length of the ATR to determine the overextension
			public double MinimumOverextensionFactor { get; set; } //minimum factor of the ATR to determine overextension
			public int FastSmoothingLength { get; set; }     //length of smoother to determine more current price move
			public int SlowSmoothingLength { get; set; }     //length of smoother to determine longer term price move
			public int GradualInclineLength { get; set; }    //length of scan to check for gradual decline

			//properties


			//attributes
			private Function._SuperSmoother m_smootherFast;
			private Function._SuperSmoother m_smootherSlow;
			private Function._ATR m_atr;

			//interface methods
			public _BullishOverextension(CStudyControl _master) : base(_master) { }
			public _BullishOverextension(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				MinimumRisePercent = DEFAULT_MINIMUM_RISE_PERCENT;
				RiseDays = DEFAULT_RISE_DAYS;
				ATRLength = DEFAULT_ATR_LENGTH;
				MinimumOverextensionFactor = DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
				FastSmoothingLength = DEFAULT_FAST_SMOOTHER_LENGTH;
				SlowSmoothingLength = DEFAULT_SLOW_SMOOTHER_LENGTH;
				GradualInclineLength = DEFAULT_GRADUAL_INCLINE_LENGTH;
				m_atr = new Function._ATR(this);
				m_smootherFast = new Function._SuperSmoother(this);
				m_smootherSlow = new Function._SuperSmoother(this);
			}

			protected override void StartCalc()
			{
				if (MinimumRisePercent < 0) throw new System.ArgumentException("MinimumRisePercent must be greater than 0", "MinimumRisePercent");
				if (RiseDays <= 0) throw new System.ArgumentException("RiseDays must be greater than zero", "RiseDays");
				if (ATRLength <= 0) throw new System.ArgumentException("ATRLength must be greater than zero", "ATRLength");
				if (MinimumOverextensionFactor <= 0) throw new System.ArgumentException("MinimumOverextensionFactor must be greater than zero", "MinimumOverextensionFactor");
				if (FastSmoothingLength <= 0) throw new System.ArgumentException("FastSmoothingLength must be greater than zero", "FastSmoothingLength");
				if (SlowSmoothingLength <= 0) throw new System.ArgumentException("SlowSmoothingLength must be greater than zero", "SlowSmoothingLength");
				m_atr.Instrument = Instrument;
				m_atr.Length = ATRLength;
				m_smootherFast.Price = Instrument.Close;
				m_smootherFast.Length = FastSmoothingLength;
				m_smootherSlow.Price = Instrument.Close;
				m_smootherSlow.Length = SlowSmoothingLength;
			}

			protected override System.Boolean CalcBar()
			{
				if (Environment.CalcReason != CalculationReason.Default) return false; //only calculate on bar close default reason
				if (Instrument.CurrentBar <= RiseDays || Instrument.CurrentBar <= ATRLength || Instrument.CurrentBar <= SlowSmoothingLength) return false; //not enough data
				double lowest = Instrument.Low.Lowest(RiseDays);
				if (lowest == 0.0d || Instrument.Close[0] / lowest < 1.0d + MinimumRisePercent) return false;  //not enough rise in price from the high																																																 				
				double delta = Instrument.High[0] - m_smootherSlow[0];
				if (delta > 0.0d && m_atr[0] * MinimumOverextensionFactor > delta) return false; //no bullish overextension away from the slow average price

				//check that the smoother fast never crosses over the slow smoother length over the last gradual incline length
				for (int i = 0; i < GradualInclineLength - 1; i++)
					if (m_smootherFast[i] < m_smootherSlow[i] || Instrument.Close[i] < m_smootherSlow[i]) return false;

				return true;
			}

			//methods


		}
	}
}