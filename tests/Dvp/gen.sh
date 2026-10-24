#!/bin/bash
# Usage: tests/Dvp/gen.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
# M-DVP (spec DV-0.3): extracts the MC-free <dvp-core> region of _DvpEngine.Function.CS together with <profile-math> into
# Generated/, under their study-file class names (namespace PowerLanguage.Function). _LevelRef, _ProfileStats (DV-4.5 entropy)
# and its MC-free dependencies (_Gmm, _Rng, _BarRing) are linked whole by DvpTests.csproj, as in tests/GmmTests.
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
ext() { # $1 region, $2 source file, $3 output
  { echo "using System; using System.Collections.Generic; using System.Globalization; using System.Text;"; echo "namespace PowerLanguage.Function {"
    awk -v r="$1" '$0 ~ "// <" r ">" {on=1; next} $0 ~ "// </" r ">" {on=0} on' "$root/$2"
    echo "}"; } > "$here/Generated/$3"
}
ext dvp-core _DvpEngine.Function.CS DvpCore.cs
ext profile-math _ProfileMath.Function.CS ProfileMath.cs
