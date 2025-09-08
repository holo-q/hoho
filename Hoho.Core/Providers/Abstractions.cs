namespace Hoho.Core.Providers;

public enum ChunkKind { Text, ToolCall, Error, Done }

public sealed record ChatChunk(ChunkKind Kind, string? Text = null, string? ToolName = null, IReadOnlyDictionary<string, object?>? ToolArgs = null);

public sealed record ChatMessage(Role Role, string Content);

public interface IChatProvider
{
    IAsyncEnumerable<ChatChunk> StreamAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default);
    string Name { get; }
}

