using System.CommandLine;
using System.Text.Json;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// Full decompilation pipeline command integration
/// </summary>
public class DecompCommand : Command
{
    public DecompCommand() : base("decomp", "Advanced decompilation and analysis tools")
    {
        // Core commands (simplified)
        AddCommand(new ExtractNewCommand());
        AddCommand(new SmartRenameCommand());
        AddCommand(new RenameAllCommand());
        AddCommand(new FinalizeCommand());
        AddCommand(new CleanupCommand());
        AddCommand(new ListVersionsCommand());
        
        // Mapping management
        AddCommand(new AddMappingCommand());
        AddCommand(new ShowMappingsCommand());
        
        // LSP server management
        AddCommand(new LspStartCommand());
        AddCommand(new LspStopCommand());
        AddCommand(new LspStatusCommand());
        
        // Legacy commands (kept for compatibility)
        AddCommand(new SetupCommand());
        AddCommand(new ExtractCommand());
        AddCommand(new LearnCommand());
        AddCommand(new LearnDirCommand());
        AddCommand(new ApplyCommand());
        AddCommand(new ApplyDirCommand());
        AddCommand(new AnalyzeCommand());
        AddCommand(new UpdateCommand());
        AddCommand(new ListCommand());
        AddCommand(new SymbolMapCommand());
    }
    
    /// <summary>
    /// Setup a new version for decompilation
    /// </summary>
    private class SetupCommand : Command
    {
        public SetupCommand() : base("setup", "Setup a new version for decompilation")
        {
            var versionArg = new Argument<string>("version", "Version number (e.g. 1.0.98)");
            var sourceOpt = new Option<string>("--source", "Source file or URL") { IsRequired = false };
            var baseDirOpt = new Option<string>("--base-dir", () => "claude-code-dev", "Base directory for all versions");
            
            AddArgument(versionArg);
            AddOption(sourceOpt);
            AddOption(baseDirOpt);
            
            this.SetHandler(async (version, source, baseDir) =>
            {
                var manager = new VersionManager(baseDir);
                var versionDir = await manager.SetupVersionAsync(version, source);
                
                Logger.Info($"Version {version} setup complete");
                Logger.Info($"Directory structure created at: {versionDir}");
                Logger.Info("");
                Logger.Info("Next steps:");
                Logger.Info($"1. Edit files in {versionDir}/manual/ with clean names");
                Logger.Info($"2. Run: hoho decomp learn-dir {version}");
                
            }, versionArg, sourceOpt, baseDirOpt);
        }
    }
    
    /// <summary>
    /// Learn from directory of manual edits
    /// </summary>
    private class LearnDirCommand : Command
    {
        public LearnDirCommand() : base("learn-dir", "Learn mappings from entire directory of manual edits")
        {
            var versionArg = new Argument<string>("version", "Version to learn from");
            var baseDirOpt = new Option<string>("--base-dir", () => "claude-code-dev", "Base directory");
            
            AddArgument(versionArg);
            AddOption(baseDirOpt);
            
            this.SetHandler(async (version, baseDir) =>
            {
                var manager = new VersionManager(baseDir);
                var result = await manager.LearnFromDirectoryAsync(version);
                
                Logger.Info($"Learned {result.TotalMappings} mappings from version {version}");
                
                Console.WriteLine("\nMappings by file:");
                foreach (var file in result.FileMappings)
                {
                    Console.WriteLine($"  {file.Key}: {file.Value.TotalMappings} mappings");
                }
                
                if (result.FileRenames.Any())
                {
                    Console.WriteLine("\nFile renames learned:");
                    foreach (var rename in result.FileRenames.Take(10))
                    {
                        Console.WriteLine($"  {rename.Key}.js -> {rename.Value}.js");
                    }
                }
                
            }, versionArg, baseDirOpt);
        }
    }
    
    /// <summary>
    /// Apply mappings to entire version directory
    /// </summary>
    private class ApplyDirCommand : Command
    {
        public ApplyDirCommand() : base("apply-dir", "Apply learned mappings to entire version")
        {
            var targetArg = new Argument<string>("target", "Target version to deobfuscate");
            var sourceOpt = new Option<string>("--source", "Source version with learned mappings") 
            { 
                IsRequired = false 
            };
            var baseDirOpt = new Option<string>("--base-dir", () => "claude-code-dev", "Base directory");
            
            AddArgument(targetArg);
            AddOption(sourceOpt);
            AddOption(baseDirOpt);
            
            this.SetHandler(async (target, source, baseDir) =>
            {
                var manager = new VersionManager(baseDir);
                await manager.ApplyMappingsToVersionAsync(target, source);
                
                var automatedDir = Path.Combine(baseDir, "versions", target, "automated");
                Logger.Info($"Automated deobfuscation complete for version {target}");
                Logger.Info($"Output: {automatedDir}");
                
            }, targetArg, sourceOpt, baseDirOpt);
        }
    }
    
