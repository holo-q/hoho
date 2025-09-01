using System.Text;
using System.Text.RegularExpressions;

namespace Hoho.Decomp {
	/// <summary>
	/// Improved symbol extraction that maps methods to their parent classes
	/// </summary>
	public static class ImprovedSymbolExtractor {
		/// <summary>
		/// Extract symbols with class-method relationships from JavaScript
		/// </summary>
		public static async Task<ExtractedSymbols> ExtractSymbolsAsync(string filePath) {
			ExtractedSymbols result  = new ExtractedSymbols();
			string           content = await File.ReadAllTextAsync(filePath);

			// Extract classes and their methods
			await ExtractClassesAsync(content, result);

			// Extract standalone functions
			ExtractStandaloneFunctions(content, result);

			// Extract exports
			ExtractExports(content, result);

			// Calculate totals
			result.TotalClasses = result.Classes.Count;
			result.TotalFunctions = result.StandaloneFunctions.Count +
			                        result.Classes.Values.Sum(c => c.Methods.Count + c.StaticMethods.Count);

			return result;
		}

		private static async Task ExtractClassesAsync(string content, ExtractedSymbols result) {
			// Pattern for class definitions
			string classPattern = @"class\s+(\w+)(?:\s+extends\s+(\w+))?\s*\{";
			Regex  classRegex   = new Regex(classPattern);

			MatchCollection matches = classRegex.Matches(content);

			foreach (Match match in matches) {
				string  className    = match.Groups[1].Value;
				string? extendsClass = match.Groups[2].Success ? match.Groups[2].Value : null;

				ClassSymbol classSymbol = new ClassSymbol {
					Name    = className,
					Extends = extendsClass,
					Line    = GetLineNumber(content, match.Index)
				};

				// Extract class body
				string classBody = ExtractClassBody(content, match.Index);

				if (!string.IsNullOrEmpty(classBody)) {
					// Extract methods
					ExtractClassMethods(classBody, classSymbol);

					// Extract properties
					ExtractClassProperties(classBody, classSymbol);
				}

				result.Classes[className] = classSymbol;
			}

			// Also look for prototype-based classes
			ExtractPrototypeMethods(content, result);
		}

		private static string ExtractClassBody(string content, int startIndex) {
			int  depth     = 0;
			bool inClass   = false;
			int  bodyStart = -1;
			int  bodyEnd   = -1;

			for (int i = startIndex; i < content.Length; i++) {
				if (content[i] == '{') {
					if (!inClass) {
						inClass   = true;
						bodyStart = i + 1;
					}
					depth++;
				} else if (content[i] == '}') {
					depth--;
					if (depth == 0 && inClass) {
						bodyEnd = i;
						break;
					}
				}
			}

			if (bodyStart > 0 && bodyEnd > bodyStart) {
				return content.Substring(bodyStart, bodyEnd - bodyStart);
			}

			return "";
		}

		private static void ExtractClassMethods(string classBody, ClassSymbol classSymbol) {
			// Regular methods
			string methodPattern = @"(?:async\s+)?(\w+)\s*\([^)]*\)\s*\{";
			Regex  methodRegex   = new Regex(methodPattern);

			foreach (Match match in methodRegex.Matches(classBody)) {
				string methodName = match.Groups[1].Value;

				// Skip constructor
				if (methodName != "constructor") {
					classSymbol.Methods.Add(methodName);
				}
			}

			// Static methods
			string staticPattern = @"static\s+(?:async\s+)?(\w+)\s*\([^)]*\)\s*\{";
			Regex  staticRegex   = new Regex(staticPattern);

			foreach (Match match in staticRegex.Matches(classBody)) {
				string methodName = match.Groups[1].Value;
				classSymbol.StaticMethods.Add(methodName);

				// Remove from regular methods if it was added there
				classSymbol.Methods.Remove(methodName);
			}

			// Arrow function properties
			string arrowPattern = @"(\w+)\s*=\s*(?:async\s*)?\([^)]*\)\s*=>";
			Regex  arrowRegex   = new Regex(arrowPattern);

			foreach (Match match in arrowRegex.Matches(classBody)) {
				string methodName = match.Groups[1].Value;
				classSymbol.Methods.Add(methodName);
			}
		}

