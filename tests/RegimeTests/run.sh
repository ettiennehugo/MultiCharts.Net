#!/bin/bash
# Usage: tests/RegimeTests/run.sh [--gv]   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
# --gv additionally exports gv/gv_features.csv + gv/gv_fits.json and runs reference.py (G-V; needs python3 with
#      numpy pandas scikit-learn hmmlearn, e.g. a venv: python3 -m venv .venv && .venv/bin/pip install numpy pandas scikit-learn hmmlearn;
#      set PYTHON=/path/to/python to pick it).
set -e
here="$(cd "$(dirname "$0")" && pwd)"
cd "$here"
if [ "$1" = "--gv" ]; then
  dotnet run -c Release -- --export gv
  "${PYTHON:-python3}" reference.py gv
else
  dotnet run -c Release
fi
