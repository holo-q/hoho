using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hoho.Core;

namespace Hoho.Decomp {
	/// <summary>
	/// Analyzes webpack bundles to identify module boundaries and structure.
	/// Black hat deobfuscation for cleanroom implementation.
	/// </summary>
	public static class WebpackBundleAnalyzer {
		/// <summary>
		/// Analyze a webpack bundle and extract module structure.
		/// </summary>
		public static async Task<BundleAnalysis> AnalyzeBundleAsync(string bundlePath) {
			using IDisposable timer = Logger.TimeOperation("Analyze webpack bundle");

			string content = await File.ReadAllTextAsync(bundlePath);
			BundleAnalysis analysis = new BundleAnalysis {
				FilePath   = bundlePath,
				FileSize   = new FileInfo(bundlePath).Length,
				TotalLines = File.ReadLines(bundlePath).Count()
			};

			// Detect bundle type
			analysis.BundleType = DetectBundleType(content);

			// Extract modules
			analysis.Modules = ExtractModules(content);

			// Find React components
			analysis.ReactComponents = FindReactComponents(content);

			// Find tool implementations
			analysis.ToolImplementations = FindToolImplementations(content);

			// Find WASM integration points
			analysis.WasmIntegration = FindWasmIntegration(content);

			// Map minified to likely original names
			analysis.SymbolMap = BuildSymbolMap(content);

			// Calculate enhanced statistics
			CalculateEnhancedStatistics(content, analysis);

			return analysis;
		}

		private static string DetectBundleType(string content) {
			if (content.Contains("__webpack_require__"))
				return "Webpack 4/5";
			if (content.Contains("webpackJsonp"))
				return "Webpack 3";
			if (content.Contains("System.register"))
				return "SystemJS";
			if (content.Contains("define(["))
				return "AMD";
			if (content.Contains("import{") && content.Contains("from\""))
				return "ESM Bundle";
			return "Unknown";
		}

		private static List<ModuleInfo> ExtractModules(string content) {
			List<ModuleInfo> modules = new List<ModuleInfo>();

			// Pattern 1: CommonJS style - var X = U((module) => { ... })
			string          cjsPattern = @"var\s+(\w+)\s*=\s*U\(\((\w+)\)\s*=>\s*\{";
			MatchCollection cjsMatches = Regex.Matches(content, cjsPattern);

			foreach (Match match in cjsMatches) {
				modules.Add(new ModuleInfo {
					Id         = match.Groups[1].Value,
					Type       = "CommonJS",
					ExportName = match.Groups[2].Value
				});
			}

			// Pattern 2: React components - function ComponentName(props)
			string          componentPattern = @"function\s+([A-Z]\w+)\s*\(\s*(?:A|props|B)\s*\)";
			MatchCollection componentMatches = Regex.Matches(content, componentPattern);

			foreach (Match match in componentMatches) {
				string name = match.Groups[1].Value;
				if (!modules.Any(m => m.Id == name)) {
					modules.Add(new ModuleInfo {
						Id         = name,
						Type       = "React Component",
						ExportName = name
					});
				}
			}

			// Pattern 3: Class definitions - class X { or class X extends
			string          classPattern = @"class\s+(\w+)\s*(?:\{|extends)";
			MatchCollection classMatches = Regex.Matches(content, classPattern);

			foreach (Match match in classMatches) {
				modules.Add(new ModuleInfo {
					Id         = match.Groups[1].Value,
					Type       = "Class",
					ExportName = match.Groups[1].Value
				});
			}

			return modules;
		}

		private static List<string> FindReactComponents(string content) {
			HashSet<string> components = new HashSet<string>();

			// Look for JSX patterns
			string          jsxPattern = @"createElement\(([A-Z]\w+)";
			MatchCollection matches    = Regex.Matches(content, jsxPattern);

			foreach (Match match in matches) {
				components.Add(match.Groups[1].Value);
			}

			// Look for useState, useEffect patterns
			string hookPattern = @"(useState|useEffect|useCallback|useMemo|useRef)\(";
			if (Regex.IsMatch(content, hookPattern)) {
				Logger.Info("Found React hooks usage");
			}

			return components.ToList();
		}

