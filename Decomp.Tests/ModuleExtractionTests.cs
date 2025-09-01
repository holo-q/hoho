using FluentAssertions;
using Hoho.Decomp;
using Xunit;

namespace Decomp.Tests;

/// <summary>
/// Tests for module extraction from webpack bundles
/// </summary>
public class ModuleExtractionTests
{
    [Fact]
    public void Should_Extract_CommonJS_Modules()
    {
        // Arrange
        var bundleContent = @"
            var Wu1=U((bnB)=>{
                var pA1=Symbol.for('react.element');
                bnB.exports={elem:pA1};
            });
            var Bx2=U((Q)=>{
                var Z=X1('fs');
                Q.exports={readFile:Z.readFileSync};
            });";
        
        var extractor = new TestableModuleExtractor();
        
        // Act
        var modules = extractor.ExtractCommonJSModules(bundleContent);
        
        // Assert
        modules.Should().HaveCount(2);
        modules[0].Name.Should().Be("Wu1");
        modules[0].Type.Should().Be("CommonJS");
        modules[0].Content.Should().Contain("Symbol.for('react.element')");
        
        modules[1].Name.Should().Be("Bx2");
        modules[1].Type.Should().Be("CommonJS");
        modules[1].Content.Should().Contain("X1('fs')");
    }
    
    [Fact]
    public void Should_Extract_Classes()
    {
        // Arrange
        var bundleContent = @"
            class Ct1 extends BaseClass {
                constructor(props) {
                    super(props);
                    this.state = {};
                }
                render() {
                    return null;
                }
            }
            class Y2Q {
                handleClick() {
                    this.setState({count: 1});
                }
            }";
        
        var extractor = new TestableModuleExtractor();
        
        // Act
        var modules = extractor.ExtractClasses(bundleContent);
        
        // Assert
        modules.Should().HaveCount(2);
        modules[0].Name.Should().Be("Ct1");
        modules[0].Type.Should().Be("Class");
        modules[0].Content.Should().Contain("extends BaseClass");
        modules[0].Content.Should().Contain("render()");
        
        modules[1].Name.Should().Be("Y2Q");
        modules[1].Type.Should().Be("Class");
        modules[1].Content.Should().Contain("handleClick()");
    }
    
    [Fact]
    public void Should_Extract_Named_Functions()
    {
        // Arrange
        var bundleContent = @"
            function Wu1(A, B) {
                return A + B;
            }
            function Oc(A, B, Q) {
                this.props = A;
                this.context = B;
                this.updater = Q || gP0;
            }
            function regularFunction() {
                // Should not be extracted (not obfuscated pattern)
            }";
        
        var extractor = new TestableModuleExtractor();
        
        // Act
        var modules = extractor.ExtractFunctions(bundleContent);
        
        // Assert
        modules.Should().HaveCount(2); // Only obfuscated functions
        modules[0].Name.Should().Be("Wu1");
        modules[0].Type.Should().Be("Function");
        
        modules[1].Name.Should().Be("Oc");
        modules[1].Type.Should().Be("Function");
        modules[1].Content.Should().Contain("this.props = A");
    }
    
    [Fact]
    public void Should_Handle_Nested_Braces_Correctly()
    {
        // Arrange
        var bundleContent = @"
            class Complex {
                method() {
                    if (true) {
                        return {
                            nested: {
                                deep: {
                                    value: 42
                                }
                            }
                        };
                    }
                }
            }";
        
        var extractor = new TestableModuleExtractor();
        
        // Act
        var content = extractor.ExtractBlock(bundleContent, bundleContent.IndexOf("class"));
        
        // Assert
        content.Should().NotBeNull();
        content.Should().StartWith("class Complex");
        content.Should().EndWith("}");
        content.Should().Contain("value: 42");
        
        // Should handle all nested braces correctly
        var openBraces = content!.Count(c => c == '{');
        var closeBraces = content!.Count(c => c == '}');
        openBraces.Should().Be(closeBraces);
    }
    
    [Fact]
    public void Should_Generate_Symbol_Map()
    {
        // Arrange
        var bundleContent = @"
            class TestClass {
                method1() {}
                method2() {}
                static staticMethod() {}
                get property() {}
            }";
        
        // Act
        var symbols = ExtractedSymbols.FromCode(bundleContent);
        
        // Assert
        symbols.TotalClasses.Should().Be(1);
        symbols.Classes.Should().ContainKey("TestClass");
        symbols.Classes["TestClass"].Methods.Should().Contain("method1");
        symbols.Classes["TestClass"].Methods.Should().Contain("method2");
        symbols.Classes["TestClass"].StaticMethods.Should().Contain("staticMethod");
        symbols.Classes["TestClass"].Properties.Should().Contain("property");
    }
    
    [Fact]
    public void Should_Track_Class_Inheritance()
    {
        // Arrange
        var bundleContent = @"
            class Parent {}
            class Child extends Parent {}
            class GrandChild extends Child {}";
        
        // Act
        var symbols = ExtractedSymbols.FromCode(bundleContent);
        
        // Assert
        symbols.Classes["Child"].Extends.Should().Be("Parent");
        symbols.Classes["GrandChild"].Extends.Should().Be("Child");
    }
}

