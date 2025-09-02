using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Hoho.Core;
using Xunit;

namespace Hoho.Decomp.Tests.Integration;

/// <summary>
/// Integration tests for MappingDisplayCommand CLI execution
/// Tests all output formats, filtering options, and error scenarios
/// </summary>
public class MappingDisplayCommandIntegrationTests : CliIntegrationTestBase {
	[Fact]
	public async Task ShowMappings_TableFormat_DisplaysFormattedTable() {
		// Arrange
		CreateSampleDatabase(50);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("SYMBOL MAPPING DATABASE");
		result.StandardOutput.Should().Contain("Original Symbol");
		result.StandardOutput.Should().Contain("Mapped Symbol");
		result.StandardOutput.Should().Contain("Type");
		result.StandardOutput.Should().Contain("Context");
		result.StandardOutput.Should().Contain("Confidence");
		result.StandardOutput.Should().Contain("Usage");

		// Should contain table borders
		result.StandardOutput.Should().Contain("┌─");
		result.StandardOutput.Should().Contain("├─");
		result.StandardOutput.Should().Contain("└─");

		// Should show statistics
		result.StandardOutput.Should().Contain("Total: 50");
		result.StandardOutput.Should().Contain("Top Contexts:");
	}

	[Fact]
	public async Task ShowMappings_TreeFormat_DisplaysHierarchicalView() {
		// Arrange
		CreateSampleDatabase(30);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format tree --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("Symbol Mapping Database");
		result.StandardOutput.Should().Contain("Statistics:");
		result.StandardOutput.Should().Contain("30 total mappings");
		result.StandardOutput.Should().Contain("Average confidence:");
		result.StandardOutput.Should().Contain("Mappings:");

		// Should contain tree structure
		result.StandardOutput.Should().Contain("├──");
		result.StandardOutput.Should().Contain("└──");
		result.StandardOutput.Should().Contain("📂");
		result.StandardOutput.Should().Contain("🏷️");

		// Should group by context and type
		result.StandardOutput.Should().Contain("global");
		result.StandardOutput.Should().Contain("ReactModule");
	}

	[Fact]
	public async Task ShowMappings_JsonFormat_ReturnsValidJson() {
		// Arrange
		CreateSampleDatabase(25);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format json --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();

		// Should be valid JSON
		var jsonDocument = JsonDocument.Parse(result.StandardOutput);
		jsonDocument.Should().NotBeNull();

		// Should have expected structure
		jsonDocument.RootElement.TryGetProperty("database", out var database).Should().BeTrue();
		database.TryGetProperty("totalMappings", out var totalMappings).Should().BeTrue();
		totalMappings.GetInt32().Should().Be(25);

		jsonDocument.RootElement.TryGetProperty("mappings", out var mappings).Should().BeTrue();
		mappings.GetArrayLength().Should().BeGreaterThan(0);

		// Check mapping structure
		var firstMapping = mappings.EnumerateArray().First();
		firstMapping.TryGetProperty("original", out _).Should().BeTrue();
		firstMapping.TryGetProperty("mapped", out _).Should().BeTrue();
		firstMapping.TryGetProperty("type", out _).Should().BeTrue();
		firstMapping.TryGetProperty("context", out _).Should().BeTrue();
		firstMapping.TryGetProperty("confidence", out _).Should().BeTrue();
	}

	[Fact]
	public async Task ShowMappings_MarkdownFormat_ReturnsMarkdownContent() {
		// Arrange
		CreateSampleDatabase(20);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format markdown --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("#"); // Markdown headers
		result.StandardOutput.Should().Contain("Symbol Mappings");
	}

	[Fact]
	public async Task ShowMappings_StatsFormat_DisplaysStatistics() {
		// Arrange
		CreateSampleDatabase(75);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format stats --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("DATABASE STATISTICS");
		result.StandardOutput.Should().Contain("Total Mappings:");
		result.StandardOutput.Should().Contain("75");
		result.StandardOutput.Should().Contain("Average Confidence:");
		result.StandardOutput.Should().Contain("High Confidence:");
		result.StandardOutput.Should().Contain("Last Modified:");

		// Should contain distribution charts
		result.StandardOutput.Should().Contain("Distribution by Type:");
		result.StandardOutput.Should().Contain("Distribution by Context:");
		result.StandardOutput.Should().Contain("Confidence Distribution:");

		// Should contain visual bars
		result.StandardOutput.Should().Contain("█");
	}

