using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Hoho.Core;
using NetFabric.Hyperlinq;

namespace Hoho.Tools;

/// <summary>
/// Tool registry and manager for HOHO.
/// HIGH-PERFORMANCE tool discovery and execution.
/// </summary>
public class ToolRegistry {
	private readonly ConcurrentDictionary<string, IHohoTool> _tools = new();
	private readonly JsonSerializerOptions                   _jsonOptions;

	public ToolRegistry() {
		_jsonOptions = new JsonSerializerOptions {
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower
		};

		// Auto-register all tools in assembly
		RegisterToolsFromAssembly();
	}

	/// <summary>
	/// Register a tool with the registry.
	/// </summary>
	public void RegisterTool(IHohoTool tool) {
		if (_tools.TryAdd(tool.Name, tool)) {
			Logger.Info($"Registered tool: {tool.Name}");
		} else {
			Logger.Warn($"Tool already registered: {tool.Name}");
		}
	}

	/// <summary>
	/// Get a tool by name.
	/// </summary>
	public IHohoTool? GetTool(string name) {
		return _tools.GetValueOrDefault(name);
	}

	/// <summary>
	/// Get all registered tools.
	/// </summary>
	public IReadOnlyDictionary<string, IHohoTool> GetAllTools() {
		return _tools;
	}

	/// <summary>
	/// Execute a tool by name with JSON input.
	/// </summary>
	public async Task<ToolResult> ExecuteToolAsync(
		string            toolName,
		JsonElement       input,
		CancellationToken cancellationToken = default) {
		var tool = GetTool(toolName);
		if (tool == null) {
			return ToolResult.Fail($"Tool not found: {toolName}");
		}

		try {
			// Deserialize input to the tool's expected type
			var inputType = GetToolInputType(tool);
			if (inputType == null) {
				return ToolResult.Fail($"Could not determine input type for tool: {toolName}");
			}

			var typedInput = input.Deserialize(inputType, _jsonOptions);
			if (typedInput == null) {
				return ToolResult.Fail($"Failed to deserialize input for tool: {toolName}");
			}

			Logger.Info($"Executing tool: {toolName}");
			return await tool.ExecuteAsync(typedInput, cancellationToken);
		} catch (JsonException ex) {
			Logger.Error(ex, "Invalid JSON input for tool {ToolName}", toolName);
			return ToolResult.Fail($"Invalid input format: {ex.Message}");
		} catch (Exception ex) {
			Logger.Error(ex, "Tool execution failed: {ToolName}", toolName);
			return ToolResult.Fail($"Tool execution failed: {ex.Message}");
		}
	}

	/// <summary>
	/// Auto-register all tools from the current assembly.
	/// </summary>
	private void RegisterToolsFromAssembly() {
		var toolType = typeof(IHohoTool);
		var assembly = Assembly.GetExecutingAssembly();

		// Use Hyperlinq for efficient type filtering
		var toolTypes = assembly.GetTypes().AsValueEnumerable()
			.Where(t => t.IsClass && !t.IsAbstract && toolType.IsAssignableFrom(t))
			.ToArray();

		foreach (var type in toolTypes) {
			try {
				if (Activator.CreateInstance(type) is IHohoTool tool) {
					RegisterTool(tool);
				}
			} catch (Exception ex) {
				Logger.Warn($"Failed to register tool type: {type.Name} - {ex.Message}");
			}
		}

		Logger.Info($"Registered {_tools.Count} tools from assembly");
	}

	/// <summary>
	/// Get the input type for a tool using reflection.
	/// </summary>
	private Type? GetToolInputType(IHohoTool tool) {
		var toolType = tool.GetType();

		// Check if it's a generic HohoTool<T>
		var baseType = toolType.BaseType;
		while (baseType != null) {
			if (baseType.IsGenericType &&
			    baseType.GetGenericTypeDefinition() == typeof(HohoTool<>)) {
				return baseType.GetGenericArguments()[0];
			}
			baseType = baseType.BaseType;
		}

		return null;
	}

