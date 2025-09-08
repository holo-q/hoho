using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hoho.Core;

namespace Hoho.Decomp {
	/// <summary>
	/// Automated decompilation pipeline that maximizes late-stage automation
	/// to handle version updates with minimal manual intervention.
	/// </summary>
	public static class AutomatedDecompPipeline {
		/// <summary>
		/// Complete automated decompilation with checkpointing
		/// </summary>
		public static async Task<DecompSession> RunPipelineAsync(string bundlePath, string? previousSessionPath = null) {
			using IDisposable timer = Logger.TimeOperation("Automated decomp pipeline");

			DecompSession session = new DecompSession {
				SourcePath = bundlePath,
				Version    = await ExtractVersionAsync(bundlePath),
				Timestamp  = DateTime.UtcNow
			};

			// Load previous session for differential analysis
			DecompSession? previousSession = null;
			if (previousSessionPath != null && File.Exists(previousSessionPath)) {
				string json = await File.ReadAllTextAsync(previousSessionPath);
				previousSession = JsonSerializer.Deserialize<DecompSession>(json);
			}

			// Stage 1: Module Extraction (Fully Automated)
			session.Modules = await ExtractModulesAsync(bundlePath);
			Logger.Info($"Extracted {session.Modules.Count} modules");

			// Stage 2: Semantic Analysis (Fully Automated)
			await RunSemanticAnalysisAsync(session);

			// Stage 3: Symbol Renaming (Automated with Confidence Scoring)
			await AutoRenameSymbolsAsync(session);

			// Stage 4: Differential Analysis (If Previous Version Exists)
			if (previousSession != null) {
				session.DiffAnalysis = await AnalyzeDifferencesAsync(previousSession, session);
				Logger.Info($"Found {session.DiffAnalysis.ChangedModules.Count} changed modules");
			}

			// Stage 5: Checkpoint Generation
			await SaveCheckpointAsync(session);

			// Stage 6: Code Generation (Final Output)
			await GenerateRefactoredCodeAsync(session);

			return session;
		}

		/// <summary>
		/// Extract all modules with boundaries intact
		/// </summary>
		private static async Task<List<ExtractedModule>> ExtractModulesAsync(string bundlePath) {
			string                content = await File.ReadAllTextAsync(bundlePath);
			List<ExtractedModule> modules = new List<ExtractedModule>();

			// Pattern 1: CommonJS modules - var X = U((Y) => { ... })
			string          modulePattern = @"var\s+(\w+)\s*=\s*U\(\((\w+)\)\s*=>\s*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}\)";
			MatchCollection matches       = Regex.Matches(content, modulePattern, RegexOptions.Singleline);

			foreach (Match match in matches) {
				ExtractedModule module = new ExtractedModule {
					Id            = Guid.NewGuid().ToString(),
					OriginalName  = match.Groups[1].Value,
					ParameterName = match.Groups[2].Value,
					Body          = match.Groups[3].Value,
					Hash          = ComputeHash(match.Groups[3].Value),
					Type          = ModuleType.CommonJS
				};

				// Extract exports
				module.Exports = ExtractExports(module.Body);
				module.Imports = ExtractImports(module.Body);

				modules.Add(module);
			}

			// Pattern 2: React Components
			string componentPattern = @"function\s+([A-Z]\w+)\s*\(([^)]*)\)\s*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}";
			matches = Regex.Matches(content, componentPattern, RegexOptions.Singleline);

			foreach (Match match in matches) {
				ExtractedModule module = new ExtractedModule {
					Id            = Guid.NewGuid().ToString(),
					OriginalName  = match.Groups[1].Value,
					ParameterName = match.Groups[2].Value,
					Body          = match.Groups[3].Value,
					Hash          = ComputeHash(match.Groups[3].Value),
					Type          = ModuleType.ReactComponent
				};

				modules.Add(module);
			}

			// Pattern 3: Classes
			string classPattern = @"class\s+(\w+)(?:\s+extends\s+(\w+))?\s*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}";
			matches = Regex.Matches(content, classPattern, RegexOptions.Singleline);

			foreach (Match match in matches) {
				ExtractedModule module = new ExtractedModule {
					Id           = Guid.NewGuid().ToString(),
					OriginalName = match.Groups[1].Value,
					SuperClass   = match.Groups[2].Value,
					Body         = match.Groups[3].Value,
					Hash         = ComputeHash(match.Groups[3].Value),
					Type         = ModuleType.Class
				};

				modules.Add(module);
			}

			return modules;
		}

