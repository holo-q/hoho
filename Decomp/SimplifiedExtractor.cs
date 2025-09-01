using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hoho.Core;

namespace Hoho.Decomp {
	/// <summary>
	/// Simplified module extraction with cleaner directory structure
	/// </summary>
	public class SimplifiedExtractor {
		private const string BASE_DIR    = "decomp";
		private const string MAPPINGS_DB = "decomp/mappings.json";

		/// <summary>
		/// Extract modules from a webpack bundle with simplified structure
		/// </summary>
		public async Task<ExtractResult> ExtractAsync(string bundlePath, string version, bool autoDeobfuscate = true) {
			using IDisposable timer = Logger.TimeOperation($"Extract {version}");

			// Setup directories
			string versionDir = Path.Combine(BASE_DIR, version);
			string devDir     = Path.Combine(BASE_DIR, $"{version}-dev");
			string finalDir   = Path.Combine(BASE_DIR, $"{version}-final");

			// Clean existing if needed
			if (Directory.Exists(versionDir)) {
				Logger.Warning($"Version {version} already exists, backing up...");
				string backupDir = $"{versionDir}-backup-{DateTime.Now:yyyyMMdd-HHmmss}";
				Directory.Move(versionDir, backupDir);
			}

			Directory.CreateDirectory(versionDir);
			Directory.CreateDirectory(devDir);

			string           bundleContent = await File.ReadAllTextAsync(bundlePath);
			List<ModuleInfo> modules       = new List<ModuleInfo>();

			// Extract modules using existing patterns
			modules.AddRange(ExtractCommonJSModules(bundleContent));
			modules.AddRange(ExtractClasses(bundleContent));
			modules.AddRange(ExtractFunctions(bundleContent));

			Logger.Info($"Extracted {modules.Count} modules");

			// Save original modules
			foreach (ModuleInfo module in modules) {
				string originalPath = Path.Combine(versionDir, $"{module.Name}.js");
				await File.WriteAllTextAsync(originalPath, module.Content);
			}

			// Copy original bundle
			string bundleCopy = Path.Combine(versionDir, "bundle.js");
			File.Copy(bundlePath, bundleCopy, true);

			// Generate symbol map
			ImprovedSymbolExtractor.ExtractedSymbols symbols       = await ImprovedSymbolExtractor.ExtractSymbolsAsync(bundlePath);
			string                                   symbolReport  = ImprovedSymbolExtractor.GenerateCompactReport(symbols);
			string                                   symbolMapPath = Path.Combine(versionDir, "symbol-map.md");
			await File.WriteAllTextAsync(symbolMapPath, symbolReport);

			// Generate comprehensive bundle analysis
			WebpackBundleAnalyzer.BundleAnalysis bundleAnalysis = await WebpackBundleAnalyzer.AnalyzeBundleAsync(bundlePath);
			string                                analysisReport = bundleAnalysis.GenerateReport();
			string                                analysisPath   = Path.Combine(versionDir, "bundle-analysis.md");
			await File.WriteAllTextAsync(analysisPath, analysisReport);

			// Create dev version
			if (autoDeobfuscate && File.Exists(MAPPINGS_DB)) {
				Logger.Info("Applying known mappings to create dev version...");
				await CreateDevVersionAsync(versionDir, devDir);
			} else {
				// Just copy original to dev
				foreach (string file in Directory.GetFiles(versionDir, "*.js")) {
					string fileName = Path.GetFileName(file);
					if (fileName != "bundle.js") {
						File.Copy(file, Path.Combine(devDir, fileName), true);
					}
				}
			}

			// Create README in dev directory
			string readme = GenerateDevReadme(version, modules.Count, symbols.TotalClasses);
			await File.WriteAllTextAsync(Path.Combine(devDir, "README.md"), readme);

			return new ExtractResult {
				Version        = version,
				ModuleCount    = modules.Count,
				OriginalDir    = versionDir,
				DevDir         = devDir,
				FinalDir       = finalDir,
				BundleAnalysis = bundleAnalysis
			};
		}

		/// <summary>
		/// Create dev version by applying known mappings
		/// </summary>
		private async Task CreateDevVersionAsync(string originalDir, string devDir) {
			// Load mappings
			string json = await File.ReadAllTextAsync(MAPPINGS_DB);
			Dictionary<string, string> mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
			                                      ?? new Dictionary<string, string>();

			int appliedCount = 0;

			foreach (string file in Directory.GetFiles(originalDir, "*.js")) {
				string fileName = Path.GetFileName(file);
				if (fileName == "bundle.js") continue;

				string content  = await File.ReadAllTextAsync(file);
				string modified = content;

				// Apply simple mappings (for globally unique symbols)
				foreach (KeyValuePair<string, string> mapping in mappings.Where(m => IsLikelyGloballyUnique(m.Key))) {
					string pattern    = $@"\b{Regex.Escape(mapping.Key)}\b";
					string newContent = Regex.Replace(modified, pattern, mapping.Value);
					if (newContent != modified) {
						modified = newContent;
						appliedCount++;
					}
				}

				string devPath = Path.Combine(devDir, fileName);
				await File.WriteAllTextAsync(devPath, modified);
			}

			if (appliedCount > 0) {
				Logger.Success($"Applied {appliedCount} mappings to dev version");
			}
		}

		/// <summary>
		/// Check if a symbol is likely globally unique (not single letter, etc)
		/// </summary>
		private bool IsLikelyGloballyUnique(string symbol) {
			// Single letters are NOT globally unique
			if (symbol.Length == 1) return false;

			// Two letters might not be unique
			if (symbol.Length == 2 && char.IsUpper(symbol[0])) return false;

			// Three+ character symbols are likely unique
			return true;
		}

