# RunLog - Lightweight Logging Framework

## Overview

RunLog is a lightweight, customizable logging framework inspired by Serilog. It provides a fluent API for configuring loggers with various sinks, enrichers, and minimum log levels. The framework supports both contextual logging and structured log templates.

## Features

- **Fluent Configuration API**: Easy-to-read logger setup with method chaining
- **Multiple Sinks**: Log to console and files simultaneously
- **Rolling File Logs**: Support for various rolling intervals (Daily, Weekly, Monthly, etc.)
- **Contextual Logging**: Create specialized loggers for different components
- **Thread-Safe**: Concurrent writing to log files is handled automatically
- **Custom Output Templates**: Format log messages with placeholders like timestamps and log levels
- **Property Enrichment**: Add additional properties to all log entries
- **Log Levels**: Standard levels from Verbose to Fatal with filtering

## Installation
Install the package via NuGet:
```bash

Install-Package RunLog
```

## Getting Started

### Basic Setup

```csharp
// Configure and create a logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("Logs/app.log", rollingInterval: RollingInterval.Day)
    .Enrich.WithProperty("Application", "MyApp")
    .Enrich.WithMachineName()
    .CreateLogger();

// Write log messages
Log.Information("Application started");
Log.Warning("Something might be wrong with {ItemName}", "database");
Log.Error(exception, "An error occurred");

// Remember to flush and close when your application exits
Log.CloseAndFlush();
```

## Log Levels

The framework supports the following log levels (in order of increasing severity):

1. **Verbose** - Very detailed logs for debugging
2. **Debug** - Debugging information
3. **Information** - General information
4. **Warning** - Potential issues
5. **Error** - Actual errors
6. **Fatal** - Critical errors that might cause application failure

## Context-Specific Logging

Create specialized loggers for different components:

```csharp
// Define contexts (recommended to use constants)
public static class LogContexts
{
    public const string Service = "Service";
    public const string FileOperations = "FileOperations";
    public const string Database = "Database";
}

// Create context-specific loggers
var fileLogger = Log.ForContext(LogContexts.FileOperations);
fileLogger.Information("Processing file {FileName}", "data.csv");

var dbLogger = Log.ForContext(LogContexts.Database);
dbLogger.Warning("Database connection pool is {PoolSize} connections", 5);

// Use class-based context
var classLogger = Log.ForContext<MyClass>();
classLogger.Information("Operation from MyClass");
```

## Multiple Independent Loggers

Create multiple loggers with different configurations:

```csharp
// Service logger with console and file output
var serviceLogger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/service-.txt", rollingInterval: RollingInterval.Week)
    .CreateLogger()
    .ForContext(LogContexts.Service);

// File operations logger with only file output
var fileLogger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs/fileops-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger()
    .ForContext(LogContexts.FileOperations);

// Set one as the default logger (optional)
Log.Logger = serviceLogger;
```

## Output Templates

Customize log format with output templates:

```csharp
.WriteTo.Console(
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message}{NewLine}{Exception}")
```

Available placeholders:
- **{Timestamp}** - When the log event occurred
- **{Level}** - The log level
- **{Message}** - The log message
- **{Exception}** - Exception details
- **{NewLine}** - Platform-specific line break
- **{SourceContext}** - Source context of the log
- Any custom properties added through enrichers or in log messages

## Adding Custom Properties

Add properties to individual log entries:

```csharp
Log.Information("User {UserId} logged in from {IpAddress}", 123, "192.168.1.1");
```

Or enrich all logs with common properties:

```csharp
.Enrich.WithProperty("Environment", "Production")
.Enrich.WithMachineName()
```

## Best Practices

1. **Define Log Contexts**: Use a static class with string constants for your log contexts
2. **Always Close and Flush**: Call `Log.CloseAndFlush()` before your application exits
3. **Structure Your Log Messages**: Use named properties like {PropertyName} in messages
4. **Set Appropriate Log Levels**: Use Verbose and Debug for development, Information and above for production
5. **Use Context-Specific Loggers**: Create specialized loggers for different components
6. **Inject Loggers**: Pass loggers as dependencies to classes that need them

## Thread Safety

The framework is thread-safe:

- **FileSink** uses locks to prevent concurrent file access issues
- **Logger** class can be safely used from multiple threads

## Implementation Details

The framework consists of several key components:

- **Log**: Static entry point for logging
- **Logger**: Core implementation of the logging logic
- **LoggerConfiguration**: Builder for creating configured loggers
- **ILogSink**: Interface for output destinations (Console, File)
- **ILogEnricher**: Interface for adding properties to log events

## License
This project is licensed under the MIT License. See the LICENSE file for details.