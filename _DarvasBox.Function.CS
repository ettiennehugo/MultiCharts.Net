using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Determines whether the current bar can be considered to be the last bar forming a Darvas Box of at least X-bars long.
		/// </summary>
		public sealed class _DarvasBox : FunctionSimple<System.Boolean>
		{
			//constants
			public const int DEFAULT_LENGTH = 10;
			public const double DEFAULT_EPSILON = 0.01;   //1% of ATR would be considered equal in price value

			//inputs
			public IInstrument Instrument { get; set; } //instrument price feed to use for the scan		
			public int Length { get; set; }       //required length of the Darvas box
			public double Epsilon { get; set; }   //epsilon factor to use for the ATR for price comparisons being "equal" 

			//properties


			//attributes
			private Function._ATR m_atr;

			//interface methods
			public _DarvasBox(CStudyControl _master) : base(_master) { }
			public _DarvasBox(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = DEFAULT_LENGTH;
				Epsilon = DEFAULT_EPSILON;
				m_atr = new Function._ATR(this);
			}

			protected override void StartCalc()
			{
				if (Length <= 0) throw new Exception("Length must be a positive number");
				if (Epsilon <= 0.0d) throw new Exception("Epsilon must be a positive number");
				m_atr.Instrument = Instrument;
				m_atr.Length = Length;
			}

			protected override System.Boolean CalcBar()
			{
				double epilonValue = (m_atr.Value * Epsilon) / 2;

				//implement the high average and low average for the scan length of bars
				double highAverage = 0;
				double lowAverage = 0;

				for (int i = Length; i > 0; i--)
				{
					highAverage += Instrument.High[i];
					lowAverage += Instrument.Low[i];
				}

				highAverage /= Length;
				lowAverage /= Length;

				//setup the upper and lower ranges based on the ATR
				double upperLow = highAverage - epilonValue;
				double upperHigh = highAverage + epilonValue;
				double lowerLow = lowAverage - epilonValue;
				double lowerHigh = lowAverage + epilonValue;

				//check that all the bars' low/high fall within the upper and lower ranges
				for (int i = Length; i > 0; i--)
					if (Instrument.Low[i] < lowerLow || Instrument.Low[i] > lowerHigh || Instrument.High[i] < upperLow || Instrument.High[i] > upperHigh)
						return false;

				return true;  //all bars meet the criteria for the Darvas box
			}

			//methods


		}
	}
}