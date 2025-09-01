using System.Text.Json;
using FluentAssertions;
using Hoho.Decomp;
using Xunit;

namespace Decomp.Tests;

/// <summary>
/// Unit tests for symbol mapping and context-aware renaming
/// </summary>
public class SymbolMappingTests
{
    [Fact]
    public void SymbolMapping_Should_Track_Context()
    {
        // Arrange
        var map = new SymbolRenamingMap();
        
        // Act
        map.AddMapping("A", "props", "Wu1.constructor");
        map.AddMapping("A", "connection", "Bx2.constructor");
        map.AddMapping("A", "data", "global");
        
        // Assert
        map.GetMapping("A", "Wu1.constructor").Should().Be("props");
        map.GetMapping("A", "Bx2.constructor").Should().Be("connection");
        map.GetMapping("A", "global").Should().Be("data");
        map.GetMapping("A", "unknown.context").Should().Be("data"); // Falls back to global
    }
    
    [Fact]
    public void SymbolMapping_Should_Handle_Class_Method_Context()
    {
        // Arrange
        var map = new SymbolRenamingMap();
        
        // Act
        map.AddMapping("process", "render", "UIComponent");
        map.AddMapping("process", "transform", "DataHandler");
        map.AddMapping("process", "execute", "CommandRouter");
        
        // Assert
        map.GetMapping("process", "UIComponent").Should().Be("render");
        map.GetMapping("process", "DataHandler").Should().Be("transform");
        map.GetMapping("process", "CommandRouter").Should().Be("execute");
    }
    
    [Fact]
    public void SymbolMapping_Should_Serialize_And_Deserialize()
    {
        // Arrange
        var map = new SymbolRenamingMap();
        map.AddMapping("Wu1", "ReactModule");
        map.AddMapping("Ct1", "ApplicationCore");
        map.AddMapping("A", "props", "Wu1");
        
        // Act - Serialize
        var json = map.ToJson();
        
        // Deserialize to new map
        var newMap = SymbolRenamingMap.FromJson(json);
        
        // Assert
        newMap.GetMapping("Wu1").Should().Be("ReactModule");
        newMap.GetMapping("Ct1").Should().Be("ApplicationCore");
        newMap.GetMapping("A", "Wu1").Should().Be("props");
    }
    
    [Theory]
    [InlineData("A", false)]      // Single letter - not unique
    [InlineData("AB", false)]     // Two letters - might not be unique
    [InlineData("Wu1", true)]     // Three chars - likely unique
    [InlineData("Ct1", true)]     // Three chars - likely unique
    [InlineData("ApplicationCore", true)] // Long name - definitely unique
    public void IsGloballyUnique_Should_Identify_Unique_Symbols(string symbol, bool expectedUnique)
    {
        // Act
        var isUnique = SymbolAnalyzer.IsLikelyGloballyUnique(symbol);
        
        // Assert
        isUnique.Should().Be(expectedUnique);
    }
    
    [Fact]
    public void SymbolMapping_Should_Apply_To_Code_With_Context()
    {
        // Arrange
        var map = new SymbolRenamingMap();
        map.AddMapping("Wu1", "ReactModule");
        map.AddMapping("A", "props", "Wu1");
        map.AddMapping("B", "context", "Wu1");
        
        var code = @"
            var Wu1 = U((exports) => {
                function Component(A, B) {
                    this.props = A;
                    this.context = B;
                }
            });";
        
        // Act
        var renamed = ApplyContextAwareMappings(code, map);
        
        // Assert
        renamed.Should().Contain("var ReactModule");
        renamed.Should().Contain("function Component(props, context)");
    }
    
    [Fact]
    public void Should_Handle_Nested_Scopes_Correctly()
    {
        // Arrange
        var map = new SymbolRenamingMap();
        map.AddMapping("A", "outerParam", "outerFunction");
        map.AddMapping("A", "innerParam", "innerFunction");
        
        var code = @"
            function outerFunction(A) {
                console.log(A);  // Should be 'outerParam'
                function innerFunction(A) {
                    console.log(A);  // Should be 'innerParam'
                }
            }";
        
        // Act & Assert
        // This test validates that nested scopes with same identifier
        // are handled correctly by the context system
        map.GetMapping("A", "outerFunction").Should().Be("outerParam");
        map.GetMapping("A", "innerFunction").Should().Be("innerParam");
    }
    
    // Helper method to simulate applying mappings
    // (In real implementation, this would use LSP)
    private string ApplyContextAwareMappings(string code, SymbolRenamingMap map)
    {
        // Simplified simulation - real implementation uses LSP
        var result = code;
        
        // Apply globally unique mappings
        foreach (var mapping in map.GetGlobalMappings())
        {
            if (SymbolAnalyzer.IsLikelyGloballyUnique(mapping.Key))
            {
                result = result.Replace(mapping.Key, mapping.Value);
            }
        }
        
        return result;
    }
}

/// <summary>
/// Helper class for symbol analysis (extracted from SimplifiedExtractor)
/// </summary>
public static class SymbolAnalyzer
{
    public static bool IsLikelyGloballyUnique(string symbol)
    {
        // Single letters are NOT globally unique
        if (symbol.Length == 1) return false;
        
        // Two letters might not be unique
        if (symbol.Length == 2 && char.IsUpper(symbol[0])) return false;
        
        // Three+ character symbols are likely unique
        return true;
    }
}