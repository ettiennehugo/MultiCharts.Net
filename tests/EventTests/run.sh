#!/bin/bash
# Usage: tests/EventTests/run.sh   (needs the .NET 8 SDK; the build runs gen.sh itself)
set -e
cd "$(cd "$(dirname "$0")" && pwd)" && dotnet run -c Release
