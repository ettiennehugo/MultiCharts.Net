using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class _BandPassDominantCycle : IndicatorObject {
    //Properties:
    [Input]public int Period { get; set; }
    [Input]public double BandWidth { get; set; }

    //Attributes:
    private BandPassDominantCycle BandPassDominantCycle;
    private IPlotObject PlotDominantCycle;

    public _BandPassDominantCycle(object _ctx):base(_ctx){}

		protected override void Create() {
			PlotDominantCycle = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
      BandPassDominantCycle = new BandPassDominantCycle(this);
      Period = BandPassDominantCycle.DEFAULT_PERIOD;
      BandWidth = BandPassDominantCycle.DEFAULT_BAND_WIDTH;
		}

		protected override void StartCalc() {
      BandPassDominantCycle.Period = Period;
      BandPassDominantCycle.BandWidth = BandWidth;
      BandPassDominantCycle.Price = Bars.Close;
		}

		protected override void CalcBar(){
      PlotDominantCycle.Set(BandPassDominantCycle[0]);
		}
	}
}