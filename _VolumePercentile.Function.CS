using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Computes the volume percentile for a bar when it closes, this can be used to determine whether a specific bar on the chart
	/// can be considered high volume or low volume.
	/// </summary>
	public sealed class _VolumePercentile : FunctionSeries<System.Double>
	{
		//constants

		
		//types


		//properties
		public IInstrument Instrument { get; set; }

		//attribute
		private List<double> m_sortedVolume;

		//interface methods
		public _VolumePercentile(CStudyControl _master) : base(_master) { }
		public _VolumePercentile(CStudyControl _master, int _ds) : base(_master, _ds) { }

		protected override void Create()
		{
			m_sortedVolume = new List<double>();
		}

		protected override void StartCalc()
		{
			m_sortedVolume.Clear();
		}

		protected override System.Double CalcBar()
		{
			double result = 0;
			if (Environment.CalcReason == CalculationReason.Default && Instrument.Status == EBarState.Close)
			{
				m_sortedVolume.Add(Instrument.Volume[0]);
				m_sortedVolume.Sort();
				int index = m_sortedVolume.IndexOf(Instrument.Volume[0]);
				//approximate calculation to get percentile in which the value resides
				//will only work once the collection has a good bit of values in it for
				//statistical significance
				result = (double)index / m_sortedVolume.Count;
			}
			
			return result;
		}

		//methods


	}
}