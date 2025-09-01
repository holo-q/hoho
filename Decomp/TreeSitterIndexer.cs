using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Hoho.Core;
using NetFabric.Hyperlinq;

namespace Hoho.Decomp;

/// <summary>
/// Tree-sitter based code indexer for extracting symbols from minified code.
/// Creates a navigable symbol map without the bulk.
/// </summary>
public static class TreeSitterIndexer
{
    /// <summary>
    /// Generate a tree-sitter index of the codebase.
    /// </summary>
    public static async Task<string> GenerateIndexAsync(string agentDir)
    {
        using var timer = Logger.TimeOperation($"Generate tree-sitter index");
        
        var packageDir = Path.Combine(agentDir, "package");
        if (!Directory.Exists(packageDir))
        {
            Logger.Error($"Package directory not found: {packageDir}");
            return string.Empty;
        }
        
        var indexBuilder = new StringBuilder();
        indexBuilder.AppendLine("SYMBOL MAP");
        indexBuilder.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        indexBuilder.AppendLine($"Package: {Path.GetFileName(packageDir)}");
        indexBuilder.AppendLine(new string('─', 80));
        indexBuilder.AppendLine();
        
        // Process TypeScript definitions first (most valuable)
        var dtsFiles = Directory.GetFiles(packageDir, "*.d.ts");
        foreach (var file in dtsFiles)
        {
            await IndexTypeScriptFileAsync(file, indexBuilder);
        }
        
        // Process JavaScript modules
        var jsFiles = Directory.GetFiles(packageDir, "*.mjs")
            .Concat(Directory.GetFiles(packageDir, "*.js"));
        
        foreach (var file in jsFiles)
        {
            await IndexJavaScriptFileAsync(file, indexBuilder);
        }
        
        return indexBuilder.ToString();
    }
    
