using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Hoho.Core;
using Hoho.Decomp;

namespace Hoho.Decomp.Tests.Integration;

/// <summary>
/// Base class for CLI integration tests with common utilities
/// </summary>
public abstract class CliIntegrationTestBase : IDisposable {
	protected readonly string                     TestDirectory;
	protected readonly string                     TestDbPath;
	protected readonly string                     TestJsonPath;
	protected readonly MessagePackMappingDatabase TestDatabase;

	protected CliIntegrationTestBase() {
		// Create unique test directory for each test
		TestDirectory = Path.Combine(Path.GetTempPath(), $"hoho-test-{Guid.NewGuid()}");
		Directory.CreateDirectory(TestDirectory);

		TestDbPath   = Path.Combine(TestDirectory, "test-mappings.msgpack");
		TestJsonPath = Path.Combine(TestDirectory, "test-mappings.json");
		TestDatabase = new MessagePackMappingDatabase(TestDbPath);
	}

	/// <summary>
	/// Execute a CLI command and return the result
	/// </summary>
	protected async Task<CliCommandResult> ExecuteCliCommandAsync(string command, string arguments = "", bool expectSuccess = true) {
		var startInfo = new ProcessStartInfo {
			FileName               = "dotnet",
			Arguments              = $"run --project /home/nuck/holoq/repositories/hoho/Hoho/Hoho.csproj -- {command} {arguments}",
			RedirectStandardOutput = true,
			RedirectStandardError  = true,
			RedirectStandardInput  = true,
			UseShellExecute        = false,
			CreateNoWindow         = true,
			WorkingDirectory       = TestDirectory
		};

		var process       = new Process { StartInfo = startInfo };
		var outputBuilder = new StringBuilder();
		var errorBuilder  = new StringBuilder();

		process.OutputDataReceived += (sender, e) => {
			if (e.Data != null) outputBuilder.AppendLine(e.Data);
		};
		process.ErrorDataReceived += (sender, e) => {
			if (e.Data != null) errorBuilder.AppendLine(e.Data);
		};

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		await process.WaitForExitAsync();

		var result = new CliCommandResult {
			ExitCode       = process.ExitCode,
			StandardOutput = outputBuilder.ToString(),
			StandardError  = errorBuilder.ToString(),
			Success        = process.ExitCode == 0
		};

		if (expectSuccess && !result.Success) {
			throw new Exception($"CLI command failed: {command} {arguments}\nExit Code: {result.ExitCode}\nStdOut: {result.StandardOutput}\nStdErr: {result.StandardError}");
		}

		return result;
	}

	/// <summary>
	/// Create a test MessagePack database with sample data
	/// </summary>
	protected void CreateSampleDatabase(int mappingCount = 100) {
		var random = new Random(42); // Deterministic for testing

		var contexts = new[] { "global", "ReactModule", "DatabaseLayer", "UtilityClass", "EventHandler" };
		var types    = Enum.GetValues<SymbolType>();

		for (int i = 0; i < mappingCount; i++) {
			var original   = $"obf_{i:D3}";
			var mapped     = $"readable_{i:D3}";
			var context    = contexts[i % contexts.Length];
			var type       = types[i % types.Length];
			var confidence = 0.5 + (random.NextDouble() * 0.5); // 0.5 to 1.0

			TestDatabase.AddMapping(original, mapped, type, context, confidence);
		}

		TestDatabase.SaveAsync().Wait();
	}

	/// <summary>
	/// Create a test JSON mapping file for migration testing
	/// </summary>
	protected async Task CreateSampleJsonMappingsAsync(int mappingCount = 50) {
		var mappings = new Dictionary<string, object>();

		for (int i = 0; i < mappingCount; i++) {
			var key = $"json_obf_{i:D3}";
			mappings[key] = new {
				mapped     = $"json_readable_{i:D3}",
				type       = "function",
				context    = "global",
				confidence = 0.8,
				usageCount = i + 1
			};
		}

		var json = System.Text.Json.JsonSerializer.Serialize(mappings, new System.Text.Json.JsonSerializerOptions {
			WriteIndented = true
		});

		await File.WriteAllTextAsync(TestJsonPath, json);
	}

	/// <summary>
	/// Create a large database for performance testing
	/// </summary>
	protected void CreateLargeDatabase(int mappingCount = 10000) {
		var random   = new Random(42);
		var contexts = Enumerable.Range(0, 50).Select(i => $"Context_{i}").ToArray();
		var types    = Enum.GetValues<SymbolType>();

		for (int i = 0; i < mappingCount; i++) {
			var original   = $"large_obf_{i:D5}";
			var mapped     = $"large_readable_symbol_with_longer_name_{i:D5}";
			var context    = contexts[i % contexts.Length];
			var type       = types[i % types.Length];
			var confidence = random.NextDouble();

			TestDatabase.AddMapping(original, mapped, type, context, confidence);

			// Occasionally save to test incremental operations
			if (i % 1000 == 0) {
				TestDatabase.SaveAsync().Wait();
			}
		}

		TestDatabase.SaveAsync().Wait();
	}

	/// <summary>
	/// Create a corrupted database file for error testing
	/// </summary>
	protected async Task CreateCorruptedDatabaseAsync() {
		await File.WriteAllBytesAsync(TestDbPath, new byte[] { 0x00, 0x01, 0x02, 0xFF });
	}

	public virtual void Dispose() {
		try {
			if (Directory.Exists(TestDirectory)) {
				Directory.Delete(TestDirectory, true);
			}
		} catch {
			// Ignore cleanup errors in tests
		}
	}
}

public class CliCommandResult {
	public int    ExitCode       { get; set; }
	public string StandardOutput { get; set; } = string.Empty;
	public string StandardError  { get; set; } = string.Empty;
	public bool   Success        { get; set; }
}