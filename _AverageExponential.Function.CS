using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Average Exponential calculation, the XAverage function in MultiCharts do not work correctly on multiple data feeds.
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
		private double m_previous;
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
			if (Instrument == null) throw new ArgumentNullException("Instrument must be set");
			if (Length <= 0) throw new ArgumentException("Length must be greater than zero");
			m_previous = 0;
			m_alpha = 2.0 / (Length + 1.0);
			m_initialized = false;
		}

		protected override System.Double CalcBar()
		{
			if (Instrument.Status != EBarState.Close) return this[1]; //only calculate on close bars

			if (!m_initialized)
			{
				//use SMA for the first 'Length' bars to initialize the EMA correctly (not using this would cause the EMA longer to converge)
				int availableBars = Math.Min(Length, Bars.CurrentBar + 1);
				if (availableBars < Length)
				{
					double sum = 0;
					for (int i = 0; i < availableBars; i++)
						sum += Instrument.Close[i];
					return sum / availableBars;
				}
				else
				{
					m_initialized = true; // Switch to EMA after Length bars
					double sma = 0;
					for (int i = 0; i < Length; i++)
						sma += Instrument.Close[i];
					return sma / Length;
				}
			}
			else
			{
				//standard EMA calculation
				double result = m_alpha * Instrument.Close[0] + (1.0 - m_alpha) * m_previous;

				//only update the previous value on close bars
				if (Instrument.Status == EBarState.Close)
					m_previous = result;

				return result;
			}
		}

		//methods


	}
}