﻿using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class _ADR : IndicatorObject {
		//inputs
		[Input]public int Length { get; set; }
		
		//attributes
		private IPlotObject plot;
		private Function._ADR adr;
		
		public _ADR(object _ctx):base(_ctx){}

		protected override void Create() {
			Length = Function._ADR.DEFAULT_LENGTH;
			adr = new Function._ADR(this);
			plot = AddPlot(new PlotAttributes("ADR", EPlotShapes.Line, Color.Green));
		}
		
		protected override void StartCalc() 
		{
			adr.Length = Length;
			adr.Instrument = Bars;
		}
		
		protected override void CalcBar()
		{
			if (Environment.CalcReason == CalculationReason.Default && Bars.CurrentBar > Length) plot.Set(100 * adr.Value); //plot the ADR value as opercentage
		}
	}
}