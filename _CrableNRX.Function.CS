using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Determines whether the current bar can be considered the last bar of an X-bar narrow range bars. E.g. for a length of
		/// 7 and scan length of 20 the function will return true if the last set of 7-bars could be considered the narrowest range.
		/// The bar open/close does not have to fall within the range of the previous bar but the high/low range must be narrowing.
		/// Notes:
		/// * The NR4 is based on a length of 3 bars and the NR7 is based on a length of 6 bars being the narrowest the 4th and 7th
		///   bars would be used to look for the breakout.
		/// </summary>
		public sealed class _CrableNRX : FunctionSimple<System.Boolean>
		{
			//constants
			public const int DEFAULT_LENGTH = 7;
			public const int DEFAULT_SCAN_LENGTH = 20;
			public const double DEFAULT_MINIMUM_ATR = 0.2;
			public const int DEFAULT_ATR_LENGTH = 20;

			//inputs
			public IInstrument Instrument { get; set; } //instrument price feed to use for the scan		
			public int Length { get; set; }       //required length of the narrow range bars
			public int ScanLength { get; set; }   //scan length for the narrow range bars
			public double MinimumATR { get; set; } //minimum ATR value required to make sure we don't get instruments that are not moving produce tight bars

			//properties


			//attributes
			private Function._ATR m_atr;

			//interface methods
			public _CrableNRX(CStudyControl _master) : base(_master) { }
			public _CrableNRX(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = DEFAULT_LENGTH;
				ScanLength = DEFAULT_SCAN_LENGTH;
				m_atr = new _ATR(this);
			}

			protected override void StartCalc() 
			{
				if (Length <= 0) throw new Exception("Length must be a positive number");
				if (ScanLength <= 0) throw new Exception("ScanLength must be a positive number");
				if (Length > ScanLength) throw new Exception("Length must be less than or equal to ScanLength");
				if (MinimumATR <= 0) throw new Exception("MinimumATR needs to be a positive number");
				m_atr.Instrument = Instrument;
				m_atr.Length = DEFAULT_ATR_LENGTH;
			}

			protected override System.Boolean CalcBar()
			{
				if (Instrument.CurrentBar < ScanLength || Instrument.CurrentBar < DEFAULT_ATR_LENGTH) return false;
				if (m_atr[0] < MinimumATR) return false;	//no minimum price movement
				
				//get the size of the largest bar in the required length, if the range is narrowing the first bar back should be the largest
				double requiredScanLength = Math.Abs(Instrument.High[Length - 1] - Instrument.Low[Length - 1]);

				//check whether at least the last length of bars are not empty and are narrowing
				for (int i = Length - 1; i > 0; i--)
					if (Instrument.High[i] == Instrument.Low[i] || Math.Abs(Instrument.High[i] - Instrument.Low[i]) < Math.Abs(Instrument.High[i - 1] - Instrument.Low[i - 1])) return false;

				//check that the above narrowing range is the tightest range of bars
				//in the scan length
				for (int i = ScanLength - 1; i > Length - 1; i--)
					if (Math.Abs(Instrument.High[i] - Instrument.Low[i]) < requiredScanLength) return false;

				return true;  //all bars meet the criteria for the Crable narrow range bars
			}

			//methods



		}
	}
}