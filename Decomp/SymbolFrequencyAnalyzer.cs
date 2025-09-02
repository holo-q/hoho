using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hoho.Core;

namespace Hoho.Decomp {
	/// <summary>
	/// Analyzes symbol usage frequency to prioritize renaming decisions
	/// </summary>
	public class SymbolFrequencyAnalyzer {
		/// <summary>
		/// Analyze symbol frequency and generate prioritized mapping recommendations
		/// </summary>
		public static async Task<FrequencyAnalysis> AnalyzeSymbolFrequencyAsync(string bundlePath) {
			if (!File.Exists(bundlePath)) {
				throw new FileNotFoundException($"Bundle not found: {bundlePath}");
			}

			using var timer = Logger.TimeOperation("Symbol frequency analysis");

			string content = await File.ReadAllTextAsync(bundlePath);
			var analysis = new FrequencyAnalysis {
				FilePath          = bundlePath,
				AnalysisTimestamp = DateTime.UtcNow
			};

			// Extract all symbol occurrences with context
			var symbolOccurrences = ExtractSymbolOccurrences(content);

			// Calculate frequency metrics
			CalculateFrequencyMetrics(symbolOccurrences, analysis);

			// Analyze symbol roles and importance
			AnalyzeSymbolRoles(content, symbolOccurrences, analysis);

			// Generate prioritized recommendations
			GeneratePrioritizedRecommendations(analysis);

			Logger.Info($"Analyzed {analysis.TotalUniqueSymbols} symbols with {analysis.TotalOccurrences} total occurrences");

			return analysis;
		}

		/// <summary>
		/// Extract all symbol occurrences with their contexts
		/// </summary>
		private static Dictionary<string, List<SymbolOccurrence>> ExtractSymbolOccurrences(string content) {
			var occurrences       = new Dictionary<string, List<SymbolOccurrence>>();
			var identifierPattern = @"\b([A-Za-z_$][A-Za-z0-9_$]*)\b";
			var matches           = Regex.Matches(content, identifierPattern);

			foreach (Match match in matches) {
				string symbol = match.Value;

				// Skip JavaScript keywords
				if (IsJavaScriptKeyword(symbol)) continue;

				// Skip very short symbols that are likely parameters
				if (symbol.Length == 1) continue;

				if (!occurrences.ContainsKey(symbol)) {
					occurrences[symbol] = new List<SymbolOccurrence>();
				}

				// Extract context around the symbol
				int    contextStart  = Math.Max(0, match.Index - 50);
				int    contextLength = Math.Min(100, content.Length - contextStart);
				string context       = content.Substring(contextStart, contextLength);

				// Determine symbol role from context
				var role = DetermineSymbolRole(context, symbol, match.Index - contextStart);

				occurrences[symbol].Add(new SymbolOccurrence {
					Position      = match.Index,
					Context       = context,
					Role          = role,
					IsDeclaration = IsDeclaration(context, symbol, match.Index - contextStart),
					LineNumber    = content.Substring(0, match.Index).Count(c => c == '\n') + 1
				});
			}

			return occurrences;
		}

