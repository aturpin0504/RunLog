# RunLog

[![NuGet](https://img.shields.io/nuget/v/RunLog.svg)](https://www.nuget.org/packages/RunLog)

RunLog is a lightweight, customizable logging framework inspired by Serilog. It provides a fluent API for configuring loggers with various sinks, enrichers, and minimum log levels. The framework supports both contextual logging and structured log templates.

## Features

- Fluent configuration API
- Multiple log levels (Verbose, Debug, Information, Warning, Error, Fatal)
- Multiple built-in sink types (Console, File)
- File sinks are buffered by default for better performance
- Customizable minimum log levels per sink
- File rolling capabilities (by time interval or size)
- Structured logging with property value capturing
- Thread-safe operation
- Exception logging
- Custom formatters support
- Enrichers for adding custom properties

## Installation

Install the RunLog package from NuGet:

```bash
Install-Package RunLog
# or
dotnet add package RunLog
```

Package URL: [NuGet Gallery](https://www.nuget.org/packages/RunLog)

## Quick Start

**Important**: You must configure the logger by setting `Log.Logger` before using any logging methods. If you attempt to use logging methods before setting `Log.Logger`, an `InvalidOperationException` will be thrown.

Here's a simple example to get you started:

```csharp
using RunLog;

// Configure the logger (this step is required)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day)
    .Enrich("ApplicationName", "MyApp")
    .Enrich("Version", "1.0.0")
    .CreateLogger();

// Log some messages
Log.Information("Application {AppName} starting up", "MyApp");
Log.Debug("Processing item {ItemId}", 42);

try
{
    // Some operation that might throw
    throw new System.InvalidOperationException("Something went wrong");
}
catch (Exception ex)
{
    Log.Error(ex, "Error processing request");
}

// Optional: You can manually flush logs at any point if needed
// Log.CloseAndFlush();
```

When you set `Log.Logger`, RunLog automatically registers shutdown handlers to ensure logs are properly flushed when your application exits, so you typically don't need to call `Log.CloseAndFlush()` explicitly.

## Configuration

RunLog uses a fluent configuration API to set up logging:

### Setting Minimum Level

```csharp
// Set minimum level for all sinks
var config = new LoggerConfiguration()
    .MinimumLevel.Information();  // Only Information and above will be logged

// Other available levels:
// .MinimumLevel.Verbose()
// .MinimumLevel.Debug()
// .MinimumLevel.Warning()
// .MinimumLevel.Error()
// .MinimumLevel.Fatal()
```

### Console Sink

```csharp
var config = new LoggerConfiguration()
    .WriteTo.Console();  // Uses the global minimum level

// Or specify a minimum level for this sink only
var config = new LoggerConfiguration()
    .WriteTo.Console(LogLevel.Warning);  // Only Warning and above will go to console
```

### File Sink

File sinks are buffered by default for improved performance.

```csharp
// Basic file sink (buffered by default)
var config = new LoggerConfiguration()
    .WriteTo.File("logs/app.log");

// File sink with custom settings
var config = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        restrictedToMinimumLevel: LogLevel.Information,
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 10 * 1024 * 1024,  // 10 MB
        retainedFileCountLimit: 31);  // Keep last 31 files

// File sink with custom buffering settings
var config = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        restrictedToMinimumLevel: LogLevel.Debug,
        enableBuffering: true,  // Enabled by default
        bufferSize: 500,        // Custom buffer size
        flushInterval: TimeSpan.FromSeconds(3));  // Custom flush interval

// Disable buffering for immediate writes (may impact performance)
var config = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        enableBuffering: false);  // Disable buffering for immediate writes
```

### Multiple Sinks

```csharp
var config = new LoggerConfiguration()
    .MinimumLevel.Debug()  // Global minimum
    .WriteTo.Console(LogLevel.Information)  // Console shows Info+
    .WriteTo.File("logs/app.log", LogLevel.Debug)  // File includes Debug+
    .WriteTo.File("logs/errors.log", LogLevel.Error);  // Only Error+
```

### Enriching Log Events

Add custom properties to all log events:

```csharp
var config = new LoggerConfiguration()
    .Enrich("ApplicationName", "MyApp")
    .Enrich("Environment", "Production")
    .Enrich("Version", "1.0.0");
```

## Logging Examples

### Basic Logging

```csharp
Log.Verbose("This is a verbose message");
Log.Debug("This is a debug message");
Log.Information("This is an information message");
Log.Warning("This is a warning message");
Log.Error("This is an error message");
Log.Fatal("This is a fatal error message");
```

### Structured Logging

```csharp
// Log with named properties
Log.Information("User {UserId} logged in from {IpAddress}", 123, "192.168.1.1");

// Multiple properties
Log.Information(
    "Order {OrderId} placed for {Amount} items by customer {CustomerId}",
    "ORD-12345",
    42,
    "CUST-789");
```

### Logging Exceptions

```csharp
try
{
    // Code that might throw
    throw new InvalidOperationException("Something went wrong");
}
catch (Exception ex)
{
    // Log with the exception
    Log.Error(ex, "Failed to process transaction {TransactionId}", "TXN-123");
}
```

### Formatted Values

```csharp
// Formatting numbers
Log.Information("Amount: {Amount:C}", 123.45);  // Currency
Log.Information("Percentage: {Percent:P}", 0.75);  // Percentage
Log.Information("Hex value: {Value:X}", 255);  // Hex

// Formatting dates
Log.Information("Date: {Date:yyyy-MM-dd}", DateTime.Now);
Log.Information("Time: {Time:HH:mm:ss}", DateTime.Now);
```

## Advanced Usage

### Creating a Custom Instance

If you need a separate logger instance with different configuration:

```csharp
// Create a specific logger for a component
var componentLogger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs/component.log")
    .Enrich("Component", "PaymentProcessor")
    .CreateLogger();

// Use the component-specific logger
componentLogger.Information("Payment processed for {OrderId}", "ORD-123");
```

### Changing Logger at Runtime

You can replace the global logger at runtime:

```csharp
// Initial configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

// Later, change to a different configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()  // More verbose
    .WriteTo.Console()
    .WriteTo.File("logs/app.log")  // Add file logging
    .CreateLogger();
```

### Manual Flushing

While RunLog automatically registers shutdown handlers to flush logs when the application exits, you can manually flush logs at any time if needed:

```csharp
// Force immediate flush of any buffered logs
Log.CloseAndFlush();
```

### File Rolling Options

RunLog supports various file rolling strategies:

```csharp
// Roll by time interval
var config = new LoggerConfiguration()
    .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day);  // _yyyyMMdd suffix

// Available intervals:
// RollingInterval.Infinite - no rolling
// RollingInterval.Year - _yyyy suffix
// RollingInterval.Month - _yyyyMM suffix
// RollingInterval.Week - _yyyyWww suffix (ISO week)
// RollingInterval.Day - _yyyyMMdd suffix
// RollingInterval.Hour - _yyyyMMddHH suffix
// RollingInterval.Minute - _yyyyMMddHHmm suffix

// Roll by file size (creates numerical sequence)
var config = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        fileSizeLimitBytes: 1024 * 1024);  // Roll at 1 MB
```

### Combining Size and Time-Based Rolling

```csharp
var config = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 10 * 1024 * 1024,  // 10 MB
        retainedFileCountLimit: 7);  // Keep 7 files
```

## Performance Considerations

File sinks are now buffered by default for better performance, but you can still adjust the buffering settings:

```csharp
// High-performance configuration with custom buffer settings
var config = new LoggerConfiguration()
    .MinimumLevel.Information()  // Filter out Debug and Verbose
    .WriteTo.File(
        path: "logs/app.log",
        bufferSize: 1000,  // Buffer up to 1000 log events
        flushInterval: TimeSpan.FromSeconds(2));  // Flush every 2 seconds
```

For scenarios requiring immediate writes regardless of performance impact:

```csharp
// Disable buffering for immediate writes
var config = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app.log",
        enableBuffering: false);  // Immediate writes
```

## Error Handling

RunLog includes built-in error handling with retry mechanisms and fallback logging:

```csharp
// The File sink will automatically:
// - Retry file writes up to 3 times with increasing delay
// - Fall back to temporary directory logging if the target location is inaccessible
// - Handle transient file access issues gracefully
```

## Thread Safety

All logging operations in RunLog are thread-safe by default:

```csharp
// These operations can be safely called from multiple threads
Log.Information("Processing request from thread {ThreadId}", Thread.CurrentThread.ManagedThreadId);
```

## Full Example

Here's a comprehensive example showcasing multiple features:

```csharp
using System;
using System.Threading;
using RunLog;

namespace LoggingDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            // Configure the logging system
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich("Application", "LoggingDemo")
                .Enrich("Environment", "Development")
                .Enrich("Version", "1.0.0")
                .WriteTo.Console(LogLevel.Information)
                .WriteTo.File(
                    path: "logs/app.log", 
                    restrictedToMinimumLevel: LogLevel.Information,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    retainedFileCountLimit: 7)
                .WriteTo.File(
                    path: "logs/debug.log",
                    restrictedToMinimumLevel: LogLevel.Debug,
                    rollingInterval: RollingInterval.Day,
                    bufferSize: 100,
                    flushInterval: TimeSpan.FromSeconds(5))
                .CreateLogger();
            
            try
            {
                Log.Information("Application starting up");
                
                // Simulating application activity
                for (int i = 0; i < 5; i++)
                {
                    Log.Debug("Processing item {ItemIndex}", i);
                    
                    if (i % 2 == 0)
                    {
                        Log.Information("Successfully processed item {ItemIndex}", i);
                    }
                    else
                    {
                        Log.Warning("Item {ItemIndex} had issues during processing", i);
                    }
                    
                    Thread.Sleep(100);  // Simulate work
                }
                
                // Simulate an error
                try
                {
                    throw new InvalidOperationException("Simulated error for demonstration");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error occurred during processing of {Operation}", "DemoOperation");
                }
                
                Log.Information("Application shutting down normally");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Unexpected error caused application to terminate");
                throw;
            }
            // No need to call Log.CloseAndFlush() as it's automatically handled on application exit
        }
    }
}
```

## Dependencies

RunLog 3.2.0 has no external dependencies. All necessary functionality for date/time calculations and file operations is now implemented directly within the package.

### Changes in 3.2.0

- Removed dependency on AaTurpin.Utilities package
- Implemented internal versions of date/time utilities and file path handling
- Removed named logger functionality (RegisterLogger, GetLogger, LoggerExists)
- Maintained compatibility with all core logging features

Applications using only the standard logging API (Log.Information, Log.Error, etc.) should require no code changes. Applications using named loggers will need to implement their own mechanism or update to use the simplified logger approach.

## License

MIT