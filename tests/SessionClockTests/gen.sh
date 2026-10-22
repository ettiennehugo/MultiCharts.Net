#!/bin/bash
# Usage: tests/SessionClockTests/gen.sh   (needs the .NET 8 SDK and git; on Windows run the same steps in Git Bash or WSL)
# Spec VPF-7: tests the shipped _CmeSessionClock.Function.CS and compares it with the pre-VPF-7 session code of
# _VolumeProfileFuturesBase (git main 4d18771, the PR #4 merge) over every 5 minutes of 2017-2026.
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
gen="$here/Generated"; mkdir -p "$gen"
rules() { awk -v tag="$2" '$0 ~ "// <calendar-rules "tag">" {on=1; next} $0 ~ "// </calendar-rules "tag">" {on=0} on' "$root/$1"; }
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function {"
  echo "public sealed class _CmeHolidays {"; rules _CmeHolidays.Function.CS cme; echo "}"
  echo "public sealed class _NyseHolidays {"; rules _NyseHolidays.Function.CS nyse; echo "}"
  echo "}"; } > "$gen/Calendars.cs"
# Legacy oracle: the pre-VPF-7 Eastern-time cores, verbatim (line ranges of that commit), inside a stand-in class.
legacy="$(cd "$root" && git show 4d18771:_VolumeProfileFuturesBase.Strategy.CS | sed '1s/^\xEF\xBB\xBF//')"
{ echo "using System; using System.Collections.Generic; using PowerLanguage.Function;"; echo "namespace Legacy {"
  echo "public partial class LegacyClock {"
  echo "$legacy" | sed -n '75,100p'      # enums MarketSession, GlobexState + session window fields
  echo "$legacy" | sed -n '188,258p'    # globexStateEt, s_singleSessions, sessionsEt, globexDateEt
  echo "$legacy" | sed -n '355,362p'    # InWindow
  echo "$legacy" | sed -n '496,533p'    # TrySessionBounds
  echo "$legacy" | sed -n '545,589p'    # TryWindow
  echo "$legacy" | sed -n '886,890p'    # IsSingleFlag
  echo "$legacy" | sed -n '631,645p'    # RthValueEndEt, IsRthValueBarEt
  echo "$legacy" | sed -n '833p;841,850p;866,869p;875,878p'   # UseCmeHolidayTable, IsGlobexClosedDay, HolidayHaltEt, RthEarlyCloseEt
  echo "}}"; } > "$gen/Legacy.cs"
