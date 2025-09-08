using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hoho.Core;

namespace Hoho.Decomp {
	/// <summary>
	/// Manages multiple versions of Claude Code with directory-based organization
	/// </summary>
	public class VersionManager {
		private readonly string                          _baseDir;
		private readonly DecompilationMapper             _mapper;
		private readonly Dictionary<string, VersionInfo> _versions = new Dictionary<string, VersionInfo>();

		public VersionManager(string baseDir = "claude-code-dev") {
			_baseDir = baseDir;
			_mapper  = new DecompilationMapper(Path.Combine(_baseDir, "mappings", "global-mappings.json"));
			LoadVersionRegistry();
		}

		/// <summary>
		/// Directory structure:
		/// claude-code-dev/
		/// mappings/
		/// global-mappings.json      - All learned mappings
		/// version-mappings/         - Per-version specific mappings
		/// versions/
		/// 1.0.98/
		/// original/              - Original obfuscated code
		/// cli.js
		/// sdk.mjs
		/// modules/
		/// Wu1.js
		/// Bx2.js
		/// manual/                - Your manual edits
		/// cli.js
		/// sdk.mjs
		/// modules/
		/// ReactModule.js     - Wu1 renamed
		/// FileUtils.js       - Bx2 renamed
		/// automated/             - Tool-generated deobfuscation
		/// analysis/              - Reports and analysis
		/// 1.0.99/
		/// original/
		/// manual/
		/// automated/
		/// registry.json              - Version tracking
		/// </summary>
		/// <summary>
		/// Setup a new version for decompilation
		/// </summary>
		public async Task<string> SetupVersionAsync(string version, string? sourcePath = null) {
			string versionDir   = Path.Combine(_baseDir, "versions", version);
			string originalDir  = Path.Combine(versionDir, "original");
			string manualDir    = Path.Combine(versionDir, "manual");
			string automatedDir = Path.Combine(versionDir, "automated");
			string analysisDir  = Path.Combine(versionDir, "analysis");

			// Create directory structure
			Directory.CreateDirectory(originalDir);
			Directory.CreateDirectory(Path.Combine(originalDir, "modules"));
			Directory.CreateDirectory(manualDir);
			Directory.CreateDirectory(Path.Combine(manualDir, "modules"));
			Directory.CreateDirectory(automatedDir);
			Directory.CreateDirectory(Path.Combine(automatedDir, "modules"));
			Directory.CreateDirectory(analysisDir);

			// Download or copy source
			if (sourcePath == null) {
				sourcePath = await DownloadVersionAsync(version);
			}

			// Extract and organize
			if (sourcePath.EndsWith(".tgz") || sourcePath.EndsWith(".tar.gz")) {
				await ExtractTarballAsync(sourcePath, originalDir);
			} else if (File.Exists(sourcePath)) {
				// Single file (like cli.js)
				string destPath = Path.Combine(originalDir, Path.GetFileName(sourcePath));
				File.Copy(sourcePath, destPath, true);

				// Extract modules from bundle
				await ExtractModulesFromBundleAsync(destPath, Path.Combine(originalDir, "modules"));
			}

			// Create initial structure in manual directory
			await CreateManualTemplateAsync(originalDir, manualDir);

			// Register version
			_versions[version] = new VersionInfo {
				Version   = version,
				Path      = versionDir,
				DateAdded = DateTime.UtcNow,
				Status    = VersionStatus.Setup
			};

			await SaveVersionRegistry();

			Logger.Info($"Version {version} setup complete at {versionDir}");
			Logger.Info("Next steps:");
			Logger.Info($"1. Edit files in {manualDir} with your clean names");
			Logger.Info($"2. Run: hoho decomp learn-dir {version}");

			return versionDir;
		}

