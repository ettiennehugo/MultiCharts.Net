using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	/// <summary>
	/// Implements an indicator to isolate the cyclic components of price series. See Empirical Mode Decomposition PDF by John Ehlers.
	/// </summary>
	public class _BandPass : IndicatorObject {
    //Properties:
    [Input]public int Period { get; set; }
    [Input]public double Delta { get; set; }

    //Attributes:
    private IPlotObject PlotZero;
    private IPlotObject PlotFilter;
    private BandPass Filter;

    public _BandPass(object _ctx):base(_ctx){}
  
		protected override void Create() {
      Period = BandPass.DEFAULT_PERIOD;
      Delta = BandPass.DEFAULT_DELTA;

	  PlotZero = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.LightGray));
      PlotFilter = AddPlot(new PlotAttributes("BPFlt", EPlotShapes.Line, Color.Red));
      Filter = new BandPass(this);
    }

    protected override void StartCalc() {
      Filter.Period = Period;
      Filter.Delta = Delta;
      Filter.Price = Bars.Close;
		}

		protected override void CalcBar(){
			PlotZero.Set(0);
      PlotFilter.Set(Filter[0]);
		}
	}
}