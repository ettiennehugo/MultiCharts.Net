using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator {

  //NOTE: Note quite sure whether this code is correct. The indicator does not seem to respond very well to length changes.


  public class _AutocorrelationReversals : IndicatorObject {
    //Constants:
    public const int DEFAULT_HIGH_PASS_LENGTH = 48;
    public const int DEFAULT_LOW_PASS_LENGTH = 10;
    public const int DEFAULT_AVG_LENGTH = 3;

    //Properties:
    //[Input] public int HPLength { get; set; }
    [Input] public int LPLength { get; set; }
    [Input] public int AvgLength { get; set; }

    //Attributes:
    private int rampupBarCount;
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
    private VariableSeries<double> smoother;
    private IPlotObject PlotReversal;

    public _AutocorrelationReversals(object _ctx) : base(_ctx) { }

    protected override void Create() {
      //setup attribute defaults
      //HPLength = DEFAULT_HIGH_PASS_LENGTH;
      LPLength = DEFAULT_LOW_PASS_LENGTH;
      AvgLength = DEFAULT_AVG_LENGTH;
      smoother = new VariableSeries<double>(this);

      //create plot components
      PlotReversal = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
    }

    protected override void StartCalc() {
      //validate inputs
      //if (HPLength < AvgLength)
      //  throw new Exception("High-pass length must be a positive integer greater than averaging length.");

      if (LPLength <= 0)
        throw new Exception("Low-pass length must be a positive integer.");

      if (AvgLength < 0)
        throw new Exception("Averaging length must be zero or a positive integer.");

      //initialize values
      highPass0 = 0;
      highPass1 = 0;
      highPass2 = 0;

      //compute once-off coefficients
      alpha1 = (Math.Cos(0.707 * 2 * Math.PI / DEFAULT_HIGH_PASS_LENGTH) + Math.Sin(0.707 * 2 * Math.PI / DEFAULT_HIGH_PASS_LENGTH) - 1) / Math.Cos(0.707 * 2 * Math.PI / DEFAULT_HIGH_PASS_LENGTH);
      one_min_alpha1 = 1 - alpha1;
      a1 = Math.Exp(-1.414 * Math.PI) / LPLength;
      b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / LPLength);
      c2 = b1;
      c3 = -a1 * a1;
      c1 = 1 - c2 - c3;

      //compute rampup number of bars before indicator can be output
      rampupBarCount = AvgLength + DEFAULT_HIGH_PASS_LENGTH + LPLength + 3;    //+3 for smoothing count for high-pass filter and super smoother
    }

    protected override void CalcBar() {
      if (Bars.CurrentBar > 5) {
        //apply high pass filter
        highPass0 = (one_min_alpha1 / 2) * (one_min_alpha1 / 2) * (Bars.Close[0] - 2 * Bars.Close[1] + Bars.Close[2]) + 2 * one_min_alpha1 * highPass1 - one_min_alpha1 * one_min_alpha1 * highPass2;
        //smoother high-pass output using super smoother
        smoother.Value = c1 * (highPass0 + highPass1) / 2 + c2 * smoother[1] + c3 * smoother[2];
      } else
        smoother.Value = 0;

      //save look back memory values for next bar computation
      highPass2 = highPass1;
      highPass1 = highPass0;

      if (Bars.CurrentBar > rampupBarCount) {
        //compute Pearson correlation for each value of lag
        double[,] correlation = new double[DEFAULT_HIGH_PASS_LENGTH + 1, 2];  //+1 because lag length below is zero based
        double m;
        double x;
        double y;
        double sx;
        double sy;
        double sxx;
        double syy;
        double sxy;

        for (int lag = 3; lag <= DEFAULT_HIGH_PASS_LENGTH; lag++) {
          correlation[lag, 1] = correlation[lag, 0];

          //set averaging length as m
          m = AvgLength;
          if (AvgLength == 0)
            m = lag;

          //initialize correlation sums
          sx = 0;
          sy = 0;
          sxx = 0;
          syy = 0;
          sxy = 0;

          //advance samples of both data streams and sum Pearson components
          for (int count = 0; count < m; count++) {
            x = smoother[count];
            y = smoother[count + lag];
            sx = sx + x;
            sy = sy + y;
            sxx = sxx + x * x;
            syy = syy + y * y;
            sxy = sxy + x * y;
          }

          //compute correlation for each value of lag
          if (((m * sxx - sx * sx) * (m * syy - sy * sy)) > 0)
            correlation[lag, 0] = (m * sxy - sx * sy) / Math.Sqrt((m * sxx - sx * sx) * (m * syy - sy * sy));

          //scale correlation to range between 0 and 1
          correlation[lag, 0] = 0.5 * (correlation[lag, 0] + 1);
        }

        int sumDeltas = 0;
        for (int lag = 3; lag <= DEFAULT_HIGH_PASS_LENGTH; lag++) {
          if ((correlation[lag, 0] > 0.5 && correlation[lag, 1] < 0.5) || (correlation[lag, 0] < 0.5 && correlation[lag, 1] > 0.5))
            sumDeltas++;
        }

        int reversal = 0;
        if (sumDeltas > (DEFAULT_HIGH_PASS_LENGTH / 2))
          reversal = 1;

        PlotReversal.Set(reversal);
      }
    }
  }
}