	[Fact]
	public async Task ShowMappings_ContextFilter_FiltersCorrectly() {
		// Arrange
		CreateSampleDatabase(100);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --context ReactModule --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("ReactModule");
		result.StandardOutput.Should().NotContain("global"); // Should not show other contexts
	}

	[Fact]
	public async Task ShowMappings_TypeFilter_FiltersCorrectly() {
		// Arrange
		CreateSampleDatabase(100);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --type function --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("function");

		// Check that only function types are shown
		var lines     = result.StandardOutput.Split('\n');
		var dataLines = lines.Where(l => l.Contains("obf_")).ToArray();
		foreach (var line in dataLines) {
			line.Should().Contain("function");
		}
	}

	[Fact]
	public async Task ShowMappings_SearchFilter_SupportsRegex() {
		// Arrange
		CreateSampleDatabase(50);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --search \"obf_0[1-3]\" --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();

		// Should only show matching symbols
		result.StandardOutput.Should().Contain("obf_001");
		result.StandardOutput.Should().Contain("obf_002");
		result.StandardOutput.Should().Contain("obf_003");
		result.StandardOutput.Should().NotContain("obf_004");
		result.StandardOutput.Should().NotContain("obf_000");
	}

	[Fact]
	public async Task ShowMappings_SearchFilter_FallsBackToStringSearch() {
		// Arrange
		CreateSampleDatabase(50);

		// Act - Use invalid regex syntax
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --search \"obf_0[1\" --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		// Should fallback to string search and find matches containing "obf_0[1"
		// (which won't match anything in our test data, but shouldn't crash)
	}

	[Fact]
	public async Task ShowMappings_LimitOption_LimitsResults() {
		// Arrange
		CreateSampleDatabase(100);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format json --limit 10 --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();

		var jsonDocument = JsonDocument.Parse(result.StandardOutput);
		jsonDocument.RootElement.TryGetProperty("mappings", out var mappings).Should().BeTrue();
		mappings.GetArrayLength().Should().BeLessOrEqualTo(10);
	}

	[Fact]
	public async Task ShowMappings_ConfidenceFilter_FiltersCorrectly() {
		// Arrange
		CreateSampleDatabase(50);

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format json --min-confidence 0.8 --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();

		var jsonDocument = JsonDocument.Parse(result.StandardOutput);
		jsonDocument.RootElement.TryGetProperty("mappings", out var mappings).Should().BeTrue();

		// Check that all returned mappings have confidence >= 0.8
		foreach (var mapping in mappings.EnumerateArray()) {
			mapping.TryGetProperty("confidence", out var confidence).Should().BeTrue();
			confidence.GetDouble().Should().BeGreaterOrEqualTo(0.8);
		}
	}

	[Fact]
	public async Task ShowMappings_CombinedFilters_AppliesAllFilters() {
		// Arrange
		CreateSampleDatabase(100);

		// Act
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format json --context ReactModule --type function --min-confidence 0.7 --limit 5 --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();

		var jsonDocument = JsonDocument.Parse(result.StandardOutput);
		jsonDocument.RootElement.TryGetProperty("mappings", out var mappings).Should().BeTrue();
		mappings.GetArrayLength().Should().BeLessOrEqualTo(5);

		foreach (var mapping in mappings.EnumerateArray()) {
			mapping.TryGetProperty("context", out var context).Should().BeTrue();
			context.GetString().Should().Be("ReactModule");

			mapping.TryGetProperty("type", out var type).Should().BeTrue();
			type.GetString().Should().Be("function");

			mapping.TryGetProperty("confidence", out var confidence).Should().BeTrue();
			confidence.GetDouble().Should().BeGreaterOrEqualTo(0.7);
		}
	}

	[Fact]
	public async Task ShowMappings_InvalidFormat_ShowsError() {
		// Arrange
		CreateSampleDatabase(10);

		// Act
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --format invalid --db \"{TestDbPath}\"", expectSuccess: false);

		// Assert
		result.Success.Should().BeFalse();
		result.StandardError.Should().Contain("Unknown format: invalid");
		result.StandardError.Should().Contain("Available formats: table, tree, markdown, json, stats");
	}

	[Fact]
	public async Task ShowMappings_NonExistentDatabase_ShowsError() {
		// Act
		var result = await ExecuteCliCommandAsync("decomp",
			"show-mappings --db \"/non/existent/path.msgpack\"", expectSuccess: false);

		// Assert
		result.Success.Should().BeFalse();
		result.StandardError.Should().Contain("Failed to display mappings");
	}

