using System.Text.Json.Serialization;

namespace Hoho.Core.Sessions;

public enum Role { System, User, Assistant, Tool }

public sealed record Attachment(
    string Path,
    string? MediaType = null,
    string? DisplayName = null
);

public sealed record ToolCall(
    string Name,
    IReadOnlyDictionary<string, object?> Arguments
);

public sealed record Message
{
    public required Role Role { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public List<Attachment> Attachments { get; init; } = new();
    public List<ToolCall> ToolCalls { get; init; } = new();
    public string? RunId { get; init; }
}

public sealed record TranscriptEvent
{
    [JsonInclude] public required string Type { get; init; } // e.g., message, tool_start, tool_chunk, tool_end
    [JsonInclude] public required DateTimeOffset At { get; init; }
    [JsonInclude] public required object Data { get; init; }
}

