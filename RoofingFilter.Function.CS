using System;
using System.Drawing;
using System.Linq;


namespace PowerLanguage
{
	namespace Function
	{
		/// <summary>
		/// Roofing filter as discussed by John F. Ehlers in Cycle Analytics for Traders p. 78 with optional
		/// zero mean. The roofing filter is essentially a high-pass filter chained with a super smoother that
		/// acts as a low-pass filter that forms a special type of band-pass filter with a very wide sweep.
		/// </summary>
		public sealed class RoofingFilter : FunctionSeries<System.Double>
		{
			//Constants:
			public const int DEFAULT_HP_LENGTH = 48;
			public const int DEFAULT_LP_LENGTH = 10;

			//Properties:
			public int LPLength { get; set; }     //low-pass filter length
			public int HPLength { get; set; }     //high-pass filter length
			public ISeries<double> Price { get; set; }  //price series to compute filter on
			public double A1 { get { return m_a1; } }
			public double B1 { get { return m_b1; } }
			public double C1 { get { return m_c1; } }
			public double C2 { get { return m_c2; } }
			public double C3 { get { return m_c3; } }
			public double Alpha1 { get { return m_alpha1; } }

			//Attributes:
			private double m_alpha1;
			private double m_a1;
			private double m_b1;
			private double m_c1;
			private double m_c2;
			private double m_c3;
			private double m_filter0;
			private double m_filter1;
			private double m_filter2;
			private double m_filter2_1;

			public RoofingFilter(CStudyControl _master) : base(_master) { }
			public RoofingFilter(CStudyControl _master, int _ds) : base(_master, _ds) { }

			protected override void Create()
			{
				//default parameters
				HPLength = DEFAULT_HP_LENGTH;
				LPLength = DEFAULT_LP_LENGTH;
			}

			protected override void StartCalc()
			{
				//validate inputs
				if (Price == null)
					throw new Exception("Assign price series for roofing filter.");
				if (LPLength <= 0)
					throw new Exception("Low pass length must be positive integer.");
				if (LPLength >= HPLength)
					throw new Exception("Low pass length must be less than high pass length.");

				//initialize general attributes
				m_filter0 = 0;
				m_filter1 = 0;
				m_filter2 = 0;
				m_filter2_1 = 0;

				//perform once-off calculations for high-pass filter and super smoother
				m_alpha1 = (Math.Cos(0.707 * 2 * Math.PI / HPLength) + Math.Sin(0.707 * 2 * Math.PI / HPLength) - 1) / Math.Cos(0.707 * 2 * Math.PI / HPLength);
				m_a1 = Math.Exp(-1.414 * Math.PI / LPLength);
				m_b1 = 2 * m_a1 * Math.Cos(1.414 * Math.PI / LPLength);
				m_c2 = m_b1;
				m_c3 = -m_a1 * m_a1;
				m_c1 = 1 - m_c2 - m_c3;
			}

			protected override System.Double CalcBar()
			{
				double result = 0.0d;

				//compute value when enough data available in time series
				if (Bars.Status == EBarState.Close && Bars.CurrentBar >= 3)
				{
					double highPass = (1 - m_alpha1 / 2) * (Price[0] - Price[1]) + (1 - m_alpha1) * this[1];
					m_filter0 = m_c1 * (highPass + this[1]) / 2 + m_c2 * m_filter1 * m_c3 * m_filter2;

					//apply zero mean transform using another high-pass filter
					result = (1 - m_alpha1 / 2) * (m_filter0 - m_filter1) + (1 - m_alpha1) * m_filter2_1;

					//record values for next bar computations
					m_filter2_1 = result;
					m_filter2 = m_filter1;
					m_filter1 = m_filter0;
				}

				return result;
			}
		}
	}
}