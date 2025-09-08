using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hoho.Core;

namespace Hoho.Decomp {
	/// <summary>
	/// Analyzes symbol consistency across multiple bundle versions for progressive learning
	/// </summary>
	public class CrossVersionConsistencyChecker {
		private const string CONSISTENCY_DB = "decomp/consistency.json";

		/// <summary>
		/// Analyze consistency between two bundle versions
		/// </summary>
		public static async Task<ConsistencyAnalysis> AnalyzeConsistencyAsync(string version1Path, string version2Path, string version1Name, string version2Name) {
			using var timer = Logger.TimeOperation($"Cross-version consistency analysis: {version1Name} vs {version2Name}");

			if (!File.Exists(version1Path) || !File.Exists(version2Path)) {
				throw new FileNotFoundException("Both bundle versions must exist for consistency analysis");
			}

			string content1 = await File.ReadAllTextAsync(version1Path);
			string content2 = await File.ReadAllTextAsync(version2Path);

			var analysis = new ConsistencyAnalysis {
				Version1Name      = version1Name,
				Version2Name      = version2Name,
				Version1Path      = version1Path,
				Version2Path      = version2Path,
				AnalysisTimestamp = DateTime.UtcNow
			};

			// Extract symbols from both versions
			var symbols1 = ExtractSymbolMetadata(content1);
			var symbols2 = ExtractSymbolMetadata(content2);

			// Analyze consistency patterns
			AnalyzeSymbolConsistency(symbols1, symbols2, analysis);
			AnalyzeStructuralConsistency(content1, content2, analysis);
			AnalyzeMappingConsistency(analysis);

			// Generate consistency recommendations
			GenerateConsistencyRecommendations(analysis);

			// Update consistency database
			await UpdateConsistencyDatabase(analysis);

			Logger.Info($"Found {analysis.ConsistentSymbols.Count} consistent symbols, {analysis.InconsistentSymbols.Count} inconsistent");

			return analysis;
		}

		/// <summary>
		/// Extract symbol metadata for consistency analysis
		/// </summary>
		private static Dictionary<string, SymbolMetadata> ExtractSymbolMetadata(string content) {
			var symbols           = new Dictionary<string, SymbolMetadata>();
			var identifierPattern = @"\b([A-Za-z_$][A-Za-z0-9_$]*)\b";
			var matches           = Regex.Matches(content, identifierPattern);

			foreach (Match match in matches) {
				string symbol = match.Value;

				// Skip JavaScript keywords and very short symbols
				if (IsJavaScriptKeyword(symbol) || symbol.Length == 1) continue;

				if (!symbols.ContainsKey(symbol)) {
					symbols[symbol] = new SymbolMetadata {
						Symbol        = symbol,
						FirstPosition = match.Index,
						Occurrences   = new List<int>(),
						Contexts      = new List<string>()
					};
				}

				var metadata = symbols[symbol];
				metadata.Occurrences.Add(match.Index);
				metadata.TotalCount++;

				// Extract context around symbol
				int    contextStart  = Math.Max(0, match.Index - 30);
				int    contextLength = Math.Min(60, content.Length - contextStart);
				string context       = content.Substring(contextStart, contextLength);

				// Only store unique contexts to avoid duplicates
				if (!metadata.Contexts.Contains(context)) {
					metadata.Contexts.Add(context);
				}

				// Determine symbol characteristics
				metadata.IsObfuscated   = IsObfuscatedSymbol(symbol);
				metadata.Role           = DetermineSymbolRole(context, symbol, match.Index - contextStart);
				metadata.IsLikelyGlobal = DetermineIfGlobal(context, symbol);
			}

			// Calculate additional metrics
			foreach (var metadata in symbols.Values) {
				metadata.SpreadScore      = CalculateSpread(metadata.Occurrences, content.Length);
				metadata.ContextDiversity = metadata.Contexts.Count;
				metadata.Signature        = GenerateSymbolSignature(metadata);
			}

			return symbols;
		}

