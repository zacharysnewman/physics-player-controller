#!/usr/bin/env bash
# Runs the full determinism scenario with the Debug and the Release Quantum libraries and checks both
# produce the same per-tick checksums.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
for config in Debug Release; do
  SKIP_TESTS=1 "$HERE/build.sh" "$config" >/dev/null
  dotnet test "$HERE/Tests/PPC.Tests.csproj" -nologo -c "$config" \
    -p:QuantumSdkDir="${QUANTUM_SDK_DIR:-$(cd "$HERE/../.." && pwd)/../quantum-sdk-libs/${QUANTUM_SDK_VERSION:-3.0.0}}" \
    -p:QuantumLibDir="$HERE/.build/lib/$config" -p:GeneratedDir="$HERE/.build/Generated" \
    -p:SimulationDir="$HERE/.build/bin/$config" \
    --filter "FullyQualifiedName~Full_Scenario_With_Four_Players_Is_Deterministic" >/dev/null
done
if cmp -s "$HERE/.build/scenario-checksums-Debug.txt" "$HERE/.build/scenario-checksums-Release.txt"; then
  echo "Cross-config: OK ($(wc -l < "$HERE/.build/scenario-checksums-Debug.txt") ticks, Debug == Release)"
else
  echo "Cross-config: MISMATCH between Debug and Release checksums" >&2
  exit 1
fi
