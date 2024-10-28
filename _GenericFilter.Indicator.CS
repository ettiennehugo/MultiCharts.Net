using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
  /// <summary>
  /// Uses a generic filter to represent a number of different indicators/filters based on the filter type.
  /// NOTE: The Period and N parameters do NOT necessarily represent the same quantative value as the input parameters of
  ///       a similar indicator, e.g. EMA with a length of 20 is represented by a generic filter selecting the EMA type with
  ///       a period of around 60.
  ///       
  /// 
  /// TODO: In general it's stupid to model a SMA and EMA with this, the SMA does not look like it works anyway!!!!
  /// 
  /// </summary>
	[SameAsSymbol]
	public class _GenericFilter : IndicatorObject {
		//Types:
		public enum ECandleStickValue { 
			Open,
			High,
			Low,
			Close,
		};		
		
		//Inputs:
		[Input] public ECandleStickValue CandleStickValue { get; set; }
		[Input] public GenericFilter.GenericFilterType FilterType { get; set; }
		[Input] public int N { get; set; }
		[Input] public int Period { get; set; }
		[Input] public double Delta { get; set; }
				
		//Attributes:
		private GenericFilter _filter;
		private IPlotObject _plot;

		public _GenericFilter(object _ctx):base(_ctx){
			CandleStickValue = ECandleStickValue.Close;
			N = GenericFilter.DEFAULT_N;
			Period = GenericFilter.DEFAULT_PERIOD;
			Delta = GenericFilter.DEFAULT_DELTA;			
		}
		
		protected override void Create() {
			_filter = new GenericFilter(this);			
			_plot = AddPlot(new PlotAttributes("GF", EPlotShapes.Line, Color.Red));
		}
		protected override void StartCalc() {
			_filter.Instrument = Bars;
			_filter.Type = FilterType;
			_filter.N = N;
			_filter.Period = Period;
			_filter.Delta = Delta;
			
			switch (CandleStickValue) {
				case ECandleStickValue.Open:
					_filter.Price = Bars.Open;
					break;
				case ECandleStickValue.High:
					_filter.Price = Bars.High;
					break;
				case ECandleStickValue.Low:
					_filter.Price = Bars.Low;
					break;
				case ECandleStickValue.Close:
					_filter.Price = Bars.Close;
					break;
			}
		}
		protected override void CalcBar(){
			// indicator logic 
			_plot.Set(_filter.Value);
		}
	}
}