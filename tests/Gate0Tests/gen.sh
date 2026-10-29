#!/bin/bash
# Extracts the MC-free <profile-math> (and, since PR-D8, <dvp-core>) regions of _ProfileMath.Function.CS (needed by _ProfileStats' KDE h_cv) into Generated/.
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function {"
  awk '/\/\/ <profile-math>/ {on=1; next} /\/\/ <\/profile-math>/ {on=0} on' "$root/_ProfileMath.Function.CS"
  echo "}"; } > "$here/Generated/ProfileMath.cs"
# M-DVP PR-D8: the KD-02 canary hosts a _DvpEngine, so the MC-free <dvp-core> region is extracted too (as tests/Dvp/gen.sh does).
{ echo "using System; using System.Collections.Generic; using System.Globalization; using System.Text;"; echo "namespace PowerLanguage.Function {"
  awk '/\/\/ <dvp-core>/ {on=1; next} /\/\/ <\/dvp-core>/ {on=0} on' "$root/_DvpEngine.Function.CS"
  echo "}"; } > "$here/Generated/DvpCore.cs"
