using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	/// <summary>
	/// Buys the pullback on the strongest stocks (reversal trade) in the market when the market is overextended to the down side. Both the stocks and the
	/// market in general should be bullish.
	/// https://www.youtube.com/watch?v=WY_5BV3BmkA
	/// Required Feeds:
	///		1. Scanner data feed - typically daily data feed for the same instrument.
	///		2. Market data feed - typically daily data feed for the market (same resolution as 2).
	/// </summary>
	[SameAsSymbol]
	public class _CapitulationReversal : IndicatorObject
	{
		//constants


		//inputs
		[Input] public double MinimumGrowthPercent { get; set; }   //minimum required growth in the past x-days
		[Input] public int GrowthDays { get; set; }                //number of days to look back for the growth
		[Input] public int ScanLength { get; set; }                //lookback length for the market high
		[Input] public double DownPercent { get; set; }            //market down percent from the high in the past y-days
		[Input] public double GapDownPercent { get; set; }         //minimum required gap down on the open - 0 to skip checking for a gap down
		[Input] public bool Debug { get; set; }                    //debug flag to print out the values

		//properties


		//attributes
		private IPlotObject m_plotO;
		private IPlotObject m_plotH;
		private IPlotObject m_plotL;
		private IPlotObject m_plotC;
		private Function._CapitulationReversal m_capitulationReversal;

		//interface methods
		public _CapitulationReversal(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			MinimumGrowthPercent = Function._CapitulationReversal.DEFAULT_MINIMUM_GROWTH_PERCENT;
			GrowthDays = Function._CapitulationReversal.DEFAULT_GROWTH_DAYS;
			ScanLength = Function._CapitulationReversal.DEFAULT_SCAN_LENGTH;
			DownPercent = Function._CapitulationReversal.DEFAULT_DOWN_PERCENT;
			GapDownPercent = Function._CapitulationReversal.DEFAULT_GAP_DOWN_PERCENT;
			Debug = false;

			m_plotO = AddPlot(new PlotAttributes("", EPlotShapes.LeftTick, Color.Purple));
			m_plotH = AddPlot(new PlotAttributes("", EPlotShapes.BarHigh, Color.Purple));
			m_plotL = AddPlot(new PlotAttributes("", EPlotShapes.BarLow, Color.Purple));
			m_plotC = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.Purple));
			m_capitulationReversal = new Function._CapitulationReversal(this);
		}

		protected override void StartCalc()
		{
			m_capitulationReversal.Instrument = BarsOfData(1); //Longer timeframe bars for the instrument, e.g. daily
			m_capitulationReversal.Market = BarsOfData(2); //Market data bars used to determine market pullback, e.g. daily
			m_capitulationReversal.MinimumGrowthPercent = MinimumGrowthPercent;
			m_capitulationReversal.GrowthDays = GrowthDays;
			m_capitulationReversal.ScanLength = ScanLength;
			m_capitulationReversal.DownPercent = DownPercent;
			m_capitulationReversal.GapDownPercent = GapDownPercent;
		}

		protected override void CalcBar()
		{
			if (m_capitulationReversal[0])
			{
				if (Debug) Output.WriteLine("CapitulationReversal Indicator, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
				m_plotO.Set(Bars.Open[0]);
				m_plotH.Set(Bars.High[0]);
				m_plotL.Set(Bars.Low[0]);
				m_plotC.Set(Bars.Close[0]);
			}
		}

		//methods


	}
}