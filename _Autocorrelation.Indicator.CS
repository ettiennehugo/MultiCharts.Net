using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator {
/// <summary>
/// Computes a heatmap based on the correlationsions found between bars using differing lag periods. A green value on the heatmap indicates a strong positive
/// correlationion between bars on that length of lag on the chart while red indicates strong anticorrelation with yellow indicating no correlation. For more
/// information see Cycle Analytics for Traders p. 91 by John F. Ehlers.
/// </summary>
  public class _Autocorrelation : IndicatorObject {
    //Constant:
    public const int DEFAULT_AVG_LENGTH = 20;
    public const int DEFAULT_HIGH_PASS_LENGTH = 48;
    public const int DEFAULT_SMOOTHING_LENGTH = 10;

    //Properties:
    [Input] public int AvgLength { get; set; }   //averaging length over which correlation is computed 

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

    public _Autocorrelation(object _ctx) : base(_ctx) { }

    protected override void Create() {
      AvgLength = DEFAULT_AVG_LENGTH;
      smoother = new VariableSeries<double>(this);

      //create plot objects used for heatmap
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
      //initialize values
      highPass0 = 0;
      highPass1 = 0;
      highPass2 = 0;

      //compute once-off coefficients
      alpha1 = (Math.Cos(0.707 * 2 * Math.PI / DEFAULT_HIGH_PASS_LENGTH) + Math.Sin(0.707 * 2 * Math.PI / DEFAULT_HIGH_PASS_LENGTH) - 1) / Math.Cos(0.707 * 2 * Math.PI / DEFAULT_HIGH_PASS_LENGTH);
      one_min_alpha1 = 1 - alpha1;
      a1 = Math.Exp(-1.414 * Math.PI) / DEFAULT_SMOOTHING_LENGTH;
      b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / DEFAULT_SMOOTHING_LENGTH);
      c2 = b1;
      c3 = -a1 * a1;
      c1 = 1 - c2 - c3;

      //compute the rampupBarCount after which we can compute the indicator value
      rampupBarCount = AvgLength + DEFAULT_HIGH_PASS_LENGTH + 5;    //+5 for smoothing count for high-pass filter and super smoother
    }

    protected override void CalcBar() {
      double m;
      double x;
      double y;
      double sx;
      double sy;
      double sxx;
      double syy;
      double sxy;

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
        double[] correlation = new double[DEFAULT_HIGH_PASS_LENGTH + 1];  //+48 for high pass filter and +1 because lag length below is zero based
        for (int lag = 0; lag <= DEFAULT_HIGH_PASS_LENGTH; lag++) {
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
          correlation[lag] = 0;
          if (((m * sxx - sx * sx) * (m * syy - sy * sy)) > 0)
            correlation[lag] = (m * sxy - sx * sy) / Math.Sqrt((m * sxx - sx * sx) * (m * syy - sy * sy));

          //scale each correlation to range between 0 to 1 for heat map rendering
          correlation[lag] = 0.5 * (correlation[lag] + 1);
        }

        //render heatmap values
        int red = 255;
        int green = 255;

        for (int lag = 3; lag <= DEFAULT_HIGH_PASS_LENGTH; lag++) {
          //reset red/green values
          red = 255;
          green = 255;

          //compute heatmap color based on correlation
          if (correlation[lag] > 0.5)
            red = (int)(255 * (2 - 2 * correlation[lag]));    //vary color from green to yellow
          else
            green = (int)(2 * 255 * correlation[lag]);        //vary color from yellow to red

          if (lag == 3)
            plot3.Set(3, Color.FromArgb(red, green, 0), 4);
          if (lag == 4)
            plot4.Set(4, Color.FromArgb(red, green, 0), 4);
          if (lag == 5)
            plot5.Set(5, Color.FromArgb(red, green, 0), 4);
          if (lag == 6)
            plot6.Set(6, Color.FromArgb(red, green, 0), 4);
          if (lag == 7)
            plot7.Set(7, Color.FromArgb(red, green, 0), 4);
          if (lag == 8)
            plot8.Set(8, Color.FromArgb(red, green, 0), 4);
          if (lag == 9)
            plot9.Set(9, Color.FromArgb(red, green, 0), 4);
          if (lag == 10)
            plot10.Set(10, Color.FromArgb(red, green, 0), 4);
          if (lag == 11)
            plot11.Set(11, Color.FromArgb(red, green, 0), 4);
          if (lag == 12)
            plot12.Set(12, Color.FromArgb(red, green, 0), 4);
          if (lag == 13)
            plot13.Set(13, Color.FromArgb(red, green, 0), 4);
          if (lag == 14)
            plot14.Set(14, Color.FromArgb(red, green, 0), 4);
          if (lag == 15)
            plot15.Set(15, Color.FromArgb(red, green, 0), 4);
          if (lag == 16)
            plot16.Set(16, Color.FromArgb(red, green, 0), 4);
          if (lag == 17)
            plot17.Set(17, Color.FromArgb(red, green, 0), 4);
          if (lag == 18)
            plot18.Set(18, Color.FromArgb(red, green, 0), 4);
          if (lag == 19)
            plot19.Set(19, Color.FromArgb(red, green, 0), 4);
          if (lag == 20)
            plot20.Set(20, Color.FromArgb(red, green, 0), 4);
          if (lag == 21)
            plot21.Set(21, Color.FromArgb(red, green, 0), 4);
          if (lag == 22)
            plot22.Set(22, Color.FromArgb(red, green, 0), 4);
          if (lag == 23)
            plot23.Set(23, Color.FromArgb(red, green, 0), 4);
          if (lag == 24)
            plot24.Set(24, Color.FromArgb(red, green, 0), 4);
          if (lag == 25)
            plot25.Set(25, Color.FromArgb(red, green, 0), 4);
          if (lag == 26)
            plot26.Set(26, Color.FromArgb(red, green, 0), 4);
          if (lag == 27)
            plot27.Set(27, Color.FromArgb(red, green, 0), 4);
          if (lag == 28)
            plot28.Set(28, Color.FromArgb(red, green, 0), 4);
          if (lag == 29)
            plot29.Set(29, Color.FromArgb(red, green, 0), 4);
          if (lag == 30)
            plot30.Set(30, Color.FromArgb(red, green, 0), 4);
          if (lag == 31)
            plot31.Set(31, Color.FromArgb(red, green, 0), 4);
          if (lag == 32)
            plot32.Set(32, Color.FromArgb(red, green, 0), 4);
          if (lag == 33)
            plot33.Set(33, Color.FromArgb(red, green, 0), 4);
          if (lag == 34)
            plot34.Set(34, Color.FromArgb(red, green, 0), 4);
          if (lag == 35)
            plot35.Set(35, Color.FromArgb(red, green, 0), 4);
          if (lag == 36)
            plot36.Set(36, Color.FromArgb(red, green, 0), 4);
          if (lag == 37)
            plot37.Set(37, Color.FromArgb(red, green, 0), 4);
          if (lag == 38)
            plot38.Set(38, Color.FromArgb(red, green, 0), 4);
          if (lag == 39)
            plot39.Set(39, Color.FromArgb(red, green, 0), 4);
          if (lag == 40)
            plot40.Set(40, Color.FromArgb(red, green, 0), 4);
          if (lag == 41)
            plot41.Set(41, Color.FromArgb(red, green, 0), 4);
          if (lag == 42)
            plot42.Set(42, Color.FromArgb(red, green, 0), 4);
          if (lag == 43)
            plot43.Set(43, Color.FromArgb(red, green, 0), 4);
          if (lag == 44)
            plot44.Set(44, Color.FromArgb(red, green, 0), 4);
          if (lag == 45)
            plot45.Set(45, Color.FromArgb(red, green, 0), 4);
          if (lag == 46)
            plot46.Set(46, Color.FromArgb(red, green, 0), 4);
          if (lag == 47)
            plot47.Set(47, Color.FromArgb(red, green, 0), 4);
          if (lag == 48)
            plot48.Set(48, Color.FromArgb(red, green, 0), 4);
        }
      }
    }
  }
}
