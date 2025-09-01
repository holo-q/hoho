using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hoho.Core;

namespace Hoho.Decomp {
	/// <summary>
	/// Simplified decompilation commands with auto-detection
	/// </summary>
	public class ExtractNewCommand : Command {
		public ExtractNewCommand() : base("extract", "Extract modules from webpack bundle") {
			Argument<string> bundleArg          = new Argument<string>("bundle", "Path to bundled JS file");
			Argument<string> versionArg         = new Argument<string>("version", "Version number (e.g., 1.0.98)");
			Option<bool>     autoDeobfuscateOpt = new Option<bool>("--auto", () => true, "Auto-apply known mappings");

			AddArgument(bundleArg);
			AddArgument(versionArg);
			AddOption(autoDeobfuscateOpt);

			this.SetHandler(async (bundle, version, autoDeobfuscate) => {
				SimplifiedExtractor               extractor = new SimplifiedExtractor();
				SimplifiedExtractor.ExtractResult result    = await extractor.ExtractAsync(bundle, version, autoDeobfuscate);

				Logger.Success($"Extracted {result.ModuleCount} modules");
				Logger.Info($"Original: {result.OriginalDir}");
				Logger.Info($"Dev: {result.DevDir}");
				Logger.Info("");
				Logger.Info("Next steps:");
				Logger.Info($"1. Edit files in {result.DevDir}");
				Logger.Info("2. Run: hoho decomp rename-all");
				Logger.Info($"3. Run: hoho decomp finalize {version}");
			}, bundleArg, versionArg, autoDeobfuscateOpt);
		}
	}

	/// <summary>
	/// Auto-detect version and rename
	/// </summary>
	public class SmartRenameCommand : Command {
		private const string MAPPINGS_DB = "decomp/mappings.json";

		public SmartRenameCommand() : base("rename", "Rename symbols in a file") {
			Argument<string> fileArg = new Argument<string>("file", "File to rename (auto-detects version)");

			AddArgument(fileArg);

			this.SetHandler(async file => {
				// Auto-detect version from path or find file
				(string? filePath, string? version) = FindFile(file);
				if (filePath == null) {
					Logger.Error($"File not found: {file}");
					return;
				}

				Logger.Info($"Found in version {version}: {filePath}");

				// Use LSP client for fast rename
				if (!File.Exists(MAPPINGS_DB)) {
					Logger.Error("No mappings found. Use 'hoho decomp add-mapping' to add mappings");
					return;
				}

				string json = await File.ReadAllTextAsync(MAPPINGS_DB);
				Dictionary<string, string> mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
				                                      ?? new Dictionary<string, string>();

				RenameResponse response = await LspClient.RenameAsync(filePath, mappings);

				if (response.Success) {
					Logger.Success($"Renamed {response.SuccessfulRenames} symbols ({response.TotalReferences} references)");
				} else {
					Logger.Error($"Rename failed: {response.Error}");
				}
			}, fileArg);
		}

		private static (string? path, string? version) FindFile(string file) {
			// Check if it's already a full path
			if (File.Exists(file)) {
				// Extract version from path
				Match match = Regex.Match(file, @"decomp[/\\](\d+\.\d+\.\d+)(-dev)?[/\\]");
				return (file, match.Success ? match.Groups[1].Value : "unknown");
			}

			// Search in decomp directory for the file
			string decompDir = "decomp";
			if (!Directory.Exists(decompDir)) return (null, null);

			// Look in all version-dev directories first (most likely place)
			foreach (string dir in Directory.GetDirectories(decompDir, "*-dev")) {
				string testPath = Path.Combine(dir, file);
				if (File.Exists(testPath)) {
					string version = Path.GetFileName(dir).Replace("-dev", "");
					return (testPath, version);
				}
			}

			// Then check original version directories
			foreach (string dir in Directory.GetDirectories(decompDir).Where(d => !d.Contains("-"))) {
				string testPath = Path.Combine(dir, file);
				if (File.Exists(testPath)) {
					string version = Path.GetFileName(dir);
					return (testPath, version);
				}
			}

			return (null, null);
		}
	}

	/// <summary>
	/// Rename all files in latest dev version
	/// </summary>
	public class RenameAllCommand : Command {
		private const string MAPPINGS_DB = "decomp/mappings.json";

