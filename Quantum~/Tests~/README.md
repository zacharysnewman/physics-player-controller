# Headless tests for the Quantum port

Generates and compiles the package's deterministic simulation code **without Unity**, using the
.NET SDK and the non-Unity Quantum libraries that ship inside every Quantum 3 SDK. Unity ignores
this folder (the trailing `~`), so none of it reaches package users.

```bash
./build.sh            # Debug
./build.sh Release
```

A successful run prints `OK: .../Quantum.Simulation.dll`. Output goes to `.build/` (git-ignored).

## What it does

1. Unpacks `Editor/Dotnet/Quantum.Dotnet.<Config>.zip` from the SDK. These are the engine
   libraries Quantum's own "Export Dotnet Project" feature uses.
2. Runs Quantum's `.qtn` code generator (`Tools/CodeGen`, a small wrapper around
   `Quantum.CodeGen.Qtn.dll`) on every `.qtn` under `Quantum~/Simulation/` plus `Fixtures/`.
3. Compiles the SDK core simulation sources, the generated code, `Quantum~/Simulation/**/*.cs` and
   `Fixtures/**/*.cs` into `Quantum.Simulation.dll`, using C# 9 to match Unity.

`Fixtures/` holds harness-only code (a smoke component and system) so the pipeline is checked even
before the package has real simulation code. It never ships.

## Requirements

### .NET 8 SDK

On Ubuntu 24.04 (including Claude Code cloud sessions), the Ubuntu archive has it:

```bash
sudo apt-get install -y dotnet-sdk-8.0
```

Anywhere else, use the installer from https://dotnet.microsoft.com. NuGet access is needed on the
first build (for `FSharp.Core`, which the code generator requires).

### The Quantum SDK files

Photon's libraries are **not** in this repo (license). The harness needs a folder laid out like a
Unity project's `Assets/Photon/Quantum`. Any of these work:

| Source | How |
|---|---|
| **`quantum-sdk-libs`** (default) | Clone the private `zacharysnewman/quantum-sdk-libs` repo **next to** this repo. `build.sh` uses `../quantum-sdk-libs/3.0.0` automatically. |
| A Unity project with Quantum 3 | `QUANTUM_SDK_DIR=/path/to/Project/Assets/Photon/Quantum ./build.sh` |
| A different version in `quantum-sdk-libs` | `QUANTUM_SDK_VERSION=3.0.13 ./build.sh` |

Only these SDK files are read:

```
Editor/Dotnet/Quantum.Dotnet.{Debug,Release}.zip
Editor/Assemblies/Quantum.CodeGen.Qtn.dll, System.CodeDom.dll
Simulation/*.cs
build_info.txt   (optional, printed for reference)
```

## SDK version

The package targets **Quantum 3.0.13** (latest stable). The harness currently builds against
**3.0.0 Stable, Build 1548**, the version available in `quantum-sdk-libs`. Differences between
3.0.x patch releases should be small, but the real check is still the Unity checkpoint in each
phase of `../PLAN.md`. Add a `3.0.13/` folder to `quantum-sdk-libs` and switch the default
version in `build.sh` when possible.

## Limits

- It checks that code **generates and compiles**. It doesn't run Unity-side code (`View/`) or
  import prefabs, scenes or assets.
- Running frames headlessly (behaviour and determinism tests) is planned. See Phase 0 in
  `../PLAN.md`.
