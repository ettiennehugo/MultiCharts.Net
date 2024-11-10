using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
  public class _BreakoutLE : _CustomSignal
  {
		//constants:
		public const int DEFAULT_PERIOD = 50;

		//properties:
		[Input] public int HighBreakoutPeriod { get; set; }		//only applies when we use long trading (no stop-loss based on this yet)
		[Input] public int SuperSmootherPeriod { get; set; }	//used only to exit trades
		[Input] public int VolumePeriod { get; set; }
		//[Input] public int MarketPeriod { get; set; }
		//[Input] public int RelativeStrengthPeriod { get; set; }
		//[Input] public int RelativeStrengthSmoothingPeriod { get; set; }

		//attributes:
		private _EfficiencyRatio m_efficiencyRatio;
		//private RelativeStrength m_relativeStrength;
		private SuperSmoother m_instrumentSmoother;
		private VolumeWithSD m_volumeWithSD;
		//private IInstrument m_marketBars;
		//private SuperSmoother m_marketSmoother;
		private IOrderMarket m_le;
		private IOrderMarket m_lx;

		public _BreakoutLE(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			HighBreakoutPeriod = DEFAULT_PERIOD;
			SuperSmootherPeriod = DEFAULT_PERIOD;
			VolumePeriod = DEFAULT_PERIOD;
			//MarketPeriod = DEFAULT_PERIOD;
			//RelativeStrengthPeriod = RelativeStrength.LENGTH;
			//RelativeStrengthSmoothingPeriod = RelativeStrength.LENGTH;

			//create functions used
			m_efficiencyRatio = new _EfficiencyRatio(this);
			m_instrumentSmoother = new SuperSmoother(this);
			m_volumeWithSD = new VolumeWithSD(this);
			//m_marketSmoother = new SuperSmoother(this);
			//m_relativeStrength = new RelativeStrength(this);

			//create order types
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, EOrderAction.Sell));
		}

		protected override void StartCalc()
		{
			//initialize functions used
			m_efficiencyRatio.Length = HighBreakoutPeriod;
			m_efficiencyRatio.Instrument = Bars;
			//m_relativeStrength.Length = RelativeStrengthPeriod;
			//m_relativeStrength.SmoothingLength = RelativeStrengthSmoothingPeriod;
			//m_relativeStrength.Instrument1 = this.BarsOfData(1);
			//m_relativeStrength.Instrument2 = this.BarsOfData(2);
			m_instrumentSmoother.Length = SuperSmootherPeriod;
			m_instrumentSmoother.Price = Bars.Close;
			m_volumeWithSD.Length = VolumePeriod > 0 ? VolumePeriod : VolumeWithSD.DEFAULT_LENGTH;
			m_volumeWithSD.Volume = Bars.TrueVolume();
			//m_marketSmoother.Length = MarketPeriod;
			//m_marketBars = this.BarsOfData(1);
			//m_marketSmoother.Price = m_marketBars.Close
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
			//prioritize signals with a higher efficiency ratio in portfolio trading
			//lower efficiency ratio means the price moves more smoothly, so we need to use 1 - ER to prioritize these signals.
			if (Environment.ApplicationCode == EApplicationCode.Portfolio) MyPortfolioData["RotationalValue"] = 1 - m_efficiencyRatio.Value;
			
			//process the entry/exit logic
			calcBarEntry();
			calcBarExit();
		}

		protected void calcBarEntry()
		{
			//determine whether we should send an entry order
			if (//m_marketBars.Close[0] > m_marketSmoother.Value &&	//market is bullish
				//m_relativeStrength[0] > 1.0d && //instrument is stronger than market
				(VolumePeriod == 0 || Bars.Volume[0] > m_volumeWithSD.AvgPlusLevel2[0]) && //above average volume on breakout
				Bars.Close[0] > Bars.High.Highest(HighBreakoutPeriod, 1))

				//TODO - Cap the position size to about 5-10% of the daily volume, this strategy does give a lot of entries that would not be able to be filled in real life.
				m_le.Send(positionSize(Bars));
		}

		protected void calcBarExit()
		{
			//send standard exit order
			if (this.PositionSide > 0 &&
					//(m_marketBars.Close.CrossesUnder(m_marketSmoother[0], 1) || Bars.Close.CrossesUnder(m_instrumentSmoother[0], 1))	//test with market filter
					Bars.Close.CrossesUnder(m_instrumentSmoother[0], 1) //no market filter - in general this is a better exit since the signal would turn bearish most likely before the market does
					)
				m_lx.Send();
		}
  }
}