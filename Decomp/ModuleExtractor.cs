using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hoho.Decomp;

public class ModuleExtractor
{
    private readonly string _baseDir;
    private readonly Dictionary<string, string> _extractedModules = new();
    
    public ModuleExtractor(string baseDir = "claude-code-dev")
    {
        _baseDir = baseDir;
    }
    
    public async Task<ExtractResult> ExtractModulesAsync(string bundlePath, string version)
    {
        var versionDir = Path.Combine(_baseDir, "versions", version);
        var originalDir = Path.Combine(versionDir, "original");
        var manualDir = Path.Combine(versionDir, "manual");
        var modulesOriginalDir = Path.Combine(originalDir, "modules");
        var modulesManualDir = Path.Combine(manualDir, "modules");
        
        // Create directory structure
        Directory.CreateDirectory(modulesOriginalDir);
        Directory.CreateDirectory(modulesManualDir);
        
        var bundleContent = await File.ReadAllTextAsync(bundlePath);
        var modules = new List<ModuleInfo>();
        
        // Pattern 1: CommonJS modules like var Wu1=U((bnB)=>{...});
        var pattern1 = @"var\s+([A-Za-z0-9_]+)\s*=\s*U\s*\(\s*\(([^)]*)\)\s*=>\s*\{";
        var matches1 = Regex.Matches(bundleContent, pattern1);
        
        foreach (Match match in matches1)
        {
            var moduleName = match.Groups[1].Value;
            var parameter = match.Groups[2].Value;
            
            // Extract the full module content
            var moduleContent = ExtractModuleContent(bundleContent, match.Index);
            if (moduleContent != null)
            {
                modules.Add(new ModuleInfo 
                { 
                    Name = moduleName,
                    Parameter = parameter,
                    Content = moduleContent,
                    Type = "CommonJS"
                });
            }
        }
        
        // Pattern 2: React components and classes
        var pattern2 = @"class\s+([A-Za-z0-9_]+)(?:\s+extends\s+[^{]+)?\s*\{";
        var matches2 = Regex.Matches(bundleContent, pattern2);
        
        foreach (Match match in matches2)
        {
            var className = match.Groups[1].Value;
            var classContent = ExtractClassContent(bundleContent, match.Index);
            if (classContent != null)
            {
                modules.Add(new ModuleInfo
                {
                    Name = className,
                    Content = classContent,
                    Type = "Class"
                });
            }
        }
        
        // Pattern 3: Function modules
        var pattern3 = @"function\s+([A-Z][A-Za-z0-9_]*)\s*\([^)]*\)\s*\{";
        var matches3 = Regex.Matches(bundleContent, pattern3);
        
        foreach (Match match in matches3)
        {
            var funcName = match.Groups[1].Value;
            if (funcName.Length <= 4) // Likely obfuscated
            {
                var funcContent = ExtractFunctionContent(bundleContent, match.Index);
                if (funcContent != null)
                {
                    modules.Add(new ModuleInfo
                    {
                        Name = funcName,
                        Content = funcContent,
                        Type = "Function"
                    });
                }
            }
        }
        
        // Save extracted modules
        foreach (var module in modules)
        {
            var originalPath = Path.Combine(modulesOriginalDir, $"{module.Name}.js");
            await File.WriteAllTextAsync(originalPath, module.Content);
            
            // Create template in manual directory
            var manualPath = Path.Combine(modulesManualDir, $"{module.Name}.js");
            var template = GenerateManualTemplate(module);
            await File.WriteAllTextAsync(manualPath, template);
        }
        
        // Copy main files
        File.Copy(bundlePath, Path.Combine(originalDir, "cli.js"), true);
        
        // Create README in manual directory
        var readme = GenerateReadme(modules, version);
        await File.WriteAllTextAsync(Path.Combine(manualDir, "README.md"), readme);
        
        // Generate symbol map automatically
        try
        {
            var symbols = await ImprovedSymbolExtractor.ExtractSymbolsAsync(bundlePath);
            var symbolReport = ImprovedSymbolExtractor.GenerateCompactReport(symbols);
            var symbolMapPath = Path.Combine(versionDir, "symbol-map.md");
            await File.WriteAllTextAsync(symbolMapPath, symbolReport);
        }
        catch (Exception ex)
        {
            // Symbol extraction is optional, don't fail the whole process
            Console.WriteLine($"Warning: Could not generate symbol map: {ex.Message}");
        }
        
        return new ExtractResult
        {
            Version = version,
            ModuleCount = modules.Count,
            Modules = modules,
            OriginalDir = originalDir,
            ManualDir = manualDir
        };
    }
    
