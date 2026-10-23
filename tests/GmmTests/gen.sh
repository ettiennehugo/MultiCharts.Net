#!/bin/bash
# Extracts the MC-free <profile-math> region of _ProfileMath.Function.CS (needed by _ProfileStats' KDE h_cv) into Generated/.
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function {"
  awk '/\/\/ <profile-math>/ {on=1; next} /\/\/ <\/profile-math>/ {on=0} on' "$root/_ProfileMath.Function.CS"
  echo "}"; } > "$here/Generated/ProfileMath.cs"
