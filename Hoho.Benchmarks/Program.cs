using Hoho.Benchmarks;

// Parse command line arguments
var commandArgs = Environment.GetCommandLineArgs();
var command = commandArgs.Length > 1 ? commandArgs[1].ToLower() : "all";

switch (command)
{
    case "all":
        HohoBenchmarkRunner.RunAllBenchmarks();
        break;
    
    case "serialization":
    case "msgpack":
        HohoBenchmarkRunner.RunSerializationBenchmarks();
        break;
    
    case "memory":
    case "gc":
        HohoBenchmarkRunner.RunMemoryBenchmarks();
        break;
    
    case "validate":
    case "targets":
        HohoBenchmarkRunner.ValidatePerformanceTargets();
        break;
    
    case "help":
    case "-h":
    case "--help":
        ShowHelp();
        break;
    
    default:
        Console.WriteLine($"❌ Unknown command: {command}");
        ShowHelp();
        Environment.Exit(1);
        break;
}

static void ShowHelp()
{
    Console.WriteLine("🚀 HOHO Performance Benchmarks - Shadow Protocol");
    Console.WriteLine(new string('=', 50));
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run [command]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  all            Run all benchmark suites (default)");
    Console.WriteLine("  serialization  Run only MessagePack vs JSON benchmarks");
    Console.WriteLine("  memory         Run only memory & GC benchmarks");
    Console.WriteLine("  validate       Show performance targets and validation checklist");
    Console.WriteLine("  help           Show this help message");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run                    # Run all benchmarks");
    Console.WriteLine("  dotnet run serialization     # Test MessagePack performance");
    Console.WriteLine("  dotnet run memory            # Test memory efficiency");
    Console.WriteLine("  dotnet run validate          # Check performance targets");
    Console.WriteLine();
    Console.WriteLine("📊 Results are saved to BenchmarkDotNet.Artifacts/");
}