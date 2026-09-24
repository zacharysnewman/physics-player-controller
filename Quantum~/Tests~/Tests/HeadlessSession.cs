namespace PPC.Tests {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using Photon.Deterministic;
  using Quantum;

  /// <summary>
  /// Runs a real Quantum session (SessionRunner, Local mode) without Unity: builds the asset
  /// database in code, feeds scripted input and steps exactly one tick at a time.
  /// </summary>
  public sealed class HeadlessSession : IDisposable {
    public const int UpdateFps = 60;

    static readonly object InitLock = new object();
    static bool _lutReady;

    readonly ResourceManagerStatic _resources;
    readonly Func<int, int, Quantum.Input> _input;
    readonly List<ulong> _checksums = new List<ulong>();
    int _firstInputFrame = -1;

    public SessionRunner Runner { get; }
    /// <summary>Subscribe to simulation events, e.g. <c>s.Events.Subscribe(this, (EventPPCJumped e) => ...)</c>.</summary>
    public EventDispatcher Events { get; }
    public QuantumGame Game => (QuantumGame)Runner.DeterministicGame;
    public Frame Frame => (Frame)Runner.Session.FrameVerified;
    public IReadOnlyList<ulong> Checksums => _checksums;

    /// <param name="setup">Builds the scene on the first frame (see <see cref="HarnessBootstrapSystem"/>).</param>
    /// <param name="input">Scripted input: (tick, player) → input. Tick 0 is the first simulated tick.</param>
    /// <param name="configureSystems">Adds the systems under test; core systems are added first.</param>
    public HeadlessSession(Action<Frame> setup, Func<int, int, Quantum.Input> input = null,
                           Action<SystemsConfig> configureSystems = null, int playerCount = 1, int seed = 0,
                           params AssetObject[] extraAssets) {
      EnsureLut();
      _input = input;

      // Mirrors Unity's QuantumDefaultConfigs (constructor defaults already match it).
      var physicsMaterial = AssetObject.Create<PhysicsMaterial>();
      Identify(physicsMaterial, 4, "Harness/PhysicsMaterial");

      var simulationConfig = AssetObject.Create<SimulationConfig>();
      simulationConfig.Entities = new Quantum.Core.FrameBase.EntitiesConfig {
        DefaultEntityCapacity = 1024,
        DefaultComponentBlockCapacity = 128,
        ComponentTypeConfiguration = Array.Empty<Quantum.Core.FrameBase.EntitiesConfig.ComponentBufferConfig>(),
      };
      simulationConfig.Physics = new PhysicsCommon.Config {
        DefaultPhysicsMaterial = new AssetRef<PhysicsMaterial>(physicsMaterial.Guid),
        // Unity normally imports these from its physics settings: 32 layers, all colliding.
        Layers = Enumerable.Range(0, 32).Select(i => i == 0 ? "Default" : $"Layer{i}").ToArray(),
        LayerMatrix = Enumerable.Repeat(~0, 32).ToArray(),
      };
      simulationConfig.Navigation = new Navigation.Config();
      simulationConfig.ThreadCount = 1;
      Identify(simulationConfig, 1, "Harness/SimulationConfig");

      var systemsConfig = AssetObject.Create<SystemsConfig>();
      systemsConfig.AddSystem<Quantum.Core.CullingSystem3D>();
      systemsConfig.AddSystem<Quantum.Core.PhysicsSystem3D>();
      systemsConfig.AddSystem<Quantum.Core.EntityPrototypeSystem>();
      systemsConfig.AddSystem<Quantum.Core.PlayerConnectedSystem>();
      systemsConfig.AddSystem<HarnessBootstrapSystem>();
      configureSystems?.Invoke(systemsConfig);
      Identify(systemsConfig, 2, "Harness/SystemsConfig");

      var map = AssetObject.Create<Map>();
      Identify(map, 3, "Harness/Map");

      var assets = new List<AssetObject> { simulationConfig, systemsConfig, map, physicsMaterial };
      assets.AddRange(extraAssets);
      _resources = new ResourceManagerStatic(assets.ToArray(), DotNetRunnerFactory.CreateNativeAllocator(), true);

      var callbacks = new CallbackDispatcher();
      callbacks.Subscribe(this, (CallbackPollInput c) => {
        // Scripts see ticks relative to the first polled frame (Quantum doesn't start at frame 0).
        if (_firstInputFrame < 0) _firstInputFrame = c.Frame;
        var i = _input != null ? _input(c.Frame - _firstInputFrame, c.PlayerSlot) : default;
        c.SetInput(i, DeterministicInputFlags.Repeatable);
      });

      HarnessBootstrapSystem.Setup = setup;
      Events = new EventDispatcher();

      Runner = SessionRunner.Start(new SessionRunner.Arguments {
        RunnerFactory = new DotNetRunnerFactory(),
        GameMode = DeterministicGameMode.Local,
        ResourceManager = _resources,
        AssetSerializer = new QuantumJsonSerializer(),
        CallbackDispatcher = callbacks,
        EventDispatcher = Events,
        GameFlags = QuantumGameFlags.DisableInterpolatableStates,
        PlayerCount = playerCount,
        SessionConfig = new DeterministicSessionConfig {
          PlayerCount = playerCount,
          UpdateFPS = UpdateFps,
          ChecksumInterval = 1,
        },
        RuntimeConfig = new RuntimeConfig {
          Seed = seed,
          Map = new AssetRef<Map>(map.Guid),
          SimulationConfig = new AssetRef<SimulationConfig>(simulationConfig.Guid),
          SystemsConfig = new AssetRef<SystemsConfig>(systemsConfig.Guid),
        },
      });

      // Local mode starts immediately; one zero-time service call brings it to Running.
      Runner.Service(0);
      if (Runner.Session == null) {
        throw new InvalidOperationException("Quantum session did not start");
      }

      // Quantum 3 only polls input for players that have been added explicitly.
      for (int slot = 0; slot < playerCount; slot++) {
        Game.AddPlayer(slot, new RuntimePlayer());
      }
    }

    /// <summary>Advances the verified frame by exactly <paramref name="ticks"/> ticks.</summary>
    public void Step(int ticks = 1) {
      for (int i = 0; i < ticks; i++) {
        int before = Runner.Session.FrameVerified?.Number ?? 0;
        int guard = 0;
        do {
          Runner.Service(1.0 / UpdateFps);
          if (++guard > 1000) {
            throw new TimeoutException($"Simulation did not advance past tick {before}");
          }
        } while ((Runner.Session.FrameVerified?.Number ?? 0) <= before);

        _checksums.Add(Frame.CalculateChecksum());
      }
    }

    public void Dispose() {
      Runner?.Shutdown();
      _resources?.Dispose();
      HarnessBootstrapSystem.Setup = null;
    }

    public static void Identify(AssetObject asset, long guid, string path) {
      asset.Identifier = new AssetObjectIdentifier { Guid = new AssetGuid(guid), Path = path };
    }

    static void EnsureLut() {
      lock (InitLock) {
        if (_lutReady) {
          return;
        }
        Log.InitForConsole();
        var dir = Path.Combine(AppContext.BaseDirectory, "LUT");
        if (!File.Exists(Path.Combine(dir, "FPSqrt.bytes"))) {
          Directory.CreateDirectory(dir);
          FPLut.GenerateTables(dir);
        }
        FPLut.Init(dir);
        _lutReady = true;
      }
    }
  }
}
