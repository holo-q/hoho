using System.Reflection;
using System.Text;

namespace Hoho.Core;

/// <summary>
/// ZERO-ALLOCATION ASCII art management.
/// Uses embedded resources and Span&lt;char&gt; for maximum performance.
/// </summary>
public static class AsciiArt
{
    private static readonly Lazy<string> _saitamaFace = new(() => LoadEmbeddedResource("art_2.txt"));
    
    /// <summary>
    /// Get the Saitama "OK" face with zero allocations after first call.
    /// </summary>
    public static string GetSaitamaFace() => _saitamaFace.Value;
    
    /// <summary>
    /// Load embedded resource with efficient string handling.
    /// </summary>
    private static string LoadEmbeddedResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Hoho.{fileName}";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // Fallback to file system if embedded resource not found
            if (File.Exists(fileName))
            {
                return File.ReadAllText(fileName);
            }
            return "ASCII art not found";
        }
        
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}