		/// <summary>
		/// Calculate frequency metrics and statistics
		/// </summary>
		private static void CalculateFrequencyMetrics(Dictionary<string, List<SymbolOccurrence>> occurrences, FrequencyAnalysis analysis) {
			analysis.TotalUniqueSymbols = occurrences.Count;
			analysis.TotalOccurrences   = occurrences.Values.Sum(list => list.Count);

			// Calculate frequency distribution
			var frequencyGroups = occurrences
				.GroupBy(kvp => GetFrequencyGroup(kvp.Value.Count))
				.ToDictionary(g => g.Key, g => g.Count());

			analysis.FrequencyDistribution = frequencyGroups;

			// Find most/least used symbols
			var sortedByFreq = occurrences
				.OrderByDescending(kvp => kvp.Value.Count)
				.ToList();

			analysis.MostUsedSymbols = sortedByFreq.Take(50)
				.ToDictionary(kvp => kvp.Key, kvp => new SymbolFrequencyInfo {
					Symbol               = kvp.Key,
					TotalOccurrences     = kvp.Value.Count,
					DeclarationCount     = kvp.Value.Count(occ => occ.IsDeclaration),
					Roles                = kvp.Value.Select(occ => occ.Role).Distinct().ToList(),
					AverageContextLength = kvp.Value.Average(occ => occ.Context.Length),
					SpreadScore          = CalculateSpreadScore(kvp.Value)
				});

			analysis.LeastUsedSymbols = sortedByFreq.TakeLast(20)
				.Where(kvp => kvp.Value.Count > 1) // Skip single-use symbols
				.ToDictionary(kvp => kvp.Key, kvp => new SymbolFrequencyInfo {
					Symbol               = kvp.Key,
					TotalOccurrences     = kvp.Value.Count,
					DeclarationCount     = kvp.Value.Count(occ => occ.IsDeclaration),
					Roles                = kvp.Value.Select(occ => occ.Role).Distinct().ToList(),
					AverageContextLength = kvp.Value.Average(occ => occ.Context.Length),
					SpreadScore          = CalculateSpreadScore(kvp.Value)
				});

			// Calculate symbol complexity scores
			foreach (var kvp in occurrences) {
				string symbol  = kvp.Key;
				var    occList = kvp.Value;

				double complexityScore = CalculateSymbolComplexity(symbol, occList);
				double impactScore     = CalculateImpactScore(symbol, occList);

				if (analysis.MostUsedSymbols.ContainsKey(symbol)) {
					analysis.MostUsedSymbols[symbol].ComplexityScore = complexityScore;
					analysis.MostUsedSymbols[symbol].ImpactScore     = impactScore;
				}
			}
		}

		/// <summary>
		/// Analyze symbol roles and categorize by importance
		/// </summary>
		private static void AnalyzeSymbolRoles(string content, Dictionary<string, List<SymbolOccurrence>> occurrences, FrequencyAnalysis analysis) {
			var roleCategories       = new Dictionary<SymbolRole, List<string>>();
			var importanceCategories = new Dictionary<SymbolImportance, List<string>>();

			foreach (var kvp in occurrences) {
				string symbol  = kvp.Key;
				var    occList = kvp.Value;

				// Determine primary role
				var primaryRole = occList.GroupBy(occ => occ.Role)
					.OrderByDescending(g => g.Count())
					.First().Key;

				if (!roleCategories.ContainsKey(primaryRole)) {
					roleCategories[primaryRole] = new List<string>();
				}
				roleCategories[primaryRole].Add(symbol);

				// Determine importance level
				var importance = DetermineSymbolImportance(symbol, occList, content);
				if (!importanceCategories.ContainsKey(importance)) {
					importanceCategories[importance] = new List<string>();
				}
				importanceCategories[importance].Add(symbol);
			}

			analysis.SymbolsByRole       = roleCategories;
			analysis.SymbolsByImportance = importanceCategories;

			// Generate role-based statistics
			analysis.RoleStatistics = roleCategories.ToDictionary(
				kvp => kvp.Key,
				kvp => new RoleStatistics {
					Role             = kvp.Key,
					SymbolCount      = kvp.Value.Count,
					TotalOccurrences = kvp.Value.Sum(symbol => occurrences[symbol].Count),
					AverageFrequency = kvp.Value.Average(symbol => occurrences[symbol].Count),
					TopSymbols       = kvp.Value.OrderByDescending(symbol => occurrences[symbol].Count).Take(5).ToList()
				});
		}

