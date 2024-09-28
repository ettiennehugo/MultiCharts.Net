using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator {
  public class _SmoothRSI : IndicatorObject {
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
    private double closesUp0;
    private double closesUp1;
    private double closesDn0;
    private double closesDn1;
    private double denom0;
    private double denom1;
    private double rsi0;
    private double rsi1;
    private double rsi2;
    private VariableSeries<double> SuperSmoother;
    private IPlotObject PlotRSI;
    private IPlotObject PlotOverBought;
    private IPlotObject PlotOverSold;

    public _SmoothRSI(object _ctx) : base(_ctx) { }

    protected override void Create() {
      Length = 10;
      HighPassPeriod = 48;
      SmoothPeriod = 10;
      OverBought = 0.7;
      OverSold = 0.3;

      highPass0 = 0;
      highPass1 = 0;
      highPass2 = 0;

      closesUp0 = 0;
      closesUp1 = 0;
      closesDn0 = 0;
      closesDn1 = 0;
      denom0 = 0;
      denom1 = 0;
      rsi0 = 0;
      rsi1 = 0;
      rsi2 = 0;

      SuperSmoother = new VariableSeries<double>(this);
      PlotRSI = AddPlot(new PlotAttributes("SRSI", EPlotShapes.Line, Color.LightGray));
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
      if (Bars.CurrentBar > Length && Bars.CurrentBar > 3) {
        //apply high pass filter
        highPass0 = (one_min_alpha1 / 2) * (one_min_alpha1 / 2) * (Bars.Close[0] - 2 * Bars.Close[1] + Bars.Close[2]) + 2 * one_min_alpha1 * highPass1 - one_min_alpha1 * one_min_alpha1 * highPass2;

        //smoother high-pass output using super smoother
        SuperSmoother.Value = c1 * (highPass0 + highPass1) / 2 + c2 * SuperSmoother[1] + c3 * SuperSmoother[2];

        //compute the RSI based on filtered/smoothed price values
        closesUp0 = 0;
        closesDn0 = 0;
        for (int i = 0; i < Length; i++) {
          if (SuperSmoother[i] > SuperSmoother[i + 1])
            closesUp0 = closesUp0 + (SuperSmoother[i] - SuperSmoother[i + 1]);
          if (SuperSmoother[i] < SuperSmoother[i + 1])
            closesDn0 = closesDn0 + (SuperSmoother[i + 1] - SuperSmoother[i]);
        }
        
        denom0 = closesUp0 + closesDn0;
        if (denom0 != 0 && denom1 != 0)
          rsi0 = c1 * (closesUp0 / denom0 + closesUp1 / denom1) / 2 + c2 * rsi1 + c3 * rsi2;

        //plot output
        PlotRSI.Set(rsi0);
        PlotOverBought.Set(OverBought);
        PlotOverSold.Set(OverSold);

        //save look back memory values for next computation
        highPass2 = highPass1;
        highPass1 = highPass0;
        closesUp1 = closesUp0;
        closesDn1 = closesDn0;
        denom1 = denom0;
        rsi2 = rsi1;
        rsi1 = rsi0;
      } else
        //fill value for super smoother series
        SuperSmoother.Value = 0;
    }
  }
}