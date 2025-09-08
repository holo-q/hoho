using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Bogus;
using Hoho.Decomp;
using MessagePack;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Hoho.Benchmarks;

/// <summary>
/// Benchmarks MessagePack serialization vs JSON alternatives
/// Target: 10x faster serialization than JSON
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[MarkdownExporter, HtmlExporter, CsvExporter]
public class MessagePackSerializationBenchmark
{
    private MessagePackSymbolMapping[] _smallDataset = null!;
    private MessagePackSymbolMapping[] _mediumDataset = null!;
    private MessagePackSymbolMapping[] _largeDataset = null!;
    private SymbolMappingCollection _smallCollection = null!;
    private SymbolMappingCollection _mediumCollection = null!;
    private SymbolMappingCollection _largeCollection = null!;
    
    private byte[] _msgPackSmallData = null!;
    private byte[] _msgPackMediumData = null!;
    private byte[] _msgPackLargeData = null!;
    
    private string _jsonSmallData = null!;
    private string _jsonMediumData = null!;
    private string _jsonLargeData = null!;
    
    private string _newtonsoftSmallData = null!;
    private string _newtonsoftMediumData = null!;
    private string _newtonsoftLargeData = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Generate realistic test data using Bogus
        var faker = new Faker<MessagePackSymbolMapping>()
            .RuleFor(m => m.Original, f => f.Hacker.Noun())
            .RuleFor(m => m.Mapped, f => f.Hacker.Noun() + f.Random.AlphaNumeric(3))
            .RuleFor(m => m.Type, f => f.PickRandom<SymbolType>())
            .RuleFor(m => m.Context, f => f.PickRandom(null, f.Hacker.Noun()))
            .RuleFor(m => m.Confidence, f => f.Random.Double(0.5, 1.0))
            .RuleFor(m => m.LastUpdated, f => f.Date.Recent())
            .RuleFor(m => m.UsageCount, f => f.Random.Int(1, 100))
            .RuleFor(m => m.References, f => f.Make(f.Random.Int(0, 5), () => f.System.FileName()));

        // Create datasets of different sizes
        _smallDataset = faker.Generate(100).ToArray();
        _mediumDataset = faker.Generate(1000).ToArray();
        _largeDataset = faker.Generate(10000).ToArray();

        // Create collections
        _smallCollection = new SymbolMappingCollection
        {
            Version = "1.0",
            Mappings = _smallDataset.ToList(),
            Statistics = new DatabaseStatistics
            {
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                TotalMappings = _smallDataset.Length
            }
        };

        _mediumCollection = new SymbolMappingCollection
        {
            Version = "1.0",
            Mappings = _mediumDataset.ToList(),
            Statistics = new DatabaseStatistics
            {
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                TotalMappings = _mediumDataset.Length
            }
        };

        _largeCollection = new SymbolMappingCollection
        {
            Version = "1.0",
            Mappings = _largeDataset.ToList(),
            Statistics = new DatabaseStatistics
            {
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                TotalMappings = _largeDataset.Length
            }
        };

        // Pre-serialize data for deserialization benchmarks
        _msgPackSmallData = MessagePackSerializer.Serialize(_smallCollection);
        _msgPackMediumData = MessagePackSerializer.Serialize(_mediumCollection);
        _msgPackLargeData = MessagePackSerializer.Serialize(_largeCollection);

        _jsonSmallData = JsonSerializer.Serialize(_smallCollection);
        _jsonMediumData = JsonSerializer.Serialize(_mediumCollection);
        _jsonLargeData = JsonSerializer.Serialize(_largeCollection);