		/// <summary>
		/// Generate prioritized renaming recommendations
		/// </summary>
		private static void GeneratePrioritizedRecommendations(FrequencyAnalysis analysis) {
			var recommendations = new List<RenamingRecommendation>();

			// High-frequency, high-impact symbols (rename first)
			var highPrioritySymbols = analysis.MostUsedSymbols.Values
				.Where(info => info.ImpactScore > 0.7 && info.TotalOccurrences > 10)
				.OrderByDescending(info => info.ImpactScore * info.TotalOccurrences);

			foreach (var symbolInfo in highPrioritySymbols) {
				recommendations.Add(new RenamingRecommendation {
					Symbol              = symbolInfo.Symbol,
					Priority            = RenamingPriority.High,
					Frequency           = symbolInfo.TotalOccurrences,
					ImpactScore         = symbolInfo.ImpactScore,
					Rationale           = $"High-impact symbol with {symbolInfo.TotalOccurrences} occurrences. Roles: {string.Join(", ", symbolInfo.Roles)}",
					SuggestedNames      = GenerateNameSuggestions(symbolInfo.Symbol, symbolInfo.Roles),
					EstimatedComplexity = symbolInfo.ComplexityScore > 0.5 ? "High" : "Medium"
				});
			}

			// Medium-frequency symbols (rename after high priority)
			var mediumPrioritySymbols = analysis.MostUsedSymbols.Values
				.Where(info => info.ImpactScore > 0.4 && info.TotalOccurrences > 3 && info.TotalOccurrences <= 10)
				.OrderByDescending(info => info.ImpactScore);

			foreach (var symbolInfo in mediumPrioritySymbols) {
				recommendations.Add(new RenamingRecommendation {
					Symbol              = symbolInfo.Symbol,
					Priority            = RenamingPriority.Medium,
					Frequency           = symbolInfo.TotalOccurrences,
					ImpactScore         = symbolInfo.ImpactScore,
					Rationale           = $"Medium-impact symbol with {symbolInfo.TotalOccurrences} occurrences. Consider renaming for clarity.",
					SuggestedNames      = GenerateNameSuggestions(symbolInfo.Symbol, symbolInfo.Roles),
					EstimatedComplexity = symbolInfo.ComplexityScore > 0.3 ? "Medium" : "Low"
				});
			}

			// Low-frequency symbols (rename last, may be parameters)
			var lowPrioritySymbols = analysis.LeastUsedSymbols.Values
				.Where(info => info.ImpactScore < 0.3)
				.OrderBy(info => info.TotalOccurrences);

			foreach (var symbolInfo in lowPrioritySymbols.Take(10)) {
				recommendations.Add(new RenamingRecommendation {
					Symbol              = symbolInfo.Symbol,
					Priority            = RenamingPriority.Low,
					Frequency           = symbolInfo.TotalOccurrences,
					ImpactScore         = symbolInfo.ImpactScore,
					Rationale           = $"Low-frequency symbol ({symbolInfo.TotalOccurrences} occurrences). May be parameter or local variable.",
					SuggestedNames      = GenerateNameSuggestions(symbolInfo.Symbol, symbolInfo.Roles),
					EstimatedComplexity = "Low"
				});
			}

			analysis.PrioritizedRecommendations = recommendations.OrderByDescending(r => (int)r.Priority).ToList();
		}

		/// <summary>
		/// Generate contextual name suggestions based on symbol role
		/// </summary>
		private static List<string> GenerateNameSuggestions(string originalSymbol, List<SymbolRole> roles) {
			var suggestions = new List<string>();

			// Role-based suggestions
			foreach (var role in roles.Distinct()) {
				switch (role) {
					case SymbolRole.ModuleName:
						suggestions.AddRange(new[] { "Module", "Component", "Handler", "Service" });
						break;
					case SymbolRole.ClassName:
						suggestions.AddRange(new[] { "Class", "Component", "Widget", "Element" });
						break;
					case SymbolRole.FunctionName:
						suggestions.AddRange(new[] { "handler", "callback", "processor", "utility" });
						break;
					case SymbolRole.PropertyName:
						suggestions.AddRange(new[] { "prop", "attr", "field", "value" });
						break;
					case SymbolRole.VariableName:
						suggestions.AddRange(new[] { "data", "config", "options", "params" });
						break;
					case SymbolRole.ConstantName:
						suggestions.AddRange(new[] { "CONSTANT", "CONFIG", "DEFAULT", "TYPE" });
						break;
				}
			}

			// Pattern-based suggestions from original symbol
			if (originalSymbol.Length == 3 && char.IsUpper(originalSymbol[0])) {
				// Wu1, Ct1 style - likely module names
				suggestions.AddRange(new[] { "ReactModule", "ComponentUtil", "HandlerCore" });
			} else if (originalSymbol.Length == 2 && originalSymbol.All(char.IsUpper)) {
				// AB, XY style - likely constants
				suggestions.AddRange(new[] { "PROP", "TYPE", "MODE", "FLAG" });
			}

			return suggestions.Distinct().Take(5).ToList();
		}

		// Helper methods
		private static string GetFrequencyGroup(int count) {
			return count switch {
				1     => "Single use",
				<= 5  => "Low frequency (2-5)",
				<= 15 => "Medium frequency (6-15)",
				<= 50 => "High frequency (16-50)",
				_     => "Very high frequency (50+)"
			};
		}

