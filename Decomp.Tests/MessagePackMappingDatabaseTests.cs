using System.Text.Json;
using FluentAssertions;
using Hoho.Decomp;
using Moq;
using Xunit;

namespace Decomp.Tests;

/// <summary>
/// Comprehensive tests for MessagePack-based symbol mapping database
/// Tests core database operations, context awareness, and data integrity
/// </summary>
public class MessagePackMappingDatabaseTests : IDisposable {
	private readonly string                     _testDbPath;
	private readonly MessagePackMappingDatabase _database;

	public MessagePackMappingDatabaseTests() {
		_testDbPath = Path.Combine(Path.GetTempPath(), $"test_mapping_{Guid.NewGuid():N}.msgpack");
		_database   = new MessagePackMappingDatabase(_testDbPath);
	}

	public void Dispose() {
		if (File.Exists(_testDbPath)) {
			File.Delete(_testDbPath);
		}
	}

#region Core Database Operations Tests

	[Fact]
	public void AddMapping_Should_Store_Symbol_With_Context() {
		// Arrange
		const string     original   = "Wu1";
		const string     mapped     = "ReactModule";
		const string     context    = "global";
		const SymbolType type       = SymbolType.Class;
		const double     confidence = 0.95;

		// Act
		_database.AddMapping(original, mapped, type, context, confidence);

		// Assert
		var result = _database.GetMapping(original, context);
		result.Should().NotBeNull();
		result!.Original.Should().Be(original);
		result.Mapped.Should().Be(mapped);
		result.Type.Should().Be(type);
		result.Context.Should().Be(context);
		result.Confidence.Should().Be(confidence);
		result.UsageCount.Should().Be(1);
		result.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void GetMapping_Should_Support_Context_Fallback() {
		// Arrange
		_database.AddMapping("A", "props", SymbolType.Parameter, "Wu1.constructor", 0.9);
		_database.AddMapping("A", "connection", SymbolType.Parameter, "Bx2.constructor", 0.9);
		_database.AddMapping("A", "data", SymbolType.Parameter, "global", 0.8);

		// Act & Assert - Context-specific lookups
		var wu1Result = _database.GetMapping("A", "Wu1.constructor");
		wu1Result.Should().NotBeNull();
		wu1Result!.Mapped.Should().Be("props");

		var bx2Result = _database.GetMapping("A", "Bx2.constructor");
		bx2Result.Should().NotBeNull();
		bx2Result!.Mapped.Should().Be("connection");

		// Act & Assert - Fallback to global when specific context not found
		var unknownContextResult = _database.GetMapping("A", "UnknownClass.constructor");
		unknownContextResult.Should().NotBeNull();
		unknownContextResult!.Mapped.Should().Be("data");
		unknownContextResult.Context.Should().Be("global");
	}

	[Fact]
	public void AddMapping_Should_Update_Existing_Mapping() {
		// Arrange
		const string original       = "Ct1";
		const string initialMapping = "CoreSystem";
		const string updatedMapping = "ApplicationCore";

		_database.AddMapping(original, initialMapping, SymbolType.Class, "global", 0.7);
		var initialResult     = _database.GetMapping(original, "global");
		var initialUsageCount = initialResult!.UsageCount;
		var initialConfidence = initialResult.Confidence;

		// Act - Update existing mapping with higher confidence
		_database.AddMapping(original, updatedMapping, SymbolType.Class, "global", 0.95);

		// Assert
		var result = _database.GetMapping(original, "global");
		result.Should().NotBeNull();
		result!.Mapped.Should().Be(updatedMapping);
		result.Confidence.Should().Be(0.95); // Should use higher confidence
		result.UsageCount.Should().Be(initialUsageCount + 1);
		result.LastUpdated.Should().BeOnOrAfter(initialResult.LastUpdated);
	}

	[Fact]
	public void GetMappingsForContext_Should_Return_Context_Specific_Mappings() {
		// Arrange
		const string context = "ReactComponent";
		_database.AddMapping("render", "renderUI", SymbolType.Method, context, 0.9);
		_database.AddMapping("setState", "updateState", SymbolType.Method, context, 0.8);
		_database.AddMapping("props", "properties", SymbolType.Property, context, 0.7);
		_database.AddMapping("globalFunc", "utilityFunction", SymbolType.Function, "global", 0.8);

		// Act
		var contextMappings = _database.GetMappingsForContext(context).ToList();

		// Assert
		contextMappings.Should().HaveCount(3);
		contextMappings.Should().AllSatisfy(m => m.Context.Should().Be(context));
		contextMappings.Select(m => m.Original).Should().Contain(new[] { "render", "setState", "props" });
	}

	[Fact]
	public void SearchMappings_Should_Support_Regex_Patterns() {
		// Arrange
		_database.AddMapping("Wu1", "ReactModule", SymbolType.Class, "global", 0.9);
		_database.AddMapping("Wu2", "ReactComponent", SymbolType.Class, "global", 0.8);
		_database.AddMapping("Bx1", "DatabaseConnection", SymbolType.Class, "global", 0.9);
		_database.AddMapping("render", "renderUI", SymbolType.Method, "Wu1", 0.8);

		// Act & Assert - Pattern matching original symbols
		var reactMappings = _database.SearchMappings("^Wu\\d+$").ToList();
		reactMappings.Should().HaveCount(2);
		reactMappings.Select(m => m.Original).Should().Contain(new[] { "Wu1", "Wu2" });

		// Act & Assert - Pattern matching mapped symbols
		var componentMappings = _database.SearchMappings(".*Component.*").ToList();
		componentMappings.Should().HaveCount(1);
		componentMappings.First().Mapped.Should().Be("ReactComponent");

		// Act & Assert - Case insensitive search
		var renderMappings = _database.SearchMappings("RENDER").ToList();
		renderMappings.Should().HaveCount(1); // Should match mapping with "render" → "renderUI"
		renderMappings.First().Original.Should().Be("render");
		renderMappings.First().Mapped.Should().Be("renderUI");
	}

	[Fact]
	public void GetAllMappings_Should_Return_Complete_Database() {
		// Arrange - Add various types of mappings
		_database.AddMapping("Wu1", "ReactModule", SymbolType.Class, "global", 0.9);
		_database.AddMapping("A", "props", SymbolType.Parameter, "Wu1.constructor", 0.8);
		_database.AddMapping("process", "transform", SymbolType.Method, "DataHandler", 0.7);

		// Act
		var allMappings = _database.GetAllMappings().ToList();

		// Assert
		allMappings.Should().HaveCount(3);
		allMappings.Select(m => m.Type).Should().Contain(new[] {
			SymbolType.Class, SymbolType.Parameter, SymbolType.Method
		});
	}

#endregion

#region Statistics and Analytics Tests

	[Fact]
	public void GetStatistics_Should_Calculate_Correct_Metrics() {
		// Arrange - Add diverse mappings
		_database.AddMapping("Wu1", "ReactModule", SymbolType.Class, "global", 0.9);
		_database.AddMapping("Wu2", "ReactComponent", SymbolType.Class, "global", 0.8);
		_database.AddMapping("A", "props", SymbolType.Parameter, "Wu1", 0.7);
		_database.AddMapping("B", "context", SymbolType.Parameter, "Wu1", 0.6);
		_database.AddMapping("process", "render", SymbolType.Method, "Wu1", 0.95);

		// Act
		var stats = _database.GetStatistics();

		// Assert
		stats.TotalMappings.Should().Be(5);
		stats.ByType.Should().ContainKey(SymbolType.Class).WhoseValue.Should().Be(2);
		stats.ByType.Should().ContainKey(SymbolType.Parameter).WhoseValue.Should().Be(2);
		stats.ByType.Should().ContainKey(SymbolType.Method).WhoseValue.Should().Be(1);

		stats.ByContext.Should().ContainKey("global").WhoseValue.Should().Be(2);
		stats.ByContext.Should().ContainKey("Wu1").WhoseValue.Should().Be(3);

		stats.AverageConfidence.Should().BeApproximately(0.79, 0.01); // (0.9+0.8+0.7+0.6+0.95)/5 = 3.95/5 = 0.79
		stats.HighConfidenceMappings.Should().Be(2);                  // Wu1 (0.9) and process (0.95)
		stats.TotalUsage.Should().Be(5);                              // Each mapping has usage count of 1
	}

	[Fact]
	public void Statistics_Should_Track_Usage_Count() {
		// Arrange
		const string symbol  = "Wu1";
		const string mapping = "ReactModule";

		// Act - Add same mapping multiple times (simulating repeated usage)
		_database.AddMapping(symbol, mapping, SymbolType.Class, "global", 0.9);
		_database.AddMapping(symbol, mapping, SymbolType.Class, "global", 0.9);
		_database.AddMapping(symbol, mapping, SymbolType.Class, "global", 0.9);

		// Assert
		var result = _database.GetMapping(symbol, "global");
		result.Should().NotBeNull();
		result!.UsageCount.Should().Be(3);

		var stats = _database.GetStatistics();
		stats.TotalUsage.Should().Be(3);
	}

#endregion

#region Error Handling and Edge Cases

	[Fact]
	public void GetMapping_Should_Return_Null_For_Unknown_Symbol() {
		// Act
		var result = _database.GetMapping("UnknownSymbol", "UnknownContext");

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public void AddMapping_Should_Handle_Null_Context() {
		// Arrange & Act
		_database.AddMapping("Wu1", "ReactModule", SymbolType.Class, null, 0.9);

		// Assert
		var result = _database.GetMapping("Wu1", null);
		result.Should().NotBeNull();
		result!.Context.Should().Be("global");
	}

	[Fact]
	public void AddMapping_Should_Handle_Empty_Strings() {
		// Act & Assert - Should not throw
		var act = () => _database.AddMapping("", "EmptyOriginal", SymbolType.Variable, "test", 0.5);
		act.Should().NotThrow();

		// Empty mapped name should also be handled
		var act2 = () => _database.AddMapping("test", "", SymbolType.Variable, "test", 0.5);
		act2.Should().NotThrow();
	}

	[Fact]
	public void SearchMappings_Should_Handle_Invalid_Regex() {
		// Arrange
		_database.AddMapping("Wu1", "ReactModule", SymbolType.Class, "global", 0.9);

		// Act & Assert - Should not throw on invalid regex, return empty results
		var act = () => _database.SearchMappings("[invalid[regex").ToList();
		act.Should().NotThrow();
	}

#endregion

#region Duplicate Handling Tests

	[Fact]
	public void AddMapping_Should_Preserve_Higher_Confidence_On_Update() {
		// Arrange
		const string symbol = "Ct1";

		// Act - Add with high confidence first
		_database.AddMapping(symbol, "CoreSystem", SymbolType.Class, "global", 0.9);

		// Try to update with lower confidence
		_database.AddMapping(symbol, "ApplicationCore", SymbolType.Class, "global", 0.7);

		// Assert - Should keep higher confidence but update mapping
		var result = _database.GetMapping(symbol, "global");
		result.Should().NotBeNull();
		result!.Mapped.Should().Be("ApplicationCore"); // Mapping should update
		result.Confidence.Should().Be(0.9);            // Confidence should remain higher
	}

	[Fact]
	public void AddMapping_Should_Handle_Same_Symbol_Different_Contexts() {
		// Arrange
		const string symbol = "A";

		// Act - Add same symbol in different contexts
		_database.AddMapping(symbol, "props", SymbolType.Parameter, "Wu1.constructor", 0.9);
		_database.AddMapping(symbol, "connection", SymbolType.Parameter, "Bx2.constructor", 0.8);
		_database.AddMapping(symbol, "data", SymbolType.Parameter, "global", 0.7);

		// Assert - All should exist independently
		var allMappings = _database.GetAllMappings().Where(m => m.Original == symbol).ToList();
		allMappings.Should().HaveCount(3);

		var contexts = allMappings.Select(m => m.Context).ToList();
		contexts.Should().Contain(new[] { "Wu1.constructor", "Bx2.constructor", "global" });
	}

#endregion

#region Serialization and Persistence Tests

	[Fact]
	public async Task SaveAsync_Should_Create_Valid_MessagePack_File() {
		// Arrange
		_database.AddMapping("Wu1", "ReactModule", SymbolType.Class, "global", 0.9);
		_database.AddMapping("A", "props", SymbolType.Parameter, "Wu1.constructor", 0.8);

		// Act
		await _database.SaveAsync();

		// Assert
		File.Exists(_testDbPath).Should().BeTrue();

		// Verify file is valid MessagePack by trying to load it
		var newDatabase   = new MessagePackMappingDatabase(_testDbPath);
		var loadedMapping = newDatabase.GetMapping("Wu1", "global");
		loadedMapping.Should().NotBeNull();
		loadedMapping!.Mapped.Should().Be("ReactModule");
	}

	[Fact]
	public async Task SaveAndLoad_Should_Preserve_All_Data_Integrity() {
		// Arrange - Add comprehensive test data
		var testMappings = new[] {
			("Wu1", "ReactModule", SymbolType.Class, "global", 0.95),
			("Bx2", "DatabaseConnection", SymbolType.Class, "global", 0.88),
			("A", "props", SymbolType.Parameter, "Wu1.constructor", 0.92),
			("B", "context", SymbolType.Parameter, "Wu1.constructor", 0.87),
			("render", "renderUI", SymbolType.Method, "Wu1", 0.90),
			("setState", "updateState", SymbolType.Method, "Wu1", 0.85)
		};

		foreach (var (original, mapped, type, context, confidence) in testMappings) {
			_database.AddMapping(original, mapped, type, context, confidence);
		}

		// Act - Save and reload
		await _database.SaveAsync();
		var reloadedDatabase = new MessagePackMappingDatabase(_testDbPath);

		// Assert - Verify all data is preserved
		var allMappings = reloadedDatabase.GetAllMappings().ToList();
		allMappings.Should().HaveCount(testMappings.Length);

		foreach (var (original, mapped, type, context, confidence) in testMappings) {
			var loadedMapping = reloadedDatabase.GetMapping(original, context);
			loadedMapping.Should().NotBeNull($"Mapping for {original} in context {context} should exist");
			loadedMapping!.Original.Should().Be(original);
			loadedMapping.Mapped.Should().Be(mapped);
			loadedMapping.Type.Should().Be(type);
			loadedMapping.Context.Should().Be(context);
			loadedMapping.Confidence.Should().BeApproximately(confidence, 0.001);
		}
	}

	[Fact]
	public async Task SerializationRoundTrip_Should_Maintain_Context_Hierarchy() {
		// Arrange - Test complex context hierarchy
		_database.AddMapping("A", "globalArg", SymbolType.Parameter, "global", 0.7);
		_database.AddMapping("A", "classProps", SymbolType.Parameter, "Wu1", 0.8);
		_database.AddMapping("A", "constructorProps", SymbolType.Parameter, "Wu1.constructor", 0.9);
		_database.AddMapping("A", "methodArg", SymbolType.Parameter, "Wu1.render", 0.85);

		// Act - Save and reload
		await _database.SaveAsync();
		var reloadedDatabase = new MessagePackMappingDatabase(_testDbPath);

		// Assert - Context fallback should work correctly
		reloadedDatabase.GetMapping("A", "Wu1.constructor")!.Mapped.Should().Be("constructorProps");
		reloadedDatabase.GetMapping("A", "Wu1.render")!.Mapped.Should().Be("methodArg");
		reloadedDatabase.GetMapping("A", "Wu1")!.Mapped.Should().Be("classProps");
		reloadedDatabase.GetMapping("A", "global")!.Mapped.Should().Be("globalArg");
		reloadedDatabase.GetMapping("A", "NonExistentContext")!.Mapped.Should().Be("globalArg"); // Fallback
	}

	[Fact]
	public async Task MessagePack_Should_Be_Significantly_Smaller_Than_JSON() {
		// Arrange - Add substantial data for meaningful comparison
		var random = new Random(42); // Deterministic for consistent tests

		for (int i = 0; i < 100; i++) {
			var original   = $"Sym{i:D3}";
			var mapped     = $"MappedSymbol{i:D3}WithLongDescriptiveName";
			var type       = (SymbolType)(i % 4); // Cycle through symbol types
			var context    = i % 10 == 0 ? "global" : $"Context{i % 5}";
			var confidence = 0.5 + (random.NextDouble() * 0.5); // 0.5 to 1.0

			_database.AddMapping(original, mapped, type, context, confidence);
		}

		// Act - Save as MessagePack
		await _database.SaveAsync();
		var msgpackSize = new FileInfo(_testDbPath).Length;

		// Create equivalent JSON for comparison
		var jsonPath = _testDbPath + ".json";
		var jsonData = new {
			mappings = _database.GetAllMappings().Select(m => new {
				original    = m.Original,
				mapped      = m.Mapped,
				type        = m.Type.ToString(),
				context     = m.Context,
				confidence  = m.Confidence,
				lastUpdated = m.LastUpdated,
				usageCount  = m.UsageCount
			})
		};

		var jsonString = System.Text.Json.JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true });
		await File.WriteAllTextAsync(jsonPath, jsonString);
		var jsonSize = new FileInfo(jsonPath).Length;

		// Assert - MessagePack should be significantly smaller
		msgpackSize.Should().BeLessThan(jsonSize, "MessagePack should be more compact than JSON");
		var compressionRatio = (double)msgpackSize / jsonSize;
		compressionRatio.Should().BeLessThan(0.8, "MessagePack should be at least 20% smaller than JSON");

		// Cleanup
		File.Delete(jsonPath);
	}