		private static Dictionary<string, List<string>> FindToolImplementations(string content) {
			Dictionary<string, List<string>> tools = new Dictionary<string, List<string>>();

			// Known tool names from sdk-tools.d.ts
			string[] toolNames = new[] {
				"FileRead", "FileWrite", "FileEdit", "FileMultiEdit",
				"Bash", "BashOutput", "KillShell",
				"Grep", "Glob",
				"WebSearch", "WebFetch",
				"TodoWrite", "Agent",
				"NotebookEdit", "ExitPlanMode"
			};

			foreach (string tool in toolNames) {
				string          pattern = $@"{tool}[\""\']?\s*[:=]";
				MatchCollection matches = Regex.Matches(content, pattern);

				if (matches.Count > 0) {
					List<string> contexts = new List<string>();
					foreach (Match match in matches.Take(3)) {
						int start  = Math.Max(0, match.Index - 50);
						int length = Math.Min(200, content.Length - start);
						contexts.Add(content.Substring(start, length));
					}
					tools[tool] = contexts;
				}
			}

			return tools;
		}

		private static WasmIntegration FindWasmIntegration(string content) {
			WasmIntegration wasm = new WasmIntegration();

			// Emscripten patterns
			if (content.Contains("Module[\"_malloc\"]") || content.Contains("Module._malloc")) {
				wasm.UsesEmscripten = true;
				wasm.EmscriptenFunctions.Add("malloc");
			}

			// WebAssembly instantiation
			string wasmInstantiate = @"WebAssembly\.instantiate";
			if (Regex.IsMatch(content, wasmInstantiate)) {
				wasm.HasDirectWasmCalls = true;
			}

			// Tree-sitter WASM functions
			string          treeSitterPattern = @"_tree_sitter_\w+";
			MatchCollection tsMatches         = Regex.Matches(content, treeSitterPattern);
			foreach (Match match in tsMatches) {
				wasm.TreeSitterFunctions.Add(match.Value);
			}

			// Yoga layout WASM
			if (content.Contains("YGNodeCalculateLayout") || content.Contains("yoga")) {
				wasm.UsesYogaLayout = true;
			}

			return wasm;
		}

		/// <summary>
		/// Calculate enhanced bundle statistics for comprehensive analysis
		/// </summary>
		private static void CalculateEnhancedStatistics(string content, BundleAnalysis analysis) {
			// Size statistics
			analysis.SizeStats.TotalCharacters        = content.Length;
			analysis.SizeStats.TotalBytes             = Encoding.UTF8.GetByteCount(content);
			analysis.SizeStats.WhitespaceCharacters   = content.Count(char.IsWhiteSpace);
			analysis.SizeStats.AlphanumericCharacters = content.Count(char.IsLetterOrDigit);
			analysis.SizeStats.SymbolCharacters       = content.Length - analysis.SizeStats.WhitespaceCharacters - analysis.SizeStats.AlphanumericCharacters;
			analysis.SizeStats.EstimatedGzipSize      = (long)(analysis.SizeStats.TotalBytes * 0.3); // ~30% compression typical for minified JS
			analysis.SizeStats.MinificationRatio      = CalculateMinificationRatio(content);

			// Symbol statistics  
			var identifiers = ExtractAllIdentifiers(content);
			analysis.SymbolStats.TotalUniqueSymbols     = identifiers.Count;
			analysis.SymbolStats.TotalSymbolOccurrences = identifiers.Values.Sum();
			analysis.SymbolStats.MostUsedSymbols        = identifiers.OrderByDescending(kvp => kvp.Value).Take(20).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			// Analyze symbol patterns
			var obfuscatedPattern = @"^[A-Z][a-z0-9]{0,2}[0-9]*$"; // Wu1, Ct1, pA1, etc.
			analysis.SymbolStats.ShortSymbols      = identifiers.Keys.Count(id => id.Length <= 3);
			analysis.SymbolStats.ObfuscatedSymbols = identifiers.Keys.Count(id => Regex.IsMatch(id, obfuscatedPattern));
			analysis.SymbolStats.ReadableSymbols   = identifiers.Count - analysis.SymbolStats.ObfuscatedSymbols - analysis.SymbolStats.ShortSymbols;
			analysis.SymbolStats.ObfuscationRatio  = identifiers.Count > 0 ? (double)analysis.SymbolStats.ObfuscatedSymbols / identifiers.Count : 0.0;

			// Complexity statistics
			analysis.ComplexityStats.MaxNestingLevel       = CalculateMaxNestingLevel(content);
			analysis.ComplexityStats.AverageNestingLevel   = CalculateAverageNestingLevel(content);
			analysis.ComplexityStats.ConditionalStatements = Regex.Matches(content, @"\b(if|switch)\s*\(").Count;
			analysis.ComplexityStats.LoopStatements        = Regex.Matches(content, @"\b(for|while|do)\s*[\s\(]").Count;
			analysis.ComplexityStats.FunctionCount         = Regex.Matches(content, @"\bfunction\s+\w+\s*\(").Count;
			analysis.ComplexityStats.ClassCount            = Regex.Matches(content, @"\bclass\s+\w+").Count;
			analysis.ComplexityStats.TotalStatements       = analysis.ComplexityStats.ConditionalStatements + analysis.ComplexityStats.LoopStatements;
		}

