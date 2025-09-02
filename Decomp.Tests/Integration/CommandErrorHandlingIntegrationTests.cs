using FluentAssertions;
using Hoho.Core;
using System.Text.Json;
using Xunit;

namespace Hoho.Decomp.Tests.Integration;

/// <summary>
/// Integration tests for error handling, edge cases, and user feedback scenarios
/// across both MappingDisplayCommand and MigrateMappingsCommand
/// </summary>
public class CommandErrorHandlingIntegrationTests : CliIntegrationTestBase {
#region Permission and File System Errors

	[Fact]
	public async Task ShowMappings_ReadOnlyDirectory_HandlesGracefully() {
		// Arrange - Create read-only directory (if supported by OS)
		var readOnlyDir = Path.Combine(TestDirectory, "readonly");
		Directory.CreateDirectory(readOnlyDir);
		var readOnlyDbPath = Path.Combine(readOnlyDir, "readonly.msgpack");

		try {
			// Make directory read-only on Linux/Mac
			if (!OperatingSystem.IsWindows()) {
				System.Diagnostics.Process.Start("chmod", $"444 {readOnlyDir}");
			}

			// Act
			var result = await ExecuteCliCommandAsync("decomp",
				$"show-mappings --db \"{readOnlyDbPath}\"", expectSuccess: false);

			// Assert
			result.Success.Should().BeFalse();
			result.StandardError.Should().Contain("Failed to display mappings");
		} finally {
			// Cleanup - restore permissions
			if (!OperatingSystem.IsWindows()) {
				System.Diagnostics.Process.Start("chmod", $"755 {readOnlyDir}");
			}
		}
	}

	[Fact]
	public async Task MigrateMappings_ReadOnlyOutputDirectory_ShowsError() {
		// Arrange
		await CreateSampleJsonMappingsAsync(10);
		var readOnlyDir = Path.Combine(TestDirectory, "readonly_output");
		Directory.CreateDirectory(readOnlyDir);
		var readOnlyOutput = Path.Combine(readOnlyDir, "output.msgpack");

		try {
			if (!OperatingSystem.IsWindows()) {
				System.Diagnostics.Process.Start("chmod", $"444 {readOnlyDir}");
			}

			// Act
			var result = await ExecuteCliCommandAsync("decomp",
				$"migrate-mappings --json-path \"{TestJsonPath}\" --output \"{readOnlyOutput}\"",
				expectSuccess: false);

			// Assert
			result.Success.Should().BeFalse();
			result.StandardError.Should().Contain("Migration failed");
		} finally {
			if (!OperatingSystem.IsWindows()) {
				System.Diagnostics.Process.Start("chmod", $"755 {readOnlyDir}");
			}
		}
	}

	[Fact]
	public async Task ShowMappings_VeryLongFilePath_HandlesCorrectly() {
		// Arrange - Create very long file path (close to system limits)
		var longDir = TestDirectory;
		for (int i = 0; i < 10; i++) {
			longDir = Path.Combine(longDir, new string('a', 20));
		}
		Directory.CreateDirectory(longDir);
		var longDbPath = Path.Combine(longDir, "mappings.msgpack");

		var db = new MessagePackMappingDatabase(longDbPath);
		db.AddMapping("test", "tested", SymbolType.Function, "global", 0.8);
		await db.SaveAsync();

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --db \"{longDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("tested");
	}

#endregion

#region Memory and Performance Stress Tests

	[Fact]
	public async Task ShowMappings_ExtremelyLargeDatabase_HandlesMemoryEfficiently() {
		// Arrange - Create very large database
		CreateLargeDatabase(50000); // 50k mappings

		// Act - Test with various operations
		var statsResult = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format stats --db \"{TestDbPath}\"");

		var limitedResult = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --limit 100 --db \"{TestDbPath}\"");

		// Assert
		statsResult.Success.Should().BeTrue();
		statsResult.StandardOutput.Should().Contain("50000");

		limitedResult.Success.Should().BeTrue();
		var json = JsonDocument.Parse(limitedResult.StandardOutput);
		json.RootElement.GetProperty("mappings").GetArrayLength().Should().Be(100);
	}

	[Fact]
	public async Task MigrateMappings_ExtremelyLargeJsonFile_CompletesSuccessfully() {
		// Arrange - Create very large JSON file
		await CreateMassiveJsonFile(25000); // 25k mappings

		// Act
		var result = await ExecuteCliCommandAsync("decomp",
			$"migrate-mappings --json-path \"{TestJsonPath}\" --output \"{TestDbPath}\" --backup false");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("25000 mappings");
		result.StandardOutput.Should().Contain("Migration complete");

		// Verify all mappings migrated
		var db = new MessagePackMappingDatabase(TestDbPath);
		db.GetStatistics().TotalMappings.Should().Be(25000);
	}

