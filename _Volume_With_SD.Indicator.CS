using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

//TODO: WHEN WORKING ON INTRADAY CHARTS this indicator is not correct, it should take into account the session in which it's operating and compute it's statistical indicators
//      on the sessions and potentially the session overlaps so as to correctly compare apples to apples concerning volume.
//      - Maybe add a setting where you can literally set this intraday setting to the following:
//        - Standard - compute volume plots the way it is
//        - Session - compute volumes based on the earliest session (does not take into account session overlaps).
//        - Session with overlap - computes volumes for the session and also take into account the session overlaps.


namespace PowerLanguage.Indicator {
  public class _Volume_With_SD : IndicatorObject {

    [Input] public int Length { get; set; }
    [Input] public double Level1 { get; set; }   //Level 1 factor for +/- standard deviation.
    [Input] public double Level2 { get; set; }   //Level 2 factor for +/- standard deviation.
    [Input] public double Level3 { get; set; }   //Level 3 factor for +/- standard deviation.

    private VolumeWithSD volumeWithSD; // <== Need to add a time filter to this function!!! This will enable computing volume per session and overlap.
    private IPlotObject volumePlot;
    private IPlotObject avgPlus1SD;
    private IPlotObject avgPlus2SD;
    private IPlotObject avgPlus3SD;
    private IPlotObject avg;
    private IPlotObject avgMinus1SD;
    private IPlotObject avgMinus2SD;
    private IPlotObject avgMinus3SD;

    public _Volume_With_SD(object _ctx) : base(_ctx) { }

    protected override void Create() {
      volumeWithSD = new VolumeWithSD(this);
      Length = VolumeWithSD.DEFAULT_LENGTH;
      Level1 = VolumeWithSD.DEFAULT_LEVEL1;
      Level2 = VolumeWithSD.DEFAULT_LEVEL2;
      Level3 = VolumeWithSD.DEFAULT_LEVEL3;

      volumePlot = AddPlot(new PlotAttributes("Vol", EPlotShapes.Histogram, Color.White, Color.Empty, 3, 0, true));
      avgPlus3SD = AddPlot(new PlotAttributes("+3SD", EPlotShapes.Line, Color.FromArgb(128, 196, 128)));
      avgPlus2SD = AddPlot(new PlotAttributes("+2SD", EPlotShapes.Line, Color.FromArgb(64, 196, 64)));
      avgPlus1SD = AddPlot(new PlotAttributes("+1SD", EPlotShapes.Line, Color.FromArgb(32, 255, 32)));
      avg = AddPlot(new PlotAttributes("Avg", EPlotShapes.Line, Color.LightGray, Color.LightGray, 2, 0, true));
      avgMinus1SD = AddPlot(new PlotAttributes("-1SD", EPlotShapes.Line, Color.FromArgb(196, 128, 128)));
      avgMinus2SD = AddPlot(new PlotAttributes("-2SD", EPlotShapes.Line, Color.FromArgb(196, 64, 64)));
      avgMinus3SD = AddPlot(new PlotAttributes("-3SD", EPlotShapes.Line, Color.FromArgb(255, 32, 32)));
    }

    protected override void StartCalc() {
      volumeWithSD.Length = Length;
      volumeWithSD.Volume = Bars.TrueVolume();
      volumeWithSD.Level1 = Level1;
      volumeWithSD.Level2 = Level2;
      volumeWithSD.Level3 = Level3;
    }

    protected override void CalcBar() {
      volumePlot.Set(volumeWithSD.Volume.Value);

      if (Bars.Open[0] < Bars.Close[0])
        volumePlot.Colors[0] = Color.LawnGreen;
      else if (Bars.Open[0] > Bars.Close[0])
        volumePlot.Colors[0] = Color.IndianRed;

      avgPlus3SD.Set(volumeWithSD.AvgPlusLevel3.Value);
      avgPlus2SD.Set(volumeWithSD.AvgPlusLevel2.Value);
      avgPlus1SD.Set(volumeWithSD.AvgPlusLevel1.Value);
      avg.Set(volumeWithSD.Value);
      avgMinus1SD.Set(volumeWithSD.AvgMinusLevel1.Value);
      avgMinus2SD.Set(volumeWithSD.AvgMinusLevel2.Value);
      avgMinus3SD.Set(volumeWithSD.AvgMinusLevel3.Value);
    }

  }
}