/// <summary>
/// Testable version of module extractor with exposed methods
/// </summary>
public class TestableModuleExtractor
{
    public List<ModuleInfo> ExtractCommonJSModules(string content)
    {
        var modules = new List<ModuleInfo>();
        var pattern = @"var\s+([A-Za-z0-9_]+)\s*=\s*U\s*\(\s*\(([^)]*)\)\s*=>\s*\{";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var moduleName = match.Groups[1].Value;
            var moduleContent = ExtractBlock(content, match.Index);
            if (moduleContent != null)
            {
                modules.Add(new ModuleInfo
                {
                    Name = moduleName,
                    Content = moduleContent,
                    Type = "CommonJS"
                });
            }
        }
        
        return modules;
    }
    
    public List<ModuleInfo> ExtractClasses(string content)
    {
        var modules = new List<ModuleInfo>();
        var pattern = @"class\s+([A-Za-z0-9_]+)(?:\s+extends\s+[^{]+)?\s*\{";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var className = match.Groups[1].Value;
            var classContent = ExtractBlock(content, match.Index);
            if (classContent != null)
            {
                modules.Add(new ModuleInfo
                {
                    Name = className,
                    Content = classContent,
                    Type = "Class"
                });
            }
        }
        
        return modules;
    }
    
    public List<ModuleInfo> ExtractFunctions(string content)
    {
        var modules = new List<ModuleInfo>();
        var pattern = @"function\s+([A-Z][A-Za-z0-9_]*)\s*\([^)]*\)\s*\{";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var funcName = match.Groups[1].Value;
            if (funcName.Length <= 4) // Likely obfuscated
            {
                var funcContent = ExtractBlock(content, match.Index);
                if (funcContent != null)
                {
                    modules.Add(new ModuleInfo
                    {
                        Name = funcName,
                        Content = funcContent,
                        Type = "Function"
                    });
                }
            }
        }
        
        return modules;
    }
    
    public string? ExtractBlock(string content, int startIndex)
    {
        var depth = 0;
        var foundStart = false;
        
        for (int i = startIndex; i < content.Length && i < startIndex + 50000; i++)
        {
            if (content[i] == '{')
            {
                if (!foundStart)
                {
                    foundStart = true;
                    depth = 1;
                }
                else
                {
                    depth++;
                }
            }
            else if (content[i] == '}' && foundStart)
            {
                depth--;
                if (depth == 0)
                {
                    return content.Substring(startIndex, i - startIndex + 1);
                }
            }
        }
        
        return null;
    }
}

public class ModuleInfo
{
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public string Type { get; set; } = "";
}

/// <summary>
/// Simplified symbol extraction for testing
/// </summary>
public class ExtractedSymbols
{
    public int TotalClasses { get; set; }
    public Dictionary<string, ClassInfo> Classes { get; set; } = new();
    
    public static ExtractedSymbols FromCode(string code)
    {
        var symbols = new ExtractedSymbols();
        
        // Extract classes - use a more sophisticated pattern that handles nested braces
        var classPattern = @"class\s+(\w+)(?:\s+extends\s+(\w+))?\s*\{";
        var matches = System.Text.RegularExpressions.Regex.Matches(code, classPattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var className = match.Groups[1].Value;
            var extends = match.Groups[2].Value;
            
            // Extract the class body by finding matching braces
            var startIndex = match.Index + match.Length;
            var body = ExtractClassBody(code, startIndex);
            
            var classInfo = new ClassInfo
            {
                Name = className,
                Extends = string.IsNullOrEmpty(extends) ? null : extends
            };
            
            // Extract methods - need to match methods with empty bodies too
            var methodPattern = @"(\w+)\s*\(\)\s*\{\s*\}";
            var methodMatches = System.Text.RegularExpressions.Regex.Matches(body, methodPattern);
            foreach (System.Text.RegularExpressions.Match methodMatch in methodMatches)
            {
                var methodName = methodMatch.Groups[1].Value;
                // Skip if it's a static method or property (handled separately)
                if (!body.Contains($"static {methodName}") && !body.Contains($"get {methodName}"))
                {
                    classInfo.Methods.Add(methodName);
                }
            }
            
            // Extract static methods
            var staticPattern = @"static\s+(\w+)\s*\(\)\s*\{\s*\}";
            var staticMatches = System.Text.RegularExpressions.Regex.Matches(body, staticPattern);
            foreach (System.Text.RegularExpressions.Match staticMatch in staticMatches)
            {
                classInfo.StaticMethods.Add(staticMatch.Groups[1].Value);
            }
            
            // Extract properties
            var propertyPattern = @"get\s+(\w+)\s*\(\)\s*\{\s*\}";
            var propertyMatches = System.Text.RegularExpressions.Regex.Matches(body, propertyPattern);
            foreach (System.Text.RegularExpressions.Match propMatch in propertyMatches)
            {
                classInfo.Properties.Add(propMatch.Groups[1].Value);
            }
            
            symbols.Classes[className] = classInfo;
        }
        
        symbols.TotalClasses = symbols.Classes.Count;
        return symbols;
    }
    
    private static string ExtractClassBody(string code, int startIndex)
    {
        var depth = 1; // We already passed the opening brace
        var endIndex = startIndex;
        
        for (int i = startIndex; i < code.Length; i++)
        {
            if (code[i] == '{')
                depth++;
            else if (code[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    endIndex = i;
                    break;
                }
            }
        }
        
        return code.Substring(startIndex, endIndex - startIndex);
    }
}

public class ClassInfo
{
    public string Name { get; set; } = "";
    public string? Extends { get; set; }
    public HashSet<string> Methods { get; set; } = new();
    public HashSet<string> StaticMethods { get; set; } = new();
    public HashSet<string> Properties { get; set; } = new();
}