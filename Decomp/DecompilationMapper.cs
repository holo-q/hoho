using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hoho.Core;

namespace Hoho.Decomp {
	/// <summary>
	/// Maps manually edited decompiled code back to original obfuscated symbols.
	/// Learns from your clean refactoring to automatically apply to new versions.
	/// </summary>
	public class DecompilationMapper {
		private readonly string    _mapPath;
		private readonly SymbolMap _symbolMap = new SymbolMap();

		public DecompilationMapper(string mapPath = "decomp/learned-mappings.json") {
			_mapPath = mapPath;
			LoadMappings();
		}

		/// <summary>
		/// Learn mappings by comparing your edited clean code with original obfuscated
		/// </summary>
		public async Task<MappingResult> LearnMappings(string originalPath, string editedPath) {
			string original = await File.ReadAllTextAsync(originalPath);
			string edited   = await File.ReadAllTextAsync(editedPath);

			MappingResult result = new MappingResult();

			// Step 1: Extract function/class structure from both
			List<CodeStructure> originalStructure = ExtractStructure(original, true);
			List<CodeStructure> editedStructure   = ExtractStructure(edited, false);

			// Step 2: Match structures by similarity
			List<StructureMatch> matches = MatchStructures(originalStructure, editedStructure);

			// Step 3: Extract symbol mappings from matched pairs
			foreach (StructureMatch match in matches) {
				// Map function/class names
				_symbolMap.Add(match.Original.Name, match.Edited.Name, SymbolType.Function);
				result.FunctionMappings[match.Original.Name] = match.Edited.Name;

				// Map parameters
				List<(string, string)> paramMatches = MatchParameters(match.Original, match.Edited);
				foreach ((string origParam, string editParam) in paramMatches) {
					_symbolMap.Add(origParam, editParam, SymbolType.Parameter);
					result.ParameterMappings[origParam] = editParam;
				}

				// Map local variables by analyzing usage patterns
				List<(string, string)> varMatches = MatchVariables(match.Original.Body, match.Edited.Body);
				foreach ((string origVar, string editVar) in varMatches) {
					_symbolMap.Add(origVar, editVar, SymbolType.Variable);
					result.VariableMappings[origVar] = editVar;
				}

				// Map property/method names
				List<(string, string)> propMatches = MatchProperties(match.Original.Body, match.Edited.Body);
				foreach ((string origProp, string editProp) in propMatches) {
					_symbolMap.Add(origProp, editProp, SymbolType.Property);
					result.PropertyMappings[origProp] = editProp;
				}
			}

			// Step 4: Learn patterns from the mappings
			LearnNamingPatterns(result);

			// Step 5: Save learned mappings
			await SaveMappings();

			return result;
		}

		/// <summary>
		/// Apply learned mappings to new obfuscated code
		/// </summary>
		public async Task<string> ApplyMappings(string obfuscatedPath) {
			string obfuscated = await File.ReadAllTextAsync(obfuscatedPath);
			string result     = obfuscated;

			// Sort mappings by length (longest first) to avoid partial replacements
			List<SymbolMapping> sortedMappings = _symbolMap.GetAllMappings()
				.OrderByDescending(m => m.Original.Length)
				.ToList();

			// Apply direct mappings
			foreach (SymbolMapping mapping in sortedMappings) {
				// Use word boundary regex to avoid partial matches
				string pattern = $@"\b{Regex.Escape(mapping.Original)}\b";
				result = Regex.Replace(result, pattern, mapping.Mapped);
			}

			// Apply pattern-based mappings for unknown symbols
			result = ApplyPatternBasedRenames(result);

			return result;
		}

		/// <summary>
		/// Generate a mapping report showing what was learned
		/// </summary>
		public string GenerateReport() {
			StringBuilder sb = new StringBuilder();

			sb.AppendLine("DECOMPILATION MAPPING REPORT");
			sb.AppendLine("=".PadRight(60, '='));
			sb.AppendLine();

			IEnumerable<IGrouping<SymbolType, SymbolMapping>> byType = _symbolMap.GetAllMappings().GroupBy(m => m.Type);

			foreach (IGrouping<SymbolType, SymbolMapping> typeGroup in byType) {
				sb.AppendLine($"{typeGroup.Key}s ({typeGroup.Count()}):");
				sb.AppendLine("-".PadRight(40, '-'));

				foreach (SymbolMapping mapping in typeGroup.Take(10)) {
					sb.AppendLine($"  {mapping.Original,-20} -> {mapping.Mapped}");
				}

				if (typeGroup.Count() > 10) {
					sb.AppendLine($"  ... and {typeGroup.Count() - 10} more");
				}
				sb.AppendLine();
			}

			// Show learned patterns
			sb.AppendLine("LEARNED PATTERNS:");
			sb.AppendLine("-".PadRight(40, '-'));

			foreach (NamingPattern pattern in _symbolMap.Patterns) {
				sb.AppendLine($"  {pattern.Pattern}: {pattern.Description}");
				sb.AppendLine($"    Example: {pattern.Example}");
			}

			return sb.ToString();
		}