	[Fact]
	public void LoadMappings_Should_Handle_Missing_Database_File() {
		// Arrange
		var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.msgpack");

		// Act - Should not throw
		var act = () => new MessagePackMappingDatabase(nonExistentPath);

		// Assert
		act.Should().NotThrow();
		var database = new MessagePackMappingDatabase(nonExistentPath);
		database.GetAllMappings().Should().BeEmpty();
		database.GetStatistics().TotalMappings.Should().Be(0);
	}

	[Fact]
	public async Task LoadMappings_Should_Handle_Corrupted_Database_With_Backup() {
		// Arrange - Create corrupted file
		await File.WriteAllBytesAsync(_testDbPath, new byte[] { 0xFF, 0xFE, 0xFD, 0xFC }); // Invalid MessagePack

		// Act - Should not throw, should create backup
		var database = new MessagePackMappingDatabase(_testDbPath);

		// Assert - Should create new empty database
		database.GetAllMappings().Should().BeEmpty();

		// Should create backup of corrupted file
		var backupFiles = Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(_testDbPath)}.backup.*");
		backupFiles.Should().NotBeEmpty("Corrupted file should be backed up");

		// Cleanup backup files
		foreach (var backup in backupFiles) {
			File.Delete(backup);
		}
	}

	[Fact]
	public async Task SaveAsync_Should_Create_Directory_If_Not_Exists() {
		// Arrange
		var nestedPath = Path.Combine(Path.GetTempPath(), $"test_nested_{Guid.NewGuid():N}", "subdir", "database.msgpack");
		var database   = new MessagePackMappingDatabase(nestedPath);
		database.AddMapping("Test", "TestValue", SymbolType.Variable, "global", 0.8);

		// Act - Should create directory structure
		var act = async () => await database.SaveAsync();

		// Assert
		await act.Should().NotThrowAsync();
		File.Exists(nestedPath).Should().BeTrue();

		// Cleanup
		Directory.Delete(Path.GetDirectoryName(nestedPath)!, true);
	}

	[Fact]
	public async Task Statistics_Should_Be_Persisted_And_Updated() {
		// Arrange
		_database.AddMapping("Wu1", "ReactModule", SymbolType.Class, "global", 0.9);
		var originalStats = _database.GetStatistics();

		// Act - Save and reload
		await _database.SaveAsync();
		var reloadedDatabase = new MessagePackMappingDatabase(_testDbPath);
		var reloadedStats    = reloadedDatabase.GetStatistics();

		// Assert - Statistics should be preserved
		reloadedStats.TotalMappings.Should().Be(originalStats.TotalMappings);
		reloadedStats.LastModified.Should().BeCloseTo(originalStats.LastModified, TimeSpan.FromSeconds(1));
		reloadedStats.AverageConfidence.Should().BeApproximately(originalStats.AverageConfidence, 0.001);
		reloadedStats.ByType.Should().BeEquivalentTo(originalStats.ByType);
		reloadedStats.ByContext.Should().BeEquivalentTo(originalStats.ByContext);
	}

