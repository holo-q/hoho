using System.Text.Json.Serialization;

namespace Hoho.Tools;

/// <summary>
/// Base interface for all HOHO tools.
/// HIGH-PERFORMANCE implementation with zero-allocation patterns.
/// </summary>
public interface IHohoTool {
	/// <summary>
	/// Tool name as exposed to the LLM.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Tool description for the LLM.
	/// </summary>
	string Description { get; }

	/// <summary>
	/// Execute the tool with given input.
	/// </summary>
	Task<ToolResult> ExecuteAsync(object input, CancellationToken cancellationToken = default);

	/// <summary>
	/// Validate input before execution.
	/// </summary>
	bool ValidateInput(object input, out string? error);
}

/// <summary>
/// Result from tool execution.
/// </summary>
public record ToolResult {
	public bool    Success { get; init; }
	public string? Output  { get; init; }
	public string? Error   { get; init; }
	public object? Data    { get; init; }

	public static ToolResult Ok(string output, object? data = null)
		=> new() { Success = true, Output = output, Data = data };

	public static ToolResult Fail(string error)
		=> new() { Success = false, Error = error };
}

/// <summary>
/// Base class for tools with typed input.
/// </summary>
public abstract class HohoTool<TInput> : IHohoTool where TInput : class {
	public abstract string Name        { get; }
	public abstract string Description { get; }

	public async Task<ToolResult> ExecuteAsync(object input, CancellationToken cancellationToken = default) {
		if (input is not TInput typedInput) {
			return ToolResult.Fail($"Invalid input type. Expected {typeof(TInput).Name}");
		}

		if (!ValidateInput(typedInput, out var error)) {
			return ToolResult.Fail(error ?? "Input validation failed");
		}

		return await ExecuteInternalAsync(typedInput, cancellationToken);
	}

	public bool ValidateInput(object input, out string? error) {
		if (input is not TInput typedInput) {
			error = $"Invalid input type. Expected {typeof(TInput).Name}";
			return false;
		}

		return ValidateInputInternal(typedInput, out error);
	}

	protected abstract Task<ToolResult> ExecuteInternalAsync(TInput  input, CancellationToken cancellationToken);
	protected abstract bool             ValidateInputInternal(TInput input, out string?       error);
}