		public RenameAllCommand() : base("rename-all", "Rename all files in latest dev version") {
			Option<string?> versionOpt = new Option<string?>("--version", "Specific version (default: latest)");

			AddOption(versionOpt);

			this.SetHandler(async version => {
				// Find version to process
				string? targetVersion = version ?? GetLatestDevVersion();
				if (targetVersion == null) {
					Logger.Error("No dev versions found");
					return;
				}

				string devDir = Path.Combine("decomp", $"{targetVersion}-dev");
				Logger.Info($"Processing {targetVersion}-dev");

				// Load mappings
				if (!File.Exists(MAPPINGS_DB)) {
					Logger.Error("No mappings found. Use 'hoho decomp add-mapping' to add mappings");
					return;
				}

				string json = await File.ReadAllTextAsync(MAPPINGS_DB);
				Dictionary<string, string> mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
				                                      ?? new Dictionary<string, string>();

				string[] files = Directory.GetFiles(devDir, "*.js");
				Logger.Info($"Processing {files.Length} files with {mappings.Count} mappings");

				int totalRenames  = 0;
				int filesModified = 0;

				foreach (string file in files) {
					try {
						RenameResponse response = await LspClient.RenameAsync(file, mappings);
						if (response.Success && response.SuccessfulRenames > 0) {
							Logger.Success($"{Path.GetFileName(file)}: {response.SuccessfulRenames} renames");
							totalRenames += response.SuccessfulRenames;
							filesModified++;
						}
					} catch (Exception ex) {
						Logger.Error($"Failed on {Path.GetFileName(file)}: {ex.Message}");
					}
				}

				Logger.Success($"Modified {filesModified} files, {totalRenames} total renames");
			}, versionOpt);
		}

		private static string? GetLatestDevVersion() {
			if (!Directory.Exists("decomp")) return null;

			string? devDirs = Directory.GetDirectories("decomp", "*-dev")
				.Select(d => Path.GetFileName(d))
				.Where(d => d != null)
				.Select(d => d!.Replace("-dev", ""))
				.OrderByDescending(v => v)
				.FirstOrDefault();

			return devDirs;
		}
	}

	/// <summary>
	/// Generate final fully-renamed version
	/// </summary>
	public class FinalizeCommand : Command {
		public FinalizeCommand() : base("finalize", "Generate final fully-renamed version") {
			Option<string?> versionOpt = new Option<string?>("--version", "Version to finalize (default: latest)");

			AddOption(versionOpt);

			this.SetHandler(async version => {
				string? targetVersion = version ?? GetLatestVersion();
				if (targetVersion == null) {
					Logger.Error("No versions found");
					return;
				}

				SimplifiedExtractor extractor = new SimplifiedExtractor();
				await extractor.GenerateFinalAsync(targetVersion);
			}, versionOpt);
		}

		private static string? GetLatestVersion() {
			if (!Directory.Exists("decomp")) return null;

			return Directory.GetDirectories("decomp", "*-dev")
				.Select(d => Path.GetFileName(d)?.Replace("-dev", ""))
				.Where(v => v != null)
				.OrderByDescending(v => v)
				.FirstOrDefault();
		}
	}