		/// <summary>
		/// Analyze symbol-level consistency between versions
		/// </summary>
		private static void AnalyzeSymbolConsistency(Dictionary<string, SymbolMetadata> symbols1, Dictionary<string, SymbolMetadata> symbols2, ConsistencyAnalysis analysis) {
			// Find symbols present in both versions
			var commonSymbols = symbols1.Keys.Intersect(symbols2.Keys).ToList();
			var uniqueToV1    = symbols1.Keys.Except(symbols2.Keys).ToList();
			var uniqueToV2    = symbols2.Keys.Except(symbols1.Keys).ToList();

			analysis.CommonSymbolCount     = commonSymbols.Count;
			analysis.UniqueToVersion1Count = uniqueToV1.Count;
			analysis.UniqueToVersion2Count = uniqueToV2.Count;

			// Analyze consistency of common symbols
			foreach (string symbol in commonSymbols) {
				var meta1 = symbols1[symbol];
				var meta2 = symbols2[symbol];

				var consistency = new SymbolConsistencyInfo {
					Symbol            = symbol,
					FrequencyChange   = meta2.TotalCount - meta1.TotalCount,
					RoleConsistent    = meta1.Role == meta2.Role,
					ContextSimilarity = CalculateContextSimilarity(meta1.Contexts, meta2.Contexts),
					SpreadChange      = meta2.SpreadScore - meta1.SpreadScore,
					ConsistencyScore  = CalculateConsistencyScore(meta1, meta2)
				};

				if (consistency.ConsistencyScore > 0.7) {
					analysis.ConsistentSymbols.Add(symbol, consistency);
				} else {
					analysis.InconsistentSymbols.Add(symbol, consistency);
				}
			}

			// Analyze unique symbols for potential renames
			analysis.PotentialRenames = FindPotentialRenames(uniqueToV1, uniqueToV2, symbols1, symbols2);
		}

		/// <summary>
		/// Analyze structural consistency (modules, classes, functions)
		/// </summary>
		private static void AnalyzeStructuralConsistency(string content1, string content2, ConsistencyAnalysis analysis) {
			// Extract structural elements from both versions
			var modules1   = ExtractModules(content1);
			var modules2   = ExtractModules(content2);
			var classes1   = ExtractClasses(content1);
			var classes2   = ExtractClasses(content2);
			var functions1 = ExtractFunctions(content1);
			var functions2 = ExtractFunctions(content2);

			analysis.StructuralConsistency = new StructuralConsistencyInfo {
				ModuleConsistency      = CalculateStructuralConsistency(modules1, modules2),
				ClassConsistency       = CalculateStructuralConsistency(classes1, classes2),
				FunctionConsistency    = CalculateStructuralConsistency(functions1, functions2),
				OverallStructuralScore = 0.0 // Will be calculated below
			};

			// Calculate overall structural score
			analysis.StructuralConsistency.OverallStructuralScore =
				(analysis.StructuralConsistency.ModuleConsistency +
				 analysis.StructuralConsistency.ClassConsistency +
				 analysis.StructuralConsistency.FunctionConsistency) / 3.0;
		}

		/// <summary>
		/// Analyze consistency of existing mappings across versions
		/// </summary>
		private static void AnalyzeMappingConsistency(ConsistencyAnalysis analysis) {
			// Load existing mappings if available
			var mappingsPath = "decomp/mappings.json";
			if (!File.Exists(mappingsPath)) {
				analysis.MappingConsistencyScore = 1.0; // No mappings to be inconsistent with
				return;
			}

			try {
				string json             = File.ReadAllText(mappingsPath);
				var    existingMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

				int consistentMappings = 0;
				int totalMappings      = existingMappings.Count;

				foreach (var mapping in existingMappings) {
					// Check if both source and target symbols appear consistently
					bool sourceConsistent = analysis.ConsistentSymbols.ContainsKey(mapping.Key);
					bool targetMakesSense = !IsObfuscatedSymbol(mapping.Value);

					if (sourceConsistent && targetMakesSense) {
						consistentMappings++;
					}
				}

				analysis.MappingConsistencyScore = totalMappings > 0 ? (double)consistentMappings / totalMappings : 1.0;
			} catch (Exception ex) {
				Logger.Warning($"Failed to analyze mapping consistency: {ex.Message}");
				analysis.MappingConsistencyScore = 0.5; // Unknown consistency
			}
		}

