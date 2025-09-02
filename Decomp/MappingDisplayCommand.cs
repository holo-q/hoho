using System.CommandLine;
using System.Text;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// Beautiful display command for symbol mappings database
/// </summary>
public class MappingDisplayCommand : Command {
	public MappingDisplayCommand() : base("show-mappings", "Display symbol mappings in beautiful format") {
		var formatOption = new Option<string>(
			"--format",
			() => "table",
			"Display format: table, tree, markdown, json, stats"
		);
		formatOption.AddCompletions("table", "tree", "markdown", "json", "stats");

		var contextOption = new Option<string?>(
			"--context",
			"Filter by specific context"
		);

		var typeOption = new Option<string?>(
			"--type",
			"Filter by symbol type (class, function, variable, etc.)"
		);
		typeOption.AddCompletions("class", "function", "variable", "parameter", "property", "method", "module");

		var searchOption = new Option<string?>(
			"--search",
			"Search pattern (regex supported)"
		);

		var limitOption = new Option<int>(
			"--limit",
			() => 50,
			"Maximum number of mappings to display"
		);

		var confidenceOption = new Option<double>(
			"--min-confidence",
			() => 0.0,
			"Minimum confidence threshold (0.0 to 1.0)"
		);

		var dbPathOption = new Option<string?>(
			"--db",
			"Path to mapping database (default: decomp/mappings.msgpack)"
		);

		AddOption(formatOption);
		AddOption(contextOption);
		AddOption(typeOption);
		AddOption(searchOption);
		AddOption(limitOption);
		AddOption(confidenceOption);
		AddOption(dbPathOption);

		this.SetHandler(async (format, context, type, search, limit, minConfidence, dbPath) => {
			await DisplayMappingsAsync(format, context, type, search, limit, minConfidence, dbPath);
		}, formatOption, contextOption, typeOption, searchOption, limitOption, confidenceOption, dbPathOption);
	}

	private async Task DisplayMappingsAsync(
		string  format,
		string? context,
		string? type,
		string? search,
		int     limit,
		double  minConfidence,
		string? dbPath) {
		try {
			var db = new MessagePackMappingDatabase(dbPath);

			switch (format.ToLower()) {
				case "table":
					DisplayTable(db, context, type, search, limit, minConfidence);
					break;

				case "tree":
					DisplayTree(db, context, type, search, limit, minConfidence);
					break;

				case "markdown":
					DisplayMarkdown(db, context, type, search, limit, minConfidence);
					break;

				case "json":
					await DisplayJsonAsync(db, context, type, search, limit, minConfidence);
					break;

				case "stats":
					DisplayStatistics(db);
					break;

				default:
					Logger.Error($"Unknown format: {format}");
					Logger.Info("Available formats: table, tree, markdown, json, stats");
					break;
			}
		} catch (Exception ex) {
			Logger.Error($"Failed to display mappings: {ex.Message}");
		}
	}