	/// <summary>
	/// Clean up old dev folders keeping only the latest
	/// </summary>
	public class CleanupCommand : Command {
		public CleanupCommand() : base("cleanup", "Delete old -dev folders keeping only the latest") {
			Option<int>  keepOpt   = new Option<int>("--keep", () => 1, "Number of recent versions to keep");
			Option<bool> dryRunOpt = new Option<bool>("--dry-run", "Show what would be deleted without deleting");

			AddOption(keepOpt);
			AddOption(dryRunOpt);

			this.SetHandler((keep, dryRun) => {
				if (!Directory.Exists("decomp")) {
					Logger.Info("No decomp directory found");
					return;
				}

				// Find all dev directories sorted by version
				var devDirs = Directory.GetDirectories("decomp", "*-dev")
					.Select(d => new {
						Path    = d,
						Name    = Path.GetFileName(d),
						Version = Path.GetFileName(d)?.Replace("-dev", "")
					})
					.Where(d => d.Version != null)
					.OrderByDescending(d => d.Version)
					.ToList();

				if (devDirs.Count <= keep) {
					Logger.Info($"Only {devDirs.Count} dev folders found, keeping all");
					return;
				}

				var toDelete = devDirs.Skip(keep).ToList();

				if (dryRun) {
					Logger.Info("DRY RUN - Would delete:");
				} else {
					Logger.Info($"Deleting {toDelete.Count} old dev folders...");
				}

				foreach (var dir in toDelete) {
					if (dryRun) {
						Logger.Info($"  Would delete: {dir.Name}");

						// Also check for related dirs
						string finalDir = dir.Path.Replace("-dev", "-final");
						if (Directory.Exists(finalDir)) {
							Logger.Info($"  Would delete: {Path.GetFileName(finalDir)}");
						}
					} else {
						try {
							// Delete dev directory
							Directory.Delete(dir.Path, true);
							Logger.Success($"  Deleted: {dir.Name}");

							// Also delete final directory if exists
							string finalDir = dir.Path.Replace("-dev", "-final");
							if (Directory.Exists(finalDir)) {
								Directory.Delete(finalDir, true);
								Logger.Success($"  Deleted: {Path.GetFileName(finalDir)}");
							}
						} catch (Exception ex) {
							Logger.Error($"  Failed to delete {dir.Name}: {ex.Message}");
						}
					}
				}

				if (!dryRun) {
					Logger.Success($"Cleanup complete. Kept {keep} most recent version(s)");
				}
			}, keepOpt, dryRunOpt);
		}
	}

	/// <summary>
	/// List all versions
	/// </summary>
	public class ListVersionsCommand : Command {
		public ListVersionsCommand() : base("versions", "List all extracted versions") {
			this.SetHandler(() => {
				if (!Directory.Exists("decomp")) {
					Logger.Info("No versions found");
					return;
				}

				List<VersionInfo> versions = new List<VersionInfo>();

				foreach (string dir in Directory.GetDirectories("decomp")) {
					string name = Path.GetFileName(dir);
					if (name.EndsWith("-dev")) {
						string       version = name.Replace("-dev", "");
						VersionInfo? info    = versions.FirstOrDefault(v => v.Version == version);
						if (info == null) {
							info = new VersionInfo { Version = version };
							versions.Add(info);
						}
						info.HasDev       = true;
						info.DevFileCount = Directory.GetFiles(dir, "*.js").Length;
					} else if (name.EndsWith("-final")) {
						string       version = name.Replace("-final", "");
						VersionInfo? info    = versions.FirstOrDefault(v => v.Version == version);
						if (info == null) {
							info = new VersionInfo { Version = version };
							versions.Add(info);
						}
						info.HasFinal = true;
					} else if (Regex.IsMatch(name, @"^\d+\.\d+\.\d+$")) {
						VersionInfo? info = versions.FirstOrDefault(v => v.Version == name);
						if (info == null) {
							info = new VersionInfo { Version = name };
							versions.Add(info);
						}
						info.HasOriginal       = true;
						info.OriginalFileCount = Directory.GetFiles(dir, "*.js").Length;
					}
				}

				Console.WriteLine("\nExtracted Versions:");
				Console.WriteLine("─".PadRight(60, '─'));

				foreach (VersionInfo v in versions.OrderByDescending(v => v.Version)) {
					List<string> status = new List<string>();
					if (v.HasOriginal) status.Add($"original ({v.OriginalFileCount} files)");
					if (v.HasDev) status.Add($"dev ({v.DevFileCount} files)");
					if (v.HasFinal) status.Add("final");

					Console.WriteLine($"{v.Version,-15} {string.Join(", ", status)}");
				}

				// Show current mappings count
				if (File.Exists("decomp/mappings.json")) {
					string                      json     = File.ReadAllText("decomp/mappings.json");
					Dictionary<string, string>? mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
					Console.WriteLine();
					Console.WriteLine($"Symbol mappings: {mappings?.Count ?? 0}");
				}
			});
		}

		private class VersionInfo {
			public string Version           { get; set; } = "";
			public bool   HasOriginal       { get; set; }
			public bool   HasDev            { get; set; }
			public bool   HasFinal          { get; set; }
			public int    OriginalFileCount { get; set; }
			public int    DevFileCount      { get; set; }
		}
	}
}