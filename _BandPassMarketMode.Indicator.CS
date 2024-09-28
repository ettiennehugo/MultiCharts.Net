using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	/// <summary>
	/// Uses a band pass filter to isolate the market mode, trending vs consolidating. When the signal line moves outside the upper or lower
	/// band lines it indicates that the market is trending up or down, respectively. See Empirical Mode Decomposition PDF by John Ehlers.
	/// </summary>
	public class _BandPassMarketMode : IndicatorObject {
		//Properties:
	    [Input]public int Period { get; set; }
	    [Input]public double Delta { get; set; }
		[Input]public double Fraction { get; set; }		//scaling factor for the upper/lower bands used to compute consolidation boundaries

		//Attributes:
		private IPlotObject PlotUpperBand;
		private IPlotObject PlotLowerBand;
    	private IPlotObject PlotTrend;
		private VariableSeries<double> Peak;
		private VariableSeries<double> Valley;
        private BandPass Filter;
		private AverageFC Average;
		
		public _BandPassMarketMode(object _ctx):base(_ctx){}

		protected override void Create() {
	      Period = BandPass.DEFAULT_PERIOD;
	      Delta = BandPass.DEFAULT_DELTA;
		  Fraction = 0.1;

		  PlotUpperBand = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.Green));
		  PlotLowerBand = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.Red));
	      PlotTrend = AddPlot(new PlotAttributes("MM", EPlotShapes.Line, Color.LightGray));
		  Peak = new VariableSeries<double>(this);
	      Valley = new VariableSeries<double>(this);
	      Filter = new BandPass(this);
		  Average = new AverageFC(this);
		}
		
		protected override void StartCalc() {
	      Filter.Period = Period;
	      Filter.Delta = Delta;
	      Filter.Price = Bars.Close;
			
   		  //trend can be isolated by computing the average of the cyclic price movement
		  Average.price = Filter;
		  Average.length = 2 * Period;
		}
		
		protected override void CalcBar() {
		  //update peak/valley values
		  if (Bars.CurrentBar > 3) {
			Peak.Value = Peak[1];
			Valley.Value = Valley[1];
			
			//set the peak/valley values according to the new filter extremes
			if (Filter[1] > Filter[0] && Filter[1] > Filter[2]) Peak.Value = Filter[1];
			if (Filter[1] < Filter[0] && Filter[1] < Filter[2]) Valley.Value = Filter[1];
		  } else {
			Peak.Value = 0;
			Valley.Value = 0;
		  }
			
		  //compute the average values for the peaks/valleys to get the upper/lower bands within
		  //which market would be consolidating
		  double upper = Peak.Average(48) * Fraction;
		  double lower = Valley.Average(48) * Fraction;
		  
	      //plot the outputs
		  PlotUpperBand.Set(upper);
		  PlotLowerBand.Set(lower);
		  PlotTrend.Set(Average[0]);
		}
	}
}