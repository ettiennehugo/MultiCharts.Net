using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Autocorrelation reversal function indicating the likelihood of a reversal based on the autocorrelation of the price series
		/// as described by Dr. John F.Ehlers in Cycle Analytics for Traders p. 110.
		/// </summary>
		public sealed class AutocorrelationReversals : FunctionSimple<System.Boolean>
		{
			//constants
			public const int DEFAULT_LENGTH = 3;
			public const int DEFAULT_LOW_PASS_LENGTH = 10;
			public const int DEFAULT_HIGH_PASS_LENGTH = 48;

			//properties
			public int Length { get; set; }						//Averaging length - take care when you make this larger as it will increase the lag of the indicator.
			public int LowPassLength { get; set; }    //Low-pass length - making this larger increases the sensitivity in detecting reversals.
			public int HighPassLength { get; set; }   //High-pass length - changing this has a relatively small effect.

			public double A1 { get { return m_a1; } }
			public double B1 { get { return m_b1; } }
			public double C1 { get { return m_c1; } }
			public double C2 { get { return m_c2; } }
			public double C3 { get { return m_c3; } }

			//attributes
			private int m_rampupBarCount;
			private double m_alpha1;
			private double m_oneMinAlpha1;
			private double m_a1;
			private double m_b1;
			private double m_c1;
			private double m_c2;
			private double m_c3;
			private double m_highPass0;
			private double m_highPass1;
			private double m_highPass2;
			private VariableSeries<double> m_smoother;

			//interface methods
			public AutocorrelationReversals(CStudyControl _master) : base(_master) { }
			public AutocorrelationReversals(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				//setup attribute defaults
				Length = DEFAULT_LENGTH;
				LowPassLength = DEFAULT_LOW_PASS_LENGTH;
				HighPassLength = DEFAULT_HIGH_PASS_LENGTH;
				m_smoother = new VariableSeries<double>(this);
			}

			protected override void StartCalc()
			{
				//validate inputs
				if (Length < 0)
					throw new Exception("Averaging length must be zero or a positive integer.");
				if (LowPassLength <= 0)
					throw new Exception("Low-pass length must be a positive integer.");
				if (HighPassLength < Length)
					throw new Exception("High-pass length must be a positive integer greater than averaging length.");
				if (LowPassLength > HighPassLength)
					throw new Exception("Low-pass length must be less than high-pass length.");

				//initialize values
				m_highPass0 = 0.0d;
				m_highPass1 = 0.0d;
				m_highPass2 = 0.0d;

				//compute once-off coefficients
				m_alpha1 = (Math.Cos(0.707 * 2 * Math.PI / DEFAULT_HIGH_PASS_LENGTH) + Math.Sin(0.707 * 2 * Math.PI / DEFAULT_HIGH_PASS_LENGTH) - 1) / Math.Cos(0.707 * 2 * Math.PI / DEFAULT_HIGH_PASS_LENGTH);
				m_oneMinAlpha1 = 1 - m_alpha1;
				m_a1 = Math.Exp(-1.414 * Math.PI) / LowPassLength;
				m_b1 = 2 * m_a1 * Math.Cos(1.414 * Math.PI / LowPassLength);
				m_c2 = m_b1;
				m_c3 = -m_a1 * m_a1;
				m_c1 = 1 - m_c2 - m_c3;

				//compute rampup number of bars before indicator can be output
				m_rampupBarCount = Length + HighPassLength + 3;    //+3 for smoothing count for high-pass filter and super smoother
			}

			protected override System.Boolean CalcBar()
			{
				Boolean result = false;

				if (Bars.Status == EBarState.Close)
				{
					if (Bars.CurrentBar > 3)
					{
						//apply high pass filter
						m_highPass0 = (m_oneMinAlpha1 / 2) * (m_oneMinAlpha1 / 2) * (Bars.Close[0] - 2 * Bars.Close[1] + Bars.Close[2]) + 2 * m_oneMinAlpha1 * m_highPass1 - m_oneMinAlpha1 * m_oneMinAlpha1 * m_highPass2;
						//smoother high-pass output using super smoother
						m_smoother.Value = m_c1 * (m_highPass0 + m_highPass1) / 2 + m_c2 * m_smoother[1] + m_c3 * m_smoother[2];
					}
					else
						m_smoother.Value = 0;

					//save look back memory values for next bar computation
					m_highPass2 = m_highPass1;
					m_highPass1 = m_highPass0;

					if (Bars.CurrentBar > m_rampupBarCount)
					{
						//compute Pearson correlation for each value of lag
						double[,] correlation = new double[HighPassLength + 1, 2];  //+1 because lag length below is zero based
						double m;
						double x;
						double y;
						double sx;
						double sy;
						double sxx;
						double syy;
						double sxy;

						for (int lag = 3; lag <= HighPassLength; lag++)
						{
							correlation[lag, 1] = correlation[lag, 0];

							//set averaging length as m
							m = Length;
							if (Length == 0) m = lag;

							//initialize correlation sums
							sx = 0.0d;
							sy = 0.0d;
							sxx = 0.0d;
							syy = 0.0d;
							sxy = 0.0d;

							//advance samples of both data streams and sum Pearson components
							for (int count = 0; count < m; count++)
							{
								x = m_smoother[count];
								y = m_smoother[count + lag];
								sx = sx + x;
								sy = sy + y;
								sxx = sxx + x * x;
								syy = syy + y * y;
								sxy = sxy + x * y;
							}

							//compute correlation for each value of lag
							if (((m * sxx - sx * sx) * (m * syy - sy * sy)) > 0)
								correlation[lag, 0] = (m * sxy - sx * sy) / Math.Sqrt((m * sxx - sx * sx) * (m * syy - sy * sy));

							//scale correlation to range between 0 and 1
							correlation[lag, 0] = 0.5d * (correlation[lag, 0] + 1);
						}

						int sumDeltas = 0;
						for (int lag = 3; lag <= HighPassLength; lag++)
							if ((correlation[lag, 0] > 0.5d && correlation[lag, 1] < 0.5d) || (correlation[lag, 0] < 0.5d && correlation[lag, 1] > 0.5d)) sumDeltas++;

						if (sumDeltas > (HighPassLength / 2))
							result = true;
					}

				}

				return result;
			}
			
			//methods
			
			
		}
	}
}