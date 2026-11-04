#!/bin/bash
# Usage: tests/OrderFlowTests/gen.sh   (needs the .NET 8 SDK; on Windows run the same steps in Git Bash or WSL)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; root="$here/../.."
mkdir -p "$here/Generated"
{ echo "using System; using System.Collections.Generic;"; echo "namespace Extracted { public sealed class OrderFlow {"
  awk '/\/\/ <order-flow>/ {on=1; next} /\/\/ <\/order-flow>/ {on=0} on' "$root/_OrderFlow.Function.CS"
  echo "} }"; } > "$here/Generated/OrderFlow.cs"
# M-INSTR PR-M1b: the <instr-host-core> region of _CustomSignal (isFuturesCategory, ClassOf, autoTradingDayOf, tick helpers),
# compiled against a mirror of ESymbolCategory with the member values of spec §4.1 (Ettienne's _McApiDump3). protected -> internal
# so the tests can call the static helpers.
{ echo "using System; using System.Globalization;"; echo "namespace Extracted {"
  echo "public enum ESymbolCategory { Future = 0, FutureOption = 1, Stock = 2, StockOption = 3, Index = 4, CurrOption = 5, MutualFund = 6, MoneyMarketFund = 7, IndexOption = 8, Cash = 9, Bond = 10, Spread = 11, Forex = 12, CPCSymbol = 13, Composites = 14, FutureRoot = 15, FutureOptionRoot = 16, StockOptionRoot = 17, FutureRolover = 18, Commodity = 19, Crypto = 20, Warrant = 21, None = 254 }"
  # PR-M2: EAssetClass is a top-level type of _InstrumentProfile.Function.CS (its <instr-asset-class> region)
  awk '/\/\/ <instr-asset-class>/ {on=1; next} /\/\/ <\/instr-asset-class>/ {on=0} on' "$root/_InstrumentProfile.Function.CS"
  echo "public sealed class InstrHost {"
  awk '/\/\/ <instr-host-core>/ {on=1; next} /\/\/ <\/instr-host-core>/ {on=0} on' "$root/_CustomSignal.Strategy.CS" | sed 's/protected static/internal static/'
  echo "} }"; } > "$here/Generated/InstrHost.cs"
# M-INSTR PR-M1c: the <legacy-exit-core> region of _ExitHalfRiskHalfRoll (F3a oneTick, F3b stopLossAmount) against a mirror of MC's
# ESpecOrdersMode; private -> internal so the tests can call it.
{ echo "using System;"; echo "namespace Extracted {"
  echo "public enum ESpecOrdersMode { PerContract, PerPosition }"
  echo "public static class LegacyExit {"
  awk '/\/\/ <legacy-exit-core>/ {on=1; next} /\/\/ <\/legacy-exit-core>/ {on=0} on' "$root/_ExitHalfRiskHalfRoll.Strategy.CS" | sed 's/private static/internal static/'
  echo "} }"; } > "$here/Generated/LegacyExit.cs"
