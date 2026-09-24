# Physics Player Controller — Quantum 3

A deterministic port of the [Physics Player Controller](https://github.com/zacharysnewman/physics-player-controller) for **Photon Quantum 3**: a
dynamic-body character controller driven by velocity layers, as an alternative to Quantum's
kinematic KCC addon.

> **Status: preview, Phase 0.** Only the package skeleton exists. See [PLAN.md](PLAN.md) for the
> roadmap and [PROGRESS.md](PROGRESS.md) for status.

## Requirements

- Unity 2021.3 LTS or newer
- Photon Quantum 3.0.x SDK imported into the project (targets 3.0.13)

## Install

Package Manager → **Add package from git URL**:

```
https://github.com/zacharysnewman/physics-player-controller.git?path=/Quantum~
```

This package is independent of the base `com.zacharysnewman.ppc` package; you don't need both.

After installing, run Quantum's code generation (Quantum menu → CodeGen), then add the package's
systems to your `SystemsConfig` after Quantum's core systems. From code, call
`PPCSystems.AddTo(systemsConfig)`.

## Layout

| Folder | Assembly | Contents |
|---|---|---|
| `Simulation/` | `Quantum.Simulation` (via `.asmref`) | Deterministic code: DSL (`.qtn`), systems, config assets |
| `View/` | `Quantum.Unity` (via `.asmref`) | Unity-side code: input polling, camera, animation |
| `Tests~/` | — | Headless .NET build and test harness (not imported by Unity) |

## Development

`Tests~/build.sh` generates, compiles and tests the simulation without Unity. See
[Tests~/README.md](Tests~/README.md).
