#!/bin/bash
# Usage: tests/RegimeGateTests/run.sh   (needs the .NET 8 SDK)
set -e
cd "$(cd "$(dirname "$0")" && pwd)"
dotnet run -c Release
