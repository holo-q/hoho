using System.CommandLine;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// Command to migrate existing JSON mapping databases to MessagePack format
/// </summary>
public class MigrateMappingsCommand : Command
{
    public MigrateMappingsCommand() : base("migrate-mappings", "Migrate JSON mapping database to MessagePack format")
    {
        var jsonPathOption = new Option<string?>(
            "--json-path",
            "Path to existing JSON mappings file (auto-discovers if not specified)"
        );
        
        var msgpackPathOption = new Option<string?>(
            "--output",
            "Output MessagePack file path (default: decomp/mappings.msgpack)"
        );
        
        var backupOption = new Option<bool>(
            "--backup",
            () => true,
            "Create backup of original JSON file"
        );
        
        var forceOption = new Option<bool>(
            "--force",
            () => false,
            "Overwrite existing MessagePack database"
        );

        AddOption(jsonPathOption);
        AddOption(msgpackPathOption);
        AddOption(backupOption);
        AddOption(forceOption);

        this.SetHandler(async (jsonPath, msgpackPath, backup, force) =>
        {
            await MigrateMappingsAsync(jsonPath, msgpackPath, backup, force);
        }, jsonPathOption, msgpackPathOption, backupOption, forceOption);
    }

    private async Task MigrateMappingsAsync(string? jsonPath, string? msgpackPath, bool backup, bool force)
    {
        try
        {
            Logger.Info("🔄 Starting JSON to MessagePack migration...");
            
            // Auto-discover JSON files if not specified
            var jsonFiles = DiscoverJsonMappingFiles(jsonPath);
            if (!jsonFiles.Any())
            {
                Logger.Warning("No JSON mapping files found. Searched for:");
                Logger.Info("  - decomp/mappings.json");
                Logger.Info("  - decomp/learned-mappings.json");
                Logger.Info("  - decomp/mappings/global-mappings.json");
                Logger.Info("");
                Logger.Info("Use --json-path to specify a custom location.");
                return;
            }

            var db = new MessagePackMappingDatabase(msgpackPath);
            var totalMigrated = 0;
            
            // Check if MessagePack DB already exists
            if (!force && File.Exists(msgpackPath ?? "decomp/mappings.msgpack"))
            {
                Logger.Warning("MessagePack database already exists. Use --force to overwrite or specify different --output path.");
                return;
            }

            foreach (var jsonFile in jsonFiles)
            {
                Logger.Info($"📁 Processing {jsonFile}...");
                
                try
                {
                    var beforeCount = db.GetStatistics().TotalMappings;
                    await db.MigrateFromJsonAsync(jsonFile);
                    var afterCount = db.GetStatistics().TotalMappings;
                    var migrated = afterCount - beforeCount;
                    
                    Logger.Success($"   ✅ Migrated {migrated} mappings from {Path.GetFileName(jsonFile)}");
                    totalMigrated += migrated;
                    
                    // Backup original file if requested
                    if (backup && migrated > 0)
                    {
                        var backupPath = $"{jsonFile}.backup.{DateTime.Now:yyyyMMdd-HHmmss}";
                        File.Copy(jsonFile, backupPath);
                        Logger.Info($"   📦 Backed up original to {Path.GetFileName(backupPath)}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"   ❌ Failed to migrate {jsonFile}: {ex.Message}");
                }
            }

            if (totalMigrated > 0)
            {
                await db.SaveAsync();
                Logger.Success($"🎉 Migration complete! Migrated {totalMigrated} total mappings to MessagePack format.");
                
                // Show performance comparison
                await ShowPerformanceComparison(jsonFiles.First(), msgpackPath ?? "decomp/mappings.msgpack");
                
                Logger.Info("");
                Logger.Info("Next steps:");
                Logger.Info($"  1. Verify migration: hoho decomp show-mappings --format stats");
                Logger.Info($"  2. Test with: hoho decomp show-mappings --format table --limit 10");
                Logger.Info($"  3. Remove old JSON files when satisfied");
            }
            else
            {
                Logger.Warning("No mappings were migrated. Check JSON file formats.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Migration failed: {ex.Message}");
        }
    }

    private List<string> DiscoverJsonMappingFiles(string? specificPath)
    {
        var jsonFiles = new List<string>();
        
        if (specificPath != null)
        {
            if (File.Exists(specificPath))
            {
                jsonFiles.Add(specificPath);
            }
            return jsonFiles;
        }
        
        // Common JSON mapping file locations
        var candidates = new[]
        {
            "decomp/mappings.json",
            "decomp/learned-mappings.json", 
            "decomp/mappings/global-mappings.json"
        };
        
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                jsonFiles.Add(candidate);
                Logger.Info($"📁 Found {candidate}");
            }
        }
        
        return jsonFiles;
    }

    private async Task ShowPerformanceComparison(string jsonPath, string msgpackPath)
    {
        try
        {
            var jsonSize = new FileInfo(jsonPath).Length;
            var msgpackSize = new FileInfo(msgpackPath).Length;
            
            var sizeSavings = ((double)(jsonSize - msgpackSize) / jsonSize) * 100;
            
            Logger.Info("");
            Logger.Info("📊 Performance Comparison:");
            Logger.Info($"   JSON size:       {FormatBytes(jsonSize)}");
            Logger.Info($"   MessagePack size: {FormatBytes(msgpackSize)}");
            Logger.Info($"   Size reduction:  {sizeSavings:F1}% smaller");
            
            // Quick serialization benchmark
            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var msgpackBytes = await File.ReadAllBytesAsync(msgpackPath);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // JSON deserialization test
            stopwatch.Restart();
            for (int i = 0; i < 100; i++)
            {
                System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
            }
            var jsonTime = stopwatch.ElapsedMilliseconds;
            
            // MessagePack deserialization test
            stopwatch.Restart();
            for (int i = 0; i < 100; i++)
            {
                MessagePack.MessagePackSerializer.Deserialize<SymbolMappingCollection>(msgpackBytes);
            }
            var msgpackTime = stopwatch.ElapsedMilliseconds;
            
            if (jsonTime > 0 && msgpackTime > 0)
            {
                var speedup = (double)jsonTime / msgpackTime;
                Logger.Info($"   Load speed:      {speedup:F1}x faster");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Could not perform benchmark: {ex.Message}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}