    /// <summary>
    /// List all managed versions
    /// </summary>
    private class ListCommand : Command
    {
        public ListCommand() : base("list", "List all managed versions")
        {
            var baseDirOpt = new Option<string>("--base-dir", () => "claude-code-dev", "Base directory");
            
            AddOption(baseDirOpt);
            
            this.SetHandler((baseDir) =>
            {
                var manager = new VersionManager(baseDir);
                manager.ListVersions();
                
            }, baseDirOpt);
        }
    }
    
    /// <summary>
    /// Extract and analyze a new bundle
    /// </summary>
    private class ExtractCommand : Command
    {
        public ExtractCommand() : base("extract", "Extract modules from webpack bundle")
        {
            var bundleArg = new Argument<string>("bundle", "Path to bundled JS file");
            var versionOpt = new Option<string>("--version", () => "1.0.98", "Version number");
            var outputOpt = new Option<string>("--output", "Output directory") { IsRequired = false };
            
            AddArgument(bundleArg);
            AddOption(versionOpt);
            AddOption(outputOpt);
            
            this.SetHandler(async (bundle, version, output) =>
            {
                // Use ModuleExtractor for better extraction
                var extractor = new ModuleExtractor();
                var result = await extractor.ExtractModulesAsync(bundle, version);
                
                Logger.Info($"Extracted {result.ModuleCount} modules from {Path.GetFileName(bundle)}");
                Logger.Info($"Version: {result.Version}");
                Logger.Info($"Original: {result.OriginalDir}");
                Logger.Info($"Manual: {result.ManualDir}");
                Console.WriteLine();
                Console.WriteLine("Module breakdown:");
                var byType = result.Modules.GroupBy(m => m.Type);
                foreach (var group in byType)
                {
                    Console.WriteLine($"  {group.Key}: {group.Count()} modules");
                }
                
                if (!string.IsNullOrEmpty(output))
                {
                    // Also use old extraction if output specified
                    output ??= $"decomp/extracted/{DateTime.Now:yyyy-MM-dd-HHmmss}";
                    
                    Logger.Info($"Also extracting with old method to {output}");
                    
                    // Step 1: Analyze bundle structure
                    var analysis = await WebpackBundleAnalyzer.AnalyzeBundleAsync(bundle);
                    Logger.Info($"Found {analysis.Modules.Count} modules");
                    
                    // Step 2: Extract modules
                    var session = await AutomatedDecompPipeline.RunPipelineAsync(bundle);
                    
                    // Step 3: Save extracted modules
                    Directory.CreateDirectory(output);
                
                foreach (var module in session.Modules)
                {
                    var modulePath = Path.Combine(output, $"{module.Hash}.js");
                    await File.WriteAllTextAsync(modulePath, module.Body);
                    
                    // Save metadata
                    var metaPath = Path.Combine(output, $"{module.Hash}.meta.json");
                    var meta = new
                    {
                        module.Hash,
                        module.OriginalName,
                        module.Type,
                        module.Exports,
                        module.Imports,
                        Analysis = module.SemanticAnalysis,
                        ProposedRenames = module.ProposedRenames
                    };
                    
                    var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    await File.WriteAllTextAsync(metaPath, json);
                }
                
                // Step 4: Generate report
                var reportPath = Path.Combine(output, "extraction-report.md");
                var report = GenerateExtractionReport(session, analysis);
                await File.WriteAllTextAsync(reportPath, report);
                
                Logger.Info($"Extracted {session.Modules.Count} modules to {output}");
                Logger.Info($"Report saved to {reportPath}");
                }
                
            }, bundleArg, versionOpt, outputOpt);
        }
        
        private static string GenerateExtractionReport(DecompSession session, WebpackBundleAnalyzer.BundleAnalysis analysis)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("# Module Extraction Report");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Version: {session.Version}");
            sb.AppendLine();
            
            sb.AppendLine("## Statistics");
            sb.AppendLine($"- Total Modules: {session.Modules.Count}");
            sb.AppendLine($"- React Components: {session.Modules.Count(m => m.Type == ModuleType.ReactComponent)}");
            sb.AppendLine($"- Classes: {session.Modules.Count(m => m.Type == ModuleType.Class)}");
            sb.AppendLine($"- Functions: {session.Modules.Count(m => m.Type == ModuleType.Function)}");
            sb.AppendLine();
            
