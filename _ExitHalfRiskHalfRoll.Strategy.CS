using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Drawing.Text;

namespace PowerLanguage.Strategy {
	/// <summary>
	/// Takes half of the position off at +1R and trails the rest. R is the first-stop distance: from the entry to one tick outside the
	/// prior bar on the entry resolution (long Low[1] - tick, short High[1] + tick), at least one tick.
	///	FirstExit: a half-size limit at entry + R (long) / entry - R (short) and GenerateStopLoss for the full position at -R, re-sent every bar.
	///	A 1-lot trade exits whole at +1R. SecondExit (the half filled, the position got smaller): the remaining lots get one stop order that
	///	trails the roll level (the entry bar's low - tick / high + tick, then the previous day's low - tick / high + tick once the entry day is
	///	over) and never loosens; GenerateStopLoss is no longer sent, so each stage has exactly one protective stop.
	///	Requires two data feeds, one on the same resolution as the strategy entry (e.g. 5-minutes) and one on a longer resolution	(e.g. daily).
	/// </summary>
	public class _ExitHalfRiskHalfRoll : PortfolioSignalObject {
		//constants


		//types
		//(State lives in the <legacy-exit-core> region below so tests/OrderFlowTests can drive it)

		//inputs


		//properties


		//attributes
		private State m_state;
		private double m_firstStopDistance;  //R: price distance from the entry to the first stop (>= one tick); feeds the GenerateStopLoss amount and the +1R target
		private double m_firstStopPrice;     //the -R stop as a price (entry -/+ R); where the SecondExit trailing stop starts
		private double m_targetPrice;        //the +1R half-exit limit price (entry +/- R)
		private double m_secondStopPrice;    //the current roll level (entry bar / previous day low - tick, high + tick)
		private double m_trailStopPrice;     //SecondExit stop: ratcheted towards the roll level, never loosened
		private double m_bigPointValue;      //guarded BigPointValue captured when the position opened; re-used by the per-bar GenerateStopLoss
		private double m_tick;               //guarded tick captured when the position opened; used by the roll level
		private int m_previousMarketPosition; //StrategyInfo.MarketPosition at the last transition check (backtest/playback fallback)
		private IInstrument m_entryResolution;
		private IInstrument m_longerResolution;
		private IOrderPriced m_lxTargetOrder;
		private IOrderPriced m_sxTargetOrder;
		private IOrderPriced m_leStopOrder;
		private IOrderPriced m_seStopOrder;

