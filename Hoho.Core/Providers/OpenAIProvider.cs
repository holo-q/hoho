using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hoho.Core.Providers;

public sealed class OpenAIProvider : IChatProvider
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly HttpClient _http;
    public string Name => "openai";

    public OpenAIProvider(string apiKey, string model = "gpt-4o-mini", HttpClient? http = null)
    {
        _apiKey = apiKey;
        _model = model;
        _http = http ?? new HttpClient();
    }

    public async IAsyncEnumerable<ChatChunk> StreamAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default)
    {
        // Non-streaming for calm parity
        var url = "https://api.openai.com/v1/chat/completions";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        var body = new
        {
            model = _model,
            stream = false,
            messages = messages.Select(m => new { role = m.Role.ToString().ToLowerInvariant(), content = m.Content }),
        };
        var json = JsonSerializer.Serialize(body);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        yield return new ChatChunk(ChunkKind.Text, content);
        yield return new ChatChunk(ChunkKind.Done);
    }
}