    /// <summary>
    /// Index a TypeScript definition file using tree-sitter.
    /// </summary>
    private static async Task IndexTypeScriptFileAsync(string filePath, StringBuilder output)
    {
        var fileName = Path.GetFileName(filePath);
        output.AppendLine($"[{fileName}]");
        
        try
        {
            // Use tree-sitter to parse TypeScript
            var symbols = await ExtractSymbolsWithTreeSitterAsync(filePath, "typescript");
            
            if (symbols.Any())
            {
                // Group symbols by type
                var grouped = symbols.GroupBy(s => s.Type)
                    .OrderBy(g => GetTypeOrder(g.Key));
                
                foreach (var group in grouped)
                {
                    var symbolNames = group.OrderBy(s => s.Name).Select(s => s.Name).ToList();
                    output.Append($"  {group.Key}s({group.Count()}): ");
                    
                    // Show ALL symbols, wrap at 100 chars
                    var currentLine = new StringBuilder();
                    for (int i = 0; i < symbolNames.Count; i++)
                    {
                        var name = symbolNames[i];
                        var separator = i < symbolNames.Count - 1 ? ", " : "";
                        
                        if (currentLine.Length + name.Length + separator.Length > 100)
                        {
                            output.AppendLine(currentLine.ToString());
                            output.Append("    "); // Indent continuation
                            currentLine.Clear();
                        }
                        
                        currentLine.Append(name + separator);
                    }
                    
                    if (currentLine.Length > 0)
                    {
                        output.AppendLine(currentLine.ToString());
                    }
                }
            }
            else
            {
                output.AppendLine("  (no symbols detected)");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to index {fileName}: {ex.Message}");
            output.AppendLine($"  ERROR: {ex.Message}");
        }
        output.AppendLine();
    }
    
    /// <summary>
    /// Index a JavaScript file using tree-sitter.
    /// </summary>
    private static async Task IndexJavaScriptFileAsync(string filePath, StringBuilder output)
    {
        var fileName = Path.GetFileName(filePath);
        var fileSize = new FileInfo(filePath).Length;
        
        output.AppendLine($"[{fileName}] {FormatFileSize(fileSize)}");
        
        // Always try to extract detailed symbols for JS files
        var symbols = await ExtractSymbolsWithTreeSitterAsync(filePath, "javascript");
        
        // If tree-sitter fails or finds nothing, try minified extraction
        if (!symbols.Any() && fileSize > 100_000)
        {
            var minifiedSymbols = await ExtractMinifiedSymbolsAsync(filePath);
            
            if (minifiedSymbols.Any())
            {
                output.AppendLine($"  Exports({minifiedSymbols.Count}):");
                
                // Output ALL symbols in wrapped lines
                var currentLine = new StringBuilder("    ");
                for (int i = 0; i < minifiedSymbols.Count; i++)
                {
                    var symbol = minifiedSymbols[i];
                    var separator = i < minifiedSymbols.Count - 1 ? ", " : "";
                    
                    if (currentLine.Length + symbol.Length + separator.Length > 100)
                    {
                        output.AppendLine(currentLine.ToString());
                        currentLine = new StringBuilder("    ");
                    }
                    
                    currentLine.Append(symbol + separator);
                }
                
                if (currentLine.Length > 4) // More than just indent
                {
                    output.AppendLine(currentLine.ToString());
                }
            }
            else
            {
                output.AppendLine("  (no symbols detected)");
            }
        }
        else if (symbols.Any())
        {
            // Group symbols by type
            var grouped = symbols.GroupBy(s => s.Type)
                .OrderBy(g => GetTypeOrder(g.Key));
            
            output.AppendLine($"  Total: {symbols.Count} symbols");
            
            foreach (var group in grouped)
            {
                var symbolList = group.OrderBy(s => s.Name).Select(s => s.Name).ToList();
                output.Append($"  {group.Key}s({group.Count()}): ");
                
                // Show ALL symbols, wrapped at 100 chars
                var currentLine = new StringBuilder();
                
                for (int i = 0; i < symbolList.Count; i++)
                {
                    var symbol = symbolList[i];
                    var separator = i < symbolList.Count - 1 ? ", " : "";
                    
                    if (currentLine.Length + symbol.Length + separator.Length > 100)
                    {
                        output.AppendLine(currentLine.ToString());
                        output.Append("    "); // Indent continuation
                        currentLine.Clear();
                    }
                    
                    currentLine.Append(symbol + separator);
                }
                
                if (currentLine.Length > 0)
                {
                    output.AppendLine(currentLine.ToString());
                }
            }
            
            // For cli.js and sdk.mjs, show ALL non-minified functions
            if ((fileName == "cli.js" || fileName == "sdk.mjs") && symbols.Any(s => s.Type == "Function"))
            {
                var keyFunctions = symbols
                    .Where(s => s.Type == "Function" && !s.Name.StartsWith("_") && s.Name.Length > 4 && !System.Text.RegularExpressions.Regex.IsMatch(s.Name, @"^[A-Z][0-9]+"))
                    .OrderBy(s => s.Name)
                    .Select(s => s.Name)
                    .ToList();
                
                if (keyFunctions.Any())
                {
                    output.Append("  Key: ");
                    
                    // Wrap key functions too
                    var currentLine = new StringBuilder();
                    for (int i = 0; i < keyFunctions.Count; i++)
                    {
                        var func = keyFunctions[i];
                        var separator = i < keyFunctions.Count - 1 ? ", " : "";
                        
                        if (currentLine.Length + func.Length + separator.Length > 100)
                        {
                            output.AppendLine(currentLine.ToString());
                            output.Append("    ");
                            currentLine.Clear();
                        }
                        
                        currentLine.Append(func + separator);
                    }
                    
                    if (currentLine.Length > 0)
                    {
                        output.AppendLine(currentLine.ToString());
                    }
                }
            }
        }
        else
        {
            output.AppendLine("  (no symbols detected)");
        }
        
        output.AppendLine();
    }
    
    /// <summary>
    /// Extract symbols using tree-sitter CLI.
    /// </summary>
    private static async Task<List<SymbolInfo>> ExtractSymbolsWithTreeSitterAsync(string filePath, string language)
    {
        var symbols = new List<SymbolInfo>();
        
        try
        {
            // Check if tree-sitter is available
            var treeSitterCheck = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "tree-sitter",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            treeSitterCheck.Start();
            await treeSitterCheck.WaitForExitAsync();
            
            if (treeSitterCheck.ExitCode != 0)
            {
                // Fallback to regex-based extraction
                return await ExtractSymbolsWithRegexAsync(filePath);
            }
            
            // Use tree-sitter parse
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "tree-sitter",
                    Arguments = $"parse \"{filePath}\" --quiet",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            // Parse tree-sitter output to extract symbols
            symbols = ParseTreeSitterOutput(output);
        }
        catch (Exception ex)
        {
            Logger.Debug($"Tree-sitter not available, using fallback: {ex.Message}");
            // Fallback to regex
            symbols = await ExtractSymbolsWithRegexAsync(filePath);
        }
        
        return symbols;
    }
    
    /// <summary>
    /// Fallback regex-based symbol extraction.
    /// </summary>
    private static async Task<List<SymbolInfo>> ExtractSymbolsWithRegexAsync(string filePath)
    {
        var symbols = new List<SymbolInfo>();
        var content = await File.ReadAllTextAsync(filePath);
        
        // TypeScript/JavaScript patterns
        var patterns = new[]
        {
            (@"export\s+(?:type|interface)\s+(\w+)", "Type"),
            (@"export\s+class\s+(\w+)", "Class"),
            (@"export\s+(?:async\s+)?function\s+(\w+)", "Function"),
            (@"export\s+const\s+(\w+)", "Constant"),
            (@"export\s+(?:let|var)\s+(\w+)", "Variable"),
            (@"(?:type|interface)\s+(\w+)\s*[={]", "Type"),
            (@"class\s+(\w+)\s*[{]", "Class"),
            (@"(?:async\s+)?function\s+(\w+)\s*\(", "Function"),
            (@"const\s+(\w+)\s*=\s*(?:async\s*)?\(", "Arrow Function"),
        };
        
        var lines = content.Split('\n');
        
        foreach (var (pattern, type) in patterns)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            for (int i = 0; i < lines.Length; i++)
            {
                var matches = regex.Matches(lines[i]);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        symbols.Add(new SymbolInfo
                        {
                            Name = match.Groups[1].Value,
                            Type = type,
                            Line = i + 1,
                            Signature = ExtractSignature(lines[i], match.Index)
                        });
                    }
                }
            }
        }
        
        // Group by name to get distinct symbols
        return symbols.AsValueEnumerable()
            .GroupBy(s => s.Name)
            .Select(g => g.First())
            .OrderBy(s => s.Line)
            .ToList();
    }
    
    /// <summary>
    /// Extract symbols from minified JavaScript.
    /// </summary>
    private static async Task<List<string>> ExtractMinifiedSymbolsAsync(string filePath)
    {
        var symbols = new HashSet<string>();
        var content = await File.ReadAllTextAsync(filePath);
        
        // Look for common patterns in minified code - expanded patterns
        var patterns = new[]
        {
            @"exports\.(\w+)",           // CommonJS exports
            @"export\s*{\s*([^}]+)\s*}", // ES6 exports
            @"\.prototype\.(\w+)",        // Prototype methods
            @"window\.(\w+)",            // Global assignments
            @"global\.(\w+)",            // Node globals
            @"define\([""'](\w+)[""']",  // AMD modules
            @"function\s+(\w{3,})\s*\(", // Named functions (3+ chars)
            @"class\s+(\w+)",            // Class definitions
            @"const\s+(\w{3,})\s*=",     // Const assignments (3+ chars)
            @"let\s+(\w{3,})\s*=",       // Let assignments (3+ chars)
            @"var\s+(\w{3,})\s*=",       // Var assignments (3+ chars)
        };
        
        foreach (var pattern in patterns)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            var matches = regex.Matches(content);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var names = match.Groups[1].Value.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s) && s.Length > 1);
                    
                    foreach (var name in names)
                    {
                        symbols.Add(name);
                    }
                }
            }
        }
        
        return symbols.OrderBy(s => s).ToList();
    }
    
    /// <summary>
    /// Parse tree-sitter output to extract symbols.
    /// </summary>
    private static List<SymbolInfo> ParseTreeSitterOutput(string output)
    {
        var symbols = new List<SymbolInfo>();
        
        // Parse S-expression output from tree-sitter
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            // Look for function, class, interface declarations
            if (line.Contains("function_declaration") || 
                line.Contains("class_declaration") ||
                line.Contains("interface_declaration") ||
                line.Contains("type_alias_declaration"))
            {
                // Extract symbol name from the line
                var match = System.Text.RegularExpressions.Regex.Match(line, @"name:\s*(\w+)");
                if (match.Success)
                {
                    var type = line.Contains("function") ? "Function" :
                               line.Contains("class") ? "Class" :
                               line.Contains("interface") ? "Interface" : "Type";
                    
                    symbols.Add(new SymbolInfo
                    {
                        Name = match.Groups[1].Value,
                        Type = type
                    });
                }
            }
        }
        
        return symbols;
    }
    
    private static string ExtractSignature(string line, int startIndex)
    {
        // Try to extract a meaningful signature
        var endIndex = line.IndexOf('{', startIndex);
        if (endIndex == -1) endIndex = line.IndexOf(';', startIndex);
        if (endIndex == -1) endIndex = line.Length;
        
        return line.Substring(startIndex, Math.Min(endIndex - startIndex, 100)).Trim();
    }
    
    private static int CountLines(string filePath)
    {
        return File.ReadLines(filePath).Count();
    }
    
    private class SymbolInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int Line { get; set; }
        public string? Signature { get; set; }
        public string? ParentClass { get; set; }  // Track which class this method belongs to
        public List<string> Methods { get; set; } = new();  // For classes, track their methods
    }
    
    private static int GetTypeOrder(string type)
    {
        return type switch
        {
            "Interface" => 1,
            "Type" => 2,
            "Class" => 3,
            "Function" => 4,
            "Arrow Function" => 5,
            "Constant" => 6,
            "Variable" => 7,
            _ => 99
        };
    }
    
    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
    
    private static string GetFileAnalysis(string fileName, List<SymbolInfo> symbols, long fileSize)
    {
        var functionCount = symbols.Count(s => s.Type == "Function" || s.Type == "Arrow Function");
        var classCount = symbols.Count(s => s.Type == "Class");
        
        return fileName switch
        {
            "cli.js" => $"Main CLI bundle with {functionCount} functions, {classCount} classes. Webpack bundle containing entire Claude Code application.",
            "sdk.mjs" => $"SDK module with {functionCount} functions, {classCount} classes. Core SDK for MCP integration and tool definitions.",
            _ when fileName.EndsWith(".d.ts") => $"TypeScript definitions with {symbols.Count} type declarations.",
            _ => $"{functionCount} functions, {classCount} classes"
        };
    }
    
    /// <summary>
    /// Generate a script that creates the symbol index.
    /// </summary>
    public static async Task GenerateIndexScriptAsync(string agentDir)
    {
        var scriptPath = Path.Combine(agentDir, "generate-symbol-index.sh");
        
        var script = @"#!/bin/bash

# Generate Symbol Index for Claude Code
# Uses tree-sitter or falls back to grep patterns

SCRIPT_DIR=""$(cd ""$(dirname ""${BASH_SOURCE[0]}"")""  && pwd)""
PACKAGE_DIR=""$SCRIPT_DIR/package""
OUTPUT_FILE=""$SCRIPT_DIR/symbol-index.md""

echo ""# Claude Code Symbol Index"" > ""$OUTPUT_FILE""
echo ""Generated: $(date -u +'%Y-%m-%d %H:%M:%S UTC')"" >> ""$OUTPUT_FILE""
echo """" >> ""$OUTPUT_FILE""

# Function to extract symbols with tree-sitter or grep
extract_symbols() {
    local file=""$1""
    local lang=""$2""
    
    if command -v tree-sitter >/dev/null 2>&1; then
        # Use tree-sitter for accurate parsing
        tree-sitter parse ""$file"" --quiet | \
            grep -E ""(function|class|interface|type)_declaration"" | \
            sed 's/.*name: \(\w\+\).*/\1/' | sort -u
    else
        # Fallback to grep patterns
        grep -oE ""(export\s+)?(class|function|interface|type|const|let|var)\s+\w+"" ""$file"" | \
            sed -E 's/.*(class|function|interface|type|const|let|var)\s+//' | \
            sort -u
    fi
}

# Process TypeScript definitions
for dts in ""$PACKAGE_DIR""/*.d.ts; do
    if [ -f ""$dts"" ]; then
        filename=$(basename ""$dts"")
        echo ""## 📄 $filename"" >> ""$OUTPUT_FILE""
        echo """" >> ""$OUTPUT_FILE""
        
        echo ""### Symbols"" >> ""$OUTPUT_FILE""
        extract_symbols ""$dts"" ""typescript"" | while read -r symbol; do
            echo ""- \`$symbol\`"" >> ""$OUTPUT_FILE""
        done
        echo """" >> ""$OUTPUT_FILE""
    fi
done

# Process JavaScript files
for js in ""$PACKAGE_DIR""/*.js ""$PACKAGE_DIR""/*.mjs; do
    if [ -f ""$js"" ]; then
        filename=$(basename ""$js"")
        filesize=$(stat -c%s ""$js"" 2>/dev/null || stat -f%z ""$js"" 2>/dev/null || echo ""unknown"")
        
        echo ""## 📄 $filename ($filesize bytes)"" >> ""$OUTPUT_FILE""
        echo """" >> ""$OUTPUT_FILE""
        
        # For minified files, just extract exports
        if [ ""$(wc -l < ""$js"")"" -lt 10 ] && [ ""$filesize"" -gt 100000 ]; then
            echo ""*Large minified file - showing exports only*"" >> ""$OUTPUT_FILE""
            echo """" >> ""$OUTPUT_FILE""
            
            grep -oE ""exports\.\w+"" ""$js"" | sed 's/exports\.//' | sort -u | head -50 | while read -r export; do
                echo ""- \`$export\`"" >> ""$OUTPUT_FILE""
            done
        else
            extract_symbols ""$js"" ""javascript"" | head -50 | while read -r symbol; do
                echo ""- \`$symbol\`"" >> ""$OUTPUT_FILE""
            done
        fi
        echo """" >> ""$OUTPUT_FILE""
    fi
done

echo ""Symbol index generated: $OUTPUT_FILE""
";
        
        await File.WriteAllTextAsync(scriptPath, script);
        
        if (!OperatingSystem.IsWindows())
        {
            var chmod = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            chmod.Start();
            await chmod.WaitForExitAsync();
        }
        
        Logger.Info($"Generated symbol index script: {scriptPath}");
    }
}