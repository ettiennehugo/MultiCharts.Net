using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Zero crossings period measurer, as discussed by Dr. John F. Ehlers in Cycle Analytics for Traders p. 58 (https://mesasoftware.com/)
		/// Measures the dominant cycle using the zero-cross over/under of the band-pass filter over the given period.
		/// NOTE: This cycle measurement is not necessarily the most accurate way of measuring cycles.
		/// </summary>
		public sealed class BandPassDominantCycle : FunctionSimple<System.Double>
		{
			//Constants:
			public const double TWO_PI = 6.283185307179586d;
			public const int DEFAULT_PERIOD = 20;
			public const double DEFAULT_BAND_WIDTH = 0.7;

			//Properties:
			public int Period { get; set; }
			public double BandWidth { get; set; }
			public ISeries<double> Price { get; set; }

			//Attributes:
			private double m_alpha1;
			private double m_beta1;
			private double m_alpha2;
			private double m_gamma1;
			private int m_counter;
			private double m_highPass0;
			private double m_highPass1;
			private double m_highPass2;
			private double m_bandPass0;
			private double m_bandPass1;
			private double m_bandPass2;
			private double m_peak0;
			private double m_peak1;
			private double m_real0;
			private double m_real1;
			private double m_result;

			public BandPassDominantCycle(CStudyControl _master) : base(_master) { }
			public BandPassDominantCycle(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Period = DEFAULT_PERIOD;
				BandWidth = DEFAULT_BAND_WIDTH;
				m_result = 6;
			}

			protected override void StartCalc()
			{
				//validate parameters
				if (Period <= 0)
					throw new Exception("Band-pass filter requires a positive integer for the period.");

				//reset state variables
				m_counter = 0;
				m_highPass0 = 0.0d;
				m_highPass1 = 0.0d;
				m_highPass2 = 0.0d;
				m_bandPass0 = 0.0d;
				m_bandPass1 = 0.0d;
				m_bandPass2 = 0.0d;
				m_peak0 = 0.0d;
				m_peak1 = 0.0d;
				m_real0 = 0.0d;
				m_real1 = 0.0d;

				//compute once-off components
				m_beta1 = Math.Cos(TWO_PI / Period);
				m_gamma1 = 1.0d / Math.Cos(TWO_PI * BandWidth / Period);
				m_alpha1 = m_gamma1 - Math.Sqrt(m_gamma1 * m_gamma1 - 1);
				m_alpha2 = (Math.Cos(0.25 * BandWidth * TWO_PI / Period) + Math.Sin(0.25 * BandWidth * TWO_PI / Period) - 1) / Math.Cos(0.25 * BandWidth * TWO_PI / Period);
			}

			protected override double CalcBar()
			{
				//filter requires at least two historical values to compute values
				if (Bars.CurrentBar >= 2)
				{
					//compute signal value
					m_highPass0 = (1.0d + m_alpha2 / 2) * (Price[0] - Price[1]) + (1 - m_alpha2) * m_highPass1;

					//compute the band-pass value
					m_bandPass0 = 0.5d * (1 - m_alpha1) * (m_highPass0 - m_highPass2) + m_beta1 * (1 + m_alpha1) * m_bandPass1 - m_alpha1 * m_bandPass2;
					m_peak0 = 0.991d * m_peak1;
					if (Math.Abs(m_bandPass0) > m_peak0)
						m_peak0 = Math.Abs(m_bandPass0);

					if (m_peak0 != 0)
						m_real0 = m_bandPass0 / m_peak0;

					//compute the dominant cycle value
					m_result = this[1];
					if (m_result < 6)
						m_result = 6;

					m_counter = m_counter + 1;
					if ((m_real1 < 0 && m_real0 > 0) ||   //cross over
							(m_real1 > 0 && m_real0 < 0))     //cross under
						m_result = m_counter * 2;
					if (2 * m_counter > 1.25d * this[1])
						m_result = 1.25d * this[1];
					if (2 * m_counter < 0.8d * this[1])
						m_result = 0.8d * this[1];
					m_counter = 0;

					//save lookback values for next bar computation
					m_highPass2 = m_highPass1;
					m_highPass1 = m_highPass0;
					m_bandPass2 = m_bandPass1;
					m_bandPass1 = m_bandPass0;
					m_peak1 = m_peak0;
					m_real1 = m_real0;
				}

				return (double)m_result;
			}
		}
	}
}