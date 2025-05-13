using System;
using System.Collections.Generic;

namespace RunLog
{
    /// <summary>
    /// Static class that provides access to the global logger instance and manages logger lifecycle.
    /// </summary>
    public class Log
    {
        private static readonly object _syncRoot = new object();
        private static volatile Logger _logger;

        // A static dictionary to store named logger instances
        private static readonly Dictionary<string, Logger> _loggerInstances =
            new Dictionary<string, Logger>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the default logger instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when trying to get the logger before it has been configured.</exception>
        public static Logger Logger
        {
            get
            {
                if (_logger == null)
                    throw new InvalidOperationException("Logger has not been configured. Call Log.Logger = new LoggerConfiguration().CreateLogger() before use.");

                return _logger;
            }
            set
            {
                lock (_syncRoot)
                {
                    // Clean up old logger if needed
                    CloseAndFlush();
                    _logger = value;

                    // Automatically register shutdown handler when Logger is configured
                    RegisterShutdownHandler();
                }
            }
        }

        private static readonly List<IDisposable> _disposableSinks = new List<IDisposable>();

        /// <summary>
        /// Creates a logger from the supplied configuration.
        /// </summary>
        /// <param name="configuration">The configuration to use for creating the logger.</param>
        /// <returns>A new logger instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the configuration is null.</exception>
        public static Logger CreateLogger(LoggerConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            return new Logger(
                configuration.MinimumLevelValue,
                configuration.SinksList,
                configuration.Enrichers);
        }

        /// <summary>
        /// Registers a named logger instance.
        /// </summary>
        /// <param name="name">The name to register the logger under.</param>
        /// <param name="logger">The logger instance to register.</param>
        /// <exception cref="ArgumentException">Thrown when the name is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the logger is null.</exception>
        public static void RegisterLogger(string name, Logger logger)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Logger name cannot be null or empty", nameof(name));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            lock (_syncRoot)
            {
                _loggerInstances[name] = logger;
            }
        }

        /// <summary>
        /// Gets a registered logger instance by name.
        /// </summary>
        /// <param name="name">The name of the logger to retrieve.</param>
        /// <returns>The registered logger instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the name is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no logger is registered with the specified name.</exception>
        public static Logger GetLogger(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Logger name cannot be null or empty", nameof(name));

            lock (_syncRoot)
            {
                if (_loggerInstances.TryGetValue(name, out Logger logger))
                    return logger;

                throw new InvalidOperationException($"Logger with name '{name}' has not been registered.");
            }
        }

        /// <summary>
        /// Checks if a named logger exists.
        /// </summary>
        /// <param name="name">The name of the logger to check for.</param>
        /// <returns>True if a logger with the specified name exists, false otherwise.</returns>
        public static bool LoggerExists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (_syncRoot)
            {
                return _loggerInstances.ContainsKey(name);
            }
        }

        /// <summary>
        /// Writes a verbose log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Verbose(string messageTemplate, params object[] propertyValues)
        {
            Logger.Verbose(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes a verbose log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Verbose(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Verbose(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes a debug log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Debug(string messageTemplate, params object[] propertyValues)
        {
            Logger.Debug(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes a debug log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Debug(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Debug(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes an information log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Information(string messageTemplate, params object[] propertyValues)
        {
            Logger.Information(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes an information log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Information(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Information(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes a warning log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Warning(string messageTemplate, params object[] propertyValues)
        {
            Logger.Warning(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes a warning log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Warning(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Warning(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes an error log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Error(string messageTemplate, params object[] propertyValues)
        {
            Logger.Error(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes an error log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Error(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes a fatal log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Fatal(string messageTemplate, params object[] propertyValues)
        {
            Logger.Fatal(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Writes a fatal log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public static void Fatal(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Fatal(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Flushes any buffered log events and closes/disposes resources.
        /// </summary>
        public static void CloseAndFlush()
        {
            lock (_syncRoot)
            {
                // Process the default logger
                if (_logger != null)
                {
                    FlushAndDisposeLogger(_logger);
                }

                // Process all registered loggers
                foreach (var loggerEntry in _loggerInstances)
                {
                    FlushAndDisposeLogger(loggerEntry.Value);
                }

                // Clear the disposable sinks list
                _disposableSinks.Clear();

                // Clear the logger instances
                _loggerInstances.Clear();
            }
        }

        /// <summary>
        /// Helper method to flush and dispose a logger.
        /// </summary>
        /// <param name="logger">The logger to flush and dispose.</param>
        private static void FlushAndDisposeLogger(Logger logger)
        {
            foreach (var sink in logger.Sinks)
            {
                if (sink is FileSink fileSink)
                {
                    try
                    {
                        fileSink.FlushBuffer();
                        fileSink.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error flushing/disposing sink: {ex.Message}");
                    }
                }
                else if (sink is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing sink: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Registers shutdown handlers to ensure logs are flushed when the application terminates.
        /// </summary>
        private static void RegisterShutdownHandler()
        {
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => CloseAndFlush();
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (_logger != null)
                {
                    Fatal("Unhandled exception: {Exception}", e.ExceptionObject);
                    CloseAndFlush();
                }
            };
        }
    }
}