		/// <summary>
		/// Run semantic analysis to understand module purposes
		/// </summary>
		private static async Task RunSemanticAnalysisAsync(DecompSession session) {
			foreach (ExtractedModule module in session.Modules) {
				SemanticAnalysis analysis = new SemanticAnalysis();

				// Analyze string literals for clues
				string stringPattern = @"[""'`]([^""'`]{3,})[""'`]";
				List<string> strings = Regex.Matches(module.Body, stringPattern)
					.Select(m => m.Groups[1].Value)
					.ToList();

				// Categorize by content
				if (strings.Any(s => s.Contains("CREATE TABLE") || s.Contains("SELECT")))
					analysis.Categories.Add("Database");
				if (strings.Any(s => s.Contains("http") || s.Contains("fetch")))
					analysis.Categories.Add("Network");
				if (strings.Any(s => s.Contains("useState") || s.Contains("useEffect")))
					analysis.Categories.Add("ReactHooks");
				if (strings.Any(s => s.Contains("fs.") || s.Contains("path.")))
					analysis.Categories.Add("FileSystem");
				if (strings.Any(s => s.Contains("child_process") || s.Contains("spawn")))
					analysis.Categories.Add("Process");

				// Analyze API calls
				if (module.Body.Contains("fetch(") || module.Body.Contains("axios"))
					analysis.Features.Add("HTTP");
				if (module.Body.Contains("WebSocket"))
					analysis.Features.Add("WebSocket");
				if (module.Body.Contains("localStorage") || module.Body.Contains("sessionStorage"))
					analysis.Features.Add("Storage");

				// Detect tool implementations
				Dictionary<string, string> toolPatterns = new Dictionary<string, string> {
					["FileRead"]  = @"readFile|readFileSync|createReadStream",
					["FileWrite"] = @"writeFile|writeFileSync|createWriteStream",
					["FileEdit"]  = @"replaceInFile|modifyFile",
					["Bash"]      = @"exec|spawn|execSync",
					["Grep"]      = @"search|findInFiles|grep",
					["WebSearch"] = @"search|query|google",
					["TodoWrite"] = @"todo|task|addTask"
				};

				foreach ((string tool, string pattern) in toolPatterns) {
					if (Regex.IsMatch(module.Body, pattern, RegexOptions.IgnoreCase)) {
						analysis.LikelyTools.Add(tool);
					}
				}

				// Calculate confidence scores
				analysis.ConfidenceScore = CalculateConfidence(analysis);

				module.SemanticAnalysis = analysis;
			}

			await Task.CompletedTask;
		}