		// Structure extraction
		private List<CodeStructure> ExtractStructure(string code, bool isObfuscated) {
			List<CodeStructure> structures = new List<CodeStructure>();

			// Extract functions
			string funcPattern = isObfuscated
				? @"function\s+([a-zA-Z_$][\w$]*)\s*\(([^)]*)\)\s*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}"
				: @"function\s+(\w+)\s*\(([^)]*)\)\s*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}";

			MatchCollection funcMatches = Regex.Matches(code, funcPattern, RegexOptions.Singleline);

			foreach (Match match in funcMatches) {
				structures.Add(new CodeStructure {
					Type       = "function",
					Name       = match.Groups[1].Value,
					Parameters = match.Groups[2].Value.Split(',').Select(p => p.Trim()).ToList(),
					Body       = match.Groups[3].Value,
					FullMatch  = match.Value
				});
			}

			// Extract classes
			string          classPattern = @"class\s+(\w+)(?:\s+extends\s+(\w+))?\s*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}";
			MatchCollection classMatches = Regex.Matches(code, classPattern, RegexOptions.Singleline);

			foreach (Match match in classMatches) {
				structures.Add(new CodeStructure {
					Type      = "class",
					Name      = match.Groups[1].Value,
					Extends   = match.Groups[2].Value,
					Body      = match.Groups[3].Value,
					FullMatch = match.Value
				});
			}

			// Extract arrow functions assigned to variables
			string          arrowPattern = @"(?:const|let|var)\s+(\w+)\s*=\s*(?:\([^)]*\)|[^=])\s*=>\s*(?:\{[^}]+\}|[^;]+);";
			MatchCollection arrowMatches = Regex.Matches(code, arrowPattern);

			foreach (Match match in arrowMatches) {
				structures.Add(new CodeStructure {
					Type      = "arrow",
					Name      = match.Groups[1].Value,
					Body      = match.Value,
					FullMatch = match.Value
				});
			}

			return structures;
		}

		// Structure matching using similarity analysis
		private List<StructureMatch> MatchStructures(List<CodeStructure> original, List<CodeStructure> edited) {
			List<StructureMatch>   matches    = new List<StructureMatch>();
			HashSet<CodeStructure> usedEdited = new HashSet<CodeStructure>();

			foreach (CodeStructure orig in original) {
				CodeStructure? bestMatch = null;
				double         bestScore = 0;

				foreach (CodeStructure edit in edited.Where(e => !usedEdited.Contains(e))) {
					// Skip if types don't match
					if (orig.Type != edit.Type) continue;

					// Calculate similarity score
					double score = CalculateSimilarity(orig, edit);

					if (score > bestScore && score > 0.6) // 60% similarity threshold
					{
						bestMatch = edit;
						bestScore = score;
					}
				}

				if (bestMatch != null) {
					matches.Add(new StructureMatch {
						Original   = orig,
						Edited     = bestMatch,
						Confidence = bestScore
					});
					usedEdited.Add(bestMatch);
				}
			}

			return matches;
		}

