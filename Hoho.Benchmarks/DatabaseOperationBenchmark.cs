using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Bogus;
using Hoho.Decomp;

namespace Hoho.Benchmarks;

/// <summary>
/// Benchmarks database operations: CRUD, search, migration, and concurrent access
/// Validates real-world usage patterns and performance characteristics
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[MarkdownExporter, HtmlExporter, CsvExporter]
public class DatabaseOperationBenchmark
{
    private MessagePackMappingDatabase _database = null!;
    private MessagePackMappingDatabase _largeDatabase = null!;
    private MessagePackSymbolMapping[] _testMappings = null!;
    private MessagePackSymbolMapping[] _largeMappings = null!;
    private string _tempDbPath = null!;
    private string _largeDbPath = null!;
    private string _jsonMigrationPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("🗄️ Setting up database operation benchmarks...");
        
        // Create temp database paths
        var tempDir = Path.Combine(Path.GetTempPath(), $"hoho_benchmarks_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        _tempDbPath = Path.Combine(tempDir, "test.msgpack");
        _largeDbPath = Path.Combine(tempDir, "large.msgpack");
        _jsonMigrationPath = Path.Combine(tempDir, "migration.json");

        // Initialize databases
        _database = new MessagePackMappingDatabase(_tempDbPath);
        _largeDatabase = new MessagePackMappingDatabase(_largeDbPath);

        // Generate realistic test data
        var faker = new Faker<MessagePackSymbolMapping>()
            .RuleFor(m => m.Original, f => GenerateObfuscatedSymbol(f))
            .RuleFor(m => m.Mapped, f => GenerateCleanSymbol(f))
            .RuleFor(m => m.Type, f => f.PickRandom<SymbolType>())
            .RuleFor(m => m.Context, f => GenerateContext(f))
            .RuleFor(m => m.Confidence, f => f.Random.Double(0.7, 1.0))
            .RuleFor(m => m.LastUpdated, f => f.Date.Recent(7))
            .RuleFor(m => m.UsageCount, f => f.Random.Int(1, 50))
            .RuleFor(m => m.References, f => f.Make(f.Random.Int(0, 3), () => f.System.FilePath()));

        _testMappings = faker.Generate(1000).ToArray();
        _largeMappings = faker.Generate(10000).ToArray();

        // Pre-populate large database for query benchmarks
        Console.WriteLine("  Populating large database...");
        foreach (var mapping in _largeMappings)
        {
            _largeDatabase.AddMapping(
                mapping.Original,
                mapping.Mapped,
                mapping.Type,
                mapping.Context,
                mapping.Confidence
            );
        }

        // Create JSON migration file
        CreateJsonMigrationFile();
        
        Console.WriteLine("✅ Database setup complete!");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            if (File.Exists(_largeDbPath)) File.Delete(_largeDbPath);
            if (File.Exists(_jsonMigrationPath)) File.Delete(_jsonMigrationPath);
            
            var tempDir = Path.GetDirectoryName(_tempDbPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Cleanup error: {ex.Message}");
        }
    }

    #region CRUD Operations

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CRUD", "Add")]
    public int Add_Single_Mapping()
    {
        var mapping = _testMappings[0];
        _database.AddMapping(
            mapping.Original,
            mapping.Mapped,
            mapping.Type,
            mapping.Context,
            mapping.Confidence
        );
        return 1;
    }

    [Benchmark]
    [BenchmarkCategory("CRUD", "Add")]
    public int Add_100_Mappings()
    {
        var database = new MessagePackMappingDatabase();
        int count = 0;
        
        foreach (var mapping in _testMappings.Take(100))
        {
            database.AddMapping(
                mapping.Original,
                mapping.Mapped,
                mapping.Type,
                mapping.Context,
                mapping.Confidence
            );
            count++;
        }
        
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("CRUD", "Add")]
    public int Add_1000_Mappings()
    {
        var database = new MessagePackMappingDatabase();
        int count = 0;
        
        foreach (var mapping in _testMappings)
        {
            database.AddMapping(
                mapping.Original,
                mapping.Mapped,
                mapping.Type,
                mapping.Context,
                mapping.Confidence
            );
            count++;
        }
        
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("CRUD", "Query")]
    public int Query_Single_Mapping()
    {
        var mapping = _largeMappings[500]; // Middle of dataset
        var result = _largeDatabase.GetMapping(mapping.Original, mapping.Context);
        return result != null ? 1 : 0;
    }

