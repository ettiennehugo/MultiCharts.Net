using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using PowerLanguage.VolumeProfile;
using System.Collections.Generic;

namespace PowerLanguage.Indicator {
  [SameAsSymbol(true)]
  public class _VolumeCenterOfGravity : IndicatorObject {
    //Properties:
    [Input] public int Length { get; set; }    //volume profiles to include in analysis

    //Attributes:
    private IPlotObject _vcogPlot;
    private double _vcog;

    public _VolumeCenterOfGravity(object _ctx) : base(_ctx) { }

    protected override void Create() {
      Length = 10;
      _vcogPlot = AddPlot(new PlotAttributes("VCOG", EPlotShapes.Line, Color.Red));
    }

    protected override void StartCalc() {
      VolumeProfile.EChanged += VolumeProfileChange;
      _vcog = 0.0;
    }

    protected override void CalcBar() {
      //compute the total price value for the POC's
      List<IProfile> profileList = new List<IProfile>(Length);
      IProfile prevProfile = null;
      IProfile profile = null;
      int barIndex = 0;

      while (profileList.Count < Length) {
        profile = VolumeProfile.ItemForBar(barIndex);

        if (prevProfile == null || !prevProfile.Equals(profile)) {
          prevProfile = profile;
          profileList.Add(profile);
        }

        barIndex++;

        if (barIndex >= Bars.CurrentBar)
          break;
      }

      for (int i = 0; i < Length; i++) {
        profile = profileList[i];
        _vcog += profile.POC.Price.Dbl;
      }

      _vcog = _vcog / Length;

      //plot the volume at center of gravity
      _vcogPlot.Set(_vcog);
    }

    protected void VolumeProfileChange(bool full) {
      if (full)
        ExecControl.Recalculate();

    }
  }
}