using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Drawing.Text;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Exits half of the position once the risk level is reached and the remaining half is free rolled based on the previous days low.
	/// The risk is computed based off the previous bar on the entry bar on the trading resolution, should typically be a 1/5/10/15-minute bar.
	///	Requires two data feeds, one on the same resolution as the strategy entry (e.g. 5-minutes) and one on a longer resolution	(e.g. daily).
	/// </summary>
	public class _ExitHalfRiskHalfRoll : PortfolioSignalObject {
		//constants


		//types
		//(State lives in the <legacy-exit-core> region below so tests/OrderFlowTests can drive exitOrdersThisBar)

		//inputs


		//properties


		//attributes
		private State m_state;
		private double m_firstStopDistance;  //price distance from the entry to the first stop (>= one tick); feeds the GenerateStopLoss amount
		private double m_firstStopPrice;     //the first stop as a price (entry -/+ m_firstStopDistance); what m_leStopOrder/m_seStopOrder are sent
		private double m_secondStopPrice;
		private double m_bigPointValue;      //guarded BigPointValue captured when the position opened; re-used by the per-bar GenerateStopLoss
		private int m_previousMarketPosition; //StrategyInfo.MarketPosition at the last transition check (backtest/playback fallback)
		private IInstrument m_entryResolution;
		private IInstrument m_longerResolution;
		private IOrderPriced m_leStopOrder;
		private IOrderPriced m_seStopOrder;

		//interface methods
		public _ExitHalfRiskHalfRoll(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//Contracts.UserSpecified: the numLots passed to Send is the order size (with Contracts.Default MC sizes the order from Strategy
			//Properties and the half exit is not half); every Send below passes its lots explicitly
			m_leStopOrder = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Sell));
			m_seStopOrder = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, EOrderAction.BuyToCover));
		}
		
		protected override void StartCalc() 
		{
			m_state = State.None;
			m_firstStopDistance = -1;
			m_firstStopPrice = -1;
			m_secondStopPrice = -1;
			m_bigPointValue = -1;
			m_previousMarketPosition = 0;
			m_entryResolution = BarsOfData(1);  //will be the same as bars of data for the strategy
			m_longerResolution = BarsOfData(2);
		}
		
		protected override void CalcBar() 
		{
			//MC raises MarketPositionChange in real time but not in backtest or playback (see _ChartTrading_Advanced), so the Default path
			//also compares StrategyInfo.MarketPosition with the value at the last check. Both paths go through checkPositionTransition, which
			//runs the transition only when the position differs from the last check and then records it, so a change seen by both an
			//event and a Default calc fires once
			switch (Environment.CalcReason)
			{
				case CalculationReason.MarketPositionChange:
					checkPositionTransition();
					break;
				case CalculationReason.Default:
					checkPositionTransition();
					defaultProcessing();
					break;
			}

			//special orders (GenerateStopLoss) and the stop orders only stay live while they are re-sent on every calculation, so they are
			//sent here on every CalcBar while in a position, whatever the CalcReason, not only on the bar the position changed
			switch (exitOrdersThisBar(m_state, PositionSide))
			{
				case State.FirstExit:
					//close half of the position at the first stop PRICE (outside the prior bar's low/high), full-position stop loss as a backstop
					//halfExitLots is at least one lot: a 1-lot position exits whole at the first stop (Sell/BuyToCover cannot reverse it)
					if (PositionSide > 0)
						m_leStopOrder.Send(m_firstStopPrice, halfExitLots(this.CurrentPosition.OpenLots));
					else
						m_seStopOrder.Send(m_firstStopPrice, halfExitLots(this.CurrentPosition.OpenLots));
					//M-INSTR F3b: GenerateStopLoss takes currency (per contract or per position, CurSpecOrdersMode), not a price distance
					GenerateStopLoss(stopLossAmount(m_firstStopDistance, m_bigPointValue, CurSpecOrdersMode, this.CurrentPosition.OpenLots));
					break;
				case State.SecondExit:
					//close the remaining half of the position at low/high of the previous day
					if (PositionSide > 0)
						m_leStopOrder.Send(m_secondStopPrice, remainingExitLots(this.CurrentPosition.OpenLots));
					else
						m_seStopOrder.Send(m_secondStopPrice, remainingExitLots(this.CurrentPosition.OpenLots));
					break;
			}
		}

		//methods
		private void checkPositionTransition()
		{
			int marketPosition = StrategyInfo.MarketPosition;
			if (!positionTransitionDue(m_previousMarketPosition, marketPosition)) return;
			m_previousMarketPosition = marketPosition;
			marketPositionChanged();
		}

		private void marketPositionChanged()
		{
			if (this.PositionSide == 0)
			{
				//clear intial risk	and previous low once we are flat
				m_state = State.None;
				m_firstStopDistance = -1;
				m_firstStopPrice = -1;
				m_secondStopPrice = -1;
				m_bigPointValue = -1;
			}
			else
			{
				//M-INSTR F3a/F3b guards: a bad QuoteManager tick or BigPointValue throws (message only; this study has no logger)
				double tick = checkedTick(m_entryResolution.Info.MinMove, m_entryResolution.Info.PriceScale, m_entryResolution.Info.Name);
				double bigPointValue = checkedBigPointValue(m_entryResolution.Info.BigPointValue, m_entryResolution.Info.Name);
				switch (m_state)
				{
					case State.None:
						m_state = State.FirstExit;
						m_bigPointValue = bigPointValue;
						bool isLong = this.PositionSide > 0;
						double entryPrice = this.EntryPrice();
						//first stop one tick OUTSIDE the prior bar (long Low[1] - tick, short High[1] + tick); the distance is clamped to at least
						//one tick (entry already beyond that level) so neither the stop price nor the GenerateStopLoss amount is on the wrong side
						m_firstStopDistance = atLeastOneTick(firstStopDistance(entryPrice, m_entryResolution.Low[1], m_entryResolution.High[1], tick, isLong), tick);
						m_firstStopPrice = firstStopPrice(entryPrice, m_firstStopDistance, isLong);
						if (isLong)
							m_secondStopPrice = m_entryResolution.Low[0] - tick;  //initialize the stop price to the ENTRY day low
						else
							m_secondStopPrice = m_entryResolution.High[0] + tick;   //initialize the stop price to the ENTRY day high
						break;
					case State.FirstExit:
						m_state = State.SecondExit;
						updateSecondExit();
						break;
				}
			}
		}
		
		private void defaultProcessing()
		{
			if (PositionSide == 0)
				return;

			updateSecondExit();
		}

		private void updateSecondExit()
		{
			var entryTime = this.EntryTime();
			if (entryTime.DayOfYear != m_longerResolution.Time[0].DayOfYear || entryTime.Year != m_longerResolution.Time[0].Year)
			{
				if (PositionSide > 0)
					m_secondStopPrice = m_longerResolution.Low[1];
				else
					m_secondStopPrice = m_longerResolution.High[1];
			}
		}

		// <legacy-exit-core> M-INSTR PR-M1c (spec rev 2.3 §3.2 F3). MC-free apart from the typed ESpecOrdersMode: tests/OrderFlowTests/gen.sh
		// compiles this region against a mirror of ESpecOrdersMode.

		private enum State {
			None,
			FirstExit,    //profit = risk exit
			SecondExit,   //roll based on previous day low
		}

		/// <summary>
		/// Which exit orders CalcBar sends on this bar: the current state while in a position, None when flat. Called on EVERY CalcBar, so the
		/// stop orders and GenerateStopLoss are re-sent each bar (MC cancels special and priced orders that are not re-sent on a calculation).
		/// </summary>
		private static State exitOrdersThisBar(State state, int positionSide)
		{
			return positionSide == 0 ? State.None : state;
		}

		/// <summary>
		/// Whether the state transition runs: only when StrategyInfo.MarketPosition differs from the value at the last check. Used by both the
		/// MarketPositionChange event and the Default-calc fallback (backtest/playback raise no event); the caller records the new value, so a
		/// change seen by both paths transitions once.
		/// </summary>
		private static bool positionTransitionDue(int previousMarketPosition, int marketPosition)
		{
			return previousMarketPosition != marketPosition;
		}

		/// <summary>
		/// Lots for the first (half) exit: half the open lots (Math.Round, as before), at least one. With one lot Math.Round(0.5) is 0, which
		/// would send a zero-lot order; one lot instead exits the whole position at the first stop, the same price GenerateStopLoss holds.
		/// </summary>
		private static int halfExitLots(int openLots)
		{
			return Math.Max(1, (int)Math.Round((double)Math.Abs(openLots) / 2, 0));
		}

		/// <summary>Lots for the second exit: the whole remaining position, passed explicitly (the orders are Contracts.UserSpecified).</summary>
		private static int remainingExitLots(int openLots)
		{
			return Math.Abs(openLots);
		}

		/// <summary>
		/// First-stop price distance from the entry, one tick outside the prior bar: long entry - (priorLow - tick), short (priorHigh + tick) - entry.
		/// May be negative/sub-tick when the entry is already beyond that level; the caller clamps it with atLeastOneTick.
		/// </summary>
		private static double firstStopDistance(double entryPrice, double priorLow, double priorHigh, double tick, bool isLong)
		{
			return isLong ? entryPrice - (priorLow - tick) : (priorHigh + tick) - entryPrice;
		}

		/// <summary>First stop as a PRICE for the stop orders: entry - distance for a long, entry + distance for a short.</summary>
		private static double firstStopPrice(double entryPrice, double distance, bool isLong)
		{
			return isLong ? entryPrice - distance : entryPrice + distance;
		}

		/// <summary>M-INSTR F3a: one tick in price = MinMove / PriceScale (ES 25 / 100 = 0.25). Was 1 / PriceScale * BigPointValue (0.5 on ES).</summary>
		private static double oneTick(double minMove, double priceScale)
		{
			return minMove / priceScale;
		}

		/// <summary>M-INSTR F3a guard: oneTick, throwing if it is not a positive finite price (zero/negative/NaN/infinite MinMove or PriceScale).</summary>
		private static double checkedTick(double minMove, double priceScale, string symbol)
		{
			double tick = oneTick(minMove, priceScale);
			if (!(tick > 0) || double.IsInfinity(tick))
				throw new InvalidOperationException("M-INSTR _ExitHalfRiskHalfRoll: invalid tick " + tick + " for symbol " + symbol + " (MinMove " + minMove
					+ ", PriceScale " + priceScale + "); the stop offsets need a positive finite tick, fix in QuoteManager.");
			return tick;
		}

		/// <summary>M-INSTR F3b guard: BigPointValue must be a positive finite currency value per point, else the stop amount is meaningless.</summary>
		private static double checkedBigPointValue(double bigPointValue, string symbol)
		{
			if (!(bigPointValue > 0) || double.IsInfinity(bigPointValue))
				throw new InvalidOperationException("M-INSTR _ExitHalfRiskHalfRoll: invalid BigPointValue " + bigPointValue + " for symbol " + symbol
					+ "; the GenerateStopLoss amount needs a positive finite BigPointValue, fix in QuoteManager.");
			return bigPointValue;
		}

		/// <summary>
		/// M-INSTR F3b: the first-stop distance, at least one tick. A negative/sub-tick distance (entry already beyond the prior bar's
		/// low/high) or NaN becomes one tick: the tightest valid protective stop, never a negative GenerateStopLoss amount and never no stop.
		/// </summary>
		private static double atLeastOneTick(double distance, double tick)
		{
			return distance >= tick ? distance : tick;
		}

		/// <summary>
		/// M-INSTR F3b: GenerateStopLoss amount (currency) for a price distance: distance * BigPointValue per contract (PerContract), times the
		/// open contracts per position (PerPosition, PG §4.6.2: "Amount ... in currency per entire position"). mode = the strategy's
		/// CurSpecOrdersMode (this study does not set it; the last signal that sets it wins), so the stop sits the same distance from the entry
		/// in either mode.
		/// </summary>
		private static double stopLossAmount(double distance, double bigPointValue, ESpecOrdersMode mode, int openLots)
		{
			double perContract = distance * bigPointValue;
			return mode == ESpecOrdersMode.PerPosition ? perContract * Math.Abs(openLots) : perContract;
		}
		// </legacy-exit-core>
	}
}