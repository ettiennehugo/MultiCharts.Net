using System;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	// =================================================================================================================
	// _Test1: INERT test slot (specs/sandbox-test-protocol.md). No orders, no [Input]s besides the inherited TraceLevel.
	// =================================================================================================================
	//
	// Slot status: free. The slot register (specs/sandbox-test-protocol.md §3.4) records which sandbox signal, if any, is loaded here.
	// To load a signal: replace everything below the using lines with the sandbox file's body, rename its public class to _Test1
	// (constructor included), keep the sandbox file as the source of truth, press F7 in the PowerLanguage Editor (never Ctrl+F7).
	// To free the slot: copy the committed inert version of this file back over it (never git checkout in the MiniPC clone) and press F7.
	// Never commit a loaded slot. Never commit Editor-generated PLStudies.csproj changes.
	public class _Test1 : _CustomSignal
	{
		public _Test1(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
		}

		protected override void StartCalc()
		{
			base.StartCalc();
		}

		protected override void CalcBar()
		{
			base.CalcBar();
		}
	}
}