            sb.AppendLine("## High Confidence Renames");
            var highConfidence = session.Modules
                .Where(m => m.ProposedRenames?.Any(r => r.Value.Confidence > 0.8f) == true)
                .OrderByDescending(m => m.ProposedRenames!.Max(r => r.Value.Confidence));
                
            foreach (var module in highConfidence.Take(20))
            {
                var best = module.ProposedRenames!.OrderByDescending(r => r.Value.Confidence).First();
                sb.AppendLine($"- {best.Key} → {best.Value.NewName} ({best.Value.Confidence:P0}): {best.Value.Reason}");
            }
            
            sb.AppendLine();
            sb.AppendLine("## Tool Implementations Found");
            foreach (var tool in analysis.ToolImplementations)
            {
                sb.AppendLine($"- {tool.Key}: {tool.Value.Count} references");
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Learn mappings from manually edited code
    /// </summary>
    private class LearnCommand : Command
    {
        public LearnCommand() : base("learn", "Learn mappings from your manual edits")
        {
            var originalArg = new Argument<string>("original", "Path to original obfuscated code");
            var editedArg = new Argument<string>("edited", "Path to your manually edited clean code");
            var mapOpt = new Option<string>("--map", () => "decomp/learned-mappings.json", "Path to mapping database");
            
            AddArgument(originalArg);
            AddArgument(editedArg);
            AddOption(mapOpt);
            
            this.SetHandler(async (original, edited, mapPath) =>
            {
                Logger.Info("Learning mappings from manual edits...");
                
                var mapper = new DecompilationMapper(mapPath);
                var result = await mapper.LearnMappings(original, edited);
                
                Logger.Info($"Learned {result.TotalMappings} mappings:");
                Logger.Info($"  Functions: {result.FunctionMappings.Count}");
                Logger.Info($"  Classes: {result.ClassMappings.Count}");
                Logger.Info($"  Variables: {result.VariableMappings.Count}");
                Logger.Info($"  Parameters: {result.ParameterMappings.Count}");
                
                // Show examples
                Console.WriteLine("\nExample mappings learned:");
                foreach (var func in result.FunctionMappings.Take(5))
                {
                    Console.WriteLine($"  {func.Key} → {func.Value}");
                }
                
                // Generate report
                var report = mapper.GenerateReport();
                var reportPath = Path.ChangeExtension(mapPath, ".report.md");
                await File.WriteAllTextAsync(reportPath, report);
                Logger.Info($"Report saved to {reportPath}");
                
            }, originalArg, editedArg, mapOpt);
        }
    }
    
    /// <summary>
    /// Apply learned mappings to new code
    /// </summary>
    private class ApplyCommand : Command
    {
        public ApplyCommand() : base("apply", "Apply learned mappings to obfuscated code")
        {
            var inputArg = new Argument<string>("input", "Path to obfuscated code");
            var outputOpt = new Option<string>("--output", "Output path for renamed code") { IsRequired = false };
            var mapOpt = new Option<string>("--map", () => "decomp/learned-mappings.json", "Path to mapping database");
            var reportOpt = new Option<bool>("--report", () => true, "Generate mapping report");
            
            AddArgument(inputArg);
            AddOption(outputOpt);
            AddOption(mapOpt);
            AddOption(reportOpt);
            
            this.SetHandler(async (input, output, mapPath, generateReport) =>
            {
                output ??= Path.ChangeExtension(input, ".deobfuscated.js");
                
                Logger.Info($"Applying mappings to {input}");
                
                var mapper = new DecompilationMapper(mapPath);
                var result = await mapper.ApplyMappings(input);
                
                await File.WriteAllTextAsync(output, result);
                Logger.Info($"Deobfuscated code saved to {output}");
                
                if (generateReport)
                {
                    var report = mapper.GenerateReport();
                    var reportPath = Path.ChangeExtension(output, ".report.md");
                    await File.WriteAllTextAsync(reportPath, report);
                    Logger.Info($"Report saved to {reportPath}");
                }
                
                // Show statistics
                var original = await File.ReadAllTextAsync(input);
                var symbolsReplaced = CountReplacements(original, result);
                Logger.Info($"Replaced {symbolsReplaced} symbol occurrences");
                
            }, inputArg, outputOpt, mapOpt, reportOpt);
        }
        
        private static int CountReplacements(string original, string result)
        {
            // Simple heuristic: count identifier changes
            var origIds = System.Text.RegularExpressions.Regex.Matches(original, @"\b\w+\b").Select(m => m.Value);
            var resultIds = System.Text.RegularExpressions.Regex.Matches(result, @"\b\w+\b").Select(m => m.Value);
            
            return origIds.Zip(resultIds).Count(pair => pair.First != pair.Second);
        }
    }
    
    /// <summary>
    /// Analyze differences between versions
    /// </summary>
    private class AnalyzeCommand : Command
    {
        public AnalyzeCommand() : base("analyze", "Analyze differences between versions")
        {
            var oldArg = new Argument<string>("old", "Path to old version");
            var newArg = new Argument<string>("new", "Path to new version");
            var outputOpt = new Option<string>("--output", "Output directory for analysis") { IsRequired = false };
            
            AddArgument(oldArg);
            AddArgument(newArg);
            AddOption(outputOpt);
            
            this.SetHandler(async (oldPath, newPath, output) =>
            {
                output ??= $"decomp/analysis/{DateTime.Now:yyyy-MM-dd-HHmmss}";
                Directory.CreateDirectory(output);
                
                Logger.Info("Analyzing version differences...");
                
                // Extract modules from both versions
                var oldSession = await AutomatedDecompPipeline.RunPipelineAsync(oldPath);
                var newSession = await AutomatedDecompPipeline.RunPipelineAsync(newPath, 
                    Path.Combine(output, "old-session.json"));
                
                // Generate diff report
                var report = GenerateDiffReport(oldSession, newSession);
                var reportPath = Path.Combine(output, "diff-report.md");
                await File.WriteAllTextAsync(reportPath, report);
                
                Logger.Info($"Analysis saved to {output}");
                Logger.Info($"Report: {reportPath}");
                
            }, oldArg, newArg, outputOpt);
        }
        
        private static string GenerateDiffReport(DecompSession old, DecompSession newSession)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("# Version Difference Analysis");
            sb.AppendLine($"Old Version: {old.Version}");
            sb.AppendLine($"New Version: {newSession.Version}");
            sb.AppendLine();
            
            if (newSession.DiffAnalysis != null)
            {
                var diff = newSession.DiffAnalysis;
                sb.AppendLine("## Summary");
                sb.AppendLine($"- Unchanged: {diff.UnchangedModules.Count} modules");
                sb.AppendLine($"- Changed: {diff.ChangedModules.Count} modules");
                sb.AppendLine($"- New: {diff.NewModules.Count} modules");
                sb.AppendLine($"- Removed: {diff.RemovedModules.Count} modules");
                sb.AppendLine();
                
                if (diff.NewModules.Any())
                {
                    sb.AppendLine("## New Modules");
                    foreach (var moduleId in diff.NewModules.Take(20))
                    {
                        var module = newSession.Modules.First(m => m.Id == moduleId);
                        sb.AppendLine($"- {module.OriginalName} ({module.Type})");
                    }
                }
                
                if (diff.ChangedModules.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("## Changed Modules");
                    foreach (var change in diff.ChangedModules.Take(20))
                    {
                        sb.AppendLine($"- Module {change.Key}:");
                        sb.AppendLine($"  Similarity: {change.Value.Similarity:P0}");
                        foreach (var changeDetail in change.Value.Changes)
                        {
                            sb.AppendLine($"  - {changeDetail}");
                        }
                    }
                }
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Full automated update workflow
    /// </summary>
    private class UpdateCommand : Command
    {
        public UpdateCommand() : base("update", "Automated update workflow for new versions")
        {
            var versionOpt = new Option<string>("--version", "Version to download") { IsRequired = false };
            var applyOpt = new Option<bool>("--apply-mappings", () => true, "Apply learned mappings");
            
            AddOption(versionOpt);
            AddOption(applyOpt);
            
            this.SetHandler(async (version, applyMappings) =>
            {
                Logger.Info("Starting automated update workflow...");
                
                // Step 1: Download latest version
                if (string.IsNullOrEmpty(version))
                {
                    version = await GetLatestVersionAsync();
                }
                
                Logger.Info($"Processing version {version}");
                
                // Step 2: Extract and analyze
                var bundlePath = await DownloadVersionAsync(version);
                var outputDir = $"decomp/versions/{version}";
                Directory.CreateDirectory(outputDir);
                
                var session = await AutomatedDecompPipeline.RunPipelineAsync(bundlePath);
                
                // Step 3: Apply mappings if requested
                if (applyMappings && File.Exists("decomp/learned-mappings.json"))
                {
                    Logger.Info("Applying learned mappings...");
                    
                    var mapper = new DecompilationMapper();
                    
                    foreach (var module in session.Modules)
                    {
                        var modulePath = Path.Combine(outputDir, $"{module.Hash}.js");
                        await File.WriteAllTextAsync(modulePath, module.Body);
                        
                        var mappedPath = Path.Combine(outputDir, $"{module.Hash}.mapped.js");
                        var mapped = await mapper.ApplyMappings(modulePath);
                        await File.WriteAllTextAsync(mappedPath, mapped);
                    }
                }
                
                // Step 4: Generate summary
                var summary = GenerateUpdateSummary(session, version);
                var summaryPath = Path.Combine(outputDir, "update-summary.md");
                await File.WriteAllTextAsync(summaryPath, summary);
                
                Logger.Info($"Update complete for version {version}");
                Logger.Info($"Output: {outputDir}");
                Logger.Info($"Summary: {summaryPath}");
                
            }, versionOpt, applyOpt);
        }
        
        private static async Task<string> GetLatestVersionAsync()
        {
            // Check npm registry for latest version
            using var http = new HttpClient();
            var response = await http.GetStringAsync("https://registry.npmjs.org/@anthropic-ai/claude-code/latest");
            var data = JsonDocument.Parse(response);
            return data.RootElement.GetProperty("version").GetString() ?? "unknown";
        }
        
        private static async Task<string> DownloadVersionAsync(string version)
        {
            var outputPath = $"decomp/downloads/claude-code-{version}.tgz";
            
            if (File.Exists(outputPath))
            {
                Logger.Info($"Using cached download: {outputPath}");
                return outputPath;
            }
            
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            
            using var http = new HttpClient();
            var url = $"https://registry.npmjs.org/@anthropic-ai/claude-code/-/claude-code-{version}.tgz";
            
            Logger.Info($"Downloading from {url}");
            var bytes = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(outputPath, bytes);
            
            return outputPath;
        }
        
        private static string GenerateUpdateSummary(DecompSession session, string version)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"# Update Summary - Version {version}");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            sb.AppendLine("## Modules Extracted");
            sb.AppendLine($"- Total: {session.Modules.Count}");
            
            if (session.DiffAnalysis != null)
            {
                sb.AppendLine();
                sb.AppendLine("## Changes from Previous Version");
                sb.AppendLine($"- New modules: {session.DiffAnalysis.NewModules.Count}");
                sb.AppendLine($"- Changed modules: {session.DiffAnalysis.ChangedModules.Count}");
                sb.AppendLine($"- Removed modules: {session.DiffAnalysis.RemovedModules.Count}");
                sb.AppendLine($"- Unchanged modules: {session.DiffAnalysis.UnchangedModules.Count}");
            }
            
            sb.AppendLine();
            sb.AppendLine("## Next Steps");
            sb.AppendLine("1. Review new/changed modules in the output directory");
            sb.AppendLine("2. Manually edit any incorrectly mapped symbols");
            sb.AppendLine("3. Run `hoho decomp learn` on your edits to update mappings");
            sb.AppendLine("4. Mappings will be applied automatically to future versions");
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Generate improved symbol map with class-method relationships
    /// </summary>
    private class SymbolMapCommand : Command
    {
        public SymbolMapCommand() : base("symbol-map", "Generate detailed symbol map with class-method relationships")
        {
            var fileArg = new Argument<string>("file", "JavaScript file to analyze");
            var versionOpt = new Option<string>("--version", "Version to save symbol map for") { IsRequired = false };
            
            AddArgument(fileArg);
            AddOption(versionOpt);
            
            this.SetHandler(async (file, version) =>
            {
                Logger.Info($"Extracting symbols from {file}...");
                
                var symbols = await ImprovedSymbolExtractor.ExtractSymbolsAsync(file);
                var report = ImprovedSymbolExtractor.GenerateCompactReport(symbols);
                
                // Auto-save to version directory if version specified
                if (!string.IsNullOrEmpty(version))
                {
                    var versionDir = Path.Combine("claude-code-dev", "versions", version);
                    if (Directory.Exists(versionDir))
                    {
                        var symbolMapPath = Path.Combine(versionDir, "symbol-map.md");
                        await File.WriteAllTextAsync(symbolMapPath, report);
                        Logger.Info($"Symbol map saved to {symbolMapPath}");
                    }
                    else
                    {
                        Logger.Warn($"Version directory {versionDir} does not exist");
                    }
                }
                
                // Summary only to console (compact)
                Console.WriteLine($"Classes: {symbols.TotalClasses}, Functions: {symbols.TotalFunctions} (Standalone: {symbols.StandaloneFunctions.Count}), Exports: {symbols.Exports.Count}");
                
            }, fileArg, versionOpt);
        }
    }
}