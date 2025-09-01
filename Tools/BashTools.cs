using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using Hoho.Core;
using Microsoft.Toolkit.HighPerformance.Buffers;

namespace Hoho.Tools;

/// <summary>
/// Bash execution tools matching Claude Code's capabilities.
/// Supports background execution, sandboxing, and output streaming.
/// </summary>

#region Input Models

public record BashInput {
	[JsonPropertyName("command")]
	public required string Command { get; init; }

	[JsonPropertyName("timeout")]
	public int? Timeout { get; init; } // milliseconds, max 600000 (10 minutes)

	[JsonPropertyName("run_in_background")]
	public bool RunInBackground { get; init; } = false;

	[JsonPropertyName("sandbox")]
	public bool Sandbox { get; init; } = false;

	[JsonPropertyName("description")]
	public string? Description { get; init; }
}

public record BashOutputInput {
	[JsonPropertyName("bash_id")]
	public required string BashId { get; init; }

	[JsonPropertyName("filter")]
	public string? Filter { get; init; } // Optional regex filter
}

public record KillBashInput {
	[JsonPropertyName("shell_id")]
	public required string ShellId { get; init; }
}

#endregion

#region Background Process Management

/// <summary>
/// Manages background bash processes with output streaming.
/// </summary>
public static class BackgroundShellManager {
	private static readonly ConcurrentDictionary<string, BackgroundShell> _shells = new();

	public class BackgroundShell {
		public Process       Process          { get; init; } = null!;
		public StringBuilder OutputBuffer     { get; init; } = new();
		public StringBuilder ErrorBuffer      { get; init; } = new();
		public DateTime      StartTime        { get; init; }
		public bool          IsComplete       { get; set; }
		public int?          ExitCode         { get; set; }
		public string        Command          { get; init; } = "";
		public int           LastReadPosition { get; set; }  = 0;
	}

	public static string StartShell(string command, bool sandbox) {
		var id = Guid.NewGuid().ToString("N")[..8]; // Short ID like Claude Code

		var processInfo = new ProcessStartInfo {
			FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
			Arguments = OperatingSystem.IsWindows()
				? $"/c {command}"
				: sandbox
					? $"-c 'set -r; {command}'" // Restricted mode for sandbox
					: $"-c '{command}'",
			RedirectStandardOutput = true,
			RedirectStandardError  = true,
			RedirectStandardInput  = true,
			UseShellExecute        = false,
			CreateNoWindow         = true,
			WorkingDirectory       = Environment.CurrentDirectory
		};

		if (sandbox) {
			// Add sandbox restrictions
			processInfo.Environment["PATH"] = "/usr/bin:/bin"; // Limited PATH
			// Could add more restrictions with firejail, docker, etc.
		}

		var process = new Process { StartInfo = processInfo };
		var shell = new BackgroundShell {
			Process   = process,
			StartTime = DateTime.UtcNow,
			Command   = command
		};

		// Start async output collection
		process.OutputDataReceived += (sender, e) => {
			if (e.Data != null) {
				lock (shell.OutputBuffer) {
					shell.OutputBuffer.AppendLine(e.Data);
				}
			}
		};

		process.ErrorDataReceived += (sender, e) => {
			if (e.Data != null) {
				lock (shell.ErrorBuffer) {
					shell.ErrorBuffer.AppendLine(e.Data);
				}
			}
		};

		process.Exited += (sender, e) => {
			shell.IsComplete = true;
			shell.ExitCode   = process.ExitCode;
		};

		process.EnableRaisingEvents = true;
		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		_shells[id] = shell;

		Logger.Info("Started background shell {ShellId} for command: {Command}", id, command);

		return id;
	}

	public static BackgroundShell? GetShell(string id) => _shells.GetValueOrDefault(id);

	public static bool KillShell(string id) {
		if (!_shells.TryRemove(id, out var shell))
			return false;

		try {
			if (!shell.Process.HasExited) {
				shell.Process.Kill(entireProcessTree: true);
				shell.Process.WaitForExit(5000);
			}
			shell.Process.Dispose();
			Logger.Info("Killed shell {ShellId}", id);
			return true;
		} catch (Exception ex) {
			Logger.Error(ex, "Failed to kill shell {ShellId}", id);
			return false;
		}
	}

	public static Dictionary<string, object> GetAllShells() {
		return _shells.ToDictionary(
			kvp => kvp.Key,
			kvp => (object)new {
				command    = kvp.Value.Command,
				is_running = !kvp.Value.Process.HasExited,
				start_time = kvp.Value.StartTime,
				exit_code  = kvp.Value.ExitCode
			});
	}
}

#endregion

/// <summary>
/// Bash tool - executes shell commands with optional background execution.
/// </summary>
public class BashTool : HohoTool<BashInput> {
	public override string Name        => "bash";
	public override string Description => "Execute bash commands with optional background execution and sandboxing";

	private const int MaxTimeout     = 600000; // 10 minutes max
	private const int DefaultTimeout = 120000; // 2 minutes default

