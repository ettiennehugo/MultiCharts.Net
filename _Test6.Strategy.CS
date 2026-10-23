using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	// ================================================================================================================
	// _Test6 — A2-63 host (H2) + the pre-registered G-R regime gate (specs/regime-consumer-prereg.md rev 2, C1)
	// ================================================================================================================
	//
	// HOST CORE (catalogue A2-63, Analyst 2's pre-registered core, unchanged): long only; buy stop at O + 0.5 x ATR20d,
	//   O = the 09:30 ET RTH open (open of the first bar closing after 09:30 ET); ATR20d = M-DAILY Atr20d (roll-gap removed),
	//   over COMPLETED PRIOR sessions only (PrevDate < today, else no entry), latched at 09:30, never updated intraday, journaled; the stop works 09:30-15:30 ET; one entry per day; market exit at 15:55 ET;
	//   no stop, no target, no filter; 1 contract. Exchange (ET) wall-clock times, so the windows hold through DST. CME early-close
	//   days (_CmeHolidays.Rules RthCloseEt): exit on the first bar closing at or after the early close minus 5m; CME no-RTH days:
	//   no entry. Known limitation: the stop works from the bar after the 09:30 latch bar (first-bar breakout missed).
	//   Bar size: 5m per ruling 2 (the header journals the bar size; any other size is journaled as a deviation).
	//   Rulings: specs/a2-63-rulings.md. Logic lives in _RegimeGate.Function.CS (A263Core, MC-free, unit-tested).
	//
	// GATE (RegimeGateConfig.arm, set IN CODE, no [Input]; prereg §8 step 1 - one build per arm, the config hash differs):
	//   U (default) : ungated host; RegimeConfig.enabled = true for the journal only. Trades = the host core.
	//   G           : skip the day's entry when the prior session's post-hysteresis HMM Label == 2 (filtered posteriors,
	//                 usable iff Valid && !UsingFallback && AsOf < t).
	//   A           : RegimeConfig.fitSessions = minSessions = 100000 so the engine publishes the RG-03 ATR tercile every day;
	//                 skip tercile 2 (usable iff Valid && AsOf < t).
	//   To build G or A: in the constructor below set RegimeGateConfig.arm = ERegimeGateArm.G (or .A), compile, run.
	//   Every other RegimeConfig field stays at its default (prereg §2.1); changing one is a new trial.
	//
	// JOURNAL (TraceLevel = Journal): one REGGATE line per RTH session in EVERY arm, also on days with no entry signal:
	//   REGGATE {date} arm() asOf() label() raw() pFilt() fb() usable() allow() atrT() wouldBlock() why() signal() cfg() seed()
	//   + host fields O() atr20d() atrAsOf() atrOk() thr() exitAt() halfDay() noRth() and the arm's running counts nSess() nUsable()
	//   nWarm() nInv() nFb() nBlock() nNoRth() (no-usable-regime days split warm-up / invalid label / ATR fallback, why(); CME no-RTH days; the last line holds the totals);
	//   a REGGATE header line (canonical config + FNV-1a hash) on the first session, the engine's REGIME lines, and the
	//   per-trade MCJournal context column with the session's REGGATE fields (arm, label, pFilt, cfg, seed, ...).
	//
	// SETUP: @ES 5m (dxFeed, not back-adjusted), CME Globex session, 1m Bar Magnifier, MC costs per prereg §5.3, data from the
	//   first available bar. Dev runs end 2024-12-31 (prereg §8 step 3). Exclusions (roll ±1, half days, not-usable days) are
	//   applied OFFLINE to all arms equally; the MC run trades them so G/A = U's trades minus the blocked days (§4.1 equivalence).
	//
	// IMPORT ORDER: _RegimeEngine.Function.CS (and its _Gmm/_Rng), _RegimeGate.Function.CS, _CustomSignal chain, this file.
	// ================================================================================================================
	public class _Test6 : _CustomSignal
	{
		/// <summary>Gate arm and placebo seed (no [Input]). Default U.</summary>
		protected RegimeGateConfig RegimeGateConfig { get; } = new RegimeGateConfig();
		/// <summary>A2-63 core (pre-registered; do not change without a new pre-registration).</summary>
		protected A263Config HostConfig { get; } = new A263Config();

		private const string EntryName = "A263LE", ExitName = "A263LX";
		private const string Et = "Eastern Standard Time";
		private IOrderPriced m_le;
		private IOrderMarket m_lx;
		private A263Core m_core;
		private AtrTercileTracker m_terc;
		private string m_cfgHash;
		private bool m_headerLogged;
		private RegimeGateCounts m_counts;

		public _Test6(object _ctx) : base(_ctx)
		{
			// RegimeGateConfig.arm = ERegimeGateArm.G;   // build G
			// RegimeGateConfig.arm = ERegimeGateArm.A;   // build A
		}

		protected override void Create()
		{
			base.Create();
			m_le = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, EntryName, EOrderAction.Buy));
			m_lx = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, ExitName, EOrderAction.Sell));
		}

		protected override void StartCalc()
		{
			string err = RegimeGateConfig.Validate();
			if (err != null) throw new ArgumentException(err);
			RegimeGate.ApplyArm(RegimeGateConfig, RegimeConfig);   // before base.StartCalc: the engines are created there
			NeedsDaily = true;
			DailyConfig.atrLength = HostConfig.atrLength;
			base.StartCalc();
			m_core = new A263Core(HostConfig);
			m_terc = new AtrTercileTracker(RegimeConfig.atrPctLookback, RegimeConfig.K);
			m_cfgHash = RegimeGate.Hash(RegimeConfig, RegimeGateConfig, HostConfig);
			m_headerLogged = false;
			m_counts = new RegimeGateCounts();
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			DateTime et = ToZone(Bars.Time[0], Et);
			_DailyContext daily = DailyContext;
			if (daily != null) m_terc.AddDay(daily.PrevDate, daily.Atr20d);   // same day / value the engine got in stepRegime
			bool isLong = StrategyInfo.MarketPosition > 0;

			if (m_core.IsLatchBar(et))
			{
				// ruling 1: Wilder M-DAILY Atr20d over completed prior sessions only (PrevDate < today, else no entry that day)
				double atr = daily != null ? daily.Atr20d : double.NaN;
				DateTime atrAsOf = daily != null ? daily.PrevDate : DateTime.MinValue;
				// ruling 2: CME early close -> exit on the first bar closing at or after the early close minus 5m; no-RTH days: no entry
				_CmeHolidays.Rules.Day cme = _CmeHolidays.Rules.Get(et.Date);
				RegimeGateDecision d = RegimeGate.Decide(RegimeGateConfig.arm, Regime, et.Date, m_terc.LastDate == atrAsOf ? m_terc.Tercile : -1);
				m_counts.Add(d, cme.NoRth);
				m_core.Latch(et, Bars.Open[0], atr, atrAsOf, d.allow, cme.RthCloseEt, cme.NoRth);
				if (journalOn())
				{
					if (!m_headerLogged)
					{
						m_headerLogged = true;
						Resolution res = Bars.Info.Resolution;
						bool fiveMin = res.Type == EResolution.Minute && res.Size == 5;
						trace(ETraceLevel.Journal, $"REGGATE header version({RegimeGate.Version}) arm({RegimeGateConfig.arm}) cfg({m_cfgHash}) seed({RegimeGateConfig.placeboSeed}) " +
							$"symbol({Bars.Info.Name}) bar({res.Type} {res.Size}) barDeviation({(fiveMin ? 0 : 1)}) firstBar({Bars.Time[0]:yyyy-MM-dd HH:mm}) canon[{RegimeGate.Canon(RegimeConfig, RegimeGateConfig, HostConfig)}]");
						if (!fiveMin) trace(ETraceLevel.Journal, $"REGGATE deviation bar size {res.Type} {res.Size} used instead of 5 Minute (ruling 2)");
					}
					trace(ETraceLevel.Journal, RegimeGate.Line(d, m_core.HasSignal, m_cfgHash, RegimeGateConfig.placeboSeed) + m_core.Suffix() + m_counts.Suffix());
					if (d.allow && m_core.HasSignal) journalArm(EntryName, m_core.Threshold, double.NaN, double.NaN, RegimeGate.TradeFields(d, m_cfgHash, RegimeGateConfig.placeboSeed));
				}
			}

			A263Action a = m_core.Step(et, isLong);
			if (a.exit) m_lx.Send();
			else if (a.sendStop) m_le.Send(a.stop, HostConfig.contracts);
		}
	}
}