		/// <summary>
		/// Generate recommendations based on consistency analysis
		/// </summary>
		private static void GenerateConsistencyRecommendations(ConsistencyAnalysis analysis) {
			var recommendations = new List<ConsistencyRecommendation>();

			// High-confidence consistent symbols (safe to rename across versions)
			var highConfidenceSymbols = analysis.ConsistentSymbols
				.Where(kvp => kvp.Value.ConsistencyScore > 0.85 && kvp.Value.RoleConsistent)
				.OrderByDescending(kvp => kvp.Value.ConsistencyScore)
				.Take(20);

			foreach (var symbol in highConfidenceSymbols) {
				recommendations.Add(new ConsistencyRecommendation {
					Type       = RecommendationType.SafeRename,
					Symbol     = symbol.Key,
					Confidence = symbol.Value.ConsistencyScore,
					Rationale  = $"Highly consistent across versions (score: {symbol.Value.ConsistencyScore:F2}), safe to rename",
					Priority   = symbol.Value.ConsistencyScore > 0.9 ? "High" : "Medium"
				});
			}

			// Potential version-specific renames that should be investigated
			var versionSpecificSymbols = analysis.InconsistentSymbols
				.Where(kvp => Math.Abs(kvp.Value.FrequencyChange) > 5)
				.OrderByDescending(kvp => Math.Abs(kvp.Value.FrequencyChange));

			foreach (var symbol in versionSpecificSymbols.Take(10)) {
				recommendations.Add(new ConsistencyRecommendation {
					Type       = RecommendationType.InvestigateChange,
					Symbol     = symbol.Key,
					Confidence = 1.0 - symbol.Value.ConsistencyScore,
					Rationale  = $"Significant frequency change ({symbol.Value.FrequencyChange:+#;-#;0}) between versions",
					Priority   = "Medium"
				});
			}

			// Potential renames (symbols that disappeared in one version but similar ones appeared)
			foreach (var rename in analysis.PotentialRenames.Take(5)) {
				recommendations.Add(new ConsistencyRecommendation {
					Type       = RecommendationType.PotentialRename,
					Symbol     = rename.OldSymbol,
					NewSymbol  = rename.NewSymbol,
					Confidence = rename.Confidence,
					Rationale  = $"Possible rename: {rename.OldSymbol} → {rename.NewSymbol} (confidence: {rename.Confidence:F2})",
					Priority   = rename.Confidence > 0.7 ? "High" : "Low"
				});
			}

			analysis.Recommendations = recommendations;
		}

		/// <summary>
		/// Update the consistency database with new findings
		/// </summary>
		private static async Task UpdateConsistencyDatabase(ConsistencyAnalysis analysis) {
			var consistencyData = new ConsistencyDatabase();

			// Load existing data if available
			if (File.Exists(CONSISTENCY_DB)) {
				try {
					string json = await File.ReadAllTextAsync(CONSISTENCY_DB);
					consistencyData = JsonSerializer.Deserialize<ConsistencyDatabase>(json) ?? new ConsistencyDatabase();
				} catch (Exception ex) {
					Logger.Warning($"Failed to load consistency database: {ex.Message}");
				}
			}

			// Add new analysis
			consistencyData.Analyses.Add(analysis);
			consistencyData.LastUpdated = DateTime.UtcNow;

			// Update symbol reliability scores based on consistency across analyses
			UpdateSymbolReliabilityScores(consistencyData);

			// Keep only recent analyses (last 10)
			if (consistencyData.Analyses.Count > 10) {
				consistencyData.Analyses = consistencyData.Analyses
					.OrderByDescending(a => a.AnalysisTimestamp)
					.Take(10)
					.ToList();
			}

			// Save updated database
			try {
				Directory.CreateDirectory(Path.GetDirectoryName(CONSISTENCY_DB)!);
				string updatedJson = JsonSerializer.Serialize(consistencyData, new JsonSerializerOptions { WriteIndented = true });
				await File.WriteAllTextAsync(CONSISTENCY_DB, updatedJson);
			} catch (Exception ex) {
				Logger.Warning($"Failed to save consistency database: {ex.Message}");
			}
		}

