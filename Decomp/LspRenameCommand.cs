using System.CommandLine;
using System.Text.Json;
using Hoho.Core;

namespace Hoho.Decomp {
	/// <summary>
	/// Simplified LSP-based structural renaming commands
	/// </summary>
	public class LspRenameCommand : Command {
		// Single mapping database for everything
		private const string DEFAULT_MAPPING_DB = "decomp/mappings.msgpack";

		public LspRenameCommand() : base("rename", "Rename symbols using TypeScript LSP") {
			Argument<string> fileArg = new Argument<string>(
				"file",
				"JavaScript file to rename symbols in"
			);

			Option<bool> dryRunOption = new Option<bool>(
				"--dry-run",
				"Show what would be renamed without making changes"
			);

			AddArgument(fileArg);
			AddOption(dryRunOption);

			this.SetHandler(async (file, dryRun) => {
				await ExecuteAsync(file, dryRun);
			}, fileArg, dryRunOption);
		}

		private static async Task ExecuteAsync(string file, bool dryRun) {
			using IDisposable timer = Logger.TimeOperation("LSP rename");

			// Always use the default mapping database
			if (!File.Exists(DEFAULT_MAPPING_DB)) {
				Logger.Error($"Mapping database not found: {DEFAULT_MAPPING_DB}");
				Logger.Info("Run 'hoho decomp learn' to create mappings first");
				return;
			}

			// Load mappings
			string json = await File.ReadAllTextAsync(DEFAULT_MAPPING_DB);
			Dictionary<string, string> symbolMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
			                                       ?? new Dictionary<string, string>();

			Logger.Info($"Loaded {symbolMap.Count} symbol mappings");

			// Resolve file path - check common locations
			string? filePath = ResolveFilePath(file);
			if (filePath == null) {
				Logger.Error($"File not found: {file}");
				return;
			}

			// Initialize LSP
			string                 workspaceRoot = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";
			using LspRenameService lsp           = new LspRenameService(workspaceRoot);

			try {
				Logger.Info("Starting TypeScript Language Server...");
				await lsp.InitializeAsync();

				if (dryRun) {
					Logger.Info("DRY RUN MODE - No changes will be made");
				}

				// Apply all mappings
				RenameReport report = await lsp.BatchRenameAsync(filePath, symbolMap);

				Logger.Success($"Renamed {report.SuccessfulRenames} symbols ({report.TotalReferences} references)");
				if (report.FailedRenames > 0) {
					Logger.Warning($"Failed to rename {report.FailedRenames} symbols");
				}
			} catch (Exception ex) {
				Logger.Error($"LSP rename failed: {ex.Message}");
			}
		}

		private static string? ResolveFilePath(string file) {
			// Try as-is first
			if (File.Exists(file)) return file;

			// Common locations to check
			string[] searchPaths = new[] {
				file,
				Path.Combine("decomp/claude-code-dev/versions/1.0.98/manual/modules", file),
				Path.Combine("decomp/claude-code-dev/versions/1.0.98/original/modules", file),
				Path.Combine("decomp", file)
			};

			foreach (string path in searchPaths) {
				if (File.Exists(path)) {
					Logger.Info($"Found file at: {path}");
					return path;
				}
			}

			return null;
		}
	}

	/// <summary>
	/// Batch rename all modules in a version
	/// </summary>
	public class LspBatchRenameCommand : Command {
		private const string DEFAULT_MAPPING_DB = "decomp/mappings.msgpack";

		public LspBatchRenameCommand() : base("rename-all", "Rename symbols in all modules") {
			Argument<string> versionArg = new Argument<string>(
				"version",
				() => "1.0.98",
				"Version to process"
			);

			AddArgument(versionArg);

			this.SetHandler(async version => {
				await ExecuteBatchAsync(version);
			}, versionArg);
		}

