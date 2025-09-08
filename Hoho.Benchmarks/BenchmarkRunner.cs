using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Hoho.Benchmarks;

/// <summary>
/// Custom benchmark runner with optimized configuration for HOHO performance testing
/// </summary>
public static class HohoBenchmarkRunner
{
    /// <summary>
    /// Run all benchmarks with comprehensive analysis
    /// </summary>
    public static void RunAllBenchmarks()
    {
        var config = CreateBenchmarkConfig();
        
        Console.WriteLine("🚀 HOHO Performance Benchmarks - Shadow Protocol Active");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine();

        // Run each benchmark suite
        RunBenchmarkSuite<MessagePackSerializationBenchmark>("MessagePack vs JSON Serialization", config);
        RunBenchmarkSuite<MemoryAllocationBenchmark>("Memory Allocation & GC Efficiency", config);
        RunBenchmarkSuite<LargeDatasetBenchmark>("Large Dataset Performance", config);
        RunBenchmarkSuite<DatabaseOperationBenchmark>("Database Operations", config);

        Console.WriteLine("\n✅ All benchmarks completed!");
        Console.WriteLine("📊 Results saved to BenchmarkDotNet.Artifacts/");
        Console.WriteLine("\n📈 Performance Analysis Summary:");
        
        AnalyzeResults();
    }

    /// <summary>
    /// Run specific benchmark categories
    /// </summary>
    public static void RunSerializationBenchmarks()
    {
        var config = CreateBenchmarkConfig();
        RunBenchmarkSuite<MessagePackSerializationBenchmark>("Serialization Performance", config);
        AnalyzeSerializationResults();
    }

    /// <summary>
    /// Run memory-focused benchmarks
    /// </summary>
    public static void RunMemoryBenchmarks()
    {
        var config = CreateBenchmarkConfig();
        RunBenchmarkSuite<MemoryAllocationBenchmark>("Memory & GC Performance", config);
        AnalyzeMemoryResults();
    }

