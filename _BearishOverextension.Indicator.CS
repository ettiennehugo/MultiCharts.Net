using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
	[SameAsSymbol]
	public class _BearishOverextension : IndicatorObject
	{
		//constants


		//inputs
		[Input] public double MinimumDropPercent { get; set; }
		[Input] public int DropDays { get; set; }
		[Input] public int ATRLength { get; set; }
		[Input] public double MinimumOverextensionFactor { get; set; }
		[Input] public int FastSmoothingLength { get; set; }
		[Input] public int SlowSmoothingLength { get; set; }
		[Input] public int GradualDeclineLength { get; set; }

		//properties


		//attributes
		private IPlotObject m_plotO;
		private IPlotObject m_plotH;
		private IPlotObject m_plotL;
		private IPlotObject m_plotC;
		private Function._BearishOverextension m_bearishOverextension;

		//interface methods
		public _BearishOverextension(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			MinimumDropPercent = Function._BearishOverextension.DEFAULT_MINIMUM_DROP_PERCENT;
			DropDays = Function._BearishOverextension.DEFAULT_DROP_DAYS;
			ATRLength = Function._BearishOverextension.DEFAULT_ATR_LENGTH;
			MinimumOverextensionFactor = Function._BearishOverextension.DEFAULT_MINIMUM_OVEREXTENSION_FACTOR;
			FastSmoothingLength = Function._BearishOverextension.DEFAULT_FAST_SMOOTHER_LENGTH;
			SlowSmoothingLength = Function._BearishOverextension.DEFAULT_SLOW_SMOOTHER_LENGTH;
			GradualDeclineLength = Function._BearishOverextension.DEFAULT_GRADUAL_DECLINE_LENGTH;
			m_plotO = AddPlot(new PlotAttributes("", EPlotShapes.LeftTick, Color.Purple));
			m_plotH = AddPlot(new PlotAttributes("", EPlotShapes.BarHigh, Color.Purple));
			m_plotL = AddPlot(new PlotAttributes("", EPlotShapes.BarLow, Color.Purple));
			m_plotC = AddPlot(new PlotAttributes("", EPlotShapes.RightTick, Color.Purple));
			m_bearishOverextension = new Function._BearishOverextension(this);
		}

		protected override void StartCalc()
		{
			m_bearishOverextension.Instrument = Bars;
			m_bearishOverextension.MinimumDropPercent = MinimumDropPercent;
			m_bearishOverextension.DropDays = DropDays;
			m_bearishOverextension.ATRLength = ATRLength;
			m_bearishOverextension.MinimumOverextensionFactor = MinimumOverextensionFactor;
			m_bearishOverextension.FastSmoothingLength = FastSmoothingLength;
			m_bearishOverextension.SlowSmoothingLength = SlowSmoothingLength;
			m_bearishOverextension.GradualDeclineLength = GradualDeclineLength;
		}

		protected override void CalcBar()
		{
			if (m_bearishOverextension[0])
			{
				Output.WriteLine("BOE - {0}", Bars.Time[0]);
				m_plotO.Set(Bars.Open[0]);
				m_plotH.Set(Bars.High[0]);
				m_plotL.Set(Bars.Low[0]);
				m_plotC.Set(Bars.Close[0]);
			}
		}

		//methods


	}
}