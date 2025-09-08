using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Hoho.Core.Sessions;

public sealed class TranscriptStore
{
    private readonly string _root;
    private readonly JsonSerializerOptions _json;

    public TranscriptStore(string? root = null)
    {
        _root = root ?? GetDefaultRoot();
        _json = new JsonSerializerOptions { WriteIndented = false };
        Directory.CreateDirectory(_root);
    }

    public static string GetDefaultRoot()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hoho", "sessions");
    }

    public string CreateNewSessionId() => DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

    public string GetTranscriptPath(string sessionId) => Path.Combine(_root, sessionId, "transcript.jsonl");

    public async Task AppendAsync(string sessionId, IEnumerable<TranscriptEvent> events, CancellationToken ct = default)
    {
        var path = GetTranscriptPath(sessionId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(fs, new UTF8Encoding(false));
        foreach (var ev in events)
        {
            ct.ThrowIfCancellationRequested();
            var line = JsonSerializer.Serialize(ev, _json);
            await writer.WriteLineAsync(line);
        }
    }

    public async IAsyncEnumerable<TranscriptEvent> ReadAllAsync(string sessionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var path = GetTranscriptPath(sessionId);
        if (!File.Exists(path)) yield break;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var ev = JsonSerializer.Deserialize<TranscriptEvent>(line);
            if (ev is not null) yield return ev;
        }
    }
}

