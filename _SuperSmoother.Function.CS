using System;

namespace PowerLanguage.Function
{
	/// <summary>
	/// SuperSmoother filter as discussed by Dr John F. Ehlers in Cycle Analytics for Traders p. 31 (https://mesasoftware.com/).
	/// </summary>
	public sealed class _SuperSmoother : FunctionSimple<System.Double>
	{
		//Constants:
		public const int DEFAULT_PERIOD = 20;
		public const double SQRT_2 = 1.414213562373095d;

		//Properties:
		public int Length { get; set; }
		public IInstrument Instrument { get; set; }
		public double A1 { get { return m_a1; } }
		public double B1 { get { return m_b1; } }
		public double C1 { get { return m_c1; } }
		public double C2 { get { return m_c2; } }
		public double C3 { get { return m_c3; } }

		//Attributes:
		private double m_previous1;
		private double m_previous2;
		private double m_a1;
		private double m_b1;
		private double m_c1;
		private double m_c2;
		private double m_c3;

		public _SuperSmoother(CStudyControl _master) : base(_master) { }
		public _SuperSmoother(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			Length = DEFAULT_PERIOD;
		}

		protected override void StartCalc()
		{
			if (Instrument == null) throw new ArgumentNullException("Supersmoother instrument not set");
			if (Length <= 0) throw new ArgumentException("Supersmoother requires a positive integer for Length.");

			m_previous1 = 0;
			m_previous2 = 0;

			//compute the coefficients of the SuperSmoother based on given length
			m_a1 = Math.Exp(-SQRT_2 * Math.PI / Length);
			m_b1 = 2.0d * m_a1 * Math.Cos(SQRT_2 * Math.PI / Length);
			m_c2 = m_b1;
			m_c3 = -m_a1 * m_a1;
			m_c1 = 1.0d - m_c2 - m_c3;
		}

		protected override double CalcBar()
		{
			double result = 0;
			if (Instrument.CurrentBar < 4)
				result = Instrument.Close[0];
			else
				result = m_c1 * (Instrument.Close[0] + Instrument.Close[1]) / 2.0d + m_c2 * m_previous1 + m_c3 * m_previous2;

			//only update the previous values on close bars
			if (Instrument.Status == EBarState.Close)
			{
				m_previous2 = m_previous1;
				m_previous1 = result;
			}

			return result;
		}
	}
}