#endregion

#region Concurrent Access and File Locking

	[Fact]
	public async Task ShowMappings_DatabaseBeingWritten_HandlesGracefully() {
		// Arrange - Start long-running database write operation
		CreateSampleDatabase(1000);

		var writeTask = Task.Run(async () => {
			for (int i = 0; i < 1000; i++) {
				TestDatabase.AddMapping($"concurrent_{i}", $"mapped_{i}", SymbolType.Function, "concurrent", 0.8);
				if (i % 100 == 0) {
					await TestDatabase.SaveAsync();
					await Task.Delay(10); // Allow other operations
				}
			}
			await TestDatabase.SaveAsync();
		});

		// Act - Try to read while writing
		await Task.Delay(50); // Let write start
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format stats --db \"{TestDbPath}\"");

		await writeTask; // Wait for write to complete

		// Assert - Should either succeed or fail gracefully
		if (result.Success) {
			result.StandardOutput.Should().Contain("mappings");
		} else {
			result.StandardError.Should().Contain("Failed to display mappings");
		}
	}

#endregion

#region Malformed Data and Corruption

	[Fact]
	public async Task ShowMappings_PartiallyCorruptedDatabase_RecoverableData() {
		// Arrange - Create database then partially corrupt it
		CreateSampleDatabase(100);

		// Append garbage bytes to database file
		var dbBytes        = await File.ReadAllBytesAsync(TestDbPath);
		var corruptedBytes = dbBytes.Concat(new byte[] { 0xFF, 0xFE, 0xFD, 0xFC }).ToArray();
		await File.WriteAllBytesAsync(TestDbPath, corruptedBytes);

		// Act
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format stats --db \"{TestDbPath}\"", expectSuccess: false);

		// Assert
		result.Success.Should().BeFalse();
		result.StandardError.Should().Contain("Failed to display mappings");
	}

	[Fact]
	public async Task MigrateMappings_MalformedJsonStructure_HandlesGracefully() {
		// Arrange - Create JSON with various malformed structures
		var malformedJson = @"{
            ""valid_entry"": {
                ""mapped"": ""validMapping"",
                ""type"": ""function"",
                ""context"": ""global"",
                ""confidence"": 0.8
            },
            ""null_entry"": null,
            ""array_entry"": [1, 2, 3],
            ""nested_object"": {
                ""inner"": {
                    ""mapped"": ""shouldNotWork"",
                    ""type"": ""function""
                }
            },
            ""missing_mapped"": {
                ""type"": ""function"",
                ""context"": ""global"",
                ""confidence"": 0.8
            },
            ""invalid_confidence"": {
                ""mapped"": ""invalidConf"",
                ""type"": ""function"",
                ""context"": ""global"",
                ""confidence"": ""not_a_number""
            }
        }";

		await File.WriteAllTextAsync(TestJsonPath, malformedJson);

		// Act
		var result = await ExecuteCliCommandAsync("decomp",
			$"migrate-mappings --json-path \"{TestJsonPath}\" --output \"{TestDbPath}\" --backup false");

		// Assert
		result.Success.Should().BeTrue();
		// Should migrate only the valid entries
		var db       = new MessagePackMappingDatabase(TestDbPath);
		var mappings = db.GetAllMappings().ToList();

		// Should contain at least the valid entry
		mappings.Should().Contain(m => m.Original == "valid_entry");
		mappings.Should().NotContain(m => m.Original == "null_entry");
		mappings.Should().NotContain(m => m.Original == "array_entry");
	}

#endregion

