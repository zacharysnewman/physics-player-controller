using System;
using System.Collections.Generic;
using System.IO;
using Quantum.CodeGen;

// Usage: CodeGen <outputDir> <file.qtn>...
// Simulation files go to <outputDir>/, Unity-only files (prototype wrappers) to <outputDir>/Unity/.
public static class Program {
  public static int Main(string[] args) {
    if (args.Length < 2) {
      Console.Error.WriteLine("Usage: CodeGen <outputDir> <file.qtn>...");
      return 2;
    }

    var outputDir = args[0];
    var qtnFiles = args[1..];
    var warnings = 0;

    IEnumerable<GeneratorOutputFile> outputs;
    try {
      outputs = Generator.Generate(qtnFiles, new GeneratorOptions(), w => {
        warnings++;
        Console.Error.WriteLine($"warning: {w.Path}({w.Position}): {w.Message}");
      });
    } catch (Exception e) {
      Console.Error.WriteLine($"error: {e.GetType().Name}: {e.Message}");
      return 1;
    }

    var unityDir = Path.Combine(outputDir, "Unity");
    if (Directory.Exists(outputDir)) {
      Directory.Delete(outputDir, recursive: true);
    }
    Directory.CreateDirectory(unityDir);

    foreach (var file in outputs) {
      var dir = IsUnitySpecific(file.Kind) ? unityDir : outputDir;
      File.WriteAllText(Path.Combine(dir, Path.GetFileName(file.Name)), file.Contents);
      Console.WriteLine($"  {file.Kind,-24} {file.Name}");
    }

    Console.WriteLine($"CodeGen: {qtnFiles.Length} qtn file(s), {warnings} warning(s).");
    return 0;
  }

  static bool IsUnitySpecific(GeneratorOutputFileKind kind) {
    switch (kind) {
      case GeneratorOutputFileKind.UnityPrototypeAdapters:
      case GeneratorOutputFileKind.UnityPrototypeWrapper:
      case GeneratorOutputFileKind.UnityLegacyAssetBase:
        return true;
      default:
        return false;
    }
  }
}