	[Fact]
	public async Task ShowMappings_CorruptedDatabase_ShowsError() {
		// Arrange
		await CreateCorruptedDatabaseAsync();

		// Act
		var result = await ExecuteCliCommandAsync("decomp",
			$"show-mappings --db \"{TestDbPath}\"", expectSuccess: false);

		// Assert
		result.Success.Should().BeFalse();
		result.StandardError.Should().Contain("Failed to display mappings");
	}

	[Fact]
	public async Task ShowMappings_EmptyDatabase_ShowsWarning() {
		// Arrange - Create empty database
		await TestDatabase.SaveAsync();

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("No mappings found matching the specified criteria");
	}

	[Fact]
	public async Task ShowMappings_DefaultDatabase_UsesDefaultPath() {
		// Arrange - Create database at default location
		var defaultDbPath = Path.Combine(TestDirectory, "decomp", "mappings.msgpack");
		Directory.CreateDirectory(Path.GetDirectoryName(defaultDbPath)!);

		var defaultDb = new MessagePackMappingDatabase(defaultDbPath);
		defaultDb.AddMapping("test", "tested", SymbolType.Function, "global", 0.9);
		await defaultDb.SaveAsync();

		// Act - Don't specify --db parameter
		var result = await ExecuteCliCommandAsync("decomp", "show-mappings --format json");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("tested");
	}

	[Fact]
	public async Task ShowMappings_LargeDatabase_HandlesPerformance() {
		// Arrange
		CreateLargeDatabase(1000);

		// Act - Test all formats with large database
		var tableResult = await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --limit 50 --db \"{TestDbPath}\"");
		var jsonResult  = await ExecuteCliCommandAsync("decomp", $"show-mappings --format json --limit 100 --db \"{TestDbPath}\"");
		var statsResult = await ExecuteCliCommandAsync("decomp", $"show-mappings --format stats --db \"{TestDbPath}\"");

		// Assert
		tableResult.Success.Should().BeTrue();
		tableResult.StandardOutput.Should().Contain("1000");

		jsonResult.Success.Should().BeTrue();
		var jsonDocument = JsonDocument.Parse(jsonResult.StandardOutput);
		jsonDocument.RootElement.GetProperty("database").GetProperty("totalMappings").GetInt32().Should().Be(1000);

		statsResult.Success.Should().BeTrue();
		statsResult.StandardOutput.Should().Contain("1000");
	}

	[Fact]
	public async Task ShowMappings_SpecialCharacters_HandlesCorrectly() {
		// Arrange - Create mappings with special characters
		TestDatabase.AddMapping("obj_$special", "specialObject", SymbolType.Variable, "global", 0.8);
		TestDatabase.AddMapping("func_[0]", "arrayFunction", SymbolType.Function, "ArrayUtils", 0.9);
		TestDatabase.AddMapping("prop.access", "propertyAccess", SymbolType.Property, "ObjectModel", 0.7);
		await TestDatabase.SaveAsync();

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		result.StandardOutput.Should().Contain("obj_$special");
		result.StandardOutput.Should().Contain("func_[0]");
		result.StandardOutput.Should().Contain("prop.access");
		result.StandardOutput.Should().Contain("specialObject");
		result.StandardOutput.Should().Contain("arrayFunction");
		result.StandardOutput.Should().Contain("propertyAccess");
	}

	[Fact]
	public async Task ShowMappings_VeryLongSymbolNames_TruncatesAppropriately() {
		// Arrange - Create mappings with very long names
		var longOriginal = "very_long_obfuscated_symbol_name_that_exceeds_normal_limits_" + new string('x', 100);
		var longMapped   = "veryLongDescriptiveSymbolNameThatExplainsWhatThisFunctionDoes_" + new string('y', 100);
		TestDatabase.AddMapping(longOriginal, longMapped, SymbolType.Function, "LongNamesModule", 0.85);
		await TestDatabase.SaveAsync();

		// Act
		var result = await ExecuteCliCommandAsync("decomp", $"show-mappings --format table --db \"{TestDbPath}\"");

		// Assert
		result.Success.Should().BeTrue();
		// Should truncate with ellipsis
		result.StandardOutput.Should().Contain("…");
		// Should not break table formatting
		result.StandardOutput.Should().Contain("│");
	}
}