		/// <summary>
		/// Learn mappings from an entire directory of manual edits
		/// </summary>
		public async Task<LearningResult> LearnFromDirectoryAsync(string version) {
			string versionDir  = Path.Combine(_baseDir, "versions", version);
			string originalDir = Path.Combine(versionDir, "original");
			string manualDir   = Path.Combine(versionDir, "manual");

			if (!Directory.Exists(manualDir)) {
				throw new DirectoryNotFoundException($"Manual directory not found: {manualDir}");
			}

			LearningResult result = new LearningResult { Version = version };

			// Process main files
			string[] mainFiles = new[] { "cli.js", "sdk.mjs", "sdk.d.ts" };
			foreach (string file in mainFiles) {
				string origPath   = Path.Combine(originalDir, file);
				string manualPath = Path.Combine(manualDir, file);

				if (File.Exists(origPath) && File.Exists(manualPath)) {
					Logger.Info($"Learning from {file}...");
					MappingResult mappings = await _mapper.LearnMappings(origPath, manualPath);
					result.FileMappings[file] =  mappings;
					result.TotalMappings      += mappings.TotalMappings;
				}
			}

			// Process module directory
			string origModulesDir   = Path.Combine(originalDir, "modules");
			string manualModulesDir = Path.Combine(manualDir, "modules");

			if (Directory.Exists(origModulesDir) && Directory.Exists(manualModulesDir)) {
				// Match files by structure, not name (since they've been renamed)
				List<(string original, string manual)> matches = await MatchModuleFilesAsync(origModulesDir, manualModulesDir);

				foreach ((string origFile, string manualFile) in matches) {
					Logger.Info($"Learning from module {Path.GetFileName(origFile)} -> {Path.GetFileName(manualFile)}");
					MappingResult mappings = await _mapper.LearnMappings(origFile, manualFile);

					string moduleKey = $"modules/{Path.GetFileName(origFile)}";
					result.FileMappings[moduleKey] =  mappings;
					result.TotalMappings           += mappings.TotalMappings;

					// Learn file renaming pattern
					string origName   = Path.GetFileNameWithoutExtension(origFile);
					string manualName = Path.GetFileNameWithoutExtension(manualFile);
					result.FileRenames[origName] = manualName;
				}
			}

			// Save version-specific mappings
			string versionMappingPath = Path.Combine(_baseDir, "mappings", "version-mappings", $"{version}.json");
			Directory.CreateDirectory(Path.GetDirectoryName(versionMappingPath)!);

			string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
			await File.WriteAllTextAsync(versionMappingPath, json);

			// Update version status
			if (_versions.ContainsKey(version)) {
				_versions[version].Status        = VersionStatus.Learned;
				_versions[version].MappingsCount = result.TotalMappings;
				await SaveVersionRegistry();
			}

			Logger.Info($"Learned {result.TotalMappings} total mappings from version {version}");

			return result;
		}

		/// <summary>
		/// Apply mappings to a new version using all learned knowledge
		/// </summary>
		public async Task ApplyMappingsToVersionAsync(string targetVersion, string? sourceVersion = null) {
			string targetDir    = Path.Combine(_baseDir, "versions", targetVersion);
			string originalDir  = Path.Combine(targetDir, "original");
			string automatedDir = Path.Combine(targetDir, "automated");

			// Use most recent learned version if not specified
			if (sourceVersion == null) {
				sourceVersion = _versions
					.Where(v => v.Value.Status == VersionStatus.Learned)
					.OrderByDescending(v => v.Value.DateAdded)
					.Select(v => v.Key)
					.FirstOrDefault();

				if (sourceVersion == null) {
					throw new InvalidOperationException("No learned versions found. Run learn-dir first.");
				}
			}

			Logger.Info($"Applying mappings from {sourceVersion} to {targetVersion}");

			// Apply to main files
			string[] mainFiles = Directory.GetFiles(originalDir, "*.*", SearchOption.TopDirectoryOnly);
			foreach (string origFile in mainFiles) {
				string fileName   = Path.GetFileName(origFile);
				string outputPath = Path.Combine(automatedDir, fileName);

				Logger.Info($"Processing {fileName}...");
				string deobfuscated = await _mapper.ApplyMappings(origFile);
				await File.WriteAllTextAsync(outputPath, deobfuscated);
			}

			// Apply to modules
			string modulesDir = Path.Combine(originalDir, "modules");
			if (Directory.Exists(modulesDir)) {
				string outputModulesDir = Path.Combine(automatedDir, "modules");
				Directory.CreateDirectory(outputModulesDir);

				// Load file renames from previous version
				Dictionary<string, string> fileRenames = await LoadFileRenamesAsync(sourceVersion);

				foreach (string moduleFile in Directory.GetFiles(modulesDir)) {
					string origName   = Path.GetFileNameWithoutExtension(moduleFile);
					string newName    = fileRenames.TryGetValue(origName, out string? renamed) ? renamed : origName;
					string outputPath = Path.Combine(outputModulesDir, $"{newName}.js");

					Logger.Info($"Processing module {origName} -> {newName}...");
					string deobfuscated = await _mapper.ApplyMappings(moduleFile);
					await File.WriteAllTextAsync(outputPath, deobfuscated);
				}
			}

			// Generate comparison report
			string report     = await GenerateComparisonReportAsync(targetVersion, originalDir, automatedDir);
			string reportPath = Path.Combine(targetDir, "analysis", "automated-deobfuscation-report.md");
			await File.WriteAllTextAsync(reportPath, report);

			// Update status
			if (_versions.ContainsKey(targetVersion)) {
				_versions[targetVersion].Status = VersionStatus.Automated;
				await SaveVersionRegistry();
			}

			Logger.Info($"Automated deobfuscation complete for {targetVersion}");
			Logger.Info($"Output: {automatedDir}");
			Logger.Info($"Report: {reportPath}");
		}

