#!/usr/bin/env bash
# Headless build of the Quantum port: runs Quantum's .qtn CodeGen, compiles the deterministic
# simulation against the non-Unity Quantum libraries, then runs the headless tests. No Unity required.
#
# Usage: ./build.sh [Debug|Release]     (SKIP_TESTS=1 to only build)
#
# QUANTUM_SDK_DIR  folder laid out like a Unity project's Assets/Photon/Quantum.
#                  Default: ../quantum-sdk-libs/<QUANTUM_SDK_VERSION> next to this repo.
# QUANTUM_SDK_VERSION  version folder inside quantum-sdk-libs. Default: 3.0.0
set -euo pipefail

CONFIG="${1:-Debug}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_ROOT="$(dirname "$HERE")"
REPO_ROOT="$(dirname "$PACKAGE_ROOT")"
SDK_VERSION="${QUANTUM_SDK_VERSION:-3.0.0}"
SDK_DIR="${QUANTUM_SDK_DIR:-$REPO_ROOT/../quantum-sdk-libs/$SDK_VERSION}"
BUILD_DIR="$HERE/.build"
LIB_DIR="$BUILD_DIR/lib/$CONFIG"
GENERATED_DIR="$BUILD_DIR/Generated"

fail() { echo "error: $*" >&2; exit 1; }

command -v dotnet >/dev/null || fail "dotnet not found. Install the .NET 8 SDK (see Tests~/README.md)."
[ -d "$SDK_DIR" ] || fail "Quantum SDK not found at '$SDK_DIR'. Set QUANTUM_SDK_DIR (see Tests~/README.md)."
SDK_DIR="$(cd "$SDK_DIR" && pwd)"
ZIP="$SDK_DIR/Editor/Dotnet/Quantum.Dotnet.$CONFIG.zip"
[ -f "$ZIP" ] || fail "missing $ZIP"
[ -f "$SDK_DIR/Editor/Assemblies/Quantum.CodeGen.Qtn.dll" ] || fail "missing Quantum.CodeGen.Qtn.dll in $SDK_DIR/Editor/Assemblies"

echo "Quantum SDK: $SDK_DIR ($(head -n1 "$SDK_DIR/build_info.txt" 2>/dev/null || echo 'unknown build'))"

# 0. Every file Unity imports from the package needs a committed .meta (git packages are read-only).
"$HERE/Tools/check-metas.sh" || fail "fix .meta files (Tools/check-metas.sh --fix creates missing ones)"

# 1. Unpack the non-Unity Quantum libraries.
rm -rf "$LIB_DIR" && mkdir -p "$LIB_DIR"
unzip -qo "$ZIP" -d "$LIB_DIR"

MSBUILD_PROPS=(-p:QuantumSdkDir="$SDK_DIR" -p:QuantumLibDir="$LIB_DIR" -p:GeneratedDir="$GENERATED_DIR")

# 2. CodeGen: every .qtn in the package, the Playground sample (the harness's "game": input + bridge)
#    and the harness fixtures.
SAMPLE_SIM="$PACKAGE_ROOT/Samples~/Playground/Simulation"
mapfile -t QTN_FILES < <(find "$PACKAGE_ROOT/Simulation" "$SAMPLE_SIM" "$HERE/Fixtures" -name '*.qtn' 2>/dev/null | sort)
[ "${#QTN_FILES[@]}" -gt 0 ] || fail "no .qtn files found"
echo "CodeGen..."
dotnet build "$HERE/Tools/CodeGen/CodeGen.csproj" -nologo -v q -c Release "${MSBUILD_PROPS[@]}" -o "$BUILD_DIR/codegen" >/dev/null
dotnet "$BUILD_DIR/codegen/CodeGen.dll" "$GENERATED_DIR" "${QTN_FILES[@]}"

# 3. Compile the simulation.
echo "Compiling simulation ($CONFIG)..."
SIM_DIR="$BUILD_DIR/bin/$CONFIG"
dotnet build "$HERE/Simulation/PPC.Simulation.csproj" -nologo -v q -c "$CONFIG" "${MSBUILD_PROPS[@]}" \
  -p:GameSourceDir="$SAMPLE_SIM" -p:FixturesDir="$HERE/Fixtures" -o "$SIM_DIR"
echo "OK: $SIM_DIR/Quantum.Simulation.dll"

# 4. The Playground sample's simulation code must also compile on its own against the package, as in a
#    user's project (no harness fixtures).
if [ -d "$SAMPLE_SIM" ]; then
  echo "Compiling Playground sample simulation..."
  mapfile -t SAMPLE_QTN < <(find "$PACKAGE_ROOT/Simulation" "$SAMPLE_SIM" -name '*.qtn' | sort)
  dotnet "$BUILD_DIR/codegen/CodeGen.dll" "$BUILD_DIR/SampleGenerated" "${SAMPLE_QTN[@]}" >/dev/null
  dotnet build "$HERE/Simulation/PPC.Simulation.csproj" -nologo -v q -c "$CONFIG" \
    -p:QuantumSdkDir="$SDK_DIR" -p:QuantumLibDir="$LIB_DIR" -p:GeneratedDir="$BUILD_DIR/SampleGenerated" \
    -p:GameSourceDir="$SAMPLE_SIM" -p:BaseIntermediateOutputPath="$BUILD_DIR/sample-obj/" \
    -o "$BUILD_DIR/sample-bin/$CONFIG" >/dev/null || fail "Playground sample simulation doesn't compile"
  echo "OK: Playground sample simulation compiles"
fi

# 5. Headless tests (real Quantum session loop). Skip with SKIP_TESTS=1.
if [ "${SKIP_TESTS:-0}" != "1" ]; then
  echo "Running headless tests ($CONFIG)..."
  dotnet test "$HERE/Tests/PPC.Tests.csproj" -nologo -c "$CONFIG" "${MSBUILD_PROPS[@]}" -p:SimulationDir="$SIM_DIR" \
    --results-directory "$BUILD_DIR/test-results" --logger "console;verbosity=normal"
fi
