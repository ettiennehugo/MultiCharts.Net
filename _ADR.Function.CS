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
			public const int LENGTH = 20;

			//inputs
			[Input] public int Length { get; set; }

			public _ADR(CStudyControl _master) : base(_master) { }
			public _ADR(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = LENGTH;
			}

			protected override void StartCalc()
			{
				if (Length <= 0) throw new Exception("Length must be positive");
			}

			protected override System.Double CalcBar()
			{
				Double value = 0.0;

				if (Bars.CurrentBar > Length)
				{
					for (int i = 0; i < Length; i++)
					{
						// Ensure Bars.Low[i] is not zero to avoid division by zero
						if (Bars.Low[i] != 0)
						{
							// Calculate the ratio
							double ratio = Bars.High[i] / Bars.Low[i];

							// Check for overflow/underflow and adjust if necessary
							if (double.IsInfinity(ratio) || double.IsNaN(ratio))
								ratio = 1.0; // Set to a neutral value to avoid affecting the result

							value += ratio - 1;
						}
					}

					// Check for overflow/underflow in the final value and adjust if necessary
					if (double.IsInfinity(value) || double.IsNaN(value))
						value = 0.0; // Set to a neutral value to avoid affecting the result
					else
						value = (100 * value) / Length;
				}

				return value;
			}
		}
	}
}