using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Band-pass filter used to isolate the dominant cycle from the price movement.
		/// </summary>


		//TODO: This code is not a band-pass filter, it needs to be simplified. It contains an automatic gain control that needs to be stripped out.


		public sealed class BandPass : FunctionSeries<System.Double>
		{
			//Constants:
			public const int DEFAULT_PERIOD = 20;
			public const double DEFAULT_DELTA = 0.3d;   //also called the Delta of the filter

			//Properties:
			public int Period { get; set; }
			public double Delta { get; set; }
			public ISeries<double> Price { get; set; }

			//Attributes:
			private double m_alpha;
			private double m_beta;
			private double m_gamma;
			private double m_result;

			public BandPass(CStudyControl _master) : base(_master) { }
			public BandPass(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				Period = DEFAULT_PERIOD;
				Delta = DEFAULT_DELTA;
			}

			protected override void StartCalc()
			{
				//validate parameters
				if (Period <= 0)
					throw new Exception("Band-pass filter requires a positive integer for the period.");

				//compute once-off components
				m_beta = Math.Cos(2 * Math.PI / Period);
				m_gamma = 1 / Math.Cos(2 * Math.PI * Delta / Period);
				m_alpha = m_gamma - Math.Sqrt(m_gamma * m_gamma - 1);
				m_result = 0.0d;
			}

			protected override double CalcBar()
			{
				if (Bars.CurrentBar < 3)
					m_result = Price[0];
				else
					m_result = 0.5d * (1.0d - m_alpha) * (Price[0] - Price[2]) + m_beta * (1 - m_alpha) * this[1] - m_alpha * this[2];

				return m_result;
			}
		}
	}
}