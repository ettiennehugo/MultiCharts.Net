using PowerLanguage.Function;
using System;
using System.Runtime.CompilerServices;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// THROW-AWAY TEST SIGNAL for Portfolio Trader live checks P5b L1-L7 (specs/m-port-spec.md §9, specs/live-check-sheet.md Check 7).
	/// Not derived from _CustomSignal on purpose (stands alone; the saved settings of the real strategies are not touched).
	/// Pair it with the MM signal _TestPortMM (_TestPortMM.Signal.CS). Delete both after the sitting.
	///
	/// COMMON SETUP (Check 7)
	///   1. Import _TestPortArm.Strategy.CS, _TestPortMM.Signal.CS (and _PortMM.Function.CS if not already imported) into the
	///      PowerLanguage .NET Editor and compile.
	///   2. Portfolio Trader: new portfolio, 2 strategies, each with ONLY this signal: index 0 = @ES 1 min, index 1 = @NQ 1 min,
	///      both dxFeed. (L7: index 0 = @ES 1 min, index 1 = @CL 5 min.) Symbol-table order defines MyIndex (0 = first row).
	///   3. Portfolio Settings tab -> Money management signal -> Select -> _TestPortMM. Exposure / Max % Capital at Risk per
	///      Position = 100% / 100% so MC's own Money & Risk layer never cuts an order. Strategy Properties trade size = 1 contract.
	///   4. Set the SAME Test and Variant on _TestPortArm (both strategies) and on _TestPortMM.
	///   5. Backtest first (1 session of data is enough). L6 also: real-time PT on the IBKR paper profile, then restart PT.
	///   6. Read the PowerLanguage .NET Editor Output; every line starts with "P5b L&lt;n&gt;" so it can be filtered/grepped.
	///
	/// INPUTS: Test = "L1".."L7" (L5/L8 need no special arm mode: use any, e.g. L1), Variant = "a"/"b"/"c" (only used by the MM).
	///
	/// BEHAVIOUR PER TEST (this signal)
	///   L1, L7  buy next bar market (Strategy Properties size) on every bar while flat; then holds.
	///   L2      buy next bar market with a CODED size of 3 contracts every bar while flat; then holds.
	///   L3      buys once and holds forever (no exit order) - only the MM's ForceClosePosition can close it.
	///   L4      buys once, then sends a protective sell stop every bar at entry - 10 ticks (journals "stop crossed" when a
	///           bar's low trades through it, so a missing fill under Paused is visible).
	///   L6      no orders; publishes a counter object (_TestPortCounter) once in StartCalc under "test.counter", increments it
	///           every bar, journals its reference hash (RuntimeHelpers.GetHashCode) and value; also publishes the hash as a
	///           scalar "test.counterHash" so the MM can compare across sides.
	///   All     publishes pm.stamp = Bars.Time[0] (and test.bar = CurrentBar) every bar and journals every fill:
	///           "P5b Lx ARM#i FILL entry|exit time(..) price(..) size(..)" plus "P5b Lx ARM#i STAMP .." each bar.
	///
	/// EXPECTED OUTCOMES: see the per-test table in _TestPortMM.Signal.CS (the MM header is the pass/fail sheet).
	/// </summary>
	public class _TestPortArm : PortfolioSignalObject
	{
		[Input] public string Test { get; set; }
		[Input] public string Variant { get; set; }

		public const string KeyCounter = "test.counter", KeyCounterHash = "test.counterHash", KeyBar = "test.bar";
		private IOrderMarket m_le, m_leSized;
		private IOrderPriced m_stop;
		private int m_lastPos, m_lastClosed;
		private bool m_entered, m_stopLogged;
		private double m_stopPrice;
		private _TestPortCounter m_counter;
		private string m_t, m_tag;

		public _TestPortArm(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			Test = "L1"; Variant = "a";
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "TstLE", EOrderAction.Buy));
			m_leSized = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "TstLE3", EOrderAction.Buy));
			m_stop = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "TstStop", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			m_t = (Test ?? "L1").Trim().ToUpperInvariant();
			m_lastPos = 0; m_lastClosed = 0; m_entered = false; m_stopLogged = false; m_stopPrice = double.NaN;
			m_tag = "P5b " + m_t + " ARM";
			if (m_t == "L6")
			{
				m_counter = new _TestPortCounter();
				MyPortfolioData[KeyCounter] = m_counter;
				MyPortfolioData[KeyCounterHash] = RuntimeHelpers.GetHashCode(m_counter);
				j($"START counter created({m_counter.Created:yyyy-MM-dd HH:mm:ss.fff}) hash({RuntimeHelpers.GetHashCode(m_counter)}) value({m_counter.Value}) appDomain({AppDomain.CurrentDomain.Id}:{AppDomain.CurrentDomain.FriendlyName}) instances({_TestPortCounter.Instances})");
			}
			else j($"START test({m_t}) variant({Variant}) symbol({Bars.Info.Name}) appDomain({AppDomain.CurrentDomain.Id})");
		}

		protected override void CalcBar()
		{
			int pos = StrategyInfo.MarketPosition;
			journalFills(pos);

			MyPortfolioData[_PortKeys.Stamp] = Bars.Time[0];
			MyPortfolioData[KeyBar] = Bars.CurrentBar;
			j($"STAMP bar({Bars.CurrentBar}) time({Bars.Time[0]:yyyy-MM-dd HH:mm}) pos({pos})");

			switch (m_t)
			{
				case "L2":
					if (pos == 0) m_leSized.Send(3);
					break;
				case "L3":
					if (pos == 0 && !m_entered) { m_le.Send(); }
					break;
				case "L4":
					if (pos == 0 && !m_entered) m_le.Send();
					if (pos > 0)
					{
						if (double.IsNaN(m_stopPrice))
						{
							double tick = Bars.Info.MinMove / Bars.Info.PriceScale;
							m_stopPrice = StrategyInfo.AvgEntryPrice - 10 * tick;
							j($"STOP set price({m_stopPrice}) entry({StrategyInfo.AvgEntryPrice})");
						}
						if (Bars.Low[0] <= m_stopPrice && !m_stopLogged) { m_stopLogged = true; j($"STOP crossed bar({Bars.CurrentBar}) time({Bars.Time[0]:yyyy-MM-dd HH:mm}) low({Bars.Low[0]}) stop({m_stopPrice}) pos({pos})"); }
						m_stop.Send(m_stopPrice);
					}
					break;
				case "L6":
					if (m_counter != null)
					{
						m_counter.Value++;
						object o = MyPortfolioData.ContainsKey(KeyCounter) ? MyPortfolioData[KeyCounter] : null;
						j($"COUNTER hash({RuntimeHelpers.GetHashCode(m_counter)}) value({m_counter.Value}) storedSame({ReferenceEquals(o, m_counter)}) created({m_counter.Created:HH:mm:ss.fff}) rt({Environment.IsRealTimeCalc}) auto({Environment.IsAutoTradingMode})");
					}
					break;
				default:   // L1, L5, L7, L8
					if (pos == 0) m_le.Send();
					break;
			}
		}

		private void journalFills(int pos)
		{
			if (pos != m_lastPos)
			{
				if (pos != 0 && m_lastPos == 0)
				{
					m_entered = true;
					DateTime t = Bars.Time[0];
					try { var ot = CurrentPosition.OpenTrades; if (ot != null && ot.Count > 0) t = ot[0].EntryOrder.Time; } catch (Exception) { }
					j($"FILL entry time({t:yyyy-MM-dd HH:mm:ss}) price({StrategyInfo.AvgEntryPrice}) size({pos}) seenOnBar({Bars.CurrentBar} {Bars.Time[0]:HH:mm}) open({Bars.Open[0]})");
				}
				else
				{
					string s = $"FILL exit size({m_lastPos}->{pos}) seenOnBar({Bars.CurrentBar} {Bars.Time[0]:HH:mm}) barOpen({Bars.Open[0]}) prevClose({Bars.Close[1]})";
					try
					{
						IMarketPosition prev = Positions != null && Positions.Count > 1 ? Positions[1] : null;
						if (prev != null && prev.ClosedTrades != null && prev.ClosedTrades.Count > 0)
						{
							ITradeOrder x = prev.ClosedTrades[prev.ClosedTrades.Count - 1].ExitOrder;
							s += $" time({x.Time:yyyy-MM-dd HH:mm:ss}) price({x.Price}) name({x.Name}) contracts({x.Contracts})";
						}
					}
					catch (Exception) { }
					j(s);
				}
				m_lastPos = pos;
			}
		}

		private void j(string msg) { Output.WriteLine($"{m_tag}#{Bars.Info.Name} {msg}"); }
	}

	/// <summary>L6 probe object shared by reference through IPortfolioData.</summary>
	public sealed class _TestPortCounter
	{
		private static int s_instances;
		public static int Instances { get { return s_instances; } }
		public int Value;
		public readonly DateTime Created = DateTime.Now;
		public _TestPortCounter() { System.Threading.Interlocked.Increment(ref s_instances); }
	}
}