		/// <summary>
		/// Extract all identifiers and their usage counts
		/// </summary>
		private static Dictionary<string, int> ExtractAllIdentifiers(string content) {
			var identifierPattern = @"\b[A-Za-z_$][A-Za-z0-9_$]*\b";
			var identifiers = Regex.Matches(content, identifierPattern)
				.Cast<Match>()
				.Select(m => m.Value)
				.Where(id => !IsJavaScriptKeyword(id))
				.GroupBy(id => id)
				.ToDictionary(g => g.Key, g => g.Count());
			return identifiers;
		}

		/// <summary>
		/// Calculate minification ratio heuristic
		/// </summary>
		private static double CalculateMinificationRatio(string content) {
			var avgLineLength   = content.Length / (double)content.Split('\n').Length;
			var whitespaceRatio = content.Count(char.IsWhiteSpace) / (double)content.Length;
			return whitespaceRatio < 0.1 && avgLineLength > 100 ? 0.85 : 0.3;
		}

		/// <summary>
		/// Calculate maximum nesting level in the code
		/// </summary>
		private static int CalculateMaxNestingLevel(string content) {
			int maxLevel     = 0;
			int currentLevel = 0;

			foreach (char c in content) {
				if (c == '{') {
					currentLevel++;
					maxLevel = Math.Max(maxLevel, currentLevel);
				} else if (c == '}') {
					currentLevel--;
				}
			}

			return maxLevel;
		}

		/// <summary>
		/// Calculate average nesting level in the code
		/// </summary>
		private static int CalculateAverageNestingLevel(string content) {
			var levels       = new List<int>();
			int currentLevel = 0;

			foreach (char c in content) {
				if (c == '{') {
					currentLevel++;
				} else if (c == '}') {
					levels.Add(currentLevel);
					currentLevel--;
				}
			}

			return levels.Any() ? (int)levels.Average() : 0;
		}

		/// <summary>
		/// Check if identifier is a JavaScript keyword
		/// </summary>
		private static bool IsJavaScriptKeyword(string identifier) {
			var keywords = new HashSet<string> {
				"var", "let", "const", "function", "return", "if", "else", "for", "while", "do", "break", "continue",
				"switch", "case", "default", "try", "catch", "finally", "throw", "new", "this", "super", "class",
				"extends", "import", "export", "from", "as", "async", "await", "yield", "typeof", "instanceof",
				"in", "of", "delete", "void", "null", "undefined", "true", "false", "NaN", "Infinity"
			};
			return keywords.Contains(identifier);
		}

