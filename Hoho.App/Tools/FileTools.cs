using System.Text;
using System.Text.Json.Serialization;
using Serilog;
using NetFabric.Hyperlinq;
using Microsoft.Toolkit.HighPerformance;

namespace Hoho.Tools;

/// <summary>
/// File operation tools matching Claude Code's string-based editing approach.
/// HIGH-PERFORMANCE implementation with zero-allocation patterns.
/// </summary>

#region Input Models

public record FileReadInput {
	[JsonPropertyName("file_path")]
	public required string FilePath { get; init; }

	[JsonPropertyName("offset")]
	public int? Offset { get; init; }

	[JsonPropertyName("limit")]
	public int? Limit { get; init; }
}

public record FileWriteInput {
	[JsonPropertyName("file_path")]
	public required string FilePath { get; init; }

	[JsonPropertyName("content")]
	public required string Content { get; init; }
}

public record FileEditInput {
	[JsonPropertyName("file_path")]
	public required string FilePath { get; init; }

	[JsonPropertyName("old_string")]
	public required string OldString { get; init; }

	[JsonPropertyName("new_string")]
	public required string NewString { get; init; }

	[JsonPropertyName("replace_all")]
	public bool ReplaceAll { get; init; } = false;
}

public record FileMultiEditInput {
	[JsonPropertyName("file_path")]
	public required string FilePath { get; init; }

	[JsonPropertyName("edits")]
	public required List<FileEditOperation> Edits { get; init; }
}

public record FileEditOperation {
	[JsonPropertyName("old_string")]
	public required string OldString { get; init; }

	[JsonPropertyName("new_string")]
	public required string NewString { get; init; }

	[JsonPropertyName("replace_all")]
	public bool ReplaceAll { get; init; } = false;
}

#endregion

/// <summary>
/// FileRead tool - reads files with optional offset/limit for large files.
/// </summary>
public class FileReadTool : HohoTool<FileReadInput> {
	public override string Name        => "file_read";
	public override string Description => "Read a file from the local filesystem with optional line offset and limit";

	protected override async Task<ToolResult> ExecuteInternalAsync(FileReadInput input, CancellationToken cancellationToken) {
        Log.Information("FileRead: {File}", input.FilePath);

		if (!File.Exists(input.FilePath)) {
			return ToolResult.Fail($"File not found: {input.FilePath}");
		}

		try {
			string content;

			if (input.Offset.HasValue || input.Limit.HasValue) {
				// Read with line offset/limit for large files
				var lines = await File.ReadAllLinesAsync(input.FilePath, cancellationToken);

				var offset = input.Offset ?? 0;
				var limit  = input.Limit ?? lines.Length;

				if (offset >= lines.Length) {
					return ToolResult.Ok(""); // Empty if offset beyond file
				}

				// Use Hyperlinq for zero-allocation processing
				var selectedLines = lines.AsValueEnumerable()
					.Skip(offset)
					.Take(limit)
					.Select((line, index) => $"{offset + index + 1,6}→{line}") // Line numbers like Claude Code
					.ToArray();

				content = string.Join(Environment.NewLine, selectedLines);
			} else {
				// Read entire file with line numbers - using Hyperlinq
				var lines = await File.ReadAllLinesAsync(input.FilePath, cancellationToken);
				var numberedLines = lines.AsValueEnumerable()
					.Select((line, index) => $"{index + 1,6}→{line}")
					.ToArray();
				content = string.Join(Environment.NewLine, numberedLines);
			}

            Log.Information("Read {ByteCount} bytes from {FilePath}",
                Encoding.UTF8.GetByteCount(content), input.FilePath);

			return ToolResult.Ok(content);
        } catch (Exception ex) {
            Log.Error(ex, "Failed to read file {FilePath}", input.FilePath);
            return ToolResult.Fail($"Error reading file: {ex.Message}");
        }
	}

	protected override bool ValidateInputInternal(FileReadInput input, out string? error) {
		error = null;

		if (string.IsNullOrWhiteSpace(input.FilePath)) {
			error = "File path is required";
			return false;
		}

		if (!Path.IsPathFullyQualified(input.FilePath)) {
			error = "File path must be absolute";
			return false;
		}

		if (input.Offset < 0) {
			error = "Offset must be non-negative";
			return false;
		}

		if (input.Limit <= 0) {
			error = "Limit must be positive";
			return false;
		}

		return true;
	}
}

/// <summary>
/// FileWrite tool - writes content to files.
/// </summary>
public class FileWriteTool : HohoTool<FileWriteInput> {
	public override string Name        => "file_write";
	public override string Description => "Write content to a file, creating or overwriting it";

	protected override async Task<ToolResult> ExecuteInternalAsync(FileWriteInput input, CancellationToken cancellationToken) {
        Log.Information("FileWrite: {File}", input.FilePath);

		try {
			// Ensure directory exists
			var directory = Path.GetDirectoryName(input.FilePath);
			if (!string.IsNullOrEmpty(directory)) {
				Directory.CreateDirectory(directory);
			}

			await File.WriteAllTextAsync(input.FilePath, input.Content, cancellationToken);

            Log.Information("Wrote {ByteCount} bytes to {FilePath}",
                Encoding.UTF8.GetByteCount(input.Content), input.FilePath);

			return ToolResult.Ok($"Successfully wrote to {input.FilePath}");
        } catch (Exception ex) {
            Log.Error(ex, "Failed to write file {FilePath}", input.FilePath);
            return ToolResult.Fail($"Error writing file: {ex.Message}");
        }
	}

