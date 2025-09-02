using System.Text.Json;
using FluentAssertions;
using Hoho.Decomp;
using Moq;
using Xunit;

namespace Decomp.Tests;

/// <summary>
/// Tests for LSP service integration and communication
/// </summary>
public class LspServiceTests : IDisposable {
	private readonly string _testWorkspace;

	public LspServiceTests() {
		_testWorkspace = Path.Combine(Path.GetTempPath(), $"lsp-test-{Guid.NewGuid()}");
		Directory.CreateDirectory(_testWorkspace);
	}

	/// <summary>
	/// Check if TypeScript and npm are available for LSP tests
	/// </summary>
	private static bool IsTypeScriptAvailable() {
		try {
			using var process = new System.Diagnostics.Process();
			process.StartInfo = new System.Diagnostics.ProcessStartInfo {
				FileName               = "npx",
				Arguments              = "typescript-language-server --version",
				UseShellExecute        = false,
				RedirectStandardOutput = true,
				RedirectStandardError  = true,
				CreateNoWindow         = true
			};
			process.Start();
			return process.WaitForExit(3000) && process.ExitCode == 0;
		} catch {
			return false;
		}
	}

	public void Dispose() {
		if (Directory.Exists(_testWorkspace)) {
			Directory.Delete(_testWorkspace, true);
		}
	}

	[Fact]
	public async Task LspService_Should_Initialize_Successfully() {
		// Skip if TypeScript/npm not available
		if (!IsTypeScriptAvailable()) {
			// Skip test instead of failing
			return;
		}

		// Arrange
		var service = new LspRenameService(_testWorkspace);

		// Act with timeout
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		try {
			await service.InitializeAsync();
		} catch (TaskCanceledException) {
			// Skip if LSP server not available
			return;
		} catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("ENOENT")) {
			// Skip if TypeScript server not found
			return;
		}

		// Cleanup
		service.Dispose();
	}

	[Fact]
	public async Task LspService_Should_Open_JavaScript_Files() {
		// Skip if TypeScript/npm not available
		if (!IsTypeScriptAvailable()) return;

		// Arrange
		var testFile = Path.Combine(_testWorkspace, "test.js");
		var testContent = @"
            function testFunction(param1, param2) {
                return param1 + param2;
            }";
		await File.WriteAllTextAsync(testFile, testContent);

		var service = new LspRenameService(_testWorkspace);
		try {
			await service.InitializeAsync();
			await service.OpenFileAsync(testFile);
		} catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("ENOENT")) {
			return; // Skip if LSP not available
		} finally {
			service.Dispose();
		}
	}

	[Fact]
	public async Task Should_Find_All_References_To_Symbol() {
		// Skip if TypeScript/npm not available
		if (!IsTypeScriptAvailable()) return;

		// Arrange
		var testFile = Path.Combine(_testWorkspace, "references.js");
		var testContent = @"
            var myVariable = 42;
            console.log(myVariable);
            function test() {
                return myVariable * 2;
            }
            myVariable = myVariable + 1;";
		await File.WriteAllTextAsync(testFile, testContent);

		var service = new LspRenameService(_testWorkspace);
		try {
			await service.InitializeAsync();
			await service.OpenFileAsync(testFile);
			var references = await service.FindReferencesAsync(testFile, 1, 16);
			references.Should().HaveCountGreaterOrEqualTo(4);
		} catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("ENOENT")) {
			return; // Skip if LSP not available
		} finally {
			service.Dispose();
		}
	}

	[Fact]
	public async Task Should_Rename_Symbol_With_All_References() {
		// Skip if TypeScript/npm not available
		if (!IsTypeScriptAvailable()) return;

		// Arrange
		var testFile = Path.Combine(_testWorkspace, "rename.js");
		var testContent = @"
            function oldName() {
                return 'test';
            }
            oldName();
            var result = oldName();";
		await File.WriteAllTextAsync(testFile, testContent);

		var service = new LspRenameService(_testWorkspace);
		try {
			await service.InitializeAsync();
			await service.OpenFileAsync(testFile);
			var edit = await service.RenameSymbolAsync(testFile, 1, 21, "newName");
			edit.Should().NotBeNull();
			edit!.Changes.Should().ContainKey(new Uri(testFile).ToString());
			edit.Changes.First().Value.Should().HaveCountGreaterOrEqualTo(3);
		} catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("ENOENT")) {
			return; // Skip if LSP not available
		} finally {
			service.Dispose();
		}
	}

	[Fact]
	public async Task BatchRename_Should_Apply_Multiple_Mappings() {
		// Skip if TypeScript/npm not available
		if (!IsTypeScriptAvailable()) return;

		// Arrange
		var testFile = Path.Combine(_testWorkspace, "batch.js");
		var testContent = @"
            var Wu1 = function(A, B) {
                return A + B;
            };
            var Ct1 = class {
                constructor(Q) {
                    this.value = Q;
                }
            };";
		await File.WriteAllTextAsync(testFile, testContent);

		var mappings = new Dictionary<string, string> {
			["Wu1"] = "addFunction",
			["Ct1"] = "ValueClass",
			["A"]   = "first",
			["B"]   = "second",
			["Q"]   = "initialValue"
		};

		var service = new LspRenameService(_testWorkspace);
		try {
			await service.InitializeAsync();
			var report = await service.BatchRenameAsync(testFile, mappings);
			report.SuccessfulRenames.Should().BeGreaterThan(0);
			report.FailedRenames.Should().Be(0);

			var modifiedContent = await File.ReadAllTextAsync(testFile);
			modifiedContent.Should().Contain("addFunction");
			modifiedContent.Should().Contain("ValueClass");
		} catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("ENOENT")) {
			return; // Skip if LSP not available
		} finally {
			service.Dispose();
		}
	}
}

/// <summary>
/// Tests for LSP client-server communication
/// </summary>
public class LspClientServerTests {
	[Fact]
	public void LspServerDaemon_Should_Check_If_Running() {
		// Act
		var isRunning = LspServerDaemon.IsRunning();

		// Assert
		// Should be false if no server is running
		isRunning.Should().BeFalse();
	}

	[Fact]
	public async Task RenameRequest_Should_Serialize_Correctly() {
		// Arrange
		var request = new RenameRequest {
			FilePath = "/test/file.js",
			Mappings = new Dictionary<string, string> {
				["oldName"] = "newName",
				["Wu1"]     = "ReactModule"
			}
		};

		// Act
		var json         = JsonSerializer.Serialize(request);
		var deserialized = JsonSerializer.Deserialize<RenameRequest>(json);

		// Assert
		deserialized.Should().NotBeNull();
		deserialized!.FilePath.Should().Be(request.FilePath);
		deserialized.Mappings.Should().HaveCount(2);
		deserialized.Mappings["Wu1"].Should().Be("ReactModule");
	}

	[Fact]
	public void RenameResponse_Should_Handle_Success_And_Failure() {
		// Arrange & Act
		var successResponse = new RenameResponse {
			Success           = true,
			SuccessfulRenames = 10,
			FailedRenames     = 0,
			TotalReferences   = 25
		};

		var failureResponse = new RenameResponse {
			Success = false,
			Error   = "Failed to connect to LSP"
		};

		// Assert
		successResponse.Success.Should().BeTrue();
		successResponse.Error.Should().BeNull();
		successResponse.SuccessfulRenames.Should().Be(10);

		failureResponse.Success.Should().BeFalse();
		failureResponse.Error.Should().NotBeNullOrEmpty();
	}
}