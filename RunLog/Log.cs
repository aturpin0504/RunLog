using System;
using System.Collections.Generic;

namespace RunLog
{
    public class Log
    {
        private static readonly object _syncRoot = new object();
        private static volatile Logger _logger;

        // A static dictionary to store named logger instances
        private static readonly Dictionary<string, Logger> _loggerInstances =
            new Dictionary<string, Logger>(StringComparer.OrdinalIgnoreCase);

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
        public static Logger CreateLogger(LoggerConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            return new Logger(
                configuration.MinimumLevelValue,
                configuration.SinksList,
                configuration.Enrichers);
        }

        // Add method to register a named logger instance
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

        // Add method to get a named logger instance
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

        // Add method to check if a named logger exists
        public static bool LoggerExists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (_syncRoot)
            {
                return _loggerInstances.ContainsKey(name);
            }
        }

        // Static methods that forward to the Logger property - with null checking
        public static void Verbose(string messageTemplate, params object[] propertyValues)
        {
            Logger.Verbose(messageTemplate, propertyValues);
        }

        public static void Verbose(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Verbose(exception, messageTemplate, propertyValues);
        }

        public static void Debug(string messageTemplate, params object[] propertyValues)
        {
            Logger.Debug(messageTemplate, propertyValues);
        }

        public static void Debug(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Debug(exception, messageTemplate, propertyValues);
        }

        public static void Information(string messageTemplate, params object[] propertyValues)
        {
            Logger.Information(messageTemplate, propertyValues);
        }

        public static void Information(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Information(exception, messageTemplate, propertyValues);
        }

        public static void Warning(string messageTemplate, params object[] propertyValues)
        {
            Logger.Warning(messageTemplate, propertyValues);
        }

        public static void Warning(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Warning(exception, messageTemplate, propertyValues);
        }

        public static void Error(string messageTemplate, params object[] propertyValues)
        {
            Logger.Error(messageTemplate, propertyValues);
        }

        public static void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Logger.Error(exception, messageTemplate, propertyValues);
        }

        public static void Fatal(string messageTemplate, params object[] propertyValues)
        {
            Logger.Fatal(messageTemplate, propertyValues);
        }

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

        // Helper method to flush and dispose a logger
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

        // Add application shutdown handler to ensure logs are flushed
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