		private static double CalculateSpreadScore(List<SymbolOccurrence> occurrences) {
			if (occurrences.Count <= 1) return 0.0;

			var positions       = occurrences.Select(occ => occ.Position).OrderBy(p => p).ToList();
			var spread          = positions.Last() - positions.First();
			var averageDistance = spread / (double)(positions.Count - 1);

			return Math.Min(1.0, averageDistance / 1000.0); // Normalize to 0-1
		}

		private static double CalculateSymbolComplexity(string symbol, List<SymbolOccurrence> occurrences) {
			double complexity = 0.0;

			// Length complexity (very short symbols are complex to understand)
			if (symbol.Length <= 2) complexity      += 0.4;
			else if (symbol.Length == 3) complexity += 0.2;

			// Role diversity complexity
			var roleCount = occurrences.Select(occ => occ.Role).Distinct().Count();
			complexity += Math.Min(0.3, roleCount * 0.1);

			// Context complexity (symbols used in many different contexts)
			var uniqueContexts = occurrences.Select(occ => occ.Context.Substring(0, Math.Min(20, occ.Context.Length))).Distinct().Count();
			complexity += Math.Min(0.3, uniqueContexts * 0.02);

			return Math.Min(1.0, complexity);
		}

		private static double CalculateImpactScore(string symbol, List<SymbolOccurrence> occurrences) {
			double impact = 0.0;

			// Frequency impact
			impact += Math.Min(0.4, occurrences.Count * 0.02);

			// Role impact
			var roles = occurrences.Select(occ => occ.Role).Distinct();
			foreach (var role in roles) {
				impact += role switch {
					SymbolRole.ModuleName   => 0.3,
					SymbolRole.ClassName    => 0.25,
					SymbolRole.FunctionName => 0.2,
					SymbolRole.ConstantName => 0.15,
					SymbolRole.PropertyName => 0.1,
					SymbolRole.VariableName => 0.05,
					_                       => 0.0
				};
			}

			// Declaration impact (symbols that are declared are more important)
			var declarationRatio = occurrences.Count(occ => occ.IsDeclaration) / (double)occurrences.Count;
			impact += declarationRatio * 0.2;

			return Math.Min(1.0, impact);
		}

		private static SymbolRole DetermineSymbolRole(string context, string symbol, int symbolPos) {
			// Look for patterns around the symbol
			string beforeSymbol = symbolPos > 0 ? context.Substring(0, symbolPos) : "";
			string afterSymbol  = symbolPos + symbol.Length < context.Length ? context.Substring(symbolPos + symbol.Length) : "";

			// Module patterns: var MODULE = U(...)
			if (Regex.IsMatch(beforeSymbol, @"var\s+$") && Regex.IsMatch(afterSymbol, @"^\s*=\s*U\s*\(")) {
				return SymbolRole.ModuleName;
			}

			// Class patterns: class CLASS extends/{ 
			if (Regex.IsMatch(beforeSymbol, @"class\s+$")) {
				return SymbolRole.ClassName;
			}

			// Function patterns: function FUNC(
			if (Regex.IsMatch(beforeSymbol, @"function\s+$") && Regex.IsMatch(afterSymbol, @"^\s*\(")) {
				return SymbolRole.FunctionName;
			}

			// Constant patterns: SYMBOL = "value" or {SYMBOL: 
			if (Regex.IsMatch(afterSymbol, @"^\s*[:=]\s*[""'\d]") || char.IsUpper(symbol[0])) {
				return SymbolRole.ConstantName;
			}

			// Property patterns: .SYMBOL or SYMBOL:
			if (Regex.IsMatch(beforeSymbol, @"\.$") || Regex.IsMatch(afterSymbol, @"^\s*:")) {
				return SymbolRole.PropertyName;
			}

			// Default to variable
			return SymbolRole.VariableName;
		}

		private static bool IsDeclaration(string context, string symbol, int symbolPos) {
			string beforeSymbol = symbolPos > 0 ? context.Substring(0, symbolPos) : "";

			// Variable declarations
			if (Regex.IsMatch(beforeSymbol, @"(var|let|const)\s+$")) return true;

			// Function declarations  
			if (Regex.IsMatch(beforeSymbol, @"function\s+$")) return true;

			// Class declarations
			if (Regex.IsMatch(beforeSymbol, @"class\s+$")) return true;

			// Property assignments (first occurrence)
			if (Regex.IsMatch(beforeSymbol, @"\w+\s*=\s*$")) return true;

			return false;
		}