    private string? ExtractModuleContent(string content, int startIndex)
    {
        var depth = 0;
        var inString = false;
        var stringChar = ' ';
        var escaped = false;
        
        for (int i = startIndex; i < content.Length; i++)
        {
            var ch = content[i];
            
            if (escaped)
            {
                escaped = false;
                continue;
            }
            
            if (ch == '\\')
            {
                escaped = true;
                continue;
            }
            
            if (!inString)
            {
                if (ch == '"' || ch == '\'' || ch == '`')
                {
                    inString = true;
                    stringChar = ch;
                }
                else if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        // Found the end, now find the semicolon
                        var endIndex = i + 1;
                        while (endIndex < content.Length && content[endIndex] != ';')
                        {
                            endIndex++;
                        }
                        if (endIndex < content.Length)
                        {
                            return content.Substring(startIndex, endIndex - startIndex + 1);
                        }
                        return content.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }
            else if (ch == stringChar)
            {
                inString = false;
            }
        }
        
        return null;
    }
    
    private string? ExtractClassContent(string content, int startIndex)
    {
        return ExtractBlockContent(content, startIndex, "class");
    }
    
    private string? ExtractFunctionContent(string content, int startIndex)
    {
        return ExtractBlockContent(content, startIndex, "function");
    }
    
    private string? ExtractBlockContent(string content, int startIndex, string blockType)
    {
        var depth = 0;
        var foundStart = false;
        
        for (int i = startIndex; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                if (!foundStart)
                {
                    foundStart = true;
                    depth = 1;
                }
                else
                {
                    depth++;
                }
            }
            else if (content[i] == '}' && foundStart)
            {
                depth--;
                if (depth == 0)
                {
                    return content.Substring(startIndex, i - startIndex + 1);
                }
            }
        }
        
        return null;
    }
    
    private string GenerateManualTemplate(ModuleInfo module)
    {
        var template = $@"// Module: {module.Name}
// Type: {module.Type}
// TODO: Rename and clean up this module

{module.Content}

// Notes:
// - 
// - 
";
        return template;
    }
    
    private string GenerateReadme(List<ModuleInfo> modules, string version)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Claude Code {version} Manual Decompilation");
        sb.AppendLine();
        sb.AppendLine($"Extracted {modules.Count} modules from cli.js");
        sb.AppendLine();
        sb.AppendLine("## Module Summary");
        sb.AppendLine();
        
        var byType = modules.GroupBy(m => m.Type);
        foreach (var group in byType)
        {
            sb.AppendLine($"- {group.Key}: {group.Count()} modules");
        }
        
        sb.AppendLine();
        sb.AppendLine("## Modules to Rename");
        sb.AppendLine();
        
        foreach (var module in modules.OrderBy(m => m.Name))
        {
            sb.AppendLine($"- [ ] `{module.Name}` ({module.Type})");
        }
        
        sb.AppendLine();
        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine("1. Edit modules in this directory with meaningful names");
        sb.AppendLine("2. Run `hoho decomp learn-dir` to learn from your edits");
        sb.AppendLine("3. Apply learned mappings to new versions");
        
        return sb.ToString();
    }
}

public class ExtractResult
{
    public string Version { get; set; } = "";
    public int ModuleCount { get; set; }
    public List<ModuleInfo> Modules { get; set; } = new();
    public string OriginalDir { get; set; } = "";
    public string ManualDir { get; set; } = "";
}

public class ModuleInfo
{
    public string Name { get; set; } = "";
    public string? Parameter { get; set; }
    public string Content { get; set; } = "";
    public string Type { get; set; } = "";
}