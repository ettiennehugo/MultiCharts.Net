using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
  /// <summary>
  /// Decycler Oscillator as discussed by John Ehlers, uses the difference between two decycler functions to create an oscillator. When the
  /// oscillator crosses the zero line it indicates a change in trend.
  /// </summary>
	public class _DecyclerOsc : IndicatorObject {
    //Constants:
    public const double DEFAULT_SHORT_CUTOFF = 30;
    public const double DEFAULT_LONG_CUTOFF = 60;

    //Properties:
    [Input] public double ShortCutoff { get; set; }
    [Input] public double LongCutoff { get; set; }

    //Attributes:
    private IPlotObject PlotZero;
    private IPlotObject PlotOsc;
    private Decycler ShortDecyler;
    private Decycler LongDecycler;

    public _DecyclerOsc(object _ctx):base(_ctx) {}

		protected override void Create() {
      ShortCutoff = DEFAULT_SHORT_CUTOFF;
      LongCutoff = DEFAULT_LONG_CUTOFF;
      PlotZero = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.LightGray));
      PlotOsc = AddPlot(new PlotAttributes("DOsc", EPlotShapes.Line, Color.Red));
      ShortDecyler = new Decycler(this);
      LongDecycler = new Decycler(this);
		}

		protected override void StartCalc() {
      //validate inputs
      if (ShortCutoff > LongCutoff)
        throw new Exception("Short cut off frequency must be less than long cut off frequency.");

      //setup the decyclers
      ShortDecyler.Cutoff = ShortCutoff;
      ShortDecyler.Price = Bars.Close;
      LongDecycler.Cutoff = LongCutoff;
      LongDecycler.Price = Bars.Close;
		}

		protected override void CalcBar() {
      PlotZero.Set(0);
      PlotOsc.Set(LongDecycler[0] - ShortDecyler[0]);
		}
	}
}