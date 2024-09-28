using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator {
  public class _AutocorrelationPeriodogram : IndicatorObject {
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
    private double maxPwr;

    private IPlotObject plotDominantCycle;
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

    public _AutocorrelationPeriodogram(object _ctx) : base(_ctx) { }

    protected override void Create() {
      AvgLength = DEFAULT_AVG_LENGTH;
      smoother = new VariableSeries<double>(this);
      maxPwr = 0;

      //create plot objects used for heatmap
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
      plotDominantCycle = AddPlot(new PlotAttributes("DC", EPlotShapes.Line, Color.Blue, Color.Black, 3, EPlotStyle.Solid, true));
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
      rampupBarCount = AvgLength + DEFAULT_HIGH_PASS_LENGTH + 3;    //+3 for smoothing count for high-pass filter and super smoother
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
        double[] correlation = new double[DEFAULT_HIGH_PASS_LENGTH + 1];  //+1 because lag length below is zero based
        double m;
        double x;
        double y;
        double sx;
        double sy;
        double sxx;
        double syy;
        double sxy;
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
        }

        //apply discrete Fourier transform on computed correlation values
        double[] cosinePart = new double[DEFAULT_HIGH_PASS_LENGTH + 1];
        double[] sinePart = new double[DEFAULT_HIGH_PASS_LENGTH + 1];
        double[] squareSum = new double[DEFAULT_HIGH_PASS_LENGTH + 1];
        double[,] r = new double[DEFAULT_HIGH_PASS_LENGTH + 1, 2];
        double[] pwr = new double[DEFAULT_HIGH_PASS_LENGTH + 1];

        for (int period = 10; period <= DEFAULT_HIGH_PASS_LENGTH; period++) {
          cosinePart[period] = 0;
          sinePart[period] = 0;

          for (int n = 3; n <= DEFAULT_HIGH_PASS_LENGTH; n++) {
            cosinePart[period] = cosinePart[period] + correlation[n] * Math.Cos((n * 2.1745 * Math.PI) / period);     //2.1745 used since original code uses 370 degrees which is about 2.1745PI
            sinePart[period] = sinePart[period] + correlation[n] * Math.Sin((n * 2.1745 * Math.PI) / period);         //2.1745 used since origianl code uses 370 degrees which is about 2.1745PI
          }

          squareSum[period] = cosinePart[period] * cosinePart[period] + sinePart[period] * sinePart[period];
        }

        for (int period = 10; period <= DEFAULT_HIGH_PASS_LENGTH; period++) {
          r[period, 1] = r[period, 0];
          r[period, 0] = 0.2 * squareSum[period] + 0.8 * r[period, 1];
        }

        //find maximum power level for normilazation
        maxPwr = 0.995 * maxPwr;
        for (int period = 10; period <= DEFAULT_HIGH_PASS_LENGTH; period++)
          if (r[period, 0] > maxPwr)
            maxPwr = r[period, 0];

        for (int period = 3; period <= DEFAULT_HIGH_PASS_LENGTH; period++)
          pwr[period] = r[period, 0] / maxPwr;

        //compute dominant cycle using the center-of-gravity of the computed power spectrum
        double spx = 0;
        double sp = 0;
        double dominantCycle = 0;

        for (int period = 0; period <= DEFAULT_HIGH_PASS_LENGTH; period++) {
          if (pwr[period] >= 0.5) {
            spx = spx + period * pwr[period];
            sp = sp + pwr[period];
          }
        }

        if (sp != 0)
          dominantCycle = spx / sp;

        plotDominantCycle.Set(dominantCycle);

        //optional: increase display resolution by raising the normilized power to higher power values
        //for (int period = 10; period <= DEFAULT_HIGH_PASS_LENGTH; period++)
        //  pwr[period] = Math.Pow(pwr[period], 2);     //power value is selected arbitrarily as 2

        //plot the heat map
        int red = 255;
        int green = 255;

        for (int period = 10; period <= DEFAULT_HIGH_PASS_LENGTH; period++) {
          //compute heatmap color based on correlation
          if (pwr[period] > 0.5) {
            red = 255;
            green = (int)(255 * (2 * pwr[period] - 1));
          } else {
            red = (int)(2 * 255 * pwr[period]);
            green = 0;
          }

          if (period == 10)
            plot10.Set(10, Color.FromArgb(red, green, 0), 4);
          if (period == 11)
            plot11.Set(11, Color.FromArgb(red, green, 0), 4);
          if (period == 12)
            plot12.Set(12, Color.FromArgb(red, green, 0), 4);
          if (period == 13)
            plot13.Set(13, Color.FromArgb(red, green, 0), 4);
          if (period == 14)
            plot14.Set(14, Color.FromArgb(red, green, 0), 4);
          if (period == 15)
            plot15.Set(15, Color.FromArgb(red, green, 0), 4);
          if (period == 16)
            plot16.Set(16, Color.FromArgb(red, green, 0), 4);
          if (period == 17)
            plot17.Set(17, Color.FromArgb(red, green, 0), 4);
          if (period == 18)
            plot18.Set(18, Color.FromArgb(red, green, 0), 4);
          if (period == 19)
            plot19.Set(19, Color.FromArgb(red, green, 0), 4);
          if (period == 20)
            plot20.Set(20, Color.FromArgb(red, green, 0), 4);
          if (period == 21)
            plot21.Set(21, Color.FromArgb(red, green, 0), 4);
          if (period == 22)
            plot22.Set(22, Color.FromArgb(red, green, 0), 4);
          if (period == 23)
            plot23.Set(23, Color.FromArgb(red, green, 0), 4);
          if (period == 24)
            plot24.Set(24, Color.FromArgb(red, green, 0), 4);
          if (period == 25)
            plot25.Set(25, Color.FromArgb(red, green, 0), 4);
          if (period == 26)
            plot26.Set(26, Color.FromArgb(red, green, 0), 4);
          if (period == 27)
            plot27.Set(27, Color.FromArgb(red, green, 0), 4);
          if (period == 28)
            plot28.Set(28, Color.FromArgb(red, green, 0), 4);
          if (period == 29)
            plot29.Set(29, Color.FromArgb(red, green, 0), 4);
          if (period == 30)
            plot30.Set(30, Color.FromArgb(red, green, 0), 4);
          if (period == 31)
            plot31.Set(31, Color.FromArgb(red, green, 0), 4);
          if (period == 32)
            plot32.Set(32, Color.FromArgb(red, green, 0), 4);
          if (period == 33)
            plot33.Set(33, Color.FromArgb(red, green, 0), 4);
          if (period == 34)
            plot34.Set(34, Color.FromArgb(red, green, 0), 4);
          if (period == 35)
            plot35.Set(35, Color.FromArgb(red, green, 0), 4);
          if (period == 36)
            plot36.Set(36, Color.FromArgb(red, green, 0), 4);
          if (period == 37)
            plot37.Set(37, Color.FromArgb(red, green, 0), 4);
          if (period == 38)
            plot38.Set(38, Color.FromArgb(red, green, 0), 4);
          if (period == 39)
            plot39.Set(39, Color.FromArgb(red, green, 0), 4);
          if (period == 40)
            plot40.Set(40, Color.FromArgb(red, green, 0), 4);
          if (period == 41)
            plot41.Set(41, Color.FromArgb(red, green, 0), 4);
          if (period == 42)
            plot42.Set(42, Color.FromArgb(red, green, 0), 4);
          if (period == 43)
            plot43.Set(43, Color.FromArgb(red, green, 0), 4);
          if (period == 44)
            plot44.Set(44, Color.FromArgb(red, green, 0), 4);
          if (period == 45)
            plot45.Set(45, Color.FromArgb(red, green, 0), 4);
          if (period == 46)
            plot46.Set(46, Color.FromArgb(red, green, 0), 4);
          if (period == 47)
            plot47.Set(47, Color.FromArgb(red, green, 0), 4);
          if (period == 48)
            plot48.Set(48, Color.FromArgb(red, green, 0), 4);
        }


      }
    }
  }
}