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
		/// </summary>
		public sealed class _CrableNRX : FunctionSimple<System.Boolean>
		{
			//constants
			public const int DEFAULT_LENGTH = 7;
			public const int DEFAULT_SCAN_LENGTH = 20;

			//inputs
			public IInstrument Instrument { get; set; } //instrument price feed to use for the scan		
			public int Length { get; set; }       //required length of the narrow range bars
			public int ScanLength { get; set; }   //scan length for the narrow range bars

			//properties


			//attributes


			//interface methods
			public _CrableNRX(CStudyControl _master) : base(_master) { }
			public _CrableNRX(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = DEFAULT_LENGTH;
				ScanLength = DEFAULT_SCAN_LENGTH;
			}

			protected override void StartCalc() 
			{
				if (Length <= 0) throw new Exception("Length must be a positive number");
				if (ScanLength <= 0) throw new Exception("ScanLength must be a positive number");
				if (Length > ScanLength) throw new Exception("Length must be less than or equal to ScanLength");
			}

			protected override System.Boolean CalcBar()
			{
				if (Instrument.CurrentBar < ScanLength) return false;

				//get the size of the largest bar in the required length, if the range is narrowing the first bar back should be the largest
				double requiredScanLength = Math.Abs(Instrument.Open[Length] - Instrument.Close[Length]);

				//check whether at least the last length of bars are narrowing 
				for (int i = Length; i > 0; i--)
					if (Math.Abs(Instrument.Open[i] - Instrument.Close[i]) < Math.Abs(Instrument.Open[i - 1] - Instrument.Close[i - 1])) return false;

				//check that the above narrowing range is the tightest range of bars
				//in the scan length
				for (int i = ScanLength; i > Length; i--)
					if (Math.Abs(Instrument.Open[i] - Instrument.Close[i]) < requiredScanLength) return false;

				return true;  //all bars meet the criteria for the Crable narrow range bars
			}

			//methods



		}
	}
}