#endregion

#region JSON Migration Tests

	[Fact]
	public async Task MigrateFromJsonAsync_Should_Convert_Legacy_Data() {
		// Arrange - Create legacy JSON file
		var jsonPath = Path.Combine(Path.GetTempPath(), $"legacy_{Guid.NewGuid():N}.json");
		var legacyData = new JsonMappingData {
			Mappings = new Dictionary<string, JsonMapping> {
				["Wu1"]    = new JsonMapping { Original = "Wu1", Mapped    = "ReactModule", Type        = SymbolType.Class, LastUpdated  = DateTime.UtcNow },
				["Bx2"]    = new JsonMapping { Original = "Bx2", Mapped    = "DatabaseConnection", Type = SymbolType.Class, LastUpdated  = DateTime.UtcNow },
				["render"] = new JsonMapping { Original = "render", Mapped = "renderUI", Type           = SymbolType.Method, LastUpdated = DateTime.UtcNow }
			}
		};

		var jsonString = System.Text.Json.JsonSerializer.Serialize(legacyData, new JsonSerializerOptions { WriteIndented = true });
		await File.WriteAllTextAsync(jsonPath, jsonString);

		// Act
		await _database.MigrateFromJsonAsync(jsonPath);

		// Assert
		var mappings = _database.GetAllMappings().ToList();
		mappings.Should().HaveCount(3);

		var wu1Mapping = _database.GetMapping("Wu1", "global");
		wu1Mapping.Should().NotBeNull();
		wu1Mapping!.Mapped.Should().Be("ReactModule");
		wu1Mapping.Type.Should().Be(SymbolType.Class);
		wu1Mapping.Context.Should().Be("global");
		wu1Mapping.Confidence.Should().Be(0.8); // Default migration confidence

		// Original JSON should be backed up
		var backupFiles = Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(jsonPath)}.migrated.*");
		backupFiles.Should().NotBeEmpty("Original JSON should be backed up");

		// Cleanup
		foreach (var backup in backupFiles) {
			File.Delete(backup);
		}
	}

	[Fact]
	public async Task MigrateFromJsonAsync_Should_Handle_Missing_Json_File() {
		// Arrange
		var nonExistentJsonPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.json");

		// Act - Should not throw
		var act = async () => await _database.MigrateFromJsonAsync(nonExistentJsonPath);

		// Assert
		await act.Should().NotThrowAsync();
		_database.GetAllMappings().Should().BeEmpty();
	}

	[Fact]
	public async Task MigrateFromJsonAsync_Should_Handle_Invalid_Json() {
		// Arrange - Create invalid JSON file
		var jsonPath = Path.Combine(Path.GetTempPath(), $"invalid_{Guid.NewGuid():N}.json");
		await File.WriteAllTextAsync(jsonPath, "{ invalid json content }");

		// Act & Assert - Should throw exception for invalid JSON
		var act = async () => await _database.MigrateFromJsonAsync(jsonPath);
		await act.Should().ThrowAsync<Exception>();

		// Cleanup
		File.Delete(jsonPath);
	}

