#!/bin/bash
# Usage: tests/InstrProbeTests/gen.sh   (needs bash; run by the build)
# M-INSTR PR-I0: extracts the MC-free <instr-probe-core> region of _InstrProbeCore.Function.CS into Generated/ (tests/Dvp/gen.sh pattern).
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
ext() { # $1 region, $2 source file, $3 output
  { echo "using System; using System.Collections; using System.Collections.Generic; using System.Globalization; using System.Reflection; using System.Text;"; echo "namespace PowerLanguage.Function {"
    awk -v r="$1" '$0 ~ "// <" r ">" {on=1; next} $0 ~ "// </" r ">" {on=0} on' "$root/$2"
    echo "}"; } > "$here/Generated/$3"
}
ext instr-probe-core _InstrProbeCore.Function.CS InstrProbeCore.cs
