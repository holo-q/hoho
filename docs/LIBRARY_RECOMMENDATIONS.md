# HOHO C# Library Recommendations

## ✅ Confirmed Compatible Libraries for .NET 9.0

### 1. **Terminal.Gui** - TUI Framework ✅
- **Version**: 1.14.1+
- **Compatibility**: Works with .NET 9 (targets .NET 6+)
- **Decision**: **USE THIS**
- Rich widget library, native performance, cross-platform

### 2. **Anthropic.SDK** - API Client ✅
- **Version**: 5.5.1 (by tghamm)
- **NuGet**: `Anthropic.SDK`
- **Compatibility**: Targets .NET 8.0, .NET 6.0, NetStandard 2.0
- **Works with .NET 9**: Yes (backward compatible)
- **Decision**: **USE THIS**
- Well-maintained, unofficial but mature

### 3. **ModelContextProtocol** - MCP SDK ✅ 🔥
- **Version**: Preview (official by Microsoft/Anthropic)
- **NuGet**: `ModelContextProtocol`
- **GitHub**: `modelcontextprotocol/csharp-sdk`
- **Compatibility**: Modern .NET, actively developed
- **Decision**: **USE THIS**
- Official collaboration between Microsoft and Anthropic!

### 4. **FluentValidation** - Schema Validation (Zod Alternative) ✅
- **Version**: 11.9.0+
- **NuGet**: `FluentValidation`
- **Compatibility**: .NET 6.0+, works with .NET 9
- **Decision**: **USE THIS**
- Industry standard for C# validation

## 📦 Complete Package List for Hoho.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
    <PublishTrimmed>true</PublishTrimmed>
  </PropertyGroup>

  <ItemGroup>
    <!-- TUI Framework -->
    <PackageReference Include="Terminal.Gui" Version="1.14.1" />
    
    <!-- API & AI -->
    <PackageReference Include="Anthropic.SDK" Version="5.5.1" />
    <PackageReference Include="ModelContextProtocol" Version="*-preview" />
    
    <!-- Validation (Zod Alternative) -->
    <PackageReference Include="FluentValidation" Version="11.9.0" />
    
    <!-- JSON & Serialization -->
    <PackageReference Include="System.Text.Json" Version="8.0.5" />
    
    <!-- CLI Parsing -->
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    
    <!-- Logging -->
    <PackageReference Include="Serilog" Version="3.1.1" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
    
    <!-- File Operations -->
    <PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="8.0.0" />
    
    <!-- Process Management (Optional but recommended) -->
    <PackageReference Include="CliWrap" Version="3.6.4" />
    
    <!-- HTTP -->
    <PackageReference Include="System.Net.Http.Json" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## 🔄 Library Mappings: Claude Code → HOHO C#

| Claude Code (JS/TS) | HOHO C# | Purpose |
|---------------------|---------|---------|
| React + Ink | **Terminal.Gui** | Terminal UI framework |
| Yoga WASM | Terminal.Gui built-in | Layout engine |
| Zod | **FluentValidation** | Schema validation |
| @anthropic-ai/sdk | **Anthropic.SDK** | Claude API calls |
| @modelcontextprotocol/sdk | **ModelContextProtocol** | MCP integration |
| ripgrep (binary) | System.Diagnostics.Process | Execute rg directly |
| sharp | ImageSharp (if needed) | Image processing |
| node:fs | System.IO | File operations |
| node:child_process | CliWrap or Process | Process management |

## 🎯 Key Implementation Notes

### FluentValidation as Zod Alternative

```csharp
// Zod-like validation in C# with FluentValidation
public class FileReadInputValidator : AbstractValidator<FileReadInput>
{
    public FileReadInputValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty()
            .Must(Path.IsPathFullyQualified)
            .WithMessage("Must be absolute path");
            
        RuleFor(x => x.Offset)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Offset.HasValue);
            
        RuleFor(x => x.Limit)
            .GreaterThan(0)
            .LessThanOrEqualTo(10000)
            .When(x => x.Limit.HasValue);
    }
}

// Usage
var validator = new FileReadInputValidator();
var result = await validator.ValidateAsync(input);
if (!result.IsValid)
{
    throw new ValidationException(result.Errors);
}
```

### Anthropic.SDK Usage

```csharp
using Anthropic.SDK;
using Anthropic.SDK.Messaging;

var client = new AnthropicClient(apiKey);

var messages = new List<Message>
{
    new Message(MessageRole.User, "Hello Claude")
};

var parameters = new MessageParameters()
{
    Messages = messages,
    MaxTokens = 1024,
    Model = AnthropicModels.Claude35Sonnet,
    Stream = true
};

await foreach (var response in client.Messages.StreamClaudeMessageAsync(parameters))
{
    // Handle streaming response
}
```

### MCP SDK Integration

```csharp
using ModelContextProtocol;

// Create MCP server
var server = new McpServer();

// Register tools
server.AddTool("file_read", async (args) => {
    var input = args.Deserialize<FileReadInput>();
    // Tool implementation
});

// Start server
await server.StartAsync();
```

## 🔧 AOT Compatibility Concerns

Some libraries may need configuration for Native AOT:

1. **FluentValidation**: May need source generators for AOT
2. **Terminal.Gui**: Should work but test thoroughly
3. **Anthropic.SDK**: Uses reflection, may need adjustments
4. **ModelContextProtocol**: Designed with modern .NET in mind

Consider adding to project file:
```xml
<PropertyGroup>
  <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
</PropertyGroup>
```

## ✨ Summary

All required libraries are available and compatible with .NET 9.0:
- **Terminal.Gui** for TUI (replacing React)
- **Anthropic.SDK** for Claude API
- **ModelContextProtocol** for MCP (official Microsoft/Anthropic!)
- **FluentValidation** for schema validation (replacing Zod)

The C# ecosystem has mature, performant alternatives for every JavaScript library used in Claude Code.