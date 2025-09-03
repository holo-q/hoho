using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Hoho.Core;
using Xunit;
using Xunit.Abstractions;

namespace Hoho.Decomp.Tests.Integration;

/// <summary>
/// Comprehensive integration test suite that orchestrates all CLI tests
/// and provides detailed reporting on command functionality and performance
/// </summary>
public class FullIntegrationTestSuite : CliIntegrationTestBase {
	private readonly ITestOutputHelper         _output;
	private readonly List<TestExecutionResult> _testResults = new();

	public FullIntegrationTestSuite(ITestOutputHelper output) {
		_output = output;
	}

	[Fact]
	public async Task FullWorkflow_EndToEndScenario_CompletesSuccessfully() {
		var stopwatch = Stopwatch.StartNew();
		_output.WriteLine("🚀 Starting Full Integration Test Suite");
		_output.WriteLine("=====================================");

		try {
			// Phase 1: Setup and Data Preparation
			await LoggedTestStep("Setup Test Environment", SetupTestEnvironment);

			// Phase 2: Migration Workflow Tests
			await LoggedTestStep("JSON to MessagePack Migration", TestJsonToMessagePackMigration);
			await LoggedTestStep("Multiple File Migration", TestMultipleFileMigration);
			await LoggedTestStep("Migration with Backup", TestMigrationWithBackup);

			// Phase 3: Display Command Tests - All Formats
			await LoggedTestStep("Table Format Display", () => TestDisplayFormat("table"));
			await LoggedTestStep("Tree Format Display", () => TestDisplayFormat("tree"));
			await LoggedTestStep("JSON Format Display", () => TestDisplayFormat("json"));
			await LoggedTestStep("Markdown Format Display", () => TestDisplayFormat("markdown"));
			await LoggedTestStep("Statistics Display", () => TestDisplayFormat("stats"));

			// Phase 4: Filtering and Search Tests
			await LoggedTestStep("Context Filtering", TestContextFiltering);
			await LoggedTestStep("Type Filtering", TestTypeFiltering);
			await LoggedTestStep("Search Functionality", TestSearchFunctionality);
			await LoggedTestStep("Combined Filters", TestCombinedFilters);
			await LoggedTestStep("Confidence Filtering", TestConfidenceFiltering);

			// Phase 5: Performance Tests
			await LoggedTestStep("Large Database Performance", TestLargeDatabasePerformance);
			await LoggedTestStep("Memory Usage Test", TestMemoryUsage);

			// Phase 6: Error Handling Tests
			await LoggedTestStep("Error Recovery", TestErrorRecovery);
			await LoggedTestStep("Invalid Input Handling", TestInvalidInputHandling);
			await LoggedTestStep("Corrupted Data Handling", TestCorruptedDataHandling);

			// Phase 7: Advanced Scenarios
			await LoggedTestStep("Unicode and Special Characters", TestUnicodeHandling);
			await LoggedTestStep("Concurrent Access", TestConcurrentAccess);
			await LoggedTestStep("Resource Cleanup", TestResourceCleanup);

			stopwatch.Stop();
			GenerateTestReport(stopwatch.Elapsed);
		} catch (Exception ex) {
			_output.WriteLine($"❌ Test suite failed: {ex.Message}");
			throw;
		}
	}