		private static SymbolImportance DetermineSymbolImportance(string symbol, List<SymbolOccurrence> occurrences, string content) {
			var frequency      = occurrences.Count;
			var hasDeclaration = occurrences.Any(occ => occ.IsDeclaration);
			var roles          = occurrences.Select(occ => occ.Role).Distinct().ToList();

			// Critical: High frequency + important roles
			if (frequency > 20 || roles.Any(r => r == SymbolRole.ModuleName || r == SymbolRole.ClassName)) {
				return SymbolImportance.Critical;
			}

			// High: Medium frequency + function/constant roles
			if (frequency > 10 || roles.Any(r => r == SymbolRole.FunctionName || r == SymbolRole.ConstantName)) {
				return SymbolImportance.High;
			}

			// Medium: Some frequency + property roles
			if (frequency > 5 || roles.Contains(SymbolRole.PropertyName)) {
				return SymbolImportance.Medium;
			}

			// Low: Low frequency variables
			return SymbolImportance.Low;
		}

		private static bool IsJavaScriptKeyword(string identifier) {
			var keywords = new HashSet<string> {
				"var", "let", "const", "function", "return", "if", "else", "for", "while", "do", "break", "continue",
				"switch", "case", "default", "try", "catch", "finally", "throw", "new", "this", "super", "class",
				"extends", "import", "export", "from", "as", "async", "await", "yield", "typeof", "instanceof",
				"in", "of", "delete", "void", "null", "undefined", "true", "false", "NaN", "Infinity"
			};
			return keywords.Contains(identifier);
		}

		/// <summary>
		/// Generate comprehensive frequency analysis report
		/// </summary>
		public static string GenerateFrequencyReport(FrequencyAnalysis analysis) {
			var report = new StringBuilder();

			report.AppendLine("# Symbol Frequency Analysis Report");
			report.AppendLine($"**File:** {Path.GetFileName(analysis.FilePath)}");
			report.AppendLine($"**Analysis Date:** {analysis.AnalysisTimestamp:yyyy-MM-dd HH:mm:ss}");
			report.AppendLine();

			// Overall Statistics
			report.AppendLine("## 📊 Overall Statistics");
			report.AppendLine($"- **Total Unique Symbols:** {analysis.TotalUniqueSymbols:N0}");
			report.AppendLine($"- **Total Occurrences:** {analysis.TotalOccurrences:N0}");
			report.AppendLine($"- **Average Frequency:** {(double)analysis.TotalOccurrences / analysis.TotalUniqueSymbols:F1}");
			report.AppendLine();

			// Frequency Distribution
			report.AppendLine("## 📈 Frequency Distribution");
			foreach (var group in analysis.FrequencyDistribution.OrderBy(kvp => kvp.Key)) {
				report.AppendLine($"- **{group.Key}:** {group.Value:N0} symbols");
			}
			report.AppendLine();

			// High Priority Renaming Recommendations
			report.AppendLine("## 🎯 Priority Renaming Recommendations");
			var highPriority = analysis.PrioritizedRecommendations.Where(r => r.Priority == RenamingPriority.High).Take(15);
			foreach (var rec in highPriority) {
				report.AppendLine($"### `{rec.Symbol}` - **{rec.Priority} Priority**");
				report.AppendLine($"- **Frequency:** {rec.Frequency} occurrences");
				report.AppendLine($"- **Impact Score:** {rec.ImpactScore:P1}");
				report.AppendLine($"- **Complexity:** {rec.EstimatedComplexity}");
				report.AppendLine($"- **Rationale:** {rec.Rationale}");
				report.AppendLine($"- **Suggested Names:** {string.Join(", ", rec.SuggestedNames)}");
				report.AppendLine();
			}

			// Symbol Role Analysis
			report.AppendLine("## 🏷️ Symbol Role Analysis");
			foreach (var roleStats in analysis.RoleStatistics.Values.OrderByDescending(rs => rs.TotalOccurrences)) {
				report.AppendLine($"### {roleStats.Role}");
				report.AppendLine($"- **Symbol Count:** {roleStats.SymbolCount}");
				report.AppendLine($"- **Total Occurrences:** {roleStats.TotalOccurrences}");
				report.AppendLine($"- **Average Frequency:** {roleStats.AverageFrequency:F1}");
				report.AppendLine($"- **Top Symbols:** {string.Join(", ", roleStats.TopSymbols.Take(3))}");
				report.AppendLine();
			}

			// Most Used Symbols
			report.AppendLine("## 🔥 Most Used Symbols");
			foreach (var symbol in analysis.MostUsedSymbols.Values.Take(20)) {
				report.AppendLine($"- **`{symbol.Symbol}`**: {symbol.TotalOccurrences} occurrences, Impact: {symbol.ImpactScore:P1}, Roles: {string.Join(", ", symbol.Roles)}");
			}
			report.AppendLine();

			// Recommendations Summary
			report.AppendLine("## 💡 Deobfuscation Strategy");
			var highPriorityCount   = analysis.PrioritizedRecommendations.Count(r => r.Priority == RenamingPriority.High);
			var mediumPriorityCount = analysis.PrioritizedRecommendations.Count(r => r.Priority == RenamingPriority.Medium);
			var lowPriorityCount    = analysis.PrioritizedRecommendations.Count(r => r.Priority == RenamingPriority.Low);

			report.AppendLine($"1. **Phase 1 - High Priority:** Rename {highPriorityCount} critical symbols first");
			report.AppendLine($"2. **Phase 2 - Medium Priority:** Rename {mediumPriorityCount} important symbols");
			report.AppendLine($"3. **Phase 3 - Low Priority:** Rename {lowPriorityCount} remaining symbols");
			report.AppendLine();
			report.AppendLine("**Recommended Approach:**");
			report.AppendLine("- Start with module and class names (highest impact)");
			report.AppendLine("- Focus on frequently used function names");
			report.AppendLine("- Save single-letter variables and parameters for last");
			report.AppendLine("- Use LSP-based renaming for complex symbols");

			return report.ToString();
		}
	}