		private static Dictionary<string, string> BuildSymbolMap(string content) {
			Dictionary<string, string> map = new Dictionary<string, string>();

			// Common minification patterns to likely originals
			Dictionary<string, string> patterns = new Dictionary<string, string> {
				[@"\bA\b"] = "props/args",
				[@"\bB\b"] = "state/buffer",
				[@"\bQ\b"] = "query/request",
				[@"\bZ\b"] = "result/response",
				[@"\bG\b"] = "global/generator",
				[@"\bI\b"] = "index/iterator",
				[@"\bW\b"] = "window/wrapper",
				[@"\bJ\b"] = "json/join",
				[@"\bX\b"] = "xml/extra",
				[@"\bY\b"] = "yield/yes"
			};

			// Function name patterns that suggest purpose
			Dictionary<string, string> functionPatterns = new Dictionary<string, string> {
				[@"[a-z]+0"] = "init/constructor",
				[@"[a-z]+1"] = "handler/callback",
				[@"[a-z]+2"] = "helper/utility",
				[@"[a-z]+Q"] = "query/request",
				[@"[a-z]+B"] = "buffer/build",
				[@"[a-z]+A"] = "async/array"
			};

			// Extract and map symbols
			string                  symbolPattern = @"\b([a-z]{2,3}[0-9A-Z]{1,2})\b";
			MatchCollection         matches       = Regex.Matches(content, symbolPattern);
			Dictionary<string, int> symbolCounts  = new Dictionary<string, int>();

			foreach (Match match in matches) {
				string symbol = match.Groups[1].Value;
				if (!symbolCounts.ContainsKey(symbol))
					symbolCounts[symbol] = 0;
				symbolCounts[symbol]++;
			}

			// Map frequently used symbols to likely purposes
			IEnumerable<KeyValuePair<string, int>> sorted = symbolCounts.OrderByDescending(x => x.Value).Take(100);
			foreach (KeyValuePair<string, int> kvp in sorted) {
				string symbol = kvp.Key;
				int    count  = kvp.Value;

				// High frequency suggests core functionality
				if (count > 1000)
					map[symbol] = "core_util";
				else if (count > 500)
					map[symbol] = "common_helper";
				else if (count > 100)
					map[symbol] = "module_func";

				// Check against patterns
				foreach (KeyValuePair<string, string> pattern in functionPatterns) {
					if (Regex.IsMatch(symbol, pattern.Key)) {
						map[symbol] = pattern.Value;
						break;
					}
				}
			}

			return map;
		}

		public class BundleAnalysis {
			public string                           FilePath            { get; set; } = "";
			public long                             FileSize            { get; set; }
			public int                              TotalLines          { get; set; }
			public string                           BundleType          { get; set; } = "";
			public List<ModuleInfo>                 Modules             { get; set; } = new List<ModuleInfo>();
			public List<string>                     ReactComponents     { get; set; } = new List<string>();
			public Dictionary<string, List<string>> ToolImplementations { get; set; } = new Dictionary<string, List<string>>();
			public WasmIntegration                  WasmIntegration     { get; set; } = new WasmIntegration();
			public Dictionary<string, string>       SymbolMap           { get; set; } = new Dictionary<string, string>();

			// Enhanced analysis statistics
			public SizeStatistics       SizeStats       { get; set; } = new SizeStatistics();
			public SymbolStatistics     SymbolStats     { get; set; } = new SymbolStatistics();
			public ComplexityStatistics ComplexityStats { get; set; } = new ComplexityStatistics();
			public DateTime             AnalysisTime    { get; set; } = DateTime.UtcNow;