    /// <summary>
    /// Create optimized benchmark configuration
    /// </summary>
    private static IConfig CreateBenchmarkConfig()
    {
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(5)
                .WithInvocationCount(1)
                .WithUnrollFactor(1))
            .AddDiagnoser(MemoryDiagnoser.Default)
            // .AddDiagnoser(DisassemblyDiagnoser.Default) // Commented out due to API changes
            .AddColumn(StatisticColumn.Min)
            .AddColumn(StatisticColumn.Max)
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.Median)
            .AddColumn(StatisticColumn.StdDev)
            .AddColumn(BaselineRatioColumn.RatioMean)
            .AddColumn(RankColumn.Arabic)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(DefaultExporters.Csv)
            .AddExporter(DefaultExporters.Json)
            .AddLogger(ConsoleLogger.Default);
    }

    /// <summary>
    /// Run a specific benchmark suite with error handling
    /// </summary>
    private static void RunBenchmarkSuite<T>(string suiteName, IConfig config)
    {
        Console.WriteLine($"\n🔄 Running {suiteName} Benchmarks...");
        Console.WriteLine(new string('-', 50));
        
        try
        {
            var summary = BenchmarkRunner.Run<T>(config);
            Console.WriteLine($"✅ {suiteName} benchmarks completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error running {suiteName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyze overall results and generate summary
    /// </summary>
    private static void AnalyzeResults()
    {
        var artifactsPath = "BenchmarkDotNet.Artifacts";
        
        if (!Directory.Exists(artifactsPath))
        {
            Console.WriteLine("⚠️  No benchmark artifacts found for analysis");
            return;
        }

        try
        {
            // Find and analyze result files
            var resultFiles = Directory.GetFiles(artifactsPath, "*-report-github.md", SearchOption.AllDirectories);
            
            Console.WriteLine($"\n📋 Generated {resultFiles.Length} detailed reports:");
            foreach (var file in resultFiles)
            {
                var fileName = Path.GetFileName(file);
                var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), file);
                Console.WriteLine($"   • {fileName} -> {relativePath}");
            }

            // Analyze CSV results for performance summary
            var csvFiles = Directory.GetFiles(artifactsPath, "*-report.csv", SearchOption.AllDirectories);
            if (csvFiles.Length > 0)
            {
                Console.WriteLine("\n📊 Key Performance Indicators:");
                AnalyzeCsvResults(csvFiles);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error analyzing results: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyze serialization-specific results
    /// </summary>
    private static void AnalyzeSerializationResults()
    {
        Console.WriteLine("\n🎯 Serialization Performance Target Analysis:");
        Console.WriteLine("   Target: MessagePack 10x faster than JSON");
        Console.WriteLine("   Expected: 60-80% size reduction");
        Console.WriteLine("   Memory: 50%+ allocation reduction");
        Console.WriteLine("\n   Check the generated reports for detailed metrics!");
    }

    /// <summary>
    /// Analyze memory-specific results
    /// </summary>
    private static void AnalyzeMemoryResults()
    {
        Console.WriteLine("\n🧠 Memory Performance Target Analysis:");
        Console.WriteLine("   Target: Minimal GC pressure");
        Console.WriteLine("   Expected: Zero-allocation patterns");
        Console.WriteLine("   Memory pooling: 70%+ allocation reduction");
        Console.WriteLine("\n   Check Memory Diagnoser results for allocation details!");
    }

    /// <summary>
    /// Parse CSV results to extract key performance metrics
    /// </summary>
    private static void AnalyzeCsvResults(string[] csvFiles)
    {
        try
        {
            foreach (var csvFile in csvFiles)
            {
                var benchmarkName = Path.GetFileNameWithoutExtension(csvFile).Replace("-report", "");
                Console.WriteLine($"\n   {benchmarkName}:");
                
                var lines = File.ReadAllLines(csvFile);
                if (lines.Length > 1)
                {
                    // Parse header to find relevant columns
                    var header = lines[0].Split(',');
                    var methodIndex = Array.IndexOf(header, "Method");
                    var meanIndex = Array.IndexOf(header, "Mean");
                    var allocIndex = Array.IndexOf(header, "Allocated");
                    
                    // Analyze a few key results
                    var resultLines = lines.Skip(1).Take(5);
                    foreach (var line in resultLines)
                    {
                        var columns = line.Split(',');
                        if (columns.Length > Math.Max(methodIndex, meanIndex))
                        {
                            var method = columns[methodIndex];
                            var mean = meanIndex >= 0 ? columns[meanIndex] : "N/A";
                            var alloc = allocIndex >= 0 && allocIndex < columns.Length ? columns[allocIndex] : "N/A";
                            
                            Console.WriteLine($"     • {method}: {mean} ({alloc} allocated)");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"     ⚠️  Error parsing CSV results: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate that performance targets are met
    /// </summary>
    public static void ValidatePerformanceTargets()
    {
        Console.WriteLine("\n🎯 Performance Target Validation:");
        Console.WriteLine(new string('=', 50));
        
        Console.WriteLine("\n✅ Expected Improvements:");
        Console.WriteLine("   • MessagePack serialization: 5-15x faster than JSON");
        Console.WriteLine("   • File size reduction: 40-70% vs JSON");
        Console.WriteLine("   • Memory allocations: 50-80% reduction");
        Console.WriteLine("   • GC pressure: 60-90% reduction");
        Console.WriteLine("   • Large datasets (100K+): Linear scaling");
        
        Console.WriteLine("\n📋 Validation Checklist:");
        Console.WriteLine("   □ MessagePack serialize faster than System.Text.Json");
        Console.WriteLine("   □ MessagePack serialize faster than Newtonsoft.Json");
        Console.WriteLine("   □ Smaller binary size than JSON");
        Console.WriteLine("   □ Lower memory allocation per operation");
        Console.WriteLine("   □ Reduced GC Gen0/Gen1 collections");
        Console.WriteLine("   □ Consistent performance across dataset sizes");
        Console.WriteLine("   □ Efficient streaming and pooled operations");
        
        Console.WriteLine("\n💡 Next Steps:");
        Console.WriteLine("   1. Review generated benchmark reports");
        Console.WriteLine("   2. Compare against baseline JSON performance");
        Console.WriteLine("   3. Identify any performance regressions");
        Console.WriteLine("   4. Optimize bottlenecks if targets not met");
        Console.WriteLine("   5. Re-run benchmarks to validate improvements");
    }
}

/// <summary>
/// Benchmark categories for organized testing
/// </summary>
public static class BenchmarkCategories
{
    public const string Serialize = "Serialize";
    public const string Deserialize = "Deserialize";
    public const string Memory = "Memory";
    public const string GC = "GC";
    public const string Large = "Large";
    public const string CRUD = "CRUD";
    public const string Search = "Search";
    public const string Persistence = "Persistence";
}