	private async Task LoggedTestStep(string stepName, Func<Task> testAction) {
		var stepStopwatch = Stopwatch.StartNew();
		_output.WriteLine($"\n🔄 {stepName}...");

		try {
			await testAction();
			stepStopwatch.Stop();

			var result = new TestExecutionResult {
				TestName      = stepName,
				Success       = true,
				ExecutionTime = stepStopwatch.Elapsed,
				Message       = "Completed successfully"
			};
			_testResults.Add(result);

			_output.WriteLine($"✅ {stepName} completed in {stepStopwatch.ElapsedMilliseconds}ms");
		} catch (Exception ex) {
			stepStopwatch.Stop();

			var result = new TestExecutionResult {
				TestName      = stepName,
				Success       = false,
				ExecutionTime = stepStopwatch.Elapsed,
				Message       = ex.Message
			};
			_testResults.Add(result);

			_output.WriteLine($"❌ {stepName} failed in {stepStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
			throw;
		}
	}

	private async Task SetupTestEnvironment() {
		// Create comprehensive test database
		CreateSampleDatabase(1000);

		// Create sample JSON files for migration testing
		await CreateSampleJsonMappingsAsync(500);
		await CreateComplexJsonMappingsAsync();

		_output.WriteLine("   📊 Created database with 1000 mappings");
		_output.WriteLine("   📄 Created JSON files for migration testing");
	}

	private async Task TestJsonToMessagePackMigration() {
		var result = await ExecuteCliCommandAsync("decomp",
			$"migrate-mappings --json-path \"{TestJsonPath}\" --output \"{Path.Combine(TestDirectory, "migrated.msgpack")}\" --backup false");

		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("Migration complete");
		result.StandardOutput.Should().Contain("Performance Comparison");

		_output.WriteLine("   📈 Migration performance comparison displayed");
	}

	private async Task TestMultipleFileMigration() {
		// Create multiple JSON files
		var decompDir = Path.Combine(TestDirectory, "decomp");
		Directory.CreateDirectory(decompDir);

		await CreateJsonMappingFile(Path.Combine(decompDir, "mappings.json"), 100);
		await CreateJsonMappingFile(Path.Combine(decompDir, "learned-mappings.json"), 75);

		var result = await ExecuteCliCommandAsync("decomp",
			$"migrate-mappings --output \"{Path.Combine(TestDirectory, "multi.msgpack")}\" --backup false");

		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("175 total mappings");

		_output.WriteLine("   🔗 Successfully migrated from multiple JSON files");
	}

	private async Task TestMigrationWithBackup() {
		var backupTestPath = Path.Combine(TestDirectory, "backup-test.json");
		await CreateJsonMappingFile(backupTestPath, 25);

		var result = await ExecuteCliCommandAsync("decomp",
			$"migrate-mappings --json-path \"{backupTestPath}\" --output \"{Path.Combine(TestDirectory, "backup.msgpack")}\" --backup true");

		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("Backed up original");

		// Verify backup file exists
		var backupFiles = Directory.GetFiles(TestDirectory, "backup-test.json.backup.*");
		backupFiles.Should().HaveCount(1);

		_output.WriteLine($"   💾 Backup created: {Path.GetFileName(backupFiles[0])}");
	}

	private async Task TestDisplayFormat(string format) {
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format {format} --limit 50 --db \"{TestDbPath}\"");

		result.Success.Should().BeTrue();
		result.StandardOutput.Should().NotBeEmpty();

		// Format-specific validations
		switch (format.ToLower()) {
			case "json":
				var json = JsonDocument.Parse(ExtractJsonFromOutput(result.StandardOutput));
				json.RootElement.TryGetProperty("mappings", out _).Should().BeTrue();
				_output.WriteLine("   ✓ Valid JSON structure");
				break;

			case "table":
				result.StandardOutput.Should().Contain("┌");
				result.StandardOutput.Should().Contain("Original Symbol");
				_output.WriteLine("   ✓ Table formatting correct");
				break;

			case "tree":
				result.StandardOutput.Should().Contain("├──");
				result.StandardOutput.Should().Contain("📂");
				_output.WriteLine("   ✓ Tree structure displayed");
				break;

			case "stats":
				result.StandardOutput.Should().Contain("DATABASE STATISTICS");
				result.StandardOutput.Should().Contain("Distribution by Type");
				_output.WriteLine("   ✓ Statistics and distributions shown");
				break;

			case "markdown":
				result.StandardOutput.Should().Contain("#");
				_output.WriteLine("   ✓ Markdown format generated");
				break;
		}
	}