		/// <summary>
		/// Automated symbol renaming with confidence scoring
		/// </summary>
		private static async Task AutoRenameSymbolsAsync(DecompSession session) {
			RenamingEngine renamingRules = new RenamingEngine();

			// Build frequency map
			Dictionary<string, int> symbolFrequency = new Dictionary<string, int>();
			foreach (ExtractedModule module in session.Modules) {
				IEnumerable<string> symbols = Regex.Matches(module.Body, @"\b([a-z][a-zA-Z0-9]{1,3})\b")
					.Select(m => m.Groups[1].Value);

				foreach (string symbol in symbols) {
					symbolFrequency.TryGetValue(symbol, out int count);
					symbolFrequency[symbol] = count + 1;
				}
			}

			// Apply renaming rules based on patterns and context
			foreach (ExtractedModule module in session.Modules) {
				Dictionary<string, RenameInfo> renames = new Dictionary<string, RenameInfo>();

				// Rule 1: React Components (high confidence)
				if (module.Type == ModuleType.ReactComponent) {
					// Components that use hooks are definitely React components
					if (module.Body.Contains("useState") || module.Body.Contains("useEffect")) {
						string baseName = InferComponentName(module.Body);
						renames[module.OriginalName] = new RenameInfo {
							NewName    = baseName + "Component",
							Confidence = 0.9f,
							Reason     = "React component with hooks"
						};
					}
				}

				// Rule 2: Tool implementations (high confidence)
				foreach (string tool in module.SemanticAnalysis?.LikelyTools ?? new List<string>()) {
					string implName = $"{tool}Implementation";
					if (!renames.ContainsKey(module.OriginalName)) {
						renames[module.OriginalName] = new RenameInfo {
							NewName    = implName,
							Confidence = 0.85f,
							Reason     = $"Implements {tool} tool"
						};
					}
				}

				// Rule 3: Database modules (medium confidence)
				if (module.SemanticAnalysis?.Categories.Contains("Database") == true) {
					renames[module.OriginalName] = new RenameInfo {
						NewName    = "Database" + module.OriginalName,
						Confidence = 0.7f,
						Reason     = "Contains SQL operations"
					};
				}

				// Rule 4: Network modules (medium confidence)
				if (module.SemanticAnalysis?.Categories.Contains("Network") == true) {
					renames[module.OriginalName] = new RenameInfo {
						NewName    = "Network" + module.OriginalName,
						Confidence = 0.7f,
						Reason     = "Contains network operations"
					};
				}

				// Rule 5: High-frequency utilities (low confidence)
				if (symbolFrequency.TryGetValue(module.OriginalName, out int freq) && freq > 1000) {
					renames[module.OriginalName] = new RenameInfo {
						NewName    = "CoreUtil" + module.OriginalName,
						Confidence = 0.5f,
						Reason     = "High frequency usage"
					};
				}

				module.ProposedRenames = renames;
			}

			await Task.CompletedTask;
		}

		/// <summary>
		/// Analyze differences between versions
		/// </summary>
		private static async Task<DiffAnalysis> AnalyzeDifferencesAsync(DecompSession previous, DecompSession current) {
			DiffAnalysis diff = new DiffAnalysis();

			// Map modules by hash for exact matches
			Dictionary<string, ExtractedModule> previousByHash = previous.Modules.ToDictionary(m => m.Hash);
			Dictionary<string, ExtractedModule> currentByHash  = current.Modules.ToDictionary(m => m.Hash);

			// Find unchanged modules (same hash)
			foreach (ExtractedModule module in current.Modules) {
				if (previousByHash.ContainsKey(module.Hash)) {
					diff.UnchangedModules.Add(module.Id);
					// Carry over high-confidence renames from previous version
					ExtractedModule prevModule = previousByHash[module.Hash];
					if (prevModule.ProposedRenames != null) {
						module.ProposedRenames = prevModule.ProposedRenames;
					}
				}
			}

			// Find new modules
			foreach (ExtractedModule module in current.Modules) {
				if (!previousByHash.ContainsKey(module.Hash)) {
					diff.NewModules.Add(module.Id);
				}
			}

			// Find removed modules
			foreach (ExtractedModule module in previous.Modules) {
				if (!currentByHash.ContainsKey(module.Hash)) {
					diff.RemovedModules.Add(module.Id);
				}
			}

			// Find changed modules (fuzzy matching by structure)
			foreach (ExtractedModule currModule in current.Modules.Where(m => diff.NewModules.Contains(m.Id))) {
				MatchResult? bestMatch = FindBestMatch(currModule, previous.Modules);
				if (bestMatch != null && bestMatch.Similarity > 0.8f) {
					diff.ChangedModules[currModule.Id] = new ModuleChange {
						PreviousId = bestMatch.Module.Id,
						Similarity = bestMatch.Similarity,
						Changes    = IdentifyChanges(bestMatch.Module.Body, currModule.Body)
					};

					// Remove from new modules list
					diff.NewModules.Remove(currModule.Id);
				}
			}

			return diff;
		}

