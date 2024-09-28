using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator {
  /// <summary>
  /// Smoothed Stochastic using a roofing filter to remove spectral dilation components from the price series before
  /// computing the Stochastic. See Cycle Analytics for Traders p. 83.
  /// NOTE: The code for this indicator looks similar to the roofing filter with the super smoother but it is slightly different.
  /// </summary>
	public class _SmoothStochastic : IndicatorObject {
    //Properties:
    [Input] public int Length { get; set; }
    [Input] public int HighPassPeriod { get; set; }
    [Input] public int SmoothPeriod { get; set; }
    [Input] public double OverBought { get; set; }
    [Input] public double OverSold { get; set; }

    //Attributes:
    private double alpha1;
    private double one_min_alpha1;
    private double a1;
    private double b1;
    private double c1;
    private double c2;
    private double c3;
    private double highPass0;
    private double highPass1;
    private double highPass2;
    private double stoch0;
    private double stoch1;
    private double smoothStoch0;
    private double smoothStoch1;
    private double smoothStoch2;
    private VariableSeries<double> SuperSmoother;
    private IPlotObject PlotStochastic;
    private IPlotObject PlotOverBought;
    private IPlotObject PlotOverSold;

    public _SmoothStochastic(object _ctx) : base(_ctx) { }

    protected override void Create() {
      Length = 20;
      HighPassPeriod = 48;
      SmoothPeriod = 10;
      OverBought = 0.8;
      OverSold = 0.2;

      highPass0 = 0;
      highPass1 = 0;
      highPass2 = 0;

      stoch0 = 0;
      stoch1 = 0;
      smoothStoch0 = 0;
      smoothStoch1 = 0;
      smoothStoch2 = 0;

      SuperSmoother = new VariableSeries<double>(this);
      PlotStochastic = AddPlot(new PlotAttributes("SStoch", EPlotShapes.Line, Color.LightGray));
      PlotOverBought = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
      PlotOverSold = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Green));
    }

    protected override void StartCalc() {
      alpha1 = (Math.Cos(0.707 * 2 * Math.PI / HighPassPeriod) + Math.Sin(0.707 * 2 * Math.PI / HighPassPeriod) - 1) / Math.Cos(0.707 * 2 * Math.PI / HighPassPeriod);
      one_min_alpha1 = 1 - alpha1;
      a1 = Math.Exp(-1.414 * Math.PI) / SmoothPeriod;
      b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / SmoothPeriod);
      c2 = b1;
      c3 = -a1 * a1;
      c1 = 1 - c2 - c3;
    }

    protected override void CalcBar() {
      if (Bars.CurrentBar >= 3) {
        //apply high pass filter
        highPass0 = (one_min_alpha1 / 2) * (one_min_alpha1 / 2) * (Bars.Close[0] - 2 * Bars.Close[1] + Bars.Close[2]) + 2 * one_min_alpha1 * highPass1 - one_min_alpha1 * one_min_alpha1 * highPass2;

        //smoother high-pass output using super smoother
        SuperSmoother.Value = c1 * (highPass0 + highPass1) / 2 + c2 * SuperSmoother[1] + c3 * SuperSmoother[2];

        //compute the stochastic
        double highest = SuperSmoother.Highest(Length);
        double lowest = SuperSmoother.Lowest(Length);

        if ((highest - lowest) != 0)
          stoch0 = (SuperSmoother[0] - lowest) / (highest - lowest);
        smoothStoch0 = c1 * (stoch0 + stoch1) / 2 + c2 * smoothStoch1 + c3 * smoothStoch2;

        //plot output
        PlotStochastic.Set(smoothStoch0);
        PlotOverBought.Set(OverBought);
        PlotOverSold.Set(OverSold);

        //save look back memory values for next bar computation
        highPass2 = highPass1;
        highPass1 = highPass0;
        stoch1 = stoch0;
        smoothStoch2 = smoothStoch1;
        smoothStoch1 = smoothStoch0;
      } else
        //fill value for super smoother series
        SuperSmoother.Value = 0;
    }
  }
}