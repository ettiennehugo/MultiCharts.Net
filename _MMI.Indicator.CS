using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	public class _MMI : IndicatorObject {
		
		[Input] public int Length { get; set; }
		
		private Function.MMI _mmiFC;		
		
		public _MMI(object _ctx):base(_ctx){
			Length = 20;
		}
		
		private IPlotObject plot1;
		
		protected override void Create() {
			// create variable objects, function objects, plot objects etc.
			_mmiFC = new Function.MMI(this);
			plot1 = AddPlot(new PlotAttributes("MMI", EPlotShapes.Line, Color.Gray));
		}
		protected override void StartCalc() {
			// assign inputs 
			_mmiFC.Length = Length;
		}
		protected override void CalcBar(){
			// indicator logic 
			plot1.Set(_mmiFC.Value);
		}
	}
}