using FluentAssertions;
using Hoho.Decomp;
using Xunit;

namespace Decomp.Tests;

/// <summary>
/// Tests for structural renaming with scope awareness
/// </summary>
public class StructuralRenamingTests
{
    [Fact]
    public void Should_Handle_Same_Identifier_Different_Scopes()
    {
        // This tests the core problem: 'A' appears 706 times with different meanings
        
        // Arrange
        var code = @"
            // A means 'exports' in module scope
            var Wu1 = U((A) => {
                A.exports = {};
                
                // A means 'props' in Component scope
                function Component(A, B) {
                    this.props = A;
                    this.context = B;
                }
                
                // A means 'data' in helper function
                function helper(A) {
                    return A.map(x => x * 2);
                }
            });
            
            // A means 'connection' in different module
            var Bx2 = U((A) => {
                function Database(A) {
                    this.connection = A;
                }
            });";
        
        var map = new SymbolRenamingMap();
        map.AddMapping("A", "exports", "Wu1");
        map.AddMapping("A", "props", "Wu1.Component");
        map.AddMapping("A", "data", "Wu1.helper");
        map.AddMapping("A", "moduleExports", "Bx2");
        map.AddMapping("A", "connection", "Bx2.Database");
        
        // Act & Assert
        map.GetMapping("A", "Wu1").Should().Be("exports");
        map.GetMapping("A", "Wu1.Component").Should().Be("props");
        map.GetMapping("A", "Wu1.helper").Should().Be("data");
        map.GetMapping("A", "Bx2").Should().Be("moduleExports");
        map.GetMapping("A", "Bx2.Database").Should().Be("connection");
    }
    
    [Fact]
    public void Should_Preserve_Method_Call_Relationships()
    {
        // Tests that renaming preserves call chains
        
        // Arrange
        var callGraph = new CallGraph();
        callGraph.AddCall("Wu1.init", "Bx2.setup");
        callGraph.AddCall("Bx2.setup", "Ct1.configure");
        callGraph.AddCall("Ct1.configure", "dP0.process");
        
        var renames = new Dictionary<string, string>
        {
            ["Wu1"] = "ReactModule",
            ["Bx2"] = "DatabaseConnection",
            ["Ct1"] = "ApplicationCore",
            ["dP0"] = "ComponentPrototype"
        };
        
        // Act
        var renamedGraph = callGraph.ApplyRenames(renames);
        
        // Assert
        renamedGraph.HasCall("ReactModule.init", "DatabaseConnection.setup").Should().BeTrue();
        renamedGraph.HasCall("DatabaseConnection.setup", "ApplicationCore.configure").Should().BeTrue();
        renamedGraph.HasCall("ApplicationCore.configure", "ComponentPrototype.process").Should().BeTrue();
    }
    
    [Fact]
    public void Should_Handle_Inheritance_Chains()
    {
        // Tests that class inheritance is preserved
        
        // Arrange
        var inheritance = new InheritanceTree();
        inheritance.AddInheritance("Au1", "Wu1");  // Au1 extends Wu1
        inheritance.AddInheritance("Bv2", "Au1");  // Bv2 extends Au1
        
        var renames = new Dictionary<string, string>
        {
            ["Wu1"] = "ReactModule",
            ["Au1"] = "Component",
            ["Bv2"] = "SpecialComponent"
        };
        
        // Act
        var renamedTree = inheritance.ApplyRenames(renames);
        
        // Assert
        renamedTree.GetParent("Component").Should().Be("ReactModule");
        renamedTree.GetParent("SpecialComponent").Should().Be("Component");
        renamedTree.IsDescendantOf("SpecialComponent", "ReactModule").Should().BeTrue();
    }
    
    [Theory]
    [InlineData("class Wu1", "class ([A-Za-z0-9_]+)", true)]
    [InlineData("function ynB(A)", @"function ([A-Za-z0-9_]+)\(", true)]
    [InlineData("var gP0 = {", @"var ([A-Za-z0-9_]+)\s*=", true)]
    [InlineData("exports.Queue = gP0", @"exports\.([A-Za-z0-9_]+)\s*=", false)] // Property, not declaration
    public void Should_Identify_Symbol_Declarations(string code, string pattern, bool isDeclaration)
    {
        // Tests pattern matching for symbol declarations
        
        // Act
        var match = System.Text.RegularExpressions.Regex.Match(code, pattern);
        
        // Assert
        if (isDeclaration)
        {
            match.Success.Should().BeTrue();
            match.Groups[1].Value.Should().NotBeEmpty();
        }
        else
        {
            // This tests that we don't confuse property assignments with declarations
            var declarationPattern = @"^(class|function|var|let|const)\s+([A-Za-z0-9_]+)";
            var declarationMatch = System.Text.RegularExpressions.Regex.Match(code, declarationPattern);
            declarationMatch.Success.Should().BeFalse();
        }
    }
    