		/// <summary>
		/// Update symbol reliability scores based on historical consistency
		/// </summary>
		private static void UpdateSymbolReliabilityScores(ConsistencyDatabase database) {
			var symbolScores = new Dictionary<string, List<double>>();

			// Collect consistency scores for each symbol across all analyses
			foreach (var analysis in database.Analyses) {
				foreach (var symbol in analysis.ConsistentSymbols) {
					if (!symbolScores.ContainsKey(symbol.Key)) {
						symbolScores[symbol.Key] = new List<double>();
					}
					symbolScores[symbol.Key].Add(symbol.Value.ConsistencyScore);
				}
			}

			// Calculate average reliability scores
			database.SymbolReliabilityScores.Clear();
			foreach (var symbol in symbolScores) {
				double averageScore = symbol.Value.Average();
				double consistency  = 1.0 - (symbol.Value.Max() - symbol.Value.Min()); // Penalize high variance
				double reliability  = (averageScore * 0.7) + (consistency * 0.3);

				database.SymbolReliabilityScores[symbol.Key] = reliability;
			}
		}

		// Helper methods
		private static List<string> ExtractModules(string content) {
			var pattern = @"var\s+([A-Za-z0-9_]+)\s*=\s*U\s*\(";
			return Regex.Matches(content, pattern).Cast<Match>()
				.Select(m => m.Groups[1].Value).ToList();
		}

		private static List<string> ExtractClasses(string content) {
			var pattern = @"class\s+([A-Za-z0-9_]+)";
			return Regex.Matches(content, pattern).Cast<Match>()
				.Select(m => m.Groups[1].Value).ToList();
		}

		private static List<string> ExtractFunctions(string content) {
			var pattern = @"function\s+([A-Za-z0-9_]+)\s*\(";
			return Regex.Matches(content, pattern).Cast<Match>()
				.Select(m => m.Groups[1].Value).ToList();
		}

		private static double CalculateStructuralConsistency(List<string> items1, List<string> items2) {
			if (items1.Count == 0 && items2.Count == 0) return 1.0;
			if (items1.Count == 0 || items2.Count == 0) return 0.0;

			int common = items1.Intersect(items2).Count();
			int total  = items1.Union(items2).Count();
			return (double)common / total;
		}

		private static double CalculateConsistencyScore(SymbolMetadata meta1, SymbolMetadata meta2) {
			double score = 0.0;

			// Role consistency (40% weight)
			if (meta1.Role == meta2.Role) score += 0.4;

			// Frequency consistency (30% weight)
			if (meta1.TotalCount > 0 && meta2.TotalCount > 0) {
				double freqRatio = Math.Min(meta1.TotalCount, meta2.TotalCount) / (double)Math.Max(meta1.TotalCount, meta2.TotalCount);
				score += 0.3 * freqRatio;
			}

			// Context similarity (20% weight)
			double contextSim = CalculateContextSimilarity(meta1.Contexts, meta2.Contexts);
			score += 0.2 * contextSim;

			// Spread consistency (10% weight)
			double spreadSim = 1.0 - Math.Abs(meta1.SpreadScore - meta2.SpreadScore);
			score += 0.1 * spreadSim;

			return score;
		}

		private static double CalculateContextSimilarity(List<string> contexts1, List<string> contexts2) {
			if (contexts1.Count == 0 && contexts2.Count == 0) return 1.0;
			if (contexts1.Count == 0 || contexts2.Count == 0) return 0.0;

			int commonPatterns = 0;
			int totalPatterns  = contexts1.Count + contexts2.Count;

			foreach (var context1 in contexts1) {
				foreach (var context2 in contexts2) {
					if (CalculateLevenshteinSimilarity(context1, context2) > 0.6) {
						commonPatterns++;
						break;
					}
				}
			}

			return (double)(commonPatterns * 2) / totalPatterns;
		}

		private static double CalculateLevenshteinSimilarity(string s1, string s2) {
			if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
			if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

			int maxLen   = Math.Max(s1.Length, s2.Length);
			int distance = CalculateLevenshteinDistance(s1, s2);
			return 1.0 - ((double)distance / maxLen);
		}

		private static int CalculateLevenshteinDistance(string s1, string s2) {
			int[,] d = new int[s1.Length + 1, s2.Length + 1];

			for (int i = 0; i <= s1.Length; i++) d[i, 0] = i;
			for (int j = 0; j <= s2.Length; j++) d[0, j] = j;

			for (int i = 1; i <= s1.Length; i++) {
				for (int j = 1; j <= s2.Length; j++) {
					int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
					d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
				}
			}

			return d[s1.Length, s2.Length];
		}