		//interface methods
		public _ExitHalfRiskHalfRoll(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			//Contracts.UserSpecified: the numLots passed to Send is the order size (with Contracts.Default MC sizes the order from Strategy
			//Properties and the half exit is not half); every Send below passes its lots explicitly
			m_lxTargetOrder = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Sell));
			m_sxTargetOrder = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, EOrderAction.BuyToCover));
			m_leStopOrder = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, EOrderAction.Sell));
			m_seStopOrder = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, EOrderAction.BuyToCover));
		}
		
		protected override void StartCalc() 
		{
			m_state = State.None;
			clearTrade();
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

			//special orders (GenerateStopLoss) and priced orders only stay live while they are re-sent on every calculation, so they are
			//sent here on every CalcBar while in a position, whatever the CalcReason. Each stage sends exactly one protective stop.
			switch (exitOrdersThisBar(m_state, PositionSide))
			{
				case State.FirstExit:
					//half off at +1R (halfExitLots is at least one lot: a 1-lot position exits whole at +1R; Sell/BuyToCover cannot reverse it)
					if (PositionSide > 0)
						m_lxTargetOrder.Send(m_targetPrice, halfExitLots(this.CurrentPosition.OpenLots));
					else
						m_sxTargetOrder.Send(m_targetPrice, halfExitLots(this.CurrentPosition.OpenLots));
					//the stage's one protective stop: full position at -R. M-INSTR F3b: GenerateStopLoss takes currency (per contract or per
					//position, CurSpecOrdersMode), not a price distance
					GenerateStopLoss(stopLossAmount(m_firstStopDistance, m_bigPointValue, CurSpecOrdersMode, this.CurrentPosition.OpenLots));
					break;
				case State.SecondExit:
					//the stage's one protective stop: the remaining lots at the trailing roll stop (GenerateStopLoss is not re-sent, so MC drops it)
					if (PositionSide > 0)
						m_leStopOrder.Send(m_trailStopPrice, remainingExitLots(this.CurrentPosition.OpenLots));
					else
						m_seStopOrder.Send(m_trailStopPrice, remainingExitLots(this.CurrentPosition.OpenLots));
					break;
			}
		}

		//methods
		private void checkPositionTransition()
		{
			int marketPosition = StrategyInfo.MarketPosition;
			if (!positionTransitionDue(m_previousMarketPosition, marketPosition)) return;
			int previousMarketPosition = m_previousMarketPosition;
			m_previousMarketPosition = marketPosition;
			marketPositionChanged(previousMarketPosition, marketPosition);
		}

		private void marketPositionChanged(int previousMarketPosition, int marketPosition)
		{
			bool newEntry = isNewEntry(m_state, previousMarketPosition, marketPosition);
			State next = stageAfterPositionChange(m_state, previousMarketPosition, marketPosition);
			if (next == State.None)
			{
				//clear the trade once we are flat
				m_state = State.None;
				clearTrade();
				return;
			}

			if (newEntry)
			{
				//M-INSTR F3a/F3b guards: a bad QuoteManager tick or BigPointValue is logged as an Error, then throws
				double tick, bigPointValue;
				try
				{
					tick = checkedTick(m_entryResolution.Info.MinMove, m_entryResolution.Info.PriceScale, m_entryResolution.Info.Name);
					bigPointValue = checkedBigPointValue(m_entryResolution.Info.BigPointValue, m_entryResolution.Info.Name);
				}
				catch (InvalidOperationException ex)
				{
					Output.WriteLine("{0}", "Error: " + ex.Message);
					throw;
				}
				m_tick = tick;
				m_bigPointValue = bigPointValue;
				bool isLong = marketPosition > 0;
				double entryPrice = this.EntryPrice();
				//R: one tick OUTSIDE the prior bar (long Low[1] - tick, short High[1] + tick), clamped to at least one tick (entry already beyond
				//that level) so neither the stop, the target nor the GenerateStopLoss amount is on the wrong side
				m_firstStopDistance = atLeastOneTick(firstStopDistance(entryPrice, m_entryResolution.Low[1], m_entryResolution.High[1], tick, isLong), tick);
				m_firstStopPrice = firstStopPrice(entryPrice, m_firstStopDistance, isLong);
				m_targetPrice = targetPrice(entryPrice, m_firstStopDistance, isLong);
				m_secondStopPrice = rollStopLevel(m_entryResolution.Low[0], m_entryResolution.High[0], tick, isLong);  //the ENTRY bar's low/high
				m_trailStopPrice = -1;
			}
			else if (m_state == State.FirstExit && next == State.SecondExit)
			{
				//the half filled: the trailing stop starts at the -R stop it replaces and ratchets to the roll level, never looser
				m_trailStopPrice = m_firstStopPrice;
				m_state = next;
				updateSecondExit();
			}
			//else an add-on, or a further partial exit in SecondExit: the stage and levels are unchanged (lots follow OpenLots each bar)
			m_state = next;
		}
		
		private void defaultProcessing()
		{
			if (PositionSide == 0)
				return;

			updateSecondExit();
		}

		private void updateSecondExit()
		{
			if (m_state == State.None) return;
			bool isLong = PositionSide > 0;
			var entryTime = this.EntryTime();
			if (entryTime.DayOfYear != m_longerResolution.Time[0].DayOfYear || entryTime.Year != m_longerResolution.Time[0].Year)
				m_secondStopPrice = rollStopLevel(m_longerResolution.Low[1], m_longerResolution.High[1], m_tick, isLong);  //the previous day's low/high
			if (m_state == State.SecondExit)
				m_trailStopPrice = ratchetStop(m_trailStopPrice, m_secondStopPrice, isLong);
		}

		private void clearTrade()
		{
			m_firstStopDistance = -1;
			m_firstStopPrice = -1;
			m_targetPrice = -1;
			m_secondStopPrice = -1;
			m_trailStopPrice = -1;
			m_bigPointValue = -1;
			m_tick = -1;
		}

		// <legacy-exit-core> M-INSTR PR-M1c (spec rev 2.3 §3.2 F3). MC-free apart from the typed ESpecOrdersMode: tests/OrderFlowTests/gen.sh
		// compiles this region against a mirror of ESpecOrdersMode.

		private enum State {
			None,
			FirstExit,    //half off at +1R, full-position GenerateStopLoss at -R
			SecondExit,   //remaining lots on a trailing roll stop (entry bar, then previous day low/high)
		}

		/// <summary>
		/// Which exit orders CalcBar sends on this bar: the current state while in a position, None when flat. Called on EVERY CalcBar, so the
		/// target, GenerateStopLoss and the trailing stop are re-sent each bar (MC cancels special and priced orders that are not re-sent).
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
		/// A position change that starts a new trade (R, target and roll level computed from the new entry): in a position after no trade
		/// (state None), or a reversal (the sign of the position flipped).
		/// </summary>
		private static bool isNewEntry(State state, int previousMarketPosition, int marketPosition)
		{
			if (marketPosition == 0) return false;
			return state == State.None || (previousMarketPosition > 0) != (marketPosition > 0) || previousMarketPosition == 0;
		}

		/// <summary>
		/// The stage after a position change: flat -> None; a new entry or reversal -> FirstExit; FirstExit with the same side and fewer lots
		/// (the +1R half filled, whole or in part) -> SecondExit; otherwise (an add-on, or a further reduction in SecondExit) unchanged.
		/// Lots are compared as long, so int.MinValue cannot overflow under checked arithmetic.
		/// </summary>
		private static State stageAfterPositionChange(State state, int previousMarketPosition, int marketPosition)
		{
			if (marketPosition == 0) return State.None;
			if (isNewEntry(state, previousMarketPosition, marketPosition)) return State.FirstExit;
			if (state == State.FirstExit && Math.Abs((long)marketPosition) < Math.Abs((long)previousMarketPosition)) return State.SecondExit;
			return state;
		}

		/// <summary>
		/// Lots for the +1R half exit: half the open lots (Math.Round, banker's), at least one. With one lot Math.Round(0.5) is 0, which would
		/// send a zero-lot order; one lot instead exits the whole position at +1R. Computed in double, so int.MinValue cannot overflow.
		/// </summary>
		private static int halfExitLots(int openLots)
		{
			return Math.Max(1, (int)Math.Round(Math.Abs((double)openLots) / 2, 0));
		}

		/// <summary>Lots for the SecondExit stop: the whole remaining position, passed explicitly (the orders are Contracts.UserSpecified).</summary>
		private static int remainingExitLots(int openLots)
		{
			if (openLots >= 0) return openLots;
			return openLots == int.MinValue ? int.MaxValue : -openLots;
		}

		/// <summary>The +1R half-exit limit price: entry + R for a long, entry - R for a short.</summary>
		private static double targetPrice(double entryPrice, double distance, bool isLong)
		{
			return isLong ? entryPrice + distance : entryPrice - distance;
		}

		/// <summary>The roll level for the SecondExit stop: one tick outside the bar/day (long low - tick, short high + tick).</summary>
		private static double rollStopLevel(double low, double high, double tick, bool isLong)
		{
			return isLong ? low - tick : high + tick;
		}

		/// <summary>
		/// Trailing stop that never loosens: a long stop only moves up, a short stop only moves down. A NaN level keeps the current stop.
		/// </summary>
		private static double ratchetStop(double currentStop, double level, bool isLong)
		{
			if (isLong) return level > currentStop ? level : currentStop;
			return level < currentStop ? level : currentStop;
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
			return mode == ESpecOrdersMode.PerPosition ? perContract * Math.Abs((double)openLots) : perContract;
		}
		// </legacy-exit-core>
	}
}