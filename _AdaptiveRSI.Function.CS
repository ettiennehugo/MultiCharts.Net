using ATCenterProxy.interop;
using System;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Adaptive RSI as described in in Cycle Analytics for Traders by John Ehlers, p. 135
	/// </summary>
	public sealed class _AdaptiveRSI : FunctionSeries<System.Double>
	{
		//constants
		public const int DEFAULT_LENGTH = 48;
		public const int DEFAULT_SMOOTHING_LENGTH = 10;
		public const int AVERAGING_LENGTH = 3; 

		//types


		//properties
		public int Length { get; set; } = DEFAULT_LENGTH;
		public int SmoothingLength { get; set; } = DEFAULT_SMOOTHING_LENGTH;
		public IInstrument Instrument { get; set; } = null;

		//attribute
		private double m_alpha1;
		private double m_alpha2;
		private double m_alpha3;
		private VariableSeries<double> m_hp;
		private VariableSeries<double> m_filt;
		private double m_a1;
		private double m_b1;
		private double m_c1;
		private double m_c2;
		private double m_c3;
		private double m_maxpwr;
		private double[] m_corr;
		private double[] m_cosinePart;
		private double[] m_sinePart;
		private double[] m_sqSum;
		private double[,] m_r;
		private double[] m_pwr;

		//interface methods
		public _AdaptiveRSI(CStudyControl _master) : base(_master) { }
		public _AdaptiveRSI(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			base.Create();
			m_hp = new VariableSeries<double>(this);
			m_filt = new VariableSeries<double>(this);
			m_alpha1 = (Math.Cos(0.707 * 360 / Length) + Math.Sin(0.707 * 360 / Length) - 1) / Math.Cos(0.707 * 360 / Length);
			m_alpha2 = 1 - m_alpha1;
			m_alpha3 = m_alpha2 / 2;
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (Length < 0) throw new ArgumentException("Length must be larger than 0");
			if (SmoothingLength < 0) throw new ArgumentException("SmoothingLength must be larger than 0");
			if (Length < SmoothingLength) throw new ArgumentException("Length must be larger than SmoothingLength");
			m_a1 = Math.Exp(-1.414 * 3.141529 / SmoothingLength);
			m_b1 = 2 * m_a1 * Math.Cos(1.414*180 / SmoothingLength);
			m_c2 = m_b1;
			m_c3 = -m_a1 * m_a1;
			m_c1 = 1 - m_c2 - m_c3;
			m_maxpwr = 0;
			m_corr = new double[Length];
			m_cosinePart = new double[Length];
			m_sinePart = new double[Length];
			m_sqSum = new double[Length];
			m_r = new double[Length, 2];
			m_pwr = new double[Length];
		}

		protected override System.Double CalcBar()
		{
			if (Instrument.CurrentBar < 2) {
				m_hp.Value = 0;
				m_filt.Value = 0;
				return 0;
			}

			m_hp.Value = m_alpha3 * m_alpha3 * (Instrument.Close[0] - 2 * Instrument.Close[1] + Instrument.Close[2]) + 2 * m_alpha2 * m_hp[1] - m_alpha2 * m_alpha2 * m_hp[2];
			m_filt.Value = m_c1 * (m_hp[0] + m_hp[1]) / 2 + m_c2 * m_filt[1] + m_c3 * m_filt[2];

			//Pearson correlation for each value of lag
			for (int lag = 0; lag < Length; lag++)
			{
				double sx = 0;
				double sy = 0;
				double sxx = 0;
				double syy = 0;
				double sxy = 0;
				m_corr[lag] = 0;

				for (int ii = 0; ii < AVERAGING_LENGTH; ii++)
				{
					double x = m_filt[ii];
					double y = m_filt[lag + ii];
					sx = sx + x;
					sy = sy + y;
					sxx = sxx + x * x;
					syy = syy + y * y;
					sxy = sxy + x * y;
				}

				if ((lag * sxx - sx * sx) * (lag * syy - sy * sy) > 0)
					m_corr[lag] = (lag * sxy - sx * sy) / Math.Sqrt((lag * sxx - sx * sx) * (lag * syy - sy * sy));
			}

			for (int period = SmoothingLength; period < Length; period++)
			{
				m_cosinePart[period] = 0;
				m_sinePart[period] = 0;
				for (int n = 3; n < Length; n++)
				{
		      m_cosinePart[period] = m_cosinePart[period] + m_corr[n] * Math.Cos(2 * Math.PI * n / period);
					m_sinePart[period] = m_sinePart[period] + m_corr[n] * Math.Sin(2 * Math.PI * n / period);
				}
				m_sqSum[period] = m_cosinePart[period] * m_cosinePart[period] + m_sinePart[period] * m_sinePart[period];
			}

			for (int period  = SmoothingLength; period < Length; period++)
			{
				m_r[period, 1] = m_r[period, 0];
				m_r[period, 0] = 0.2 * m_sqSum[period] * m_sqSum[period] + 0.8 * m_r[period, 1];
			}

			//find maximum power level for normalization
			m_maxpwr = 0.991 * m_maxpwr;
			for (int period = SmoothingLength; period < Length; period++)
				if (m_r[period, 0] > m_maxpwr)
					m_maxpwr = m_r[period, 0];

			for (int period = 3; period < Length; period++)
				m_pwr[period] = m_r[period, 0] / m_maxpwr;

			//compute the dominant cycle using the CG for spectrum
			double spx = 0;
			double sp = 0;
			for (int period = SmoothingLength; period < Length; period++)
			{
				if (m_pwr[period] >= 0.5)
				{
					spx = spx + m_pwr[period] * period;
					sp = sp + m_pwr[period];
				}
			}

			int dominantCycle = sp != 0 ? (int)Math.Round(spx / sp, 0) : 0;
			if (dominantCycle < SmoothingLength) dominantCycle = SmoothingLength;
			if (dominantCycle > Length) dominantCycle = Length;

			//adaptive RSI starts here

			
			
			//TODO - implement this, needs a bunch of VariableSeries around the closesUp, closesDown, denominator

			
			
			double closesUp = 0;
			double closesDown = 0;
			for (int count = 0; count < dominantCycle; count++)
			{
				if (m_filt[count] > m_filt[count + 1])
					closesUp += m_filt[count] - m_filt[count + 1];
				else if (m_filt[count] < m_filt[count + 1])
					closesDown += m_filt[count + 1] - m_filt[count];
			}

			double denominator = closesUp + closesDown;
			if (denominator == 0)
				return 0;
			return default(System.Double);

		}

		//methods


	}
}