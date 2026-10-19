using ATCenterProxy.interop;
using PowerLanguage.Function;
using PowerLanguage.TradeManager;
using System;
using System.Drawing;
using System.Linq;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Delta divergence reversal long for futures. Volume Profile Book 1 chapter 6.1. Scalp.
	/// Keep the shorter-timeframe bias. Long only. No short entry, no scale-out, no breakeven.
	///
	/// Data1  Price. Execution stream. Volume profile is built here by the base.
	/// Data2  Cumulative delta, same bar type and interval as Data1. Required, bar-aligned.
	/// Data3+ Optional higher-timeframe price. Point ConfluenceDataStream2 / 3 at those
	///        streams (base properties, default -1 = off). This signal does not build a
	///        confluence engine.
	///
	/// Inheritance: _DeltaDivRevFuturesLELX : _VolumeProfileFuturesBase : _VolumeProfileBase : _CustomSignal.
	/// Public volume-profile bar indexes are 1-based Status Bar # (FullSymbolData.Current).
	/// </summary>
	public class _DeltaDivRevFuturesLELX2 : _VolumeProfileFuturesBase
	{
		//constants
		public const int DEFAULT_PRICE_MOMENTUM_LENGTH = 3;         // price lookback to detect falling price
		public const int DEFAULT_VOLUME_MOMENTUM_LENGTH = 2;        // cumulative volume lookback to detect buying volume
		public const int DEFAULT_LOW_LOOKBACK_LENGTH = 5;           // most recent low lookback length 
		public const double DEFAULT_RISK_REWARD_FACTOR = 1.0;       // risk reward factor to use per trade
		public const int DEFAULT_ENTRIES_PER_SESSION = 0;           // per default allow unlimited entries per session
		public const double DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT = 1.0; // default equity to risk per position
		public const double DEFAULT_RISK_BASED_ENTRY_PERCENT = 0.01;    // use 1% of equity for risk based entry

		//types


		//properties
		[Input] public int PriceMomentumLength { get; set; }  // Momentum lookback for the price.
		[Input] public int VolumeMomentumLength { get; set; } // Cumulative volume lookback.
		[Input] public int LowLookbackLength { get; set; }   // Lookback length for most recent low.
		[Input] public double RiskRewardFactor { get; set; } // Risk reward factor to use for entries exits	
		[Input] public bool AllowOutsideRth { get; set; }       // Allow trading outside of New York session (main session)
		[Input] public bool AllowAsia { get; set; }             // Allow trading the Asia session
		[Input] public int EntriesPerSession { get; set; }      // Number of entries per session - 0 = unlimited
		[Input] public int AbsoluteSize { get; set; } // [alternate A] Overrides the below position sizing parameters to take an absolute position size irrespective or risk/equity, this is the BIG HAMMER for position sizing use with caution, default 0 to disable, greater than 0 to enable.
		[Input] public double MaximumEquityPerPosPercent { get; set; } // [alternate B] maximum equity per position as a fraction of equityAvailable - caps the contract count at equity * fraction / contract notional (price * BigPointValue)
		[Input] public double RiskBasedEntryPercent { get; set; } // [alternate B] risk based entry percentage - this is used to base the position size as a percentage of the euity based on the risk taken, set to zero to disable risk based entries

		//attribute
		private IOrderMarket m_le;
		private IOrderPriced m_lxStop;
		private IOrderPriced m_lxTgt;
		private double m_stopLoss;
		private double m_targetPrice;
		private IInstrument m_price;
		private IInstrument m_cumulativeDelta;
		private Function._SuperSmoother m_priceMA;
		private Function._SuperSmoother m_cumulativeDeltaMA;

		//interface methods
		public _DeltaDivRevFuturesLELX2(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			PriceMomentumLength = DEFAULT_PRICE_MOMENTUM_LENGTH;
			VolumeMomentumLength = DEFAULT_VOLUME_MOMENTUM_LENGTH;
			LowLookbackLength = DEFAULT_LOW_LOOKBACK_LENGTH;
			RiskRewardFactor = DEFAULT_RISK_REWARD_FACTOR;
			AllowOutsideRth = false;
			AllowAsia = false;
			EntriesPerSession = DEFAULT_ENTRIES_PER_SESSION;
			AbsoluteSize = 0;
			MaximumEquityPerPosPercent = DEFAULT_MAXIMUM_EQUITY_PER_POS_PERCENT;
			RiskBasedEntryPercent = DEFAULT_RISK_BASED_ENTRY_PERCENT;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LEDDR", EOrderAction.Buy));
			m_lxStop = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LXStp", EOrderAction.Sell, OrderExit.FromAll));
			m_lxTgt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "LXTgt", EOrderAction.Sell, OrderExit.FromAll));
			m_priceMA = new Function._SuperSmoother(this);
			m_cumulativeDeltaMA = new Function._SuperSmoother(this);
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			if (PriceMomentumLength <= 0) throw new Exception("Price momentum length should be larger than 0.");
			if (VolumeMomentumLength <= 0) throw new Exception("Volume momentum length should be larger than 0.");
			if (RiskBasedEntryPercent < 0 || RiskBasedEntryPercent > 1) throw new Exception("RiskBasedEntryPercent must be larger than or equal to zero and less than one.");
			if (MaximumEquityPerPosPercent <= 0 || MaximumEquityPerPosPercent > 1) throw new Exception("MaximumEquityPerPosPercent must be larger than zero and less then one.");

			m_price = Bars;
			m_cumulativeDelta = BarsOfData(2);
			if (m_cumulativeDelta.Info.Resolution.DeltaType != DeltaType.Cumulative) throw new Exception("Second data feed must be a cumulative delta feed.");
			m_priceMA.Instrument = m_price;
			m_priceMA.Length = PriceMomentumLength;
			m_cumulativeDeltaMA.Instrument = m_cumulativeDelta;
			m_cumulativeDeltaMA.Length = VolumeMomentumLength;
		}

		//static DateTime s_testDate = DateTime.Parse("04/29/2026");

		protected override void CalcBar()
		{
			base.CalcBar();

			//if (Bars.Time[0].Date == s_testDate.Date)
			//	Output.WriteLine("TestPoint");

			if (StrategyInfo.MarketPosition == 0)
				calcBarEntry();
			else
				calcBarExit();
		}

		//methods
		private void calcBarEntry()
		{
			// top level filters to allow/disallow entries
			if (!AllowAsia && IsAsia()) return;
			if (!AllowOutsideRth && !IsRth()) return;

			//TODO - need to identify strong zones of confluence (however you define that)

			double priceMomentum = m_price.Close[0] - m_price.Close[1];
			double volumeMomentum = m_cumulativeDelta.Close[0] - m_cumulativeDelta.Close[1];
			//double priceMomentum = m_priceMA[0] - m_priceMA[5];
			//double volumeMomentum = m_cumulativeDeltaMA[0] - m_cumulativeDeltaMA[5];
			trace(ETraceLevel.Debug, $"priceMom({priceMomentum:F4}), volumeMom({volumeMomentum:F4})");
			if (priceMomentum < 0 && volumeMomentum > 0)
			{ 
				trace(ETraceLevel.Debug, $"Divergence detected: bar({m_price.CurrentBar}), priceMom({priceMomentum:F4}), volumeMom({volumeMomentum:F4})");
				int size = 0;
				double low = m_price.Low.Lowest(LowLookbackLength);
				// DER-02: StatusLine is the live quote (today's ask, or 0 offline) on every historical bar. Historical bars use the
				// close as the reference (the entry is MarketNextBar); real time uses the live ask when there is one.
				double refPrice = m_price.Close[0];
				if (Environment.IsRealTimeCalc)
				{
					double ask = m_price.StatusLine.Ask;
					if (ask > 0) refPrice = ask;
				}
				double risk = refPrice - low;
				if (risk <= 0)
				{
					trace(ETraceLevel.Debug, $"skip: refPrice({refPrice:F4}) not above low({low:F4})");
					return;
				}

				if (AbsoluteSize > 0)
				{
					size = AbsoluteSize;
					trace(ETraceLevel.Debug, $"Sending LE: size({size})");
				}
				else
				{
					// DER-03: contract sizing, |entry - stop| * BigPointValue dollars at risk per contract (the old equity/price
					// "shares" and points-as-dollars sizes were meaningless for futures), capped by MaximumEquityPerPosPercent of equityAvailable at the contract notional (PR #1 review).
					int riskBasedSize = stopLossBasedPositionSize(m_price, refPrice, low, RiskBasedEntryPercent, false);
					double contractNotional = refPrice * m_price.Info.BigPointValue;
					size = capByNotional(riskBasedSize, contractNotional, MaximumEquityPerPosPercent);
					trace(ETraceLevel.Debug, $"Sending LE: size({size}) riskBasedSize({riskBasedSize}) ref({refPrice:F4}) stop({low:F4}) risk({risk:F4}) bpv({m_price.Info.BigPointValue}) notional({contractNotional:F2}) maxEquityPct({MaximumEquityPerPosPercent})");
				}

				if (size <= 0)
				{
					trace(ETraceLevel.Debug, "skip: size 0");
					return;
				}
				m_le.Send(size);

				// TODO - clean this up
				m_stopLoss = low;
				m_targetPrice = refPrice + Math.Abs(risk * RiskRewardFactor);
				trace(ETraceLevel.Debug, $"SL({m_stopLoss:F4}), TP({m_targetPrice:F4})");
			}
		}

		private void calcBarExit()
		{
			//m_lxStop.Send(m_stopLoss);
			//m_lxTgt.Send(m_targetPrice);

			// entry is MarketNextBar, so on the bar the position is open
			// AvgEntryPrice is the actual fill
			double entry = StrategyInfo.AvgEntryPrice;
			double riskDist = Math.Abs(entry - m_stopLoss);
			m_targetPrice = entry + riskDist * RiskRewardFactor;   // 1:1 when RR = 1.0

			// optional safety: only send limit if it is above the market
			if (m_targetPrice > m_price.Close[0])
				m_lxTgt.Send(m_targetPrice);

			if (m_stopLoss < m_price.Close[0])
				m_lxStop.Send(m_stopLoss);
		}

	}
}