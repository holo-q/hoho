using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Hoho.Core;

/// <summary>
/// ZERO-ALLOCATION high-performance logging service.
/// Uses Serilog with structured logging and performance optimizations.
/// </summary>
public static class Logger
{
    private static readonly Serilog.ILogger _logger = CreateLogger();
    
    /// <summary>
    /// Initialize high-performance Serilog logger with structured output.
    /// </summary>
    private static Serilog.ILogger CreateLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                path: "logs/hoho-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger();
    }
    
    /// <summary>
    /// Log information with zero-allocation string interpolation.
    /// </summary>
    public static void Info(string message) => _logger.Information(message);
    
    /// <summary>
    /// Log information with structured parameters (zero-allocation).
    /// </summary>
    public static void Info<T>(string template, T propertyValue) => 
        _logger.Information(template, propertyValue);
    
    /// <summary>
    /// Log information with two structured parameters.
    /// </summary>
    public static void Info<T1, T2>(string template, T1 propertyValue1, T2 propertyValue2) => 
        _logger.Information(template, propertyValue1, propertyValue2);
    
    /// <summary>
    /// Log warning with zero-allocation performance.
    /// </summary>
    public static void Warn(string message) => _logger.Warning(message);
    
    /// <summary>
    /// Log warning with structured parameters.
    /// </summary>
    public static void Warn<T>(string template, T propertyValue) => 
        _logger.Warning(template, propertyValue);
    
    /// <summary>
    /// Log warning (alias for Warn for compatibility).
    /// </summary>
    public static void Warning(string message) => Warn(message);
    
    /// <summary>
    /// Log success message (displayed as Info with green color in console).
    /// </summary>
    public static void Success(string message) => Info($"✓ {message}");
    
    /// <summary>
    /// Log error with zero-allocation performance.
    /// </summary>
    public static void Error(string message) => _logger.Error(message);
    
    /// <summary>
    /// Log error with exception and structured parameters.
    /// </summary>
    public static void Error(Exception ex, string template) => 
        _logger.Error(ex, template);
    
    /// <summary>
    /// Log error with exception and one parameter.
    /// </summary>
    public static void Error<T>(Exception ex, string template, T propertyValue) => 
        _logger.Error(ex, template, propertyValue);
    
    /// <summary>
    /// Log debug information (file only in production).
    /// </summary>
    public static void Debug(string message) => _logger.Debug(message);
    
    /// <summary>
    /// Log debug with structured parameters.
    /// </summary>
    public static void Debug<T>(string template, T propertyValue) => 
        _logger.Debug(template, propertyValue);
    
    /// <summary>
    /// Performance timing wrapper with automatic disposal.
    /// </summary>
    public static IDisposable TimeOperation(string operationName)
    {
        return new PerformanceTimer(operationName);
    }
    
    /// <summary>
    /// Dispose logger resources on application shutdown.
    /// </summary>
    public static void Shutdown()
    {
        Log.CloseAndFlush();
    }
}

/// <summary>
/// High-performance timing wrapper for operations.
/// </summary>
internal sealed class PerformanceTimer : IDisposable
{
    private readonly string _operationName;
    private readonly long _startTicks;
    
    public PerformanceTimer(string operationName)
    {
        _operationName = operationName;
        _startTicks = Environment.TickCount64;
        Logger.Debug("Starting operation {OperationName}", operationName);
    }
    
    public void Dispose()
    {
        var elapsed = Environment.TickCount64 - _startTicks;
        Logger.Debug("Completed operation {OperationName} in {ElapsedMs}ms".Replace("{OperationName}", _operationName).Replace("{ElapsedMs}", elapsed.ToString()));
    }
}