using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Hoho.Decomp;
using MessagePack;
using Xunit;

namespace Decomp.Tests;

/// <summary>
/// Tests for MessagePack serialization/deserialization, persistence, and corruption handling
/// Validates binary format compatibility and round-trip data integrity
/// </summary>
public class MessagePackSerializationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly MessagePackMappingDatabase _database;
    private readonly string _tempDirectory;

    public MessagePackSerializationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"msgpack_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _testDbPath = Path.Combine(_tempDirectory, "test_mappings.msgpack");
        _database = new MessagePackMappingDatabase(_testDbPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    #region Serialization Round-Trip Tests

    [Fact]
    public async Task SaveAsync_And_Load_Should_Preserve_All_Data()
    {
        // Arrange - Create comprehensive test data
        _database.AddMapping("Wu1", "ReactModule", SymbolType.Class, "global", 0.95);
        _database.AddMapping("A", "props", SymbolType.Parameter, "Wu1.constructor", 0.9);
        _database.AddMapping("B", "context", SymbolType.Parameter, "Wu1.constructor", 0.85);
        _database.AddMapping("Ct1", "ApplicationCore", SymbolType.Class, "global", 0.92);
        _database.AddMapping("process", "render", SymbolType.Method, "Wu1", 0.88);
        
        var originalStats = _database.GetStatistics();
        
        // Act - Save to file
        await _database.SaveAsync();
        
        // Create new database instance loading from file
        var loadedDatabase = new MessagePackMappingDatabase(_testDbPath);
        var loadedStats = loadedDatabase.GetStatistics();

        // Assert - All statistics should match
        loadedStats.TotalMappings.Should().Be(originalStats.TotalMappings);
        loadedStats.ByType.Should().BeEquivalentTo(originalStats.ByType);
        loadedStats.ByContext.Should().BeEquivalentTo(originalStats.ByContext);
        loadedStats.AverageConfidence.Should().BeApproximately(originalStats.AverageConfidence, 0.001);
        loadedStats.HighConfidenceMappings.Should().Be(originalStats.HighConfidenceMappings);

        // Assert - Individual mappings should be preserved
        var wu1Mapping = loadedDatabase.GetMapping("Wu1", "global");
        wu1Mapping.Should().NotBeNull();
        wu1Mapping!.Mapped.Should().Be("ReactModule");
        wu1Mapping.Confidence.Should().Be(0.95);
        wu1Mapping.Type.Should().Be(SymbolType.Class);

        var contextMapping = loadedDatabase.GetMapping("A", "Wu1.constructor");
        contextMapping.Should().NotBeNull();
        contextMapping!.Mapped.Should().Be("props");
        contextMapping.Context.Should().Be("Wu1.constructor");
    }

    [Fact]
    public async Task SaveAsync_Should_Create_Directory_If_Not_Exists()
    {
        // Arrange
        var nestedPath = Path.Combine(_tempDirectory, "nested", "deep", "path", "mappings.msgpack");
        var nestedDatabase = new MessagePackMappingDatabase(nestedPath);
        nestedDatabase.AddMapping("Test", "TestMapping", SymbolType.Class, "global", 0.8);

        // Act
        await nestedDatabase.SaveAsync();

        // Assert
        File.Exists(nestedPath).Should().BeTrue();
        var reloadedDatabase = new MessagePackMappingDatabase(nestedPath);
        reloadedDatabase.GetMapping("Test", "global").Should().NotBeNull();
    }

    [Fact]
    public async Task Load_Should_Handle_Nonexistent_File_Gracefully()
    {
        // Arrange
        var nonexistentPath = Path.Combine(_tempDirectory, "doesnotexist.msgpack");

        // Act & Assert - Should not throw
        var act = () => new MessagePackMappingDatabase(nonexistentPath);
        act.Should().NotThrow();

        var database = new MessagePackMappingDatabase(nonexistentPath);
        database.GetAllMappings().Should().BeEmpty();
        database.GetStatistics().TotalMappings.Should().Be(0);
    }

    [Fact]
    public async Task Multiple_Save_Load_Cycles_Should_Maintain_Data_Integrity()
    {
        // Arrange - Initial data
        _database.AddMapping("Initial", "InitialMapping", SymbolType.Class, "global", 0.8);
        
        for (int cycle = 0; cycle < 5; cycle++)
        {
            // Act - Save current state
            await _database.SaveAsync();
            
            // Add more data
            _database.AddMapping($"Symbol{cycle}", $"Mapping{cycle}", SymbolType.Method, $"context{cycle}", 0.9);
            
            // Load from file to new instance
            var reloadedDatabase = new MessagePackMappingDatabase(_testDbPath);
            
            // Assert - Previous data should still exist
            var initialMapping = reloadedDatabase.GetMapping("Initial", "global");
            initialMapping.Should().NotBeNull();
            initialMapping!.Mapped.Should().Be("InitialMapping");
            
            // Check cycle data exists
            if (cycle > 0)
            {
                for (int i = 0; i < cycle; i++)
                {
                    var cycleMapping = reloadedDatabase.GetMapping($"Symbol{i}", $"context{i}");
                    cycleMapping.Should().NotBeNull($"Symbol{i} should exist after cycle {cycle}");
                }
            }
        }
    }

    #endregion

    #region Corruption Handling Tests

    [Fact]
    public async Task Load_Should_Handle_Corrupted_File_With_Backup()
    {
        // Arrange - Create valid database
        _database.AddMapping("Wu1", "ReactModule", SymbolType.Class, "global", 0.9);
        await _database.SaveAsync();
        
        // Corrupt the file
        await File.WriteAllTextAsync(_testDbPath, "This is not valid MessagePack data!");

        // Act - Create new database (should detect corruption)
        var corruptedDatabase = new MessagePackMappingDatabase(_testDbPath);

        // Assert - Should create empty database and backup corrupted file
        corruptedDatabase.GetAllMappings().Should().BeEmpty();
        
        // Check backup file was created
        var backupFiles = Directory.GetFiles(_tempDirectory, "*.backup.*");
        backupFiles.Should().NotBeEmpty("Corrupted file should be backed up");
        
        var backupFile = backupFiles.First();
        var backupContent = await File.ReadAllTextAsync(backupFile);
        backupContent.Should().Be("This is not valid MessagePack data!");
    }

    [Fact]
    public async Task Load_Should_Handle_Empty_File()
    {
        // Arrange - Create empty file
        await File.WriteAllBytesAsync(_testDbPath, Array.Empty<byte>());

        // Act & Assert - Should handle gracefully
        var act = () => new MessagePackMappingDatabase(_testDbPath);
        act.Should().NotThrow();
        
        var database = new MessagePackMappingDatabase(_testDbPath);
        database.GetAllMappings().Should().BeEmpty();
    }

    [Fact]
    public async Task Load_Should_Handle_Partial_MessagePack_Data()
    {
        // Arrange - Create valid data then truncate
        _database.AddMapping("Test", "TestMapping", SymbolType.Class, "global", 0.8);
        await _database.SaveAsync();
        
        // Read and truncate the file
        var fullData = await File.ReadAllBytesAsync(_testDbPath);
        var truncatedData = fullData.Take(fullData.Length / 2).ToArray();
        await File.WriteAllBytesAsync(_testDbPath, truncatedData);

        // Act - Should handle truncated data gracefully
        var truncatedDatabase = new MessagePackMappingDatabase(_testDbPath);

        // Assert - Should create empty database and backup truncated data
        truncatedDatabase.GetAllMappings().Should().BeEmpty();
        Directory.GetFiles(_tempDirectory, "*.backup.*").Should().NotBeEmpty();
    }

    #endregion

    #region Binary Format Compatibility Tests

    [Fact]
    public async Task MessagePack_Format_Should_Be_Compact()
    {
        // Arrange - Create substantial test data
        for (int i = 0; i < 100; i++)
        {
            _database.AddMapping($"Symbol{i}", $"MappedSymbol{i}", SymbolType.Class, "global", 0.8 + (i % 20) * 0.01);
            _database.AddMapping($"Param{i}", $"parameter{i}", SymbolType.Parameter, $"Class{i % 10}", 0.7 + (i % 30) * 0.01);
        }

        // Act - Save to MessagePack
        await _database.SaveAsync();
        var messagePackSize = new FileInfo(_testDbPath).Length;

        // Create equivalent JSON for comparison
        var jsonPath = Path.Combine(_tempDirectory, "comparison.json");
        var allMappings = _database.GetAllMappings().ToList();
        var jsonData = new
        {
            Version = "1.0",
            TotalMappings = allMappings.Count,
            Mappings = allMappings.Select(m => new
            {
                m.Original,
                m.Mapped,
                Type = m.Type.ToString(),
                m.Context,
                m.Confidence,
                LastUpdated = m.LastUpdated.ToString("O"),
                m.UsageCount
            }).ToList()
        };
        
        var jsonString = JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, jsonString);
        var jsonSize = new FileInfo(jsonPath).Length;

        // Assert - MessagePack should be significantly smaller
        messagePackSize.Should().BeLessThan(jsonSize, "MessagePack should be more compact than JSON");
        
        // Should be at least 30% smaller (typical MessagePack compression)
        var compressionRatio = (double)messagePackSize / jsonSize;
        compressionRatio.Should().BeLessThan(0.7, "MessagePack should achieve at least 30% size reduction");
        
        Console.WriteLine($"MessagePack size: {messagePackSize:N0} bytes");
        Console.WriteLine($"JSON size: {jsonSize:N0} bytes"); 
        Console.WriteLine($"Compression ratio: {compressionRatio:P1}");
    }

    [Fact]
    public void MessagePack_Should_Preserve_DateTime_Precision()
    {
        // Arrange
        var originalTime = DateTime.UtcNow;
        var mapping = new MessagePackSymbolMapping
        {
            Original = "Test",
            Mapped = "TestMapping", 
            Type = SymbolType.Class,
            LastUpdated = originalTime,
            Confidence = 0.85
        };

        // Act - Serialize and deserialize
        var bytes = MessagePackSerializer.Serialize(mapping);
        var deserialized = MessagePackSerializer.Deserialize<MessagePackSymbolMapping>(bytes);

        // Assert - DateTime should be preserved with millisecond precision
        deserialized.LastUpdated.Should().Be(originalTime);
        
        // Should preserve at least millisecond precision
        var timeDifference = Math.Abs((deserialized.LastUpdated - originalTime).TotalMilliseconds);
        timeDifference.Should().BeLessThan(1, "DateTime should preserve millisecond precision");
    }

    [Fact]
    public void MessagePack_Should_Handle_Special_Characters_In_Symbols()
    {
        // Arrange - Symbols with special characters, unicode, etc.
        var testMappings = new[]
        {
            ("$_var", "dollarVariable", SymbolType.Variable),
            ("함수", "koreanFunction", SymbolType.Function), // Korean characters
            ("🚀rocket", "rocketFunction", SymbolType.Function), // Emoji
            ("'quoted'", "quotedString", SymbolType.Variable), // Quotes
            ("back\\slash", "backslashVar", SymbolType.Variable), // Backslashes
            ("\r\n\t", "whitespaceVar", SymbolType.Variable) // Control characters
        };

        foreach (var (original, mapped, type) in testMappings)
        {
            // Act - Add mapping with special characters
            _database.AddMapping(original, mapped, type, "global", 0.8);
            
            // Assert - Should retrieve correctly
            var result = _database.GetMapping(original, "global");
            result.Should().NotBeNull($"Mapping with special character '{original}' should be preserved");
            result!.Mapped.Should().Be(mapped);
            result.Type.Should().Be(type);
        }
    }

    [Fact]
    public void MessagePack_Should_Handle_Null_And_Empty_Values()
    {
        // Arrange
        var mapping = new MessagePackSymbolMapping
        {
            Original = "Test",
            Mapped = "TestMapping",
            Type = SymbolType.Class,
            Context = null, // Null context
            References = new List<string>() // Empty list
        };

        // Act - Serialize and deserialize
        var bytes = MessagePackSerializer.Serialize(mapping);
        var deserialized = MessagePackSerializer.Deserialize<MessagePackSymbolMapping>(bytes);

        // Assert
        deserialized.Context.Should().BeNull();
        deserialized.References.Should().NotBeNull().And.BeEmpty();
        deserialized.Original.Should().Be("Test");
        deserialized.Mapped.Should().Be("TestMapping");
    }

    #endregion

    #region Large Dataset Handling Tests

    [Fact]
    public async Task Should_Handle_Large_Dataset_Efficiently()
    {
        // Arrange - Create large dataset (simulating real-world usage)
        const int mappingCount = 10000;
        var stopwatch = Stopwatch.StartNew();

        // Act - Add large number of mappings
        for (int i = 0; i < mappingCount; i++)
        {
            var context = i % 100 == 0 ? "global" : $"Class{i % 50}"; // Vary contexts
            var type = (SymbolType)(i % Enum.GetValues<SymbolType>().Length);
            _database.AddMapping($"sym{i}", $"symbol{i}", type, context, 0.5 + (i % 50) * 0.01);
        }

        var addTime = stopwatch.Elapsed;
        stopwatch.Restart();

        // Save large dataset
        await _database.SaveAsync();
        var saveTime = stopwatch.Elapsed;
        stopwatch.Restart();

        // Load large dataset
        var largeDatabase = new MessagePackMappingDatabase(_testDbPath);
        var loadTime = stopwatch.Elapsed;

        // Assert - Performance should be reasonable
        addTime.Should().BeLessThan(TimeSpan.FromSeconds(10), "Adding 10k mappings should take < 10 seconds");
        saveTime.Should().BeLessThan(TimeSpan.FromSeconds(5), "Saving 10k mappings should take < 5 seconds");
        loadTime.Should().BeLessThan(TimeSpan.FromSeconds(3), "Loading 10k mappings should take < 3 seconds");

        // Data integrity check
        largeDatabase.GetStatistics().TotalMappings.Should().Be(mappingCount);
        
        // Random sampling check
        var random = new Random(42); // Fixed seed for reproducible tests
        for (int i = 0; i < 100; i++)
        {
            var randomIndex = random.Next(mappingCount);
            var expectedContext = randomIndex % 100 == 0 ? "global" : $"Class{randomIndex % 50}";
            
            var mapping = largeDatabase.GetMapping($"sym{randomIndex}", expectedContext);
            mapping.Should().NotBeNull($"Random mapping {randomIndex} should exist");
            mapping!.Mapped.Should().Be($"symbol{randomIndex}");
        }

        Console.WriteLine($"Large dataset performance:");
        Console.WriteLine($"  Add {mappingCount:N0} mappings: {addTime.TotalMilliseconds:N0}ms");
        Console.WriteLine($"  Save to disk: {saveTime.TotalMilliseconds:N0}ms");
        Console.WriteLine($"  Load from disk: {loadTime.TotalMilliseconds:N0}ms");
        Console.WriteLine($"  File size: {new FileInfo(_testDbPath).Length:N0} bytes");
    }

    [Fact]
    public void Memory_Usage_Should_Be_Reasonable_For_Large_Datasets()
    {
        // Arrange
        const int mappingCount = 5000;
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Add significant number of mappings
        for (int i = 0; i < mappingCount; i++)
        {
            _database.AddMapping($"sym{i}", $"symbol{i}", SymbolType.Class, $"context{i % 100}", 0.8);
        }

        var afterAddMemory = GC.GetTotalMemory(true);
        var memoryIncrease = afterAddMemory - initialMemory;
        var memoryPerMapping = (double)memoryIncrease / mappingCount;

        // Assert - Memory usage should be reasonable (< 1KB per mapping)
        memoryPerMapping.Should().BeLessThan(1024, "Memory per mapping should be less than 1KB");
        
        Console.WriteLine($"Memory usage for {mappingCount:N0} mappings:");
        Console.WriteLine($"  Total increase: {memoryIncrease:N0} bytes");
        Console.WriteLine($"  Per mapping: {memoryPerMapping:N1} bytes");
    }

    #endregion
}