# Stage a semantic C# 7.3 compile of every sandbox strategy against main's engines and MC stubs.
# Copies sources into Generated/ so no csproj Compile Include points at sandbox/.
import os, pathlib, re
here = pathlib.Path(__file__).resolve().parent
root = here.parent.parent
gen = here / "Generated"
src = gen / "src"
if gen.exists():
    for p in sorted(gen.rglob("*"), reverse=True):
        if p.is_file():
            p.unlink()
src.mkdir(parents=True, exist_ok=True)

custom = (root / "_CustomSignal.Strategy.CS").read_text()
custom = custom.replace("using PowerLanguage.TradeManager;", "using TradeManager = McTradeManager;")
(src / "CustomSignal.cs").write_text(custom)

functions = [
    "_PortMM.Function.CS", "_SignalJournal.Function.CS", "_MtfMath.Function.CS", "_BarRing.Function.CS",
    "_LevelRef.Function.CS", "_LevelStudy.Function.CS", "_Holidays.Function.CS", "_CmeHolidays.Function.CS",
    "_CmeSessionClock.Function.CS", "_Gmm.Function.CS", "_ProfileMath.Function.CS", "_OrderFlow.Function.CS",
    "_Rng.Function.CS", "_ProfileStats.Function.CS", "_Gate0.Function.CS", "_NyseHolidays.Function.CS",
    "_InstrumentProfile.Function.CS", "_RollDetector.Function.CS", "_PriceActionEngine.Function.CS",
    "_DvpEngine.Function.CS", "_DailyContext.Function.CS", "_RegimeEngine.Function.CS", "_EventCalendar.Function.CS",
    "_AvwapManager.Function.CS", "_TodNormaliser.Function.CS", "_FvgDetector.Function.CS", "_TickStream.Function.CS",
    "_XmktLeg.Function.CS", "_BarLadder.Function.CS", "_VpHistogram.Function.CS", "_VpVwapEngine.Function.CS", "_RangeProfile.Function.CS",
    "_WeeklyProfiles.Function.CS", "_VpConfluenceEngine.Function.CS",
]
bases = ["_VolumeProfileBase.Strategy.CS", "_VolumeProfileFuturesBase.Strategy.CS", "_GenericExit.Strategy.CS"]
names = []
for name in functions + bases:
    text = (root / name).read_text()
    (src / name).write_text(text)
    names.append("src/" + name)
for path in sorted((root / "sandbox").glob("_*.Strategy.CS")):
    (src / path.name).write_text(path.read_text())
    names.append("src/" + path.name)
for path in sorted((root / "sandbox").glob("_*.Function.CS")):
    (src / path.name).write_text(path.read_text())
    names.append("src/" + path.name)
names.insert(0, "src/CustomSignal.cs")

includes = "\n".join('    <Compile Include="%s" />' % n for n in names)
proj = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0649;CS0169;CS0414</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
    <Compile Include="..\\McStubs.cs" Link="McStubs.cs" />
%s
  </ItemGroup>
</Project>
""" % includes
(gen / "SandboxLib.csproj").write_text(proj)
print("staged", len(names), "sources")
