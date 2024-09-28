using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator {
  public class _Convolution : IndicatorObject {
    //Constants:
    public const int DEFAULT_SHORT_LENGTH = 40;
    public const int DEFAULT_LONG_LENGTH = 80;

    //Properties:
    [Input] public int ShortLength { get; set; }
    [Input] public int LongLength { get; set; }

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
    private VariableSeries<double> smoother;
    private int rampUpBarCount;

    private IPlotObject plot2;
    private IPlotObject plot3;
    private IPlotObject plot4;
    private IPlotObject plot5;
    private IPlotObject plot6;
    private IPlotObject plot7;
    private IPlotObject plot8;
    private IPlotObject plot9;
    private IPlotObject plot10;
    private IPlotObject plot11;
    private IPlotObject plot12;
    private IPlotObject plot13;
    private IPlotObject plot14;
    private IPlotObject plot15;
    private IPlotObject plot16;
    private IPlotObject plot17;
    private IPlotObject plot18;
    private IPlotObject plot19;
    private IPlotObject plot20;
    private IPlotObject plot21;
    private IPlotObject plot22;
    private IPlotObject plot23;
    private IPlotObject plot24;
    private IPlotObject plot25;
    private IPlotObject plot26;
    private IPlotObject plot27;
    private IPlotObject plot28;
    private IPlotObject plot29;
    private IPlotObject plot30;
    private IPlotObject plot31;
    private IPlotObject plot32;
    private IPlotObject plot33;
    private IPlotObject plot34;
    private IPlotObject plot35;
    private IPlotObject plot36;
    private IPlotObject plot37;
    private IPlotObject plot38;
    private IPlotObject plot39;
    private IPlotObject plot40;
    private IPlotObject plot41;
    private IPlotObject plot42;
    private IPlotObject plot43;
    private IPlotObject plot44;
    private IPlotObject plot45;
    private IPlotObject plot46;
    private IPlotObject plot47;
    private IPlotObject plot48;

    public _Convolution(object _ctx) : base(_ctx) { }

    protected override void Create() {
      ShortLength = DEFAULT_SHORT_LENGTH;
      LongLength = DEFAULT_LONG_LENGTH;
      smoother = new VariableSeries<double>(this);

      //create plot objects used for heatmap
      plot2 = AddPlot();
      plot3 = AddPlot();
      plot4 = AddPlot();
      plot5 = AddPlot();
      plot6 = AddPlot();
      plot7 = AddPlot();
      plot8 = AddPlot();
      plot9 = AddPlot();
      plot10 = AddPlot();
      plot11 = AddPlot();
      plot12 = AddPlot();
      plot13 = AddPlot();
      plot14 = AddPlot();
      plot15 = AddPlot();
      plot16 = AddPlot();
      plot17 = AddPlot();
      plot18 = AddPlot();
      plot19 = AddPlot();
      plot20 = AddPlot();
      plot21 = AddPlot();
      plot22 = AddPlot();
      plot23 = AddPlot();
      plot24 = AddPlot();
      plot25 = AddPlot();
      plot26 = AddPlot();
      plot27 = AddPlot();
      plot28 = AddPlot();
      plot29 = AddPlot();
      plot30 = AddPlot();
      plot31 = AddPlot();
      plot32 = AddPlot();
      plot33 = AddPlot();
      plot34 = AddPlot();
      plot35 = AddPlot();
      plot36 = AddPlot();
      plot37 = AddPlot();
      plot38 = AddPlot();
      plot39 = AddPlot();
      plot40 = AddPlot();
      plot41 = AddPlot();
      plot42 = AddPlot();
      plot43 = AddPlot();
      plot44 = AddPlot();
      plot45 = AddPlot();
      plot46 = AddPlot();
      plot47 = AddPlot();
      plot48 = AddPlot();
    }

    protected override void StartCalc() {
      //validate inputs
      if (ShortLength <= 0 || LongLength <= 0)
        throw new Exception("Short/long lengths for computing convolution must be positive integers.");

      if (ShortLength > LongLength)
        throw new Exception("Short length must be less than the long length for computing convolution.");

      //initialize values
      highPass0 = 0;
      highPass1 = 0;
      highPass2 = 0;

      //compute ramp up bar count for computations
      rampUpBarCount = ShortLength + LongLength;
      rampUpBarCount = rampUpBarCount >= 48 ? rampUpBarCount : 48;

      //compute once-off coefficients
      alpha1 = (Math.Cos(1.414 * 2 * Math.PI / LongLength) + Math.Sin(1.414 * 2 * Math.PI / LongLength) - 1) / Math.Cos(1.414 * 2 * Math.PI / LongLength);
      one_min_alpha1 = 1 - alpha1;
      a1 = Math.Exp(-1.414 * Math.PI) / ShortLength;
      b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / ShortLength);
      c2 = b1;
      c3 = -a1 * a1;
      c1 = 1 - c2 - c3;
    }

    protected override void CalcBar() {
      if (Bars.CurrentBar > 5) {
        //compute two-pole high pass filter value
        highPass0 = (one_min_alpha1 / 2) * (one_min_alpha1 / 2) * (Bars.Close[0] - 2 * Bars.Close[1] + Bars.Close[2]) + 2 * one_min_alpha1 * highPass1 - one_min_alpha1 * one_min_alpha1 * highPass2;
        //smoother high-pass output using super smoother
        smoother.Value = c1 * (highPass0 + highPass1) / 2 + c2 * smoother[1] + c3 * smoother[2];
      } else
        smoother.Value = 0;

      //save look back memory values for next bar computation
      highPass2 = highPass1;
      highPass1 = highPass0;

      //compute the Pearson correlation, slope and convolution for plot rendering
      double[] correlation = new double[48];
      double[] slope = new double[48];
      double[] convolution = new double[48];

      for (int n = 0; n < 48; n++) {
        double sx = 0;
        double sy = 0;
        double sxx = 0;
        double syy = 0;
        double sxy = 0;

        for (int ii = 0; ii < n; ii++) {
          double x = smoother[ii];
          double y = smoother[n - ii];
          sx = sx + x;
          sy = sy + y;
          sxx = sxx + x * x;
          syy = syy + y * y;
          sxy = sxy + x * y;
        }

        if ((n * sxx - sx * sx) * (n * syy - sy * sy) > 0)
          correlation[n] = (n * sxy - sx * sy) / Math.Sqrt((n * sxx - sx * sx) * (n * syy - sy * sy));

        slope[n] = 1;
        if (smoother[n / 2] < smoother[0])
          slope[n] = -1;

        convolution[n] = ((1 + Math.Exp(3 * correlation[n]) - 1) / (Math.Exp(3 * correlation[n]) + 1)) / 2;
      }

      //plot the convolution as heat-map
      int red = 0;
      int green = 0; 

      for (int n = 2; n < 48; n++) {
        //compute the shade of the heat-map based on computed slope
        if (slope[n] > 0) {
          red = (int)(255 * convolution[n]);
          green = 0;
        } else if (slope[n] < 0) {
          red = 0;
          green = (int)(255 * convolution[n]);
        }

        //render heat-map components
        //NOTE: Heat-map is rendered with a 4-bar lag to align origin points of "plumes" with the actual reversal point.
        if (n == 2)
          plot2.Set(4, 2, Color.FromArgb(red, green, 0), 4);
        if (n == 3)
          plot3.Set(4, 3, Color.FromArgb(red, green, 0), 4);
        if (n == 4)
          plot4.Set(4, 4, Color.FromArgb(red, green, 0), 4);
        if (n == 5)
          plot5.Set(4, 5, Color.FromArgb(red, green, 0), 4);
        if (n == 6)
          plot6.Set(4, 6, Color.FromArgb(red, green, 0), 4);
        if (n == 7)
          plot7.Set(4, 7, Color.FromArgb(red, green, 0), 4);
        if (n == 8)
          plot8.Set(4, 8, Color.FromArgb(red, green, 0), 4);
        if (n == 9)
          plot9.Set(4, 9, Color.FromArgb(red, green, 0), 4);
        if (n == 10)
          plot10.Set(4, 10, Color.FromArgb(red, green, 0), 4);
        if (n == 11)
          plot11.Set(4, 11, Color.FromArgb(red, green, 0), 4);
        if (n == 12)
          plot12.Set(4, 12, Color.FromArgb(red, green, 0), 4);
        if (n == 13)
          plot13.Set(4, 13, Color.FromArgb(red, green, 0), 4);
        if (n == 14)
          plot14.Set(4, 14, Color.FromArgb(red, green, 0), 4);
        if (n == 15)
          plot15.Set(4, 15, Color.FromArgb(red, green, 0), 4);
        if (n == 16)
          plot16.Set(4, 16, Color.FromArgb(red, green, 0), 4);
        if (n == 17)
          plot17.Set(4, 17, Color.FromArgb(red, green, 0), 4);
        if (n == 18)
          plot18.Set(4, 18, Color.FromArgb(red, green, 0), 4);
        if (n == 19)
          plot19.Set(4, 19, Color.FromArgb(red, green, 0), 4);
        if (n == 20)
          plot20.Set(4, 20, Color.FromArgb(red, green, 0), 4);
        if (n == 21)
          plot21.Set(4, 21, Color.FromArgb(red, green, 0), 4);
        if (n == 22)
          plot22.Set(4, 22, Color.FromArgb(red, green, 0), 4);
        if (n == 23)
          plot23.Set(4, 23, Color.FromArgb(red, green, 0), 4);
        if (n == 24)
          plot24.Set(4, 24, Color.FromArgb(red, green, 0), 4);
        if (n == 25)
          plot25.Set(4, 25, Color.FromArgb(red, green, 0), 4);
        if (n == 26)
          plot26.Set(4, 26, Color.FromArgb(red, green, 0), 4);
        if (n == 27)
          plot27.Set(4, 27, Color.FromArgb(red, green, 0), 4);
        if (n == 28)
          plot28.Set(4, 28, Color.FromArgb(red, green, 0), 4);
        if (n == 29)
          plot29.Set(4, 29, Color.FromArgb(red, green, 0), 4);
        if (n == 30)
          plot30.Set(4, 30, Color.FromArgb(red, green, 0), 4);
        if (n == 31)
          plot31.Set(4, 31, Color.FromArgb(red, green, 0), 4);
        if (n == 32)
          plot32.Set(4, 32, Color.FromArgb(red, green, 0), 4);
        if (n == 33)
          plot33.Set(4, 33, Color.FromArgb(red, green, 0), 4);
        if (n == 34)
          plot34.Set(4, 34, Color.FromArgb(red, green, 0), 4);
        if (n == 35)
          plot35.Set(4, 35, Color.FromArgb(red, green, 0), 4);
        if (n == 36)
          plot36.Set(4, 36, Color.FromArgb(red, green, 0), 4);
        if (n == 37)
          plot37.Set(4, 37, Color.FromArgb(red, green, 0), 4);
        if (n == 38)
          plot38.Set(4, 38, Color.FromArgb(red, green, 0), 4);
        if (n == 39)
          plot39.Set(4, 39, Color.FromArgb(red, green, 0), 4);
        if (n == 40)
          plot40.Set(4, 40, Color.FromArgb(red, green, 0), 4);
        if (n == 41)
          plot41.Set(4, 41, Color.FromArgb(red, green, 0), 4);
        if (n == 42)
          plot42.Set(4, 42, Color.FromArgb(red, green, 0), 4);
        if (n == 43)
          plot43.Set(4, 43, Color.FromArgb(red, green, 0), 4);
        if (n == 44)
          plot44.Set(4, 44, Color.FromArgb(red, green, 0), 4);
        if (n == 45)
          plot45.Set(4, 45, Color.FromArgb(red, green, 0), 4);
        if (n == 46)
          plot46.Set(4, 46, Color.FromArgb(red, green, 0), 4);
        if (n == 47)
          plot47.Set(4, 47, Color.FromArgb(red, green, 0), 4);
        if (n == 48)
          plot48.Set(4, 48, Color.FromArgb(red, green, 0), 4);
      }
    }
  }
}