		/// <summary>
		/// Extract individual modules from webpack bundle
		/// </summary>
		private async Task ExtractModulesFromBundleAsync(string bundlePath, string outputDir) {
			string content = await File.ReadAllTextAsync(bundlePath);

			// Extract CommonJS modules: var X = U((Y) => { ... })
			string          modulePattern = @"var\s+(\w+)\s*=\s*U\(\((\w+)\)\s*=>\s*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}\)";
			MatchCollection matches       = Regex.Matches(content, modulePattern, RegexOptions.Singleline);

			foreach (Match match in matches) {
				string moduleName    = match.Groups[1].Value;
				string moduleContent = match.Value;

				string modulePath = Path.Combine(outputDir, $"{moduleName}.js");
				await File.WriteAllTextAsync(modulePath, moduleContent);
			}

			// Extract classes
			string classPattern = @"class\s+(\w+)(?:\s+extends\s+\w+)?\s*\{(?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!))\}";
			matches = Regex.Matches(content, classPattern, RegexOptions.Singleline);

			foreach (Match match in matches) {
				string className    = match.Groups[1].Value;
				string classContent = match.Value;

				string classPath = Path.Combine(outputDir, $"{className}.js");
				await File.WriteAllTextAsync(classPath, classContent);
			}

			Logger.Info($"Extracted {matches.Count} modules to {outputDir}");
		}

		/// <summary>
		/// Create template structure in manual directory
		/// </summary>
		private async Task CreateManualTemplateAsync(string originalDir, string manualDir) {
			// Copy structure with README files
			string readmeContent = @"# Manual Decompilation Directory

Edit the files in this directory with your clean, deobfuscated names.

## Guidelines:
1. Keep the same file structure as the original
2. Use meaningful names for all symbols
3. Add comments where helpful
4. Preserve functionality - only rename, don't refactor logic

## File Mapping:
Original obfuscated files are in: ../original/
Your clean versions go here: ./

The tool will learn from your edits and apply them to future versions.
";

			await File.WriteAllTextAsync(Path.Combine(manualDir, "README.md"), readmeContent);

			// Create placeholder for main files
			foreach (string file in Directory.GetFiles(originalDir, "*.*", SearchOption.TopDirectoryOnly)) {
				string fileName    = Path.GetFileName(file);
				string placeholder = $"// Manual deobfuscation of {fileName}\n// Original: ../original/{fileName}\n\n";
				await File.WriteAllTextAsync(Path.Combine(manualDir, fileName), placeholder);
			}
		}

		/// <summary>
		/// Match module files between original and manual directories
		/// </summary>
		private async Task<List<(string original, string manual)>> MatchModuleFilesAsync(string origDir, string manualDir) {
			List<(string, string)> matches     = new List<(string, string)>();
			string[]               origFiles   = Directory.GetFiles(origDir, "*.js");
			string[]               manualFiles = Directory.GetFiles(manualDir, "*.js");

			// Use structural matching to pair files
			foreach (string origFile in origFiles) {
				string  origContent = await File.ReadAllTextAsync(origFile);
				double  bestScore   = 0;
				string? bestMatch   = null;

				foreach (string manualFile in manualFiles) {
					if (matches.Any(m => m.Item2 == manualFile)) continue; // Already matched

					string manualContent = await File.ReadAllTextAsync(manualFile);
					double score         = CalculateStructuralSimilarity(origContent, manualContent);

					if (score > bestScore && score > 0.6) // 60% threshold
					{
						bestScore = score;
						bestMatch = manualFile;
					}
				}

				if (bestMatch != null) {
					matches.Add((origFile, bestMatch));
				}
			}

			return matches;
		}

