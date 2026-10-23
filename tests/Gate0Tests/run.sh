#!/bin/bash
# Usage: tests/Gate0Tests/run.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
cd "$here" && dotnet run -c Release
