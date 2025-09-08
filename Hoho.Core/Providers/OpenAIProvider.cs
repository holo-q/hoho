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
        // Try streaming via SSE first; fallback to non-streaming
        var url = "https://api.openai.com/v1/chat/completions";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            var bodyStream = new
            {
                model = _model,
                stream = true,
                messages = messages.Select(m => new { role = m.Role.ToString().ToLowerInvariant(), content = m.Content }),
            };
            var jsonStream = JsonSerializer.Serialize(bodyStream);
            req.Content = new StringContent(jsonStream, Encoding.UTF8, "application/json");
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            res.EnsureSuccessStatusCode();
            using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                ct.ThrowIfCancellationRequested();
                if (line.StartsWith("data: "))
                {
                    var payload = line.Substring(6).Trim();
                    if (payload == "[DONE]") break;
                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentEl))
                        {
                            var t = contentEl.GetString();
                            if (!string.IsNullOrEmpty(t)) yield return new ChatChunk(ChunkKind.Text, t);
                        }
                    }
                    catch { /* ignore malformed lines */ }
                }
            }
            yield return new ChatChunk(ChunkKind.Done);
            yield break;
        }
        catch
        {
            // fall through to non-streaming
        }

        using (var req = new HttpRequestMessage(HttpMethod.Post, url))
        {
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
}
