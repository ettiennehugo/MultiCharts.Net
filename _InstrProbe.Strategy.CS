using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	// ================================================================================================================
	// _InstrProbe — M-INSTR PR-I0 probe (specs/instrument-profile-spec.md rev 3, §12 PR-I0; closes VERIFY items IP-1.1..IP-1.5).
	// ================================================================================================================
	// One-off diagnostic. Places NO orders, has NO inputs, touches no engine and does not derive from _CustomSignal.
	// On the first bar of every data stream (Data1..DataN) it reports:
	//   1. FIXTURE   one IP-11.2 fixture line: name|category|exchange|minMove|priceScale|bigPointValue|expiration|timeZone|description
	//   2. TYPED     the verified IInstrumentSettings members read directly (Name, Category raw + int, Exchange, MinMove, ...)
	//   3. ENUM      Enum.GetNames(typeof(ESymbolCategory)) with values (IP-1.1)
	//   3b. FIRSTBAR Time[0] and CurrentBar of the stream (own section, after ENUM, so a stream without a bar yet loses nothing above)
	//   4. INFO      every public property of Bars.Info by reflection (Expiration, Description, SessionName, QuoteField, ...)
	//   5. BARS      every public property of the Bars object (looks for Sessions, IP-1.3)
	//   6. SESSION   any Info/Bars property whose name contains "Session" or "SymbolInfo", dumped 3 levels deep (IP-1.2/1.3)
	//   7. TYPES     public MC types named *SymbolInfo* / *Session* / *SymbolCategory* / *Currency* / *InstrumentSettings*, with members
	// Output: the PowerLanguage .NET Editor Output window AND the file OUT_DIR\InstrProbe_<symbol>_ds<n>.txt (one per data stream;
	// <symbol> made file-safe, @ES -> _ES; overwritten each run). If OUT_DIR cannot be written, retried once in %TEMP%\InstrProbe.
	// Each section has its own try: a failing section writes "ERROR <section> ..." and the rest still run (FIXTURE/TYPED/ENUM first).
	// Import: _InstrProbeCore.Function.CS first, then this file. Run instructions: PR-I0 description.
	// ================================================================================================================
	public class _InstrProbe : SignalObject
	{
		public const string OUT_DIR = @"C:\Temp\InstrProbe";
		private bool[] m_done;

		public _InstrProbe(object _ctx) : base(_ctx) { }

		protected override void Create() { }

		protected override void StartCalc() { m_done = null; }

		protected override void CalcBar()
		{
			int n = MaxDataStream; if (n < 1) n = 1;
			if (m_done == null) m_done = new bool[n + 1];
			for (int ds = 1; ds <= n; ds++)
			{
				if (m_done[ds]) continue;
				m_done[ds] = true;
				probe(ds, ds == 1 ? Bars : BarsOfData(ds));
			}
		}

		private static void section(List<string> lines, string p, string tag, Action body)
		{
			try { body(); }
			catch (Exception e) { lines.Add(p + " ERROR " + tag + " " + InstrProbeCore.Err(e) + " at " + InstrProbeCore.OneLine(e.StackTrace)); }
		}

		private void probe(int ds, IInstrument bars)
		{
			var lines = new List<string>();
			string p = InstrProbeCore.PREFIX + " ds(" + ds + ")";
			string sym = "unknown";
			IInstrumentSettings info = null;
			lines.Add(p + " BEGIN probeVersion(2) runLocal(" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ") study(" + Name + ")");
			section(lines, p, "INFO-READ", () => { info = bars.Info; sym = info.Name; });
			if (info != null)
			{
				object exp = get(info, "Expiration"), desc = get(info, "Description");
				section(lines, p, "FIXTURE", () => lines.Add(p + " FIXTURE " + InstrProbeCore.FixtureLine(info.Name, info.Category.ToString(), info.Exchange, info.MinMove, info.PriceScale,
					info.BigPointValue, exp is DateTime ? (DateTime)exp : default(DateTime), info.TimeZone.ToString(), desc as string)));

				section(lines, p, "TYPED", () =>
				{
					var res = info.Resolution;
					string resText = res == null ? "<null>" : res.Type + " " + res.Size;
					lines.Add(p + " TYPED name(" + info.Name + ") feed(" + info.DataFeed + ") category(" + info.Category.ToString() + ") categoryInt(" + Convert.ToInt64(info.Category) +
						") categoryUpper(" + info.Category.ToString().ToUpperInvariant() + ") exchange(" + info.Exchange + ") minMove(" + InstrProbeCore.FormatValue(info.MinMove) +
						") priceScale(" + InstrProbeCore.FormatValue(info.PriceScale) + ") tick(" + InstrProbeCore.FormatValue(info.MinMove / info.PriceScale) +
						") bigPointValue(" + InstrProbeCore.FormatValue(info.BigPointValue) + ") timeZone(" + info.TimeZone.ToString() +
						") resolution(" + resText + ") expiration(" + InstrProbeCore.FormatValue(exp) + ") contSeries(" +
						(info.Name != null && info.Name.StartsWith("@") ? 1 : 0) + ")");
				});
			}
			section(lines, p, "ENUM", () => lines.Add(p + " ENUM ESymbolCategory " + string.Join(",", InstrProbeCore.EnumMembers(typeof(ESymbolCategory)).ToArray())));
			section(lines, p, "FIRSTBAR", () => lines.Add(p + " FIRSTBAR time(" + bars.Time[0].ToString("yyyy-MM-dd HH:mm:ss") + ") currentBar(" + bars.CurrentBar + ")"));

			if (info != null)
				section(lines, p, "INFO", () => { var d = new List<string>(); InstrProbeCore.Dump(info, "Info", 2, d); foreach (string s in d) lines.Add(p + " INFO " + s); });
			section(lines, p, "BARS", () => { var d = new List<string>(); InstrProbeCore.Dump(bars, "Bars", 1, d); foreach (string s in d) lines.Add(p + " BARS " + s); });

			section(lines, p, "SESSION", () =>
			{
				var d = new List<string>();
				foreach (object[] o in new[] { new object[] { "Info", info }, new object[] { "Bars", bars } })
				{
					if (o[1] == null) continue;
					foreach (PropertyInfo pi in InstrProbeCore.Properties(o[1].GetType()))
						if (pi.Name.IndexOf("Session", StringComparison.OrdinalIgnoreCase) >= 0 || pi.Name.IndexOf("SymbolInfo", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							d.Clear();
							object v; try { v = pi.GetValue(o[1], null); } catch (Exception e) { lines.Add(p + " SESSION " + o[0] + "." + pi.Name + " = " + InstrProbeCore.Err(e)); continue; }
							InstrProbeCore.Dump(v, o[0] + "." + pi.Name, 3, d);
							foreach (string s in d) lines.Add(p + " SESSION " + s);
						}
				}
			});

			if (ds == 1)
				section(lines, p, "TYPES", () =>
				{
					var asms = new List<Assembly>();
					foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
					{
						string an = a.GetName().Name;
						if (an.StartsWith("System") || an.StartsWith("Microsoft") || an == "mscorlib" || an == "netstandard" || a.IsDynamic) continue;
						asms.Add(a);
					}
					foreach (Type t in InstrProbeCore.FindTypes(asms, new[] { "SymbolInfo", "Session", "SymbolCategory", "Currency", "InstrumentSettings" }))
					{
						lines.Add(p + " TYPES " + t.FullName + " [" + t.Assembly.GetName().Name + "]");
						foreach (string m in InstrProbeCore.Members(t)) lines.Add(p + " TYPES   " + t.Name + " " + m);
					}
				});
			lines.Add(p + " END lines(" + (lines.Count + 1) + ")");

			foreach (string s in lines) Output.WriteLine("{0}", s);
			string file = "InstrProbe_" + InstrProbeCore.SafeFileName(sym) + "_ds" + ds + ".txt";
			string primary = Path.Combine(OUT_DIR, file), fallback = null;
			string err = write(primary, lines);
			if (err == null) { Output.WriteLine("{0} ds({1}) written to {2}", InstrProbeCore.PREFIX, ds, primary); return; }
			Output.WriteLine("{0} ds({1}) file write failed: {2} {3}", InstrProbeCore.PREFIX, ds, primary, err);
			try { fallback = Path.Combine(Path.Combine(Path.GetTempPath(), "InstrProbe"), file); } catch (Exception e) { Output.WriteLine("{0} ds({1}) no temp path: {2}", InstrProbeCore.PREFIX, ds, InstrProbeCore.Err(e)); return; }
			string err2 = write(fallback, lines);
			if (err2 == null) Output.WriteLine("{0} ds({1}) written to fallback {2} (primary {3} failed)", InstrProbeCore.PREFIX, ds, fallback, primary);
			else Output.WriteLine("{0} ds({1}) fallback write failed too: {2} {3}; primary {4}. Copy the INSTRPROBE lines from this Output window.", InstrProbeCore.PREFIX, ds, fallback, err2, primary);
		}

		private static string write(string path, List<string> lines)
		{
			try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllLines(path, lines.ToArray()); return null; }
			catch (Exception e) { return InstrProbeCore.Err(e); }
		}

		private static object get(object o, string prop)
		{
			try { PropertyInfo pi = o.GetType().GetProperty(prop); if (pi == null) foreach (Type i in o.GetType().GetInterfaces()) { pi = i.GetProperty(prop); if (pi != null) break; } return pi == null ? null : pi.GetValue(o, null); }
			catch (Exception) { return null; }
		}
	}
}
