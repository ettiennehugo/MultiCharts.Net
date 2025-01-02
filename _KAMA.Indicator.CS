using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
    [SameAsSymbol]
	public class _KAMA : IndicatorObject {
        //Inputs:
        [Input] public int FastLength { get; set; }
        [Input] public int SlowLength { get; set; }
        [Input] public int EfficiencyRatioLength { get; set; }

        //Attributes:
        private Function._KAMA _kama;
        private IPlotObject _plot1;

        public _KAMA(object _ctx):base(_ctx){
            FastLength = 2;
            SlowLength = 30;
            EfficiencyRatioLength = 10;
        }
		
        protected override void Create() {
			_plot1 = AddPlot(new PlotAttributes("KAMA", EPlotShapes.Line, Color.Red));
      _kama = new Function._KAMA(this);
		}
		protected override void StartCalc() {
            _kama.FastLength = FastLength;
            _kama.SlowLength = SlowLength;
            _kama.EfficiencyRatioLength = EfficiencyRatioLength;
			_kama.Price = Bars.Close;
		}

		protected override void CalcBar(){
			_plot1.Set(_kama[0]);
		}
	}
}