#endregion

#region Performance and Concurrency Tests

	[Fact]
	public async Task Database_Should_Handle_Large_Dataset_Efficiently() {
		// Arrange - Create large dataset
		const int mappingCount = 10000;
		var       stopwatch    = System.Diagnostics.Stopwatch.StartNew();

		// Act - Add many mappings
		for (int i = 0; i < mappingCount; i++) {
			var original = $"Symbol{i:D5}";
			var mapped   = $"MappedSymbol{i:D5}";
			var type     = (SymbolType)(i % 4);
			var context  = i % 100 == 0 ? "global" : $"Context{i % 10}";

			_database.AddMapping(original, mapped, type, context, 0.8);
		}

		var addTime = stopwatch.ElapsedMilliseconds;
		stopwatch.Restart();

		// Save to disk
		await _database.SaveAsync();
		var saveTime = stopwatch.ElapsedMilliseconds;
		stopwatch.Restart();

		// Load from disk
		var loadedDatabase = new MessagePackMappingDatabase(_testDbPath);
		var loadTime       = stopwatch.ElapsedMilliseconds;
		stopwatch.Stop();

		// Assert - Performance should be reasonable
		addTime.Should().BeLessThan(5000, "Adding 10k mappings should take less than 5 seconds");
		saveTime.Should().BeLessThan(2000, "Saving 10k mappings should take less than 2 seconds");
		loadTime.Should().BeLessThan(1000, "Loading 10k mappings should take less than 1 second");

		// Verify data integrity
		loadedDatabase.GetAllMappings().Should().HaveCount(mappingCount);
		var stats = loadedDatabase.GetStatistics();
		stats.TotalMappings.Should().Be(mappingCount);
	}

	[Fact]
	public void Context_Aware_Lookups_Should_Be_Fast_With_Large_Dataset() {
		// Arrange - Add mappings with complex context hierarchy
		const int symbolsPerContext = 1000;
		var       contexts          = new[] { "global", "Wu1", "Wu1.constructor", "Wu1.render", "Bx2", "Bx2.query" };

		foreach (var context in contexts) {
			for (int i = 0; i < symbolsPerContext; i++) {
				_database.AddMapping($"sym{i}", $"mapped{i}_{context}", SymbolType.Variable, context, 0.8);
			}
		}

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		// Act - Perform many context-aware lookups
		for (int i = 0; i < 1000; i++) {
			var symbolIndex  = i % symbolsPerContext;
			var contextIndex = i % contexts.Length;
			var result       = _database.GetMapping($"sym{symbolIndex}", contexts[contextIndex]);
			result.Should().NotBeNull();
		}

		stopwatch.Stop();

		// Assert - Lookups should be fast even with large dataset
		var avgLookupTime = stopwatch.ElapsedMilliseconds / 1000.0;
		avgLookupTime.Should().BeLessThan(5.0, "Average lookup time should be less than 5ms");
	}

