#!/bin/bash
# Usage: tests/OrderFlowTests/run.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
{ echo "using System; using System.Collections.Generic;"; echo "namespace Extracted { public sealed class OrderFlow {"
  awk '/\/\/ <order-flow>/ {on=1; next} /\/\/ <\/order-flow>/ {on=0} on' "$root/_OrderFlow.Function.CS"
  echo "} }"; } > "$here/Generated/OrderFlow.cs"
cd "$here" && dotnet run -c Release
