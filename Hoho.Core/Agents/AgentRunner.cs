using System.Text.Json;
using Hoho.Core.Providers;
using Hoho.Core.Sessions;

namespace Hoho.Core.Agents;

public sealed class AgentRunner
{
    private readonly IChatProvider _provider;
    private readonly TranscriptStore _store;

    public AgentRunner(IChatProvider provider, TranscriptStore store)
    {
        _provider = provider;
        _store = store;
    }

    public async Task RunOnceAsync(string sessionId, string userContent, Action<string>? onText = null, string? systemPrompt = null, CancellationToken ct = default)
    {
        var history = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            history.Add(new ChatMessage(Role.System, systemPrompt!));
        }
        await foreach (var ev in _store.ReadAllAsync(sessionId, ct))
        {
            if (ev.Type == "message" && ev.Data is JsonElement el && el.TryGetProperty("Role", out _))
            {
                try
                {
                    var msg = el.Deserialize<Message>();
                    if (msg is not null)
                    {
                        history.Add(new ChatMessage(msg.Role, msg.Content));
                    }
                }
                catch { /* ignore malformed lines */ }
            }
        }

        var userMsg = new Message { Role = Role.User, Content = userContent };
        await _store.AppendAsync(sessionId, new[] { new TranscriptEvent { Type = "message", At = DateTimeOffset.UtcNow, Data = userMsg } }, ct);
        history.Add(new ChatMessage(Role.User, userContent));

        var assistantBuffer = new System.Text.StringBuilder();
        await foreach (var chunk in _provider.StreamAsync(history, ct))
        {
            switch (chunk.Kind)
            {
                case ChunkKind.Text:
                    assistantBuffer.Append(chunk.Text);
                    if (chunk.Text is { Length: > 0 }) onText?.Invoke(chunk.Text);
                    await _store.AppendAsync(sessionId, new[] { new TranscriptEvent { Type = "assistant_chunk", At = DateTimeOffset.UtcNow, Data = chunk.Text ?? string.Empty } }, ct);
                    break;
                case ChunkKind.ToolCall:
                    await _store.AppendAsync(sessionId, new[] { new TranscriptEvent { Type = "tool_call", At = DateTimeOffset.UtcNow, Data = new { name = chunk.ToolName, args = chunk.ToolArgs } } }, ct);
                    break;
                case ChunkKind.Error:
                    await _store.AppendAsync(sessionId, new[] { new TranscriptEvent { Type = "error", At = DateTimeOffset.UtcNow, Data = chunk.Text ?? "error" } }, ct);
                    break;
                case ChunkKind.Done:
                    break;
            }
        }

        var assistantMsg = new Message { Role = Role.Assistant, Content = assistantBuffer.ToString() };
        await _store.AppendAsync(sessionId, new[] { new TranscriptEvent { Type = "message", At = DateTimeOffset.UtcNow, Data = assistantMsg } }, ct);
    }
}