			public string GenerateReport() {
				StringBuilder sb = new StringBuilder();

				sb.AppendLine("# WEBPACK BUNDLE ANALYSIS REPORT");
				sb.AppendLine("=".PadRight(80, '='));
				sb.AppendLine($"**File:** {Path.GetFileName(FilePath)}");
				sb.AppendLine($"**Analysis Date:** {AnalysisTime:yyyy-MM-dd HH:mm:ss}");
				sb.AppendLine($"**Bundle Type:** {BundleType}");
				sb.AppendLine();

				// Enhanced Size Statistics
				sb.AppendLine("## 📊 SIZE STATISTICS");
				sb.AppendLine($"- **File Size:** {FormatBytes(FileSize)}");
				sb.AppendLine($"- **Total Lines:** {TotalLines:N0}");
				sb.AppendLine($"- **Total Characters:** {SizeStats.TotalCharacters:N0}");
				sb.AppendLine($"- **Total Bytes:** {FormatBytes(SizeStats.TotalBytes)}");
				sb.AppendLine($"- **Whitespace:** {SizeStats.WhitespaceCharacters:N0} ({(double)SizeStats.WhitespaceCharacters / SizeStats.TotalCharacters * 100:F1}%)");
				sb.AppendLine($"- **Alphanumeric:** {SizeStats.AlphanumericCharacters:N0} ({(double)SizeStats.AlphanumericCharacters / SizeStats.TotalCharacters * 100:F1}%)");
				sb.AppendLine($"- **Symbols:** {SizeStats.SymbolCharacters:N0} ({(double)SizeStats.SymbolCharacters / SizeStats.TotalCharacters * 100:F1}%)");
				sb.AppendLine($"- **Estimated Gzip Size:** {FormatBytes(SizeStats.EstimatedGzipSize)}");
				sb.AppendLine($"- **Minification Ratio:** {SizeStats.MinificationRatio:P1}");
				sb.AppendLine();

				// Enhanced Symbol Statistics
				sb.AppendLine("## 🔤 SYMBOL STATISTICS");
				sb.AppendLine($"- **Total Unique Symbols:** {SymbolStats.TotalUniqueSymbols:N0}");
				sb.AppendLine($"- **Total Symbol Occurrences:** {SymbolStats.TotalSymbolOccurrences:N0}");
				sb.AppendLine($"- **Short Symbols (≤3 chars):** {SymbolStats.ShortSymbols:N0}");
				sb.AppendLine($"- **Obfuscated Symbols:** {SymbolStats.ObfuscatedSymbols:N0}");
				sb.AppendLine($"- **Readable Symbols:** {SymbolStats.ReadableSymbols:N0}");
				sb.AppendLine($"- **Obfuscation Ratio:** {SymbolStats.ObfuscationRatio:P1}");
				sb.AppendLine();

				// Most Used Symbols
				sb.AppendLine("### Most Used Symbols");
				foreach (var symbol in SymbolStats.MostUsedSymbols.Take(15)) {
					sb.AppendLine($"- `{symbol.Key}`: {symbol.Value:N0} occurrences");
				}
				sb.AppendLine();

				// Complexity Statistics
				sb.AppendLine("## 🧩 COMPLEXITY STATISTICS");
				sb.AppendLine($"- **Max Nesting Level:** {ComplexityStats.MaxNestingLevel}");
				sb.AppendLine($"- **Average Nesting Level:** {ComplexityStats.AverageNestingLevel}");
				sb.AppendLine($"- **Function Count:** {ComplexityStats.FunctionCount:N0}");
				sb.AppendLine($"- **Class Count:** {ComplexityStats.ClassCount:N0}");
				sb.AppendLine($"- **Conditional Statements:** {ComplexityStats.ConditionalStatements:N0}");
				sb.AppendLine($"- **Loop Statements:** {ComplexityStats.LoopStatements:N0}");
				sb.AppendLine($"- **Total Control Flow:** {ComplexityStats.TotalStatements:N0}");
				sb.AppendLine();

				sb.AppendLine("MODULE STRUCTURE");
				sb.AppendLine("-".PadRight(80, '-'));
				IEnumerable<IGrouping<string, ModuleInfo>> moduleTypes = Modules.GroupBy(m => m.Type);
				foreach (IGrouping<string, ModuleInfo> type in moduleTypes) {
					sb.AppendLine($"{type.Key}: {type.Count()}");
					foreach (ModuleInfo module in type.Take(5)) {
						sb.AppendLine($"  - {module.Id}");
					}
					if (type.Count() > 5)
						sb.AppendLine($"  ... and {type.Count() - 5} more");
				}
				sb.AppendLine();

				sb.AppendLine("REACT COMPONENTS DETECTED");
				sb.AppendLine("-".PadRight(80, '-'));
				foreach (string comp in ReactComponents.Take(10)) {
					sb.AppendLine($"  - {comp}");
				}
				if (ReactComponents.Count > 10)
					sb.AppendLine($"  ... and {ReactComponents.Count - 10} more");
				sb.AppendLine();

				sb.AppendLine("TOOL IMPLEMENTATIONS FOUND");
				sb.AppendLine("-".PadRight(80, '-'));
				foreach (KeyValuePair<string, List<string>> tool in ToolImplementations) {
					sb.AppendLine($"{tool.Key}: {tool.Value.Count} references");
				}
				sb.AppendLine();

				sb.AppendLine("WASM INTEGRATION");
				sb.AppendLine("-".PadRight(80, '-'));
				sb.AppendLine($"Uses Emscripten: {WasmIntegration.UsesEmscripten}");
				sb.AppendLine($"Has Direct WASM Calls: {WasmIntegration.HasDirectWasmCalls}");
				sb.AppendLine($"Uses Yoga Layout: {WasmIntegration.UsesYogaLayout}");
				if (WasmIntegration.TreeSitterFunctions.Any()) {
					sb.AppendLine($"Tree-sitter Functions: {WasmIntegration.TreeSitterFunctions.Count}");
					foreach (string func in WasmIntegration.TreeSitterFunctions.Take(5)) {
						sb.AppendLine($"  - {func}");
					}
				}
				sb.AppendLine();

				sb.AppendLine("DEOBFUSCATION STRATEGY");
				sb.AppendLine("-".PadRight(80, '-'));
				sb.AppendLine("1. Extract module boundaries using CommonJS patterns");
				sb.AppendLine("2. Identify React component hierarchy");
				sb.AppendLine("3. Map tool implementations to their minified symbols");
				sb.AppendLine("4. Separate WASM integration layer");
				sb.AppendLine("5. Build symbol rename map based on usage patterns");
				sb.AppendLine($"6. Identified {SymbolMap.Count} symbols for remapping");

				return sb.ToString();
			}