		private static List<PotentialRename> FindPotentialRenames(List<string>                       uniqueToV1,
		                                                          List<string>                       uniqueToV2,
		                                                          Dictionary<string, SymbolMetadata> symbols1,
		                                                          Dictionary<string, SymbolMetadata> symbols2) {
			var potentialRenames = new List<PotentialRename>();

			foreach (string oldSymbol in uniqueToV1) {
				if (!symbols1.ContainsKey(oldSymbol)) continue;
				var oldMeta = symbols1[oldSymbol];

				var    bestMatch      = string.Empty;
				double bestConfidence = 0.0;

				foreach (string newSymbol in uniqueToV2) {
					if (!symbols2.ContainsKey(newSymbol)) continue;
					var newMeta = symbols2[newSymbol];

					// Calculate similarity based on characteristics
					double confidence = CalculateRenameConfidence(oldMeta, newMeta);
					if (confidence > bestConfidence && confidence > 0.5) {
						bestMatch      = newSymbol;
						bestConfidence = confidence;
					}
				}

				if (!string.IsNullOrEmpty(bestMatch)) {
					potentialRenames.Add(new PotentialRename {
						OldSymbol  = oldSymbol,
						NewSymbol  = bestMatch,
						Confidence = bestConfidence
					});
				}
			}

			return potentialRenames.OrderByDescending(r => r.Confidence).ToList();
		}

		private static double CalculateRenameConfidence(SymbolMetadata oldMeta, SymbolMetadata newMeta) {
			double confidence = 0.0;

			// Role similarity (50% weight)
			if (oldMeta.Role == newMeta.Role) confidence += 0.5;

			// Frequency similarity (30% weight)
			if (oldMeta.TotalCount > 0 && newMeta.TotalCount > 0) {
				double freqRatio = Math.Min(oldMeta.TotalCount, newMeta.TotalCount) / (double)Math.Max(oldMeta.TotalCount, newMeta.TotalCount);
				confidence += 0.3 * freqRatio;
			}

			// Context similarity (20% weight)
			double contextSim = CalculateContextSimilarity(oldMeta.Contexts, newMeta.Contexts);
			confidence += 0.2 * contextSim;

			return confidence;
		}

		private static bool IsObfuscatedSymbol(string symbol) {
			if (symbol.Length <= 3 && Regex.IsMatch(symbol, @"^[A-Z][a-z0-9]*[0-9]?$")) return true;
			if (symbol.Length == 2 && symbol.All(char.IsUpper)) return true;
			return false;
		}

		private static SymbolRole DetermineSymbolRole(string context, string symbol, int symbolPos) {
			string beforeSymbol = symbolPos > 0 ? context.Substring(0, symbolPos) : "";
			string afterSymbol  = symbolPos + symbol.Length < context.Length ? context.Substring(symbolPos + symbol.Length) : "";

			if (Regex.IsMatch(beforeSymbol, @"var\s+$") && Regex.IsMatch(afterSymbol, @"^\s*=\s*U\s*\(")) {
				return SymbolRole.ModuleName;
			}
			if (Regex.IsMatch(beforeSymbol, @"class\s+$")) {
				return SymbolRole.ClassName;
			}
			if (Regex.IsMatch(beforeSymbol, @"function\s+$") && Regex.IsMatch(afterSymbol, @"^\s*\(")) {
				return SymbolRole.FunctionName;
			}
			if (Regex.IsMatch(afterSymbol, @"^\s*[:=]\s*[""'\d]") || char.IsUpper(symbol[0])) {
				return SymbolRole.ConstantName;
			}
			if (Regex.IsMatch(beforeSymbol, @"\.$") || Regex.IsMatch(afterSymbol, @"^\s*:")) {
				return SymbolRole.PropertyName;
			}

			return SymbolRole.VariableName;
		}

		private static bool DetermineIfGlobal(string context, string symbol) {
			// Heuristic: if symbol appears in module definition or class definition, it's likely global
			return Regex.IsMatch(context, @"var\s+" + Regex.Escape(symbol) + @"\s*=") ||
			       Regex.IsMatch(context, @"class\s+" + Regex.Escape(symbol)) ||
			       Regex.IsMatch(context, @"function\s+" + Regex.Escape(symbol));
		}

