using System.Text;
using System.Text.RegularExpressions;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// Analyzes webpack bundles to identify module boundaries and structure.
/// Black hat deobfuscation for cleanroom implementation.
/// </summary>
public static class WebpackBundleAnalyzer
{
    /// <summary>
    /// Analyze a webpack bundle and extract module structure.
    /// </summary>
    public static async Task<BundleAnalysis> AnalyzeBundleAsync(string bundlePath)
    {
        using var timer = Logger.TimeOperation("Analyze webpack bundle");
        
        var content = await File.ReadAllTextAsync(bundlePath);
        var analysis = new BundleAnalysis
        {
            FilePath = bundlePath,
            FileSize = new FileInfo(bundlePath).Length,
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
        
        return analysis;
    }
    
    private static string DetectBundleType(string content)
    {
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
    
    private static List<ModuleInfo> ExtractModules(string content)
    {
        var modules = new List<ModuleInfo>();
        
        // Pattern 1: CommonJS style - var X = U((module) => { ... })
        var cjsPattern = @"var\s+(\w+)\s*=\s*U\(\((\w+)\)\s*=>\s*\{";
        var cjsMatches = Regex.Matches(content, cjsPattern);
        
        foreach (Match match in cjsMatches)
        {
            modules.Add(new ModuleInfo
            {
                Id = match.Groups[1].Value,
                Type = "CommonJS",
                ExportName = match.Groups[2].Value
            });
        }
        
        // Pattern 2: React components - function ComponentName(props)
        var componentPattern = @"function\s+([A-Z]\w+)\s*\(\s*(?:A|props|B)\s*\)";
        var componentMatches = Regex.Matches(content, componentPattern);
        
        foreach (Match match in componentMatches)
        {
            var name = match.Groups[1].Value;
            if (!modules.Any(m => m.Id == name))
            {
                modules.Add(new ModuleInfo
                {
                    Id = name,
                    Type = "React Component",
                    ExportName = name
                });
            }
        }
        
        // Pattern 3: Class definitions - class X { or class X extends
        var classPattern = @"class\s+(\w+)\s*(?:\{|extends)";
        var classMatches = Regex.Matches(content, classPattern);
        
        foreach (Match match in classMatches)
        {
            modules.Add(new ModuleInfo
            {
                Id = match.Groups[1].Value,
                Type = "Class",
                ExportName = match.Groups[1].Value
            });
        }
        
        return modules;
    }
    
    private static List<string> FindReactComponents(string content)
    {
        var components = new HashSet<string>();
        
        // Look for JSX patterns
        var jsxPattern = @"createElement\(([A-Z]\w+)";
        var matches = Regex.Matches(content, jsxPattern);
        
        foreach (Match match in matches)
        {
            components.Add(match.Groups[1].Value);
        }
        
        // Look for useState, useEffect patterns
        var hookPattern = @"(useState|useEffect|useCallback|useMemo|useRef)\(";
        if (Regex.IsMatch(content, hookPattern))
        {
            Logger.Info("Found React hooks usage");
        }
        
        return components.ToList();
    }
    
    private static Dictionary<string, List<string>> FindToolImplementations(string content)
    {
        var tools = new Dictionary<string, List<string>>();
        
        // Known tool names from sdk-tools.d.ts
        var toolNames = new[]
        {
            "FileRead", "FileWrite", "FileEdit", "FileMultiEdit",
            "Bash", "BashOutput", "KillShell",
            "Grep", "Glob", 
            "WebSearch", "WebFetch",
            "TodoWrite", "Agent",
            "NotebookEdit", "ExitPlanMode"
        };
        
        foreach (var tool in toolNames)
        {
            var pattern = $@"{tool}[\""\']?\s*[:=]";
            var matches = Regex.Matches(content, pattern);
            
            if (matches.Count > 0)
            {
                var contexts = new List<string>();
                foreach (Match match in matches.Take(3))
                {
                    var start = Math.Max(0, match.Index - 50);
                    var length = Math.Min(200, content.Length - start);
                    contexts.Add(content.Substring(start, length));
                }
                tools[tool] = contexts;
            }
        }
        
        return tools;
    }
    
    private static WasmIntegration FindWasmIntegration(string content)
    {
        var wasm = new WasmIntegration();
        
        // Emscripten patterns
        if (content.Contains("Module[\"_malloc\"]") || content.Contains("Module._malloc"))
        {
            wasm.UsesEmscripten = true;
            wasm.EmscriptenFunctions.Add("malloc");
        }
        
        // WebAssembly instantiation
        var wasmInstantiate = @"WebAssembly\.instantiate";
        if (Regex.IsMatch(content, wasmInstantiate))
        {
            wasm.HasDirectWasmCalls = true;
        }
        
        // Tree-sitter WASM functions
        var treeSitterPattern = @"_tree_sitter_\w+";
        var tsMatches = Regex.Matches(content, treeSitterPattern);
        foreach (Match match in tsMatches)
        {
            wasm.TreeSitterFunctions.Add(match.Value);
        }
        
        // Yoga layout WASM
        if (content.Contains("YGNodeCalculateLayout") || content.Contains("yoga"))
        {
            wasm.UsesYogaLayout = true;
        }
        
        return wasm;
    }
    
    private static Dictionary<string, string> BuildSymbolMap(string content)
    {
        var map = new Dictionary<string, string>();
        
        // Common minification patterns to likely originals
        var patterns = new Dictionary<string, string>
        {
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
        var functionPatterns = new Dictionary<string, string>
        {
            [@"[a-z]+0"] = "init/constructor",
            [@"[a-z]+1"] = "handler/callback", 
            [@"[a-z]+2"] = "helper/utility",
            [@"[a-z]+Q"] = "query/request",
            [@"[a-z]+B"] = "buffer/build",
            [@"[a-z]+A"] = "async/array"
        };
        
        // Extract and map symbols
        var symbolPattern = @"\b([a-z]{2,3}[0-9A-Z]{1,2})\b";
        var matches = Regex.Matches(content, symbolPattern);
        var symbolCounts = new Dictionary<string, int>();
        
        foreach (Match match in matches)
        {
            var symbol = match.Groups[1].Value;
            if (!symbolCounts.ContainsKey(symbol))
                symbolCounts[symbol] = 0;
            symbolCounts[symbol]++;
        }
        
        // Map frequently used symbols to likely purposes
        var sorted = symbolCounts.OrderByDescending(x => x.Value).Take(100);
        foreach (var kvp in sorted)
        {
            var symbol = kvp.Key;
            var count = kvp.Value;
            
            // High frequency suggests core functionality
            if (count > 1000)
                map[symbol] = "core_util";
            else if (count > 500)
                map[symbol] = "common_helper";
            else if (count > 100)
                map[symbol] = "module_func";
            
            // Check against patterns
            foreach (var pattern in functionPatterns)
            {
                if (Regex.IsMatch(symbol, pattern.Key))
                {
                    map[symbol] = pattern.Value;
                    break;
                }
            }
        }
        
        return map;
    }
    
    public class BundleAnalysis
    {
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public int TotalLines { get; set; }
        public string BundleType { get; set; } = "";
        public List<ModuleInfo> Modules { get; set; } = new();
        public List<string> ReactComponents { get; set; } = new();
        public Dictionary<string, List<string>> ToolImplementations { get; set; } = new();
        public WasmIntegration WasmIntegration { get; set; } = new();
        public Dictionary<string, string> SymbolMap { get; set; } = new();
        
        public string GenerateReport()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("WEBPACK BUNDLE ANALYSIS REPORT");
            sb.AppendLine("=" .PadRight(80, '='));
            sb.AppendLine($"File: {Path.GetFileName(FilePath)}");
            sb.AppendLine($"Size: {FileSize:N0} bytes");
            sb.AppendLine($"Lines: {TotalLines:N0}");
            sb.AppendLine($"Bundle Type: {BundleType}");
            sb.AppendLine();
            
            sb.AppendLine("MODULE STRUCTURE");
            sb.AppendLine("-".PadRight(80, '-'));
            var moduleTypes = Modules.GroupBy(m => m.Type);
            foreach (var type in moduleTypes)
            {
                sb.AppendLine($"{type.Key}: {type.Count()}");
                foreach (var module in type.Take(5))
                {
                    sb.AppendLine($"  - {module.Id}");
                }
                if (type.Count() > 5)
                    sb.AppendLine($"  ... and {type.Count() - 5} more");
            }
            sb.AppendLine();
            
            sb.AppendLine("REACT COMPONENTS DETECTED");
            sb.AppendLine("-".PadRight(80, '-'));
            foreach (var comp in ReactComponents.Take(10))
            {
                sb.AppendLine($"  - {comp}");
            }
            if (ReactComponents.Count > 10)
                sb.AppendLine($"  ... and {ReactComponents.Count - 10} more");
            sb.AppendLine();
            
            sb.AppendLine("TOOL IMPLEMENTATIONS FOUND");
            sb.AppendLine("-".PadRight(80, '-'));
            foreach (var tool in ToolImplementations)
            {
                sb.AppendLine($"{tool.Key}: {tool.Value.Count} references");
            }
            sb.AppendLine();
            
            sb.AppendLine("WASM INTEGRATION");
            sb.AppendLine("-".PadRight(80, '-'));
            sb.AppendLine($"Uses Emscripten: {WasmIntegration.UsesEmscripten}");
            sb.AppendLine($"Has Direct WASM Calls: {WasmIntegration.HasDirectWasmCalls}");
            sb.AppendLine($"Uses Yoga Layout: {WasmIntegration.UsesYogaLayout}");
            if (WasmIntegration.TreeSitterFunctions.Any())
            {
                sb.AppendLine($"Tree-sitter Functions: {WasmIntegration.TreeSitterFunctions.Count}");
                foreach (var func in WasmIntegration.TreeSitterFunctions.Take(5))
                {
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
    }
    
    public class ModuleInfo
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string ExportName { get; set; } = "";
    }
    
    public class WasmIntegration
    {
        public bool UsesEmscripten { get; set; }
        public bool HasDirectWasmCalls { get; set; }
        public bool UsesYogaLayout { get; set; }
        public List<string> EmscriptenFunctions { get; set; } = new();
        public List<string> TreeSitterFunctions { get; set; } = new();
    }
}