			/// <summary>
			/// Format bytes into human readable format
			/// </summary>
			private static string FormatBytes(long bytes) {
				string[] sizes = { "B", "KB", "MB", "GB" };
				double   len   = bytes;
				int      order = 0;
				while (len >= 1024 && order < sizes.Length - 1) {
					order++;
					len /= 1024;
				}
				return $"{len:0.##} {sizes[order]}";
			}
		}

		public class ModuleInfo {
			public string Id         { get; set; } = "";
			public string Type       { get; set; } = "";
			public string ExportName { get; set; } = "";
		}

		public class WasmIntegration {
			public bool         UsesEmscripten      { get; set; }
			public bool         HasDirectWasmCalls  { get; set; }
			public bool         UsesYogaLayout      { get; set; }
			public List<string> EmscriptenFunctions { get; set; } = new List<string>();
			public List<string> TreeSitterFunctions { get; set; } = new List<string>();
		}

		/// <summary>
		/// Enhanced size statistics for comprehensive bundle analysis
		/// </summary>
		public class SizeStatistics {
			public int    TotalCharacters        { get; set; }
			public long   TotalBytes             { get; set; }
			public int    WhitespaceCharacters   { get; set; }
			public int    AlphanumericCharacters { get; set; }
			public int    SymbolCharacters       { get; set; }
			public long   EstimatedGzipSize      { get; set; }
			public double MinificationRatio      { get; set; }
		}

		/// <summary>
		/// Enhanced symbol statistics for obfuscation analysis
		/// </summary>
		public class SymbolStatistics {
			public int                     TotalUniqueSymbols     { get; set; }
			public int                     TotalSymbolOccurrences { get; set; }
			public int                     ShortSymbols           { get; set; }
			public int                     ObfuscatedSymbols      { get; set; }
			public int                     ReadableSymbols        { get; set; }
			public double                  ObfuscationRatio       { get; set; }
			public Dictionary<string, int> MostUsedSymbols        { get; set; } = new Dictionary<string, int>();
		}

		/// <summary>
		/// Code complexity statistics for deobfuscation planning
		/// </summary>
		public class ComplexityStatistics {
			public int MaxNestingLevel       { get; set; }
			public int AverageNestingLevel   { get; set; }
			public int ConditionalStatements { get; set; }
			public int LoopStatements        { get; set; }
			public int FunctionCount         { get; set; }
			public int ClassCount            { get; set; }
			public int TotalStatements       { get; set; }
		}
	}
}