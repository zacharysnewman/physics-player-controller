# Headless tests for the Quantum port

Generates, compiles and **runs** the package's deterministic simulation code **without Unity**,
using the .NET SDK and the non-Unity Quantum libraries that ship inside every Quantum 3 SDK. Unity
ignores this folder (the trailing `~`), so none of it reaches package users.

```bash
./build.sh                 # Debug: build + tests
./build.sh Release
SKIP_TESTS=1 ./build.sh    # build only
```

A successful run ends with `Passed: N` and no failures. Output goes to `.build/` (git-ignored).

## What it does

0. Checks every file Unity imports from the package has a `.meta` (`Tools/check-metas.sh`; run it
   with `--fix` to create missing ones). Git-installed packages are read-only, so Unity ignores
   files without one.
1. Unpacks `Editor/Dotnet/Quantum.Dotnet.<Config>.zip` from the SDK. These are the engine
   libraries Quantum's own "Export Dotnet Project" feature uses.
2. Runs Quantum's `.qtn` code generator (`Tools/CodeGen`, a small wrapper around
   `Quantum.CodeGen.Qtn.dll`) on every `.qtn` under `Quantum~/Simulation/` plus `Fixtures/`.
3. Compiles the SDK core simulation sources, the generated code, `Quantum~/Simulation/**/*.cs` and
   `Fixtures/**/*.cs` into `Quantum.Simulation.dll`, using C# 9 to match Unity.
4. Compiles the Playground sample's simulation code against the package, the way a user's project
   would (with its own `input` instead of the fixtures).
5. Runs the xUnit tests in `Tests/` against the assembly from step 3. They start real Quantum sessions.

The harness plays "the game" with the Playground sample's `input` and input bridge (compiled
alongside), plus `Fixtures/`: harness-only systems (bootstrap, kicks, movers, probes) and game-level
settings (`#pragma max_players 16`). None of it ships.

## Test suites

| File | Covers |
|---|---|
| `HarnessTests` | The harness itself: stepping, input delivery, determinism and its control |
| `PackageTests` | Registration, `PPCSystemGroup` equivalence, missing-config safety |
| `PhysicsApiTests` | The Quantum physics API the port relies on |
| `Phase1SkeletonTests` … `Phase6ClimbTests` | Each phase's behaviour (see `../PLAN.md`) |
| `Phase8HardeningTests` | Four-player full-feature determinism; 16-character cost |
| `GoldenTraceTests` | Behaviour lock: the four-player scenario matches `Golden/scenario-trace.txt` within 1 mm |
| `ConfigPresetTests` | Recommended defaults, the Unity parity preset, crouch ignoring run |

The behaviour tests use the **Unity parity** preset (`PPCTestWorld.DefaultAssets`), since they were
written against the Unity package's numbers. When a change is *meant* to alter behaviour, regenerate
the golden trace with `PPC_UPDATE_GOLDEN=1` and say why in the commit.

`Tools/cross-config-check.sh` runs the four-player scenario on the Debug and the Release Quantum
libraries and checks the per-tick checksums match.

The harness fixture sets `#pragma max_players 16` for the cost test. That's a game-level setting;
the package doesn't set it.

## Writing tests

`HeadlessSession` runs a real `SessionRunner` in Local mode with the asset database built in code.
For controller tests, `PPCTest.Session(...)` wraps it with the default config, the controller
systems and the kick/mover fixtures (see `Phase*Tests.cs` for examples).

```csharp
[Collection("Quantum")]   // sessions share static state, so they must not run in parallel
public unsafe class MyTests {
  [Fact]
  public void Character_Walks_Forward() {
    EntityRef e = default;
    using var s = new HeadlessSession(
      setup: f => { e = f.Create(); /* Set components... */ },          // runs on the first frame
      input: (tick, player) => new Quantum.Input { Move = FPVector2.Up },  // scripted input
      configureSystems: PPCSystems.AddTo);                                 // systems under test

    s.Step(60);                                   // exactly 60 ticks (1 s at 60 Hz)
    var t = s.Frame.Get<Transform3D>(e);          // read state from the verified frame
    // s.Checksums holds one frame checksum per tick; run twice and compare for determinism
  }
}
```

Core systems added automatically: `CullingSystem3D`, `PhysicsSystem3D`, `EntityPrototypeSystem`,
`PlayerConnectedSystem`, then `HarnessBootstrapSystem`, then whatever `configureSystems` adds.
Physics settings match Unity's `QuantumDefaultConfigs` defaults (gravity −10, 32 layers all
colliding, default material).

Things a headless session needs that Unity normally provides, for reference:
- `SimulationConfig.Entities` capacities, `Physics.Layers`/`LayerMatrix`, a default `PhysicsMaterial`
- `QuantumJsonSerializer` as the asset serializer (depends on Newtonsoft.Json)
- `FPLut` tables, generated on first run with `FPLut.GenerateTables` into the test output folder
- `QuantumGame.AddPlayer` per player slot. Quantum 3 doesn't poll input for players that haven't been added.

## Requirements

In Claude Code cloud sessions, `.claude/hooks/session-start.sh` handles both requirements below
automatically. It needs `quantum-sdk-libs` attached to the session or environment so it can clone it.

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

- It doesn't run Unity-side code (`View/`), import prefabs, scenes or assets, or check how things
  look and feel. That's what the Unity checkpoint in each phase of `../PLAN.md` is for.
- Scenes are built in code (entities and a code-built `Map`), not baked from Unity.
- Determinism is checked within one machine and runtime. Cross-platform determinism is Quantum's
  job; the tests catch non-determinism in *our* code (e.g. iteration-order bugs or float leaks).
