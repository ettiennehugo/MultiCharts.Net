using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Simple Average calculation, the AverageFC function in MultiCharts do not work correctly on multiple data feeds.
	/// </summary>
	public sealed class _AverageSimple : FunctionSeries<System.Double>
	{
		//constants
		public const int DEFAULT_LENGTH = 10;

		//types


		//properties
		public IInstrument Instrument { get; set; } //data feed to use for the average
		public int Length { get; set; }             //length of the average calculation

		//attribute


		//interface methods
		public _AverageSimple(CStudyControl _master) : base(_master) { }
		public _AverageSimple(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			Length = DEFAULT_LENGTH;
		}

		protected override void StartCalc()
		{
			if (Instrument == null) throw new ArgumentNullException("Instrument must be set");
			if (Length <= 0) throw new ArgumentException("Length must be greater than zero");
		}

		protected override System.Double CalcBar()
		{
			double sum = 0;
			int availableBars = Math.Min(Length, Bars.CurrentBar + 1);

			for (int i = 0; i < availableBars; i++)
				sum += Instrument.Close[i]; //instrument.Close[0] updates intrabar

			return sum / availableBars;
		}

		//methods


	}
}