		private double CalculateStructuralSimilarity(string orig, string edited) {
			// Extract structural elements
			List<string> origStructure   = ExtractStructure(orig);
			List<string> editedStructure = ExtractStructure(edited);

			// Compare control flow, string literals, etc.
			int origFlow   = Regex.Matches(orig, @"\b(if|else|for|while|switch|return|throw|try|catch)\b").Count;
			int editedFlow = Regex.Matches(edited, @"\b(if|else|for|while|switch|return|throw|try|catch)\b").Count;

			double flowSimilarity = 1.0 - Math.Abs(origFlow - editedFlow) / (double)Math.Max(Math.Max(origFlow, editedFlow), 1);

			// Compare string literals
			HashSet<string> origStrings   = ExtractStringLiterals(orig);
			HashSet<string> editedStrings = ExtractStringLiterals(edited);

			int commonStrings = origStrings.Intersect(editedStrings).Count();
			double stringSimilarity = origStrings.Count > 0
				? commonStrings / (double)origStrings.Count
				: 0;

			return flowSimilarity * 0.6 + stringSimilarity * 0.4;
		}

		private List<string> ExtractStructure(string code) {
			List<string> structure = new List<string>();

			// Function definitions
			structure.AddRange(Regex.Matches(code, @"function\s*\w*\s*\([^)]*\)")
				.Select(m => "function"));

			// Class definitions  
			structure.AddRange(Regex.Matches(code, @"class\s+\w+")
				.Select(m => "class"));

			// Control flow
			structure.AddRange(Regex.Matches(code, @"\b(if|else|for|while|switch)\b")
				.Select(m => m.Value));

			return structure;
		}

		private HashSet<string> ExtractStringLiterals(string code) {
			HashSet<string> strings = new HashSet<string>();

			MatchCollection matches = Regex.Matches(code, @"[""']([^""']{3,})[""']");
			foreach (Match match in matches) {
				strings.Add(match.Groups[1].Value);
			}

			return strings;
		}

		private async Task<Dictionary<string, string>> LoadFileRenamesAsync(string version) {
			string mappingPath = Path.Combine(_baseDir, "mappings", "version-mappings", $"{version}.json");

			if (!File.Exists(mappingPath))
				return new Dictionary<string, string>();

			string          json   = await File.ReadAllTextAsync(mappingPath);
			LearningResult? result = JsonSerializer.Deserialize<LearningResult>(json);

			return result?.FileRenames ?? new Dictionary<string, string>();
		}

		private async Task<string> GenerateComparisonReportAsync(string version, string originalDir, string automatedDir) {
			StringBuilder sb = new StringBuilder();

			sb.AppendLine($"# Automated Deobfuscation Report - Version {version}");
			sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			sb.AppendLine();

			sb.AppendLine("## Files Processed");

			string[] files = Directory.GetFiles(automatedDir, "*.*", SearchOption.AllDirectories);
			sb.AppendLine($"Total files: {files.Length}");
			sb.AppendLine();

			// Sample comparison
			sb.AppendLine("## Sample Transformations");

			foreach (string file in files.Take(3)) {
				string fileName = Path.GetFileName(file);
				string origPath = Path.Combine(originalDir, Path.GetRelativePath(automatedDir, file));

				if (File.Exists(origPath)) {
					string origContent = await File.ReadAllTextAsync(origPath);
					string newContent  = await File.ReadAllTextAsync(file);

					// Show first function transformation
					Match origFunc = Regex.Match(origContent, @"function\s+(\w+)");
					Match newFunc  = Regex.Match(newContent, @"function\s+(\w+)");

					if (origFunc.Success && newFunc.Success) {
						sb.AppendLine($"### {fileName}");
						sb.AppendLine($"- Original: `function {origFunc.Groups[1].Value}`");
						sb.AppendLine($"- Deobfuscated: `function {newFunc.Groups[1].Value}`");
						sb.AppendLine();
					}
				}
			}

			return sb.ToString();
		}

