using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	/// <summary>
	/// Indicator based on the Ehlers filter function, it is similar to a moving average with improved response to actual price moves and less response to
	/// market noise.
	/// </summary>
  [SameAsSymbol]
	public class _EhlersFilter : IndicatorObject {
    //Inputs:    
    [Input] public int Length { get; set; }
    [Input] public EhlersFilter.ECoefficientStrategy CoefficientStrategy { get; set; }
  
    //Attributes:
    private IPlotObject _plot;
    private VariableSeries<double> _price;
    private EhlersFilter _ehlersFilter;

    public _EhlersFilter(object _ctx):base(_ctx){
      Length = EhlersFilter.DEFAULT_LENGTH;
      CoefficientStrategy = EhlersFilter.DEFAULT_COEFFICIENT_STRATEGY;
    }
		
    protected override void Create() {
			_plot = AddPlot(new PlotAttributes("EHLF", EPlotShapes.Line, Color.Red));
      _price = new VariableSeries<double>(this, 0);
      _ehlersFilter = new EhlersFilter(this);
		}

		protected override void StartCalc() {
      _ehlersFilter.Length = Length;
      _ehlersFilter.CoeffientStrategy = CoefficientStrategy;
      _ehlersFilter.Instrument = Bars;
		}

		protected override void CalcBar() {
      //only add new values when bar closes
      _price.Value = (Bars.High[0] + Bars.Low[0] + Bars.Close[0]) / 3;
			_plot.Set(_ehlersFilter.Value);
		}
	}
}