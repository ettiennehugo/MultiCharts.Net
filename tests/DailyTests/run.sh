#!/bin/bash
# Usage: tests/DailyTests/run.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
cp "$root/_DailyContext.Function.CS" "$here/Generated/DailyContext.cs"
cp "$root/_AvwapManager.Function.CS" "$here/Generated/AvwapManager.cs"   # _AvwapCalendar week keys
cd "$here" && dotnet run -c Release
