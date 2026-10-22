#!/bin/bash
# Usage: tests/AvwapTests/run.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
cp "$root/_AvwapManager.Function.CS" "$here/Generated/AvwapManager.cs"
cp "$root/_Rng.Function.CS" "$here/Generated/Rng.cs"
cd "$here" && dotnet run -c Release