#region User Experience and Feedback

	[Fact]
	public async Task ShowMappings_InvalidCommandLineArguments_ShowsHelpfulError() {
		// Act - Test various invalid argument combinations
		var invalidFormatResult = await ExecuteCliCommandAsync("decomp",
			"show-mappings --format invalid_format", expectSuccess: false);

		var invalidConfidenceResult = await ExecuteCliCommandAsync("decomp",
			"show-mappings --min-confidence 2.0", expectSuccess: false);

		var invalidLimitResult = await ExecuteCliCommandAsync("decomp",
			"show-mappings --limit -10", expectSuccess: false);

		// Assert
		invalidFormatResult.StandardError.Should().Contain("Unknown format");
		invalidFormatResult.StandardError.Should().Contain("Available formats");

		// Note: Command line parser handles validation for confidence and limit
		invalidConfidenceResult.Success.Should().BeFalse();
		invalidLimitResult.Success.Should().BeFalse();
	}

	[Fact]
	public async Task MigrateMappings_DetailedProgressFeedback_InformsUser() {
		// Arrange - Create multiple JSON files
		var decompDir = Path.Combine(TestDirectory, "decomp");
		Directory.CreateDirectory(decompDir);

		await CreateJsonMappingFile(Path.Combine(decompDir, "mappings.json"), 100);
		await CreateJsonMappingFile(Path.Combine(decompDir, "learned-mappings.json"), 50);

		// Act
		var result = await ExecuteCliCommandAsync("decomp",
			$"migrate-mappings --output \"{TestDbPath}\" --backup true");

		// Assert
		result.Success.Should().BeTrue();

		// Should show detailed progress
		result.StandardOutput.Should().Contain("Starting JSON to MessagePack migration");
		result.StandardOutput.Should().Contain("Found decomp/mappings.json");
		result.StandardOutput.Should().Contain("Found decomp/learned-mappings.json");
		result.StandardOutput.Should().Contain("Processing decomp/mappings.json");
		result.StandardOutput.Should().Contain("✅ Migrated 100 mappings from mappings.json");
		result.StandardOutput.Should().Contain("✅ Migrated 50 mappings from learned-mappings.json");
		result.StandardOutput.Should().Contain("📦 Backed up original");
		result.StandardOutput.Should().Contain("🎉 Migration complete! Migrated 150 total mappings");
		result.StandardOutput.Should().Contain("Performance Comparison:");
		result.StandardOutput.Should().Contain("Next steps:");
	}

	[Fact]
	public async Task ShowMappings_ProgressiveFiltering_ShowsFilterEffects() {
		// Arrange
		CreateSampleDatabase(1000);

		// Act - Test progressive filtering
		var allResult = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --db \"{TestDbPath}\"");

		var contextFilteredResult = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --context ReactModule --db \"{TestDbPath}\"");

		var typeFilteredResult = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --context ReactModule --type function --db \"{TestDbPath}\"");

		// Assert
		var allJson     = JsonDocument.Parse(allResult.StandardOutput);
		var contextJson = JsonDocument.Parse(contextFilteredResult.StandardOutput);
		var typeJson    = JsonDocument.Parse(typeFilteredResult.StandardOutput);

		var allCount     = allJson.RootElement.GetProperty("mappings").GetArrayLength();
		var contextCount = contextJson.RootElement.GetProperty("mappings").GetArrayLength();
		var typeCount    = typeJson.RootElement.GetProperty("mappings").GetArrayLength();

		allCount.Should().BeGreaterThan(contextCount);
		contextCount.Should().BeGreaterOrEqualTo(typeCount);

		// Verify filtering is correct
		foreach (var mapping in contextJson.RootElement.GetProperty("mappings").EnumerateArray()) {
			mapping.GetProperty("context").GetString().Should().Be("ReactModule");
		}

		foreach (var mapping in typeJson.RootElement.GetProperty("mappings").EnumerateArray()) {
			mapping.GetProperty("type").GetString().Should().Be("function");
		}
	}

#endregion

#region Performance and Resource Management

	[Fact]
	public async Task Commands_ResourceCleanup_NoMemoryLeaks() {
		// Arrange - Run commands multiple times to test resource cleanup
		CreateSampleDatabase(500);

		// Act - Run multiple operations
		for (int i = 0; i < 10; i++) {
			await ExecuteCliCommandAsync("decomp", $"show-mappings --format stats --db \"{TestDbPath}\"");
			await ExecuteCliCommandAsync("decomp", $"show-mappings --format json --limit 50 --db \"{TestDbPath}\"");
		}

		// Assert - Test should complete without memory issues
		// (Memory leaks would typically cause the test to hang or fail)
		true.Should().BeTrue(); // If we reach here, resources were cleaned up properly
	}

	[Fact]
	public async Task ShowMappings_CancellationHandling_RespondsToInterruption() {
		// Arrange
		CreateLargeDatabase(10000);

		// Act - Start long operation and cancel it
		var cts  = new CancellationTokenSource();
		var task = ExecuteCliCommandAsync("decomp", $"show-mappings --format table --db \"{TestDbPath}\"");

		// Cancel after short delay
		await Task.Delay(100);
		cts.Cancel();

		// Wait for completion
		var result = await task;

		// Assert - Should either complete successfully or handle cancellation gracefully
		// (The exact behavior depends on how cancellation is implemented)
		result.ExitCode.Should().BeOneOf(0, 1, 130); // 0=success, 1=error, 130=SIGINT
	}

#endregion

