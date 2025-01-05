using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.IO;

namespace PowerLanguage.Strategy {
	public class _PMMS_DebugTrace : _CustomSignal {
		//constants
		public const string PARAMETER_DEBUG_TRACE = "DTDebugTrace"; //debug trace message string to log in the trace file
		public const string TRACE_SEPARATOR = "|";

		//inputs
		[Input] public string TraceFilename { get; set; }

		//properties


		//attributes
		private StreamWriter m_traceFile;

		//interface methods
		public _PMMS_DebugTrace(object _ctx):base(_ctx){}

		protected override void StartCalc() {
			if (TraceFilename == "")
			{
				TraceFilename = "C:\\temp\\debug_trace.txt";
				Output.WriteLine(string.Format("No trace filename specified, using default filename: {0}", TraceFilename));
			}

			//setup the shared trace file
			m_traceFile = File.CreateText(TraceFilename);
			m_traceFile.AutoFlush = true;
			for (int i = 0; i < PortfolioStrategies.Count(); i++)
				PortfolioStrategies[i].PortfolioData[PARAMETER_DEBUG_TRACE] = m_traceFile;
		}

		protected override void StopCalc()
		{
			m_traceFile.Close();
		}

		protected override void CalcBar()
		{
			////iterate all the signals and output the debug trace messages where required
			//string traceMessage;
			//for (int i = 0; i < PortfolioStrategies.Count(); i++)
			//{
			//	if (!PortfolioStrategies[i].PortfolioData.ContainsKey(PARAMETER_DEBUG_TRACE))
			//		continue;

			//	traceMessage = (string)PortfolioStrategies[i].PortfolioData[PARAMETER_DEBUG_TRACE];
			//	if (traceMessage != null && traceMessage != "")
			//	{
			//		string[] messages = traceMessage.Split(new string[] { TRACE_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);
			//		foreach (string message in messages) m_traceFile.WriteLine(message);
			//	}
			//}

			////clear the trace message for this signal
			//for (int i = 0; i < PortfolioStrategies.Count(); i++)
			//	PortfolioStrategies[i].PortfolioData[PARAMETER_DEBUG_TRACE] = "";
		}

		//methods


	}
}