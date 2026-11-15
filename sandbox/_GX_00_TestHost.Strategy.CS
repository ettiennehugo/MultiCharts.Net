// ## 1. Identity
// Catalogue ID: GX-00
// ID mapping: GX-00 → sandbox/_GX_00_TestHost.Strategy.CS (class _GX_00_TestHost). TestHost is the A-check entry, not a catalogue idea.
// Name: GX test host
// Header version: 1
// Rules version: specs/generic-exit-manager-spec.md rev 1.1 §8.1 and §8.4
// Study class: _GX_00_TestHost
// File: sandbox/_GX_00_TestHost.Strategy.CS
// Stage: sandbox
// Owner approval: Analyst decision (2026-11-12), owner may override
//
// ## 2. Provenance
// Source: specs/generic-exit-manager-spec.md rev 1.1 §8.4
// Taken from the source: fixed-cadence entries so A-E2, A-E4, A-E7 and A-S are repeatable
// Research assumption: one contract, market next bar, no research claim
//
// ## 3. Hypothesis
// Mechanism: Not applicable. This file is a test signal, not a strategy, and is never used for a research result.
// Link: specs/generic-exit-manager-spec.md §8.4
//
// ## 4. Trade walkthrough
// Trigger: every EntryEveryBars closed bars, while flat, buy or sell short one contract
// Management: EmbedEngine true calls exitPlan and exitBar. EmbedEngine false only arms and publishes pm.gxPlanEntry / pm.gxPlanStop
// Exit: the embedded engine, or _GenericExit when EmbedEngine is false
//
// ## 5. Timing
// Signal evaluated: closed bar
// Order sent: market next bar
// Can fill: the next bar
//
// ## 6. Position and re-entry
// Position: flat or one. No adds and no reversal.
// Re-entry: the next cadence after the position is flat
// Same session after a stop: yes, on the cadence
//
// ## 7. Instruments
// Typical: @ES and @NQ, 1 minute, data through 2024-12-31
// Validated: Not yet verified
//
// ## 8. Data
// Data1: the chart the analyst loads. No second series.
// Data2: Not applicable
// Further series: Not applicable
// Warmup: EntryEveryBars closed bars
//
// ## 9. Entry and filters
// All of these must be true: flat, and the cadence count has reached EntryEveryBars
// Skip when: a position is open
//
// ## 10. Position sizing and risk
// Size: 1 contract
// Risk limit: the planned stop passed to exitPlan or gxPublishPlan
// Size is never shrunk to fit a stop: yes
//
// ## 11. Orders, execution, and costs
// Entry order: THLE buy, THSE sell short, market next bar
// Exit orders: none from this file when EmbedEngine is false. When true, the GX exit orders from createGxOrders with prefix TH
// Fill assumption: next bar
// Costs: the chart's own commission and slippage. This file does not change them.
//
// ## 12. Trade management
// While open: exitBar on every closed bar when EmbedEngine is true, including the flat bar. Otherwise the wrapper.
//
// ## 13. Exits
// Hard stop: the widened GX stop when embedded
// Target: the planned T1 when embedded
// Time exit: the GX clock when embedded
// Invalidation: Not applicable
//
// ## 14. Regimes
// Suitable: Not applicable
// Unsuitable: Not applicable
//
// ## 15. Dependencies
// Base class: _CustomSignal
// Engines enabled: the GX helpers only when EmbedEngine is true
// Engine settings enabled: GxConfig stays at the embedded defaults
//
// ## 16. Pitfalls
// Lookahead: entries use the closed bar only
// Repaint: Not applicable
// Survivorship: Not applicable
// Rolls: Not applicable
// Data quality: Not yet verified
// Other: this file is not in PLStudies.csproj. A native run adds a temporary Compile Include and removes it before commit.
//
// ## 17. Parameters
// Each exposed strategy parameter is its own [Input] property. No input class or protected config class for those parameters.
// No description argument on [Input] was verified in this repo. This section is the description. Do not invent an attribute.
// TraceLevel stays the base [Input] on _CustomSignal.
// Input: EmbedEngine
// Purpose: true hosts the engine; false only publishes the plan for _GenericExit
// Default: true
// Units: bool
// Range: true or false
// Effect: false sends no GX exit orders
// Interactions: the wrapper must be on the same strategy when this is false
// Fixed or research-tuned: fixed
// Input: EntryEveryBars
// Purpose: closed bars between entries while flat
// Default: 30
// Units: bars
// Range: integer >= 1
// Effect: a larger count waits longer
// Interactions: none
// Fixed or research-tuned: fixed
// Input: EntrySide
// Purpose: which side the next entry uses
// Default: Alternate
// Units: ETestGxSide
// Range: Long, Short, Alternate
// Effect: Alternate flips after each entry
// Interactions: none
// Fixed or research-tuned: fixed
//
// ## 18. Acceptance policy
// Policy: catalogue-default
// Predeclared before any result was read: Not applicable. This file produces no research result.
// catalogue-default: Not applicable
// big-winner-trend: Not applied
// Expectancy = win_rate * avg_win - loss_rate * avg_loss, on cost-adjusted trades. avg_win and avg_loss are both positive magnitudes.
//
// ## 19. Validation
// Status: Not yet verified
// Evidence: the A-checks in specs/generic-exit-manager-spec.md §8.4
// Window used: data through 2024-12-31 only
// Holdout (W-M and W-D Jan 2025–Oct 2026; W-T last 125 sessions) stays unviewed. Do not put those results here.
//
// ## 20. Edge monitoring
// A person reads this section. It does not place orders and it does not change parameters.
// Expectancy: Not applicable
//   Formula: win_rate * avg_win - loss_rate * avg_loss, same cost model as section 11.
// Observations: Not applicable
// Drift: Not applicable
// Warning: Not applicable
// Pause: Not applicable
// Review: Not applicable
// Uncertainty: this is a test host
// No automatic retune. No autonomous live trading. A later parameter change is a new set, written offline.
//
// ## 21. Log diagnostics
// journalArm name: THLE or THSE
// logSignal tag: Not applicable
// Skip / flatten lines: EXIT Info: R fill TH
// How to turn logging on: engines manual §1.5 (TraceLevel Journal). Do not paste that section.
//
// ## 22. Notes
// Not a research strategy. Delete nothing from the studies folder to run it. Add the temporary csproj line, run the check, then remove the line.