		// Calculate similarity between two code structures
		private double CalculateSimilarity(CodeStructure orig, CodeStructure edited) {
			double score   = 0;
			double weights = 0;

			// Compare structure (calls, loops, conditions)
			List<string> origCalls = Regex.Matches(orig.Body, @"\w+\s*\(").Select(m => m.Value).ToList();
			List<string> editCalls = Regex.Matches(edited.Body, @"\w+\s*\(").Select(m => m.Value).ToList();

			// Remove obvious renamed calls but keep structure
			List<string> origCallsNormalized = origCalls.Select(c => Regex.Replace(c, @"\w+", "FUNC")).ToList();
			List<string> editCallsNormalized = editCalls.Select(c => Regex.Replace(c, @"\w+", "FUNC")).ToList();

			double callSimilarity = CalculateListSimilarity(origCallsNormalized, editCallsNormalized);
			score   += callSimilarity * 0.3;
			weights += 0.3;

			// Compare control flow
			List<string> origFlow       = ExtractControlFlow(orig.Body);
			List<string> editFlow       = ExtractControlFlow(edited.Body);
			double       flowSimilarity = CalculateListSimilarity(origFlow, editFlow);
			score   += flowSimilarity * 0.3;
			weights += 0.3;

			// Compare string literals (strong indicator)
			List<string> origStrings      = ExtractStringLiterals(orig.Body);
			List<string> editStrings      = ExtractStringLiterals(edited.Body);
			double       stringSimilarity = CalculateListSimilarity(origStrings, editStrings);
			score   += stringSimilarity * 0.4;
			weights += 0.4;

			return score / weights;
		}

		private List<string> ExtractControlFlow(string body) {
			List<string> flow = new List<string>();

			// Extract control flow keywords in order
			string          pattern = @"\b(if|else|for|while|switch|case|return|throw|try|catch)\b";
			MatchCollection matches = Regex.Matches(body, pattern);

			foreach (Match match in matches) {
				flow.Add(match.Groups[1].Value);
			}

			return flow;
		}

		private List<string> ExtractStringLiterals(string body) {
			List<string> strings = new List<string>();

			string          pattern = @"[""']([^""']{2,})[""']";
			MatchCollection matches = Regex.Matches(body, pattern);

			foreach (Match match in matches) {
				strings.Add(match.Groups[1].Value);
			}

			return strings;
		}

		private double CalculateListSimilarity<T>(List<T> list1, List<T> list2) {
			if (!list1.Any() && !list2.Any()) return 1.0;
			if (!list1.Any() || !list2.Any()) return 0.0;

			HashSet<T> set1 = new HashSet<T>(list1);
			HashSet<T> set2 = new HashSet<T>(list2);

			int intersection = set1.Intersect(set2).Count();
			int union        = set1.Union(set2).Count();

			return (double)intersection / union;
		}

		// Match parameters between original and edited functions
		private List<(string, string)> MatchParameters(CodeStructure orig, CodeStructure edited) {
			List<(string, string)> matches = new List<(string, string)>();

			// If same number of parameters, match by position
			if (orig.Parameters.Count == edited.Parameters.Count) {
				for (int i = 0; i < orig.Parameters.Count; i++) {
					if (!string.IsNullOrWhiteSpace(orig.Parameters[i]) &&
					    !string.IsNullOrWhiteSpace(edited.Parameters[i])) {
						matches.Add((orig.Parameters[i], edited.Parameters[i]));
					}
				}
			}

			return matches;
		}

		// Match variables by usage patterns
		private List<(string, string)> MatchVariables(string origBody, string editBody) {
			List<(string, string)> matches = new List<(string, string)>();

			// Extract variable declarations
			List<string> origVars = ExtractVariables(origBody);
			List<string> editVars = ExtractVariables(editBody);

			// Match by usage context
			foreach (string origVar in origVars) {
				VariableContext origContext = GetVariableContext(origBody, origVar);

				foreach (string editVar in editVars) {
					VariableContext editContext = GetVariableContext(editBody, editVar);

					// If used in similar contexts, likely the same variable
					if (ContextSimilarity(origContext, editContext) > 0.7) {
						matches.Add((origVar, editVar));
						break;
					}
				}
			}

			return matches;
		}

		private List<string> ExtractVariables(string body) {
			HashSet<string> vars = new HashSet<string>();

			// Extract let/const/var declarations
			string          pattern = @"(?:let|const|var)\s+(\w+)";
			MatchCollection matches = Regex.Matches(body, pattern);

			foreach (Match match in matches) {
				vars.Add(match.Groups[1].Value);
			}

			return vars.ToList();
		}

		private VariableContext GetVariableContext(string body, string varName) {
			VariableContext context = new VariableContext { Name = varName };

			// Find what the variable is assigned from
			string assignPattern = $@"{Regex.Escape(varName)}\s*=\s*([^;]+);";
			Match  assignMatch   = Regex.Match(body, assignPattern);
			if (assignMatch.Success) {
				context.AssignedFrom = assignMatch.Groups[1].Value;
			}

			// Find what methods are called on it
			string          methodPattern = $@"{Regex.Escape(varName)}\.(\w+)\(";
			MatchCollection methodMatches = Regex.Matches(body, methodPattern);
			context.MethodsCalled = methodMatches.Select(m => m.Groups[1].Value).ToList();

			// Find where it's passed as argument
			string argPattern = $@"\w+\([^)]*{Regex.Escape(varName)}[^)]*\)";
			context.UsedAsArgument = Regex.IsMatch(body, argPattern);

			return context;
		}

