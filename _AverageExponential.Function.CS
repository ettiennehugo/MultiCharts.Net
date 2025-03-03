using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Corrected Average Exponential calculation, the XAverage function in MultiCharts do not work correctly on multiple data feeds.
	/// </summary>
	public sealed class _AverageExponential : FunctionSimple<System.Double>
	{
		//constants
		public const int DEFAULT_LENGTH = 10;

		//types


		//properties
		public IInstrument Instrument { get; set; } //data feed to use for the average
		public int Length { get; set; }             //length of the average calculation

		//attribute
		private double m_result;
		private double m_alpha;
		private bool m_initialized;

		//interface methods
		public _AverageExponential(CStudyControl _master) : base(_master) { }
		public _AverageExponential(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			Length = DEFAULT_LENGTH;
		}

		protected override void StartCalc()
		{
			if (Length <= 0) throw new ArgumentException("Length must be greater than zero");
			m_alpha = 2.0 / (Length + 1.0);
			m_result = 0.0;
			m_initialized = false;
		}

		protected override System.Double CalcBar()
		{
			if (m_initialized)
			{
				m_result = m_alpha * Instrument.Close[0] + (1.0 - m_alpha) * m_result;
			}
			else
			{
				m_result = Instrument.Close[0];
				m_initialized = true;
			}

			return m_result;
		}

		//methods


	}
}