	// Data structures for frequency analysis
	public class FrequencyAnalysis {
		public string   FilePath           { get; set; } = "";
		public DateTime AnalysisTimestamp  { get; set; }
		public int      TotalUniqueSymbols { get; set; }
		public int      TotalOccurrences   { get; set; }

		public Dictionary<string, int>                 FrequencyDistribution { get; set; } = new();
		public Dictionary<string, SymbolFrequencyInfo> MostUsedSymbols       { get; set; } = new();
		public Dictionary<string, SymbolFrequencyInfo> LeastUsedSymbols      { get; set; } = new();

		public Dictionary<SymbolRole, List<string>>       SymbolsByRole       { get; set; } = new();
		public Dictionary<SymbolImportance, List<string>> SymbolsByImportance { get; set; } = new();
		public Dictionary<SymbolRole, RoleStatistics>     RoleStatistics      { get; set; } = new();

		public List<RenamingRecommendation> PrioritizedRecommendations { get; set; } = new();
	}

	public class SymbolFrequencyInfo {
		public string           Symbol               { get; set; } = "";
		public int              TotalOccurrences     { get; set; }
		public int              DeclarationCount     { get; set; }
		public List<SymbolRole> Roles                { get; set; } = new();
		public double           AverageContextLength { get; set; }
		public double           SpreadScore          { get; set; }
		public double           ComplexityScore      { get; set; }
		public double           ImpactScore          { get; set; }
	}

	public class SymbolOccurrence {
		public int        Position      { get; set; }
		public string     Context       { get; set; } = "";
		public SymbolRole Role          { get; set; }
		public bool       IsDeclaration { get; set; }
		public int        LineNumber    { get; set; }
	}

	public class RoleStatistics {
		public SymbolRole   Role             { get; set; }
		public int          SymbolCount      { get; set; }
		public int          TotalOccurrences { get; set; }
		public double       AverageFrequency { get; set; }
		public List<string> TopSymbols       { get; set; } = new();
	}

	public class RenamingRecommendation {
		public string           Symbol              { get; set; } = "";
		public RenamingPriority Priority            { get; set; }
		public int              Frequency           { get; set; }
		public double           ImpactScore         { get; set; }
		public string           Rationale           { get; set; } = "";
		public List<string>     SuggestedNames      { get; set; } = new();
		public string           EstimatedComplexity { get; set; } = "";
	}

	public enum SymbolRole {
		ModuleName,
		ClassName,
		FunctionName,
		PropertyName,
		VariableName,
		ConstantName,
		ParameterName
	}

	public enum SymbolImportance {
		Critical,
		High,
		Medium,
		Low
	}

	public enum RenamingPriority {
		High   = 3,
		Medium = 2,
		Low    = 1
	}
}