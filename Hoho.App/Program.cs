using System.CommandLine;
using Serilog;

namespace Hoho;

/// <summary>
/// HOHO - The CLI Agent That Just Says "OK."
/// Shadow Protocol: Overwhelming power through calm, principled simplicity.
/// </summary>
public static class Program {
	public static async Task<int> Main(string[] args) {
		// Initialize logging
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
			.WriteTo.Console()
			.WriteTo.File("logs/hoho-.log", rollingInterval: RollingInterval.Day)
			.CreateLogger();

		try {
			Log.Information("🥋 HOHO Shadow Protocol - Startup initiated");

			var rootCommand = new RootCommand("🥋 HOHO - The CLI Agent That Just Says 'OK.'");

			// Home command - Show Saitama face
			var homeCommand = new Command("home", "Show hoho home screen with Saitama face");
			homeCommand.SetHandler(ShowHome);
			rootCommand.AddCommand(homeCommand);

			// Version command
			var versionCommand = new Command("version", "Show hoho version");
			versionCommand.SetHandler(ShowVersion);
			rootCommand.AddCommand(versionCommand);


			return await rootCommand.InvokeAsync(args);
		} catch (Exception ex) {
			Log.Error(ex, "Fatal error in HOHO Shadow Protocol");
			return 1;
		} finally {
			Log.Information("🥋 HOHO Shadow Protocol - Shutdown complete");
			Log.CloseAndFlush();
		}
	}

	private static void ShowHome() {
		Console.WriteLine(ReadEmbedded("art_2.txt"));
		Console.WriteLine();
		Console.WriteLine("HOHO - The CLI Agent That Just Says 'OK.'");
		Console.WriteLine("Shadow Protocol Active");
	}

	private static string ReadEmbedded(string name)
	{
		var asm = typeof(Program).Assembly;
		var resource = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
		if (resource is null) return "";
		using var s = asm.GetManifestResourceStream(resource);
		if (s is null) return "";
		using var r = new StreamReader(s);
		return r.ReadToEnd();
	}

	private static void ShowVersion() {
		Console.WriteLine("hoho v0.1.0 - Native C# Shadow Protocol");
	}
}
