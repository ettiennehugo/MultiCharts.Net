using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	[SameAsSymbol]
	public class _BullishOverextension : IndicatorObject {
		//constants


		//inputs
		[Input] public double MinimumRisePercent { get; set; }   //minimum required increase in the past x-days
		[Input] public int RiseDays { get; set; }                //number of days to look back for the drop
		[Input] public int ATRLength { get; set; }               //length of the ATR to determine the overextension
		[Input] public double MinimumOverextensionFactor { get; set; } //minimum factor of the ATR to determine overextension
		[Input] public int FastSmoothingLength { get; set; }     //length of smoother to determine more current price move
		[Input] public int SlowSmoothingLength { get; set; }     //length of smoother to determine longer term price move
		[Input] public int GradualInclineLength { get; set; }    //length of scan to check for gradual decline
		[Input] public bool Debug { get; set; }

		//properties


		//attributes
		private IPlotObject m_plotO;
		private IPlotObject m_plotH;
		private IPlotObject m_plotL;
		private IPlotObject m_plotC;
		private Function._BullishOverextension m_bullishOverextension;

		//interface methods
		public _BullishOverextension(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			MinimumRisePercent = Function._BullishOverextension.DEFAULT_MINIMUM_RISE_PERCENT;
			RiseDays = Function._BullishOverextension.DEFAULT_RISE_DAYS;
			ATRLength = Function._BullishOverextension.DEFAULT_ATR_LENGTH;
			MinimumOverextensionFactor = Function._BullishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			FastSmoothingLength = Function._BullishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			SlowSmoothingLength = Function._BullishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			GradualInclineLength = Function._BullishOverextension.DEFAULT_GRADUAL_INCLINE_LENGTH;
			m_plotO = AddPlot(new PlotAttributes("", EPlotShapes.LeftTick, Color.Purple));
			m_plotH = AddPlot(new PlotAttributes("", EPlotShapes.BarHigh, Color.Purple));
			m_plotL = AddPlot(new PlotAttributes("", EPlotShapes.BarLow, Color.Purple));
			m_plotC = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.Purple));
			m_bullishOverextension = new Function._BullishOverextension(this);
		}

		protected override void StartCalc()
		{
		  m_bullishOverextension.Instrument = Bars;
			m_bullishOverextension.MinimumRisePercent = MinimumRisePercent;
			m_bullishOverextension.RiseDays = RiseDays;
			m_bullishOverextension.ATRLength = ATRLength;
			m_bullishOverextension.MinimumOverextensionFactor = MinimumOverextensionFactor;
			m_bullishOverextension.FastSmoothingLength = FastSmoothingLength;
			m_bullishOverextension.SlowSmoothingLength = SlowSmoothingLength;
			m_bullishOverextension.GradualInclineLength = GradualInclineLength;
		}

		protected override void CalcBar()
		{
			if (m_bullishOverextension[0])
			{
				if (Debug) Output.WriteLine("BullishOverextension Indicator, {0}, {1}", Bars.Info.Name, Bars.Time[0]);
				m_plotO.Set(Bars.Open[0]);
				m_plotH.Set(Bars.High[0]);
				m_plotL.Set(Bars.Low[0]);
				m_plotC.Set(Bars.Close[0]);
			}
		}

		//methods


	}
}