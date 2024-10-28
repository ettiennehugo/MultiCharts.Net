using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using System.Drawing.Drawing2D;

namespace PowerLanguage.Indicator
{
	[SameAsSymbol]
	public class _Dochian_Channel : IndicatorObject
	{
		//Inputs:
		[Input] public int LowLength { get; set; }
		[Input] public int HighLength { get; set; }

		//Constants:
		private const int DefaultFillAlpha = 30;

		//Attributes:		
		private IPlotObject _lowPlot;
		private IPlotObject _highPlot;

		public _Dochian_Channel(object _ctx) : base(_ctx)
		{
			LowLength = 20;
			HighLength = 20;
		}

		protected override void Create()
		{
			// create variable objects, function objects, plot objects etc.
			_lowPlot = AddPlot(new PlotAttributes("DCLow", EPlotShapes.Line, Color.Red));
			_highPlot = AddPlot(new PlotAttributes("DCHigh", EPlotShapes.Line, Color.Green));
		}

		//protected override void StartCalc() {
		//	//adjust the fill alpha for rendering
		//	FillColor = Color.FromArgb(DefaultFillAlpha, FillColor.R, FillColor.G, FillColor.B);
		//}

		protected override void CalcBar()
		{
			_lowPlot.Set(Bars.Low.Lowest(LowLength));
			_highPlot.Set(Bars.High.Highest(HighLength));
		}

	}
}