	protected override async Task<ToolResult> ExecuteInternalAsync(BashInput input, CancellationToken cancellationToken) {
		using var timer = Logger.TimeOperation($"Bash: {input.Description ?? input.Command}");

		if (input.RunInBackground) {
			// Background execution - return immediately with shell ID
			var shellId = BackgroundShellManager.StartShell(input.Command, input.Sandbox);

			return ToolResult.Ok(
				$"Started background shell with ID: {shellId}\nUse bash_output tool to read output.",
				new { shell_id = shellId });
		}

		// Foreground execution
		var timeout = Math.Min(input.Timeout ?? DefaultTimeout, MaxTimeout);

		try {
			using var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
					Arguments = OperatingSystem.IsWindows()
						? $"/c {input.Command}"
						: input.Sandbox
							? $"-c 'set -r; {input.Command}'"
							: $"-c '{input.Command}'",
					RedirectStandardOutput = true,
					RedirectStandardError  = true,
					UseShellExecute        = false,
					CreateNoWindow         = true,
					WorkingDirectory       = Environment.CurrentDirectory
				}
			};

			if (input.Sandbox) {
				process.StartInfo.Environment["PATH"] = "/usr/bin:/bin";
			}

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

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(timeout);

			try {
				await process.WaitForExitAsync(cts.Token);
			} catch (OperationCanceledException) {
				process.Kill(entireProcessTree: true);
				return ToolResult.Fail($"Command timed out after {timeout}ms");
			}

			var output = outputBuilder.ToString();
			var error  = errorBuilder.ToString();

			// Combine output and error like Claude Code
			var result = output;
			if (!string.IsNullOrWhiteSpace(error)) {
				result += $"\n--- stderr ---\n{error}";
			}

			// Truncate output if too large (30000 chars like Claude Code)
			if (result.Length > 30000) {
				result = result[..30000] + "\n... (output truncated)";
			}

			Logger.Info("Executed command with exit code {ExitCode}", process.ExitCode);

			if (process.ExitCode != 0) {
				return ToolResult.Ok($"Exit code: {process.ExitCode}\n{result}");
			}

			return ToolResult.Ok(result);
		} catch (Exception ex) {
			Logger.Error(ex, "Failed to execute command: {Command}", input.Command);
			return ToolResult.Fail($"Command execution failed: {ex.Message}");
		}
	}

	protected override bool ValidateInputInternal(BashInput input, out string? error) {
		error = null;

		if (string.IsNullOrWhiteSpace(input.Command)) {
			error = "Command is required";
			return false;
		}

		if (input.Timeout.HasValue && (input.Timeout <= 0 || input.Timeout > MaxTimeout)) {
			error = $"Timeout must be between 1 and {MaxTimeout}ms";
			return false;
		}

		return true;
	}
}

/// <summary>
/// BashOutput tool - reads output from background shells.
/// </summary>
public class BashOutputTool : HohoTool<BashOutputInput> {
	public override string Name        => "bash_output";
	public override string Description => "Read output from a background shell process";

	protected override Task<ToolResult> ExecuteInternalAsync(BashOutputInput input, CancellationToken cancellationToken) {
		var shell = BackgroundShellManager.GetShell(input.BashId);

		if (shell == null) {
			return Task.FromResult(ToolResult.Fail($"Shell not found: {input.BashId}"));
		}

		string output;
		string error;

		lock (shell.OutputBuffer) {
			output = shell.OutputBuffer.ToString();
		}

		lock (shell.ErrorBuffer) {
			error = shell.ErrorBuffer.ToString();
		}

		// Get only new output since last read
		var newOutput = output.Length > shell.LastReadPosition
			? output[shell.LastReadPosition..]
			: "";

		shell.LastReadPosition = output.Length;

		// Apply regex filter if provided
		if (!string.IsNullOrEmpty(input.Filter)) {
			try {
				var regex    = new System.Text.RegularExpressions.Regex(input.Filter);
				var lines    = newOutput.Split('\n');
				var filtered = lines.Where(line => regex.IsMatch(line));
				newOutput = string.Join('\n', filtered);
			} catch (Exception ex) {
				Logger.Warn($"Invalid regex filter: {input.Filter} - {ex.Message}");
			}
		}

		var status = shell.IsComplete
			? $"completed (exit code: {shell.ExitCode})"
			: "running";

		var result = $"Status: {status}\n{newOutput}";

		if (!string.IsNullOrWhiteSpace(error)) {
			result += $"\n--- stderr ---\n{error}";
		}

		return Task.FromResult(ToolResult.Ok(result, new {
			status,
			exit_code       = shell.ExitCode,
			has_more_output = !shell.IsComplete
		}));
	}

	protected override bool ValidateInputInternal(BashOutputInput input, out string? error) {
		error = null;

		if (string.IsNullOrWhiteSpace(input.BashId)) {
			error = "Bash ID is required";
			return false;
		}

		return true;
	}
}

/// <summary>
/// KillBash tool - terminates background shell processes.
/// </summary>
public class KillBashTool : HohoTool<KillBashInput> {
	public override string Name        => "kill_bash";
	public override string Description => "Kill a background shell process";

	protected override Task<ToolResult> ExecuteInternalAsync(KillBashInput input, CancellationToken cancellationToken) {
		if (BackgroundShellManager.KillShell(input.ShellId)) {
			return Task.FromResult(ToolResult.Ok($"Successfully killed shell: {input.ShellId}"));
		}

		return Task.FromResult(ToolResult.Fail($"Shell not found or already terminated: {input.ShellId}"));
	}

	protected override bool ValidateInputInternal(KillBashInput input, out string? error) {
		error = null;

		if (string.IsNullOrWhiteSpace(input.ShellId)) {
			error = "Shell ID is required";
			return false;
		}

		return true;
	}
}