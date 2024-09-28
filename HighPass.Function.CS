using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// High pass filter to remove cycle components shorter than a specified
		/// number of periods in the given time series.
		/// As discussed by Dr John F. Ehler in Cycle Analytics for Traders p. 11 - 14
		/// (https://mesasoftware.com/).
		/// </summary>
		public sealed class HighPass : FunctionSeries<System.Double>
		{
			//Constants:
			public const int DEFAULT_PERIOD = 48;

			//Properties:
			public int Period { get; set; }
			public ISeries<double> Price { get; set; }
			public double Alpha1 { get { return alpha1; } }

			//Attributes:
			private double alpha1;
			private double one_min_alpha1;
			private double result;

			public HighPass(CStudyControl _master) : base(_master) { }
			public HighPass(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				alpha1 = 0;
				result = 0;
				Period = DEFAULT_PERIOD;
			}

			protected override void StartCalc()
			{
				//validate inputs
				if (Period <= 0) throw new Exception("Period must be larger than zero.");
				if (Price == null) throw new Exception("High pass filter requires a price series to operate on.");

				//compute once-off components
				alpha1 = (Math.Cos(0.707 * 2 * Math.PI / Period) + Math.Sin(0.707 * 2 * Math.PI / Period) - 1) / Math.Cos(0.707 * 2 * Math.PI / Period);
				one_min_alpha1 = 1 - alpha1;
			}

			protected override System.Double CalcBar()
			{
				if (Bars.Status == EBarState.Close && Bars.CurrentBar >= 3)
					result = (one_min_alpha1 / 2) * (one_min_alpha1 / 2) * (Price[0] - 2 * Price[1] + Price[2]) + 2 * one_min_alpha1 * this[1] - one_min_alpha1 * one_min_alpha1 * this[2];

				return result;
			}
		}
	}
}