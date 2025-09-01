using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Bogus;
using Hoho.Decomp;
using MessagePack;

namespace Hoho.Benchmarks;

/// <summary>
/// Benchmarks large dataset handling performance to validate scalability
/// Tests realistic symbol mapping database sizes (10k - 100k+ mappings)
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[MarkdownExporter, HtmlExporter, CsvExporter]
public class LargeDatasetBenchmark
{
    private MessagePackSymbolMapping[] _dataset10K = null!;
    private MessagePackSymbolMapping[] _dataset50K = null!;
    private MessagePackSymbolMapping[] _dataset100K = null!;
    
    private SymbolMappingCollection _collection10K = null!;
    private SymbolMappingCollection _collection50K = null!;
    private SymbolMappingCollection _collection100K = null!;
    
    private byte[] _msgPack10K = null!;
    private byte[] _msgPack50K = null!;
    private byte[] _msgPack100K = null!;
    
    private string _json10K = null!;
    private string _json50K = null!;
    private string _json100K = null!;

    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("🔄 Generating large test datasets...");
        
        var faker = new Faker<MessagePackSymbolMapping>()
            .RuleFor(m => m.Original, f => GenerateRealisticObfuscatedName(f))
            .RuleFor(m => m.Mapped, f => GenerateRealisticMappedName(f))
            .RuleFor(m => m.Type, f => f.PickRandom<SymbolType>())
            .RuleFor(m => m.Context, f => GenerateRealisticContext(f))
            .RuleFor(m => m.Confidence, f => f.Random.Double(0.6, 1.0))
            .RuleFor(m => m.LastUpdated, f => f.Date.Recent(30))
            .RuleFor(m => m.UsageCount, f => f.Random.Int(1, 500))
            .RuleFor(m => m.References, f => f.Make(f.Random.Int(0, 8), () => f.System.FileName()));

        Console.WriteLine("  Generating 10K dataset...");
        _dataset10K = faker.Generate(10_000).ToArray();
        
        Console.WriteLine("  Generating 50K dataset...");
        _dataset50K = faker.Generate(50_000).ToArray();
        
        Console.WriteLine("  Generating 100K dataset...");
        _dataset100K = faker.Generate(100_000).ToArray();

        // Create collections
        _collection10K = CreateCollection(_dataset10K, "10K Dataset");
        _collection50K = CreateCollection(_dataset50K, "50K Dataset");
        _collection100K = CreateCollection(_dataset100K, "100K Dataset");

        // Pre-serialize for deserialization benchmarks
        Console.WriteLine("  Pre-serializing datasets...");
        _msgPack10K = MessagePackSerializer.Serialize(_collection10K);
        _msgPack50K = MessagePackSerializer.Serialize(_collection50K);
        _msgPack100K = MessagePackSerializer.Serialize(_collection100K);
        
        _json10K = JsonSerializer.Serialize(_collection10K);
        _json50K = JsonSerializer.Serialize(_collection50K);
        _json100K = JsonSerializer.Serialize(_collection100K);