	protected override bool ValidateInputInternal(FileWriteInput input, out string? error) {
		error = null;

		if (string.IsNullOrWhiteSpace(input.FilePath)) {
			error = "File path is required";
			return false;
		}

		if (!Path.IsPathFullyQualified(input.FilePath)) {
			error = "File path must be absolute";
			return false;
		}

		if (input.Content == null) {
			error = "Content is required";
			return false;
		}

		return true;
	}
}

/// <summary>
/// FileEdit tool - STRING-BASED editing (Claude Code's key innovation).
/// Uses old_string → new_string replacement, NOT line numbers!
/// </summary>
public class FileEditTool : HohoTool<FileEditInput> {
	public override string Name        => "file_edit";
	public override string Description => "Edit a file using string-based find and replace (NOT line-based)";

	protected override async Task<ToolResult> ExecuteInternalAsync(FileEditInput input, CancellationToken cancellationToken) {
        Log.Information("FileEdit: {File}", input.FilePath);

		if (!File.Exists(input.FilePath)) {
			return ToolResult.Fail($"File not found: {input.FilePath}");
		}

		try {
			var content = await File.ReadAllTextAsync(input.FilePath, cancellationToken);

			// Check if old string exists
			if (!content.Contains(input.OldString)) {
				return ToolResult.Fail($"String not found in file: {input.OldString}");
			}

			// STRING-BASED REPLACEMENT - The Claude Code approach
			string newContent;
			if (input.ReplaceAll) {
				newContent = content.Replace(input.OldString, input.NewString);
                Log.Information("Replaced all occurrences in {FilePath}", input.FilePath);
			} else {
				// Replace only first occurrence
				var index = content.IndexOf(input.OldString);
				if (index >= 0) {
					newContent = content.Remove(index, input.OldString.Length)
						.Insert(index, input.NewString);
				} else {
					return ToolResult.Fail("String not found");
				}
                Log.Information("Replaced first occurrence in {FilePath}", input.FilePath);
			}

			// Only write if content actually changed
			if (content == newContent) {
				return ToolResult.Fail("No changes made - replacement resulted in same content");
			}

			await File.WriteAllTextAsync(input.FilePath, newContent, cancellationToken);

			return ToolResult.Ok($"Successfully edited {input.FilePath}");
        } catch (Exception ex) {
            Log.Error(ex, "Failed to edit file {FilePath}", input.FilePath);
            return ToolResult.Fail($"Error editing file: {ex.Message}");
        }
	}

	protected override bool ValidateInputInternal(FileEditInput input, out string? error) {
		error = null;

		if (string.IsNullOrWhiteSpace(input.FilePath)) {
			error = "File path is required";
			return false;
		}

		if (!Path.IsPathFullyQualified(input.FilePath)) {
			error = "File path must be absolute";
			return false;
		}

		if (input.OldString == null) {
			error = "Old string is required";
			return false;
		}

		if (input.NewString == null) {
			error = "New string is required";
			return false;
		}

		if (input.OldString == input.NewString) {
			error = "Old string and new string cannot be the same";
			return false;
		}

		return true;
	}
}

/// <summary>
/// FileMultiEdit tool - multiple string-based edits in a single transaction.
/// </summary>
public class FileMultiEditTool : HohoTool<FileMultiEditInput> {
	public override string Name        => "file_multi_edit";
	public override string Description => "Apply multiple string-based edits to a file in sequence";

	protected override async Task<ToolResult> ExecuteInternalAsync(FileMultiEditInput input, CancellationToken cancellationToken) {
        Log.Information("FileMultiEdit: {File}", input.FilePath);

		if (!File.Exists(input.FilePath)) {
			return ToolResult.Fail($"File not found: {input.FilePath}");
		}

		try {
			var content         = await File.ReadAllTextAsync(input.FilePath, cancellationToken);
			var originalContent = content;

			// Apply edits in sequence
			foreach (var edit in input.Edits) {
				if (!content.Contains(edit.OldString)) {
					return ToolResult.Fail($"String not found: {edit.OldString}");
				}

				if (edit.ReplaceAll) {
					content = content.Replace(edit.OldString, edit.NewString);
				} else {
					var index = content.IndexOf(edit.OldString);
					if (index >= 0) {
						content = content.Remove(index, edit.OldString.Length)
							.Insert(index, edit.NewString);
					}
				}
			}

			if (content == originalContent) {
				return ToolResult.Fail("No changes made");
			}

			await File.WriteAllTextAsync(input.FilePath, content, cancellationToken);

            Log.Information("Applied {EditCount} edits to {FilePath}", input.Edits.Count, input.FilePath);
			return ToolResult.Ok($"Successfully applied {input.Edits.Count} edits to {input.FilePath}");
        } catch (Exception ex) {
            Log.Error(ex, "Failed to apply multi-edit to {FilePath}", input.FilePath);
            return ToolResult.Fail($"Error applying edits: {ex.Message}");
        }
	}

	protected override bool ValidateInputInternal(FileMultiEditInput input, out string? error) {
		error = null;

		if (string.IsNullOrWhiteSpace(input.FilePath)) {
			error = "File path is required";
			return false;
		}

		if (!Path.IsPathFullyQualified(input.FilePath)) {
			error = "File path must be absolute";
			return false;
		}

		if (input.Edits == null || input.Edits.Count == 0) {
			error = "At least one edit is required";
			return false;
		}

		foreach (var edit in input.Edits) {
			if (edit.OldString == null || edit.NewString == null) {
				error = "All edits must have old_string and new_string";
				return false;
			}

			if (edit.OldString == edit.NewString) {
				error = "Old string and new string cannot be the same";
				return false;
			}
		}

		return true;
	}
}
