using System;

namespace PowerLanguage.Function
{
	/// <summary>
	/// Spec CS-0 (analyst-base-class-spec.md): compile probe for a plain (non-study) helper class in its own
	/// *.Function.CS file. It does not derive from FunctionSimple/FunctionSeries and has no MultiCharts lifecycle.
	/// If the PowerLanguage .NET Editor compiles this file (and _CustomSignal's nameof reference to it), the helper
	/// types of CS-1, CS-2, VPB-1, VPB-9 and VPF-7 can live in their own files; if not, they stay nested classes.
	/// Nothing calls this class at run time. CS-1 (per-trade journal) is expected to replace it with the real
	/// journal component.
	/// </summary>
	internal sealed class _SignalJournal
	{
		//constants
		public const string PROBE_ID = "CS-0";

		//interface methods
		public static string Describe()
		{
			return "_SignalJournal helper-file probe (" + PROBE_ID + ")";
		}
	}
}