using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	[IOGMode(IOGMode.Disabled)]
	public class _GX_00_TestHost : _CustomSignal
	{
		public enum ETestGxSide { Long, Short, Alternate }

		// Purpose: true hosts the engine. Default: true. Units: bool. Range: true or false. Effect: false leaves exits to _GenericExit. Interactions: the wrapper. Fixed or research-tuned: fixed
		[Input] public bool EmbedEngine { get; set; }
		// Purpose: bars between entries. Default: 30. Units: bars. Range: >= 1. Effect: wait. Interactions: none. Fixed or research-tuned: fixed
		[Input] public int EntryEveryBars { get; set; }
		// Purpose: entry side. Default: Alternate. Units: ETestGxSide. Range: Long, Short, Alternate. Effect: Alternate flips. Interactions: none. Fixed or research-tuned: fixed
		[Input] public ETestGxSide EntrySide { get; set; }

		private IOrderMarket m_le, m_se;
		private int m_wait;
		private bool m_nextLong = true;

		public _GX_00_TestHost(object _ctx) : base(_ctx)
		{
			EmbedEngine = true;
			EntryEveryBars = 30;
			EntrySide = ETestGxSide.Alternate;
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			m_wait = 0;
			m_nextLong = true;
		}

		protected override void Create()
		{
			base.Create();
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "THLE", EOrderAction.Buy));
			m_se = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "THSE", EOrderAction.SellShort));
			if (EmbedEngine) { GxOrderPrefix = "TH"; createGxOrders(); }
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			int pos = StrategyInfo.MarketPosition;
			if (EmbedEngine) exitBar();
			if (pos != 0) return;
			m_wait++;
			int every = EntryEveryBars < 1 ? 1 : EntryEveryBars;
			if (m_wait < every) return;
			m_wait = 0;
			bool longSide = EntrySide == ETestGxSide.Long || (EntrySide == ETestGxSide.Alternate && m_nextLong);
			if (EntrySide == ETestGxSide.Alternate) m_nextLong = !m_nextLong;
			double entry = Bars.Close[0];
			double tick = tickSize();
			double structural = longSide ? Bars.Low[0] - tick : Bars.High[0] + tick;
			var mkt = new ExitMarket();
			mkt.Tick = tick;
			try { mkt.Bpv = Bars.Info.BigPointValue; } catch (Exception) { mkt.Bpv = 1; }
			mkt.AtrStop = tick * 8; mkt.AtrTrail = tick * 8;
			mkt.EntryStop = structural;
			mkt.SignalLow = Bars.Low[0]; mkt.SignalHigh = Bars.High[0]; mkt.SwingLow = Bars.Low[0]; mkt.SwingHigh = Bars.High[0];
			mkt.BarMinutes = 1; mkt.BarKind = EGxBarKind.Minute; mkt.ResolutionLabel = "1 min";
			try { InstrumentProfile ip = InstrumentProfileOf(1); if (ip != null && ip.costFloorTicks > 0) mkt.FloorTicks = ip.costFloorTicks; } catch (Exception) { }
			var cfg = new ExitConfig { InitialStop = EInitialStop.AtrMultiple, StopAtrMult = 1.5, StopAtrLength = 14, Id = "TH" };
			double planned = _ExitEngine.InitialStop(EInitialStop.AtrMultiple, longSide, entry, mkt, cfg, true);
			if (EmbedEngine)
			{
				gxConfigure(cfg);
				gxSetMarket(mkt);
				exitPlan(longSide, entry, structural, 1, mkt, longSide ? "THLE" : "THSE");
			}
			else if (!gxArmEntry(longSide, entry, structural, planned)) return;
			if (longSide) m_le.Send(); else m_se.Send();
		}
	}
}
