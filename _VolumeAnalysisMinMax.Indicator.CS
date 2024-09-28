using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using PowerLanguage.VolumeProfile;

namespace PowerLanguage.Indicator{
  [SameAsSymbol(true)]
	public class _VolumeAnalysisMinMax : IndicatorObject {

    //Attributes:
    private IPlotObject min;
    private IPlotObject max;

    public _VolumeAnalysisMinMax(object _ctx):base(_ctx){}
		
		protected override void Create() {
			// create variable objects, function objects, plot objects etc.
			min = AddPlot(new PlotAttributes("vaMin", EPlotShapes.Line, Color.Green));
      max = AddPlot(new PlotAttributes("vaMax", EPlotShapes.Line, Color.Red));
    }

    protected override void StartCalc() {
      VolumeProfile.EChanged += VolumeProfileOnChanged;
		}

		protected override void CalcBar(){
      int currentBar = Bars.FullSymbolData.Current - 1;
      IProfile volumeProfile = VolumeProfile.ItemForBar(currentBar);

      if (volumeProfile != null) {
        min.Set((double)volumeProfile.HighVAForBar(currentBar).Dbl);
        max.Set((double)volumeProfile.LowVAForBar(currentBar).Dbl);
      }
    }

    protected void VolumeProfileOnChanged(bool full) {
      if (full)
        ExecControl.Recalculate();
    }

  }
}