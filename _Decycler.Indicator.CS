using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
  /// <summary>
  /// Uses John Ehler's decycler to remove cycle components out of a price series.
  /// </summary>
  [SameAsSymbol]
  public class _Decycler : IndicatorObject {

    //Properties:
    [Input] public double Cutoff { get; set; }

    //Attributes:
    private IPlotObject Plot1;
    private Decycler Decycler;

    public _Decycler(object _ctx):base(_ctx){}

		protected override void Create() {
      Cutoff = Decycler.DEFAULT_CUTOFF;
      Decycler = new Decycler(this);
      Plot1 = AddPlot(new PlotAttributes("DCyc", EPlotShapes.Line, Color.Red));
		}

		protected override void StartCalc() {
      Decycler.Cutoff = Cutoff;
      Decycler.Price = Bars.Close;
		}

		protected override void CalcBar(){
      Plot1.Set(Decycler[0]);
		}
	}
}