using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Implements an estimation of the Hurst Exponent using a standard deviation of the detrended price data. 
		/// </summary>
		public sealed class Hurst : FunctionSeries<System.Double>
		{
			//Constants:
			public const int DEFAULT_LENGTH = 20;
			public const double TREND_LEVEL = 0.5;

			//Properties:
			public int Length { get; set; }   //must be an even number
			public bool Smooth { get; set; }  //apply SuperSmoother to get a smooth Hurst coefficient 
			public ISeries<double> Price { get; set; }

			//Attributes:
			private double a1;
			private double b1;
			private double c1;
			private double c2;
			private double c3;

			private double n1;
			private double n2;
			private double n3;
			private double hh;
			private double ll;
			private VariableSeries<double> SmoothHurst;
			private VariableSeries<double> Dimension;
			private double result;

			public Hurst(CStudyControl _master) : base(_master) { }
			public Hurst(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Length = DEFAULT_LENGTH;
				Smooth = true;

				a1 = 0;
				b1 = 0;
				c1 = 0;
				c2 = 0;
				c3 = 0;

				n1 = 0;
				n2 = 0;
				n3 = 0;
				hh = 0;
				ll = 0;

				result = 0.0;

				SmoothHurst = new VariableSeries<double>(this);
				Dimension = new VariableSeries<double>(this);
			}

			protected override void StartCalc()
			{
				//length must be larger than 2
				if (Length <= 2)
					throw new Exception("Hurst coefficient length must be larger than 2.");

				//length must be an even number
				if (Length % 2 != 0)
					throw new Exception("Hurst coefficient can only be estimated on an even length number of bars.");

				//compute once-off coefficients if smoothing used
				if (Smooth)
				{
					a1 = Math.Exp(-1.414 * Math.PI / 20);
					b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / 20);
					c2 = b1;
					c3 = -a1 * a1;
					c1 = 1 - c2 - c3;
				}
			}

			protected override System.Double CalcBar()
			{
				if (Bars.Status == EBarState.Close)
				{
					//only estimate the Hurst coefficient if we have enough bars to work on
					if (Bars.CurrentBar >= Length)
					{
						n3 = (Price.Highest(Length) - Price.Lowest(Length)) / Length;

						hh = Price[0];
						ll = Price[0];

						for (int i = 0; i <= Length / 2 - 1; i++)
						{
							if (Price[i] > hh)
								hh = Price[i];
							if (Price[i] < ll)
								ll = Price[i];
						}

						n1 = (hh - ll) / (Length / 2);

						hh = Price[Length / 2];
						ll = Price[Length / 2];

						for (int i = Length / 2; i <= Length - 1; i++)
						{
							if (Price[i] > hh)
								hh = Price[i];
							if (Price[i] < ll)
								ll = Price[i];
						}

						n2 = (hh - ll) / (Length / 2);
						if (Bars.CurrentBar < 2)
							Dimension.Value = 1.5;   //set the Hurst coefficient computed below to 0.5 for initial values
						else if (n1 > 0 && n2 > 0 && n3 > 0)
							Dimension.Value = 0.5 * ((Math.Log10(n1 + n2) - Math.Log10(n3)) / Math.Log10(2) + Dimension[1]);
					}
					else
					{
						//default dimension to result in zero Hurst coefficient if not enough bars
						Dimension.Value = 2;
					}

					//compute the unsmoothed Hurst coefficient
					result = 2 - Dimension.Value;

					//smooth the Hurst coefficient if required
					if (Smooth)
					{
						SmoothHurst.Value = c1 * (result + this[1]) / 2 + c2 * SmoothHurst[1] + c3 * SmoothHurst[2];
						result = SmoothHurst.Value;
					}
				}

				return result;
			}
		}
	}
}