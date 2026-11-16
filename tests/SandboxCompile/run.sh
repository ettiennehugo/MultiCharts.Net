#!/bin/bash
# Semantic C# 7.3 compile of every sandbox strategy, then the naming and header checks.
# MultiCharts reference assemblies are not in this repo. The stubs are the documented MC shapes
# the studies call. Native F7 after a slot copy is still required.
set -e
here="$(cd "$(dirname "$0")" && pwd)"
cd "$here" && dotnet run -c Release
