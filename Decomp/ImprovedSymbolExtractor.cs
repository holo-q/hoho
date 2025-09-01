using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// Improved symbol extraction that maps methods to their parent classes
/// </summary>
public static class ImprovedSymbolExtractor
{
    public class ClassSymbol
    {
        public string Name { get; set; } = "";
        public List<string> Methods { get; set; } = new();
        public List<string> Properties { get; set; } = new();
        public List<string> StaticMethods { get; set; } = new();
        public string? Extends { get; set; }
        public int Line { get; set; }
    }
    
    public class ExtractedSymbols
    {
        public Dictionary<string, ClassSymbol> Classes { get; set; } = new();
        public List<string> StandaloneFunctions { get; set; } = new();
        public List<string> GlobalVariables { get; set; } = new();
        public List<string> Exports { get; set; } = new();
        public int TotalFunctions { get; set; }
        public int TotalClasses { get; set; }
    }
    
    /// <summary>
    /// Extract symbols with class-method relationships from JavaScript
    /// </summary>
    public static async Task<ExtractedSymbols> ExtractSymbolsAsync(string filePath)
    {
        var result = new ExtractedSymbols();
        var content = await File.ReadAllTextAsync(filePath);
        
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
    
    private static async Task ExtractClassesAsync(string content, ExtractedSymbols result)
    {
        // Pattern for class definitions
        var classPattern = @"class\s+(\w+)(?:\s+extends\s+(\w+))?\s*\{";
        var classRegex = new Regex(classPattern);
        
        var matches = classRegex.Matches(content);
        
        foreach (Match match in matches)
        {
            var className = match.Groups[1].Value;
            var extendsClass = match.Groups[2].Success ? match.Groups[2].Value : null;
            
            var classSymbol = new ClassSymbol
            {
                Name = className,
                Extends = extendsClass,
                Line = GetLineNumber(content, match.Index)
            };
            
            // Extract class body
            var classBody = ExtractClassBody(content, match.Index);
            
            if (!string.IsNullOrEmpty(classBody))
            {
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
    
    private static string ExtractClassBody(string content, int startIndex)
    {
        var depth = 0;
        var inClass = false;
        var bodyStart = -1;
        var bodyEnd = -1;
        
        for (int i = startIndex; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                if (!inClass)
                {
                    inClass = true;
                    bodyStart = i + 1;
                }
                depth++;
            }
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0 && inClass)
                {
                    bodyEnd = i;
                    break;
                }
            }
        }
        
        if (bodyStart > 0 && bodyEnd > bodyStart)
        {
            return content.Substring(bodyStart, bodyEnd - bodyStart);
        }
        
        return "";
    }
    
    private static void ExtractClassMethods(string classBody, ClassSymbol classSymbol)
    {
        // Regular methods
        var methodPattern = @"(?:async\s+)?(\w+)\s*\([^)]*\)\s*\{";
        var methodRegex = new Regex(methodPattern);
        
        foreach (Match match in methodRegex.Matches(classBody))
        {
            var methodName = match.Groups[1].Value;
            
            // Skip constructor
            if (methodName != "constructor")
            {
                classSymbol.Methods.Add(methodName);
            }
        }
        
        // Static methods
        var staticPattern = @"static\s+(?:async\s+)?(\w+)\s*\([^)]*\)\s*\{";
        var staticRegex = new Regex(staticPattern);
        
        foreach (Match match in staticRegex.Matches(classBody))
        {
            var methodName = match.Groups[1].Value;
            classSymbol.StaticMethods.Add(methodName);
            
            // Remove from regular methods if it was added there
            classSymbol.Methods.Remove(methodName);
        }
        
        // Arrow function properties
        var arrowPattern = @"(\w+)\s*=\s*(?:async\s*)?\([^)]*\)\s*=>";
        var arrowRegex = new Regex(arrowPattern);
        
        foreach (Match match in arrowRegex.Matches(classBody))
        {
            var methodName = match.Groups[1].Value;
            classSymbol.Methods.Add(methodName);
        }
    }
    
    private static void ExtractClassProperties(string classBody, ClassSymbol classSymbol)
    {
        // Look for property assignments
        var propPattern = @"this\.(\w+)\s*=";
        var propRegex = new Regex(propPattern);
        
        var props = new HashSet<string>();
        foreach (Match match in propRegex.Matches(classBody))
        {
            props.Add(match.Groups[1].Value);
        }
        
        classSymbol.Properties = props.ToList();
    }
    
    private static void ExtractPrototypeMethods(string content, ExtractedSymbols result)
    {
        // Pattern for prototype method assignments
        var protoPattern = @"(\w+)\.prototype\.(\w+)\s*=\s*function";
        var protoRegex = new Regex(protoPattern);
        
        foreach (Match match in protoRegex.Matches(content))
        {
            var className = match.Groups[1].Value;
            var methodName = match.Groups[2].Value;
            
            if (!result.Classes.ContainsKey(className))
            {
                result.Classes[className] = new ClassSymbol { Name = className };
            }
            
            result.Classes[className].Methods.Add(methodName);
        }
        
        // Also check for Object.defineProperty style
        var definePattern = @"Object\.defineProperty\((\w+)\.prototype,\s*['""](\w+)['""]";
        var defineRegex = new Regex(definePattern);
        
        foreach (Match match in defineRegex.Matches(content))
        {
            var className = match.Groups[1].Value;
            var propertyName = match.Groups[2].Value;
            
            if (!result.Classes.ContainsKey(className))
            {
                result.Classes[className] = new ClassSymbol { Name = className };
            }
            
            result.Classes[className].Properties.Add(propertyName);
        }
    }
    
    private static void ExtractStandaloneFunctions(string content, ExtractedSymbols result)
    {
        // Pattern for standalone functions
        var funcPattern = @"(?:^|\n)\s*(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\(";
        var funcRegex = new Regex(funcPattern, RegexOptions.Multiline);
        
        var functions = new HashSet<string>();
        foreach (Match match in funcRegex.Matches(content))
        {
            var funcName = match.Groups[1].Value;
            
            // Check if this function is inside a class (rough check)
            if (!IsInsideClass(content, match.Index))
            {
                functions.Add(funcName);
            }
        }
        
        // Also get arrow functions at top level
        var arrowPattern = @"(?:^|\n)\s*(?:export\s+)?(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s*)?\([^)]*\)\s*=>";
        var arrowRegex = new Regex(arrowPattern, RegexOptions.Multiline);
        
        foreach (Match match in arrowRegex.Matches(content))
        {
            var funcName = match.Groups[1].Value;
            if (!IsInsideClass(content, match.Index))
            {
                functions.Add(funcName);
            }
        }
        
        result.StandaloneFunctions = functions.ToList();
    }
    
    private static void ExtractExports(string content, ExtractedSymbols result)
    {
        var exports = new HashSet<string>();
        
        // ES6 exports
        var es6Pattern = @"export\s*\{([^}]+)\}";
        var es6Regex = new Regex(es6Pattern);
        
        foreach (Match match in es6Regex.Matches(content))
        {
            var exportList = match.Groups[1].Value;
            var items = exportList.Split(',');
            foreach (var item in items)
            {
                var parts = item.Split(new[] { "as" }, StringSplitOptions.None);
                var exportName = parts[0].Trim();
                if (!string.IsNullOrWhiteSpace(exportName))
                {
                    exports.Add(exportName);
                }
            }
        }
        
        // CommonJS exports
        var cjsPattern = @"(?:module\.)?exports\.(\w+)";
        var cjsRegex = new Regex(cjsPattern);
        
        foreach (Match match in cjsRegex.Matches(content))
        {
            exports.Add(match.Groups[1].Value);
        }
        
        result.Exports = exports.ToList();
    }
    
    private static bool IsInsideClass(string content, int position)
    {
        // Simple heuristic: count braces before this position
        var beforeContent = content.Substring(0, position);
        
        // Find last class declaration before this position
        var lastClassIndex = beforeContent.LastIndexOf("class ", StringComparison.Ordinal);
        if (lastClassIndex == -1) return false;
        
        // Count braces between class and current position
        var relevantContent = beforeContent.Substring(lastClassIndex);
        var openBraces = relevantContent.Count(c => c == '{');
        var closeBraces = relevantContent.Count(c => c == '}');
        
        return openBraces > closeBraces;
    }
    
    private static int GetLineNumber(string content, int index)
    {
        return content.Substring(0, index).Count(c => c == '\n') + 1;
    }
    
    /// <summary>
    /// Generate a compact report of extracted symbols
    /// </summary>
    public static string GenerateCompactReport(ExtractedSymbols symbols)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Symbol Map");
        sb.AppendLine($"Total Classes: {symbols.TotalClasses}");
        sb.AppendLine($"Total Functions: {symbols.TotalFunctions}");
        sb.AppendLine($"Standalone Functions: {symbols.StandaloneFunctions.Count}");
        sb.AppendLine();
        
        if (symbols.Classes.Any())
        {
            sb.AppendLine("## Classes");
            foreach (var cls in symbols.Classes.Values.OrderBy(c => c.Name))
            {
                sb.Append($"**{cls.Name}**");
                if (!string.IsNullOrEmpty(cls.Extends))
                {
                    sb.Append($" extends {cls.Extends}");
                }
                sb.AppendLine(":");
                
                if (cls.Methods.Any())
                {
                    var methodList = string.Join(", ", cls.Methods.OrderBy(m => m));
                    sb.AppendLine($"  Methods({cls.Methods.Count}): {methodList}");
                }
                
                if (cls.StaticMethods.Any())
                {
                    var staticList = string.Join(", ", cls.StaticMethods.OrderBy(m => m).Select(m => $"static {m}"));
                    sb.AppendLine($"  Static({cls.StaticMethods.Count}): {staticList}");
                }
                
                if (cls.Properties.Any())
                {
                    var propList = string.Join(", ", cls.Properties.OrderBy(p => p));
                    sb.AppendLine($"  Properties({cls.Properties.Count}): {propList}");
                }
                
                sb.AppendLine();
            }
        }
        
        if (symbols.StandaloneFunctions.Any())
        {
            sb.AppendLine("## Standalone Functions");
            var funcList = string.Join(", ", symbols.StandaloneFunctions.OrderBy(f => f));
            sb.AppendLine($"Functions({symbols.StandaloneFunctions.Count}): {funcList}");
            sb.AppendLine();
        }
        
        if (symbols.Exports.Any())
        {
            sb.AppendLine("## Exports");
            var exportList = string.Join(", ", symbols.Exports.OrderBy(e => e));
            sb.AppendLine($"Exports({symbols.Exports.Count}): {exportList}");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}