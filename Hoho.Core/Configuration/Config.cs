using System.Text.Json;
using System.Text.Json.Serialization;
using Hoho.Core.Sandbox;

namespace Hoho.Core.Configuration;

public sealed class HohoConfig
{
    [JsonInclude] public SandboxSettings Sandbox { get; private set; } = new();
    [JsonInclude] public string Profile { get; private set; } = "default";
    [JsonInclude] public Dictionary<string, string> Secrets { get; private set; } = new();

    public static string GetDefaultPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hoho", "config.json");
    }

    public static HohoConfig Load(string? path = null)
    {
        path ??= GetDefaultPath();
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<HohoConfig>(json);
                if (cfg is not null) return cfg;
            }
        }
        catch
        {
            // fall through to defaults
        }
        return new HohoConfig();
    }

    public void Save(string? path = null)
    {
        path ??= GetDefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}