		/// <summary>
		/// Save checkpoint for incremental processing
		/// </summary>
		private static async Task SaveCheckpointAsync(DecompSession session) {
			string checkpointDir = Path.Combine("decomp", "checkpoints", session.Version);
			Directory.CreateDirectory(checkpointDir);

			// Save session metadata
			string sessionPath = Path.Combine(checkpointDir, "session.json");
			string json = JsonSerializer.Serialize(session, new JsonSerializerOptions {
				WriteIndented          = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			});
			await File.WriteAllTextAsync(sessionPath, json);

			// Save individual modules for easy diffing
			string modulesDir = Path.Combine(checkpointDir, "modules");
			Directory.CreateDirectory(modulesDir);

			foreach (ExtractedModule module in session.Modules) {
				string modulePath = Path.Combine(modulesDir, $"{module.Hash}.js");
				await File.WriteAllTextAsync(modulePath, module.Body);

				// Save metadata
				string metaPath = Path.Combine(modulesDir, $"{module.Hash}.meta.json");
				string metaJson = JsonSerializer.Serialize(module, new JsonSerializerOptions { WriteIndented = true });
				await File.WriteAllTextAsync(metaPath, metaJson);
			}

			Logger.Info($"Checkpoint saved to {checkpointDir}");
		}

		/// <summary>
		/// Generate final refactored code
		/// </summary>
		private static async Task GenerateRefactoredCodeAsync(DecompSession session) {
			string outputDir = Path.Combine("decomp", "output", session.Version);
			Directory.CreateDirectory(outputDir);

			// Group modules by category
			IEnumerable<IGrouping<string, ExtractedModule>> byCategory = session.Modules
				.GroupBy(m => m.SemanticAnalysis?.Categories.FirstOrDefault() ?? "Uncategorized");

			foreach (IGrouping<string, ExtractedModule> category in byCategory) {
				string categoryDir = Path.Combine(outputDir, category.Key.ToLower());
				Directory.CreateDirectory(categoryDir);

				foreach (ExtractedModule module in category) {
					// Apply high-confidence renames
					string code = module.Body;
					foreach (KeyValuePair<string, RenameInfo> rename in module.ProposedRenames?.Where(r => r.Value.Confidence > 0.7f) ?? new Dictionary<string, RenameInfo>()) {
						code = Regex.Replace(code, $@"\b{Regex.Escape(rename.Key)}\b", rename.Value.NewName);
					}

					// Generate file name
					string fileName = module.ProposedRenames?.Values
						.Where(r => r.Confidence > 0.7f)
						.Select(r => r.NewName)
						.FirstOrDefault() ?? module.OriginalName;

					string filePath = Path.Combine(categoryDir, $"{fileName}.js");

					// Add header with metadata
					string header = $"""
					                 /**
					                  * Original: {module.OriginalName}
					                  * Hash: {module.Hash}
					                  * Type: {module.Type}
					                  * Categories: {string.Join(", ", module.SemanticAnalysis?.Categories ?? new List<string>())}
					                  * Confidence: {module.ProposedRenames?.Values.Max(r => r.Confidence) ?? 0:P}
					                  */

					                 """;

					await File.WriteAllTextAsync(filePath, header + code);
				}
			}

			// Generate index and dependency graph
			string indexPath = Path.Combine(outputDir, "index.md");
			string index     = GenerateIndex(session);
			await File.WriteAllTextAsync(indexPath, index);
		}

		// Helper methods
		private static string ComputeHash(string content) {
			using SHA256 sha256 = SHA256.Create();
			byte[]       bytes  = Encoding.UTF8.GetBytes(content);
			byte[]       hash   = sha256.ComputeHash(bytes);
			return Convert.ToBase64String(hash).Substring(0, 12);
		}

		private static List<string> ExtractExports(string body) {
			List<string> exports = new List<string>();
			string[] patterns = new[] {
				@"exports\.(\w+)",
				@"module\.exports\.(\w+)",
				@"export\s+(?:const|let|var|function|class)\s+(\w+)",
				@"export\s+\{\s*([^}]+)\s*\}"
			};

			foreach (string pattern in patterns) {
				MatchCollection matches = Regex.Matches(body, pattern);
				foreach (Match match in matches) {
					if (match.Groups.Count > 1) {
						exports.Add(match.Groups[1].Value);
					}
				}
			}

			return exports.Distinct().ToList();
		}