        _newtonsoftSmallData = JsonConvert.SerializeObject(_smallCollection);
        _newtonsoftMediumData = JsonConvert.SerializeObject(_mediumCollection);
        _newtonsoftLargeData = JsonConvert.SerializeObject(_largeCollection);
    }

    #region Small Dataset Benchmarks (100 mappings)

    [Benchmark]
    [BenchmarkCategory("Serialize", "Small")]
    public byte[] MessagePack_Serialize_Small() => 
        MessagePackSerializer.Serialize(_smallCollection, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Serialize", "Small")]
    public string SystemTextJson_Serialize_Small() => 
        JsonSerializer.Serialize(_smallCollection);

    [Benchmark]
    [BenchmarkCategory("Serialize", "Small")]
    public string NewtonsoftJson_Serialize_Small() => 
        JsonConvert.SerializeObject(_smallCollection);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Small")]
    public SymbolMappingCollection MessagePack_Deserialize_Small() => 
        MessagePackSerializer.Deserialize<SymbolMappingCollection>(_msgPackSmallData, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Small")]
    public SymbolMappingCollection? SystemTextJson_Deserialize_Small() => 
        JsonSerializer.Deserialize<SymbolMappingCollection>(_jsonSmallData);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Small")]
    public SymbolMappingCollection? NewtonsoftJson_Deserialize_Small() => 
        JsonConvert.DeserializeObject<SymbolMappingCollection>(_newtonsoftSmallData);

    #endregion

    #region Medium Dataset Benchmarks (1,000 mappings)

    [Benchmark]
    [BenchmarkCategory("Serialize", "Medium")]
    public byte[] MessagePack_Serialize_Medium() => 
        MessagePackSerializer.Serialize(_mediumCollection, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Serialize", "Medium")]
    public string SystemTextJson_Serialize_Medium() => 
        JsonSerializer.Serialize(_mediumCollection);

    [Benchmark]
    [BenchmarkCategory("Serialize", "Medium")]
    public string NewtonsoftJson_Serialize_Medium() => 
        JsonConvert.SerializeObject(_mediumCollection);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Medium")]
    public SymbolMappingCollection MessagePack_Deserialize_Medium() => 
        MessagePackSerializer.Deserialize<SymbolMappingCollection>(_msgPackMediumData, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Medium")]
    public SymbolMappingCollection? SystemTextJson_Deserialize_Medium() => 
        JsonSerializer.Deserialize<SymbolMappingCollection>(_jsonMediumData);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Medium")]
    public SymbolMappingCollection? NewtonsoftJson_Deserialize_Medium() => 
        JsonConvert.DeserializeObject<SymbolMappingCollection>(_newtonsoftMediumData);

    #endregion

    #region Large Dataset Benchmarks (10,000 mappings)

    [Benchmark]
    [BenchmarkCategory("Serialize", "Large")]
    public byte[] MessagePack_Serialize_Large() => 
        MessagePackSerializer.Serialize(_largeCollection, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Serialize", "Large")]
    public string SystemTextJson_Serialize_Large() => 
        JsonSerializer.Serialize(_largeCollection);

    [Benchmark]
    [BenchmarkCategory("Serialize", "Large")]
    public string NewtonsoftJson_Serialize_Large() => 
        JsonConvert.SerializeObject(_largeCollection);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Large")]
    public SymbolMappingCollection MessagePack_Deserialize_Large() => 
        MessagePackSerializer.Deserialize<SymbolMappingCollection>(_msgPackLargeData, MessagePackSerializerOptions.Standard);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Large")]
    public SymbolMappingCollection? SystemTextJson_Deserialize_Large() => 
        JsonSerializer.Deserialize<SymbolMappingCollection>(_jsonLargeData);

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Large")]
    public SymbolMappingCollection? NewtonsoftJson_Deserialize_Large() => 
        JsonConvert.DeserializeObject<SymbolMappingCollection>(_newtonsoftLargeData);

    #endregion

    #region Size Comparison Methods

    /// <summary>
    /// Compare serialized sizes - not a benchmark, just for analysis
    /// </summary>
    public void AnalyzeSizes()
    {
        Console.WriteLine("\n📏 Serialized Size Analysis:");
        Console.WriteLine(new string('=', 50));

        AnalyzeDatasetSizes("Small (100 mappings)", _smallCollection);
        AnalyzeDatasetSizes("Medium (1,000 mappings)", _mediumCollection);
        AnalyzeDatasetSizes("Large (10,000 mappings)", _largeCollection);
    }

    private static void AnalyzeDatasetSizes(string label, SymbolMappingCollection collection)
    {
        var msgPackBytes = MessagePackSerializer.Serialize(collection, MessagePackSerializerOptions.Standard);
        var jsonString = JsonSerializer.Serialize(collection);
        var newtonsoftJsonString = JsonConvert.SerializeObject(collection);
        
        var msgPackSize = msgPackBytes.Length;
        var jsonSize = System.Text.Encoding.UTF8.GetByteCount(jsonString);
        var newtonsoftSize = System.Text.Encoding.UTF8.GetByteCount(newtonsoftJsonString);

        Console.WriteLine($"\n{label}:");
        Console.WriteLine($"  MessagePack:     {msgPackSize:N0} bytes");
        Console.WriteLine($"  System.Text.Json: {jsonSize:N0} bytes");
        Console.WriteLine($"  Newtonsoft.Json:  {newtonsoftSize:N0} bytes");
        Console.WriteLine($"  MessagePack vs STJ: {(double)msgPackSize / jsonSize:P1} size ratio");
        Console.WriteLine($"  MessagePack vs NSJ: {(double)msgPackSize / newtonsoftSize:P1} size ratio");
    }

    #endregion
}