		private async Task<string> DownloadVersionAsync(string version) {
			string cachePath = Path.Combine(_baseDir, "downloads", $"claude-code-{version}.tgz");

			if (File.Exists(cachePath)) {
				Logger.Info($"Using cached download: {cachePath}");
				return cachePath;
			}

			Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

			using HttpClient http = new HttpClient();
			string           url  = $"https://registry.npmjs.org/@anthropic-ai/claude-code/-/claude-code-{version}.tgz";

			Logger.Info($"Downloading {url}...");
			byte[] bytes = await http.GetByteArrayAsync(url);
			await File.WriteAllBytesAsync(cachePath, bytes);

			return cachePath;
		}

		private async Task ExtractTarballAsync(string tarPath, string outputDir) {
			// Simple extraction using tar command
			Process process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName               = "tar",
					Arguments              = $"-xzf \"{tarPath}\" -C \"{outputDir}\" --strip-components=1",
					RedirectStandardOutput = true,
					RedirectStandardError  = true,
					UseShellExecute        = false
				}
			};

			process.Start();
			await process.WaitForExitAsync();

			if (process.ExitCode != 0) {
				string error = await process.StandardError.ReadToEndAsync();
				throw new Exception($"Failed to extract tarball: {error}");
			}
		}

		private void LoadVersionRegistry() {
			string registryPath = Path.Combine(_baseDir, "registry.json");

			if (!File.Exists(registryPath)) return;

			try {
				string                           json     = File.ReadAllText(registryPath);
				Dictionary<string, VersionInfo>? registry = JsonSerializer.Deserialize<Dictionary<string, VersionInfo>>(json);

				if (registry != null) {
					_versions.Clear();
					foreach (KeyValuePair<string, VersionInfo> kvp in registry) {
						_versions[kvp.Key] = kvp.Value;
					}
				}
			} catch (Exception ex) {
				Logger.Warn($"Failed to load version registry: {ex.Message}");
			}
		}

		private async Task SaveVersionRegistry() {
			string registryPath = Path.Combine(_baseDir, "registry.json");
			Directory.CreateDirectory(Path.GetDirectoryName(registryPath)!);

			string json = JsonSerializer.Serialize(_versions, new JsonSerializerOptions {
				WriteIndented = true
			});

			await File.WriteAllTextAsync(registryPath, json);
		}

		/// <summary>
		/// List all managed versions
		/// </summary>
		public void ListVersions() {
			Console.WriteLine("\nManaged Claude Code Versions:");
			Console.WriteLine("=".PadRight(60, '='));

			foreach (KeyValuePair<string, VersionInfo> version in _versions.OrderByDescending(v => v.Value.DateAdded)) {
				VersionInfo info = version.Value;
				Console.WriteLine($"\n{info.Version}:");
				Console.WriteLine($"  Status: {info.Status}");
				Console.WriteLine($"  Added: {info.DateAdded:yyyy-MM-dd}");

				if (info.MappingsCount > 0) {
					Console.WriteLine($"  Mappings: {info.MappingsCount}");
				}

				Console.WriteLine($"  Path: {info.Path}");
			}
		}
	}

// Data models
	public class VersionInfo {
		public string        Version       { get; set; } = "";
		public string        Path          { get; set; } = "";
		public DateTime      DateAdded     { get; set; }
		public VersionStatus Status        { get; set; }
		public int           MappingsCount { get; set; }
	}

	public enum VersionStatus {
		Setup,      // Directory created, waiting for manual edits
		InProgress, // Manual editing in progress
		Learned,    // Mappings learned from manual edits
		Automated   // Automated deobfuscation applied
	}

	public class LearningResult {
		public string                            Version       { get; set; } = "";
		public int                               TotalMappings { get; set; }
		public Dictionary<string, MappingResult> FileMappings  { get; set; } = new Dictionary<string, MappingResult>();
		public Dictionary<string, string>        FileRenames   { get; set; } = new Dictionary<string, string>();
	}
}