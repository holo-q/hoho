using System.Collections.Generic;
using System.Text.Json;

namespace Hoho.Decomp;

/// <summary>
/// Context-aware symbol renaming map that handles the same identifier
/// appearing with different meanings in different scopes.
/// </summary>
public class SymbolRenamingMap
{
    private readonly Dictionary<string, Dictionary<string, string>> _scopedMappings = new();
    private readonly Dictionary<string, string> _globalMappings = new();
    
    /// <summary>
    /// Add a mapping for a symbol in a specific scope.
    /// </summary>
    /// <param name="originalName">The original obfuscated name (e.g., "A")</param>
    /// <param name="newName">The meaningful name to replace it with</param>
    /// <param name="scope">The scope where this mapping applies (e.g., "Wu1.Component")</param>
    public void AddMapping(string originalName, string newName, string scope)
    {
        if (!_scopedMappings.ContainsKey(scope))
        {
            _scopedMappings[scope] = new Dictionary<string, string>();
        }
        _scopedMappings[scope][originalName] = newName;
    }
    
    /// <summary>
    /// Add a global mapping that applies everywhere.
    /// </summary>
    public void AddGlobalMapping(string originalName, string newName)
    {
        _globalMappings[originalName] = newName;
    }
    
    /// <summary>
    /// Get the mapping for a symbol in a specific scope.
    /// </summary>
    /// <param name="originalName">The original obfuscated name</param>
    /// <param name="scope">The scope to look up</param>
    /// <returns>The mapped name, or null if no mapping exists</returns>
    public string? GetMapping(string originalName, string scope)
    {
        // First check for a scoped mapping
        if (_scopedMappings.ContainsKey(scope) && 
            _scopedMappings[scope].ContainsKey(originalName))
        {
            return _scopedMappings[scope][originalName];
        }
        
        // Check parent scopes (e.g., "Wu1.Component.method" -> "Wu1.Component" -> "Wu1")
        var parts = scope.Split('.');
        for (int i = parts.Length - 1; i > 0; i--)
        {
            var parentScope = string.Join(".", parts[0..i]);
            if (_scopedMappings.ContainsKey(parentScope) &&
                _scopedMappings[parentScope].ContainsKey(originalName))
            {
                return _scopedMappings[parentScope][originalName];
            }
        }
        
        // Fall back to global mapping
        if (_globalMappings.ContainsKey(originalName))
        {
            return _globalMappings[originalName];
        }
        
        return null;
    }
    
    /// <summary>
    /// Get all mappings for a specific scope.
    /// </summary>
    public Dictionary<string, string> GetScopeMappings(string scope)
    {
        return _scopedMappings.ContainsKey(scope) 
            ? new Dictionary<string, string>(_scopedMappings[scope]) 
            : new Dictionary<string, string>();
    }
    
    /// <summary>
    /// Get all global mappings.
    /// </summary>
    public Dictionary<string, string> GetGlobalMappings()
    {
        return new Dictionary<string, string>(_globalMappings);
    }
    
    /// <summary>
    /// Check if a mapping exists for a symbol in any scope.
    /// </summary>
    public bool HasMapping(string originalName)
    {
        if (_globalMappings.ContainsKey(originalName))
            return true;
            
        foreach (var scopeMappings in _scopedMappings.Values)
        {
            if (scopeMappings.ContainsKey(originalName))
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Get the total number of mappings.
    /// </summary>
    public int Count
    {
        get
        {
            int count = _globalMappings.Count;
            foreach (var scopeMappings in _scopedMappings.Values)
            {
                count += scopeMappings.Count;
            }
            return count;
        }
    }
    
    /// <summary>
    /// Add a global mapping (overload without scope).
    /// </summary>
    public void AddMapping(string originalName, string newName)
    {
        AddGlobalMapping(originalName, newName);
    }
    
    /// <summary>
    /// Get mapping without scope (returns global mapping).
    /// </summary>
    public string? GetMapping(string originalName)
    {
        return _globalMappings.ContainsKey(originalName) ? _globalMappings[originalName] : null;
    }
    
    /// <summary>
    /// Serialize the mapping to JSON.
    /// </summary>
    public string ToJson()
    {
        var data = new
        {
            GlobalMappings = _globalMappings,
            ScopedMappings = _scopedMappings
        };
        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }
    
    /// <summary>
    /// Deserialize a mapping from JSON.
    /// </summary>
    public static SymbolRenamingMap FromJson(string json)
    {
        var map = new SymbolRenamingMap();
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        
        if (data.TryGetProperty("GlobalMappings", out var globalMappings))
        {
            foreach (var kvp in globalMappings.EnumerateObject())
            {
                map.AddGlobalMapping(kvp.Name, kvp.Value.GetString() ?? "");
            }
        }
        
        if (data.TryGetProperty("ScopedMappings", out var scopedMappings))
        {
            foreach (var scope in scopedMappings.EnumerateObject())
            {
                foreach (var mapping in scope.Value.EnumerateObject())
                {
                    map.AddMapping(mapping.Name, mapping.Value.GetString() ?? "", scope.Name);
                }
            }
        }
        
        return map;
    }
}

/// <summary>
/// Helper class for analyzing symbols.
/// </summary>
public static class SymbolAnalyzer
{
    /// <summary>
    /// Check if a symbol is likely to be globally unique.
    /// </summary>
    public static bool IsLikelyGloballyUnique(string symbol)
    {
        // Single or two-letter identifiers are typically parameters/variables
        if (symbol.Length <= 2)
            return false;
            
        // Long descriptive names are unique
        if (symbol.Length > 10)
            return true;
            
        // Three+ character obfuscated names (like Wu1, Ct1) are typically unique
        if (symbol.Length >= 3 && char.IsUpper(symbol[0]))
            return true;
            
        return false;
    }
}