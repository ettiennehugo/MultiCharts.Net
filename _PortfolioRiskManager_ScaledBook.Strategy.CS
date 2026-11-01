using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// M-PORT money-management signal, ScaledBook profile (spec §5.1/§5.1a): 4% heat, 2% per occupied cluster, 6 positions,
	/// drawdown throttles 5/10/15%, book vol target 10%. Everything else as _PortfolioRiskManager (Starter).
	/// Manual switch only: select this signal instead of _PortfolioRiskManager in Portfolio Trader once the daily
	/// "PORT advisory" line reads eligible (uncorr ≥ 3 and uncorr03 ≥ 3), restart the portfolio and journal the switch.
	/// Import after _PortMM.Function.CS and _PortfolioRiskManager.Strategy.CS.
	/// </summary>
	public class _PortfolioRiskManager_ScaledBook : _PortfolioRiskManager
	{
		public _PortfolioRiskManager_ScaledBook(object _ctx) : base(_ctx) { }

		protected override EPortProfile Profile { get { return EPortProfile.ScaledBook; } }
	}
}