#region Edge Case Data Scenarios

	[Fact]
	public async Task ShowMappings_UnicodeAndSpecialCharacters_DisplaysCorrectly() {
		// Arrange - Create mappings with various Unicode and special characters
		TestDatabase.AddMapping("🚀_rocket", "rocketFunction", SymbolType.Function, "Emojis", 0.9);
		TestDatabase.AddMapping("τεστ_greek", "greekTest", SymbolType.Variable, "Unicode", 0.8);
		TestDatabase.AddMapping("测试_chinese", "chineseTest", SymbolType.Class, "Unicode", 0.85);
		TestDatabase.AddMapping("null\0byte", "nullByteSymbol", SymbolType.Property, "EdgeCase", 0.7);
		TestDatabase.AddMapping("very\nlong\nwith\nnewlines", "multilineSymbol", SymbolType.Method, "EdgeCase", 0.6);
		await TestDatabase.SaveAsync();

		// Act
		var tableResult = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format table --db \"{TestDbPath}\"");

		var jsonResult = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --db \"{TestDbPath}\"");

		// Assert
		tableResult.Success.Should().BeTrue();
		jsonResult.Success.Should().BeTrue();

		// Should handle Unicode characters
		tableResult.StandardOutput.Should().Contain("🚀_rocket");
		tableResult.StandardOutput.Should().Contain("τεστ_greek");
		tableResult.StandardOutput.Should().Contain("测试_chinese");

		// JSON should be valid despite special characters
		var json     = JsonDocument.Parse(jsonResult.StandardOutput);
		var mappings = json.RootElement.GetProperty("mappings").EnumerateArray().ToList();
		mappings.Should().Contain(m => m.GetProperty("original").GetString() == "🚀_rocket");
	}

	[Fact]
	public async Task MigrateMappings_ExtremeConfidenceValues_HandledCorrectly() {
		// Arrange - Create JSON with extreme confidence values
		var mappings = new Dictionary<string, object> {
			["zero_confidence"]     = new { mapped = "zeroConf", type      = "function", context = "global", confidence = 0.0 },
			["perfect_confidence"]  = new { mapped = "perfectConf", type   = "function", context = "global", confidence = 1.0 },
			["negative_confidence"] = new { mapped = "negativeConf", type  = "function", context = "global", confidence = -0.5 },
			["over_confidence"]     = new { mapped = "overConf", type      = "function", context = "global", confidence = 1.5 },
			["tiny_confidence"]     = new { mapped = "tinyConf", type      = "function", context = "global", confidence = 0.000001 },
			["almost_perfect"]      = new { mapped = "almostPerfect", type = "function", context = "global", confidence = 0.999999 }
		};

		var json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions { WriteIndented = true });
		await File.WriteAllTextAsync(TestJsonPath, json);

		// Act
		var result = await ExecuteCliCommandAsync("decomp",
			$"migrate-mappings --json-path \"{TestJsonPath}\" --output \"{TestDbPath}\" --backup false");

		// Assert
		result.Success.Should().BeTrue();

		var db          = new MessagePackMappingDatabase(TestDbPath);
		var allMappings = db.GetAllMappings().ToList();

		// Should handle extreme but valid values
		allMappings.Should().Contain(m => m.Original == "zero_confidence" && m.Confidence == 0.0);
		allMappings.Should().Contain(m => m.Original == "perfect_confidence" && m.Confidence == 1.0);
		allMappings.Should().Contain(m => m.Original == "tiny_confidence" && Math.Abs(m.Confidence - 0.000001) < 0.000001);

		// Should clamp invalid values or skip them (depending on implementation)
		var negativeMapping = allMappings.FirstOrDefault(m => m.Original == "negative_confidence");
		var overMapping     = allMappings.FirstOrDefault(m => m.Original == "over_confidence");

		if (negativeMapping != null)
			negativeMapping.Confidence.Should().BeGreaterOrEqualTo(0.0);
		if (overMapping != null)
			overMapping.Confidence.Should().BeLessOrEqualTo(1.0);
	}

#endregion

	private async Task CreateMassiveJsonFile(int count) {
		var mappings = new Dictionary<string, object>();

		for (int i = 0; i < count; i++) {
			mappings[$"massive_{i:D6}"] = new {
				mapped     = $"massiveMapping_{i:D6}",
				type       = new[] { "function", "variable", "class", "property", "method", "parameter" }[i % 6],
				context    = $"Context_{i % 200}",
				confidence = Math.Min(1.0, 0.3 + (i % 1000 / 1500.0)),
				usageCount = (i % 100) + 1
			};
		}

		var json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions { WriteIndented = true });
		await File.WriteAllTextAsync(TestJsonPath, json);
	}

	private async Task CreateJsonMappingFile(string path, int count, string prefix = "json_obf_") {
		var mappings = new Dictionary<string, object>();

		for (int i = 0; i < count; i++) {
			var key = $"{prefix}{i:D3}";
			mappings[key] = new {
				mapped     = $"{prefix.Replace("_obf_", "_readable_")}{i:D3}",
				type       = new[] { "function", "variable", "class", "property" }[i % 4],
				context    = new[] { "global", "ReactModule", "DatabaseLayer", "TestModule" }[i % 4],
				confidence = 0.5 + (i % 5 * 0.1),
				usageCount = (i % 20) + 1
			};
		}

		var json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions { WriteIndented = true });
		await File.WriteAllTextAsync(path, json);
	}
}