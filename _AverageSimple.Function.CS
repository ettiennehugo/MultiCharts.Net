using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Simple Average calculation, the AverageFC function in MultiCharts do not work correctly on multiple data feeds.
	/// </summary>
	public sealed class _AverageSimple : FunctionSimple<System.Double>
	{
		//constants
		public const int DEFAULT_LENGTH = 10;

		//types


		//properties
		public IInstrument Instrument { get; set; } //data feed to use for the average
		public int Length { get; set; }             //length of the average calculation

		//attribute
		private Queue<double> m_values;
		private double m_result;
		private double m_rollingSum;

		//interface methods
		public _AverageSimple(CStudyControl _master) : base(_master) { }
		public _AverageSimple(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			Length = DEFAULT_LENGTH;
		}

		protected override void StartCalc()
		{
			if (Length <= 0) throw new ArgumentException("Length must be greater than zero");
			m_values = new Queue<double>(Length);
			m_rollingSum = 0.0;
			m_result = 0.0;
		}

		protected override System.Double CalcBar()
		{
			if (m_values.Count <= Length)
			{
				m_values.Enqueue(Instrument.Close[0]);
				m_rollingSum += Instrument.Close[0];
				m_result = m_rollingSum / m_values.Count;
			}
			else
			{
				m_rollingSum -= m_values.Dequeue();
				m_values.Enqueue(Instrument.Close[0]);
				m_rollingSum += Instrument.Close[0];
				m_result = m_rollingSum / Length;
			}

			return m_result;
		}

		//methods


	}
}