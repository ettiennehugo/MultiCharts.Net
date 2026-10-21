using PowerLanguage.Function;
using System;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Probe P2 (missing-functionality spec rev 4 §3, VPB-8 acceptance, extended). Places no orders. Insert on a chart with
	/// Data1 = ES 1-min and Data2 = ES 15-min or 60-min (same symbol, 24h Globex template), TraceLevel = Info. Run with IOG on and
	/// off, backtest with and without Bar Magnifier, playback and live. One line per CalcBar call:
	///   which streams are closed (Status per stream), Data1/Data2 Time[0], Status, Close[0], CurrentBar, the HtfClosed index and
	///   the mapped HTF bar, plus MaxBarsBack.
	/// Answers: (a) forming Data2 values/Status intrabar; (b) whether bar times are END stamps (the 10:00 60-min bar visible on the
	/// 10:00 1-min bar); (c) ordering of Data1/Data2 closes that share a timestamp; (d) PG p54 "last calc before Data1 Open is
	/// valid"; (e) per-series MaxBarsBack (read Data2 Close[MaxBarsBack] at the first calc).
	/// </summary>
	public class _MtfProbe : _CustomSignal
	{
		private int m_calls;

		public _MtfProbe(object _ctx) : base(_ctx) { }

		protected override void StartCalc()
		{
			HtfStreams = new[] { 2 };
			base.StartCalc();
			m_calls = 0;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (!traceOn(ETraceLevel.Info)) return;
			IInstrument d1 = Bars, d2 = BarsOfData(2);
			if (m_calls++ == 0)
			{
				string deep;
				try { deep = d2.Close[ExecInfo.MaxBarsBack].ToString(); } catch (Exception e) { deep = "<" + e.GetType().Name + ">"; }
				trace(ETraceLevel.Info, $"P2 start iog({Environment.IOGEnabled}) maxBarsBack({ExecInfo.MaxBarsBack}) d1({d1.Info.Name} {d1.Info.Resolution.Type} {d1.Info.Resolution.Size}) " +
					$"d2({d2.Info.Name} {d2.Info.Resolution.Type} {d2.Info.Resolution.Size}) d2.Close[MaxBarsBack]={deep}");
			}
			int ci = HtfClosedIndex(2), mi = htfMapped(2);
			trace(ETraceLevel.Info, $"P2 call({m_calls}) d1[{d1.Time[0]:yyyy-MM-dd HH:mm:ss} {d1.Status} c={d1.Close[0]} cb={d1.CurrentBar}] " +
				$"d2[{d2.Time[0]:yyyy-MM-dd HH:mm:ss} {d2.Status} c={d2.Close[0]} cb={d2.CurrentBar}] closedIdx({ci}) closed[{d2.Time[ci]:HH:mm:ss} c={d2.Close[ci]}] " +
				$"mapped({mi}{(mi >= 0 ? " " + d2.Time[mi].ToString("HH:mm:ss") : "")}) realTime({Environment.IsRealTimeCalc})");
		}
	}
}