        Console.WriteLine($"✅ Setup complete!");
        Console.WriteLine($"   10K MessagePack: {_msgPack10K.Length:N0} bytes");
        Console.WriteLine($"   50K MessagePack: {_msgPack50K.Length:N0} bytes");
        Console.WriteLine($"  100K MessagePack: {_msgPack100K.Length:N0} bytes");
    }

    private static SymbolMappingCollection CreateCollection(MessagePackSymbolMapping[] mappings, string description)
    {
        return new SymbolMappingCollection
        {
            Version = "1.0",
            Mappings = mappings.ToList(),
            Statistics = new DatabaseStatistics
            {
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                TotalMappings = mappings.Length,
                LastVersion = description
            }
        };
    }

    #region Realistic Data Generation

    private static string GenerateRealisticObfuscatedName(Faker faker)
    {
        return faker.Random.Int(1, 10) switch
        {
            1 => faker.Random.String2(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ"), // Single letter
            2 => faker.Random.String2(2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ"), // Two letters
            3 => faker.Random.String2(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") + faker.Random.Int(0, 999), // Letter + number
            4 => "$" + faker.Random.AlphaNumeric(2), // Dollar prefix
            5 => "_" + faker.Random.AlphaNumeric(3), // Underscore prefix
            _ => faker.Random.AlphaNumeric(faker.Random.Int(2, 8)) // Mixed
        };
    }

    private static string GenerateRealisticMappedName(Faker faker)
    {
        return faker.Random.Int(1, 8) switch
        {
            1 => faker.Hacker.Noun(),
            2 => faker.Name.FirstName().ToLower(),
            3 => faker.Hacker.Noun() + faker.Random.AlphaNumeric(2),
            4 => "get" + faker.Hacker.Noun(),
            5 => "set" + faker.Hacker.Noun(),
            6 => "handle" + faker.Hacker.Noun(),
            7 => faker.Hacker.Noun() + "Handler",
            _ => faker.Lorem.Word() + faker.Lorem.Word()
        };
    }

    private static string? GenerateRealisticContext(Faker faker)
    {
        return faker.Random.Int(1, 10) switch
        {
            1 => "global",
            2 => null,
            3 => faker.Hacker.Noun() + "Class",
            4 => faker.Hacker.Noun() + ".constructor",
            5 => faker.Hacker.Noun() + "." + faker.Hacker.Noun(),
            _ => faker.Hacker.Noun()
        };
    }

    #endregion

    #region 10K Dataset Benchmarks

    [Benchmark]
    [BenchmarkCategory("Serialize", "10K")]
    public byte[] MessagePack_Serialize_10K() => 
        MessagePackSerializer.Serialize(_collection10K, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Serialize", "10K")]
    public string Json_Serialize_10K() => 
        JsonSerializer.Serialize(_collection10K);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "10K")]
    public SymbolMappingCollection MessagePack_Deserialize_10K() => 
        MessagePackSerializer.Deserialize<SymbolMappingCollection>(_msgPack10K, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "10K")]
    public SymbolMappingCollection? Json_Deserialize_10K() => 
        JsonSerializer.Deserialize<SymbolMappingCollection>(_json10K);

    #endregion

    #region 50K Dataset Benchmarks

    [Benchmark]
    [BenchmarkCategory("Serialize", "50K")]
    public byte[] MessagePack_Serialize_50K() => 
        MessagePackSerializer.Serialize(_collection50K, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Serialize", "50K")]
    public string Json_Serialize_50K() => 
        JsonSerializer.Serialize(_collection50K);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "50K")]
    public SymbolMappingCollection MessagePack_Deserialize_50K() => 
        MessagePackSerializer.Deserialize<SymbolMappingCollection>(_msgPack50K, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "50K")]
    public SymbolMappingCollection? Json_Deserialize_50K() => 
        JsonSerializer.Deserialize<SymbolMappingCollection>(_json50K);

    #endregion

    #region 100K Dataset Benchmarks

    [Benchmark]
    [BenchmarkCategory("Serialize", "100K")]
    public byte[] MessagePack_Serialize_100K() => 
        MessagePackSerializer.Serialize(_collection100K, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Serialize", "100K")]
    public string Json_Serialize_100K() => 
        JsonSerializer.Serialize(_collection100K);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "100K")]
    public SymbolMappingCollection MessagePack_Deserialize_100K() => 
        MessagePackSerializer.Deserialize<SymbolMappingCollection>(_msgPack100K, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "100K")]
    public SymbolMappingCollection? Json_Deserialize_100K() => 
        JsonSerializer.Deserialize<SymbolMappingCollection>(_json100K);

    #endregion

    #region Scalability Analysis

    [Benchmark]
    [BenchmarkCategory("Scalability", "Linear")]
    public (long time10K, long time50K, long time100K) Scalability_MessagePack_Serialize()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        var _ = MessagePackSerializer.Serialize(_collection10K, MessagePackSerializerOptions.Standard);
        var time10K = sw.ElapsedTicks;
        
        sw.Restart();
        var __ = MessagePackSerializer.Serialize(_collection50K, MessagePackSerializerOptions.Standard);
        var time50K = sw.ElapsedTicks;
        
        sw.Restart();
        var ___ = MessagePackSerializer.Serialize(_collection100K, MessagePackSerializerOptions.Standard);
        var time100K = sw.ElapsedTicks;
        
        return (time10K, time50K, time100K);
    }

    [Benchmark]
    [BenchmarkCategory("Scalability", "Linear")]
    public (long time10K, long time50K, long time100K) Scalability_Json_Serialize()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        var _ = JsonSerializer.Serialize(_collection10K);
        var time10K = sw.ElapsedTicks;
        
        sw.Restart();
        var __ = JsonSerializer.Serialize(_collection50K);
        var time50K = sw.ElapsedTicks;
        
        sw.Restart();
        var ___ = JsonSerializer.Serialize(_collection100K);
        var time100K = sw.ElapsedTicks;
        
        return (time10K, time50K, time100K);
    }

    #endregion

    #region File I/O Simulation

    [Benchmark]
    [BenchmarkCategory("FileIO", "MessagePack")]
    public async Task<long> File_Write_Read_MessagePack_10K()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            var data = MessagePackSerializer.Serialize(_collection10K, MessagePackSerializerOptions.Standard);
            await File.WriteAllBytesAsync(tempPath, data);
            
            var readData = await File.ReadAllBytesAsync(tempPath);
            var deserialized = MessagePackSerializer.Deserialize<SymbolMappingCollection>(readData, MessagePackSerializerOptions.Standard);
            
            return deserialized.Mappings.Count;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Benchmark]
    [BenchmarkCategory("FileIO", "Json")]
    public async Task<long> File_Write_Read_Json_10K()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            var data = JsonSerializer.Serialize(_collection10K);
            await File.WriteAllTextAsync(tempPath, data);
            
            var readData = await File.ReadAllTextAsync(tempPath);
            var deserialized = JsonSerializer.Deserialize<SymbolMappingCollection>(readData);
            
            return deserialized?.Mappings.Count ?? 0;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    #endregion

    #region Memory and Size Analysis

    /// <summary>
    /// Analyze memory usage patterns - not a benchmark
    /// </summary>
    public void AnalyzeMemoryUsage()
    {
        Console.WriteLine("\n🧠 Memory Usage Analysis:");
        Console.WriteLine(new string('=', 50));

        AnalyzeDatasetMemory("10K Mappings", _collection10K, _msgPack10K, _json10K);
        AnalyzeDatasetMemory("50K Mappings", _collection50K, _msgPack50K, _json50K);
        AnalyzeDatasetMemory("100K Mappings", _collection100K, _msgPack100K, _json100K);
    }

    private static void AnalyzeDatasetMemory(string label, SymbolMappingCollection collection, byte[] msgPackData, string jsonData)
    {
        var objectCount = collection.Mappings.Count;
        var msgPackSize = msgPackData.Length;
        var jsonSize = System.Text.Encoding.UTF8.GetByteCount(jsonData);
        
        // Estimate in-memory object size (rough calculation)
        var estimatedObjectSize = objectCount * (
            50 + // Base object overhead
            20 + // Average string lengths
            8 +  // DateTime
            8 +  // Double
            4 +  // Int
            16   // List overhead
        );

        Console.WriteLine($"\n{label}:");
        Console.WriteLine($"  Object Count:     {objectCount:N0}");
        Console.WriteLine($"  Est. Memory Size: {estimatedObjectSize:N0} bytes ({estimatedObjectSize / (1024 * 1024.0):F1} MB)");
        Console.WriteLine($"  MessagePack Size: {msgPackSize:N0} bytes ({msgPackSize / (1024 * 1024.0):F1} MB)");
        Console.WriteLine($"  JSON Size:        {jsonSize:N0} bytes ({jsonSize / (1024 * 1024.0):F1} MB)");
        Console.WriteLine($"  MessagePack Compression: {(double)msgPackSize / estimatedObjectSize:P1}");
        Console.WriteLine($"  JSON Compression:        {(double)jsonSize / estimatedObjectSize:P1}");
        Console.WriteLine($"  MessagePack vs JSON:     {(double)msgPackSize / jsonSize:P1}");
    }

    #endregion
}