    [Fact]
    public void Should_Handle_Module_Exports_And_Imports()
    {
        // Tests that module boundaries are respected
        
        // Arrange
        var moduleSystem = new ModuleSystem();
        
        // Module Wu1 exports certain symbols
        moduleSystem.AddExport("Wu1", "Queue", "gP0");
        moduleSystem.AddExport("Wu1", "getIter", "ynB");
        
        // Other modules import from Wu1
        moduleSystem.AddImport("ConsumerModule", "Wu1", "Queue");
        
        var renames = new Dictionary<string, string>
        {
            ["Wu1"] = "ReactModule",
            ["gP0"] = "ReactNoopUpdateQueue",
            ["ynB"] = "getIteratorFn"
        };
        
        // Act
        var renamedSystem = moduleSystem.ApplyRenames(renames);
        
        // Assert
        renamedSystem.GetExport("ReactModule", "Queue").Should().Be("ReactNoopUpdateQueue");
        renamedSystem.GetExport("ReactModule", "getIter").Should().Be("getIteratorFn");
        renamedSystem.GetImportSource("ConsumerModule", "Queue").Should().Be("ReactModule");
    }
}

/// <summary>
/// Helper classes for testing structural relationships
/// </summary>
public class CallGraph
{
    private readonly Dictionary<string, HashSet<string>> _calls = new();
    
    public void AddCall(string from, string to)
    {
        if (!_calls.ContainsKey(from))
            _calls[from] = new HashSet<string>();
        _calls[from].Add(to);
    }
    
    public CallGraph ApplyRenames(Dictionary<string, string> renames)
    {
        var newGraph = new CallGraph();
        foreach (var kvp in _calls)
        {
            var from = RenameSymbol(kvp.Key, renames);
            foreach (var to in kvp.Value)
            {
                newGraph.AddCall(from, RenameSymbol(to, renames));
            }
        }
        return newGraph;
    }
    
    public bool HasCall(string from, string to)
    {
        return _calls.ContainsKey(from) && _calls[from].Contains(to);
    }
    
    private string RenameSymbol(string symbol, Dictionary<string, string> renames)
    {
        // Handle method calls like Wu1.init -> ReactModule.init
        var parts = symbol.Split('.');
        if (parts.Length == 2 && renames.ContainsKey(parts[0]))
        {
            return $"{renames[parts[0]]}.{parts[1]}";
        }
        return renames.ContainsKey(symbol) ? renames[symbol] : symbol;
    }
}

public class InheritanceTree
{
    private readonly Dictionary<string, string> _parents = new();
    
    public void AddInheritance(string child, string parent)
    {
        _parents[child] = parent;
    }
    
    public InheritanceTree ApplyRenames(Dictionary<string, string> renames)
    {
        var newTree = new InheritanceTree();
        foreach (var kvp in _parents)
        {
            var child = renames.ContainsKey(kvp.Key) ? renames[kvp.Key] : kvp.Key;
            var parent = renames.ContainsKey(kvp.Value) ? renames[kvp.Value] : kvp.Value;
            newTree.AddInheritance(child, parent);
        }
        return newTree;
    }
    
    public string? GetParent(string child)
    {
        return _parents.ContainsKey(child) ? _parents[child] : null;
    }
    
    public bool IsDescendantOf(string child, string ancestor)
    {
        var current = child;
        while (_parents.ContainsKey(current))
        {
            current = _parents[current];
            if (current == ancestor) return true;
        }
        return false;
    }
}

public class ModuleSystem
{
    private readonly Dictionary<string, Dictionary<string, string>> _exports = new();
    private readonly Dictionary<string, Dictionary<string, string>> _imports = new();
    
    public void AddExport(string module, string exportName, string internalName)
    {
        if (!_exports.ContainsKey(module))
            _exports[module] = new Dictionary<string, string>();
        _exports[module][exportName] = internalName;
    }
    
    public void AddImport(string module, string fromModule, string importName)
    {
        if (!_imports.ContainsKey(module))
            _imports[module] = new Dictionary<string, string>();
        _imports[module][importName] = fromModule;
    }
    
    public ModuleSystem ApplyRenames(Dictionary<string, string> renames)
    {
        var newSystem = new ModuleSystem();
        
        // Rename exports
        foreach (var module in _exports)
        {
            var newModuleName = renames.ContainsKey(module.Key) ? renames[module.Key] : module.Key;
            foreach (var export in module.Value)
            {
                var newInternalName = renames.ContainsKey(export.Value) ? renames[export.Value] : export.Value;
                newSystem.AddExport(newModuleName, export.Key, newInternalName);
            }
        }
        
        // Rename imports
        foreach (var module in _imports)
        {
            var newModuleName = renames.ContainsKey(module.Key) ? renames[module.Key] : module.Key;
            foreach (var import in module.Value)
            {
                var newFromModule = renames.ContainsKey(import.Value) ? renames[import.Value] : import.Value;
                newSystem.AddImport(newModuleName, newFromModule, import.Key);
            }
        }
        
        return newSystem;
    }
    
    public string? GetExport(string module, string exportName)
    {
        return _exports.ContainsKey(module) && _exports[module].ContainsKey(exportName) 
            ? _exports[module][exportName] 
            : null;
    }
    
    public string? GetImportSource(string module, string importName)
    {
        return _imports.ContainsKey(module) && _imports[module].ContainsKey(importName)
            ? _imports[module][importName]
            : null;
    }
}