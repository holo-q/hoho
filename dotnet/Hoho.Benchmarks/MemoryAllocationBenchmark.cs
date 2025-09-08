using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Bogus;
using Hoho.Decomp;
using MessagePack;
using System.Text.Json;
using Microsoft.IO;
using Microsoft.Toolkit.HighPerformance;

namespace Hoho.Benchmarks;

/// <summary>
/// Benchmarks memory allocation patterns and garbage collection efficiency
/// Focus on zero-allocation patterns for high-performance operations
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[MarkdownExporter, HtmlExporter, CsvExporter]
public class MemoryAllocationBenchmark
{
    private MessagePackSymbolMapping[] _testMappings = null!;
    private SymbolMappingCollection _testCollection = null!;
    private byte[] _msgPackData = null!;
    private string _jsonData = null!;
    private RecyclableMemoryStreamManager _memoryStreamManager = null!;
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;

    [GlobalSetup]
    public void Setup()
    {
        // Initialize RecyclableMemoryStream for efficient stream operations
        _memoryStreamManager = new RecyclableMemoryStreamManager();

        // Generate test data
        var faker = new Faker<MessagePackSymbolMapping>()
            .RuleFor(m => m.Original, f => f.Hacker.Noun())
            .RuleFor(m => m.Mapped, f => f.Hacker.Noun() + f.Random.AlphaNumeric(3))
            .RuleFor(m => m.Type, f => f.PickRandom<SymbolType>())
            .RuleFor(m => m.Context, f => f.PickRandom(null, f.Hacker.Noun()))
            .RuleFor(m => m.Confidence, f => f.Random.Double(0.5, 1.0))
            .RuleFor(m => m.LastUpdated, f => f.Date.Recent())
            .RuleFor(m => m.UsageCount, f => f.Random.Int(1, 100))
            .RuleFor(m => m.References, f => f.Make(f.Random.Int(0, 3), () => f.System.FileName()));

        _testMappings = faker.Generate(1000).ToArray();
        _testCollection = new SymbolMappingCollection
        {
            Version = "1.0",
            Mappings = _testMappings.ToList(),
            Statistics = new DatabaseStatistics
            {
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                TotalMappings = _testMappings.Length
            }
        };

        // Pre-serialize for deserialization benchmarks
        _msgPackData = MessagePackSerializer.Serialize(_testCollection);
        _jsonData = JsonSerializer.Serialize(_testCollection);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // RecyclableMemoryStreamManager doesn't implement IDisposable in this version
        // _memoryStreamManager?.Dispose();
    }

    #region Standard Allocation Patterns

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Serialize", "Standard")]
    public byte[] MessagePack_Standard_Serialize()
    {
        return MessagePackSerializer.Serialize(_testCollection, MessagePackSerializerOptions.Standard);
    }

    [Benchmark]
    [BenchmarkCategory("Serialize", "Standard")]
    public string Json_Standard_Serialize()
    {
        return JsonSerializer.Serialize(_testCollection);
    }

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Standard")]
    public SymbolMappingCollection MessagePack_Standard_Deserialize()
    {
        return MessagePackSerializer.Deserialize<SymbolMappingCollection>(_msgPackData, MessagePackSerializerOptions.Standard);
    }

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Standard")]
    public SymbolMappingCollection? Json_Standard_Deserialize()
    {
        return JsonSerializer.Deserialize<SymbolMappingCollection>(_jsonData);
    }

    #endregion

    #region Zero-Allocation Patterns with Pooling

    [Benchmark]
    [BenchmarkCategory("Serialize", "ZeroAlloc")]
    public async Task<Memory<byte>> MessagePack_ZeroAlloc_Serialize_ToMemory()
    {
        // Use RecyclableMemoryStream to avoid LOH allocations
        using var stream = _memoryStreamManager.GetStream();
        var bytes = MessagePackSerializer.Serialize(_testCollection, MessagePackSerializerOptions.Standard);
        await stream.WriteAsync(bytes);
        
        // Return as Memory<byte> to avoid copying
        return stream.GetReadOnlySequence().ToArray().AsMemory();
    }

