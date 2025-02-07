using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	public class _BreakoutWithConfirmLE : _CustomSignal {
		//constants:
		public const int DEFAULT_PERIOD = 50;
		public const int DEFAULT_MARKET_PERIOD = 200;
		public const int RS_SMOOTHING_PERIOD = 0;

		//properties:
		[Input] public int HighBreakoutPeriod { get; set; }		//only applies when we use long trading (no stop-loss based on this yet)
		[Input] public int SuperSmootherPeriod { get; set; }  //used only to exit trades		
		[Input] public bool RiskBasedPositionSizing { get; set; }
		[Input] public double MaxRiskEquityPerPosPercent { get; set; }
		[Input] public int VolumePeriod { get; set; }
		[Input] public double MaxVolumeBasedPosPercent { get; set; }
		[Input] public int RelativeEfficiencyPeriod { get; set; }
		[Input] public int MarketPeriod { get; set; }

		//attributes:
		//private EfficiencyRatio m_efficiencyRatio;
		private _RelativeEfficiency m_relativeEfficiency;
		private PowerLanguage.Function._SuperSmoother m_instrumentSmoother;
		private _VolumeWithSD m_instrumentVolume;
		private IInstrument m_marketBars;
		private PowerLanguage.Function._SuperSmoother m_marketSmoother;
		private IOrderMarket m_le;
		private IOrderMarket m_lx;

		public _BreakoutWithConfirmLE(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			HighBreakoutPeriod = DEFAULT_PERIOD;
			RelativeEfficiencyPeriod = DEFAULT_PERIOD;
			SuperSmootherPeriod = DEFAULT_PERIOD;
			VolumePeriod = DEFAULT_PERIOD;
			RiskBasedPositionSizing = true;
			MaxRiskEquityPerPosPercent = 1.0d;
			MaxVolumeBasedPosPercent = 10.0d;
			MarketPeriod = DEFAULT_MARKET_PERIOD;

			//create functions used
			//m_efficiencyRatio = new EfficiencyRatio(this);
			m_instrumentSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_instrumentVolume = new _VolumeWithSD(this);
			m_marketSmoother = new PowerLanguage.Function._SuperSmoother(this);
			m_relativeEfficiency = new _RelativeEfficiency(this);

			//create order types
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Sell));
		}

		protected override void StartCalc()
		{
			//initialize functions used
			//m_efficiencyRatio.Length = InstrumentPeriod;
			m_relativeEfficiency.Length = RelativeEfficiencyPeriod;
			m_relativeEfficiency.SmoothingLength = RS_SMOOTHING_PERIOD;
			m_relativeEfficiency.Instrument1 = this.BarsOfData(1);
			m_relativeEfficiency.Instrument2 = this.BarsOfData(2);
			m_instrumentSmoother.Length = SuperSmootherPeriod;
			m_instrumentSmoother.Price = Bars.Close;
			m_instrumentVolume.Length = VolumePeriod > 0 ? VolumePeriod : _VolumeWithSD.DEFAULT_LENGTH;
			m_instrumentVolume.Instrument = Bars;
			m_marketSmoother.Length = MarketPeriod;
			m_marketBars = this.BarsOfData(2);
			m_marketSmoother.Price = m_marketBars.Close;
		}

		protected override void CalcBar()
		{
			switch (Environment.CalcReason)
			{
				case CalculationReason.Default:
					CalcBarDefault();
					break;
			}
		}

		protected void CalcBarDefault()
		{
			//process the entry/exit logic
			calcBarEntry();
			calcBarExit();
		}

		protected void calcBarEntry()
		{
			//prioritize signals with a higher efficiency ratio in portfolio trading
			//lower efficiency ratio means the price moves more smoothly, so we need to use 1 - ER to prioritize the signals.
			//m_priority = 1 - m_efficiencyRatio.Value;
			if (Environment.ApplicationCode == EApplicationCode.Portfolio) MyPortfolioData["RotationalValue"] = m_relativeEfficiency.Value;

			//determine whether we should send an entry order
			if (m_marketBars.Close[0] > m_marketSmoother.Value &&	//market is bullish
					Bars.Close[0] > Bars.High.Highest(HighBreakoutPeriod, 1))
				m_le.Send(RiskBasedPositionSizing ? positionSize(Bars, m_instrumentSmoother[0]) : positionSize(Bars));
		}

		protected void calcBarExit()
		{
			//send standard exit order
			if (this.PositionSide > 0 &&
				//(m_marketBars.Close.CrossesUnder(m_marketSmoother[0], 1) || Bars.Close.CrossesUnder(m_instrumentSmoother[0], 1))	//in general do not perform test with market filter
				Bars.Close.CrossesUnder(m_instrumentSmoother[0], 1) //no market filter - in general this is a better exit since the signal would turn bearish most likely before the market does
				)
				m_lx.Send();
		}
	}
}