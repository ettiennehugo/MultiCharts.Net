//
// **Features (direct).** F-SESS (§5.6), F-ROLL (§5.7), F-CAL (§5.8), F-VWAP (§5.10), F-TM (§5.12), F-JRNL (§5.13), F-EVT (§5.14), M-DAILY (§5.19).
//
// **What it does** (from the file header):
// DV-15 EMA20 pullback with a session-VWAP slope filter.
// Side = sign(close - EMA20) held for >= 3 closed 30m bars. On a touch of the EMA (bar range contains it) while the session-VWAP slope
// over 8 bars has the same sign with |slope| >= s x ATR(14, 30m) and price is on the same side of VWAP: a limit at the EMA for the next bar.
// Pitfall: stop/target are sent from the bar after the fill, so the fill bar itself is unprotected.
// Bracket 1:1 at f x ATR20d (DailyContext), floored at 14 ticks. RTH 10:00-15:00 entries, flat 15:55. s = 0.05, f = 0.15 (grid values from
// the catalogue, not tuned results).
//
// Chart: 
//   Data1 = @ES 30m (24h Globex template), dxFeed continuous UNADJUSTED.
// 
// Features:
// F-VWAP (Vwap1Type/Vwap1DataStream set in code, CurrentVwap1, Vwap1Active), M-DAILY (NeedsDaily, Atr20d), F-TM (FlatBy).
//
// **Pitfalls.**
// - The fill bar is unprotected: the stop is first sent on the bar after the fill.
//
// **Code.**
// Manual v4 example 20 - DV-15 EMA20 pullback with a session-VWAP slope filter.
// Side = sign(close - EMA20) held for >= 3 closed 30m bars. On a touch of the EMA (bar range contains it) while the session-VWAP slope
// over 8 bars has the same sign with |slope| >= s x ATR(14, 30m) and price is on the same side of VWAP: a limit at the EMA for the next bar.
// Pitfall: stop/target are sent from the bar after the fill, so the fill bar itself is unprotected.
// Bracket 1:1 at f x ATR20d (DailyContext), floored at 14 ticks. RTH 10:00-15:00 entries, flat 15:55. s = 0.05, f = 0.15 (grid values from
// the catalogue, not tuned results).
// Chart: Data1 = @ES 30m (24h Globex template), dxFeed continuous UNADJUSTED.
// Features: F-VWAP (Vwap1Type/Vwap1DataStream set in code, CurrentVwap1, Vwap1Active), M-DAILY (NeedsDaily, Atr20d), F-TM (FlatBy).
//
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _Ex20EmaVwapPullback : _VolumeProfileFuturesBase
	{
		protected sealed class Dv15Config
		{
			public int contracts = 1, emaLength = 20, sideBars = 3, slopeBars = 8, atrLength = 14;
			public double slopeAtr = 0.05, bracketAtr20d = 0.15;
			public TimeSpan firstEntry = new TimeSpan(10, 0, 0), lastEntry = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected Dv15Config Cfg { get; } = new Dv15Config();

		private IOrderPriced m_leLmt, m_seLmt, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private IOrderMarket m_lx, m_sx;
		private double m_ema = double.NaN, m_stop, m_target;
		private int m_sideRun, m_side, m_emaBars;
		private readonly List<double> m_vwap = new List<double>();   // closed-bar VWAP values, newest last (session-reset aware)
		private DateTime m_vwapDay = DateTime.MinValue;

		public _Ex20EmaVwapPullback(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			Vwap1Type = EVwapType.Session; Vwap1DataStream = 1;     // [Input]s on the base: set in code
			FlatBy = Cfg.flat;
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "DV15LE", EOrderAction.Buy));
			m_seLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "DV15SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "DV15LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "DV15SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "DV15LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "DV15SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "DV15LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "DV15ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			NeedsDaily = true;                                      // before base.StartCalc (M-DAILY is built there)
			base.StartCalc();
			m_ema = double.NaN; m_emaBars = 0; m_sideRun = 0; m_side = 0; m_vwap.Clear(); m_vwapDay = DateTime.MinValue;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			double c = Bars.Close[0], tick = tickSize();
			double k = 2.0 / (Cfg.emaLength + 1);
			m_ema = double.IsNaN(m_ema) ? c : m_ema + k * (c - m_ema); m_emaBars++;
			int prevSide = m_side, prevRun = m_sideRun;              // side state up to the previous bar
			int s = Math.Sign(c - m_ema);
			m_sideRun = s != 0 && s == m_side ? m_sideRun + 1 : 1; m_side = s;
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_vwapDay) { m_vwapDay = day; m_vwap.Clear(); }
			if (Vwap1Active) m_vwap.Add(CurrentVwap1);

			int pos = StrategyInfo.MarketPosition;
			if (pos != 0)
			{
				if (mustFlatten()) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(m_target); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(m_target); }
				return;
			}
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay;
			if (!IsRth() || tod < Cfg.firstEntry || tod > Cfg.lastEntry || m_emaBars < Cfg.emaLength || rollOn(1)) return;
			if (m_vwap.Count <= Cfg.slopeBars || DailyContext == null || !DailyContext.Valid) return;
			int side = prevRun >= Cfg.sideBars ? prevSide : 0;                                      // side established BEFORE this bar
			if (side == 0 || !(Bars.Low[0] <= m_ema && Bars.High[0] >= m_ema)) return;                // touch
			double vw = m_vwap[m_vwap.Count - 1], slope = vw - m_vwap[m_vwap.Count - 1 - Cfg.slopeBars];
			double atr = averageTrueRange(Bars, Cfg.atrLength);
			if (!(atr > 0) || Math.Sign(slope) != side || Math.Abs(slope) < Cfg.slopeAtr * atr || Math.Sign(c - vw) != side) return;
			double d = Math.Max(Cfg.bracketAtr20d * DailyContext.Atr20d, Cfg.costFloorTicks * tick);
			if (!(d > 0) || overRiskBudget(d)) { if (journalOn()) trace(ETraceLevel.Journal, $"DV-15 {Bars.Time[0]:yyyy-MM-dd HH:mm} skip: bracket {d / tick:F0} ticks breaches risk budget"); return; }
			double entry = Math.Round(m_ema / tick) * tick;
			m_stop = entry - side * d; m_target = entry + side * d;      // 1:1 bracket
			if (side > 0) m_leLmt.Send(entry, Cfg.contracts); else m_seLmt.Send(entry, Cfg.contracts);
			if (journalOn()) logSignal("DV-15", side, entry, m_stop, new Dictionary<string, string> { { "vwap", vw.ToString("F2") }, { "slope", slope.ToString("F2") } }, true);
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }
	}
}
