// **Features (direct).** F-ROLL (§5.7), F-JRNL (§5.13), F-EVT (§5.14), M-AVWAP (§5.15).
//
// **What it does** (from the file header):
//   SW-02 Connors RSI(2) pullback with the AVWAP filter variant (long only, daily).
//   Entry: close > SMA200 and RSI(2) < 10 (and close above the latest valid swing-low AVWAP) -> buy next open.
//   Exit: close > SMA5, or after 5 daily bars. Disaster stop 3 x ATR(20) daily, floored.
//   Chart: Data1 = @ES daily, RTH session template, dxFeed continuous UNADJUSTED (roll gaps distort SMA200: see pitfalls).
//   Features: M-AVWAP (AvwapEvents = SwingLow -> AvwapManager.Latest), M-PA (forced on by the swing anchor), averageTrueRange, F-JRNL.
//
// **Code.**
//   Manual v4 example 16 - SW-02 Connors RSI(2) pullback with the AVWAP filter variant (long only, daily).
//   Entry: close > SMA200 and RSI(2) < 10 (and close above the latest valid swing-low AVWAP) -> buy next open.
//   Exit: close > SMA5, or after 5 daily bars. Disaster stop 3 x ATR(20) daily, floored.
//
// Chart: 
//   Data1 = @ES daily, RTH session template, dxFeed continuous UNADJUSTED (roll gaps distort SMA200: see pitfalls).
// Features: M-AVWAP (AvwapEvents = SwingLow -> AvwapManager.Latest), M-PA (forced on by the swing anchor), averageTrueRange, F-JRNL.
//
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _SW_02_SwingPullback : _CustomSignal
	{
		protected sealed class Sw02Config
		{
			public int contracts = 1;
			public int smaLong = 200, smaExit = 5, rsiLength = 2, maxHoldBars = 5, atrLength = 20;
			public double rsiEntry = 10, disasterAtr = 3.0;
			public bool avwapFilter = true;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Sw02Config Cfg { get; } = new Sw02Config();

		private IOrderMarket m_le, m_lx;
		private IOrderPriced m_lxStp;
		private double m_stop = double.NaN;
		private int m_entryBar = -1;

		public _SW_02_SwingPullback(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			AvwapEvents = EAvwapAnchor.SwingLow;      // lazy: creates the AVWAP manager; swing anchors force PRICEACTION on (Data1)
			AvwapLimits.maxAgeSessions = 60;          // daily bars: one session per bar
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.UserSpecified, "SW02LE", EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "SW02LX", EOrderAction.Sell));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "SW02LS", EOrderAction.Sell, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_stop = double.NaN; m_entryBar = -1;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close || Bars.CurrentBar <= Cfg.smaLong + 1) return;
			double c = Bars.Close[0];
			int pos = StrategyInfo.MarketPosition;
			if (pos > 0)
			{
				if (m_entryBar < 0) m_entryBar = Bars.CurrentBar;
				if (c > sma(Cfg.smaExit) || Bars.CurrentBar - m_entryBar >= Cfg.maxHoldBars) { m_lx.Send(); return; }
				m_lxStp.Send(m_stop);
				return;
			}
			m_entryBar = -1;
			if (rollOn(1)) return;
			if (!(c > sma(Cfg.smaLong)) || !(rsi(Cfg.rsiLength) < Cfg.rsiEntry)) return;
			double av = double.NaN;
			if (Cfg.avwapFilter)
			{
				_AvwapManager mgr = AvwapManager;
				_Avwap a = mgr != null ? mgr.Latest(EAvwapAnchor.SwingLow) : null;
				av = a != null && a.Valid ? a.Value : double.NaN;
				if (double.IsNaN(av) || c <= av) { if (journalOn()) logSignal("SW-02", +1, c, double.NaN, new Dictionary<string, string> { { "avwap", double.IsNaN(av) ? "na" : av.ToString("F2") } }, false); return; }
			}
			double tick = tickSize(), atr = averageTrueRange(Bars, Cfg.atrLength);
			if (!(atr > 0)) return;
			double r = Math.Max(Cfg.disasterAtr * atr, Cfg.costFloorTicks * tick);
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"SW-02 {Bars.Time[0]:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); return; }
			m_stop = c - r;
			m_le.Send(Cfg.contracts);
			if (journalOn())
			{
				var ctx = new Dictionary<string, string> { { "rsi2", rsi(Cfg.rsiLength).ToString("F1") }, { "atr20", atr.ToString("F2") }, { "avwap", double.IsNaN(av) ? "off" : av.ToString("F2") } };
				journalArm("SW02LE", c, m_stop, double.NaN, ctx);
				logSignal("SW-02", +1, c, m_stop, ctx, true);
			}
		}

		private double sma(int n) { double s = 0; for (int i = 0; i < n; i++) s += Bars.Close[i]; return s / n; }

		/// <summary>Cutler RSI (simple averages) over n closes; 50 when flat.</summary>
		private double rsi(int n)
		{
			double up = 0, dn = 0;
			for (int i = 0; i < n; i++) { double d = Bars.Close[i] - Bars.Close[i + 1]; if (d > 0) up += d; else dn -= d; }
			return up + dn == 0 ? 50.0 : 100.0 * up / (up + dn);
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
	}
}
