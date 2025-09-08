namespace Hoho.Core.Sessions;

public sealed record SessionInfo(string Id, string Dir, DateTimeOffset CreatedAt);

public static class SessionDiscovery
{
    public static IEnumerable<SessionInfo> ListSessions(int limit = 20)
    {
        var root = TranscriptStore.GetDefaultRoot();
        if (!Directory.Exists(root)) yield break;
        var dirs = Directory.EnumerateDirectories(root)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(di => di.CreationTimeUtc)
            .Take(limit);
        foreach (var di in dirs)
        {
            yield return new SessionInfo(di.Name, di.FullName, new DateTimeOffset(di.CreationTimeUtc));
        }
    }

    public static string? FirstUserPreview(string sessionId, int maxLen = 120)
    {
        var store = new TranscriptStore();
        var lines = store.ReadAllAsync(sessionId);
        // Read synchronously the first few events
        var enumerator = lines.GetAsyncEnumerator();
        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                var ev = enumerator.Current;
                if (ev.Type == "message")
                {
                    if (ev.Data is System.Text.Json.JsonElement el)
                    {
                        var role = el.TryGetProperty("Role", out var r) ? r.GetString() : null;
                        if (string.Equals(role, nameof(Role.User), StringComparison.OrdinalIgnoreCase))
                        {
                            var content = el.TryGetProperty("Content", out var c) ? c.GetString() : null;
                            if (!string.IsNullOrEmpty(content))
                            {
                                content = content.Replace("\r", " ").Replace("\n", " ");
                                if (content.Length > maxLen) content = content.Substring(0, maxLen - 1) + "…";
                                return content;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        return null;
    }
}