		private static double CalculateSpread(List<int> positions, int totalLength) {
			if (positions.Count <= 1) return 0.0;

			var    sorted = positions.OrderBy(p => p).ToList();
			double spread = sorted.Last() - sorted.First();
			return Math.Min(1.0, spread / totalLength);
		}

		private static string GenerateSymbolSignature(SymbolMetadata metadata) {
			// Generate a signature based on symbol characteristics
			var signature = new StringBuilder();
			signature.Append(metadata.Role.ToString()[0]);
			signature.Append(metadata.TotalCount.ToString("000"));
			signature.Append(metadata.IsObfuscated ? "O" : "R");
			signature.Append(metadata.IsLikelyGlobal ? "G" : "L");
			return signature.ToString();
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
		/// Generate comprehensive consistency report
		/// </summary>
		public static string GenerateConsistencyReport(ConsistencyAnalysis analysis) {
			var report = new StringBuilder();

			report.AppendLine("# Cross-Version Consistency Analysis Report");
			report.AppendLine($"**Versions:** {analysis.Version1Name} vs {analysis.Version2Name}");
			report.AppendLine($"**Analysis Date:** {analysis.AnalysisTimestamp:yyyy-MM-dd HH:mm:ss}");
			report.AppendLine();

			// Overall Statistics
			report.AppendLine("## 📊 Overall Statistics");
			report.AppendLine($"- **Common Symbols:** {analysis.CommonSymbolCount:N0}");
			report.AppendLine($"- **Consistent Symbols:** {analysis.ConsistentSymbols.Count:N0}");
			report.AppendLine($"- **Inconsistent Symbols:** {analysis.InconsistentSymbols.Count:N0}");
			report.AppendLine($"- **Unique to {analysis.Version1Name}:** {analysis.UniqueToVersion1Count:N0}");
			report.AppendLine($"- **Unique to {analysis.Version2Name}:** {analysis.UniqueToVersion2Count:N0}");
			report.AppendLine($"- **Potential Renames:** {analysis.PotentialRenames.Count:N0}");

			// Calculate consistency percentage
			double consistencyPercentage = analysis.CommonSymbolCount > 0 ? (double)analysis.ConsistentSymbols.Count / analysis.CommonSymbolCount * 100 : 100;
			report.AppendLine($"- **Overall Consistency:** {consistencyPercentage:F1}%");
			report.AppendLine();

			// Structural Consistency
			if (analysis.StructuralConsistency != null) {
				report.AppendLine("## 🏗️ Structural Consistency");
				report.AppendLine($"- **Module Consistency:** {analysis.StructuralConsistency.ModuleConsistency:P1}");
				report.AppendLine($"- **Class Consistency:** {analysis.StructuralConsistency.ClassConsistency:P1}");
				report.AppendLine($"- **Function Consistency:** {analysis.StructuralConsistency.FunctionConsistency:P1}");
				report.AppendLine($"- **Overall Structural Score:** {analysis.StructuralConsistency.OverallStructuralScore:P1}");
				report.AppendLine();
			}

			// Top Consistent Symbols
			report.AppendLine("## ✅ Most Consistent Symbols");
			var topConsistent = analysis.ConsistentSymbols.Values
				.OrderByDescending(s => s.ConsistencyScore)
				.Take(15);

			foreach (var symbol in topConsistent) {
				report.AppendLine($"- **`{symbol.Symbol}`**: {symbol.ConsistencyScore:P1} consistency");
				if (symbol.FrequencyChange != 0) {
					report.AppendLine($"  - Frequency change: {symbol.FrequencyChange:+#;-#;0}");
				}
				if (!symbol.RoleConsistent) {
					report.AppendLine($"  - ⚠️ Role changed between versions");
				}
			}
			report.AppendLine();

			// Potential Renames
			if (analysis.PotentialRenames.Any()) {
				report.AppendLine("## 🔄 Potential Renames Detected");
				foreach (var rename in analysis.PotentialRenames.Take(10)) {
					report.AppendLine($"- **`{rename.OldSymbol}` → `{rename.NewSymbol}`** (confidence: {rename.Confidence:P1})");
				}
				report.AppendLine();
			}

			// Recommendations
			if (analysis.Recommendations.Any()) {
				report.AppendLine("## 💡 Consistency Recommendations");
				var groupedRecs = analysis.Recommendations.GroupBy(r => r.Type);

				foreach (var group in groupedRecs) {
					report.AppendLine($"### {group.Key} ({group.Count()} items)");

					foreach (var rec in group.Take(8)) {
						report.AppendLine($"- **{rec.Priority} Priority**: {rec.Rationale}");
						if (!string.IsNullOrEmpty(rec.NewSymbol)) {
							report.AppendLine($"  - Suggested: `{rec.Symbol}` → `{rec.NewSymbol}`");
						}
					}
					report.AppendLine();
				}
			}

			// Progressive Learning Strategy
			report.AppendLine("## 🎯 Progressive Learning Strategy");
			report.AppendLine("1. **Safe Renames**: Start with high-confidence consistent symbols");
			report.AppendLine("2. **Investigate Changes**: Review symbols with significant frequency changes");
			report.AppendLine("3. **Validate Renames**: Verify potential renames through manual inspection");
			report.AppendLine("4. **Update Mappings**: Apply validated consistent mappings to both versions");
			report.AppendLine($"5. **Mapping Consistency**: Current mapping consistency score: {analysis.MappingConsistencyScore:P1}");

			return report.ToString();
		}
	}