    [Benchmark]
    [BenchmarkCategory("CRUD", "Query")]
    public int Query_100_Mappings()
    {
        int found = 0;
        var random = new Random(42); // Consistent seed for reproducible results
        
        for (int i = 0; i < 100; i++)
        {
            var mapping = _largeMappings[random.Next(_largeMappings.Length)];
            var result = _largeDatabase.GetMapping(mapping.Original, mapping.Context);
            if (result != null) found++;
        }
        
        return found;
    }

    [Benchmark]
    [BenchmarkCategory("CRUD", "Update")]
    public int Update_Existing_Mappings()
    {
        int updated = 0;
        var random = new Random(42);
        
        for (int i = 0; i < 50; i++)
        {
            var mapping = _largeMappings[random.Next(_largeMappings.Length)];
            _largeDatabase.AddMapping(
                mapping.Original,
                mapping.Mapped + "_updated",
                mapping.Type,
                mapping.Context,
                mapping.Confidence + 0.01
            );
            updated++;
        }
        
        return updated;
    }

    #endregion

    #region Search Operations

    [Benchmark]
    [BenchmarkCategory("Search", "Pattern")]
    public int Search_Simple_Pattern()
    {
        var results = _largeDatabase.SearchMappings(".*[0-9].*");
        return results.Count();
    }

    [Benchmark]
    [BenchmarkCategory("Search", "Pattern")]
    public int Search_Complex_Pattern()
    {
        var results = _largeDatabase.SearchMappings("^[A-Z][a-z]+.*Component$");
        return results.Count();
    }

    [Benchmark]
    [BenchmarkCategory("Search", "Context")]
    public int Search_By_Context()
    {
        var results = _largeDatabase.GetMappingsForContext("global");
        return results.Count();
    }

    [Benchmark]
    [BenchmarkCategory("Search", "Type")]
    public int Search_By_Type()
    {
        var allMappings = _largeDatabase.GetAllMappings();
        var classMappings = allMappings.Where(m => m.Type == SymbolType.Class);
        return classMappings.Count();
    }

    [Benchmark]
    [BenchmarkCategory("Search", "HighConfidence")]
    public int Search_High_Confidence_Mappings()
    {
        var allMappings = _largeDatabase.GetAllMappings();
        var highConfidence = allMappings.Where(m => m.Confidence > 0.9);
        return highConfidence.Count();
    }

    #endregion

    #region Persistence Operations

    [Benchmark]
    [BenchmarkCategory("Persistence", "Save")]
    public async Task<long> Save_Small_Database()
    {
        var database = new MessagePackMappingDatabase();
        
        // Add 100 mappings
        foreach (var mapping in _testMappings.Take(100))
        {
            database.AddMapping(
                mapping.Original,
                mapping.Mapped,
                mapping.Type,
                mapping.Context,
                mapping.Confidence
            );
        }
        
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await database.SaveAsync();
        sw.Stop();
        
        return sw.ElapsedMilliseconds;
    }

    [Benchmark]
    [BenchmarkCategory("Persistence", "Save")]
    public async Task<long> Save_Large_Database()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _largeDatabase.SaveAsync();
        sw.Stop();
        
