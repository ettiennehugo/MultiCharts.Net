using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;


namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Scan the price data for a specific high volume breakout/breakdown around a specific price level.
	/// </summary>
	public class _BullBearFlags : _CustomSignal
	{
		//constants
		public const int DEFAULT_VOLUME_PERIOD = 20;
		public const double DEFAULT_MAXIMUM_VOLUME_PERCENT = 0.2;   //default to 20% of the average volume over the volume period
		public const double DEFAULT_EQUITY_RISK_PER_POSITION = 0.01; //default to 1% of the equity risk per position

		//inputs
		[Input] public EMarketPositionSide EntrySide { get; set; }   //entry side to scan for, use flat to scan for both long and short entries
		[Input] public double LongEntryPrice { get; set; }           //price level to enter a long position
		[Input] public double ShortEntryPrice { get; set; }          //price level to enter a short position
		[Input] public double EquityRiskPerPosition { get; set; }    //equity risk per position
		[Input] public double Risk { get; set; }                     //DOLLAR risk for the position based on stop-loss
		[Input] public int VolumePeriod { get; set; }                //period to calculate the volume average and standard deviation
		[Input] public double MaximumVolumePercent { get; set; }     //maximum percentage of the average volume to enter a position

		//TBD - should we add high volume controls here or do we require the user to monitor this???

		//properties


		//attributes
		private IOrderPriced m_le;
		private IOrderPriced m_se;

		//interface methods
		public _BullBearFlags(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//initialize the strategy inputs
			EntrySide = EMarketPositionSide.Flat;
			LongEntryPrice = 0.0d;
			ShortEntryPrice = 0.0d;
			Risk = -1.0d;
			EquityRiskPerPosition = DEFAULT_EQUITY_RISK_PER_POSITION;
			VolumePeriod = DEFAULT_VOLUME_PERIOD;
			MaximumVolumePercent = DEFAULT_MAXIMUM_VOLUME_PERCENT;

			// create variable objects, function objects, order objects etc.
			m_le = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
			m_se = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, EOrderAction.SellShort));
		}

		protected override void StartCalc()
		{
			if (LongEntryPrice < 0 || ShortEntryPrice < 0) throw new Exception("Long and short entry prices must be greater or equal to zero.");
			if (EquityRiskPerPosition <= 0 || EquityRiskPerPosition > 1) throw new Exception("Equity risk per position must be between 0 and 1.");
		}

		protected override void CalcBar()
		{
			if (Environment.CalcReason == CalculationReason.Default && PositionSide == 0)
			{
				switch (EntrySide)
				{
					case EMarketPositionSide.Long:
						if (Bars.Close[0] >= LongEntryPrice) enterLong();
						break;
					case EMarketPositionSide.Short:
						if (Bars.Close[0] <= ShortEntryPrice) enterShort();
						break;
					case EMarketPositionSide.Flat:
						if (Bars.Close[0] >= LongEntryPrice)
							enterLong();
						else if (Bars.Close[0] <= ShortEntryPrice)
							enterShort();
						break;
				}
			}
		}

		//methods
		private void enterLong()
		{
			m_le.Send(LongEntryPrice, positionSize(Bars, positionSize(Bars, Risk, EquityRiskPerPosition), VolumePeriod, MaximumVolumePercent));
		}

		private void enterShort()
		{
			m_se.Send(ShortEntryPrice, positionSize(Bars, positionSize(Bars, Math.Abs(Risk) /* make sure risk is always positive */, EquityRiskPerPosition), VolumePeriod, MaximumVolumePercent));
		}
	}
}