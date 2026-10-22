#!/bin/bash
# Usage: tests/DailyTests/gen.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
rules() { awk -v tag="$2" '$0 ~ "// <calendar-rules "tag">" {on=1; next} $0 ~ "// </calendar-rules "tag">" {on=0} on' "$root/$1"; }
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function {"
  echo "public sealed class _CmeHolidays {"; rules _CmeHolidays.Function.CS cme; echo "}"
  echo "public sealed class _NyseHolidays {"; rules _NyseHolidays.Function.CS nyse; echo "}"
  echo "}"; } > "$here/Generated/Calendars.cs"