		private static List<string> ExtractImports(string body) {
			List<string> imports = new List<string>();
			string[] patterns = new[] {
				@"require\(['""]([^'""]+)['""]\)",
				@"import\s+.*\s+from\s+['""]([^'""]+)['""]",
				@"import\(['""]([^'""]+)['""]\)"
			};

			foreach (string pattern in patterns) {
				MatchCollection matches = Regex.Matches(body, pattern);
				foreach (Match match in matches) {
					if (match.Groups.Count > 1) {
						imports.Add(match.Groups[1].Value);
					}
				}
			}

			return imports.Distinct().ToList();
		}

		private static float CalculateConfidence(SemanticAnalysis analysis) {
			float score = 0.5f; // Base confidence

			// Increase confidence for clear indicators
			if (analysis.LikelyTools.Any()) score    += 0.2f;
			if (analysis.Categories.Count > 1) score += 0.1f;
			if (analysis.Features.Any()) score       += 0.1f;

			return Math.Min(score, 1.0f);
		}

		private static string InferComponentName(string body) {
			// Look for display name or common patterns
			Match displayNameMatch = Regex.Match(body, @"displayName\s*=\s*['""](\w+)['""]");
			if (displayNameMatch.Success)
				return displayNameMatch.Groups[1].Value;

			// Look for main element being rendered
			Match renderMatch = Regex.Match(body, @"return\s+.*?<(\w+)");
			if (renderMatch.Success)
				return renderMatch.Groups[1].Value;

			return "Unknown";
		}

		private static MatchResult? FindBestMatch(ExtractedModule module, List<ExtractedModule> candidates) {
			MatchResult? best = null;

			foreach (ExtractedModule candidate in candidates) {
				if (candidate.Type != module.Type) continue;

				float similarity = CalculateSimilarity(module.Body, candidate.Body);
				if (best == null || similarity > best.Similarity) {
					best = new MatchResult {
						Module     = candidate,
						Similarity = similarity
					};
				}
			}

			return best;
		}

		private static float CalculateSimilarity(string a, string b) {
			// Simple token-based similarity
			HashSet<string> tokensA = Regex.Matches(a, @"\b\w+\b").Select(m => m.Value).ToHashSet();
			HashSet<string> tokensB = Regex.Matches(b, @"\b\w+\b").Select(m => m.Value).ToHashSet();

			int intersection = tokensA.Intersect(tokensB).Count();
			int union        = tokensA.Union(tokensB).Count();

			return union > 0 ? (float)intersection / union : 0;
		}

		private static List<string> IdentifyChanges(string oldCode, string newCode) {
			List<string> changes = new List<string>();

			// Simple line diff
			string[] oldLines = oldCode.Split('\n');
			string[] newLines = newCode.Split('\n');

			if (oldLines.Length != newLines.Length)
				changes.Add($"Line count changed: {oldLines.Length} -> {newLines.Length}");

			// Check for new imports
			List<string>        oldImports   = ExtractImports(oldCode);
			List<string>        newImports   = ExtractImports(newCode);
			IEnumerable<string> addedImports = newImports.Except(oldImports);
			if (addedImports.Any())
				changes.Add($"New imports: {string.Join(", ", addedImports)}");

			return changes;
		}

		private static async Task<string> ExtractVersionAsync(string bundlePath) {
			string content      = await File.ReadAllTextAsync(bundlePath);
			Match  versionMatch = Regex.Match(content, @"Version:\s*([0-9.]+)");
			return versionMatch.Success ? versionMatch.Groups[1].Value : "unknown";
		}

