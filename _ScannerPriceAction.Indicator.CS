using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using PriceAction;

// TODO:
// * Implement squeeze where the price is being pressed between a moving average (super smoother) and a specific set of highs.
// * Implement cup-and-handle, saucer-and-handle using the above squeeze indication - so you have a fall in price from a high and it is rising back up
//   being squeezed between that high and the moving average, this is setting the stock up for a continuation (bullish/bearish)
//   -  fairly easy way to implement this would be to first implement the squeeze pattern and once you detect the squeeze you can
//      scan back to the first high bar that you detect historically and then detect whether you have a fall in price down between that
//      that high and the squeeze
// * Determine whether you'd add these patterns for the price action class or whether your move them to a sub-class of PriceActionAnalysis
//   maybe call is ChartFormationAnalysis - the purpose of this class would be to recognize more advanced chart formations based on the
//   price action analysis. Possible chart formations:
//	 - flags
//   - wedges
//   - cup-and-handle, saucer-and-handle



namespace PowerLanguage.Indicator{
	public class _ScannerPriceAction : IndicatorObject {
	    public const int DEFAULT_SIGNAL_LOOKBACK = 20;
	    public const int DEFAULT_FRACTAL_LAYERS = 3;

	    [Input] public int SignalLookback { get; set; }         //number of bars to look back to price action
	    [Input] public int BarBufferSize { get; set; }          //number of bars to keep for price action analysis
	    [Input] public int FractalLayers { get; set; }          //number of fractal layers to use for analysis
	    [Input] public int AtrLength { get; set; }              //count to use to compute the ATR bar size
	    //[Input] public int VolumeLength { get; set; }           //length to use for the volume average
	    [Input] public double FactorDoji { get; set; }          //factor of ATR to consider a bar a doji
	    [Input] public double FactorClose { get; set; }         //factor of ATR to consider price values close
	    [Input] public double FactorStrong { get; set; }        //factor of ATR to consider a bar as a strong move
	    [Input] public double FactorEpsilon { get; set; }       //factor of ATR used to judge stuffs????
	    //[Input] public int TopBottomSearchLength { get; set; }  //number of bars to search to double top/bottom

	    private IPlotObject _dblBottom;
	    private IPlotObject _dblTop;
	    private IPlotObject _cupAndHandle;
	    private PriceActionAnalysis priceActionAnalysis;
		
		public _ScannerPriceAction(object _ctx):base(_ctx) {
	      SignalLookback = DEFAULT_SIGNAL_LOOKBACK;
	      BarBufferSize = PriceActionAnalysis.DefaultSize;
	      FractalLayers = DEFAULT_FRACTAL_LAYERS;
	      AtrLength = PriceActionAnalysis.DefaultAtrLength;
	      //VolumeLength = PriceActionAnalysis.DefaultVolumeLength;
	      FactorDoji = PriceActionAnalysis.DefaultFactorDoji;
	      FactorClose = PriceActionAnalysis.DefaultFactorClose;
	      FactorStrong = PriceActionAnalysis.DefaultFactorStrong;
	      FactorEpsilon = PriceActionAnalysis.DefaultFactorEpsilon;
		}		

		protected override void Create() {
	      _dblBottom = AddPlot(new PlotAttributes("DblBtm"));
	      _dblTop = AddPlot(new PlotAttributes("DblTp"));
	      _cupAndHandle = AddPlot(new PlotAttributes("Cp&Hdl"));
		}
		
		protected override void StartCalc() {
	      //create the price action analysis
	      priceActionAnalysis = new PriceActionAnalysis(FractalLayers);
	      priceActionAnalysis.Size = BarBufferSize;
	      priceActionAnalysis.AtrLength = AtrLength;
	      //priceActionAnalysis.VolumeLength = VolumeLength;
	      priceActionAnalysis.FactorDoji = FactorDoji;
	      priceActionAnalysis.FactorClose = FactorClose;
	      priceActionAnalysis.FactorStrong = FactorStrong;
	      priceActionAnalysis.FactorEpsilon = FactorEpsilon;

	      //reset the price action indicators
	      _dblBottom.Set(0);
	      _dblTop.Set(0);
	      _cupAndHandle.Set(0);
		}
		
	    protected void updateFlag(ref bool priceBarFlag, ref bool indicatorFlag)
	    {
	      if (!indicatorFlag) indicatorFlag = priceBarFlag;
	    }

	    protected void updateIndicator(ref bool indicatorFlag, ref IPlotObject indicator)
	    {
	      if (indicatorFlag)
	        indicator.BGColor = Color.DarkGreen;
	      else
	        indicator.BGColor = Color.Red;
	    }
				
		protected override void CalcBar(){
	      if (Bars.Status == EBarState.Close && Bars.CurrentBar > 2)
	        priceActionAnalysis.Analyse(Bars.Time[0], Bars.Open[0], Bars.High[0], Bars.Low[0], Bars.Close[0], Bars.Volume[0]);

	      //update indicator values based off the bar lookback
	      bool dblBottom = false;
	      bool dblTop = false;
	      bool cupAndHandle = false;

	      int count = priceActionAnalysis.Bars.Count < SignalLookback ? priceActionAnalysis.Bars.Count : SignalLookback;
	      for (int i = 0; i < SignalLookback; i++)
	      {
	        PriceActionBar bar = priceActionAnalysis.Bars.Values[i];
	        updateFlag(ref bar.state.doubleBottom, ref dblBottom);
	        updateFlag(ref bar.state.doubleTop, ref dblTop);
	      }

	      updateIndicator(ref dblBottom, ref _dblBottom);
	      updateIndicator(ref dblTop, ref _dblTop);
	      updateIndicator(ref cupAndHandle, ref _cupAndHandle);
		}
	}
}