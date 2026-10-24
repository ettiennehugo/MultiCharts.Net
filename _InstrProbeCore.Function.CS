using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace PowerLanguage.Function
{
	// M-INSTR PR-I0 (specs/instrument-profile-spec.md rev 3, §12 PR-I0, IP-1.1..IP-1.5, IP-11.2, IP-T60).
	// MC-free helpers for the one-off _InstrProbe strategy: a reflection dumper of any object (IInstrumentSettings, Bars, ...),
	// enum listing, type search over loaded assemblies and the IP-11.2 fixture line. No MultiCharts types inside <instr-probe-core>,
	// so tests/InstrProbeTests/gen.sh can extract and test it. Nothing here is used by any engine.
	// <instr-probe-core>
	public static class InstrProbeCore
	{
		public const string PREFIX = "INSTRPROBE";
		public const int MAX_ITEMS = 20;

		/// <summary>Types whose value is printed directly (no recursion).</summary>
		public static bool IsSimple(Type t)
		{
			if (t == null) return true;
			Type u = Nullable.GetUnderlyingType(t); if (u != null) t = u;
			return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(TimeSpan) || t == typeof(DateTimeOffset) || t == typeof(Guid);
		}

		/// <summary>Invariant, single-line text of a value; enums as Name(=int); doubles round-trip ("R").</summary>
		public static string FormatValue(object v)
		{
			if (v == null) return "<null>";
			if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
			if (v is float) return ((float)v).ToString("R", CultureInfo.InvariantCulture);
			if (v is DateTime) return ((DateTime)v).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " kind=" + ((DateTime)v).Kind;
			if (v is Enum) return v.ToString() + "(=" + Convert.ToInt64(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + ")";
			if (v is string) return "\"" + OneLine((string)v) + "\"";
			IFormattable f = v as IFormattable;
			return OneLine(f != null ? f.ToString(null, CultureInfo.InvariantCulture) : v.ToString());
		}

		public static string OneLine(string s)
		{
			if (s == null) return "";
			return s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
		}

		/// <summary>Every readable public instance property of the runtime type (and its interfaces), sorted by name, de-duplicated.</summary>
		public static List<PropertyInfo> Properties(Type t)
		{
			var seen = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
			var types = new List<Type> { t };
			types.AddRange(t.GetInterfaces());
			foreach (Type x in types)
				foreach (PropertyInfo p in x.GetProperties(BindingFlags.Public | BindingFlags.Instance))
					if (p.CanRead && p.GetIndexParameters().Length == 0 && !seen.ContainsKey(p.Name)) seen[p.Name] = p;
			var keys = new List<string>(seen.Keys); keys.Sort(StringComparer.Ordinal);
			var r = new List<PropertyInfo>(); foreach (string k in keys) r.Add(seen[k]); return r;
		}

		/// <summary>
		/// Dumps obj as lines "path : DeclaredType = value". Simple values are printed; other objects recurse up to depth;
		/// enumerables (not strings) list up to MAX_ITEMS items. A throwing getter prints "&lt;ExceptionType: message&gt;".
		/// </summary>
		public static void Dump(object obj, string path, int depth, List<string> lines)
		{
			Dump(obj, path, depth, lines, new List<object>());
		}

		static void Dump(object obj, string path, int depth, List<string> lines, List<object> stack)
		{
			if (obj == null) { lines.Add(path + " = <null>"); return; }
			Type t = obj.GetType();
			if (IsSimple(t)) { lines.Add(path + " = " + FormatValue(obj)); return; }
			foreach (object s in stack) if (ReferenceEquals(s, obj)) { lines.Add(path + " = <cycle>"); return; }
			lines.Add(path + " : runtimeType = " + t.FullName);
			if (depth <= 0) { lines.Add(path + " = " + FormatValue(SafeToString(obj))); return; }
			stack.Add(obj);
			IEnumerable en = obj as IEnumerable;
			if (en != null)
			{
				int n = 0;
				try
				{
					foreach (object item in en)
					{
						if (n >= MAX_ITEMS) { lines.Add(path + "[...] = <truncated at " + MAX_ITEMS + ">"); break; }
						Dump(item, path + "[" + n + "]", depth - 1, lines, stack); n++;
					}
					lines.Add(path + ".<count listed> = " + n);
				}
				catch (Exception e) { lines.Add(path + "[enum] = " + Err(e)); }
			}
			foreach (PropertyInfo p in Properties(t))
			{
				object v; string label = path + "." + p.Name + " : " + TypeName(p.PropertyType);
				try { v = p.GetValue(obj, null); }
				catch (Exception e) { lines.Add(label + " = " + Err(e)); continue; }
				if (v == null || IsSimple(v.GetType())) { lines.Add(label + " = " + FormatValue(v)); continue; }
				lines.Add(label + " = {" + FormatValue(SafeToString(v)) + "}");
				Dump(v, path + "." + p.Name, depth - 1, lines, stack);
			}
			stack.RemoveAt(stack.Count - 1);
		}

		static string SafeToString(object o) { try { return o.ToString(); } catch (Exception e) { return Err(e); } }

		public static string Err(Exception e)
		{
			while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
			return "<" + e.GetType().Name + ": " + OneLine(e.Message) + ">";
		}

		public static string TypeName(Type t)
		{
			if (t == null) return "?";
			if (!t.IsGenericType) return t.Name;
			var sb = new StringBuilder(); string n = t.Name; int q = n.IndexOf('`'); sb.Append(q > 0 ? n.Substring(0, q) : n).Append('<');
			Type[] a = t.GetGenericArguments(); for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(TypeName(a[i])); }
			return sb.Append('>').ToString();
		}

		/// <summary>"Name=int" for every member of an enum type, in declaration (value) order (IP-1.1).</summary>
		public static List<string> EnumMembers(Type enumType)
		{
			var r = new List<string>();
			string[] names = Enum.GetNames(enumType); Array values = Enum.GetValues(enumType);
			for (int i = 0; i < names.Length; i++) r.Add(names[i] + "=" + Convert.ToInt64(values.GetValue(i), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
			return r;
		}

		/// <summary>Public types in the given assemblies whose name contains any token (case-insensitive), sorted by full name (IP-1.2/1.3).</summary>
		public static List<Type> FindTypes(IEnumerable<Assembly> assemblies, string[] tokens)
		{
			var r = new List<Type>();
			foreach (Assembly a in assemblies)
			{
				Type[] ts;
				try { ts = a.GetExportedTypes(); }
				catch (ReflectionTypeLoadException e) { ts = e.Types; }
				catch (Exception) { continue; }
				foreach (Type t in ts)
				{
					if (t == null) continue;
					foreach (string k in tokens)
						if (t.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) { r.Add(t); break; }
				}
			}
			r.Sort((x, y) => string.CompareOrdinal(x.FullName, y.FullName));
			return r;
		}

		/// <summary>Public member signatures of a type (properties, methods without property accessors, fields; enum members for enums).</summary>
		public static List<string> Members(Type t)
		{
			var r = new List<string>();
			if (t.IsEnum) { foreach (string s in EnumMembers(t)) r.Add("enum " + s); return r; }
			foreach (PropertyInfo p in Properties(t)) r.Add("prop " + TypeName(p.PropertyType) + " " + p.Name);
			var ms = new List<string>();
			foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
			{
				if (m.IsSpecialName) continue;
				var sb = new StringBuilder("method " + TypeName(m.ReturnType) + " " + m.Name + "(");
				ParameterInfo[] ps = m.GetParameters(); for (int i = 0; i < ps.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(TypeName(ps[i].ParameterType)).Append(' ').Append(ps[i].Name); }
				ms.Add(sb.Append(')').ToString());
			}
			ms.Sort(StringComparer.Ordinal); r.AddRange(ms);
			foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) r.Add("field " + TypeName(f.FieldType) + " " + f.Name);
			return r;
		}

		// expiration = default(DateTime) (0001-01-01 00:00:00) when the member is missing or default; see TYPED expiration(<null>|date)
		// to tell them apart. '/' in a value may be an escaped '|'.
		/// <summary>IP-11.2 fixture line: name|category|exchange|minMove|priceScale|bigPointValue|expiration|timeZone|description ('|' in values escaped as '/').</summary>
		public static string FixtureLine(string name, string category, string exchange, double minMove, double priceScale, double bigPointValue, DateTime expiration, string timeZone, string description)
		{
			return string.Join("|", new[] {
				F(name), F(category), F(exchange),
				minMove.ToString("R", CultureInfo.InvariantCulture), priceScale.ToString("R", CultureInfo.InvariantCulture), bigPointValue.ToString("R", CultureInfo.InvariantCulture),
				expiration.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), F(timeZone), F(description) });
		}

		static string F(string s) { return OneLine(s ?? "").Replace('|', '/'); }

		/// <summary>File-name-safe form of a symbol name ("@ES" -> "_ES", "EUR/USD" -> "EUR_USD").</summary>
		public static string SafeFileName(string s)
		{
			if (string.IsNullOrEmpty(s)) return "unnamed";
			var sb = new StringBuilder(s.Length);
			foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_');
			return sb.ToString();
		}
	}
	// </instr-probe-core>
}
