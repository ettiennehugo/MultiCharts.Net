#!/bin/bash
# Usage: tests/VpHelperTests/gen.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
# Phase 3 VP helpers (VP-1..VP-6 + _BarRing, _Rng, _LevelRef): the pure helper files are copied whole; ProfileMath and
# OrderFlow are extracted from their regions under their study-file class names.
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function {"
  awk '/\/\/ <profile-math>/ {on=1; next} /\/\/ <\/profile-math>/ {on=0} on' "$root/_ProfileMath.Function.CS"
  echo "}"; } > "$here/Generated/ProfileMath.cs"
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function { public sealed class _OrderFlow {"
  awk '/\/\/ <order-flow>/ {on=1; next} /\/\/ <\/order-flow>/ {on=0} on' "$root/_OrderFlow.Function.CS"
  echo "} }"; } > "$here/Generated/OrderFlow.cs"
# PR #47: MC-free confluence rules (zone split width rule, distinct sources, duplicate-stream check).
{ echo "using System; using System.Collections.Generic;"; echo "namespace PowerLanguage.Function {"
  awk '/\/\/ <vpconf-rules>/ {on=1; next} /\/\/ <\/vpconf-rules>/ {on=0} on' "$root/_VpConfluenceEngine.Function.CS"
  echo "}"; } > "$here/Generated/VpConfluenceRules.cs"
# PR I: MC-free VWAP roll-reset rule + VwapConfig (opt-in Week/Month reset at a roll, default off).
{ echo "using System;"; echo "namespace PowerLanguage.Function {"
  awk '/\/\/ <vwap-roll-rules>/ {on=1; next} /\/\/ <\/vwap-roll-rules>/ {on=0} on' "$root/_VpVwapEngine.Function.CS"
  echo "}"; } > "$here/Generated/VwapRollRules.cs"
# fix/lelx-profile-walk-hurdle: MC-free LELX rules (bounded profile walk, profit hurdle on actual risk).
{ echo "using System;"; echo "namespace PowerLanguage.Strategy {"
  awk '/\/\/ <lelx-rules>/ {on=1; next} /\/\/ <\/lelx-rules>/ {on=0} on' "$root/_DeltaDivRevFuturesLELX.Strategy.CS"
  echo "}"; } > "$here/Generated/LelxRules.cs"