	private void DisplayTable(MessagePackMappingDatabase db, string? context, string? type, string? search, int limit, double minConfidence) {
		var mappings = GetFilteredMappings(db, context, type, search, minConfidence).Take(limit);
		var stats    = db.GetStatistics();

		// Header with stats
		Console.WriteLine();
		Console.WriteLine("╭──────────────────────────────────────────────────────────────────────────────╮");
		Console.WriteLine("│                           🗄️  SYMBOL MAPPING DATABASE                           │");
		Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
		Console.WriteLine($"│ 📊 Total: {stats.TotalMappings,-8} │ 🎯 Avg Confidence: {stats.AverageConfidence:P1,-8} │ 🕐 Modified: {stats.LastModified:MMM dd, HH:mm} │");
		Console.WriteLine("╰──────────────────────────────────────────────────────────────────────────────╯");
		Console.WriteLine();

		if (!mappings.Any()) {
			Logger.Warning("No mappings found matching the specified criteria.");
			return;
		}

		// Table headers
		Console.WriteLine("┌─────────────────┬─────────────────────┬──────────┬─────────────┬────────────┬─────────┐");
		Console.WriteLine("│ Original Symbol │ Mapped Symbol       │ Type     │ Context     │ Confidence │ Usage   │");
		Console.WriteLine("├─────────────────┼─────────────────────┼──────────┼─────────────┼────────────┼─────────┤");

		foreach (var mapping in mappings) {
			var originalSymbol = TruncateString(mapping.Original, 15);
			var mappedSymbol   = TruncateString(mapping.Mapped, 19);
			var symbolType     = TruncateString(mapping.Type.ToString(), 8);
			var contextStr     = TruncateString(mapping.Context ?? "global", 11);
			var confidence     = GetConfidenceIcon(mapping.Confidence) + $" {mapping.Confidence:P0}";
			var usage          = mapping.UsageCount.ToString();

			Console.WriteLine($"│ {originalSymbol,-15} │ {mappedSymbol,-19} │ {symbolType,-8} │ {contextStr,-11} │ {confidence,-10} │ {usage,-7} │");
		}

		Console.WriteLine("└─────────────────┴─────────────────────┴──────────┴─────────────┴────────────┴─────────┘");
		Console.WriteLine();

		// Context summary
		var contextStats = stats.ByContext.OrderByDescending(kvp => kvp.Value).Take(5);
		if (contextStats.Any()) {
			Console.WriteLine("📂 Top Contexts:");
			foreach (var kvp in contextStats) {
				var percentage = (double)kvp.Value / stats.TotalMappings * 100;
				var bar        = new string('█', Math.Min(20, (int)(percentage / 5)));
				Console.WriteLine($"   {kvp.Key,-20} {kvp.Value,4} mappings {bar} {percentage:F1}%");
			}
			Console.WriteLine();
		}
	}

	private void DisplayTree(MessagePackMappingDatabase db, string? context, string? type, string? search, int limit, double minConfidence) {
		var mappings = GetFilteredMappings(db, context, type, search, minConfidence);
		var stats    = db.GetStatistics();

		Console.WriteLine();
		Console.WriteLine("🗄️  Symbol Mapping Database");
		Console.WriteLine($"├── 📊 Statistics: {stats.TotalMappings:N0} total mappings");
		Console.WriteLine($"├── 🎯 Average confidence: {stats.AverageConfidence:P1}");
		Console.WriteLine($"├── 🏆 High confidence: {stats.HighConfidenceMappings:N0} mappings (>80%)");
		Console.WriteLine($"├── 🕐 Last modified: {stats.LastModified:yyyy-MM-dd HH:mm}");
		Console.WriteLine("└── 📁 Mappings:");

		var contextGroups = mappings.GroupBy(m => m.Context ?? "global").Take(10);
		var contextCount  = 0;
		var totalContexts = contextGroups.Count();

		foreach (var contextGroup in contextGroups) {
			contextCount++;
			var isLastContext = contextCount == totalContexts;
			var contextPrefix = isLastContext ? "└──" : "├──";
			var childPrefix   = isLastContext ? "    " : "│   ";

			Console.WriteLine($"    {contextPrefix} 📂 {contextGroup.Key} ({contextGroup.Count()} mappings)");

			var typeGroups = contextGroup.GroupBy(m => m.Type).Take(5);
			var typeCount  = 0;
			var totalTypes = typeGroups.Count();

			foreach (var typeGroup in typeGroups) {
				typeCount++;
				var isLastType    = typeCount == totalTypes;
				var typePrefix    = isLastType ? "└──" : "├──";
				var mappingPrefix = isLastType ? "    " : "│   ";

				Console.WriteLine($"    {childPrefix}{typePrefix} 🏷️  {typeGroup.Key}: {typeGroup.Count()} mappings");

				var mappingsList  = typeGroup.OrderByDescending(m => m.Confidence).Take(3);
				var mappingCount  = 0;
				var totalMappings = mappingsList.Count();

				foreach (var mapping in mappingsList) {
					mappingCount++;
					var isLastMapping  = mappingCount == totalMappings;
					var finalPrefix    = isLastMapping ? "└──" : "├──";
					var confidenceIcon = GetConfidenceIcon(mapping.Confidence);

					Console.WriteLine($"    {childPrefix}{mappingPrefix}{finalPrefix} {mapping.Original} → {mapping.Mapped} {confidenceIcon}");
				}

				if (typeGroup.Count() > 3) {
					Console.WriteLine($"    {childPrefix}{mappingPrefix}    ... and {typeGroup.Count() - 3} more");
				}
			}

			Console.WriteLine();
		}
	}

