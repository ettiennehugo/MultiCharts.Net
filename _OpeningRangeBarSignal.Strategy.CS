using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Adds common logic to enter the market based on the opening range bar breakout - typically at
	/// 1/5/10/15-minute intervals.
	/// </summary>
	public class _OpeningRangeBarSignal : _CustomSignal
	{
		//constants


		//inputs
		public int _VolumeLength { get; set; }           //volume length to use for opening bar volume detection
		public double _VolumeStdDevFactor { get; set; }  //volume standard deviation factor to use for high volume detection
		public double _MaxVolumeBasedPosPercent { get; set; }  //maximum volume based position size as a percentage of equity
		public EMarketPositionSide _EntryPositionSide { get; set; }  //position side to take on the opening 

		//attributes
		protected IOrderPriced m_le;
		protected IOrderPriced m_se;

		//interface methods
		public _OpeningRangeBarSignal(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			_EntryPositionSide = EMarketPositionSide.Long;		//default long entries
			m_le = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
			m_se = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, EOrderAction.SellShort));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (_VolumeLength <= 0) throw new Exception("Volume length must be greater than zero.");
			if (_VolumeStdDevFactor <= 0) throw new Exception("Volume standard deviation factor must be greater than zero.");
		}

		protected override void CalcBar()
		{
			//only process entries on bar close when we're flat and we just have a high volume opening bar for the session
			if (Bars.Status == EBarState.Close && PositionSide == 0 && highVolumeOpeningBar()) return;
			//NOTE: The risk below is based on a bullish bar and risk is open to close (not close to low).
			m_le.Send(Bars.Close[0], positionSize(Bars, positionSize(Bars, Bars.Open[0] - Bars.Info.MinMove), _VolumeLength, _MaxVolumeBasedPosPercent));
		}

		//methods		
		/// <summary>
		/// Determine a high volume opening bar for the session that closes in the top 25% percent of the opening bar range.
		/// </summary>
		private bool highVolumeOpeningBar()
		{
			if (_EntryPositionSide == EMarketPositionSide.Long)
			{
				if ((Bars.Close[0] - Bars.Low[0]) / (Bars.High[0] - Bars.Low[0]) < 0.75) return false;    //not a bullish closing in the top 25% of the range
			}
			else
				if ((Bars.Low[0] - Bars.Close[0]) / (Bars.High[0] - Bars.Low[0]) < 0.75) return false;    //not a bearish closing in the bottom 25% of the range

			SessionObject session = Bars.Sessions.SessionForDate(Bars.Time[0]);
			if (session.StartTime == Bars.Time[0].TimeOfDay) return false; //not an opening bar
			if (Bars.Volume[0] < _VolumeStdDevFactor * Bars.Volume.StandardDeviationCustom(_VolumeLength, 1)) return false;  //not a high volume bar
			return true;
		}

		//methods


	}
}