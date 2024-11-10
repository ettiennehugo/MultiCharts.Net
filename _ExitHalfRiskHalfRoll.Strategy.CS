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
		private enum State {
			None,
			FirstExit,    //profit = risk exit
			SecondExit,   //roll based on previous day low
		}

		//inputs


		//properties


		//attributes
		private State m_state;
		private double m_firstStopPrice;
		private double m_secondStopPrice;
		private IInstrument m_entryResolution;
		private IInstrument m_longerResolution;
		private IOrderPriced m_leStopOrder;
		private IOrderPriced m_seStopOrder;

		//interface methods
		public _ExitHalfRiskHalfRoll(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			m_leStopOrder = OrderCreator.Stop(new SOrderParameters(Contracts.Default, EOrderAction.Sell));
			m_seStopOrder = OrderCreator.Stop(new SOrderParameters(Contracts.Default, EOrderAction.BuyToCover));
		}
		
		protected override void StartCalc() 
		{
			m_firstStopPrice = -1;
			m_secondStopPrice = -1;
			m_entryResolution = BarsOfData(1);  //will be the same as bars of data for the strategy
			m_longerResolution = BarsOfData(2);
		}
		
		protected override void CalcBar() 
		{
			switch (Environment.CalcReason)
			{
				case CalculationReason.MarketPositionChange:
					marketPositionChanged();
					break;
				case CalculationReason.Default:
					defaultProcessing();
					break;
			}

			switch (m_state)
			{
				case State.FirstExit:
					//close half of the position at the original entry day low/high
					IMarketPosition marketPosition = this.CurrentPosition;
					if (PositionSide > 0)
						m_leStopOrder.Send(m_firstStopPrice, (int)Math.Round((double)this.CurrentPosition.OpenLots / 2, 0));
					else
						m_seStopOrder.Send(m_firstStopPrice, (int)Math.Round((double)this.CurrentPosition.OpenLots / 2, 0));
					break;
				case State.SecondExit:
					//close the remaining half of the position at low/high of the previous day
					if (PositionSide > 0)
						m_leStopOrder.Send(m_secondStopPrice);
					else
						m_seStopOrder.Send(m_secondStopPrice);
					break;
			}
		}

		//methods
		private void marketPositionChanged()
		{
			if (this.PositionSide == 0)
			{
				//clear intial risk	and previous low once we are flat
				m_state = State.None;
				m_firstStopPrice = -1;
				m_secondStopPrice = -1;
			}
			else
			{
				switch (m_state)
				{
					case State.None:
						m_state = State.FirstExit;
						if (this.PositionSide > 0)
						{
							m_firstStopPrice = this.EntryPrice() - m_entryResolution.Low[1] - (1 / m_entryResolution.Info.PriceScale * m_entryResolution.Info.BigPointValue);
							m_secondStopPrice = m_entryResolution.Low[0] - (1 / m_entryResolution.Info.PriceScale * m_entryResolution.Info.BigPointValue);  //initialize the stop price to the ENTRY day low
						}
						else
						{
							m_firstStopPrice = m_entryResolution.High[1] - this.EntryPrice() + (1 / m_entryResolution.Info.PriceScale * m_entryResolution.Info.BigPointValue);
							m_secondStopPrice = m_entryResolution.High[0] + (1 / m_entryResolution.Info.PriceScale * m_entryResolution.Info.BigPointValue);   //initialize the stop price to the ENTRY day high
						}
						break;
					case State.FirstExit:
						m_state = State.SecondExit;
						updateSecondExit();
						break;
				}

				//start sending orders
				if (m_state == State.FirstExit) GenerateStopLoss(m_firstStopPrice);
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
	}
}