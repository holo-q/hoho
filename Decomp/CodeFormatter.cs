using System.Diagnostics;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// FAST code formatter using existing CLI tools.
/// Delegates to dotnet format, prettier, etc. for maximum speed.
/// </summary>
public static class CodeFormatter
{
    /// <summary>
    /// Format directory using external CLI tools - MAXIMUM SPEED.
    /// </summary>
    public static async Task FormatExtractedCodeAsync(string extractedDir, string formattedDir)
    {
        using var timer = Logger.TimeOperation("Format Extracted Code Directory");
        
        Directory.CreateDirectory(formattedDir);
        
        // Copy all files first
        await CopyDirectoryAsync(extractedDir, formattedDir);
        
        // Run formatters on copied files
        var tasks = new[]
        {
            FormatWithPrettierAsync(formattedDir), // JS/TS/JSON
            FormatWithDotnetAsync(formattedDir),   // C#
        };
        
        await Task.WhenAll(tasks);
        Logger.Info("Code formatting completed using external CLI tools");
    }
    
    /// <summary>
    /// Format JS/TS/JSON using Prettier CLI.
    /// </summary>
    private static async Task FormatWithPrettierAsync(string dir)
    {
        try
        {
            var process = new ProcessStartInfo
            {
                FileName = "npx",
                Arguments = "prettier --write \"**/*.{js,jsx,ts,tsx,json}\"",
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            
            using var proc = Process.Start(process);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                Logger.Debug("Prettier formatting completed with exit code {ExitCode}", proc.ExitCode);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Prettier not available: {Error}", ex.Message);
        }
    }
    
    /// <summary>
    /// Format C# using dotnet format CLI.
    /// </summary>
    private static async Task FormatWithDotnetAsync(string dir)
    {
        try
        {
            var process = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "format --include \"**/*.cs\"",
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            
            using var proc = Process.Start(process);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                Logger.Debug("dotnet format completed with exit code {ExitCode}", proc.ExitCode);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("dotnet format not available: {Error}", ex.Message);
        }
    }
    
    /// <summary>
    /// Fast directory copy with async operations.
    /// </summary>
    private static async Task CopyDirectoryAsync(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        var tasks = files.Select(file =>
            Task.Run(() => {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var destFile = Path.Combine(destDir, relativePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                File.Copy(file, destFile, true);
            }));
        
        await Task.WhenAll(tasks);
    }
}