using System.IO;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// THROW-AWAY TEST MONEY-MANAGEMENT SIGNAL for live Check 10: _PortfolioRiskManager unchanged, plus a JSON ConfigPath =
	/// %USERPROFILE%\Documents\_TestPortThrottle\mm.json (missing file = code defaults; bad values = PORT Warning + defaults).
	/// Runs and suggested overrides: see the _TestPortThrottle header. Delete after the sitting.
	/// </summary>
	public class _TestPortThrottleMM : _PortfolioRiskManager
	{
		public _TestPortThrottleMM(object _ctx) : base(_ctx) { }

		protected override string ConfigPath
		{
			get { string f = _TestPortThrottle.SettingsFile("mm.json"); return File.Exists(f) ? f : null; }
		}
	}
}
