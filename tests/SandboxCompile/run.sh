#!/bin/bash
# Syntax-check every sandbox strategy as C# 7.3. Does not Compile-include sandbox (RepoLint).
# MultiCharts reference assemblies are not in this repo. Native F7 is still required.
set -e
here="$(cd "$(dirname "$0")" && pwd)"
cd "$here" && dotnet run -c Release