		private static void ExtractClassProperties(string classBody, ClassSymbol classSymbol) {
			// Look for property assignments
			string propPattern = @"this\.(\w+)\s*=";
			Regex  propRegex   = new Regex(propPattern);

			HashSet<string> props = new HashSet<string>();
			foreach (Match match in propRegex.Matches(classBody)) {
				props.Add(match.Groups[1].Value);
			}

			classSymbol.Properties = props.ToList();
		}

		private static void ExtractPrototypeMethods(string content, ExtractedSymbols result) {
			// Pattern for prototype method assignments
			string protoPattern = @"(\w+)\.prototype\.(\w+)\s*=\s*function";
			Regex  protoRegex   = new Regex(protoPattern);

			foreach (Match match in protoRegex.Matches(content)) {
				string className  = match.Groups[1].Value;
				string methodName = match.Groups[2].Value;

				if (!result.Classes.ContainsKey(className)) {
					result.Classes[className] = new ClassSymbol { Name = className };
				}

				result.Classes[className].Methods.Add(methodName);
			}

			// Also check for Object.defineProperty style
			string definePattern = @"Object\.defineProperty\((\w+)\.prototype,\s*['""](\w+)['""]";
			Regex  defineRegex   = new Regex(definePattern);

			foreach (Match match in defineRegex.Matches(content)) {
				string className    = match.Groups[1].Value;
				string propertyName = match.Groups[2].Value;

				if (!result.Classes.ContainsKey(className)) {
					result.Classes[className] = new ClassSymbol { Name = className };
				}

				result.Classes[className].Properties.Add(propertyName);
			}
		}

		private static void ExtractStandaloneFunctions(string content, ExtractedSymbols result) {
			// Pattern for standalone functions
			string funcPattern = @"(?:^|\n)\s*(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\(";
			Regex  funcRegex   = new Regex(funcPattern, RegexOptions.Multiline);

			HashSet<string> functions = new HashSet<string>();
			foreach (Match match in funcRegex.Matches(content)) {
				string funcName = match.Groups[1].Value;

				// Check if this function is inside a class (rough check)
				if (!IsInsideClass(content, match.Index)) {
					functions.Add(funcName);
				}
			}

			// Also get arrow functions at top level
			string arrowPattern = @"(?:^|\n)\s*(?:export\s+)?(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s*)?\([^)]*\)\s*=>";
			Regex  arrowRegex   = new Regex(arrowPattern, RegexOptions.Multiline);

			foreach (Match match in arrowRegex.Matches(content)) {
				string funcName = match.Groups[1].Value;
				if (!IsInsideClass(content, match.Index)) {
					functions.Add(funcName);
				}
			}

			result.StandaloneFunctions = functions.ToList();
		}

		private static void ExtractExports(string content, ExtractedSymbols result) {
			HashSet<string> exports = new HashSet<string>();

			// ES6 exports
			string es6Pattern = @"export\s*\{([^}]+)\}";
			Regex  es6Regex   = new Regex(es6Pattern);

			foreach (Match match in es6Regex.Matches(content)) {
				string   exportList = match.Groups[1].Value;
				string[] items      = exportList.Split(',');
				foreach (string item in items) {
					string[] parts      = item.Split(new[] { "as" }, StringSplitOptions.None);
					string   exportName = parts[0].Trim();
					if (!string.IsNullOrWhiteSpace(exportName)) {
						exports.Add(exportName);
					}
				}
			}

			// CommonJS exports
			string cjsPattern = @"(?:module\.)?exports\.(\w+)";
			Regex  cjsRegex   = new Regex(cjsPattern);

			foreach (Match match in cjsRegex.Matches(content)) {
				exports.Add(match.Groups[1].Value);
			}

			result.Exports = exports.ToList();
		}

