using MessagePack;
using System.Text;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// High-performance MessagePack-based symbol mapping database
/// 10x faster serialization and smaller file sizes than JSON
/// </summary>
public class MessagePackMappingDatabase {
	private const    string                  DEFAULT_DB_PATH = "decomp/mappings.msgpack";
	private readonly string                  _dbPath;
	private          SymbolMappingCollection _mappings;

	public MessagePackMappingDatabase(string? dbPath = null) {
		_dbPath   = dbPath ?? DEFAULT_DB_PATH;
		_mappings = LoadMappings();
	}

	/// <summary>
	/// Add a symbol mapping with context awareness
	/// </summary>
	public void AddMapping(string original, string mapped, SymbolType type, string? context = null, double confidence = 1.0) {
		var mapping = new MessagePackSymbolMapping {
			Original    = original,
			Mapped      = mapped,
			Type        = type,
			Context     = context ?? "global", // Normalize null context to "global"
			Confidence  = confidence,
			LastUpdated = DateTime.UtcNow,
			UsageCount  = 1
		};

		// Check if mapping already exists
		var existingKey = $"{original}:{context ?? "global"}";
		var existing = _mappings.Mappings.FirstOrDefault(m =>
			m.Original == original && m.Context == (context ?? "global"));

		if (existing != null) {
			// Update existing mapping
			existing.Mapped      = mapped;
			existing.Type        = type;
			existing.Confidence  = Math.Max(existing.Confidence, confidence);
			existing.LastUpdated = DateTime.UtcNow;
			existing.UsageCount++;
		} else {
			// Add new mapping
			_mappings.Mappings.Add(mapping);
		}

		// Update statistics
		_mappings.Statistics.TotalMappings = _mappings.Mappings.Count;
		_mappings.Statistics.LastModified  = DateTime.UtcNow;
	}

	/// <summary>
	/// Get a symbol mapping with context awareness
	/// </summary>
	public MessagePackSymbolMapping? GetMapping(string original, string? context = null) {
		// Convert null context to "global" for consistency
		var normalizedContext = context ?? "global";

		// Try exact context match first
		var contextMapping = _mappings.Mappings.FirstOrDefault(m =>
			m.Original == original && m.Context == normalizedContext);

		if (contextMapping != null) return contextMapping;

		// If not found and not looking for global, fallback to global context
		if (normalizedContext != "global") {
			return _mappings.Mappings.FirstOrDefault(m =>
				m.Original == original && m.Context == "global");
		}

		return null;
	}

	/// <summary>
	/// Get all mappings for a specific context
	/// </summary>
	public IEnumerable<MessagePackSymbolMapping> GetMappingsForContext(string context) {
		return _mappings.Mappings.Where(m => m.Context == context);
	}

	/// <summary>
	/// Get all mappings in the database
	/// </summary>
	public IEnumerable<MessagePackSymbolMapping> GetAllMappings() {
		return _mappings.Mappings;
	}

	/// <summary>
	/// Search mappings by pattern
	/// </summary>
	public IEnumerable<MessagePackSymbolMapping> SearchMappings(string pattern) {
		try {
			var regex = new System.Text.RegularExpressions.Regex(pattern,
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);

			return _mappings.Mappings.Where(m =>
				regex.IsMatch(m.Original) || regex.IsMatch(m.Mapped));
		} catch (System.Text.RegularExpressions.RegexParseException) {
			// Invalid regex pattern - return empty results instead of throwing
			return Enumerable.Empty<MessagePackSymbolMapping>();
		}
	}

	/// <summary>
	/// Get mappings statistics
	/// </summary>
	public MappingStatistics GetStatistics() {
		var stats = new MappingStatistics {
			TotalMappings = _mappings.Mappings.Count,
			LastModified  = _mappings.Statistics.LastModified,
			ByType = _mappings.Mappings
				.GroupBy(m => m.Type)
				.ToDictionary(g => g.Key, g => g.Count()),
			ByContext = _mappings.Mappings
				.GroupBy(m => m.Context ?? "global")
				.ToDictionary(g => g.Key, g => g.Count()),
			AverageConfidence = _mappings.Mappings.Count > 0
				? _mappings.Mappings.Average(m => m.Confidence)
				: 0.0,
			HighConfidenceMappings = _mappings.Mappings.Count(m => m.Confidence > 0.8),
			TotalUsage             = _mappings.Mappings.Sum(m => m.UsageCount)
		};

		return stats;
	}

