using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Determines whether a stock is in a bearish overextension with a gradual decline in price, we don't want to see massive
		/// volatility in the price that would make it hard to determine where the price is going.
		/// Notes:
		/// * This indicator would need to be run most likely based on industry group or sector to work effectively since the notion of
		///   bearish overextension would differ between sectors, e.g. percentage down and number of down days etc.
		/// </summary>
		public sealed class _BearishOverextension : FunctionSimple<System.Boolean>
		{
			//constants
			public const double DEFAULT_MINIMUM_DROP_PERCENT = -0.25; //25% drop required
			public const int DEFAULT_DROP_DAYS = 60;								  //six months drop
			public const int DEFAULT_ATR_LENGTH = 14;                 //14-day ATR
			public const double DEFAULT_MINIMUM_OVEREXTENSION_FACTOR = 5.0d; //factor of the ATR to determine overextension
			public const int DEFAULT_GRADUAL_DECLINE_LENGTH = 40;     //scan length to check gradual decline of the price without much consolidation
			public const int DEFAULT_FAST_SMOOTHER_LENGTH = 10;       //smoothing length for price move
			public const int DEFAULT_SLOW_SMOOTHER_LENGTH = 100;      //smoothing length to measure gradual decline against

			//inputs
			public IInstrument Instrument { get; set; }      //larger timeframe instrument bar data, e.g. daily, to determine pullback state
			public double MinimumDropPercent { get; set; }   //minimum required drop in the past x-days
			public int DropDays { get; set; }                //number of days to look back for the drop
			public int ATRLength { get; set; }               //length of the ATR to determine the overextension
			public double MinimumOverextensionFactor { get; set; } //minimum factor of the ATR to determine overextension
			public int FastSmoothingLength { get; set; }     //length of smoother to determine more current price move
			public int SlowSmoothingLength { get; set; }     //length of smoother to determine longer term price move
			public int GradualDeclineLength { get; set; }    //length of scan to check for gradual decline

			//properties


			//attributes
			private Function._SuperSmoother m_smootherFast;
			private Function._SuperSmoother m_smootherSlow;
			private Function._ATR m_atr;

			//interface methods
			public _BearishOverextension(CStudyControl _master) : base(_master) { }
			public _BearishOverextension(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				MinimumDropPercent = DEFAULT_MINIMUM_DROP_PERCENT;
				DropDays = DEFAULT_DROP_DAYS;
				ATRLength = DEFAULT_ATR_LENGTH;
				MinimumOverextensionFactor = DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
				FastSmoothingLength = DEFAULT_FAST_SMOOTHER_LENGTH;
				SlowSmoothingLength = DEFAULT_SLOW_SMOOTHER_LENGTH;
				GradualDeclineLength = DEFAULT_GRADUAL_DECLINE_LENGTH;
				m_atr = new Function._ATR(this);
				m_smootherFast = new Function._SuperSmoother(this);
				m_smootherSlow = new Function._SuperSmoother(this);
			}

			protected override void StartCalc()
			{
				if (MinimumDropPercent >= 0.0d) throw new System.ArgumentException("MinimumDropPercent must be negative.");
				if (DropDays <= 0) throw new System.ArgumentException("DropDays must be greater than zero.");
				if (ATRLength <= 0) throw new System.ArgumentException("ATRLength must be greater than zero.");
				if (MinimumOverextensionFactor <= 0.0d) throw new System.ArgumentException("MinimumOverextensionFactor must be greater than zero.");
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
				if (Instrument.CurrentBar <= DropDays || Instrument.CurrentBar <= ATRLength || Instrument.CurrentBar <= SlowSmoothingLength) return false; //not enough data
				if (Instrument.Close[0] / Instrument.High.Highest(DropDays) > 1.0d + MinimumDropPercent) return false;  //not enough drop in price from the high																																																 				
				double delta = m_smootherSlow[0] - Instrument.Low[0];
				if (delta > 0.0d && m_atr[0] * MinimumOverextensionFactor > delta) return false; //no bearish overextension away from the slow average price

				//check that the smoother fast never crosses over the slow smoother length over the last gradual decline length
				for (int i = 0; i < GradualDeclineLength - 1; i++)
					if (m_smootherFast[i] > m_smootherSlow[i] || Instrument.Close[i] > m_smootherSlow[i]) return false;

				return true;
			}

			//methods


		}
	}
}