		private static async Task ExecuteBatchAsync(string version) {
			using IDisposable timer = Logger.TimeOperation("Batch rename");

			// Check mapping database
			if (!File.Exists(DEFAULT_MAPPING_DB)) {
				Logger.Error($"Mapping database not found: {DEFAULT_MAPPING_DB}");
				Logger.Info("Run 'hoho decomp learn' to create mappings first");
				return;
			}

			// Load mappings
			string json = await File.ReadAllTextAsync(DEFAULT_MAPPING_DB);
			Dictionary<string, string> symbolMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
			                                       ?? new Dictionary<string, string>();

			Logger.Info($"Loaded {symbolMap.Count} symbol mappings");

			// Find modules
			string modulesDir = Path.Combine("decomp/claude-code-dev/versions", version, "manual/modules");
			if (!Directory.Exists(modulesDir)) {
				// Try original if manual doesn't exist
				modulesDir = Path.Combine("decomp/claude-code-dev/versions", version, "original/modules");
			}

			if (!Directory.Exists(modulesDir)) {
				Logger.Error($"Modules directory not found for version {version}");
				return;
			}

			string[] files = Directory.GetFiles(modulesDir, "*.js");
			Logger.Info($"Found {files.Length} modules to process");

			int successCount = 0;
			int failCount    = 0;
			int totalRenames = 0;

			// Process one at a time (LSP is stateful)
			foreach (string file in files) {
				string fileName = Path.GetFileName(file);

				try {
					string                 workspaceRoot = Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".";
					using LspRenameService lsp           = new LspRenameService(workspaceRoot);

					await lsp.InitializeAsync();
					RenameReport report = await lsp.BatchRenameAsync(file, symbolMap);

					if (report.SuccessfulRenames > 0) {
						Logger.Success($"{fileName}: {report.SuccessfulRenames} renames");
						successCount++;
						totalRenames += report.SuccessfulRenames;
					} else {
						Logger.Info($"{fileName}: No matching symbols");
					}

					if (report.FailedRenames > 0) {
						failCount++;
					}
				} catch (Exception ex) {
					Logger.Error($"Failed to process {fileName}: {ex.Message}");
					failCount++;
				}
			}

			Logger.Success($"Processed {files.Length} files");
			Logger.Info($"Successful: {successCount} files, {totalRenames} total renames");
			if (failCount > 0) {
				Logger.Warning($"Failed: {failCount} files");
			}
		}
	}

	/// <summary>
	/// Add new symbol mappings to the database
	/// </summary>
	public class AddMappingCommand : Command {
		private const string DEFAULT_MAPPING_DB = "decomp/mappings.msgpack";

		public AddMappingCommand() : base("add-mapping", "Add symbol mapping to database") {
			Argument<string> oldNameArg = new Argument<string>("old", "Obfuscated symbol name");
			Argument<string> newNameArg = new Argument<string>("new", "Clean replacement name");

			AddArgument(oldNameArg);
			AddArgument(newNameArg);

			this.SetHandler(async (oldName, newName) => {
				await AddMappingAsync(oldName, newName);
			}, oldNameArg, newNameArg);
		}

		private static async Task AddMappingAsync(string oldName, string newName) {
			// Load existing or create new
			Dictionary<string, string> mappings;
			if (File.Exists(DEFAULT_MAPPING_DB)) {
				string json = await File.ReadAllTextAsync(DEFAULT_MAPPING_DB);
				mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
				           ?? new Dictionary<string, string>();
			} else {
				mappings = new Dictionary<string, string>();
				Directory.CreateDirectory(Path.GetDirectoryName(DEFAULT_MAPPING_DB)!);
			}

			// Add/update mapping
			if (mappings.ContainsKey(oldName)) {
				Logger.Info($"Updating: {oldName} -> {newName} (was {mappings[oldName]})");
			} else {
				Logger.Info($"Adding: {oldName} -> {newName}");
			}

			mappings[oldName] = newName;

			// Save
			string updatedJson = JsonSerializer.Serialize(mappings, new JsonSerializerOptions {
				WriteIndented = true
			});
			await File.WriteAllTextAsync(DEFAULT_MAPPING_DB, updatedJson);

			Logger.Success($"Mapping database updated ({mappings.Count} total mappings)");
		}
	}

	/// <summary>
	/// Show current mappings
	/// </summary>
	public class ShowMappingsCommand : Command {
		private const string DEFAULT_MAPPING_DB = "decomp/mappings.msgpack";

		public ShowMappingsCommand() : base("mappings", "Show current symbol mappings") {
			this.SetHandler(async () => {
				if (!File.Exists(DEFAULT_MAPPING_DB)) {
					Logger.Warning("No mappings database found");
					Logger.Info("Use 'hoho decomp add-mapping' to add mappings");
					return;
				}

				string json = await File.ReadAllTextAsync(DEFAULT_MAPPING_DB);
				Dictionary<string, string> mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
				                                      ?? new Dictionary<string, string>();

				Console.WriteLine($"\nSymbol Mappings ({mappings.Count} total):");
				Console.WriteLine("─".PadRight(50, '─'));

				foreach (KeyValuePair<string, string> mapping in mappings.OrderBy(m => m.Key)) {
					Console.WriteLine($"{mapping.Key,-20} → {mapping.Value}");
				}
			});
		}
	}
}