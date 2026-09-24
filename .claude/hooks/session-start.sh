#!/bin/bash
# Sets up the headless Quantum test harness (Quantum~/Tests~) in Claude Code cloud sessions:
# .NET 8 SDK + the private quantum-sdk-libs repo cloned next to this repo.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
SDK_LIBS_DIR="$(dirname "$PROJECT_DIR")/quantum-sdk-libs"

# .NET 8 SDK from the Ubuntu archive (Microsoft's download hosts are blocked by the network policy).
if ! command -v dotnet >/dev/null 2>&1; then
  echo "Installing .NET 8 SDK..."
  # Some third-party apt sources are blocked; the Ubuntu archive still updates.
  apt-get update -qq >/dev/null 2>&1 || true
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-8.0 >/dev/null
fi
echo "dotnet $(dotnet --version)"

# Quantum SDK files (private repo; must be attached to the session/environment).
if [ -d "$SDK_LIBS_DIR/.git" ]; then
  git -C "$SDK_LIBS_DIR" pull --ff-only -q || echo "warning: could not update $SDK_LIBS_DIR" >&2
elif git clone --depth 1 -q https://github.com/zacharysnewman/quantum-sdk-libs "$SDK_LIBS_DIR"; then
  echo "Cloned quantum-sdk-libs to $SDK_LIBS_DIR"
else
  echo "warning: could not clone zacharysnewman/quantum-sdk-libs; add it to this session's repositories." \
       "Quantum~/Tests~/build.sh needs it (or QUANTUM_SDK_DIR)." >&2
fi

# Warm the NuGet cache for the CodeGen wrapper (FSharp.Core).
dotnet restore "$PROJECT_DIR/Quantum~/Tests~/Tools/CodeGen/CodeGen.csproj" -v q >/dev/null || \
  echo "warning: NuGet restore failed; build.sh will retry" >&2
