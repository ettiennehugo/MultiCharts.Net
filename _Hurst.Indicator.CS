using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator {
  /// <summary>
  /// Estimates the Hurst coefficient as described by John Ehlers in Cycle Analytics for Traders p.68
  /// </summary>
  public class _Hurst : IndicatorObject {
    //Properties:
    [Input]public int Length { get; set; }
	[Input]public double TrendLevel { get; set; }
    [Input]public bool Smooth { get; set; }

    //Attributes:
    private IPlotObject PlotHurst;
    private IPlotObject PlotTrendLevel;
    private Function.Hurst Hurst; 

    public _Hurst(object _ctx):base(_ctx) {}
	
		protected override void Create() {
      Length = Function.Hurst.DEFAULT_LENGTH;
	  TrendLevel = Function.Hurst.TREND_LEVEL;
      Smooth = true;
      Hurst = new Function.Hurst(this);
			PlotHurst = AddPlot(new PlotAttributes("Hrst", EPlotShapes.Line, Color.Red));
      PlotTrendLevel = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.LightGray));
    }

    protected override void StartCalc() {
      Hurst.Length = Length;
      Hurst.Smooth = Smooth;
      Hurst.Price = Bars.Close;
		}

		protected override void CalcBar(){
      PlotTrendLevel.Set(TrendLevel);
      PlotHurst.Set(Hurst[0]);
		}
	}
}