	// Data structures for consistency analysis
	public class ConsistencyAnalysis {
		public string   Version1Name      { get; set; } = "";
		public string   Version2Name      { get; set; } = "";
		public string   Version1Path      { get; set; } = "";
		public string   Version2Path      { get; set; } = "";
		public DateTime AnalysisTimestamp { get; set; }

		public int CommonSymbolCount     { get; set; }
		public int UniqueToVersion1Count { get; set; }
		public int UniqueToVersion2Count { get; set; }

		public Dictionary<string, SymbolConsistencyInfo> ConsistentSymbols   { get; set; } = new();
		public Dictionary<string, SymbolConsistencyInfo> InconsistentSymbols { get; set; } = new();
		public List<PotentialRename>                     PotentialRenames    { get; set; } = new();

		public StructuralConsistencyInfo?      StructuralConsistency   { get; set; }
		public double                          MappingConsistencyScore { get; set; }
		public List<ConsistencyRecommendation> Recommendations         { get; set; } = new();
	}

	public class SymbolMetadata {
		public string       Symbol           { get; set; } = "";
		public int          FirstPosition    { get; set; }
		public List<int>    Occurrences      { get; set; } = new();
		public List<string> Contexts         { get; set; } = new();
		public int          TotalCount       { get; set; }
		public bool         IsObfuscated     { get; set; }
		public SymbolRole   Role             { get; set; }
		public bool         IsLikelyGlobal   { get; set; }
		public double       SpreadScore      { get; set; }
		public int          ContextDiversity { get; set; }
		public string       Signature        { get; set; } = "";
	}

	public class SymbolConsistencyInfo {
		public string Symbol            { get; set; } = "";
		public int    FrequencyChange   { get; set; }
		public bool   RoleConsistent    { get; set; }
		public double ContextSimilarity { get; set; }
		public double SpreadChange      { get; set; }
		public double ConsistencyScore  { get; set; }
	}

	public class StructuralConsistencyInfo {
		public double ModuleConsistency      { get; set; }
		public double ClassConsistency       { get; set; }
		public double FunctionConsistency    { get; set; }
		public double OverallStructuralScore { get; set; }
	}

	public class PotentialRename {
		public string OldSymbol  { get; set; } = "";
		public string NewSymbol  { get; set; } = "";
		public double Confidence { get; set; }
	}

	public class ConsistencyRecommendation {
		public RecommendationType Type       { get; set; }
		public string             Symbol     { get; set; } = "";
		public string             NewSymbol  { get; set; } = "";
		public double             Confidence { get; set; }
		public string             Rationale  { get; set; } = "";
		public string             Priority   { get; set; } = "";
	}

	public class ConsistencyDatabase {
		public DateTime                   LastUpdated             { get; set; }
		public List<ConsistencyAnalysis>  Analyses                { get; set; } = new();
		public Dictionary<string, double> SymbolReliabilityScores { get; set; } = new();
	}

	public enum RecommendationType {
		SafeRename,
		InvestigateChange,
		PotentialRename,
		AvoidRename
	}
}