	/// <summary>
	/// Generate tool descriptions for LLM.
	/// </summary>
	public string GenerateToolDescriptions() {
		var descriptions = _tools.Values.AsValueEnumerable()
			.Select(tool => new {
				name         = tool.Name,
				description  = tool.Description,
				input_schema = GetToolInputSchema(tool)
			})
			.ToArray();

		return JsonSerializer.Serialize(descriptions, _jsonOptions);
	}

	/// <summary>
	/// Get input schema for a tool (for LLM understanding).
	/// </summary>
	private object? GetToolInputSchema(IHohoTool tool) {
		var inputType = GetToolInputType(tool);
		if (inputType == null) return null;

		// Build schema from properties
		var properties = inputType.GetProperties()
			.Where(p => p.CanWrite)
			.Select(p => new {
				name     = _jsonOptions.PropertyNamingPolicy?.ConvertName(p.Name) ?? p.Name,
				type     = GetJsonTypeName(p.PropertyType),
				required = IsRequired(p)
			})
			.ToArray();

		return new {
			type = "object",
			properties = properties.ToDictionary(p => p.name, p => (object)new {
				type     = p.type,
				required = p.required
			})
		};
	}

	private static string GetJsonTypeName(Type type) {
		// Handle nullable types
		var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

		return underlyingType switch {
			_ when underlyingType == typeof(string)                                                       => "string",
			_ when underlyingType == typeof(int) || underlyingType == typeof(long)                        => "integer",
			_ when underlyingType == typeof(bool)                                                         => "boolean",
			_ when underlyingType == typeof(double) || underlyingType == typeof(float)                    => "number",
			_ when underlyingType.IsArray || typeof(IEnumerable<object>).IsAssignableFrom(underlyingType) => "array",
			_                                                                                             => "object"
		};
	}

	private static bool IsRequired(PropertyInfo property) {
		// Check for required modifier (C# 11)
		return property.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>() != null ||
		       property.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null;
	}
}

/// <summary>
/// Tool execution context for advanced scenarios.
/// </summary>
public class ToolExecutionContext {
	public string?                     UserId      { get; set; }
	public string?                     SessionId   { get; set; }
	public string?                     ProjectPath { get; set; }
	public Dictionary<string, object>? Metadata    { get; set; }

	/// <summary>
	/// Permission check callback.
	/// </summary>
	public Func<string, object, Task<bool>>? CheckPermission { get; set; }

	/// <summary>
	/// Hook for pre-tool execution.
	/// </summary>
	public Func<string, object, Task>? PreExecuteHook { get; set; }

	/// <summary>
	/// Hook for post-tool execution.
	/// </summary>
	public Func<string, ToolResult, Task>? PostExecuteHook { get; set; }
}

/// <summary>
/// Advanced tool registry with context support.
/// </summary>
public class ContextualToolRegistry : ToolRegistry {
	private ToolExecutionContext? _currentContext;

	public void SetContext(ToolExecutionContext context) {
		_currentContext = context;
	}

	public async Task<ToolResult> ExecuteToolWithContextAsync(
		string            toolName,
		JsonElement       input,
		CancellationToken cancellationToken = default) {
		if (_currentContext?.CheckPermission != null) {
			var hasPermission = await _currentContext.CheckPermission(toolName, input);
			if (!hasPermission) {
				return ToolResult.Fail($"Permission denied for tool: {toolName}");
			}
		}

		if (_currentContext?.PreExecuteHook != null) {
			await _currentContext.PreExecuteHook(toolName, input);
		}

		var result = await ExecuteToolAsync(toolName, input, cancellationToken);

		if (_currentContext?.PostExecuteHook != null) {
			await _currentContext.PostExecuteHook(toolName, result);
		}

		return result;
	}
}