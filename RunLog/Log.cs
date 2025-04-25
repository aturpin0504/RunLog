using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RunLog
{
    // Core logger class that will be the main entry point
    public static class Log
    {
        /// <summary>
        /// The main logger instance used throughout the application.
        /// Set this property to configure the global logger.
        /// </summary>
        public static ILogger Logger { get; set; } = new EmptyLogger();

        /// <summary>
        /// Creates a new logger configuration that can be used to build a custom logger.
        /// </summary>
        /// <returns>A new LoggerConfiguration instance to configure log behavior.</returns>
        /// <example>
        /// <code>
        /// var logger = Log.CreateConfiguration()
        ///     .MinimumLevel.Information()
        ///     .WriteTo.Console()
        ///     .WriteTo.File("log.txt")
        ///     .CreateLogger();
        /// </code>
        /// </example>
        public static LoggerConfiguration CreateConfiguration() => new LoggerConfiguration();

        /// <summary>
        /// Closes and flushes the logger, ensuring all logs are written before application exit.
        /// Should be called before application shutdown.
        /// </summary>
        public static void CloseAndFlush()
        {
            Logger.Dispose();
        }

        /// <summary>
        /// Creates a logger that enriches log events with the specified type name.
        /// </summary>
        /// <typeparam name="T">The type to use as a source context.</typeparam>
        /// <returns>A logger that adds the type name as a property named "SourceContext".</returns>
        public static ILogger ForContext<T>() => Logger.ForContext<T>();

        /// <summary>
        /// Creates a logger that enriches log events with the specified context name.
        /// </summary>
        /// <param name="context">The context name to add to log events.</param>
        /// <returns>A logger that adds the context name as a property named "SourceContext".</returns>
        public static ILogger ForContext(string context) => Logger.ForContext(context);

        /// <summary>
        /// Writes a debug log event with the specified message template.
        /// </summary>
        /// <param name="messageTemplate">The message template containing property placeholders.</param>
        /// <param name="propertyValues">Values for the message template properties.</param>
        public static void Debug(string messageTemplate, params object[] propertyValues) =>
            Logger.Write(LogLevel.Debug, messageTemplate, propertyValues);

        /// <summary>
        /// Writes an information log event with the specified message template.
        /// </summary>
        /// <param name="messageTemplate">The message template containing property placeholders.</param>
        /// <param name="propertyValues">Values for the message template properties.</param>
        public static void Information(string messageTemplate, params object[] propertyValues) =>
            Logger.Write(LogLevel.Information, messageTemplate, propertyValues);

        /// <summary>
        /// Writes a warning log event with the specified message template.
        /// </summary>
        /// <param name="messageTemplate">The message template containing property placeholders.</param>
        /// <param name="propertyValues">Values for the message template properties.</param>
        public static void Warning(string messageTemplate, params object[] propertyValues) =>
            Logger.Write(LogLevel.Warning, messageTemplate, propertyValues);

        /// <summary>
        /// Writes an error log event with the specified message template.
        /// </summary>
        /// <param name="messageTemplate">The message template containing property placeholders.</param>
        /// <param name="propertyValues">Values for the message template properties.</param>
        public static void Error(string messageTemplate, params object[] propertyValues) =>
            Logger.Write(LogLevel.Error, messageTemplate, propertyValues);

        /// <summary>
        /// Writes an error log event with an exception and the specified message template.
        /// </summary>
        /// <param name="exception">The exception to include in the log event.</param>
        /// <param name="messageTemplate">The message template containing property placeholders.</param>
        /// <param name="propertyValues">Values for the message template properties.</param>
        public static void Error(Exception exception, string messageTemplate, params object[] propertyValues) =>
            Logger.Write(LogLevel.Error, exception, messageTemplate, propertyValues);

        public static void Fatal(string messageTemplate, params object[] propertyValues) =>
            Logger.Write(LogLevel.Fatal, messageTemplate, propertyValues);

        public static void Fatal(Exception exception, string messageTemplate, params object[] propertyValues) =>
            Logger.Write(LogLevel.Fatal, exception, messageTemplate, propertyValues);

        public static void Verbose(string messageTemplate, params object[] propertyValues) =>
            Logger.Write(LogLevel.Verbose, messageTemplate, propertyValues);

    }

    // Log levels
    public enum LogLevel
    {
        Verbose,
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    }

    // Interfaces
    public interface ILogger : IDisposable
    {
        ILogger ForContext<T>();
        ILogger ForContext(string context);
        void Write(LogLevel level, string messageTemplate, params object[] propertyValues);
        void Write(LogLevel level, Exception exception, string messageTemplate, params object[] propertyValues);
    }

    public interface ILogSink : IDisposable
    {
        void Emit(LogEvent logEvent);
    }

    // Logger Configuration
    public class LoggerConfiguration
    {
        private readonly List<ILogSink> _sinks = new List<ILogSink>();
        private readonly List<ILogEnricher> _enrichers = new List<ILogEnricher>();
        private LogLevel _minimumLevel = LogLevel.Information;

        public MinimumLevelConfiguration MinimumLevel => new MinimumLevelConfiguration(this);
        public SinkConfiguration WriteTo => new SinkConfiguration(this);
        public EnrichConfiguration Enrich => new EnrichConfiguration(this);

        internal void AddSink(ILogSink sink)
        {
            _sinks.Add(sink);
        }

        internal void AddEnricher(ILogEnricher enricher)
        {
            _enrichers.Add(enricher);
        }

        internal void SetMinimumLevel(LogLevel level)
        {
            _minimumLevel = level;
        }

        public ILogger CreateLogger()
        {
            // Create logger with the configured settings
            var logger = new Logger(_sinks, _enrichers, _minimumLevel);
            return logger;
        }
    }

    // Minimum level configuration
    public class MinimumLevelConfiguration
    {
        private readonly LoggerConfiguration _loggerConfiguration;

        public MinimumLevelConfiguration(LoggerConfiguration loggerConfiguration)
        {
            _loggerConfiguration = loggerConfiguration;
        }

        public LoggerConfiguration Verbose()
        {
            _loggerConfiguration.SetMinimumLevel(LogLevel.Verbose);
            return _loggerConfiguration;
        }

        public LoggerConfiguration Debug()
        {
            _loggerConfiguration.SetMinimumLevel(LogLevel.Debug);
            return _loggerConfiguration;
        }

        public LoggerConfiguration Information()
        {
            _loggerConfiguration.SetMinimumLevel(LogLevel.Information);
            return _loggerConfiguration;
        }

        public LoggerConfiguration Warning()
        {
            _loggerConfiguration.SetMinimumLevel(LogLevel.Warning);
            return _loggerConfiguration;
        }

        public LoggerConfiguration Error()
        {
            _loggerConfiguration.SetMinimumLevel(LogLevel.Error);
            return _loggerConfiguration;
        }

        public LoggerConfiguration Fatal()
        {
            _loggerConfiguration.SetMinimumLevel(LogLevel.Fatal);
            return _loggerConfiguration;
        }
    }

    // Sink configuration
    public class SinkConfiguration
    {
        private readonly LoggerConfiguration _loggerConfiguration;

        public SinkConfiguration(LoggerConfiguration loggerConfiguration)
        {
            _loggerConfiguration = loggerConfiguration;
        }

        public LoggerConfiguration Console(string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message}{NewLine}{Exception}")
        {
            _loggerConfiguration.AddSink(new ConsoleSink(outputTemplate));
            return _loggerConfiguration;
        }

        public LoggerConfiguration File(string path,
            RollingInterval rollingInterval = RollingInterval.Day,
            string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        {
            _loggerConfiguration.AddSink(new FileSink(path, rollingInterval, outputTemplate));
            return _loggerConfiguration;
        }
    }

    // Enrich configuration
    public class EnrichConfiguration
    {
        private readonly LoggerConfiguration _loggerConfiguration;

        public EnrichConfiguration(LoggerConfiguration loggerConfiguration)
        {
            _loggerConfiguration = loggerConfiguration;
        }

        public LoggerConfiguration WithProperty(string name, object value)
        {
            _loggerConfiguration.AddEnricher(new PropertyEnricher(name, value));
            return _loggerConfiguration;
        }

        public LoggerConfiguration WithMachineName()
        {
            _loggerConfiguration.AddEnricher(new MachineNameEnricher());
            return _loggerConfiguration;
        }
    }

    // Concrete logger implementation
    public class Logger : ILogger
    {
        private readonly List<ILogSink> _sinks;
        private readonly List<ILogEnricher> _enrichers;
        private readonly LogLevel _minimumLevel;
        private readonly Dictionary<string, object> _properties;

        public Logger(List<ILogSink> sinks, List<ILogEnricher> enrichers, LogLevel minimumLevel, Dictionary<string, object> properties = null)
        {
            _sinks = new List<ILogSink>(sinks);
            _enrichers = new List<ILogEnricher>(enrichers);
            _minimumLevel = minimumLevel;
            _properties = properties ?? new Dictionary<string, object>();
        }

        public ILogger ForContext<T>()
        {
            return ForContext(typeof(T).Name);
        }

        public ILogger ForContext(string context)
        {
            var contextProperties = new Dictionary<string, object>(_properties);
            contextProperties["SourceContext"] = context;
            return new Logger(_sinks, _enrichers, _minimumLevel, contextProperties);
        }

        public void Write(LogLevel level, string messageTemplate, params object[] propertyValues)
        {
            Write(level, null, messageTemplate, propertyValues);
        }

        public void Write(LogLevel level, Exception exception, string messageTemplate, params object[] propertyValues)
        {
            try
            {
                if (level < _minimumLevel)
                    return;

                if (messageTemplate == null)
                {
                    messageTemplate = exception?.Message ?? string.Empty;
                }

                var properties = new Dictionary<string, object>(_properties);

                // Add standard properties
                if (!properties.ContainsKey("Timestamp"))
                    properties["Timestamp"] = DateTimeOffset.Now;

                // Process property values from the message template
                var processedMessage = ProcessMessageTemplate(messageTemplate, propertyValues, properties);

                var logEvent = new LogEvent(
                    level,
                    processedMessage,
                    exception,
                    properties
                );

                // Apply enrichers
                try
                {
                    foreach (var enricher in _enrichers)
                    {
                        if (enricher != null)
                        {
                            enricher.Enrich(logEvent);
                        }
                    }
                }
                catch (Exception enricherEx)
                {
                    // If enrichers fail, add a note to the log but continue processing
                    Console.Error.WriteLine($"Error applying enrichers: {enricherEx.Message}");
                }

                // Emit to all sinks
                var emitExceptions = new List<Exception>();
                foreach (var sink in _sinks)
                {
                    if (sink == null) continue;

                    try
                    {
                        sink.Emit(logEvent);
                    }
                    catch (Exception sinkEx)
                    {
                        // Collect exceptions but don't let them stop other sinks
                        emitExceptions.Add(sinkEx);
                    }
                }

                // If any sinks failed, log to console as last resort
                if (emitExceptions.Count > 0)
                {
                    Console.Error.WriteLine($"Failed to emit log event to {emitExceptions.Count} sinks. First error: {emitExceptions[0].Message}");
                }
            }
            catch (Exception ex)
            {
                // Avoid throwing from logging methods - last resort fallback
                Console.Error.WriteLine($"Critical error in logger: {ex.Message}");
                Console.Error.WriteLine($"Original message: {messageTemplate}");
                Console.Error.WriteLine($"Original exception: {exception}");
            }
        }

        private string ProcessMessageTemplate(string messageTemplate, object[] propertyValues, Dictionary<string, object> properties)
        {
            // Add null check for the message template
            if (messageTemplate == null)
            {
                return string.Empty;
            }

            try
            {
                // Simple but effective parser for named properties
                if (propertyValues != null && propertyValues.Length > 0)
                {
                    // Extract property names from the template
                    var propertyNames = new List<string>();
                    int tokenStart = -1;
                    for (int i = 0; i < messageTemplate.Length; i++)
                    {
                        if (messageTemplate[i] == '{' && (i == 0 || messageTemplate[i - 1] != '\\'))
                        {
                            tokenStart = i + 1;
                        }
                        else if (messageTemplate[i] == '}' && tokenStart != -1)
                        {
                            // Guard against index out of range
                            if (tokenStart < i)
                            {
                                string propertyName = messageTemplate.Substring(tokenStart, i - tokenStart);
                                // Remove any format specifier
                                int formatSpecifierPos = propertyName.IndexOf(':');
                                if (formatSpecifierPos >= 0)
                                    propertyName = propertyName.Substring(0, formatSpecifierPos);

                                propertyNames.Add(propertyName);
                            }
                            tokenStart = -1;
                        }
                    }

                    // Associate values with names and add to properties
                    for (int i = 0; i < Math.Min(propertyNames.Count, propertyValues.Length); i++)
                    {
                        // Guard against null property names
                        if (!string.IsNullOrEmpty(propertyNames[i]))
                        {
                            properties[propertyNames[i]] = propertyValues[i];
                        }
                    }

                    // Format the message by replacing property placeholders
                    string result = messageTemplate;
                    for (int i = 0; i < Math.Min(propertyNames.Count, propertyValues.Length); i++)
                    {
                        // Guard against null or empty property names
                        if (string.IsNullOrEmpty(propertyNames[i]))
                            continue;

                        string propertyName = propertyNames[i];
                        string placeholder = "{" + propertyName + "}";

                        // Handle property with format specifier
                        int formatPos = messageTemplate.IndexOf(propertyName + ":", StringComparison.Ordinal);
                        if (formatPos > 0)
                        {
                            int closeBrace = messageTemplate.IndexOf('}', formatPos);
                            if (closeBrace > 0)
                            {
                                int openBracePos = messageTemplate.LastIndexOf('{', formatPos);
                                if (openBracePos >= 0) // Ensure we found an open brace
                                {
                                    string fullPlaceholder = messageTemplate.Substring(openBracePos,
                                                                                 closeBrace - openBracePos + 1);
                                    placeholder = fullPlaceholder;
                                }
                            }
                        }

                        result = result.Replace(placeholder, propertyValues[i]?.ToString() ?? "null");
                    }
                    return result;
                }

                return messageTemplate;
            }
            catch (Exception ex)
            {
                // Log internally and return the original template to avoid losing the message
                Console.Error.WriteLine($"Error processing message template: {ex.Message}");
                return messageTemplate;
            }
        }

        public void Dispose()
        {
            foreach (var sink in _sinks)
            {
                sink.Dispose();
            }
        }
    }

    // Empty logger for initial setup
    internal class EmptyLogger : ILogger
    {
        public ILogger ForContext<T>() => this;
        public ILogger ForContext(string context) => this;
        public void Write(LogLevel level, string messageTemplate, params object[] propertyValues) { }
        public void Write(LogLevel level, Exception exception, string messageTemplate, params object[] propertyValues) { }
        public void Dispose() { }
    }

    // Log event class
    public class LogEvent
    {
        public DateTimeOffset Timestamp => (DateTimeOffset)Properties["Timestamp"];
        public LogLevel Level { get; }
        public string MessageTemplate { get; }
        public string RenderedMessage { get; }
        public Exception Exception { get; }
        public Dictionary<string, object> Properties { get; }

        public LogEvent(LogLevel level, string renderedMessage, Exception exception, Dictionary<string, object> properties)
        {
            Level = level;
            MessageTemplate = renderedMessage;
            RenderedMessage = renderedMessage;
            Exception = exception;
            Properties = properties;
        }
    }

    // Enricher interface and implementations
    public interface ILogEnricher
    {
        void Enrich(LogEvent logEvent);
    }

    public class PropertyEnricher : ILogEnricher
    {
        private readonly string _name;
        private readonly object _value;

        public PropertyEnricher(string name, object value)
        {
            _name = name;
            _value = value;
        }

        public void Enrich(LogEvent logEvent)
        {
            logEvent.Properties[_name] = _value;
        }
    }

    public class MachineNameEnricher : ILogEnricher
    {
        public void Enrich(LogEvent logEvent)
        {
            logEvent.Properties["MachineName"] = Environment.MachineName;
        }
    }

    // Sink implementations
    public class ConsoleSink : ILogSink
    {
        private readonly string _outputTemplate;

        public ConsoleSink(string outputTemplate)
        {
            _outputTemplate = outputTemplate;
        }

        public void Emit(LogEvent logEvent)
        {
            var output = LogEventFormatter.FormatLogEvent(logEvent, _outputTemplate);

            // Set console color based on log level
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = GetColorForLogLevel(logEvent.Level);

            Console.WriteLine(output);

            // Reset console color
            Console.ForegroundColor = originalColor;
        }


        private ConsoleColor GetColorForLogLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Verbose:
                case LogLevel.Debug:
                    return ConsoleColor.Gray;
                case LogLevel.Information:
                    return ConsoleColor.White;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
                case LogLevel.Error:
                    return ConsoleColor.Red;
                case LogLevel.Fatal:
                    return ConsoleColor.DarkRed;
                default:
                    return ConsoleColor.White;
            }
        }

        public void Dispose() { }
    }

    public enum RollingInterval
    {
        Infinite,
        Year,
        Month,
        Day,
        Hour,
        Minute,
        Week
    }

    public class FileSink : ILogSink
    {
        private readonly string _pathFormat;
        private readonly RollingInterval _rollingInterval;
        private readonly string _outputTemplate;

        private string _currentFilePath;
        private StreamWriter _currentWriter;
        private DateTimeOffset _nextCheckpoint;

        public FileSink(string pathFormat, RollingInterval rollingInterval, string outputTemplate)
        {
            _pathFormat = pathFormat;
            _rollingInterval = rollingInterval;
            _outputTemplate = outputTemplate;

            // Initialize writer
            RollFile(DateTimeOffset.Now);
        }

        public void Emit(LogEvent logEvent)
        {
            try
            {
                // Check if we need to roll to a new file
                if (logEvent.Timestamp >= _nextCheckpoint)
                {
                    RollFile(logEvent.Timestamp);
                }

                // Format the log message
                string formattedLog = LogEventFormatter.FormatLogEvent(logEvent, _outputTemplate);

                // Write to file
                lock (this)
                {
                    if (_currentWriter == null)
                    {
                        // Try to re-initialize the writer if it's null
                        try
                        {
                            RollFile(logEvent.Timestamp);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Failed to create log file writer: {ex.Message}");
                            return;
                        }
                    }

                    try
                    {
                        _currentWriter.WriteLine(formattedLog);
                        _currentWriter.Flush();
                    }
                    catch (IOException ioEx)
                    {
                        Console.Error.WriteLine($"IO error writing to log file: {ioEx.Message}");
                        // Try to re-create the writer
                        try
                        {
                            _currentWriter?.Dispose();
                            _currentWriter = null;
                            RollFile(logEvent.Timestamp);
                            // Try writing one more time
                            _currentWriter?.WriteLine(formattedLog);
                            _currentWriter?.Flush();
                        }
                        catch
                        {
                            // If it still fails, there's not much we can do
                            Console.Error.WriteLine("Failed to recover from IO error in log file writer");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Last resort error handling
                Console.Error.WriteLine($"Error in FileSink.Emit: {ex.Message}");
            }
        }

        private void RollFile(DateTimeOffset now)
        {
            lock (this)
            {
                // Close the existing writer if it exists
                if (_currentWriter != null)
                {
                    _currentWriter.Dispose();
                    _currentWriter = null;
                }

                // Get new file path
                string filePath = ComputeFilePath(now);
                _currentFilePath = filePath;

                // Ensure directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create new writer
                var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                _currentWriter = new StreamWriter(fileStream, Encoding.UTF8);

                // Set next checkpoint
                _nextCheckpoint = ComputeNextCheckpoint(now);
            }
        }

        private string ComputeFilePath(DateTimeOffset now)
        {
            // If using infinite interval or pathFormat doesn't contain date format placeholder,
            // just return the original path
            if (_rollingInterval == RollingInterval.Infinite)
                return _pathFormat;

            string extension = Path.GetExtension(_pathFormat);
            string basePath = Path.Combine(
                Path.GetDirectoryName(_pathFormat) ?? "",
                Path.GetFileNameWithoutExtension(_pathFormat)
            );

            string suffix = "";
            switch (_rollingInterval)
            {
                case RollingInterval.Year:
                    suffix = now.ToString("yyyy");
                    break;
                case RollingInterval.Month:
                    suffix = now.ToString("yyyyMM");
                    break;
                case RollingInterval.Day:
                    suffix = now.ToString("yyyyMMdd");
                    break;
                case RollingInterval.Hour:
                    suffix = now.ToString("yyyyMMddHH");
                    break;
                case RollingInterval.Minute:
                    suffix = now.ToString("yyyyMMddHHmm");
                    break;
                case RollingInterval.Week:
                    // Get the date of the start of the week (assuming Sunday is the first day)
                    int diff = (7 + (now.DayOfWeek - DayOfWeek.Sunday)) % 7;
                    var startOfWeek = now.AddDays(-1 * diff);
                    suffix = startOfWeek.ToString("yyyyMMdd");
                    break;
            }

            return $"{basePath}_{suffix}{extension}";
        }

        private DateTimeOffset ComputeNextCheckpoint(DateTimeOffset now)
        {
            switch (_rollingInterval)
            {
                case RollingInterval.Infinite:
                    return DateTimeOffset.MaxValue;
                case RollingInterval.Year:
                    return new DateTimeOffset(now.Year + 1, 1, 1, 0, 0, 0, now.Offset);
                case RollingInterval.Month:
                    return new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset).AddMonths(1);
                case RollingInterval.Day:
                    return now.Date.AddDays(1);
                case RollingInterval.Hour:
                    return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset).AddHours(1);
                case RollingInterval.Minute:
                    return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset).AddMinutes(1);
                case RollingInterval.Week:
                    // Calculate days until next Sunday
                    int daysUntilNextWeek = (7 - (int)now.DayOfWeek) % 7;
                    if (daysUntilNextWeek == 0) daysUntilNextWeek = 7; // If today is Sunday, go to next Sunday
                    return now.Date.AddDays(daysUntilNextWeek);
                default:
                    return DateTimeOffset.MaxValue;
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                _currentWriter?.Dispose();
                _currentWriter = null;
            }
        }
    }

    // Helper method for formatting log events
    public static class LogEventFormatter
    {
        public static string FormatLogEvent(LogEvent logEvent, string template)
        {
            // Replace known tokens in the template
            var result = template
                .Replace("{Timestamp}", logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{Timestamp:yyyy-MM-dd HH:mm:ss}", logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}", logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                .Replace("{Level}", logEvent.Level.ToString())
                .Replace("{Level:u3}", logEvent.Level.ToString().Substring(0, Math.Min(3, logEvent.Level.ToString().Length)).ToUpper())
                .Replace("{Message}", logEvent.RenderedMessage)
                .Replace("{Message:lj}", logEvent.RenderedMessage) // lj = line-joined, simplified here
                .Replace("{NewLine}", Environment.NewLine)
                .Replace("{Exception}", logEvent.Exception?.ToString() ?? "");

            // Process other properties
            foreach (var prop in logEvent.Properties)
            {
                result = result.Replace($"{{{prop.Key}}}", prop.Value?.ToString() ?? "");
            }

            // Handle the case when SourceContext is not present
            if (!logEvent.Properties.ContainsKey("SourceContext"))
            {
                result = result.Replace("{SourceContext} ", ""); // Remove the token and the space after it
            }

            return result;
        }
    }

    // Extension method for the formatters - add this at the end of your namespace
    public static class LogSinkExtensions
    {
        public static string FormatLogEvent(this ILogSink sink, LogEvent logEvent, string template)
        {
            return LogEventFormatter.FormatLogEvent(logEvent, template);
        }
    }
    public static class LoggerExtensions
    {
        public static void Verbose(this ILogger logger, string messageTemplate, params object[] propertyValues) =>
            logger.Write(LogLevel.Verbose, messageTemplate, propertyValues);

        public static void Debug(this ILogger logger, string messageTemplate, params object[] propertyValues) =>
            logger.Write(LogLevel.Debug, messageTemplate, propertyValues);

        public static void Information(this ILogger logger, string messageTemplate, params object[] propertyValues) =>
            logger.Write(LogLevel.Information, messageTemplate, propertyValues);

        public static void Warning(this ILogger logger, string messageTemplate, params object[] propertyValues) =>
            logger.Write(LogLevel.Warning, messageTemplate, propertyValues);

        /// <summary>
        /// Writes a warning log event with an exception and the specified message template.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="exception">The exception to include in the log event.</param>
        /// <param name="messageTemplate">The message template containing property placeholders.</param>
        /// <param name="propertyValues">Values for the message template properties.</param>
        public static void Warning(this ILogger logger, Exception exception, string messageTemplate, params object[] propertyValues) =>
            logger.Write(LogLevel.Warning, exception, messageTemplate, propertyValues);

        public static void Error(this ILogger logger, string messageTemplate, params object[] propertyValues) =>
            logger.Write(LogLevel.Error, messageTemplate, propertyValues);

        public static void Error(this ILogger logger, Exception exception, string messageTemplate, params object[] propertyValues) =>
            logger.Write(LogLevel.Error, exception, messageTemplate, propertyValues);

        public static void Fatal(this ILogger logger, string messageTemplate, params object[] propertyValues) =>
            logger.Write(LogLevel.Fatal, messageTemplate, propertyValues);

        public static void Fatal(this ILogger logger, Exception exception, string messageTemplate, params object[] propertyValues) =>
            logger.Write(LogLevel.Fatal, exception, messageTemplate, propertyValues);
    }
}
