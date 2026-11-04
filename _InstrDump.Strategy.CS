// ============================================================================================
// M-INSTR PR-M0: VERIFY dump (specs/m-instr-spec.md §4.4, §4.4.1). Places no orders, changes nothing; typed MC members only
// (no reflection, no Enum.Parse, no ToString compares: MC enums are formatted into the human log only, R1).
//
// OUTPUT. One file per symbol per run, MCInstrDump_<symbol>_<yyyyMMdd-HHmmss>-<pid>.txt, in the directory of the standard
//   MCSignal*.csv trace log (_CustomSignal.LogDirectory(): MCSignalLogs under LocalApplicationData, or %TEMP% when that failed).
//   The trace log gets one "INSTRDUMP file(<path>)" line (written whatever TraceLevel is) and Output shows the path.
//   One File.AppendAllText per line, UTF-8 without BOM, culture-invariant numbers (doubles "R").
//
// RUNS (dxFeed futures; one chart per run, any bar resolution, e.g. 1 minute, a few days of data is enough).
//   1. @ES, Default sessions (the live chart).        2. @ES, "CME Globex" session template (D9a/canary chart).
//   3. @MES.   4. @CL.   5. @GC.   6. @6E.   (Default sessions, Format Instrument > Time Zone = Exchange)
//   7. A3: @ES and @CL again with Format Instrument > Time Zone = Local (compare Time[0] and the session times with runs 1/4).
//   8. A4: @ES 1 minute with Data2 = @ES 60 minute on a different session template (Data2 sessions are printed per stream).
//   9. A4: Portfolio Trader with @ES and @CL (one file per symbol).
//   Optional when available: @ZN. The spec's FX pair, crypto and stock runs are skipped (futures-only dxFeed data).
//
// SECTIONS.
//   A1  instrument fields that compile today: name, cat, catInt, exch, tz, minMove, priceScale, bpv, bvo, res.Type, sessionName.
//   A2  Bars.Sessions in StartCalc and again on the first CalcBar (populated at StartCalc? identical? sessions per day kept?),
//       the first bar's Time[0] and LastBarInSession, and the first SESSION_END_BARS bars with LastBarInSession = true.
//   A3  zone: tz is on the A1 line; run 7 gives the Local counterpart of the same bars and sessions.
//   A4  Data2..N sessions (BarsOfData(n).Sessions) and the application (Portfolio Trader: one file per symbol).
//   B   one candidate per build (see the B block in dumpFirstBar): remove the // of ONE line, compile, run once, put the // back.
//       A compile error = the member does not exist on this MC build: record the error text, do not guess another name.
//
// Import order: _CustomSignal.Strategy.CS (adds LogDirectory()), then _InstrDump.Strategy.CS, then Compile.
// ============================================================================================
using PowerLanguage.Function;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PowerLanguage.Strategy
{
	/// <summary>M-INSTR PR-M0 VERIFY dump (spec §4.4): instrument fields and session data per stream into
	/// MCInstrDump_*.txt next to the standard trace log. Typed members only.</summary>
	public class _InstrDump : _CustomSignal
	{
		private const int SESSION_END_BARS = 3;   // A2: how many LastBarInSession bars to print after the first bar
		private static readonly Encoding s_utf8NoBom = new UTF8Encoding(false);
		private static readonly CultureInfo s_inv = CultureInfo.InvariantCulture;

		private string m_path;          // null = not created (or creation failed)
		private bool m_ioFailed;        // first IO error reported to Output once; the strategy keeps running
		private bool m_firstBarDone;
		private int m_sessionEndsSeen;
		private _MtfMath.Session[][] m_startCalcSessions;   // per stream (index = data stream), A2 typed comparison; null = unavailable

		public _InstrDump(object _ctx) : base(_ctx) { }

		protected override void StartCalc()
		{
			base.StartCalc();
			m_ioFailed = false;
			m_firstBarDone = false;
			m_sessionEndsSeen = 0;
			m_path = null;
			try
			{
				string file = "MCInstrDump_" + SafeName(Bars.Info.Name) + "_" + DateTime.Now.ToString("yyyyMMdd-HHmmss", s_inv) + "-" +
					System.Diagnostics.Process.GetCurrentProcess().Id.ToString(s_inv) + ".txt";
				m_path = Path.Combine(LogDirectory(), file);
				File.AppendAllText(m_path, "", s_utf8NoBom);   // prove we can write here
			}
			catch (Exception e) { reportIo(e); m_path = null; return; }
			traceAlways(ETraceLevel.Info, Bars.Info.Name, "INSTRDUMP file(" + m_path + ")");
			try { Output.WriteLine(GetType().Name + " " + Bars.Info.Name + ": INSTRDUMP file " + m_path); } catch { }

			line("M-INSTR PR-M0 dump run(" + RunId + ") written(" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", s_inv) + ") app(" + Environment.ApplicationCode + ") maxDataStream(" + Int(MaxDataStream) + ")");
			m_startCalcSessions = new _MtfMath.Session[MaxDataStream + 1][];
			for (int ds = 1; ds <= MaxDataStream; ds++)
			{
				if (!IsExist(ds)) { line("ds(" + Int(ds) + ") absent"); continue; }
				IInstrument b = BarsOfData(ds);
				dumpA1(ds, b.Info);
				string why;
				m_startCalcSessions[ds] = readSessions(b, out why);
				line("A2 ds(" + Int(ds) + ") at(StartCalc) sessions(" + sessionsText(m_startCalcSessions[ds], why) + ")");
			}
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (m_path == null) return;
			if (!m_firstBarDone)
			{
				m_firstBarDone = true;
				dumpFirstBar();
				return;
			}
			if (m_sessionEndsSeen < SESSION_END_BARS)
			{
				bool last;
				try { last = Bars.LastBarInSession; } catch (Exception) { return; }
				if (last)
				{
					m_sessionEndsSeen++;
					line("A2 ds(1) sessionEndBar(" + Int(m_sessionEndsSeen) + ") time(" + Ts(Bars.Time[0]) + ") lastBarInSession(True)");
				}
			}
		}

		private void dumpFirstBar()
		{
			for (int ds = 1; ds <= MaxDataStream; ds++)
			{
				if (!IsExist(ds)) continue;
				IInstrument b = BarsOfData(ds);
				string why;
				_MtfMath.Session[] now = readSessions(b, out why);
				_MtfMath.Session[] atStart = m_startCalcSessions != null && ds < m_startCalcSessions.Length ? m_startCalcSessions[ds] : null;
				line("A2 ds(" + Int(ds) + ") at(FirstCalcBar) sessions(" + sessionsText(now, why) + ") sameAsStartCalc(" + (Same(now, atStart) ? "yes" : "no") + ")");
				string t, lbis;
				try { t = Ts(b.Time[0]); } catch (Exception e) { t = "<" + e.GetType().Name + ">"; }
				try { lbis = b.LastBarInSession ? "True" : "False"; } catch (Exception e) { lbis = "<" + e.GetType().Name + ">"; }
				line("A2 ds(" + Int(ds) + ") firstBar time(" + t + ") tz(" + b.Info.TimeZone + ") lastBarInSession(" + lbis + ")");
			}

			// ---- B: one candidate per build (spec §4.4 B). Remove the // of ONE line, compile, run once, then put the // back. ----
			// B1 Info.Expiration (DateTime): a compile error means the member does not exist on this build.
			// line("B1 expiration(" + Bars.Info.Expiration.ToString("yyyy-MM-dd", s_inv) + ")");
			// B2 StrategyCurrencyCode on a plain signal (MTPA_MCSymbolCurrency, log only): a compile error means it is portfolio-only.
			// line("B2 strategyCurrencyCode(" + StrategyCurrencyCode + ") int(" + Int((int)StrategyCurrencyCode) + ")");
			line("B none (both candidates commented out in this build)");
		}

		/// <summary>A1: fields that compile today (spec §4.4 A1). Enums are formatted for the log only (R1).</summary>
		private static string exc(Exception e) { return "<" + e.GetType().Name + ">"; }

		private void dumpA1(int ds, IInstrumentSettings i)
		{
			string p = "A1 ds(" + Int(ds) + ") ";
			// PR-M2: every read guarded (a member that throws on some feed prints <ExceptionType> instead of aborting the dump)
			string v;
			try { v = "name(" + i.Name + ")"; } catch (Exception e) { v = "name(" + exc(e) + ")"; }
			line(p + v);
			try { v = "cat(" + i.Category + ") catInt(" + Int((int)i.Category) + ")"; } catch (Exception e) { v = "cat(" + exc(e) + ")"; }
			line(p + v);
			try { v = "exch(" + i.Exchange + ")"; } catch (Exception e) { v = "exch(" + exc(e) + ")"; }
			line(p + v);
			try { v = "tz(" + i.TimeZone + ")"; } catch (Exception e) { v = "tz(" + exc(e) + ")"; }
			line(p + v);
			try { v = "minMove(" + Num(i.MinMove) + ") priceScale(" + Num(i.PriceScale) + ") bpv(" + Num(i.BigPointValue) + ")"; } catch (Exception e) { v = "minMove(" + exc(e) + ")"; }
			line(p + v);
			try { v = "bvo(" + i.BVO + ")"; } catch (Exception e) { v = "bvo(" + exc(e) + ")"; }
			line(p + v);
			try { v = "res.Type(" + i.Resolution.Type + ")"; } catch (Exception e) { v = "res.Type(" + exc(e) + ")"; }
			line(p + v);
			string sn;
			try { sn = i.SessionName; } catch (Exception e) { sn = exc(e); }
			line(p + "sessionName(" + sn + ")");
		}

		/// <summary>Typed read of IInstrument.Sessions / SessionObject (same members as _CustomSignal.mtfSessions). null when the list
		/// is null (why = "null") or reading it throws (why = "<ExceptionType>").</summary>
		private static _MtfMath.Session[] readSessions(IInstrument b, out string why)
		{
			why = null;
			try
			{
				IROList<SessionObject> list = b.Sessions;
				if (list == null) { why = "null"; return null; }
				var r = new _MtfMath.Session[list.Count];
				for (int k = 0; k < r.Length; k++) { SessionObject so = list[k]; r[k] = new _MtfMath.Session(so.StartDay, so.StartTime, so.EndDay, so.EndTime); }
				return r;
			}
			catch (Exception e) { why = "<" + e.GetType().Name + ">"; return null; }
		}

		/// <summary>"n=<count>; <StartDay> <StartTime> -> <EndDay> <EndTime>; ..." (log only). TimeSpan "c" (invariant; keeps a
		/// 1.00:00:00 end, unlike hh:mm).</summary>
		private static string sessionsText(_MtfMath.Session[] s, string why)
		{
			if (s == null) return why ?? "null";
			var sb = new StringBuilder();
			sb.Append("n=").Append(s.Length.ToString(s_inv));
			foreach (_MtfMath.Session so in s)
				sb.Append("; ").Append(so.StartDay).Append(' ').Append(so.StartTime.ToString("c", s_inv))
					.Append(" -> ").Append(so.EndDay).Append(' ').Append(so.EndTime.ToString("c", s_inv));
			return sb.ToString();
		}

		/// <summary>A2: typed field-by-field equality (DayOfWeek and TimeSpan ==); two unavailable reads count as different.</summary>
		private static bool Same(_MtfMath.Session[] a, _MtfMath.Session[] b)
		{
			if (a == null || b == null || a.Length != b.Length) return false;
			for (int k = 0; k < a.Length; k++)
				if (a[k].StartDay != b[k].StartDay || a[k].StartTime != b[k].StartTime || a[k].EndDay != b[k].EndDay || a[k].EndTime != b[k].EndTime) return false;
			return true;
		}

		/// <summary>One File.AppendAllText per line; IO errors go to Output once and never stop the strategy.</summary>
		private void line(string text)
		{
			if (m_path == null) return;
			try { File.AppendAllText(m_path, text + System.Environment.NewLine, s_utf8NoBom); }
			catch (Exception e) { reportIo(e); }
		}

		private void reportIo(Exception e)
		{
			if (m_ioFailed) return;
			m_ioFailed = true;
			try { Output.WriteLine(GetType().Name + ": INSTRDUMP write failed (" + e.GetType().Name + ": " + e.Message + ")"); } catch { }
		}

		/// <summary>Bars.Info.Name with every invalid file-name character and '/' replaced by '_' (@ES stays @ES; EUR/USD -> EUR_USD).</summary>
		private static string SafeName(string name)
		{
			if (string.IsNullOrEmpty(name)) return "_";
			char[] bad = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder(name.Length);
			foreach (char c in name) sb.Append(c == '/' || Array.IndexOf(bad, c) >= 0 ? '_' : c);
			return sb.ToString();
		}

		private static string Num(double v) { return v.ToString("R", s_inv); }
		private static string Int(int v) { return v.ToString(s_inv); }
		private static string Ts(DateTime t) { return t.ToString("yyyy-MM-dd HH:mm:ss", s_inv); }
	}
}
