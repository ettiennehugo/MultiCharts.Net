using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Computes the average daily range for a stock, does not take into account gaps like the ATR.
		/// </summary>
		public sealed class _ADR : FunctionSeries<System.Double>
		{
			//constants
			public const int DEFAULT_LENGTH = 20;

			//inputs
			public int Length { get; set; }
			public IInstrument Instrument { get; set; } //instrument to use for the ADR calculation, should be a daily data feed

			public _ADR(CStudyControl _master) : base(_master) { }
			public _ADR(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = DEFAULT_LENGTH;
			}

			protected override void StartCalc()
			{
				if (Length <= 0) throw new Exception("Length must be positive");
			}

			protected override System.Double CalcBar()
			{
				double value = 0.0;

				if (Instrument.CurrentBar > Length)
				{
					for (int i = 0; i < Length; i++)
					{
						//ensure Bars.Low[i] is not zero to avoid division by zero, in general should not occur
						if (Instrument.Low[i] != 0)
						{
							//calculate the ratio
							double ratio = Instrument.High[i] / Instrument.Low[i];

							//check for overflow/underflow and adjust if necessary
							if (!double.IsInfinity(ratio) && !double.IsNaN(ratio))
								value += ratio;
						}
					}

					// Check for overflow/underflow in the final value and adjust if necessary
					if (double.IsInfinity(value) || double.IsNaN(value))
						value = 0.0; //set to a neutral value to avoid affecting the result
					else
						value = 100 * ((value / Length) - 1);
				}

				return value;
			}
		}
	}
}