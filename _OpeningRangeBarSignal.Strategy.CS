using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Adds common logic to enter the market based on a high volume opening range bar computing the position
	/// size based on the risk of the opening range bar and the account equity available.
	/// Entry bars are typically 1/5/10/15-minute bars and volume bars are typically daily bars.
	/// </summary>
	public class _OpeningRangeBarSignal : _CustomSignal
	{
		//constants
		public const int DEFAULT_DATA_FEED = 1;
		public const double DEFAULT_ENTRY_VOLUME_PERCENT = 0.15;
		public const int DEFAULT_VOLUME_LENGTH = 20;
		public const double DEFAULT_MAX_VOLUME_BASED_POS_PERCENT = 0.2;
		public const double DEFAULT_ENTRY_STOP_LOSS_BUFFER_PRICE = 0.0;
		public const EMarketPositionSide DEFAULT_MARKET_POSITION_SIDE = EMarketPositionSide.Flat;
		public const double DEFAULT_CLOSE_PERCENTILE = 0.9;
		public const double DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT = 1.0;

		//inputs
		public int _EntryDataFeed { get; set; }          //data feed around which entry calculations are made, typically a 1/5/10/15-minute data feed
		public double _EntryVolumePercent { get; set; }  //entry volume percent of the average volume that would be considered a high volume bar, e.g. 0.25 means we want to see about 25% of the average volume to consider the bar a high volume bar
		public int _VolumeDataFeed { get; set; }				 //data feed around which volume calculations are made, typically a daily data feed
		public int _VolumeLength { get; set; }           //volume length to use for opening bar volume detection
		public double _MaxVolumeBasedPosPercent { get; set; }  //maximum volume based position size as a percentage of equity
		public double _EntryStopLossBufferPrice { get; set; }    //buffer price to add to the entry price to avoid slippage
		public EMarketPositionSide _EntryPositionSide { get; set; }  //position side to take on the opening, set Flat to be direction agnostic
		public double _ClosePercentile { get; set; }		 //close percentile in which directional trade must be made
		public double _MaxRiskEquityPerPosPercent { get; set; }  //maximum risk equity per position as a percentage of equity

		//attributes
		protected IOrderPriced m_le;
		protected IOrderPriced m_se;
		protected IInstrument m_entryInstrument;
		protected IInstrument m_volumeInstrument;
		protected VariableSeries<bool> m_lastBarInSession;
		protected double m_riskAmountPerContract;

		//interface methods
		public _OpeningRangeBarSignal(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			_EntryDataFeed = DEFAULT_DATA_FEED;
			_EntryVolumePercent = DEFAULT_ENTRY_VOLUME_PERCENT;
			_VolumeDataFeed = DEFAULT_DATA_FEED;
			_VolumeLength = DEFAULT_VOLUME_LENGTH;
			_MaxVolumeBasedPosPercent = DEFAULT_MAX_VOLUME_BASED_POS_PERCENT;
			_EntryStopLossBufferPrice = DEFAULT_ENTRY_STOP_LOSS_BUFFER_PRICE;
			_EntryPositionSide = DEFAULT_MARKET_POSITION_SIDE;
			_ClosePercentile = DEFAULT_CLOSE_PERCENTILE;
			_MaxRiskEquityPerPosPercent = DEFAULT_MAX_RISK_EQUITY_PER_POS_PERCENT;
			m_le = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Buy));
			m_se = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, EOrderAction.SellShort));
			m_lastBarInSession = new VariableSeries<bool>(this);
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (_EntryDataFeed < 1 || _EntryDataFeed > MaxDataStream) throw new ArgumentException("_EntryDataFeed out of valid range");
			if (_VolumeDataFeed < 1 || _VolumeDataFeed > MaxDataStream) throw new ArgumentException("_VolumeDataFeed out of valid range");
			if (_EntryVolumePercent <= 0 || _EntryVolumePercent > 1) throw new ArgumentException("_EntryVolumePercent must be between 0 and 1.");
			if (_VolumeDataFeed < 1 || _VolumeDataFeed > MaxDataStream) throw new ArgumentException("_VolumeDataFeed out of valid range");
			if (_VolumeLength <= 0) throw new ArgumentException("_VolumeLength must be greater than zero.");
			if (_EntryStopLossBufferPrice < 0) throw new ArgumentException("_EntryStopLossBufferPrice must be greater than or equal to zero.");
			if (_MaxVolumeBasedPosPercent <= 0 || _MaxVolumeBasedPosPercent > 1) throw new ArgumentException("_MaxVolumeBasedPosPercent must be between 0 and 1.");
			if (_ClosePercentile <= 0 || _ClosePercentile > 1) throw new ArgumentException("_ClosePercentile must be between 0 and 1.");
			if (_MaxRiskEquityPerPosPercent <= 0 || _MaxRiskEquityPerPosPercent > 1) throw new ArgumentException("_MaxRiskEquityPerPosPercent must be between 0 and 1.");

			m_riskAmountPerContract = 0;

			m_entryInstrument = BarsOfData(_EntryDataFeed);
			m_volumeInstrument = BarsOfData(_VolumeDataFeed);
			m_lastBarInSession.Value = false;
		}

		protected override void CalcBar()
		{
			m_lastBarInSession.Value = m_entryInstrument.LastBarInSession;

			//only process entries on bar close when we're flat and we just have a high volume opening bar for the session
			if (PositionSide == 0 && Environment.CalcReason == CalculationReason.Default && m_entryInstrument.Status == EBarState.Close)
			{
				EMarketPositionSide entrySide = highVolumeOpeningBar();
				if (entrySide == EMarketPositionSide.Long)
				{
					m_riskAmountPerContract = m_entryInstrument.Close[0] - m_entryInstrument.Low[0] + _EntryStopLossBufferPrice;
					m_le.Send(m_entryInstrument.Close[0], positionSize(m_volumeInstrument, positionSize(m_entryInstrument, m_entryInstrument.Low[0] - _EntryStopLossBufferPrice, _MaxRiskEquityPerPosPercent), _VolumeLength, _MaxRiskEquityPerPosPercent));
				}
				else if (entrySide == EMarketPositionSide.Short)
				{
					m_riskAmountPerContract = m_entryInstrument.High[0] - m_entryInstrument.Close[0] + _EntryStopLossBufferPrice;
					m_se.Send(m_entryInstrument.Close[0], positionSize(m_volumeInstrument, positionSize(m_entryInstrument, m_entryInstrument.High[0] + _EntryStopLossBufferPrice, _MaxRiskEquityPerPosPercent), _VolumeLength, _MaxRiskEquityPerPosPercent));
				}
			}
		}

		//methods		
		/// <summary>
		/// Determine a high volume opening bar for the opening bar closes above/below the required percentile and returns the market position side to consider for entry.
		/// </summary>
		private EMarketPositionSide highVolumeOpeningBar()
		{
			if (!m_lastBarInSession[1]) return EMarketPositionSide.Flat; // previous bar was not the last bar in the session so this is not an opening bar

			double range = m_entryInstrument.High[0] - m_entryInstrument.Low[0];
			if (range == 0) return EMarketPositionSide.Flat; // avoid division by zero

			// Check for bullish bar
			if ((_EntryPositionSide == EMarketPositionSide.Flat || _EntryPositionSide == EMarketPositionSide.Long) &&
					(m_entryInstrument.Close[0] - m_entryInstrument.Low[0]) / range > _ClosePercentile)
			{
				if (m_entryInstrument.Volume[0] > _EntryVolumePercent * m_volumeInstrument.Volume.AverageCustom(_VolumeLength, 1))
					return EMarketPositionSide.Long; // high volume bullish bar
				else
					return EMarketPositionSide.Flat; // volume criteria not met
			}

			// Check for bearish bar
			if ((_EntryPositionSide == EMarketPositionSide.Flat || _EntryPositionSide == EMarketPositionSide.Short) &&
					(Bars.Low[0] - Bars.Close[0]) / range > _ClosePercentile)
			{
				if (m_entryInstrument.Volume[0] > _EntryVolumePercent * m_volumeInstrument.Volume.AverageCustom(_VolumeLength, 1))
					return EMarketPositionSide.Short; // high volume bearish bar
			}

			return EMarketPositionSide.Flat; // criteria not met
		}

		//methods


	}
}