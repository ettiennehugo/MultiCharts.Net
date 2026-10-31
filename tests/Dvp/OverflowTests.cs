using System;
using PowerLanguage.Function;

static partial class Program
{
	// Regression (MC run 2026-10-31): MultiCharts compiles studies with overflow checking ON; Fnv1a64's h *= prime threw
	// OverflowException from the _DvpEngine ctor. The test projects now set CheckForOverflowUnderflow=true to match.
	static void OverflowTests()
	{
		Check(_DvpEngine.Fnv1a64("") == 0xcbf29ce484222325UL, "Fnv1a64 empty vector");
		Check(_DvpEngine.Fnv1a64("a") == 0xaf63dc4c8601ec8cUL, "Fnv1a64 'a' vector");
		Check(_DvpEngine.Fnv1a64("foobar") == 0x85944171f73967e8UL, "Fnv1a64 'foobar' vector");
		ulong h = 0; bool threw = false;
		try { h = _DvpEngine.Fnv1a64(new string('x', 10000)); } catch (OverflowException) { threw = true; }
		Check(!threw, "Fnv1a64 long string: no OverflowException under checked arithmetic");
		Check(h == 0x4118176bad223b65UL, "Fnv1a64 long string: reference value (reference: Python mod 2^64)");
		bool chk = false; try { ulong x = ulong.MaxValue; x += (ulong)Environment.TickCount | 1UL; } catch (OverflowException) { chk = true; }
		Check(chk, "test project compiles with CheckForOverflowUnderflow=true (matches MultiCharts)");
	}
}