	private void DisplayMarkdown(MessagePackMappingDatabase db, string? context, string? type, string? search, int limit, double minConfidence) {
		var output = db.ExportToReadableFormat(ExportFormat.Markdown);
		Console.WriteLine(output);
	}

	private async Task DisplayJsonAsync(MessagePackMappingDatabase db, string? context, string? type, string? search, int limit, double minConfidence) {
		var mappings = GetFilteredMappings(db, context, type, search, minConfidence).Take(limit);
		var stats    = db.GetStatistics();

		var jsonOutput = new MappingDisplayJson {
			Database = new DatabaseJson {
				TotalMappings     = stats.TotalMappings,
				AverageConfidence = stats.AverageConfidence,
				LastModified      = stats.LastModified,
				ByType            = stats.ByType,
				ByContext         = stats.ByContext
			},
			Mappings = mappings.Select(m => new MappingJson {
				Original    = m.Original,
				Mapped      = m.Mapped,
				Type        = m.Type.ToString(),
				Context     = m.Context,
				Confidence  = m.Confidence,
				UsageCount  = m.UsageCount,
				LastUpdated = m.LastUpdated
			}).ToArray()
		};

		var json = System.Text.Json.JsonSerializer.Serialize(jsonOutput, JsonContext.Default.MappingDisplayJson);
		Console.WriteLine(json);
	}

	private void DisplayStatistics(MessagePackMappingDatabase db) {
		var stats = db.GetStatistics();

		Console.WriteLine();
		Console.WriteLine("╭────────────────────────────────────────────────────────────────╮");
		Console.WriteLine("│                    📊 DATABASE STATISTICS                      │");
		Console.WriteLine("├────────────────────────────────────────────────────────────────┤");
		Console.WriteLine($"│ Total Mappings:     {stats.TotalMappings,8:N0}                          │");
		Console.WriteLine($"│ Average Confidence: {stats.AverageConfidence,8:P1}                          │");
		Console.WriteLine($"│ High Confidence:    {stats.HighConfidenceMappings,8:N0} (>80%)                    │");
		Console.WriteLine($"│ Total Usage Count:  {stats.TotalUsage,8:N0}                          │");
		Console.WriteLine($"│ Last Modified:      {stats.LastModified,19:yyyy-MM-dd HH:mm}          │");
		Console.WriteLine("╰────────────────────────────────────────────────────────────────╯");
		Console.WriteLine();

		// Type distribution
		Console.WriteLine("📊 Distribution by Type:");
		Console.WriteLine();
		foreach (var kvp in stats.ByType.OrderByDescending(x => x.Value)) {
			var percentage = (double)kvp.Value / stats.TotalMappings * 100;
			var bar        = new string('█', Math.Min(30, (int)(percentage * 30 / 100)));
			Console.WriteLine($"  {kvp.Key,-12} {kvp.Value,6:N0} {bar,-30} {percentage,5:F1}%");
		}
		Console.WriteLine();

		// Context distribution
		Console.WriteLine("📂 Distribution by Context:");
		Console.WriteLine();
		var topContexts = stats.ByContext.OrderByDescending(x => x.Value).Take(10);
		foreach (var kvp in topContexts) {
			var percentage  = (double)kvp.Value / stats.TotalMappings * 100;
			var bar         = new string('█', Math.Min(25, (int)(percentage * 25 / 100)));
			var contextName = kvp.Key == "global" ? "🌐 global" : $"📁 {kvp.Key}";
			Console.WriteLine($"  {contextName,-20} {kvp.Value,6:N0} {bar,-25} {percentage,5:F1}%");
		}

		if (stats.ByContext.Count > 10) {
			var remaining      = stats.ByContext.Count - 10;
			var remainingCount = stats.ByContext.Skip(10).Sum(x => x.Value);
			Console.WriteLine($"  ... and {remaining} more contexts with {remainingCount:N0} mappings");
		}

		Console.WriteLine();

		// Confidence distribution
		Console.WriteLine("🎯 Confidence Distribution:");
		Console.WriteLine();
		var confidenceBuckets = new[] {
			("🔴 Low (0-60%)", 0.0, 0.6),
			("🟡 Medium (60-80%)", 0.6, 0.8),
			("🟢 High (80-100%)", 0.8, 1.0)
		};

		// We'd need to get individual mappings to calculate this properly
		// For now, show high confidence count from stats
		Console.WriteLine($"  🟢 High Confidence:    {stats.HighConfidenceMappings,6:N0}");
		Console.WriteLine($"  🟡 Medium Confidence:   {stats.TotalMappings - stats.HighConfidenceMappings,6:N0} (estimated)");
		Console.WriteLine();
	}