	/// <summary>
	/// Save mappings to MessagePack file
	/// </summary>
	public async Task SaveAsync() {
		try {
			// Ensure directory exists
			var directory = Path.GetDirectoryName(_dbPath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
				Directory.CreateDirectory(directory);
			}

			// Update statistics before saving
			_mappings.Statistics.TotalMappings = _mappings.Mappings.Count;
			_mappings.Statistics.LastModified  = DateTime.UtcNow;

			// Serialize to MessagePack
			var bytes = MessagePackSerializer.Serialize(_mappings, MessagePackSerializerOptions.Standard);
			await File.WriteAllBytesAsync(_dbPath, bytes);

			Logger.Info($"Saved {_mappings.Mappings.Count} mappings to {_dbPath} ({bytes.Length} bytes)");
		} catch (Exception ex) {
			Logger.Error($"Failed to save mappings: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Load mappings from MessagePack file
	/// </summary>
	private SymbolMappingCollection LoadMappings() {
		if (!File.Exists(_dbPath)) {
			Logger.Info($"Creating new mapping database at {_dbPath}");
			return new SymbolMappingCollection {
				Version = "1.0",
				Statistics = new DatabaseStatistics {
					CreatedAt    = DateTime.UtcNow,
					LastModified = DateTime.UtcNow
				}
			};
		}

		try {
			var bytes = File.ReadAllBytes(_dbPath);
			var mappings = MessagePackSerializer.Deserialize<SymbolMappingCollection>(bytes,
				MessagePackSerializerOptions.Standard);

			Logger.Info($"Loaded {mappings.Mappings.Count} mappings from {_dbPath} ({bytes.Length} bytes)");
			return mappings;
		} catch (Exception ex) {
			Logger.Error($"Failed to load mappings, creating new database: {ex.Message}");

			// Backup corrupted file
			if (File.Exists(_dbPath)) {
				var backupPath = $"{_dbPath}.backup.{DateTime.Now:yyyyMMdd-HHmmss}";
				File.Move(_dbPath, backupPath);
				Logger.Warning($"Corrupted database backed up to {backupPath}");
			}

			return new SymbolMappingCollection {
				Version = "1.0",
				Statistics = new DatabaseStatistics {
					CreatedAt    = DateTime.UtcNow,
					LastModified = DateTime.UtcNow
				}
			};
		}
	}

	/// <summary>
	/// Migrate from JSON database to MessagePack
	/// </summary>
	public async Task MigrateFromJsonAsync(string jsonPath) {
		if (!File.Exists(jsonPath)) {
			Logger.Warning($"JSON file not found: {jsonPath}");
			return;
		}

		try {
			var jsonContent = await File.ReadAllTextAsync(jsonPath);
			var jsonData    = System.Text.Json.JsonSerializer.Deserialize(jsonContent, JsonContext.Default.JsonMappingData);

			if (jsonData?.Mappings != null) {
				foreach (var kvp in jsonData.Mappings) {
					var jsonMapping = kvp.Value;
					AddMapping(
						jsonMapping.Original,
						jsonMapping.Mapped,
						jsonMapping.Type,
						"global", // JSON didn't have context support
						0.8       // Assume medium confidence for migrated data
					);
				}

				await SaveAsync();
				Logger.Success($"Migrated {jsonData.Mappings.Count} mappings from JSON to MessagePack");

				// Backup original JSON file
				var backupPath = $"{jsonPath}.migrated.{DateTime.Now:yyyyMMdd-HHmmss}";
				File.Move(jsonPath, backupPath);
				Logger.Info($"Original JSON backed up to {backupPath}");
			}
		} catch (Exception ex) {
			Logger.Error($"Failed to migrate from JSON: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Export mappings to human-readable format
	/// </summary>
	public string ExportToReadableFormat(ExportFormat format = ExportFormat.Table) {
		var sb = new StringBuilder();

		switch (format) {
			case ExportFormat.Table:
				return ExportAsTable();

			case ExportFormat.Tree:
				return ExportAsTree();

			case ExportFormat.Markdown:
				return ExportAsMarkdown();

			default:
				return ExportAsTable();
		}
	}

	private string ExportAsTable() {
		var sb    = new StringBuilder();
		var stats = GetStatistics();

		sb.AppendLine("╭─────────────────────────────────────────────────────────────────╮");
		sb.AppendLine("│                     SYMBOL MAPPING DATABASE                     │");
		sb.AppendLine("├─────────────────────────────────────────────────────────────────┤");
		sb.AppendLine($"│ Total Mappings: {stats.TotalMappings,-10} │ Avg Confidence: {stats.AverageConfidence:P1,-10} │");
		sb.AppendLine($"│ Last Modified:  {stats.LastModified:yyyy-MM-dd HH:mm,-19} │");
		sb.AppendLine("╰─────────────────────────────────────────────────────────────────╯");
		sb.AppendLine();

		// Group by type
		foreach (var typeGroup in _mappings.Mappings.GroupBy(m => m.Type)) {
			sb.AppendLine($"┌─ {typeGroup.Key} Mappings ({typeGroup.Count()}) ─────────────────────────");
			sb.AppendLine("│");

			foreach (var mapping in typeGroup.OrderBy(m => m.Original).Take(20)) {
				var confidence = new string('●', (int)(mapping.Confidence * 5));
				var context    = mapping.Context != "global" ? $"[{mapping.Context}]" : "";
				sb.AppendLine($"│ {mapping.Original,-15} → {mapping.Mapped,-20} {confidence,-5} {context}");
			}

			if (typeGroup.Count() > 20) {
				sb.AppendLine($"│ ... and {typeGroup.Count() - 20} more");
			}

			sb.AppendLine("│");
		}

		return sb.ToString();
	}

	private string ExportAsTree() {
		var sb = new StringBuilder();

		sb.AppendLine("🗄️  Symbol Mapping Database");
		sb.AppendLine($"├── 📊 Statistics: {_mappings.Mappings.Count} total mappings");
		sb.AppendLine($"├── 🕐 Last Modified: {_mappings.Statistics.LastModified:yyyy-MM-dd HH:mm}");
		sb.AppendLine("└── 📁 Mappings by Context:");

		var contextGroups = _mappings.Mappings.GroupBy(m => m.Context ?? "global");
		foreach (var contextGroup in contextGroups) {
			sb.AppendLine($"    ├── 📂 {contextGroup.Key} ({contextGroup.Count()} mappings)");

			var typeGroups = contextGroup.GroupBy(m => m.Type);
			foreach (var typeGroup in typeGroups.Take(3)) {
				sb.AppendLine($"    │   ├── 🏷️  {typeGroup.Key}: {typeGroup.Count()} mappings");

				foreach (var mapping in typeGroup.Take(3)) {
					var confidence = mapping.Confidence >= 0.8 ? "🟢" : mapping.Confidence >= 0.6 ? "🟡" : "🔴";
					sb.AppendLine($"    │   │   └── {mapping.Original} → {mapping.Mapped} {confidence}");
				}

				if (typeGroup.Count() > 3) {
					sb.AppendLine($"    │   │       ... and {typeGroup.Count() - 3} more");
				}
			}
		}

		return sb.ToString();
	}

	private string ExportAsMarkdown() {
		var sb    = new StringBuilder();
		var stats = GetStatistics();

		sb.AppendLine("# Symbol Mapping Database");
		sb.AppendLine();
		sb.AppendLine("## Statistics");
		sb.AppendLine();
		sb.AppendLine($"- **Total Mappings**: {stats.TotalMappings:N0}");
		sb.AppendLine($"- **Average Confidence**: {stats.AverageConfidence:P1}");
		sb.AppendLine($"- **High Confidence**: {stats.HighConfidenceMappings:N0} mappings (>80%)");
		sb.AppendLine($"- **Last Modified**: {stats.LastModified:yyyy-MM-dd HH:mm}");
		sb.AppendLine();

		sb.AppendLine("## Mappings by Type");
		sb.AppendLine();

		foreach (var typeGroup in _mappings.Mappings.GroupBy(m => m.Type)) {
			sb.AppendLine($"### {typeGroup.Key} ({typeGroup.Count()} mappings)");
			sb.AppendLine();
			sb.AppendLine("| Original | Mapped | Context | Confidence | Usage |");
			sb.AppendLine("|----------|---------|---------|------------|-------|");

			foreach (var mapping in typeGroup.OrderByDescending(m => m.Confidence).Take(10)) {
				var context = mapping.Context != "global" ? mapping.Context : "-";
				sb.AppendLine($"| `{mapping.Original}` | `{mapping.Mapped}` | {context} | {mapping.Confidence:P0} | {mapping.UsageCount} |");
			}

			if (typeGroup.Count() > 10) {
				sb.AppendLine($"| ... | ... | ... | ... | {typeGroup.Count() - 10} more |");
			}

			sb.AppendLine();
		}

		return sb.ToString();
	}
}

/// <summary>
/// MessagePack serializable symbol mapping collection
/// </summary>
[MessagePackObject]
public class SymbolMappingCollection {
	[Key(0)]
	public string Version { get; set; } = "1.0";

	[Key(1)]
	public List<MessagePackSymbolMapping> Mappings { get; set; } = new();

	[Key(2)]
	public List<MessagePackNamingPattern> Patterns { get; set; } = new();

	[Key(3)]
	public DatabaseStatistics Statistics { get; set; } = new();
}

/// <summary>
/// MessagePack serializable symbol mapping
/// </summary>
[MessagePackObject]
public class MessagePackSymbolMapping {
	[Key(0)]
	public string Original { get; set; } = "";

	[Key(1)]
	public string Mapped { get; set; } = "";

	[Key(2)]
	public SymbolType Type { get; set; }

	[Key(3)]
	public string? Context { get; set; }

	[Key(4)]
	public double Confidence { get; set; } = 1.0;

	[Key(5)]
	public DateTime LastUpdated { get; set; }

	[Key(6)]
	public int UsageCount { get; set; } = 1;

	[Key(7)]
	public List<string> References { get; set; } = new();
}

/// <summary>
/// MessagePack serializable naming pattern
/// </summary>
[MessagePackObject]
public class MessagePackNamingPattern {
	[Key(0)]
	public string Pattern { get; set; } = "";

	[Key(1)]
	public string Description { get; set; } = "";

	[Key(2)]
	public SymbolType TargetType { get; set; }

	[Key(3)]
	public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// MessagePack serializable database statistics
/// </summary>
[MessagePackObject]
public class DatabaseStatistics {
	[Key(0)]
	public DateTime CreatedAt { get; set; }

	[Key(1)]
	public DateTime LastModified { get; set; }

	[Key(2)]
	public int TotalMappings { get; set; }

	[Key(3)]
	public string LastVersion { get; set; } = "";
}

/// <summary>
/// Runtime mapping statistics (not serialized)
/// </summary>
public class MappingStatistics {
	public int                         TotalMappings          { get; set; }
	public DateTime                    LastModified           { get; set; }
	public Dictionary<SymbolType, int> ByType                 { get; set; } = new();
	public Dictionary<string, int>     ByContext              { get; set; } = new();
	public double                      AverageConfidence      { get; set; }
	public int                         HighConfidenceMappings { get; set; }
	public int                         TotalUsage             { get; set; }
}

// SymbolType enum is already defined in DecompilationMapper.cs

/// <summary>
/// Export format options
/// </summary>
public enum ExportFormat {
	Table,
	Tree,
	Markdown
}

/// <summary>
/// Legacy JSON structure for migration
/// </summary>
public class JsonMappingData {
	public Dictionary<string, JsonMapping>? Mappings { get; set; }
}

public class JsonMapping {
	public string     Original    { get; set; } = "";
	public string     Mapped      { get; set; } = "";
	public SymbolType Type        { get; set; }
	public DateTime   LastUpdated { get; set; }
}