		/// <summary>
		/// Generate final fully-renamed version
		/// </summary>
		public async Task GenerateFinalAsync(string version) {
			using IDisposable timer = Logger.TimeOperation($"Generate final for {version}");

			string devDir   = Path.Combine(BASE_DIR, $"{version}-dev");
			string finalDir = Path.Combine(BASE_DIR, $"{version}-final");

			if (!Directory.Exists(devDir)) {
				Logger.Error($"Dev version {version}-dev not found");
				return;
			}

			// Clean final dir
			if (Directory.Exists(finalDir)) {
				Directory.Delete(finalDir, true);
			}
			Directory.CreateDirectory(finalDir);

			// Copy all files from dev to final
			foreach (string file in Directory.GetFiles(devDir, "*.js")) {
				string fileName  = Path.GetFileName(file);
				string finalPath = Path.Combine(finalDir, fileName);
				File.Copy(file, finalPath);
			}

			// Generate combined bundle
			StringBuilder allContent = new StringBuilder();
			allContent.AppendLine("// Deobfuscated Claude Code " + version);
			allContent.AppendLine("// Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			allContent.AppendLine();

			foreach (string file in Directory.GetFiles(finalDir, "*.js").OrderBy(f => f)) {
				string content = await File.ReadAllTextAsync(file);
				allContent.AppendLine($"// Module: {Path.GetFileName(file)}");
				allContent.AppendLine(content);
				allContent.AppendLine();
			}

			string bundlePath = Path.Combine(finalDir, "deobfuscated-bundle.js");
			await File.WriteAllTextAsync(bundlePath, allContent.ToString());

			Logger.Success($"Generated final version in {finalDir}");
		}

		private List<ModuleInfo> ExtractCommonJSModules(string content) {
			List<ModuleInfo> modules = new List<ModuleInfo>();
			string           pattern = @"var\s+([A-Za-z0-9_]+)\s*=\s*U\s*\(\s*\(([^)]*)\)\s*=>\s*\{";
			MatchCollection  matches = Regex.Matches(content, pattern);

			foreach (Match match in matches) {
				string  moduleName    = match.Groups[1].Value;
				string? moduleContent = ExtractBlockContent(content, match.Index);
				if (moduleContent != null) {
					modules.Add(new ModuleInfo {
						Name    = moduleName,
						Content = moduleContent,
						Type    = "CommonJS"
					});
				}
			}

			return modules;
		}

		private List<ModuleInfo> ExtractClasses(string content) {
			List<ModuleInfo> modules = new List<ModuleInfo>();
			string           pattern = @"class\s+([A-Za-z0-9_]+)(?:\s+extends\s+[^{]+)?\s*\{";
			MatchCollection  matches = Regex.Matches(content, pattern);

			foreach (Match match in matches) {
				string  className    = match.Groups[1].Value;
				string? classContent = ExtractBlockContent(content, match.Index);
				if (classContent != null) {
					modules.Add(new ModuleInfo {
						Name    = className,
						Content = classContent,
						Type    = "Class"
					});
				}
			}

			return modules;
		}

		private List<ModuleInfo> ExtractFunctions(string content) {
			List<ModuleInfo> modules = new List<ModuleInfo>();
			string           pattern = @"function\s+([A-Z][A-Za-z0-9_]*)\s*\([^)]*\)\s*\{";
			MatchCollection  matches = Regex.Matches(content, pattern);

			foreach (Match match in matches) {
				string funcName = match.Groups[1].Value;
				if (funcName.Length <= 4) // Likely obfuscated
				{
					string? funcContent = ExtractBlockContent(content, match.Index);
					if (funcContent != null) {
						modules.Add(new ModuleInfo {
							Name    = funcName,
							Content = funcContent,
							Type    = "Function"
						});
					}
				}
			}

			return modules;
		}

		private string? ExtractBlockContent(string content, int startIndex) {
			int  depth      = 0;
			bool foundStart = false;

			for (int i = startIndex; i < content.Length && i < startIndex + 50000; i++) {
				if (content[i] == '{') {
					if (!foundStart) {
						foundStart = true;
						depth      = 1;
					} else {
						depth++;
					}
				} else if (content[i] == '}' && foundStart) {
					depth--;
					if (depth == 0) {
						return content.Substring(startIndex, i - startIndex + 1);
					}
				}
			}

			return null;
		}

		private string GenerateDevReadme(string version, int moduleCount, int classCount) =>
			$@"# Claude Code {version} Development Version

## Structure
- **{version}/**: Original extracted modules (DO NOT EDIT)
- **{version}-dev/**: Working copy for manual renaming (EDIT HERE)
- **{version}-final/**: Fully renamed version (generated)

## Statistics
- Modules: {moduleCount}
- Classes: {classCount}

## Workflow

1. Edit files in this directory to rename symbols
2. Add mappings: `hoho decomp add-mapping OldName NewName`
3. Apply to all: `hoho decomp rename-all`
4. Generate final: `hoho decomp finalize {version}`

## Quick Commands
```bash
# Show current mappings
hoho decomp mappings

# Rename single file
hoho decomp rename FileName.js

# Apply to all files
hoho decomp rename-all

# Generate final version
hoho decomp finalize {version}
```
";

		public class ExtractResult {
			public string Version     { get; set; } = "";
			public int    ModuleCount { get; set; }
			public string OriginalDir { get; set; } = "";
			public string DevDir      { get; set; } = "";
			public string FinalDir    { get; set; } = "";
			public WebpackBundleAnalyzer.BundleAnalysis? BundleAnalysis { get; set; }
		}

		public class ModuleInfo {
			public string Name    { get; set; } = "";
			public string Content { get; set; } = "";
			public string Type    { get; set; } = "";
		}
	}
}