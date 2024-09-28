using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
  /// <summary>
  /// Better sinewave indicator as discussed in Cycle Analytics for Trader by John Ehlers p. 159. This indicator is very useful in identifying pullbacks in a trend.
  /// </summary>
  /// <parameter>Duration - Controls the maximum length of an indicated position.</parameter>
  public class _SineWave : IndicatorObject {
    //Constants:
    public const double DEFAULT_LONG_LEVEL = 0.9;
    public const double DEFAULT_SHORT_LEVEL = -0.9;

    //Properties:
    [Input] public int Duration { get; set; }
    [Input] public double LongLevel { get; set; }
    [Input] public double ShortLevel { get; set; }

    //Attributes:
    private SineWave sineWave;
    private IPlotObject PlotSinewave;
    private IPlotObject PlotLongLevel;
    private IPlotObject PlotShortLevel;

    public _SineWave(object _ctx):base(_ctx){}

	protected override void Create() {
      Duration = SineWave.DEFAULT_DURATION;
      LongLevel = DEFAULT_LONG_LEVEL;
      ShortLevel = DEFAULT_SHORT_LEVEL;
      sineWave = new SineWave(this);
      PlotSinewave = AddPlot(new PlotAttributes("SW", EPlotShapes.Line, Color.White));
      PlotLongLevel = AddPlot(new PlotAttributes("LL", EPlotShapes.RightTick, Color.Green));
      PlotShortLevel = AddPlot(new PlotAttributes("SL", EPlotShapes.RightTick, Color.Red));
    }

    protected override void StartCalc() {
      sineWave.Duration = Duration;
    }

    protected override void CalcBar() {
      PlotLongLevel.Set(LongLevel);
      PlotShortLevel.Set(ShortLevel);
      PlotSinewave.Set(sineWave.Value);
		}
	}
}