	private async Task TestContextFiltering() {
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --context ReactModule --db \"{TestDbPath}\"");

		result.Success.Should().BeTrue();

		var json     = JsonDocument.Parse(ExtractJsonFromOutput(result.StandardOutput));
		var mappings = json.RootElement.GetProperty("mappings").EnumerateArray();

		foreach (var mapping in mappings) {
			mapping.GetProperty("context").GetString().Should().Be("ReactModule");
		}

		_output.WriteLine("   🎯 Context filtering working correctly");
	}

	private async Task TestTypeFiltering() {
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --type function --db \"{TestDbPath}\"");

		result.Success.Should().BeTrue();

		var json     = JsonDocument.Parse(ExtractJsonFromOutput(result.StandardOutput));
		var mappings = json.RootElement.GetProperty("mappings").EnumerateArray();

		foreach (var mapping in mappings) {
			mapping.GetProperty("type").GetString().Should().Be("function");
		}

		_output.WriteLine("   🏷️ Type filtering working correctly");
	}

	private async Task TestSearchFunctionality() {
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --search \"obf_0[0-5]\" --db \"{TestDbPath}\"");

		result.Success.Should().BeTrue();

		var json     = JsonDocument.Parse(ExtractJsonFromOutput(result.StandardOutput));
		var mappings = json.RootElement.GetProperty("mappings").EnumerateArray().ToList();

		mappings.Should().NotBeEmpty();
		mappings.Should().OnlyContain(m =>
			System.Text.RegularExpressions.Regex.IsMatch(
				m.GetProperty("original").GetString()!, @"obf_0[0-5]"));

		_output.WriteLine($"   🔍 Search found {mappings.Count} matching symbols");
	}

	private async Task TestCombinedFilters() {
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --context ReactModule --type function --min-confidence 0.7 --limit 10 --db \"{TestDbPath}\"");

		result.Success.Should().BeTrue();

		var json     = JsonDocument.Parse(ExtractJsonFromOutput(result.StandardOutput));
		var mappings = json.RootElement.GetProperty("mappings").EnumerateArray().ToList();

		mappings.Should().HaveCountLessOrEqualTo(10);

		foreach (var mapping in mappings) {
			mapping.GetProperty("context").GetString().Should().Be("ReactModule");
			mapping.GetProperty("type").GetString().Should().Be("function");
			mapping.GetProperty("confidence").GetDouble().Should().BeGreaterOrEqualTo(0.7);
		}

		_output.WriteLine($"   ⚙️ Combined filters returned {mappings.Count} results");
	}

	private async Task TestConfidenceFiltering() {
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --min-confidence 0.8 --db \"{TestDbPath}\"");

		result.Success.Should().BeTrue();

		var json     = JsonDocument.Parse(ExtractJsonFromOutput(result.StandardOutput));
		var mappings = json.RootElement.GetProperty("mappings").EnumerateArray();

		foreach (var mapping in mappings) {
			mapping.GetProperty("confidence").GetDouble().Should().BeGreaterOrEqualTo(0.8);
		}

		_output.WriteLine("   🎯 Confidence filtering working correctly");
	}

	private async Task TestLargeDatabasePerformance() {
		// Create large database for performance testing
		var largeDbPath = Path.Combine(TestDirectory, "large.msgpack");
		var largeDb     = new MessagePackMappingDatabase(largeDbPath);

		// Add 10k mappings
		for (int i = 0; i < 10000; i++) {
			largeDb.AddMapping($"perf_{i:D5}", $"performance_{i:D5}",
				(SymbolType)(i % 6), $"Context_{i % 50}", 0.5 + (i % 50 / 100.0));
		}
		await largeDb.SaveAsync();

		var stopwatch = Stopwatch.StartNew();
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format stats --db \"{largeDbPath}\"");
		stopwatch.Stop();

		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("10000");

		// Should complete within reasonable time (less than 5 seconds)
		stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));

		_output.WriteLine($"   ⚡ Large database (10k mappings) processed in {stopwatch.ElapsedMilliseconds}ms");
	}

	private async Task TestMemoryUsage() {
		var initialMemory = GC.GetTotalMemory(false);

		// Perform several memory-intensive operations
		for (int i = 0; i < 5; i++) {
			await ExecuteCliCommandAsync("decomp", $"show-mappings --format json --db \"{TestDbPath}\"");
			await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --limit 100 --db \"{TestDbPath}\"");
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		var finalMemory    = GC.GetTotalMemory(false);
		var memoryIncrease = finalMemory - initialMemory;

		// Memory increase should be reasonable (less than 50MB)
		memoryIncrease.Should().BeLessThan(50 * 1024 * 1024);

		_output.WriteLine($"   💾 Memory increase after operations: {memoryIncrease / 1024 / 1024:F1}MB");
	}

	private async Task TestErrorRecovery() {
		// Test with non-existent database
		var result = await ExecuteCliCommandAsync("decomp",
			"show-mappings --db \"/non/existent/path.msgpack\"", expectSuccess: false);

		result.Success.Should().BeFalse();
		result.StandardError.Should().Contain("Failed to display mappings");

		_output.WriteLine("   🛡️ Error recovery handled gracefully");
	}

	private async Task TestInvalidInputHandling() {
		// Test invalid format
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format invalid --db \"{TestDbPath}\"", expectSuccess: false);

		result.Success.Should().BeFalse();
		result.StandardError.Should().Contain("Unknown format");

		_output.WriteLine("   ⚠️ Invalid input handled properly");
	}

	private async Task TestCorruptedDataHandling() {
		// Create corrupted database
		var corruptDbPath = Path.Combine(TestDirectory, "corrupt.msgpack");
		await File.WriteAllBytesAsync(corruptDbPath, new byte[] { 0xFF, 0xFE, 0xFD });

		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --db \"{corruptDbPath}\"", expectSuccess: false);

		result.Success.Should().BeFalse();
		result.StandardError.Should().Contain("Failed to display mappings");

		_output.WriteLine("   🔧 Corrupted data handled gracefully");
	}

	private async Task TestUnicodeHandling() {
		// Add Unicode mappings
		var unicodeDb = new MessagePackMappingDatabase(Path.Combine(TestDirectory, "unicode.msgpack"));
		unicodeDb.AddMapping("🚀_test", "rocketTest", SymbolType.Function, "Unicode", 0.9);
		unicodeDb.AddMapping("τεστ", "greekTest", SymbolType.Variable, "Unicode", 0.8);
		await unicodeDb.SaveAsync();

		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format table --db \"{Path.Combine(TestDirectory, "unicode.msgpack")}\"");

		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("🚀_test");
		result.StandardOutput.Should().Contain("τεστ");

		_output.WriteLine("   🌍 Unicode characters handled correctly");
	}

	private async Task TestConcurrentAccess() {
		// Simulate concurrent reads
		var tasks = new List<Task<CliCommandResult>>();

		for (int i = 0; i < 3; i++) {
			tasks.Add(ExecuteCliCommandAsync("decomp", $"show-mappings --format json --limit 50 --db \"{TestDbPath}\""));
		}

		var results = await Task.WhenAll(tasks);

		results.Should().AllSatisfy(r => r.Success.Should().BeTrue());

		_output.WriteLine("   🔄 Concurrent access handled properly");
	}

	private async Task TestResourceCleanup() {
		var filesBefore = Directory.GetFiles(TestDirectory, "*.tmp").Length;

		// Perform operations that might create temp files
		for (int i = 0; i < 3; i++) {
			await ExecuteCliCommandAsync("decomp", $"show-mappings --format stats --db \"{TestDbPath}\"");
		}

		var filesAfter = Directory.GetFiles(TestDirectory, "*.tmp").Length;

		// Should not leave temporary files
		filesAfter.Should().Be(filesBefore);

		_output.WriteLine("   🧹 No temporary files left behind");
	}

	private void GenerateTestReport(TimeSpan totalTime) {
		_output.WriteLine("\n🎉 Full Integration Test Suite Completed!");
		_output.WriteLine("==========================================");
		_output.WriteLine($"Total execution time: {totalTime.TotalMinutes:F2} minutes");
		_output.WriteLine($"Tests executed: {_testResults.Count}");
		_output.WriteLine($"Tests passed: {_testResults.Count(r => r.Success)}");
		_output.WriteLine($"Tests failed: {_testResults.Count(r => !r.Success)}");

		if (_testResults.Any(r => !r.Success)) {
			_output.WriteLine("\n❌ Failed Tests:");
			foreach (var failure in _testResults.Where(r => !r.Success)) {
				_output.WriteLine($"   - {failure.TestName}: {failure.Message}");
			}
		}

		_output.WriteLine("\n⏱️ Performance Summary:");
		var slowestTests = _testResults.OrderByDescending(r => r.ExecutionTime).Take(3);
		foreach (var test in slowestTests) {
			_output.WriteLine($"   {test.TestName}: {test.ExecutionTime.TotalMilliseconds:F0}ms");
		}

		_output.WriteLine("\n✅ All core CLI functionality verified:");
		_output.WriteLine("   • JSON to MessagePack migration");
		_output.WriteLine("   • All display formats (table, tree, json, markdown, stats)");
		_output.WriteLine("   • Filtering and search capabilities");
		_output.WriteLine("   • Error handling and recovery");
		_output.WriteLine("   • Performance with large datasets");
		_output.WriteLine("   • Unicode and special character support");
		_output.WriteLine("   • Resource management and cleanup");
	}

	private async Task CreateComplexJsonMappingsAsync() {
		var complexMappings = new Dictionary<string, object> {
			["complex_A1"] = new { mapped = "ReactComponent", type     = "class", context    = "ReactModule", confidence   = 0.95, usageCount = 150 },
			["complex_B2"] = new { mapped = "databaseConnection", type = "variable", context = "DatabaseLayer", confidence = 0.88, usageCount = 75 },
			["complex_C3"] = new { mapped = "handleUserInput", type    = "function", context = "EventHandler", confidence  = 0.92, usageCount = 200 },
			["complex_D4"] = new { mapped = "ValidationError", type    = "class", context    = "ErrorHandler", confidence  = 0.85, usageCount = 45 },
			["complex_E5"] = new { mapped = "formatResponse", type     = "method", context   = "UtilityClass", confidence  = 0.78, usageCount = 120 }
		};

		var json = JsonSerializer.Serialize(complexMappings, new JsonSerializerOptions { WriteIndented = true });
		await File.WriteAllTextAsync(Path.Combine(TestDirectory, "complex.json"), json);
	}

	private async Task CreateJsonMappingFile(string path, int count, string prefix = "json_") {
		var mappings = new Dictionary<string, object>();

		for (int i = 0; i < count; i++) {
			mappings[$"{prefix}{i:D3}"] = new {
				mapped     = $"{prefix}mapped_{i:D3}",
				type       = new[] { "function", "variable", "class", "property", "method" }[i % 5],
				context    = new[] { "global", "TestModule", "UtilityClass" }[i % 3],
				confidence = 0.6 + (i % 4 * 0.1),
				usageCount = (i % 10) + 1
			};
		}

		var json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions { WriteIndented = true });
		await File.WriteAllTextAsync(path, json);
	}

	private class TestExecutionResult {
		public string   TestName      { get; set; } = string.Empty;
		public bool     Success       { get; set; }
		public TimeSpan ExecutionTime { get; set; }
		public string   Message       { get; set; } = string.Empty;
	}
}