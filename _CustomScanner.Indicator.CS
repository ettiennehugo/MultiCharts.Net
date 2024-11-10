using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
  /// <summary>
  /// TODO:
	///   * See whether you can adjust this criteria strategy so it would work with both the scanner and the chart, that is, if it's a scanner it will raise the criteria signal and when it's a
	///     chart it will actually plot on the chart where the criteria is met.
  /// </summary>
  public class _CustomScanner : IndicatorObject {
		public _CustomScanner(object _ctx):base(_ctx){}

		//inputs
		[Input] public double PercentageChange { get; set; }			//percentage increase/decrease in a stock
		[Input] public int BarCount { get; set; }									//number of bars to check for the specific increase/decrease
		[Input] public double MinimumPrice { get; set; }					//minimum price for stock
		[Input] public double MinimumAverageVolume { get; set; }	//minimum average volume for stock to trade at
		[Input] public int AverageVolumeLength { get; set; }			//length in number of bars used to compute average volume
		[Input] public bool Historical { get; set; }							//allow historical scanning
		[Input] public bool Debug { get; set; }

		//attributes
		private IPlotObject criteriaPlot;

		protected override void Create() {
			Debug = false;
			Historical = false;		//assume real time use
			PercentageChange = 100;
			BarCount = 252;
			MinimumPrice = 10.0;
			MinimumAverageVolume = 100000;
			AverageVolumeLength = 252;
			criteriaPlot = AddPlot(new PlotAttributes("", EPlotShapes.BarHigh, Color.Red));
		}

		protected override void StartCalc() {
			if (Debug) Output.Clear();
		}

		protected override void CalcBar(){

			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					//determine the percentage change criteria and liquidity criteria
					bool percentChangeCriteria = PercentageChange > 0 ? Bars.CurrentBar >= BarCount && Bars.Close[0] != 0.0 && (Bars.Close[BarCount - 1] / Bars.Close[0]) > (PercentageChange / 100.0) : Bars.CurrentBar >= BarCount && Bars.Close[0] != 0.0 && (Bars.Close[BarCount - 1] / Bars.Close[0]) < (PercentageChange / 100.0);
					bool liquidityCriteria = Bars.CurrentBar >= AverageVolumeLength && Bars.Close[0] >= MinimumPrice && MinimumAverageVolume >= Bars.TrueVolume().AverageCustom(AverageVolumeLength);

					if (Debug) {
						Output.WriteLine("{0} - percentage change({1}), liquidity({2})", Bars.Time[0], percentChangeCriteria, liquidityCriteria);
					}

          if (percentChangeCriteria && liquidityCriteria) {
            if (!Historical && Bars.LastBarOnChart) 
						{
              criteriaPlot.Set(Bars.Close[0]);
              criteriaPlot.Set(Bars.Close[BarCount - 1]);
            }
            else 
						{
              criteriaPlot.Set(Bars.Close[0]);
              criteriaPlot.Set(Bars.Close[BarCount - 1]);
            }
          }
          break;
			}
		}
	}
}