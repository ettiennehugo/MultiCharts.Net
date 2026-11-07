//
// **Features (direct).** F-ROLL (§5.7), F-CAL (§5.8), F-TM (§5.12), F-JRNL (§5.13), M-DAILY (§5.19).
//
// **What it does** (from the file header):
// A2-64 intraday mean reversion to the RTH open, gated by A2-70 (HV20 off-switch with hysteresis).
// Entry: limit fades at R +/- m x ATR20d (R = 09:30 RTH open), 09:45-15:00 ET. Target R. Flat 15:55. Disaster stop 2m x ATR20d, floored.
// Gate (A2-70): HV20 (annualised close-to-close, daily) > thetaOff blocks entries; re-enabled when HV20 < 0.8 x thetaOff.
// thetaOff below is a PLACEHOLDER to be set by the A2-70 study; it is not a result.
// 
// Chart:
// Data1 = @ES 1m (24h Globex template), dxFeed continuous UNADJUSTED. Daily context aggregated from Data1 (no daily stream).
//
// Features: F-SESS (Session.rthOpen), M-DAILY (NeedsDaily, DailyConfig.hvLengths, Atr20d, Hv(n)), F-TM (entryAllowed), F-JRNL.
//
// **Pitfalls.**
// - Contains PLACEHOLDER values (marked in the code); calibrate them before reading any result.
//
// **Code.**
// Manual v4 example 17 - A2-64 intraday mean reversion to the RTH open, gated by A2-70 (HV20 off-switch with hysteresis).
// Entry: limit fades at R +/- m x ATR20d (R = 09:30 RTH open), 09:45-15:00 ET. Target R. Flat 15:55. Disaster stop 2m x ATR20d, floored.
// Gate (A2-70): HV20 (annualised close-to-close, daily) > thetaOff blocks entries; re-enabled when HV20 < 0.8 x thetaOff.
// thetaOff below is a PLACEHOLDER to be set by the A2-70 study; it is not a result.
// Chart: Data1 = @ES 1m (24h Globex template), dxFeed continuous UNADJUSTED. Daily context aggregated from Data1 (no daily stream).
// Features: F-SESS (Session.rthOpen), M-DAILY (NeedsDaily, DailyConfig.hvLengths, Atr20d, Hv(n)), F-TM (entryAllowed), F-JRNL.
//
using System;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	public class _Test10 : _VolumeProfileFuturesBase
	{
		protected sealed class A264Config
		{
			public int contracts = 1;
			public TimeSpan from = new TimeSpan(9, 45, 0), to = new TimeSpan(15, 0, 0), flat = new TimeSpan(15, 55, 0);
			public TimeSpan halfDayExitBefore = new TimeSpan(0, 5, 0);
			public double m = 0.5;                 // band multiple of ATR20d (A2-64 sweep parameter)
			public int atrLength = 20, hvLength = 20;
			public double thetaOff = 0.30;         // PLACEHOLDER (annualised HV); A2-70 sets it
			public double reEnable = 0.8;
			public int costFloorTicks = 14;
			public double riskBudgetFraction = 0.01;
		}
		protected A264Config Cfg { get; } = new A264Config();

		private IOrderPriced m_leLmt, m_seLmt, m_lxStp, m_sxStp, m_lxLmt, m_sxLmt;
		private IOrderMarket m_lx, m_sx;
		private DateTime m_lastDaily = DateTime.MinValue, m_day = DateTime.MinValue;
		private bool m_gateOff;
		private double m_atr = double.NaN, m_hv = double.NaN, m_stop = double.NaN;
		private bool m_traded;
		private double m_r = double.NaN;

		public _Test10(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Series;
			MaxTradesPerDay = 1;
			m_leLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "A264LE", EOrderAction.Buy));
			m_seLmt = OrderCreator.Limit(new SOrderParameters(Contracts.UserSpecified, "A264SE", EOrderAction.SellShort));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A264LX", EOrderAction.Sell));
			m_sx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "A264SX", EOrderAction.BuyToCover));
			m_lxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A264LS", EOrderAction.Sell, OrderExit.FromAll));
			m_sxStp = OrderCreator.Stop(new SOrderParameters(Contracts.Default, "A264SS", EOrderAction.BuyToCover, OrderExit.FromAll));
			m_lxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A264LT", EOrderAction.Sell, OrderExit.FromAll));
			m_sxLmt = OrderCreator.Limit(new SOrderParameters(Contracts.Default, "A264ST", EOrderAction.BuyToCover, OrderExit.FromAll));
		}

		protected override void StartCalc()
		{
			NeedsDaily = true;                      // M-DAILY, dataStream 0 = aggregate Data1 per Globex day (roll-adjusted)
			DailyConfig.atrLength = Cfg.atrLength; DailyConfig.hvLengths = new[] { Cfg.hvLength };
			base.StartCalc();
			m_lastDaily = m_day = DateTime.MinValue; m_gateOff = false; m_atr = m_hv = m_stop = double.NaN; m_traded = false;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			stepDaily();
			DateTime day = GlobexDate(Bars.Time[0]);
			if (day != m_day) { m_day = day; m_traded = false; }
			TimeSpan tod = ToEastern(Bars.Time[0]).TimeOfDay, flat = flatAt(day);
			SessionStats s = Session;
			int pos = StrategyInfo.MarketPosition;
			if (pos != 0)
			{
				m_traded = true;
				if (double.IsNaN(m_stop)) m_stop = StrategyInfo.AvgEntryPrice - Math.Sign(pos) * m_r;   // first bar in the trade
				if (tod >= flat && tod < new TimeSpan(17, 0, 0)) { if (pos > 0) m_lx.Send(); else m_sx.Send(); return; }
				if (pos > 0) { m_lxStp.Send(m_stop); m_lxLmt.Send(s.rthOpen); } else { m_sxStp.Send(m_stop); m_sxLmt.Send(s.rthOpen); }
				return;
			}
			if (rollOn(1)) m_traded = true;
			if (m_traded || s == null || s.rthOpen <= 0 || !(m_atr > 0) || m_gateOff) return;
			if (tod < Cfg.from || tod >= Cfg.to || tod >= flat) return;
			string why;
			if (!entryAllowed(out why)) return;
			double tick = tickSize(), band = Cfg.m * m_atr;
			double r = Math.Max(2 * Cfg.m * m_atr, Cfg.costFloorTicks * tick);   // disaster stop 2m x ATR, floored
			if (overRiskBudget(r)) { if (journalOn()) trace(ETraceLevel.Journal, $"A2-64 {day:yyyy-MM-dd} skip: stop {r / tick:F0} ticks breaches risk budget {Cfg.riskBudgetFraction:P2}"); m_traded = true; return; }
			double buyAt = Math.Round((s.rthOpen - band) / tick) * tick, sellAt = Math.Round((s.rthOpen + band) / tick) * tick;
			// both limits stand; whichever fills first sets the side (MaxTradesPerDay = 1 blocks a second entry). The protective stop
			// is placed from the fill price on the first closed bar in the trade: a fill-bar spike is unprotected (pitfall).
			m_leLmt.Send(buyAt, Cfg.contracts); m_seLmt.Send(sellAt, Cfg.contracts);
			m_r = r; m_stop = double.NaN;
		}

		/// <summary>New completed day in M-DAILY: ATR20d and HV20 from the (roll-adjusted) daily context, and the A2-70 hysteresis gate.</summary>
		private void stepDaily()
		{
			_DailyContext d = DailyContext;
			if (d == null || !d.Valid || d.PrevDate == m_lastDaily) return;
			m_lastDaily = d.PrevDate;
			m_atr = d.Atr20d; m_hv = d.Hv(Cfg.hvLength);
			if (double.IsNaN(m_hv)) return;
			bool was = m_gateOff;
			if (!m_gateOff && m_hv > Cfg.thetaOff) m_gateOff = true;
			else if (m_gateOff && m_hv < Cfg.reEnable * Cfg.thetaOff) m_gateOff = false;
			if (was != m_gateOff && journalOn()) trace(ETraceLevel.Journal, $"A2-70 {d.PrevDate:yyyy-MM-dd} HV20 {m_hv:P1} gate {(m_gateOff ? "OFF" : "ON")}");
		}

		private bool overRiskBudget(double stopDist) { return Cfg.contracts * stopDist * Bars.Info.BigPointValue > equityAvailable(Cfg.riskBudgetFraction); }

		private TimeSpan flatAt(DateTime day)
		{
			_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(day);
			TimeSpan f = Cfg.flat;
			if (cme.RthCloseEt.HasValue && cme.RthCloseEt.Value - Cfg.halfDayExitBefore < f) f = cme.RthCloseEt.Value - Cfg.halfDayExitBefore;
			return f;
		}
	}
}