		private static string GenerateIndex(DecompSession session) {
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"# Decompiled Code Index - Version {session.Version}");
			sb.AppendLine($"Generated: {session.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
			sb.AppendLine();

			sb.AppendLine("## Statistics");
			sb.AppendLine($"- Total Modules: {session.Modules.Count}");
			sb.AppendLine($"- React Components: {session.Modules.Count(m => m.Type == ModuleType.ReactComponent)}");
			sb.AppendLine($"- Classes: {session.Modules.Count(m => m.Type == ModuleType.Class)}");
			sb.AppendLine($"- CommonJS Modules: {session.Modules.Count(m => m.Type == ModuleType.CommonJS)}");

			if (session.DiffAnalysis != null) {
				sb.AppendLine();
				sb.AppendLine("## Version Changes");
				sb.AppendLine($"- New Modules: {session.DiffAnalysis.NewModules.Count}");
				sb.AppendLine($"- Changed Modules: {session.DiffAnalysis.ChangedModules.Count}");
				sb.AppendLine($"- Removed Modules: {session.DiffAnalysis.RemovedModules.Count}");
				sb.AppendLine($"- Unchanged Modules: {session.DiffAnalysis.UnchangedModules.Count}");
			}

			sb.AppendLine();
			sb.AppendLine("## Module Categories");
			IOrderedEnumerable<IGrouping<string, ExtractedModule>> byCategory = session.Modules
				.GroupBy(m => m.SemanticAnalysis?.Categories.FirstOrDefault() ?? "Uncategorized")
				.OrderBy(g => g.Key);

			foreach (IGrouping<string, ExtractedModule> category in byCategory) {
				sb.AppendLine($"### {category.Key} ({category.Count()})");
				foreach (ExtractedModule module in category.Take(10)) {
					string name = module.ProposedRenames?.Values
						.Where(r => r.Confidence > 0.7f)
						.Select(r => r.NewName)
						.FirstOrDefault() ?? module.OriginalName;

					sb.AppendLine($"- {name} (confidence: {module.ProposedRenames?.Values.Max(r => r.Confidence) ?? 0:P})");
				}
				if (category.Count() > 10)
					sb.AppendLine($"- ... and {category.Count() - 10} more");
			}

			return sb.ToString();
		}
	}

// Data models
	public class DecompSession {
		public string                SourcePath   { get; set; } = "";
		public string                Version      { get; set; } = "";
		public DateTime              Timestamp    { get; set; }
		public List<ExtractedModule> Modules      { get; set; } = new List<ExtractedModule>();
		public DiffAnalysis?         DiffAnalysis { get; set; }
	}

	public class ExtractedModule {
		public string                          Id               { get; set; } = "";
		public string                          OriginalName     { get; set; } = "";
		public string?                         ParameterName    { get; set; }
		public string?                         SuperClass       { get; set; }
		public string                          Body             { get; set; } = "";
		public string                          Hash             { get; set; } = "";
		public ModuleType                      Type             { get; set; }
		public List<string>                    Exports          { get; set; } = new List<string>();
		public List<string>                    Imports          { get; set; } = new List<string>();
		public SemanticAnalysis?               SemanticAnalysis { get; set; }
		public Dictionary<string, RenameInfo>? ProposedRenames  { get; set; }
	}

	public enum ModuleType {
		CommonJS,
		ReactComponent,
		Class,
		Function,
		Unknown
	}

	public class SemanticAnalysis {
		public List<string> Categories      { get; set; } = new List<string>();
		public List<string> Features        { get; set; } = new List<string>();
		public List<string> LikelyTools     { get; set; } = new List<string>();
		public float        ConfidenceScore { get; set; }
	}

	public class RenameInfo {
		public string NewName    { get; set; } = "";
		public float  Confidence { get; set; }
		public string Reason     { get; set; } = "";
	}

	public class DiffAnalysis {
		public List<string>                     NewModules       { get; set; } = new List<string>();
		public List<string>                     RemovedModules   { get; set; } = new List<string>();
		public List<string>                     UnchangedModules { get; set; } = new List<string>();
		public Dictionary<string, ModuleChange> ChangedModules   { get; set; } = new Dictionary<string, ModuleChange>();
	}

	public class ModuleChange {
		public string       PreviousId { get; set; } = "";
		public float        Similarity { get; set; }
		public List<string> Changes    { get; set; } = new List<string>();
	}

	public class MatchResult {
		public ExtractedModule Module     { get; set; } = null!;
		public float           Similarity { get; set; }
	}

	public class RenamingEngine {
		// Placeholder for more sophisticated renaming logic
	}
}