#endregion

#region Edge Cases and Error Conditions

	[Fact]
	public void AddMapping_Should_Handle_Extreme_Confidence_Values() {
		// Arrange & Act - Test boundary values
		_database.AddMapping("test1", "mapped1", SymbolType.Variable, "global", -1.0); // Below 0
		_database.AddMapping("test2", "mapped2", SymbolType.Variable, "global", 0.0);  // Exactly 0
		_database.AddMapping("test3", "mapped3", SymbolType.Variable, "global", 1.0);  // Exactly 1
		_database.AddMapping("test4", "mapped4", SymbolType.Variable, "global", 2.0);  // Above 1

		// Assert - Should store values as provided (no validation in current implementation)
		_database.GetMapping("test1", "global")!.Confidence.Should().Be(-1.0);
		_database.GetMapping("test2", "global")!.Confidence.Should().Be(0.0);
		_database.GetMapping("test3", "global")!.Confidence.Should().Be(1.0);
		_database.GetMapping("test4", "global")!.Confidence.Should().Be(2.0);
	}

	[Fact]
	public void AddMapping_Should_Handle_Very_Long_Strings() {
		// Arrange
		var longString  = new string('A', 10000); // 10KB string
		var longContext = new string('B', 1000);  // 1KB context

		// Act - Should not throw
		var act = () => _database.AddMapping(longString, "mapped", SymbolType.Variable, longContext, 0.8);
		act.Should().NotThrow();

		// Assert
		var result = _database.GetMapping(longString, longContext);
		result.Should().NotBeNull();
		result!.Original.Should().Be(longString);
		result.Context.Should().Be(longContext);
	}

	[Fact]
	public void SearchMappings_Should_Handle_Complex_Regex_Patterns() {
		// Arrange
		_database.AddMapping("Wu1", "ReactModule", SymbolType.Class, "global", 0.9);
		_database.AddMapping("Wu2_test", "ReactComponent", SymbolType.Class, "global", 0.8);
		_database.AddMapping("Bx1", "DatabaseConnection", SymbolType.Class, "global", 0.9);
		_database.AddMapping("render_v2", "renderUI", SymbolType.Method, "Wu1", 0.8);

		// Act & Assert - Complex patterns should work
		var results1 = _database.SearchMappings(@"^Wu\d+$").ToList();
		results1.Should().HaveCount(1); // Only Wu1, not Wu2_test

		var results2 = _database.SearchMappings(@"Wu\d+.*").ToList();
		results2.Should().HaveCount(2); // Both Wu1 and Wu2_test

		var results3 = _database.SearchMappings(@".*_v?\d+$").ToList();
		results3.Should().HaveCount(1); // Only render_v2

		var results4 = _database.SearchMappings(@"(?i)react").ToList(); // Case insensitive
		results4.Should().HaveCount(2);                                 // ReactModule and ReactComponent
	}

	[Fact]
	public void UpdateMapping_Should_Handle_Type_Changes() {
		// Arrange
		const string symbol = "testSymbol";
		_database.AddMapping(symbol, "initialMapping", SymbolType.Variable, "global", 0.7);

		// Act - Update with different type
		_database.AddMapping(symbol, "updatedMapping", SymbolType.Method, "global", 0.8);

		// Assert - Should update type and mapping
		var result = _database.GetMapping(symbol, "global");
		result.Should().NotBeNull();
		result!.Type.Should().Be(SymbolType.Method);
		result.Mapped.Should().Be("updatedMapping");
		result.Confidence.Should().Be(0.8); // Higher confidence should be used
		result.UsageCount.Should().Be(2);   // Should increment
	}

#endregion
}