        return sw.ElapsedMilliseconds;
    }

    [Benchmark]
    [BenchmarkCategory("Persistence", "Load")]
    public int Load_Database()
    {
        // First save current state
        _largeDatabase.SaveAsync().Wait();
        
        // Load fresh instance
        var loadedDatabase = new MessagePackMappingDatabase(_largeDbPath);
        var stats = loadedDatabase.GetStatistics();
        
        return stats.TotalMappings;
    }

    #endregion

    #region Migration Operations

    [Benchmark]
    [BenchmarkCategory("Migration", "JsonToMessagePack")]
    public async Task<int> Migrate_From_Json()
    {
        var database = new MessagePackMappingDatabase();
        await database.MigrateFromJsonAsync(_jsonMigrationPath);
        
        var stats = database.GetStatistics();
        return stats.TotalMappings;
    }

    #endregion

    #region Statistics and Reporting

    [Benchmark]
    [BenchmarkCategory("Stats", "Calculate")]
    public MappingStatistics Calculate_Statistics()
    {
        return _largeDatabase.GetStatistics();
    }

    [Benchmark]
    [BenchmarkCategory("Export", "Table")]
    public int Export_Table_Format()
    {
        var exported = _largeDatabase.ExportToReadableFormat(ExportFormat.Table);
        return exported.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Export", "Markdown")]
    public int Export_Markdown_Format()
    {
        var exported = _largeDatabase.ExportToReadableFormat(ExportFormat.Markdown);
        return exported.Length;
    }

    #endregion

    #region Concurrent Operations (Simulation)

    [Benchmark]
    [BenchmarkCategory("Concurrent", "Mixed")]
    public int Simulate_Concurrent_Operations()
    {
        var database = new MessagePackMappingDatabase();
        int operations = 0;
        var random = new Random(42);

        // Simulate mixed read/write operations
        Parallel.For(0, 100, i =>
        {
            if (i % 3 == 0)
            {
                // Add operation
                var mapping = _testMappings[i % _testMappings.Length];
                database.AddMapping(
                    mapping.Original + "_" + i,
                    mapping.Mapped + "_" + i,
                    mapping.Type,
                    mapping.Context,
                    mapping.Confidence
                );
                Interlocked.Increment(ref operations);
            }
            else
            {
                // Query operation
                var mapping = _testMappings[random.Next(_testMappings.Length)];
                var result = database.GetMapping(mapping.Original, mapping.Context);
                if (result != null) Interlocked.Increment(ref operations);
            }
        });

        return operations;
    }

    #endregion

    #region Helper Methods

    private static string GenerateObfuscatedSymbol(Faker faker)
    {
        return faker.Random.Int(1, 10) switch
        {
            1 => faker.Random.String2(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ"),
            2 => faker.Random.String2(2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ"),
            3 => faker.Random.String2(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") + faker.Random.Int(0, 99),
            4 => "_" + faker.Random.AlphaNumeric(3),
            5 => "$" + faker.Random.AlphaNumeric(2),
            _ => faker.Random.AlphaNumeric(faker.Random.Int(2, 6))
        };
    }

    private static string GenerateCleanSymbol(Faker faker)
    {
        return faker.Random.Int(1, 8) switch
        {
            1 => faker.Hacker.Noun(),
            2 => faker.Hacker.Noun() + "Handler",
            3 => "get" + faker.Hacker.Noun(),
            4 => "set" + faker.Hacker.Noun(),
            5 => faker.Hacker.Noun() + "Component",
            6 => faker.Lorem.Word() + faker.Lorem.Word(),
            7 => faker.Name.FirstName().ToLower(),
            _ => faker.Hacker.Noun() + faker.Random.AlphaNumeric(2)
        };
    }

    private static string? GenerateContext(Faker faker)
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

    private void CreateJsonMigrationFile()
    {
        var jsonMappings = new Dictionary<string, object>();
        
        foreach (var mapping in _testMappings.Take(500))
        {
            jsonMappings[mapping.Original] = new
            {
                Original = mapping.Original,
                Mapped = mapping.Mapped,
                Type = mapping.Type.ToString(),
                LastUpdated = mapping.LastUpdated.ToString("O")
            };
        }

        var jsonData = new
        {
            Version = "1.0",
            Mappings = jsonMappings
        };

        var json = JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_jsonMigrationPath, json);
    }

    #endregion
}