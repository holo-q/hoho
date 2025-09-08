namespace Hoho.Core.Providers;

public sealed class EchoProvider : IChatProvider
{
    public string Name => "echo";

    public async IAsyncEnumerable<ChatChunk> StreamAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default)
    {
        // Very simple provider that echoes the last user message
        var last = messages.LastOrDefault(m => m.Role == Role.User);
        var text = last?.Content ?? string.Empty;
        foreach (var ch in text.Chunk(64))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(1, ct);
            yield return new ChatChunk(ChunkKind.Text, new string(ch));
        }
        yield return new ChatChunk(ChunkKind.Done);
    }
}