		private static bool IsInsideClass(string content, int position) {
			// Simple heuristic: count braces before this position
			string beforeContent = content.Substring(0, position);

			// Find last class declaration before this position
			int lastClassIndex = beforeContent.LastIndexOf("class ", StringComparison.Ordinal);
			if (lastClassIndex == -1) return false;

			// Count braces between class and current position
			string relevantContent = beforeContent.Substring(lastClassIndex);
			int    openBraces      = relevantContent.Count(c => c == '{');
			int    closeBraces     = relevantContent.Count(c => c == '}');

			return openBraces > closeBraces;
		}

		private static int GetLineNumber(string content, int index) {
			return content.Substring(0, index).Count(c => c == '\n') + 1;
		}

		/// <summary>
		/// Generate a compact report of extracted symbols
		/// </summary>
		public static string GenerateCompactReport(ExtractedSymbols symbols) {
			StringBuilder sb = new StringBuilder();

			sb.AppendLine("# Symbol Map");
			sb.AppendLine($"Total Classes: {symbols.TotalClasses}");
			sb.AppendLine($"Total Functions: {symbols.TotalFunctions}");
			sb.AppendLine($"Standalone Functions: {symbols.StandaloneFunctions.Count}");
			sb.AppendLine();

			if (symbols.Classes.Any()) {
				sb.AppendLine("## Classes");
				foreach (ClassSymbol cls in symbols.Classes.Values.OrderBy(c => c.Name)) {
					sb.Append($"**{cls.Name}**");
					if (!string.IsNullOrEmpty(cls.Extends)) {
						sb.Append($" extends {cls.Extends}");
					}
					sb.AppendLine(":");

					if (cls.Methods.Any()) {
						string methodList = string.Join(", ", cls.Methods.OrderBy(m => m));
						sb.AppendLine($"  Methods({cls.Methods.Count}): {methodList}");
					}

					if (cls.StaticMethods.Any()) {
						string staticList = string.Join(", ", cls.StaticMethods.OrderBy(m => m).Select(m => $"static {m}"));
						sb.AppendLine($"  Static({cls.StaticMethods.Count}): {staticList}");
					}

					if (cls.Properties.Any()) {
						string propList = string.Join(", ", cls.Properties.OrderBy(p => p));
						sb.AppendLine($"  Properties({cls.Properties.Count}): {propList}");
					}

					sb.AppendLine();
				}
			}

			if (symbols.StandaloneFunctions.Any()) {
				sb.AppendLine("## Standalone Functions");
				string funcList = string.Join(", ", symbols.StandaloneFunctions.OrderBy(f => f));
				sb.AppendLine($"Functions({symbols.StandaloneFunctions.Count}): {funcList}");
				sb.AppendLine();
			}

			if (symbols.Exports.Any()) {
				sb.AppendLine("## Exports");
				string exportList = string.Join(", ", symbols.Exports.OrderBy(e => e));
				sb.AppendLine($"Exports({symbols.Exports.Count}): {exportList}");
				sb.AppendLine();
			}

			return sb.ToString();
		}

		public class ClassSymbol {
			public string       Name          { get; set; } = "";
			public List<string> Methods       { get; set; } = new List<string>();
			public List<string> Properties    { get; set; } = new List<string>();
			public List<string> StaticMethods { get; set; } = new List<string>();
			public string?      Extends       { get; set; }
			public int          Line          { get; set; }
		}

		public class ExtractedSymbols {
			public Dictionary<string, ClassSymbol> Classes             { get; set; } = new Dictionary<string, ClassSymbol>();
			public List<string>                    StandaloneFunctions { get; set; } = new List<string>();
			public List<string>                    GlobalVariables     { get; set; } = new List<string>();
			public List<string>                    Exports             { get; set; } = new List<string>();
			public int                             TotalFunctions      { get; set; }
			public int                             TotalClasses        { get; set; }
		}
	}
}