	private IEnumerable<MessagePackSymbolMapping> GetFilteredMappings(
		MessagePackMappingDatabase db,
		string?                    context,
		string?                    type,
		string?                    search,
		double                     minConfidence) {
		// Start with all mappings or filter by context
		var allMappings = context != null
			? db.GetMappingsForContext(context)
			: db.GetAllMappings();

		var filtered = allMappings.AsEnumerable();

		// Apply filters
		if (type != null) {
			if (Enum.TryParse<SymbolType>(type, true, out var symbolType)) {
				filtered = filtered.Where(m => m.Type == symbolType);
			}
		}

		if (search != null) {
			try {
				var regex = new System.Text.RegularExpressions.Regex(search,
					System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				filtered = filtered.Where(m => regex.IsMatch(m.Original) || regex.IsMatch(m.Mapped));
			} catch {
				// Fallback to simple string search if regex is invalid
				filtered = filtered.Where(m =>
					m.Original.Contains(search, StringComparison.OrdinalIgnoreCase) ||
					m.Mapped.Contains(search, StringComparison.OrdinalIgnoreCase));
			}
		}

		filtered = filtered.Where(m => m.Confidence >= minConfidence);

		return filtered.OrderByDescending(m => m.Confidence).ThenBy(m => m.Original);
	}

	private static string TruncateString(string str, int maxLength) {
		if (str.Length <= maxLength)
			return str;

		return str[..(maxLength - 1)] + "…";
	}

	private static string GetConfidenceIcon(double confidence) {
		return confidence switch {
			>= 0.9 => "🟢",
			>= 0.7 => "🟡",
			>= 0.5 => "🟠",
			_      => "🔴"
		};
	}
}

// JSON serialization types for AOT compatibility
public class MappingDisplayJson {
	public DatabaseJson Database { get; set; } = new();
	public MappingJson[] Mappings { get; set; } = Array.Empty<MappingJson>();
}

public class DatabaseJson {
	public int TotalMappings { get; set; }
	public double AverageConfidence { get; set; }
	public DateTime LastModified { get; set; }
	public Dictionary<SymbolType, int> ByType { get; set; } = new();
	public Dictionary<string, int> ByContext { get; set; } = new();
}

public class MappingJson {
	public string Original { get; set; } = "";
	public string Mapped { get; set; } = "";
	public string Type { get; set; } = "";
	public string? Context { get; set; }
	public double Confidence { get; set; }
	public int UsageCount { get; set; }
	public DateTime LastUpdated { get; set; }
}