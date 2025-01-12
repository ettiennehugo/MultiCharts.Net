using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Voss predictive filter, it aims to minimize the group and phase delays of a filter in order to improve the indicator robustness. See Stocks and Commodities
		/// magazine August 2019 p. 9
		/// NOTES:
		/// * Can not be used on it's own but works well for mean-reversion but does not work as well for trending markets, must use it in conjunction with another
		///   confirmation signal.
		/// * Output becomes noisier as the prediction period becomes longer so keep it to a minimum with 3 being advised.
		/// * Outputs a two-pole band pass fitler value that can be plotted against the Voss filter output that can be used to construct trading rules.
		/// </summary>
		public sealed class _Voss : FunctionSimple<System.Double>
		{
			//Inputs/Outputs:
			[Input] public int Period { get; set; }
			[Input] public int Predict { get; set; }
			public ISeries<double> Price { get; set; }
			public double Filter { get { return filter.Value; } }

			//Attributes:
			private int order;
			private double f1;
			private double g1;
			private double s1;
			private const double bandWidth = 0.25;
			private VariableSeries<double> filter;
			private VariableSeries<double> voss;

			public _Voss(CStudyControl _master) : base(_master) { }
			public _Voss(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				// create variable objects and function objects
				Period = 20;
				Predict = 3;
				Price = null;
				voss = new VariableSeries<double>(this);
				filter = new VariableSeries<double>(this);
			}

			protected override void StartCalc()
			{
				//compute once-off values on the first bar processed
				order = 3 * Predict;
				f1 = Math.Cos(2 * Math.PI / Period);
				g1 = Math.Cos(2 * Math.PI * bandWidth / Period);
				s1 = 1 / g1 - Math.Sqrt(1 / (g1 * g1) - 1);
			}

			protected override System.Double CalcBar()
			{
				//compute the values for the two-pole band pass filter
				if (Bars.CurrentBar <= 5)
					filter.Value = 0;
				else
					filter.Value = 0.5 * (1 - s1) * (Price[0] - Price[2]) + f1 * (1 * s1) * filter[1] - s1 * filter[2];

				//compute the value for the Voss predictive filter
				double sumC = 0.0;
				for (int i = 0; i < order; i++)
					sumC += ((i + 1) / order) * voss[order - i];
				voss.Value = ((3 + order) / 2) * filter.Value - sumC;

				return voss.Value;
			}
		}
	}
}