#!/bin/bash
# Usage: tests/EventTests/gen.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
# Extracts the MC-free M-EVENT regions of _EventCalendar.Function.CS plus the NYSE / CME holiday rules.
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
rules() { awk -v tag="$2" '$0 ~ "// <calendar-rules "tag">" {on=1; next} $0 ~ "// </calendar-rules "tag">" {on=0} on' "$root/$1"; }
region() { awk -v tag="$1" '$0 ~ "^[ \t]*// <"tag">[ \t]*$" {on=1; next} $0 ~ "^[ \t]*// </"tag">" {on=0} on' "$root/_EventCalendar.Function.CS"; }
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function {"
  echo "public sealed class _CmeHolidays {"; rules _CmeHolidays.Function.CS cme; echo "}"
  echo "public sealed class _NyseHolidays {"; rules _NyseHolidays.Function.CS nyse; echo "}"
  echo "}"; } > "$here/Generated/Calendars.cs"
{ echo "using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Security.Cryptography; using System.Text;"
  echo "namespace PowerLanguage.Function {"
  region event-rules; region event-table; region event-engine
  echo "}"; } > "$here/Generated/Event.cs"
