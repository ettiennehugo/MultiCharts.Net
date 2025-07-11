using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	/// <summary>
	/// Marks bars that meet or exceed a specified volume percentile threshold.
	/// </summary>
	[SameAsSymbol(true)]
	public class _VolumePercentile : IndicatorObject {
		//constants

		
		//types


		//properties
		[Input] public double Percentile { get; set; }

		//attribute
		private IPlotObject m_plot;
		private Function._VolumePercentile m_volumePercentile;
		private double m_percentile;

		//interface methods
		public _VolumePercentile(object _ctx):base(_ctx){}

		protected override void Create() {
			Percentile = 50;
			m_plot = AddPlot(new PlotAttributes("", EPlotShapes.Point, Color.Red, Color.Empty, 10, 0, true));
			m_volumePercentile = new Function._VolumePercentile(this);			
		}

		protected override void StartCalc() {
			m_percentile = Percentile / 100;
			m_volumePercentile.Instrument = Bars;
		}

		protected override void CalcBar(){
			m_volumePercentile.Call();
			if (m_volumePercentile[0] >= m_percentile)
				m_plot.Set(0, Bars.Low[0]);
		}

		//methods


	}
}