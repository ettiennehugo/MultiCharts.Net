#!/bin/bash
# Usage: tests/GxMcCompile/run.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
cp "$root/sandbox/_GX_00_TestHost.Strategy.CS" "$here/Generated/Host.cs"
cd "$here" && dotnet run -c Release
