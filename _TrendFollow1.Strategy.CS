using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Trend following strategy using a supersmoother cross. Need to be paired with good StopsAndExits strategy to make good money.
	/// Needs multiple data stream inputs.
	/// </summary>
	public class _TrendFollow1 : _CustomSignal
	{
		//constants:
		public const int DEFAULT_PERIOD = 50;

		//properties:
		[Input] public int Period { get; set; }
		[Input] public bool RiskBasedPositionSizing { get; set; }
		[Input] public double MaxRiskEquityPerPosPercent { get; set; }
		[Input] public int VolumePeriod { get; set; }
		[Input] public double MaxVolumeBasedPosPercent { get; set; }
		[Input] public int MarketPeriod { get; set; }		//market period used to avoid entries that moves against the market trend.
		[Input] public bool EnableLong { get; set; }
		[Input] public bool EnableShort { get; set; }

		//attributes:
		private PowerLanguage.Function._EfficiencyRatio m_efficiencyRatio;
		private SuperSmoother m_instrumentSmoother;
		private PowerLanguage.Function.VolumeWithSD m_instrumentVolume;
		private IInstrument m_marketInstrument;
		private SuperSmoother m_marketSmoother;
		private IOrderMarket m_le;
		private IOrderMarket m_se;

		public _TrendFollow1(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize input parameters
			Period = DEFAULT_PERIOD;
			RiskBasedPositionSizing = true;
			MaxRiskEquityPerPosPercent = 1.0;		//make zero or less to use portfolio percentage
			VolumePeriod = DEFAULT_PERIOD;
			MaxVolumeBasedPosPercent = 10.0;			//make zero or less to only use risk or portfolio percent
			MarketPeriod = DEFAULT_PERIOD;
			EnableLong = true;
			EnableShort = true;

			//create functions used
			m_instrumentSmoother = new SuperSmoother(this);
			m_instrumentVolume = new VolumeWithSD(this);
			m_marketSmoother = new SuperSmoother(this);
			m_efficiencyRatio = new PowerLanguage.Function._EfficiencyRatio(this);

			//create order types
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TF1LE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TF1SE", EOrderAction.SellShort));
		}

		protected override void StartCalc()
		{
			//initialize functions used
			if (MaxRiskEquityPerPosPercent > 1.0d) throw new Exception("MaxRiskEquityPerPosPercent must be less than 1.0.");
			if (MaxVolumeBasedPosPercent > 1.0d) throw new Exception("MaxVolumeBasedPosPercent must be less than 1.0.");
			m_marketInstrument = BarsOfData(2);
			m_instrumentSmoother.Length = Period;
			m_instrumentSmoother.Price = Bars.Close;
			m_instrumentVolume.Length = VolumePeriod;
			m_instrumentVolume.Volume = Bars.Volume;
			m_marketSmoother.Length = MarketPeriod != 0 ? MarketPeriod : _SuperSmoother.DEFAULT_PERIOD;
			m_marketSmoother.Price = m_marketInstrument.Close;
			m_efficiencyRatio.Length = Period;
			m_efficiencyRatio.Instrument = Bars;
			m_instrumentVolume.Length = VolumePeriod;
			m_instrumentVolume.Volume = Bars.TrueVolume();
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
			
			//process the entry logic
			//determine whether we should send an entry order
			if (EnableLong &&
				(MarketPeriod <= 0 || m_marketSmoother[0] < m_marketInstrument.Close[0]) &&
				Bars.Close.CrossesOver(m_instrumentSmoother[0], 1))
			{
				int size = RiskBasedPositionSizing ? positionSize(Bars, positionSize(Bars, m_instrumentSmoother[0], MaxVolumeBasedPosPercent), VolumePeriod) : positionSize(Bars);
				m_le.Send(size);
			}
			else if (EnableShort && 
				     (MarketPeriod <= 0 || m_marketSmoother[0] > m_marketInstrument.Close[0]) &&
				     Bars.Close.CrossesUnder(m_instrumentSmoother[0], 1))
			{
				int size = RiskBasedPositionSizing ? positionSize(Bars, positionSize(Bars, m_instrumentSmoother[0], MaxVolumeBasedPosPercent), VolumePeriod) : positionSize(Bars);
				m_se.Send(size);
			}
		}
	}
}