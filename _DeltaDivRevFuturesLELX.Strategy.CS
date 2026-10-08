using System;
using System.Collections.Generic;
using PowerLanguage.Function;
using ATCenterProxy.interop;

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
	public class _DeltaDivRevFuturesLELX : _VolumeProfileFuturesBase
	{
		// A structural exit candidate. Eligibility is the conservative touch.
		// The order price is that touch, or the anchor when TargetAtTouch is off.
		private sealed class ExitCandidate
		{
			public double orderPrice;
			public double touchPrice;
			public int sources;
			public int kindRank;
			public double quality;
			public string kind;
		}

		[Input] public double RiskPercent { get; set; }          // > 0 and <= 1. 0.01 = 1% of available equity
		[Input] public double ProfitFactor { get; set; }         // > 0. min profit = risk equity * factor
		[Input] public int PivotStrength { get; set; }           // >= 1
		[Input] public int EqualLowTicks { get; set; }           // >= 0. stall tolerance vs prior swing low
		[Input] public double MinDeltaRise { get; set; }         // >= 0. delta-chart units
		[Input] public int MaxEntryDistanceTicks { get; set; }   // >= 1. max close distance above swing low
		[Input] public int StopBufferTicks { get; set; }         // >= 0. stop = swing low - buffer
		[Input] public bool RequireBullishClose { get; set; }
		[Input] public bool AllowOutsideRth { get; set; }
		[Input] public bool AllowAsia { get; set; }
		[Input] public bool OneTradePerGlobexDay { get; set; }
		[Input] public int MaxContracts { get; set; }            // 0 = no cap
		[Input] public int TargetScanCount { get; set; }         // >= 1. passed to GetConfluenceTargets
		[Input] public int ProfileLookback { get; set; }         // >= 1. completed profiles walked for POC/HVN
		[Input] public double HvnStrengthFloor { get; set; }     // 0..1. Node.strength minimum
		[Input] public bool TargetAtTouch { get; set; }          // order at touch; false = anchor/peak

		private IOrderMarket m_le;
		private IOrderPriced m_lxStop;
		private IOrderPriced m_lxTgt;

		private bool m_data2Missing;
		private bool m_hasPrevSwing;
		private double m_prevSwingLow;
		private int m_prevSwingBar;          // 1-based Status Bar # of the prior confirmed swing
		private int m_lastSwingBar;          // last swing bar identity already consumed

		private bool m_armed;
		private bool m_seenLong;
		private int m_armBar;
		private double m_stopPrice;
		private double m_targetPrice;
		private int m_qty;
		private DateTime m_entryGlobexDate;

		public _DeltaDivRevFuturesLELX(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();

			RiskPercent = 0.01;
			ProfitFactor = 2.0;
			PivotStrength = 3;
			EqualLowTicks = 1;
			MinDeltaRise = 0;
			MaxEntryDistanceTicks = 8;
			StopBufferTicks = 1;
			RequireBullishClose = true;
			AllowOutsideRth = false;
			AllowAsia = false;
			OneTradePerGlobexDay = false;
			MaxContracts = 0;
			TargetScanCount = 12;
			ProfileLookback = 4;
			HvnStrengthFloor = 0.35;
			TargetAtTouch = true;

			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "LE Div", EOrderAction.Buy));
			m_lxStop = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "LX Stop", EOrderAction.Sell, OrderExit.FromAll));
			m_lxTgt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "LX Target", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			validateInputs();
			m_data2Missing = false;
			m_hasPrevSwing = false;
			m_prevSwingLow = 0;
			m_prevSwingBar = 0;
			m_lastSwingBar = 0;
			m_entryGlobexDate = DateTime.MinValue;
			clearTicket();
			m_seenLong = false;
		}

		protected override void CalcBar()
		{
			// Steps VWAP, stamps VpExtra, stamps naked POCs, marks confluence dirty.
			base.CalcBar();
			maintainProtection();

			// The pivot is a closed-bar fact. Protective orders still resend intra-bar.
			if (Bars.Status != EBarState.Close)
				return;
			if (!deltaStreamReady())
				return;

			tryConfirmAndEnter();
		}

		private void validateInputs()
		{
			if (!(RiskPercent > 0.0) || RiskPercent > 1.0)
				throw new ArgumentException("RiskPercent must be greater than 0 and less than or equal to 1.");
			if (!(ProfitFactor > 0.0))
				throw new ArgumentException("ProfitFactor must be greater than 0.");
			if (PivotStrength < 1)
				throw new ArgumentException("PivotStrength must be 1 or greater.");
			if (EqualLowTicks < 0)
				throw new ArgumentException("EqualLowTicks must be 0 or greater.");
			if (MinDeltaRise < 0.0)
				throw new ArgumentException("MinDeltaRise must be 0.0 or greater.");
			if (MaxEntryDistanceTicks < 1)
				throw new ArgumentException("MaxEntryDistanceTicks must be 1 or greater.");
			if (StopBufferTicks < 0)
				throw new ArgumentException("StopBufferTicks must be 0 or greater.");
			if (MaxContracts < 0)
				throw new ArgumentException("MaxContracts must be 0 or greater.");
			if (TargetScanCount < 1)
				throw new ArgumentException("TargetScanCount must be 1 or greater.");
			if (ProfileLookback < 1)
				throw new ArgumentException("ProfileLookback must be 1 or greater.");
			if (HvnStrengthFloor < 0.0 || HvnStrengthFloor > 1.0)
				throw new ArgumentException("HvnStrengthFloor must be between 0.0 and 1.0.");
		}

		// Generated orders are bar-scoped. Resend the frozen stop and target on every
		// CalcBar while the long is open, including intra-bar. Do not flatten because
		// the session filter flipped, and do not move the stop.
		// MarketNextBar is still flat on the signal bar. Keep the ticket until that
		// working entry has been long and is flat again, so the fill bar still has a stop.
		private void maintainProtection()
		{
			int pos = StrategyInfo.MarketPosition;
			if (pos > 0)
			{
				m_seenLong = true;
				resendProtection();
				return;
			}

			if (m_seenLong)
			{
				clearTicket();
				m_seenLong = false;
				return;
			}

			if (!m_armed)
				return;

			int bar = Bars.FullSymbolData.Current;
			if (bar > m_armBar)
				resendProtection();

			// Fill bar closed and still flat: the market-next-bar entry did not fill.
			if (bar > m_armBar && Bars.Status == EBarState.Close)
				clearTicket();
		}

		private void resendProtection()
		{
			if (!m_armed || m_qty < 1)
				return;
			m_lxStop.Send(m_stopPrice, m_qty);
			m_lxTgt.Send(m_targetPrice, m_qty);
		}

		private void clearTicket()
		{
			m_armed = false;
			m_armBar = 0;
			m_stopPrice = 0;
			m_targetPrice = 0;
			m_qty = 0;
		}

		private bool deltaStreamReady()
		{
			if (m_data2Missing)
				return false;

			IInstrument delta = null;
			try
			{
				if (MaxDataStream >= 2)
					delta = BarsOfData(2);
			}
			catch
			{
				delta = null;
			}

			if (delta == null)
			{
				m_data2Missing = true;
				trace(ETraceLevel.Error, "Data2 cumulative delta is missing. No trades.");
				return false;
			}

			if (delta.Info.Resolution.DeltaType == DeltaType.None)
			{
				trace(ETraceLevel.Error, "Data2 is not setup as a cumulative delta chart.");
				throw new Exception("Data2 is not setup as a cumulative delta chart.");
			}

			return true;
		}

		private void tryConfirmAndEnter()
		{
			int strength = PivotStrength;
			int span = 2 * strength;
			if (Bars.CurrentBar < span)
				return;

			double swingLow = Bars.Low[strength];
			if (!(swingLow > 0) || double.IsNaN(swingLow) || double.IsInfinity(swingLow))
				return;

			for (int i = 0; i <= span; i++)
			{
				double low = Bars.Low[i];
				if (low < swingLow)
					return;
			}

			int swingBar = Bars.FullSymbolData.Current - strength;
			if (swingBar < 1 || swingBar == m_lastSwingBar)
				return;
			if (m_hasPrevSwing && swingBar <= m_prevSwingBar)
				return;

			// First confirmed swing arms the chain. A divergence needs a prior low.
			if (!m_hasPrevSwing)
			{
				rememberSwing(swingBar, swingLow);
				return;
			}

			double tick = tickSize();
			int prevAgo = Bars.FullSymbolData.Current - m_prevSwingBar;
			if (prevAgo < 0)
			{
				rememberSwing(swingBar, swingLow);
				return;
			}

			bool diverged = isDeltaDivergence(strength, prevAgo, swingLow, tick);
			bool canEnter = diverged
				&& StrategyInfo.MarketPosition == 0
				&& !m_armed
				&& sessionAllowsEntry()
				&& !blockedByDayLimit();
			if (canEnter)
				enterLong(swingBar, swingLow, tick);

			rememberSwing(swingBar, swingLow);
		}

		private void rememberSwing(int swingBar, double swingLow)
		{
			m_prevSwingBar = swingBar;
			m_prevSwingLow = swingLow;
			m_hasPrevSwing = true;
			m_lastSwingBar = swingBar;
		}

		private bool isDeltaDivergence(int swingAgo, int prevAgo, double swingLow, double tick)
		{
			// Lower low, or a stall within EqualLowTicks of the prior swing. A higher low is not a reversal.
			if (swingLow > m_prevSwingLow + EqualLowTicks * tick)
				return false;

			double deltaAtSwing;
			double deltaAtPrev;
			double deltaNow;
			try
			{
				IInstrument delta = BarsOfData(2);
				deltaAtSwing = delta.Close[swingAgo];
				deltaAtPrev = delta.Close[prevAgo];
				deltaNow = delta.Close[0];
			}
			catch
			{
				return false;
			}

			if (double.IsNaN(deltaAtSwing) || double.IsNaN(deltaAtPrev) || double.IsNaN(deltaNow))
				return false;
			if (deltaAtSwing < deltaAtPrev + MinDeltaRise)
				return false;
			// Higher delta low that is already rolling over is not an entry.
			if (!(deltaNow > deltaAtSwing))
				return false;

			double close = Bars.Close[0];
			if (close < swingLow)
				return false;
			if (close - swingLow > MaxEntryDistanceTicks * tick)
				return false;
			if (RequireBullishClose && !(close > Bars.Open[0]))
				return false;
			return true;
		}

		// Open Globex only. Default is RTH. AllowOutsideRth adds Europe or New York.
		// Asia stays off unless AllowAsia. Halt and weekend are not Open.
		private bool sessionAllowsEntry()
		{
			if (GetGlobexState() != GlobexState.Open)
				return false;
			if (InSession(MarketSession.Rth))
				return true;
			if (AllowOutsideRth && (InSession(MarketSession.Europe) || InSession(MarketSession.NewYork)))
				return true;
			if (AllowAsia && InSession(MarketSession.Asia))
				return true;
			return false;
		}

		private bool blockedByDayLimit()
		{
			if (!OneTradePerGlobexDay)
				return false;
			return m_entryGlobexDate == GlobexDate(Bars.Time[0]);
		}

		private void enterLong(int swingBar, double swingLow, double tick)
		{
			double close = Bars.Close[0];
			double stopPrice = swingLow - StopBufferTicks * tick;
			if (close <= stopPrice)
			{
				trace(ETraceLevel.Info, string.Format(
					"skip size {0:MM/dd/yyyy HH:mm} swing {1} close {2} is not above stop {3}",
					Bars.Time[0], swingLow, close, stopPrice));
				return;
			}

			double pointValue = Bars.Info.BigPointValue;
			if (double.IsNaN(pointValue) || double.IsInfinity(pointValue) || pointValue <= 0)
			{
				trace(ETraceLevel.Error, "BigPointValue is missing or not positive. No trade.");
				return;
			}

			// Dollar risk per contract. Do not call stopLossBasedPositionSize — that
			// treats the raw price distance as dollars and oversizes ES by BigPointValue.
			// qty = floor( equityAvailable(RiskPercent) / dollarRiskPerContract ), round lots off.
			double dollarRiskPerContract = Math.Round(Math.Abs(close - stopPrice) * pointValue, 2);
			int qty = riskBasedPositionSize(dollarRiskPerContract, RiskPercent, false);
			if (MaxContracts > 0 && qty > MaxContracts)
				qty = MaxContracts;
			if (qty < 1)
			{
				trace(ETraceLevel.Info, string.Format(
					"skip size {0:MM/dd/yyyy HH:mm} swing {1} stop {2} pointValue {3} dollarRiskPerContract {4} qty {5}",
					Bars.Time[0], swingLow, stopPrice, pointValue, dollarRiskPerContract, qty));
				return;
			}

			double riskEquity = equityAvailable(RiskPercent);
			double minProfit = riskEquity * ProfitFactor;
			double minTarget = close + minProfit / (pointValue * qty);
			ExitCandidate target;
			int examined;
			if (!trySelectTarget(close, pointValue, qty, minProfit, out target, out examined))
			{
				trace(ETraceLevel.Info, string.Format(
					"skip target {0:MM/dd/yyyy HH:mm} riskEquity {1} minProfit {2} minTarget {3} candidates {4}",
					Bars.Time[0], riskEquity, minProfit, minTarget, examined));
				return;
			}

			m_stopPrice = stopPrice;
			m_targetPrice = target.orderPrice;
			m_qty = qty;
			m_armed = true;
			m_armBar = Bars.FullSymbolData.Current;
			m_entryGlobexDate = GlobexDate(Bars.Time[0]);

			m_le.Send(qty);
			m_lxStop.Send(stopPrice, qty);
			m_lxTgt.Send(target.orderPrice, qty);

			double profit = profitDollars(target.orderPrice, close, pointValue, qty);
			trace(ETraceLevel.Info, string.Format(
				"entry {0:MM/dd/yyyy HH:mm} swingBar {1} swingLow {2} stop {3} qty {4} pointValue {5} dollarRiskPerContract {6} riskEquity {7} minProfit {8} target {9} {10} profitDollars {11}",
				Bars.Time[0], swingBar, swingLow, stopPrice, qty, pointValue, dollarRiskPerContract,
				riskEquity, minProfit, target.kind, target.orderPrice, profit));
		}

		// Nearest structural level that pays the factor. The factor is a floor, not a
		// target to maximize. No eligible level means no trade — do not invent a limit
		// at the raw dollar price.
		private bool trySelectTarget(double close, double pointValue, int qty, double minProfit, out ExitCandidate best, out int examined)
		{
			best = null;
			examined = 0;

			List<ConfluenceTarget> zones = GetConfluenceTargets(close, EVpDirection.Bullish, TargetScanCount);
			if (zones != null)
			{
				for (int i = 0; i < zones.Count; i++)
				{
					ConfluenceTarget t = zones[i];
					if (t == null || t.zone == null)
						continue;
					// Engine already drops Rejection and Mixed. Keep Magnet and Acceptance only.
					if (t.zone.role != EConfluenceRole.Magnet && t.zone.role != EConfluenceRole.Acceptance)
						continue;

					double touch = t.touch > 0 ? t.touch : t.zone.low;
					double anchor = t.zone.center > 0 ? t.zone.center : t.price;
					double order = TargetAtTouch ? touch : anchor;
					int rank = t.zone.role == EConfluenceRole.Magnet ? 3 : 2;
					string kind = t.zone.role == EConfluenceRole.Magnet ? "Magnet" : "Acceptance";
					consider(ref best, ref examined, touch, order, t.zone.independentSources, rank, t.zone.acceptanceScore, kind, close, pointValue, qty, minProfit);
				}
			}

			List<int> streams = new List<int>();
			addStream(streams, 1);
			addStream(streams, ConfluenceDataStream1);
			addStream(streams, ConfluenceDataStream2);
			addStream(streams, ConfluenceDataStream3);

			for (int s = 0; s < streams.Count; s++)
			{
				int stream = streams[s];
				int asOf = streamBar(stream);
				if (asOf < 1)
					continue;

				List<VpExtra> naked = NakedPocs(stream, asOf);
				if (naked != null)
				{
					for (int i = 0; i < naked.Count; i++)
					{
						VpExtra extra = naked[i];
						if (extra == null || !(extra.poc > 0))
							continue;
						consider(ref best, ref examined, extra.poc, extra.poc, 1, 4, 1.0, "NakedPoc", close, pointValue, qty, minProfit);
					}
				}

				collectCompletedProfiles(stream, asOf, close, pointValue, qty, minProfit, ref best, ref examined);
			}

			return best != null;
		}

		private void collectCompletedProfiles(int stream, int asOf, double close, double pointValue, int qty, double minProfit, ref ExitCandidate best, ref int examined)
		{
			int barIndex = asOf;
			int completed = 0;
			HashSet<int> seen = new HashSet<int>();
			while (barIndex >= 1 && completed < ProfileLookback)
			{
				VpExtra extra;
				try
				{
					extra = ExtraVolumeProfileForBar(stream, barIndex);
				}
				catch
				{
					break;
				}
				if (extra == null)
				{
					barIndex--;
					continue;
				}

				int start = extra.startBar;
				if (start > 0 && !seen.Add(start))
				{
					barIndex = start < barIndex ? start - 1 : barIndex - 1;
					continue;
				}

				int next = start > 0 && start <= barIndex ? start - 1 : barIndex - 1;
				// Developing profile is not a target and does not consume lookback.
				if (!extra.pocComplete)
				{
					barIndex = next;
					continue;
				}

				completed++;
				if (extra.poc > 0)
					consider(ref best, ref examined, extra.poc, extra.poc, 1, 1, 1.0, "POC", close, pointValue, qty, minProfit);

				List<Node> hvns = extra.hvns;
				if (hvns != null)
				{
					for (int i = 0; i < hvns.Count; i++)
					{
						Node node = hvns[i];
						if (!(node.peak > 0) || node.strength < HvnStrengthFloor)
							continue;
						consider(ref best, ref examined, node.peak, node.peak, 1, 0, node.strength, "HVN", close, pointValue, qty, minProfit);
					}
				}
				barIndex = next;
			}
		}

		private void consider(ref ExitCandidate best, ref int examined, double touch, double order, int sources, int kindRank, double quality, string kind, double close, double pointValue, int qty, double minProfit)
		{
			if (!(touch > 0) || !(order > 0) || double.IsNaN(touch) || double.IsNaN(order))
				return;
			examined++;
			// Touch must clear the dollar hurdle even when the order sits at the anchor.
			if (profitDollars(touch, close, pointValue, qty) + 0.01 < minProfit)
				return;
			if (!(order > close))
				return;

			ExitCandidate candidate = new ExitCandidate();
			candidate.orderPrice = order;
			candidate.touchPrice = touch;
			candidate.sources = sources;
			candidate.kindRank = kindRank;
			candidate.quality = quality;
			candidate.kind = kind;
			if (best == null || prefer(candidate, best))
				best = candidate;
		}

		// Nearest touch first. Tie-break: more independent sources, then NakedPoc,
		// Magnet, Acceptance, POC, HVN, then strength / acceptanceScore.
		private static bool prefer(ExitCandidate candidate, ExitCandidate incumbent)
		{
			int price = candidate.touchPrice.CompareTo(incumbent.touchPrice);
			if (price != 0)
				return price < 0;
			if (candidate.sources != incumbent.sources)
				return candidate.sources > incumbent.sources;
			if (candidate.kindRank != incumbent.kindRank)
				return candidate.kindRank > incumbent.kindRank;
			return candidate.quality > incumbent.quality;
		}

		private static double profitDollars(double price, double close, double pointValue, int qty)
		{
			return (price - close) * pointValue * qty;
		}

		private static void addStream(List<int> streams, int stream)
		{
			if (stream <= 0)
				return;
			for (int i = 0; i < streams.Count; i++)
				if (streams[i] == stream)
					return;
			streams.Add(stream);
		}

		private int streamBar(int stream)
		{
			try
			{
				if (stream < 1 || stream > MaxDataStream)
					return -1;
				IInstrument bars = stream == 1 ? Bars : BarsOfData(stream);
				if (bars == null)
					return -1;
				int n = bars.FullSymbolData.Current;
				return n >= 1 ? n : -1;
			}
			catch
			{
				return -1;
			}
		}

		private double tickSize()
		{
			double tick = Bars.Info.MinMove / Bars.Info.PriceScale;
			if (double.IsNaN(tick) || double.IsInfinity(tick) || tick <= 0)
				return 0.01;
			return tick;
		}
	}
}
