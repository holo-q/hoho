using System.CommandLine;
using Hoho.Core;
using Hoho.Decomp;
using Serilog;

namespace Hoho;

/// <summary>
/// HOHO - The CLI Agent That Just Says "OK."
/// Shadow Protocol: Overwhelming power through calm, principled simplicity.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Initialize high-performance logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console()
            .WriteTo.File("logs/hoho-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        try
        {
            Core.Logger.Info("🥋 HOHO Shadow Protocol - Startup initiated");
            
            var rootCommand = new RootCommand("🥋 HOHO - The CLI Agent That Just Says 'OK.'");

        // Home command - Show Saitama face
        var homeCommand = new Command("home", "Show hoho home screen with Saitama face");
        homeCommand.SetHandler(ShowHome);
        rootCommand.AddCommand(homeCommand);

        // Status command - Show decomp status
        var statusCommand = new Command("status", "Show project status and decomp data");
        statusCommand.SetHandler(ShowStatus);
        rootCommand.AddCommand(statusCommand);

        // Version command
        var versionCommand = new Command("version", "Show hoho version");
        versionCommand.SetHandler(ShowVersion);
        rootCommand.AddCommand(versionCommand);

        // Decomp commands - use the comprehensive DecompCommand
        var decompCommand = new DecompCommand();
        rootCommand.AddCommand(decompCommand);

            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Core.Logger.Error(ex, "Fatal error in HOHO Shadow Protocol");
            return 1;
        }
        finally
        {
            Core.Logger.Info("🥋 HOHO Shadow Protocol - Shutdown complete");
            Core.Logger.Shutdown();
        }
    }

    private static void ShowHome()
    {
        Console.WriteLine(AsciiArt.GetSaitamaFace());
        Console.WriteLine();
        Console.WriteLine("HOHO - The CLI Agent That Just Says 'OK.'");
        Console.WriteLine("Shadow Protocol Active");
    }

    private static void ShowStatus()
    {
        var decompDir = Path.Combine(Environment.CurrentDirectory, "decomp");
        
        if (!Directory.Exists(decompDir))
        {
            Console.WriteLine("No decomp data found. Run 'hoho decomp setup' first.");
            return;
        }

        var agents = new[] { "claude-code", "openai-codex", "gemini-cli" };
        
        foreach (var agent in agents)
        {
            var agentPath = Path.Combine(decompDir, agent);
            if (Directory.Exists(agentPath))
            {
                Console.WriteLine($"✓ {agent}");
                var analysisPath = Path.Combine(agentPath, "analysis");
                if (Directory.Exists(analysisPath))
                {
                    var files = Directory.GetFiles(analysisPath).Length;
                    Console.WriteLine($"  └─ {files} analysis files");
                }
            }
            else
            {
                Console.WriteLine($"✗ {agent} (not found)");
            }
        }
    }

    private static void ShowVersion()
    {
        Console.WriteLine("hoho v0.1.0 - Native C# Shadow Protocol");
    }
}