#!/bin/bash
# Usage: tests/ProfileTests/run.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
{ echo "using System; using System.Collections.Generic;"; echo "namespace Extracted {"
  awk '/\/\/ <profile-math>/ {on=1; next} /\/\/ <\/profile-math>/ {on=0} on' "$root/_VolumeProfileBase.Strategy.CS"
  echo "}"; } > "$here/Generated/ProfileMath.cs"
cd "$here" && dotnet run -c Release
