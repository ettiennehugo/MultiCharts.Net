#!/bin/bash
# Usage: tests/DailyTests/run.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
cp "$root/_DailyContext.Function.CS" "$here/Generated/DailyContext.cs"
cp "$root/_AvwapManager.Function.CS" "$here/Generated/AvwapManager.cs"   # _AvwapCalendar week keys
cp "$root/_Rng.Function.CS" "$here/Generated/Rng.cs"
sed '1s/^\xEF\xBB\xBF//' "$root/_CmeSessionClock.Function.CS" > "$here/Generated/CmeSessionClock.cs"   # S2 half-day RTH close
rules() { awk -v tag="$2" '$0 ~ "// <calendar-rules "tag">" {on=1; next} $0 ~ "// </calendar-rules "tag">" {on=0} on' "$root/$1"; }
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function {"
  echo "public sealed class _CmeHolidays {"; rules _CmeHolidays.Function.CS cme; echo "}"
  echo "public sealed class _NyseHolidays {"; rules _NyseHolidays.Function.CS nyse; echo "}"
  echo "}"; } > "$here/Generated/Calendars.cs"
cd "$here" && dotnet run -c Release