		private double ContextSimilarity(VariableContext ctx1, VariableContext ctx2) {
			double score = 0;

			// Similar assignment patterns
			if (!string.IsNullOrEmpty(ctx1.AssignedFrom) && !string.IsNullOrEmpty(ctx2.AssignedFrom)) {
				// Normalize and compare
				string norm1              = Regex.Replace(ctx1.AssignedFrom, @"\w+", "X");
				string norm2              = Regex.Replace(ctx2.AssignedFrom, @"\w+", "X");
				if (norm1 == norm2) score += 0.4;
			}

			// Similar methods called
			double methodSimilarity = CalculateListSimilarity(ctx1.MethodsCalled, ctx2.MethodsCalled);
			score += methodSimilarity * 0.4;

			// Both used as arguments
			if (ctx1.UsedAsArgument == ctx2.UsedAsArgument)
				score += 0.2;

			return score;
		}

		// Match properties/methods
		private List<(string, string)> MatchProperties(string origBody, string editBody) {
			List<(string, string)> matches = new List<(string, string)>();

			// Extract property accesses
			IEnumerable<string> origProps = Regex.Matches(origBody, @"\.(\w+)(?:\(|[^(])").Select(m => m.Groups[1].Value).Distinct();
			IEnumerable<string> editProps = Regex.Matches(editBody, @"\.(\w+)(?:\(|[^(])").Select(m => m.Groups[1].Value).Distinct();

			// Match by co-occurrence patterns
			// If properties appear together in both versions, they're likely related

			return matches;
		}

		// Learn naming patterns from mappings
		private void LearnNamingPatterns(MappingResult result) {
			// Pattern: Single letter -> descriptive name
			foreach (KeyValuePair<string, string> mapping in result.ParameterMappings) {
				if (mapping.Key.Length == 1) {
					_symbolMap.AddPattern(new NamingPattern {
						Pattern     = "^[A-Z]$",
						Replacement = DetermineParameterType(mapping.Value),
						Description = "Single letter parameters",
						Example     = $"{mapping.Key} -> {mapping.Value}"
					});
				}
			}

			// Pattern: Numeric suffixes
			foreach (KeyValuePair<string, string> mapping in result.FunctionMappings) {
				if (Regex.IsMatch(mapping.Key, @"\d+$")) {
					string baseType = DetermineFunctionType(mapping.Value);
					_symbolMap.AddPattern(new NamingPattern {
						Pattern     = @"(\w+)\d+$",
						Replacement = baseType,
						Description = "Numeric suffixed functions",
						Example     = $"{mapping.Key} -> {mapping.Value}"
					});
				}
			}
		}

		private string DetermineParameterType(string paramName) {
			// Common parameter patterns
			if (paramName.Contains("props")) return "props";
			if (paramName.Contains("state")) return "state";
			if (paramName.Contains("event") || paramName.Contains("evt")) return "event";
			if (paramName.Contains("callback") || paramName.Contains("cb")) return "callback";
			if (paramName.Contains("options") || paramName.Contains("opts")) return "options";
			if (paramName.Contains("config")) return "config";
			if (paramName.Contains("data")) return "data";
			if (paramName.Contains("error") || paramName.Contains("err")) return "error";
			if (paramName.Contains("request") || paramName.Contains("req")) return "request";
			if (paramName.Contains("response") || paramName.Contains("res")) return "response";
			return "param";
		}

		private string DetermineFunctionType(string funcName) {
			// Common function patterns
			if (funcName.StartsWith("handle")) return "Handler";
			if (funcName.StartsWith("get")) return "Getter";
			if (funcName.StartsWith("set")) return "Setter";
			if (funcName.StartsWith("is") || funcName.StartsWith("has")) return "Checker";
			if (funcName.StartsWith("create") || funcName.StartsWith("make")) return "Factory";
			if (funcName.StartsWith("init")) return "Initializer";
			if (funcName.StartsWith("render")) return "Renderer";
			if (funcName.StartsWith("use")) return "Hook";
			if (funcName.Contains("Component")) return "Component";
			return "Function";
		}

