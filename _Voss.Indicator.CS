using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class _Voss : IndicatorObject {
    [Input] public int Period { get; set; }
    [Input] public int Predict { get; set; }

		public _Voss(object _ctx):base(_ctx){}

    private Function._Voss vossFC;
    private IPlotObject filterPlot;
    private IPlotObject vossPlot;

		protected override void Create() {
      // create variable objects, function objects, plot objects etc.
      Period = 20;
      Predict = 3;
      vossFC = new Function._Voss(this);
      filterPlot = AddPlot(new PlotAttributes("Filter", EPlotShapes.Line, Color.Red));
      vossPlot = AddPlot(new PlotAttributes("Voss", EPlotShapes.Line, Color.Blue));
		}

		protected override void StartCalc() {
      // assign inputs 
      vossFC.Period = Period;
      vossFC.Predict = Predict;
      vossFC.Price = Bars.Close;
		}

		protected override void CalcBar(){
      filterPlot.Set(vossFC.Filter);
			vossPlot.Set(vossFC.Value);
		}
	}
}