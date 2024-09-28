using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Generic filter that can assume the form of a number of other filters using a generic discrete linear system with a set of
		/// coefficients that determine what type of filter it will implement. The original source of this filter is the "Swiss Army
		/// Knife Indicator" by Dr. John Ehlers (https://mesasoftware.com/).
		/// </summary>
		public sealed class GenericFilter : FunctionSeries<System.Double>
		{
			//Constants:
			public const double PI_2 = 6.283185307179586;
			public const int DEFAULT_N = 0;
			public const int DEFAULT_PERIOD = 20;
			public const double DEFAULT_DELTA = 0.1;

			//Types:
			public enum GenericFilterType
			{
				SimpleMovingAverage,
				ExponentialMovingAverage,
				TwoPoleGaussian,
				TwoPoleButterworth,
				HighPass,
				TwoPoleHighPass,
				BandPass,
				BandStop,
			}

			//Properties:
			public ISeries<double> Price { get; set; }
			public IInstrument Instrument { get; set; }
			public GenericFilterType Type { get; set; }
			public int N { get; set; }
			public int Period { get; set; }
			public double Delta { get; set; }

			//Attributes:
			private double a1;
			private double a2;
			private double b0;
			private double b1;
			private double b2;
			private double c0;
			private double c1;
			private double alpha;
			private double beta;
			private double gamma;
			private double result;

			public GenericFilter(CStudyControl _master) : base(_master) { }

			public GenericFilter(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Type = GenericFilterType.SimpleMovingAverage;
				N = DEFAULT_N;
				Period = DEFAULT_PERIOD;
				Delta = DEFAULT_DELTA;
				result = 0.0;

				//set the defaults for the coeficcients of the transfer response function
				a1 = 0;
				a2 = 0;
				b0 = 1;
				b1 = 0;
				b2 = 0;
				c0 = 1;
				c1 = 0;
				alpha = 0;
				beta = 0;
				gamma = 0;
			}

			protected override void StartCalc()
			{
				//ensure we have valid inputs
				if (N == 0)
					throw new Exception("Input parameter N must have a non-zero value.");

				if (Period == 0)
					throw new Exception("Filters requires that Period parameter be set to a non-zero value.");

				//assign correct coefficients based on the selected filter type
				switch (Type)
				{
					case GenericFilterType.SimpleMovingAverage:
						a1 = 1;
						b0 = 1 / N;
						c1 = 1 / N;
						break;
					case GenericFilterType.ExponentialMovingAverage:
						alpha = (Math.Cos(PI_2 / Period) + Math.Sin(PI_2 / Period) - 1) / Math.Cos(PI_2 / Period);
						a1 = 1 - alpha;
						b0 = alpha;
						break;
					case GenericFilterType.TwoPoleGaussian:
						beta = 2.415 * (1 - Math.Cos(PI_2 / Period));
						alpha = -beta + Math.Sqrt(beta * beta + 2 * beta);
						c0 = alpha * alpha;
						a1 = 2 * (1 - alpha);
						a2 = -(1 - alpha) * (1 - alpha);
						break;
					case GenericFilterType.TwoPoleButterworth:
						beta = 2.415 * (1 - Math.Cos(PI_2 / Period));
						alpha = -beta + Math.Sqrt(beta * beta + 2 * beta);
						c0 = alpha * alpha / 4;
						b1 = 2;
						b2 = 1;
						a1 = 2 * (1 - alpha);
						a2 = -(1 - alpha) * (1 - alpha);
						break;
					case GenericFilterType.HighPass:
						alpha = (Math.Cos(PI_2 / Period) + Math.Sin(PI_2 / Period) - 1) / Math.Cos(PI_2 / Period);
						c0 = 1 - alpha / 2;
						b1 = -1;
						a1 = 1 - alpha;
						break;
					case GenericFilterType.TwoPoleHighPass:
						beta = 2.415 * (1 - Math.Cos(PI_2 / Period));
						alpha = -beta + Math.Sqrt(beta * beta + 2 * beta);
						c0 = (1 - alpha / 2) * (1 - alpha / 2);
						b1 = -2;
						b2 = 1;
						a1 = 2 * (1 - alpha);
						a2 = -(1 - alpha) * (1 - alpha);
						break;
					case GenericFilterType.BandPass:
						beta = Math.Cos(PI_2 / Period);
						gamma = 1 / Math.Cos(2 * PI_2 * Delta / Period);
						alpha = gamma - Math.Sqrt(gamma * gamma - 1);
						c0 = (1 - alpha) / 2;
						b2 = -1;
						a1 = beta * (1 + alpha);
						a2 = -alpha;
						break;
					case GenericFilterType.BandStop:
						beta = Math.Cos(PI_2 / Period);
						gamma = 1 / Math.Cos(2 * PI_2 * Delta / Period);
						alpha = gamma - Math.Sqrt(gamma * gamma - 1);
						c0 = (1 - alpha) / 2;
						b1 = -2 * beta;
						b2 = 1;
						a1 = beta * (1 + alpha);
						a2 = -alpha;
						break;
				}
			}

			protected override System.Double CalcBar()
			{
				//ensure we have enough bars to compute the filter value, otherwise we return just the price value
				if (Bars.Status == EBarState.Close)
				{
					if (Bars.CurrentBar < 3 || Bars.CurrentBar < N)
						result = Price[0];
					else
						result = c0 * (b0 * Price[0] + b1 * Price[1] + b2 * Price[2]) + a1 * this[1] + a2 * this[2] - c1 * Price[N];
				}

				return result;
			}
		}
	}
}