		private string ApplyPatternBasedRenames(string code) {
			string result = code;

			foreach (NamingPattern pattern in _symbolMap.Patterns) {
				result = Regex.Replace(result, pattern.Pattern, m => {
					// Generate contextual name based on pattern
					if (pattern.Replacement.Contains("$1")) {
						return pattern.Replacement.Replace("$1", m.Groups[1].Value);
					}
					return pattern.Replacement + m.Value;
				});
			}

			return result;
		}

		private async Task SaveMappings() {
			string json = JsonSerializer.Serialize(_symbolMap, new JsonSerializerOptions {
				WriteIndented = true
			});

			Directory.CreateDirectory(Path.GetDirectoryName(_mapPath)!);
			await File.WriteAllTextAsync(_mapPath, json);

			Logger.Info($"Saved {_symbolMap.Mappings.Count} mappings to {_mapPath}");
		}

		private void LoadMappings() {
			if (!File.Exists(_mapPath)) return;

			try {
				string     json   = File.ReadAllText(_mapPath);
				SymbolMap? loaded = JsonSerializer.Deserialize<SymbolMap>(json);
				if (loaded != null) {
					_symbolMap.Mappings = loaded.Mappings;
					_symbolMap.Patterns = loaded.Patterns;
					Logger.Info($"Loaded {_symbolMap.Mappings.Count} mappings from {_mapPath}");
				}
			} catch (Exception ex) {
				Logger.Warn($"Failed to load mappings: {ex.Message}");
			}
		}
	}

// Data models
	public class SymbolMap {
		public Dictionary<string, SymbolMapping> Mappings { get; set; } = new Dictionary<string, SymbolMapping>();
		public List<NamingPattern>               Patterns { get; set; } = new List<NamingPattern>();

		public void Add(string original, string mapped, SymbolType type) {
			Mappings[original] = new SymbolMapping {
				Original    = original,
				Mapped      = mapped,
				Type        = type,
				LastUpdated = DateTime.UtcNow
			};
		}

		public void AddPattern(NamingPattern pattern) {
			// Don't add duplicates
			if (!Patterns.Any(p => p.Pattern == pattern.Pattern)) {
				Patterns.Add(pattern);
			}
		}

		public IEnumerable<SymbolMapping> GetAllMappings() => Mappings.Values;
	}

	public class SymbolMapping {
		public string     Original    { get; set; } = "";
		public string     Mapped      { get; set; } = "";
		public SymbolType Type        { get; set; }
		public DateTime   LastUpdated { get; set; }
	}

	public enum SymbolType {
		Function,
		Class,
		Variable,
		Parameter,
		Property,
		Method
	}

	public class NamingPattern {
		public string Pattern     { get; set; } = "";
		public string Replacement { get; set; } = "";
		public string Description { get; set; } = "";
		public string Example     { get; set; } = "";
	}

	public class CodeStructure {
		public string       Type       { get; set; } = "";
		public string       Name       { get; set; } = "";
		public List<string> Parameters { get; set; } = new List<string>();
		public string       Body       { get; set; } = "";
		public string?      Extends    { get; set; }
		public string       FullMatch  { get; set; } = "";
	}

	public class StructureMatch {
		public CodeStructure Original   { get; set; } = null!;
		public CodeStructure Edited     { get; set; } = null!;
		public double        Confidence { get; set; }
	}

	public class VariableContext {
		public string       Name           { get; set; } = "";
		public string       AssignedFrom   { get; set; } = "";
		public List<string> MethodsCalled  { get; set; } = new List<string>();
		public bool         UsedAsArgument { get; set; }
	}

	public class MappingResult {
		public Dictionary<string, string> FunctionMappings  { get; set; } = new Dictionary<string, string>();
		public Dictionary<string, string> ClassMappings     { get; set; } = new Dictionary<string, string>();
		public Dictionary<string, string> VariableMappings  { get; set; } = new Dictionary<string, string>();
		public Dictionary<string, string> ParameterMappings { get; set; } = new Dictionary<string, string>();
		public Dictionary<string, string> PropertyMappings  { get; set; } = new Dictionary<string, string>();

		public int TotalMappings =>
			FunctionMappings.Count +
			ClassMappings.Count +
			VariableMappings.Count +
			ParameterMappings.Count +
			PropertyMappings.Count;
	}
}