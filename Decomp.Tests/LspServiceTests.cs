using System.Text.Json;
using FluentAssertions;
using Hoho.Decomp;
using Moq;
using Xunit;

namespace Decomp.Tests;

/// <summary>
/// Tests for LSP service integration and communication
/// </summary>
public class LspServiceTests : IDisposable
{
    private readonly string _testWorkspace;
    
    public LspServiceTests()
    {
        _testWorkspace = Path.Combine(Path.GetTempPath(), $"lsp-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testWorkspace);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_testWorkspace))
        {
            Directory.Delete(_testWorkspace, true);
        }
    }
    
    [Fact]
    public async Task LspService_Should_Initialize_Successfully()
    {
        // Arrange
        var service = new LspRenameService(_testWorkspace);
        
        // Act 
        var initTask = service.InitializeAsync();
        
        // Assert
        var completedInTime = initTask.Wait(TimeSpan.FromSeconds(10));
        completedInTime.Should().BeTrue("initialization should complete within 10 seconds");
        
        // Cleanup
        service.Dispose();
    }
    
    [Fact]
    public async Task LspService_Should_Open_JavaScript_Files()
    {
        // Arrange
        var testFile = Path.Combine(_testWorkspace, "test.js");
        var testContent = @"
            function testFunction(param1, param2) {
                return param1 + param2;
            }";
        await File.WriteAllTextAsync(testFile, testContent);
        
        var service = new LspRenameService(_testWorkspace);
        await service.InitializeAsync();
        
        // Act
        var openTask = service.OpenFileAsync(testFile);
        
        // Assert
        var completedInTime = openTask.Wait(TimeSpan.FromSeconds(5));
        completedInTime.Should().BeTrue("file open should complete within 5 seconds");
        
        // Cleanup
        service.Dispose();
    }
    
    [Fact]
    public async Task Should_Find_All_References_To_Symbol()
    {
        // Arrange
        var testFile = Path.Combine(_testWorkspace, "references.js");
        var testContent = @"
            var myVariable = 42;
            console.log(myVariable);
            function test() {
                return myVariable * 2;
            }
            myVariable = myVariable + 1;";
        await File.WriteAllTextAsync(testFile, testContent);
        
        var service = new LspRenameService(_testWorkspace);
        await service.InitializeAsync();
        await service.OpenFileAsync(testFile);
        
        // Act
        // Find references to myVariable at line 1, character 16
        var references = await service.FindReferencesAsync(testFile, 1, 16);
        
        // Assert
        references.Should().HaveCountGreaterOrEqualTo(4); // Definition + 3 usages
        
        // Cleanup
        service.Dispose();
    }
    
    [Fact]
    public async Task Should_Rename_Symbol_With_All_References()
    {
        // Arrange
        var testFile = Path.Combine(_testWorkspace, "rename.js");
        var testContent = @"
            function oldName() {
                return 'test';
            }
            oldName();
            var result = oldName();";
        await File.WriteAllTextAsync(testFile, testContent);
        
        var service = new LspRenameService(_testWorkspace);
        await service.InitializeAsync();
        await service.OpenFileAsync(testFile);
        
        // Act
        var edit = await service.RenameSymbolAsync(testFile, 1, 21, "newName");
        
        // Assert
        edit.Should().NotBeNull();
        edit!.Changes.Should().ContainKey(new Uri(testFile).ToString());
        edit.Changes.First().Value.Should().HaveCountGreaterOrEqualTo(3); // All occurrences
        
        // Cleanup
        service.Dispose();
    }
    
    [Fact]
    public async Task BatchRename_Should_Apply_Multiple_Mappings()
    {
        // Arrange
        var testFile = Path.Combine(_testWorkspace, "batch.js");
        var testContent = @"
            var Wu1 = function(A, B) {
                return A + B;
            };
            var Ct1 = class {
                constructor(Q) {
                    this.value = Q;
                }
            };";
        await File.WriteAllTextAsync(testFile, testContent);
        
        var mappings = new Dictionary<string, string>
        {
            ["Wu1"] = "addFunction",
            ["Ct1"] = "ValueClass",
            ["A"] = "first",
            ["B"] = "second",
            ["Q"] = "initialValue"
        };
        
        var service = new LspRenameService(_testWorkspace);
        await service.InitializeAsync();
        
        // Act
        var report = await service.BatchRenameAsync(testFile, mappings);
        
        // Assert
        report.SuccessfulRenames.Should().BeGreaterThan(0);
        report.FailedRenames.Should().Be(0);
        
        // Verify file was modified
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        modifiedContent.Should().Contain("addFunction");
        modifiedContent.Should().Contain("ValueClass");
        
        // Cleanup
        service.Dispose();
    }
}

/// <summary>
/// Tests for LSP client-server communication
/// </summary>
public class LspClientServerTests
{
    [Fact]
    public void LspServerDaemon_Should_Check_If_Running()
    {
        // Act
        var isRunning = LspServerDaemon.IsRunning();
        
        // Assert
        // Should be false if no server is running
        isRunning.Should().BeFalse();
    }
    
    [Fact]
    public async Task RenameRequest_Should_Serialize_Correctly()
    {
        // Arrange
        var request = new RenameRequest
        {
            FilePath = "/test/file.js",
            Mappings = new Dictionary<string, string>
            {
                ["oldName"] = "newName",
                ["Wu1"] = "ReactModule"
            }
        };
        
        // Act
        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<RenameRequest>(json);
        
        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.FilePath.Should().Be(request.FilePath);
        deserialized.Mappings.Should().HaveCount(2);
        deserialized.Mappings["Wu1"].Should().Be("ReactModule");
    }
    
    [Fact]
    public void RenameResponse_Should_Handle_Success_And_Failure()
    {
        // Arrange & Act
        var successResponse = new RenameResponse
        {
            Success = true,
            SuccessfulRenames = 10,
            FailedRenames = 0,
            TotalReferences = 25
        };
        
        var failureResponse = new RenameResponse
        {
            Success = false,
            Error = "Failed to connect to LSP"
        };
        
        // Assert
        successResponse.Success.Should().BeTrue();
        successResponse.Error.Should().BeNull();
        successResponse.SuccessfulRenames.Should().Be(10);
        
        failureResponse.Success.Should().BeFalse();
        failureResponse.Error.Should().NotBeNullOrEmpty();
    }
}