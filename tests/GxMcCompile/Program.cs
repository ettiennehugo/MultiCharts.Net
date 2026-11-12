using System;

// Compiles the GX MultiCharts half (no GX_OFFMC) against McStubs. A missing using or a wrong MC name fails this project.
static class Program
{
	static int Main()
	{
		Console.WriteLine("GxMcCompile: MC half compiled");
		return 0;
	}
}
