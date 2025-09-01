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
        indexBuilder.AppendLine("# Claude Code Symbol Index (via Tree-sitter)");
        indexBuilder.AppendLine("Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"));
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
        output.AppendLine($"## 📄 {fileName}");
        output.AppendLine();
        
        try
        {
            // Use tree-sitter to parse TypeScript
            var symbols = await ExtractSymbolsWithTreeSitterAsync(filePath, "typescript");
            
            if (symbols.Any())
            {
                // Group symbols by type
                var grouped = symbols.GroupBy(s => s.Type)
                    .OrderBy(g => g.Key);
                
                foreach (var group in grouped)
                {
                    output.AppendLine($"### {group.Key}s");
                    foreach (var symbol in group.OrderBy(s => s.Name))
                    {
                        output.AppendLine($"- `{symbol.Name}`{(symbol.Line > 0 ? $" (line {symbol.Line})" : "")}");
                        if (!string.IsNullOrEmpty(symbol.Signature))
                        {
                            output.AppendLine($"  ```typescript");
                            output.AppendLine($"  {symbol.Signature}");
                            output.AppendLine($"  ```");
                        }
                    }
                    output.AppendLine();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to index {fileName}: {ex.Message}");
            output.AppendLine($"*Failed to parse: {ex.Message}*");
            output.AppendLine();
        }
    }
    
    /// <summary>
    /// Index a JavaScript file using tree-sitter.
    /// </summary>
    private static async Task IndexJavaScriptFileAsync(string filePath, StringBuilder output)
    {
        var fileName = Path.GetFileName(filePath);
        var fileSize = new FileInfo(filePath).Length;
        
        output.AppendLine($"## 📄 {fileName} ({fileSize:N0} bytes)");
        output.AppendLine();
        
        // For large minified files, extract high-level structure only
        if (fileSize > 100_000 && CountLines(filePath) < 10)
        {
            output.AppendLine("*Large minified file - extracting top-level symbols only*");
            output.AppendLine();
            
            var symbols = await ExtractMinifiedSymbolsAsync(filePath);
            if (symbols.Any())
            {
                output.AppendLine("### Detected Symbols");
                foreach (var symbol in symbols.Take(100)) // Limit output
                {
                    output.AppendLine($"- `{symbol}`");
                }
                if (symbols.Count > 100)
                {
                    output.AppendLine($"- ... and {symbols.Count - 100} more");
                }
            }
        }
        else
        {
            // Full parse for non-minified files
            var symbols = await ExtractSymbolsWithTreeSitterAsync(filePath, "javascript");
            foreach (var symbol in symbols.Take(50))
            {
                output.AppendLine($"- `{symbol.Name}` ({symbol.Type})");
            }
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
        
        // Look for common patterns in minified code
        var patterns = new[]
        {
            @"exports\.(\w+)",           // CommonJS exports
            @"export\s*{\s*([^}]+)\s*}", // ES6 exports
            @"\.prototype\.(\w+)",        // Prototype methods
            @"window\.(\w+)",            // Global assignments
            @"global\.(\w+)",            // Node globals
            @"define\([""'](\w+)[""']",  // AMD modules
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