    [Benchmark]
    [BenchmarkCategory("Serialize", "ZeroAlloc")]
    public ReadOnlyMemory<byte> MessagePack_ArrayPool_Serialize()
    {
        // Estimate buffer size based on data
        var estimatedSize = _testMappings.Length * 200; // Rough estimate
        var buffer = _arrayPool.Rent(estimatedSize);
        
        try
        {
            var writer = new ArrayBufferWriter<byte>(buffer);
            MessagePackSerializer.Serialize(writer, _testCollection, MessagePackSerializerOptions.Standard);
            
            return writer.WrittenMemory;
        }
        finally
        {
            _arrayPool.Return(buffer);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Deserialize", "ZeroAlloc")]
    public SymbolMappingCollection MessagePack_ReadOnlySequence_Deserialize()
    {
        // Use ReadOnlySequence to avoid unnecessary copying
        var sequence = new ReadOnlySequence<byte>(_msgPackData);
        return MessagePackSerializer.Deserialize<SymbolMappingCollection>(sequence, MessagePackSerializerOptions.Standard);
    }

    [Benchmark]
    [BenchmarkCategory("Deserialize", "ZeroAlloc")]
    public SymbolMappingCollection MessagePack_ReadOnlyMemory_Deserialize()
    {
        // Use ReadOnlyMemory for efficient deserialization
        var memory = _msgPackData.AsMemory();
        return MessagePackSerializer.Deserialize<SymbolMappingCollection>(memory, MessagePackSerializerOptions.Standard);
    }

    #endregion

    #region Streaming Operations

    [Benchmark]
    [BenchmarkCategory("Serialize", "Streaming")]
    public async Task<long> MessagePack_Stream_Serialize()
    {
        using var stream = _memoryStreamManager.GetStream();
        var bytes = MessagePackSerializer.Serialize(_testCollection, MessagePackSerializerOptions.Standard);
        await stream.WriteAsync(bytes);
        return stream.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Serialize", "Streaming")]
    public long Json_Stream_Serialize()
    {
        using var stream = _memoryStreamManager.GetStream();
        using var writer = new Utf8JsonWriter(stream as Stream);
        JsonSerializer.Serialize(writer, _testCollection);
        return stream.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Deserialize", "Streaming")]
    public SymbolMappingCollection MessagePack_Stream_Deserialize()
    {
        using var stream = _memoryStreamManager.GetStream();
        stream.Write(_msgPackData);
        stream.Position = 0;
        return MessagePackSerializer.Deserialize<SymbolMappingCollection>(stream, MessagePackSerializerOptions.Standard);
    }

    #endregion

    #region Collection Operation Benchmarks

    [Benchmark]
    [BenchmarkCategory("Collections", "Add")]
    public int Add_Multiple_Mappings_Standard()
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
    [BenchmarkCategory("Collections", "Query")]
    public int Query_Multiple_Mappings()
    {
        var database = new MessagePackMappingDatabase();
        
        // Pre-populate
        foreach (var mapping in _testMappings)
        {
            database.AddMapping(
                mapping.Original,
                mapping.Mapped,
                mapping.Type,
                mapping.Context,
                mapping.Confidence
            );
        }

        // Query operations
        int found = 0;
        foreach (var mapping in _testMappings.Take(100))
        {
            var result = database.GetMapping(mapping.Original, mapping.Context);
            if (result != null) found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Collections", "Search")]
    public int Search_Pattern_Mappings()
    {
        var database = new MessagePackMappingDatabase();
        
        // Pre-populate
        foreach (var mapping in _testMappings)
        {
            database.AddMapping(
                mapping.Original,
                mapping.Mapped,
                mapping.Type,
                mapping.Context,
                mapping.Confidence
            );
        }

        // Pattern search operations
        var results = database.SearchMappings(".*[a-z].*");
        return results.Count();
    }

    #endregion

    #region Memory Pressure Analysis

    [Benchmark]
    [BenchmarkCategory("Memory", "GCPressure")]
    public long Memory_Pressure_MessagePack_Serialize()
    {
        var initialMemory = GC.GetTotalMemory(false);
        
        // Serialize multiple times to generate pressure
        for (int i = 0; i < 10; i++)
        {
            var _ = MessagePackSerializer.Serialize(_testCollection, MessagePackSerializerOptions.Standard);
        }
        
        var finalMemory = GC.GetTotalMemory(false);
        return finalMemory - initialMemory;
    }

    [Benchmark]
    [BenchmarkCategory("Memory", "GCPressure")]
    public long Memory_Pressure_Json_Serialize()
    {
        var initialMemory = GC.GetTotalMemory(false);
        
        // Serialize multiple times to generate pressure
        for (int i = 0; i < 10; i++)
        {
            var _ = JsonSerializer.Serialize(_testCollection);
        }
        
        var finalMemory = GC.GetTotalMemory(false);
        return finalMemory - initialMemory;
    }

    [Benchmark]
    [BenchmarkCategory("Memory", "ZeroAlloc")]
    public async Task<long> Memory_Pressure_MessagePack_Pooled()
    {
        var initialMemory = GC.GetTotalMemory(false);
        
        // Use pooled memory to minimize allocations
        for (int i = 0; i < 10; i++)
        {
            using var stream = _memoryStreamManager.GetStream();
            var bytes = MessagePackSerializer.Serialize(_testCollection, MessagePackSerializerOptions.Standard);
        await stream.WriteAsync(bytes);
        }
        
        var finalMemory = GC.GetTotalMemory(false);
        return finalMemory - initialMemory;
    }

    #endregion
}

/// <summary>
/// Helper class for efficient buffer writing
/// </summary>
public class ArrayBufferWriter<T> : IBufferWriter<T>
{
    private T[] _buffer;
    private int _written;

    public ArrayBufferWriter(T[] buffer)
    {
        _buffer = buffer;
        _written = 0;
    }

    public ReadOnlyMemory<T> WrittenMemory => _buffer.AsMemory(0, _written);
    public ReadOnlySpan<T> WrittenSpan => _buffer.AsSpan(0, _written);
    public int WrittenCount => _written;

    public void Advance(int count)
    {
        if (count < 0 || _written + count > _buffer.Length)
            throw new ArgumentException("Invalid count");
        
        _written += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        var remaining = _buffer.Length - _written;
        if (sizeHint > remaining)
            throw new InvalidOperationException("Not enough space in buffer");
        
        return _buffer.AsMemory(_written);
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        var remaining = _buffer.Length - _written;
        if (sizeHint > remaining)
            throw new InvalidOperationException("Not enough space in buffer");
        
        return _buffer.AsSpan(_written);
    }
}