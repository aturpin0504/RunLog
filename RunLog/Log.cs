using System;
using System.Collections.Generic;

namespace RunLog
{
    public class Log
    {
        private static readonly object _syncRoot = new object();
        private static volatile Logger _logger;

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

        // Static methods that forward to the Logger property - with null checking
        public static void Verbose(string messageTemplate, params object[] propertyValues)
        {
            Logger.Verbose(messageTemplate, propertyValues);
        }

        public static void Debug(string messageTemplate, params object[] propertyValues)
        {
            Logger.Debug(messageTemplate, propertyValues);
        }

        public static void Information(string messageTemplate, params object[] propertyValues)
        {
            Logger.Information(messageTemplate, propertyValues);
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
                // Since we no longer use ILogEventSink, we need to handle concrete sink types
                if (_logger != null)
                {
                    // Get file sinks for flushing
                    foreach (var sink in _logger.Sinks) // Use the new Sinks property instead of _sinks
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

                // Clear the disposable sinks list
                _disposableSinks.Clear();
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