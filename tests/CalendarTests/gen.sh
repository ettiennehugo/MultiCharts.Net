#!/bin/bash
# Usage: tests/CalendarTests/gen.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
extract() { # file tag class
  awk -v tag="$2" '$0 ~ "// <calendar-rules "tag">" {on=1; next} $0 ~ "// </calendar-rules "tag">" {on=0} on' "$root/$1" > "$here/Generated/$3.body"
  { echo "using System; using System.Collections.Generic;"; echo "namespace Extracted.$3 {"; cat "$here/Generated/$3.body"; echo "}"; } > "$here/Generated/$3.cs"
  rm "$here/Generated/$3.body"
}
extract _NyseHolidays.Function.CS nyse Nyse
extract _CmeHolidays.Function.CS cme Cme
