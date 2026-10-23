#!/bin/bash
# Extracts the MC-free CME calendar rules (_CmeHolidays.Function.CS, <calendar-rules cme>) into Generated/ (same as tests/CalendarTests).
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
awk '$0 ~ "// <calendar-rules cme>" {on=1; next} $0 ~ "// </calendar-rules cme>" {on=0} on' "$root/_CmeHolidays.Function.CS" > "$here/Generated/Cme.body"
{ echo "using System; using System.Collections.Generic;"; echo "namespace Extracted.Cme {"; cat "$here/Generated/Cme.body"; echo "}"; } > "$here/Generated/Cme.cs"
rm "$here/Generated/Cme.body"
