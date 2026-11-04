#!/bin/bash
# Usage: tests/InstrumentTests/gen.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
# M-INSTR PR-M2: extracts the MC-free <instr-core> region of _InstrumentProfile.Function.CS, its typed <instr-mc-fields> region
# against a mirror of MTPA_SymbolFields (spec §4.1 value of eMTPA_SF_Root), and the calendar / event rules (as tests/EventTests).
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
rules() { awk -v tag="$2" '$0 ~ "// <calendar-rules "tag">" {on=1; next} $0 ~ "// </calendar-rules "tag">" {on=0} on' "$root/$1"; }
region() { awk -v tag="$2" '$0 ~ "^[ \t]*// <"tag">[ \t]*$" {on=1; next} $0 ~ "^[ \t]*// </"tag">" {on=0} on' "$root/$1"; }
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function {"
  echo "public sealed class _CmeHolidays {"; rules _CmeHolidays.Function.CS cme; echo "}"
  echo "public sealed class _NyseHolidays {"; rules _NyseHolidays.Function.CS nyse; echo "}"
  echo "}"; } > "$here/Generated/Calendars.cs"
{ echo "using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Security.Cryptography; using System.Text;"
  echo "namespace PowerLanguage.Function {"
  region _EventCalendar.Function.CS event-rules; region _EventCalendar.Function.CS event-table; region _EventCalendar.Function.CS event-engine
  echo "}"; } > "$here/Generated/Event.cs"
{ echo "using System; using System.Collections.Generic; using System.Globalization; using System.Text;"
  echo "namespace PowerLanguage.Function {"
  region _InstrumentProfile.Function.CS instr-core
  echo "}"; } > "$here/Generated/InstrumentCore.cs"
{ echo "using System;"; echo "namespace Mirror {"
  echo "public enum MTPA_SymbolFields { eMTPA_SF_Root = 1 }"
  echo "public static class InstrumentMcFields {"
  region _InstrumentProfile.Function.CS instr-mc-fields
  echo "} }"; } > "$here/Generated/McFields.cs"
