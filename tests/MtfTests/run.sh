#!/bin/bash
# Usage: tests/MtfTests/run.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
cp "$root/_MtfMath.Function.CS" "$here/Generated/MtfMath.cs"
cd "$here" && dotnet run -c Release
