using PowerLanguage.Function;
using System;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// THROW-AWAY for Check 7 L9 / Check 8. Derives from _CustomSignal so the existing roll table runs.
	/// Backtest: one C7 ROLL FIRE on the profile roll date inside the window (ES Dec 2024 is Mon 2024-12-16).
	/// Paper: the same log on the dxFeed switch day, then the IBKR remap in specs/check7-protocol.md.
	/// Scratch. Delete after the checks. Not for merge.
	/// </summary>
	public class _TestCheck7Roll : _CustomSignal
	{
		private IOrderMarket m_le;
		private bool m_sent;

		public _TestCheck7Roll(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Table;
			m_le = OrderCreator.MarketNextBar(new SOrderParameters(Contracts.Default, "C7LE", EOrderAction.Buy));
		}

		protected override void StartCalc()
		{
			base.StartCalc();
			fillProfileRollDates(2024, 2026, "C7");
			Output.WriteLine("C7 ROLL START symbol(" + Bars.Info.Name + ") dates(" + RollDates.Count + ") digest(" + _RollDetector.DatesDigest(RollDates) + ") mode(Table)");
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (Bars.Status != EBarState.Close) return;
			if (rollOn(1))
				Output.WriteLine("C7 ROLL FIRE bar(" + Bars.CurrentBar + ") time(" + Bars.Time[0].ToString("yyyy-MM-dd HH:mm") + ") symbol(" + Bars.Info.Name + ") pos(" + StrategyInfo.MarketPosition + ")");
			if (StrategyInfo.MarketPosition == 0 && !m_sent)
			{
				m_le.Send();
				m_sent = true;
				Output.WriteLine("C7 ROLL ARM buy next bar market time(" + Bars.Time[0].ToString("yyyy-MM-dd HH:mm") + ") symbol(" + Bars.Info.Name + ")");
			}
		}
	}
}
