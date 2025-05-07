using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RunLog
{
    /// <summary>
    /// Defines the frequency at which log files should be rotated.
    /// </summary>
    public enum RollingInterval
    {
        /// <summary>
        /// No rotation - use a single log file indefinitely.
        /// </summary>
        Infinite,

        /// <summary>
        /// Create a new log file each year.
        /// </summary>
        Year,

        /// <summary>
        /// Create a new log file each month.
        /// </summary>
        Month,

        /// <summary>
        /// Create a new log file each week.
        /// </summary>
        Week,

        /// <summary>
        /// Create a new log file each day.
        /// </summary>
        Day,

        /// <summary>
        /// Create a new log file each hour.
        /// </summary>
        Hour,

        /// <summary>
        /// Create a new log file each minute.
        /// </summary>
        Minute
    }

    /// <summary>
    /// Defines the severity level of log messages.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Verbose - most detailed level, typically used for debugging.
        /// </summary>
        Verbose,

        /// <summary>
        /// Debug - detailed information useful for debugging.
        /// </summary>
        Debug,

        /// <summary>
        /// Information - general information about application flow.
        /// </summary>
        Information,

        /// <summary>
        /// Warning - potential issues that don't prevent the application from functioning.
        /// </summary>
        Warning,

        /// <summary>
        /// Error - issues that prevent specific operations from functioning correctly.
        /// </summary>
        Error,

        /// <summary>
        /// Fatal - severe errors that cause the application to terminate.
        /// </summary>
        Fatal
    }

    /// <summary>
    /// Represents a log event with all associated metadata.
    /// </summary>
    public class LogEvent
    {
        /// <summary>
        /// Gets or sets the time when the log event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the log event.
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the message template with placeholders for properties.
        /// </summary>
        public string MessageTemplate { get; set; }

        /// <summary>
        /// Gets or sets the fully rendered message with property values inserted.
        /// </summary>
        public string RenderedMessage { get; set; }

        /// <summary>
        /// Gets or sets the exception associated with this log event, if any.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of named properties associated with this log event.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Outputs log events to the console with color-coding based on log level.
    /// </summary>
    public class ConsoleSink
    {
        private readonly LogLevel _restrictedToMinimumLevel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleSink"/> class.
        /// </summary>
        /// <param name="restrictedToMinimumLevel">The minimum log level this sink will process.</param>
        public ConsoleSink(LogLevel restrictedToMinimumLevel)
        {
            _restrictedToMinimumLevel = restrictedToMinimumLevel;
        }

        /// <summary>
        /// Writes a log event to the console with appropriate color formatting.
        /// </summary>
        /// <param name="logEvent">The log event to write to the console.</param>
        public void Emit(LogEvent logEvent)
        {
            if (logEvent.Level < _restrictedToMinimumLevel)
                return;

            var originalColor = Console.ForegroundColor;
            SetConsoleColor(logEvent.Level);

            Console.WriteLine($"[{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss}] [{logEvent.Level}] {logEvent.RenderedMessage}");

            if (logEvent.Exception != null)
                Console.WriteLine($"Exception: {logEvent.Exception}");

            Console.ForegroundColor = originalColor;
        }

        /// <summary>
        /// Sets the console foreground color based on the log event severity level.
        /// </summary>
        /// <param name="level">The log level that determines the color.</param>
        private void SetConsoleColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Verbose:
                case LogLevel.Debug:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogLevel.Information:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
            }
        }
    }

    /// <summary>
    /// Outputs log events to a file with support for file rotation and buffering.
    /// Implements IDisposable to ensure proper resource cleanup.
    /// </summary>
    public class FileSink : IDisposable
    {
        private readonly string _path;
        private readonly LogLevel _restrictedToMinimumLevel;
        private readonly RollingInterval _rollingInterval;
        private readonly long? _fileSizeLimitBytes;
        private readonly int? _retainedFileCountLimit;

        // Buffering properties
        private readonly int _bufferSize;
        private readonly TimeSpan _flushInterval;
        private readonly bool _enableBuffering;
        private readonly ConcurrentQueue<string> _messageQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _backgroundTask;
        private readonly AutoResetEvent _flushEvent;
        private readonly bool _backgroundThread;
        private bool _disposed;

        private string _currentFileName;
        private DateTime _nextCheckpoint;
        private long _currentFileSize;
        private readonly object _syncRoot = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSink"/> class with basic configuration.
        /// </summary>
        /// <param name="path">The base path for the log file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log level this sink will process.</param>
        /// <param name="rollingInterval">The interval at which to create new log files.</param>
        /// <param name="fileSizeLimitBytes">Maximum size of a log file before rolling to a new file, or null for no limit.</param>
        /// <param name="retainedFileCountLimit">Maximum number of log files to retain, or null for no limit.</param>
        public FileSink(
            string path,
            LogLevel restrictedToMinimumLevel,
            RollingInterval rollingInterval = RollingInterval.Infinite,
            long? fileSizeLimitBytes = null,
            int? retainedFileCountLimit = null)
        {
            // Forward to the constructor with buffering options
            // but disable buffering by default to maintain backward compatibility
            // for direct calls to this constructor
            _path = path;
            _restrictedToMinimumLevel = restrictedToMinimumLevel;
            _rollingInterval = rollingInterval;
            _fileSizeLimitBytes = fileSizeLimitBytes;
            _retainedFileCountLimit = retainedFileCountLimit;

            // No buffering in this constructor for backward compatibility
            _enableBuffering = false;

            _currentFileName = ComputeCurrentFileName();
            _nextCheckpoint = ComputeNextCheckpoint(DateTime.Now);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSink"/> class with advanced buffering options.
        /// </summary>
        /// <param name="path">The base path for the log file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log level this sink will process.</param>
        /// <param name="rollingInterval">The interval at which to create new log files.</param>
        /// <param name="fileSizeLimitBytes">Maximum size of a log file before rolling to a new file, or null for no limit.</param>
        /// <param name="retainedFileCountLimit">Maximum number of log files to retain, or null for no limit.</param>
        /// <param name="enableBuffering">Whether to enable write buffering for better performance.</param>
        /// <param name="bufferSize">The number of log events to buffer before writing to disk.</param>
        /// <param name="flushInterval">The maximum time to wait before flushing the buffer.</param>
        /// <param name="backgroundThread">Whether to use a background thread for buffer processing.</param>
        public FileSink(
            string path,
            LogLevel restrictedToMinimumLevel,
            RollingInterval rollingInterval = RollingInterval.Infinite,
            long? fileSizeLimitBytes = null,
            int? retainedFileCountLimit = null,
            bool enableBuffering = false,
            int bufferSize = 100,
            TimeSpan? flushInterval = null,
            bool backgroundThread = true)
        {
            _path = path;
            _restrictedToMinimumLevel = restrictedToMinimumLevel;
            _rollingInterval = rollingInterval;
            _fileSizeLimitBytes = fileSizeLimitBytes;
            _retainedFileCountLimit = retainedFileCountLimit;

            // Initialize buffering properties
            _enableBuffering = enableBuffering;
            _bufferSize = bufferSize > 0 ? bufferSize : 100;
            _flushInterval = flushInterval ?? TimeSpan.FromSeconds(10);
            _backgroundThread = backgroundThread && enableBuffering;

            _currentFileName = ComputeCurrentFileName();
            _nextCheckpoint = ComputeNextCheckpoint(DateTime.Now);

            // Initialize buffering components if enabled
            if (_enableBuffering)
            {
                _messageQueue = new ConcurrentQueue<string>();
                _flushEvent = new AutoResetEvent(false);

                if (_backgroundThread)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    _backgroundTask = Task.Factory.StartNew(
                        ProcessQueue,
                        _cancellationTokenSource.Token,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default);
                }
            }
        }

        /// <summary>
        /// Processes a log event and writes it to the configured file.
        /// </summary>
        /// <param name="logEvent">The log event to process.</param>
        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null)
                return;

            if (logEvent.Level < _restrictedToMinimumLevel || _disposed)
                return;

            try
            {
                string lineToWrite = FormatLogLine(logEvent);

                if (_enableBuffering)
                {
                    // Add to buffer queue
                    _messageQueue.Enqueue(lineToWrite);

                    // If the buffer is full, trigger a flush
                    if (_messageQueue.Count >= _bufferSize)
                    {
                        if (_backgroundThread)
                            _flushEvent.Set();
                        else
                            FlushBuffer();
                    }
                }
                else
                {
                    // Original non-buffered behavior
                    WriteToFile(lineToWrite, logEvent.Timestamp);
                }
            }
            catch (Exception ex)
            {
                // Enhanced error handling - try to write to a fallback location
                TryWriteToFallbackLocation(ex, logEvent);
            }
        }

        /// <summary>
        /// Writes a log line to the current log file, handling file rolling if necessary.
        /// </summary>
        /// <param name="lineToWrite">The formatted log line to write.</param>
        /// <param name="timestamp">The timestamp of the log event.</param>
        private void WriteToFile(string lineToWrite, DateTime timestamp)
        {
            if (string.IsNullOrEmpty(lineToWrite))
                return;

            try
            {
                lock (_syncRoot)
                {
                    // Check if we need to roll to a new file
                    if (ShouldRollFile(timestamp, lineToWrite.Length))
                    {
                        _currentFileName = ComputeCurrentFileName(timestamp);
                        _nextCheckpoint = ComputeNextCheckpoint(timestamp);
                        _currentFileSize = 0;

                        if (_retainedFileCountLimit.HasValue)
                            ApplyRetentionPolicy();
                    }

                    // Ensure directory exists before attempting to write
                    string directory = Path.GetDirectoryName(_currentFileName);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Add retry logic for transient IO issues
                    int retryCount = 0;
                    bool success = false;

                    while (!success && retryCount < 3)
                    {
                        try
                        {
                            File.AppendAllText(_currentFileName, lineToWrite);
                            _currentFileSize += lineToWrite.Length;
                            success = true;
                        }
                        catch (IOException)
                        {
                            retryCount++;
                            if (retryCount >= 3)
                                throw;

                            // Short delay before retry
                            Thread.Sleep(50 * retryCount);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If we still failed after retries, throw to be handled by caller
                throw new Exception($"Failed to write to log file {_currentFileName} after retries", ex);
            }
        }

        /// <summary>
        /// Attempts to write a log event to a fallback location if the primary location fails.
        /// </summary>
        /// <param name="originalException">The exception that occurred during the primary write attempt.</param>
        /// <param name="logEvent">The log event to write.</param>
        private void TryWriteToFallbackLocation(Exception originalException, LogEvent logEvent)
        {
            try
            {
                // Try to log the error to console first
                Console.WriteLine($"Failed to write to log file: {originalException.Message}");

                // Try to write to a fallback location (e.g., temp directory)
                string fallbackDir = Path.Combine(Path.GetTempPath(), "LogFallback");
                string fallbackFile = Path.Combine(fallbackDir, "fallback_log.txt");

                if (!Directory.Exists(fallbackDir))
                    Directory.CreateDirectory(fallbackDir);

                string errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR WRITING LOG: {originalException.Message}\r\n";
                string logMessage = FormatLogLine(logEvent);

                File.AppendAllText(fallbackFile, errorMessage + logMessage);
            }
            catch
            {
                // At this point we've done our best - silently fail
                // We don't want logging failures to crash the application
            }
        }

        /// <summary>
        /// Background task that processes the message queue at regular intervals.
        /// </summary>
        private void ProcessQueue()
        {
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    // Wait for a signal to flush or the flush interval to elapse
                    _flushEvent.WaitOne(_flushInterval);

                    if (!_disposed)
                        FlushBuffer();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in background processing: {ex.Message}");
            }
        }

        /// <summary>
        /// Flushes any buffered log messages to disk.
        /// </summary>
        public void FlushBuffer()
        {
            if (!_enableBuffering || _disposed || (_messageQueue != null && _messageQueue.IsEmpty))
                return;

            var linesToWrite = new List<string>();

            // Dequeue all messages
            while (_messageQueue.TryDequeue(out string line))
            {
                linesToWrite.Add(line);
            }

            if (linesToWrite.Count == 0)
                return;

            try
            {
                // Join all lines into a single string for efficient writing
                string allLines = string.Concat(linesToWrite);
                WriteToFile(allLines, DateTime.Now);
            }
            catch (Exception ex)
            {
                // Log the error but don't rethrow - logging should not break the application
                Console.WriteLine($"Failed to flush log buffer: {ex.Message}");

                // Try to recover any lost messages
                foreach (var line in linesToWrite)
                {
                    _messageQueue.Enqueue(line);
                }
            }
        }

        /// <summary>
        /// Formats a log event into a text line suitable for writing to a file.
        /// </summary>
        /// <param name="logEvent">The log event to format.</param>
        /// <returns>A formatted string representing the log event.</returns>
        private string FormatLogLine(LogEvent logEvent)
        {
            var logLine = $"[{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss}] [{logEvent.Level}] {logEvent.RenderedMessage}";
            if (logEvent.Exception != null)
                logLine += $"{Environment.NewLine}Exception: {logEvent.Exception}";

            return logLine + Environment.NewLine;
        }

        /// <summary>
        /// Determines whether a new log file should be created based on size or time.
        /// </summary>
        /// <param name="timestamp">The timestamp of the current log event.</param>
        /// <param name="nextWriteSize">The size in bytes of the next log write operation.</param>
        /// <returns>True if a new log file should be created, false otherwise.</returns>
        private bool ShouldRollFile(DateTime timestamp, int nextWriteSize)
        {
            // Roll if file size limit reached
            if (_fileSizeLimitBytes.HasValue && _currentFileSize + nextWriteSize > _fileSizeLimitBytes.Value)
                return true;

            // Roll if time period crossed
            if (_rollingInterval != RollingInterval.Infinite && timestamp >= _nextCheckpoint)
                return true;

            return false;
        }

        /// <summary>
        /// Applies the retention policy by deleting old log files that exceed the retention limit.
        /// </summary>
        private void ApplyRetentionPolicy()
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_path);
                var extension = Path.GetExtension(_path);

                var files = Directory.GetFiles(
                    string.IsNullOrEmpty(directory) ? "." : directory,
                    $"{fileNameWithoutExtension}*{extension}")
                    .OrderByDescending(f => f)
                    .Skip(_retainedFileCountLimit.Value)
                    .ToList();

                foreach (var obsoleteFile in files)
                {
                    try
                    {
                        File.Delete(obsoleteFile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete old log file {obsoleteFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying retention policy: {ex.Message}");
            }
        }

        /// <summary>
        /// Computes the current filename based on the rolling interval.
        /// </summary>
        /// <param name="date">The date to use for the filename, or null to use the current date.</param>
        /// <returns>The full path to the current log file.</returns>
        private string ComputeCurrentFileName(DateTime? date = null)
        {
            if (_rollingInterval == RollingInterval.Infinite)
                return _path;

            var dt = date ?? DateTime.Now;
            var directory = Path.GetDirectoryName(_path);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_path);
            var extension = Path.GetExtension(_path);

            string suffix = GetSuffix(dt);
            string fileName = $"{fileNameWithoutExtension}{suffix}{extension}";

            return string.IsNullOrEmpty(directory)
                ? fileName
                : Path.Combine(directory, fileName);
        }

        /// <summary>
        /// Gets the first day of the week containing the specified date.
        /// </summary>
        /// <param name="dt">The date to find the first day of the week for.</param>
        /// <returns>The first day (Monday) of the week.</returns>
        private DateTime GetFirstDayOfWeek(DateTime dt)
        {
            // Use ISO 8601 standard (Monday is first day of week)
            int diff = dt.DayOfWeek - DayOfWeek.Monday;
            if (diff < 0)
                diff += 7;
            return dt.AddDays(-diff).Date;
        }

        /// <summary>
        /// Gets the ISO 8601 week number for the specified date.
        /// </summary>
        /// <param name="date">The date to get the week number for.</param>
        /// <returns>The ISO 8601 week number (1-53).</returns>
        private int GetIso8601WeekOfYear(DateTime date)
        {
            // Get Thursday of the current week (ISO 8601 defines week by its Thursday)
            DateTime thursdayOfCurrentWeek = GetFirstDayOfWeek(date).AddDays(3);
            // Get the first Thursday of the year
            DateTime firstDayOfYear = new DateTime(thursdayOfCurrentWeek.Year, 1, 1);
            DateTime firstThursdayOfYear = firstDayOfYear;
            while (firstThursdayOfYear.DayOfWeek != DayOfWeek.Thursday)
                firstThursdayOfYear = firstThursdayOfYear.AddDays(1);
            // Calculate the week number
            int weekNumber = (int)Math.Floor((thursdayOfCurrentWeek - firstThursdayOfYear).TotalDays / 7) + 1;
            return weekNumber;
        }

        /// <summary>
        /// Gets the appropriate suffix for the log filename based on the rolling interval.
        /// </summary>
        /// <param name="dateTime">The date and time to use for the suffix.</param>
        /// <returns>A string suffix for the filename.</returns>
        private string GetSuffix(DateTime dateTime)
        {
            switch (_rollingInterval)
            {
                case RollingInterval.Minute:
                    return $"_{dateTime:yyyyMMddHHmm}";
                case RollingInterval.Hour:
                    return $"_{dateTime:yyyyMMddHH}";
                case RollingInterval.Day:
                    return $"_{dateTime:yyyyMMdd}";
                case RollingInterval.Week:
                    // ISO 8601 week format: year and week number
                    return $"_{dateTime:yyyy}W{GetIso8601WeekOfYear(dateTime):00}";
                case RollingInterval.Month:
                    return $"_{dateTime:yyyyMM}";
                case RollingInterval.Year:
                    return $"_{dateTime:yyyy}";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Computes the next checkpoint time when a log file should be rolled.
        /// </summary>
        /// <param name="dateTime">The current date and time.</param>
        /// <returns>The next checkpoint date and time.</returns>
        private DateTime ComputeNextCheckpoint(DateTime dateTime)
        {
            switch (_rollingInterval)
            {
                case RollingInterval.Minute:
                    return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                        dateTime.Hour, dateTime.Minute, 0).AddMinutes(1);
                case RollingInterval.Hour:
                    return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                        dateTime.Hour, 0, 0).AddHours(1);
                case RollingInterval.Day:
                    return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day).AddDays(1);
                case RollingInterval.Week:
                    // Calculate the start of next week (first day of current week + 7 days)
                    return GetFirstDayOfWeek(dateTime).AddDays(7);
                case RollingInterval.Month:
                    return new DateTime(dateTime.Year, dateTime.Month, 1).AddMonths(1);
                case RollingInterval.Year:
                    return new DateTime(dateTime.Year, 1, 1).AddYears(1);
                default:
                    return DateTime.MaxValue;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    // Flush any remaining logs
                    FlushBuffer();

                    if (_backgroundThread)
                    {
                        // Signal the background task to stop
                        _cancellationTokenSource?.Cancel();
                        _flushEvent?.Set();

                        try
                        {
                            // Wait for the background task to complete (with a timeout)
                            if (_backgroundTask != null && !_backgroundTask.Wait(TimeSpan.FromSeconds(5)))
                            {
                                // Log a warning if the task didn't complete in time
                                Console.WriteLine("Warning: Background logging task did not complete in the allotted time.");
                            }
                        }
                        catch (AggregateException ex)
                        {
                            // Log the specific task exceptions
                            Console.WriteLine($"Error waiting for background logging task: {ex.InnerException?.Message}");
                        }

                        // Dispose resources
                        _cancellationTokenSource?.Dispose();
                        _flushEvent?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    // Last resort error handling
                    Console.WriteLine($"Error during FileSink disposal: {ex.Message}");
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// Finalizer for the FileSink class.
        /// </summary>
        ~FileSink()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Provides a fluent interface for configuring the minimum log level.
    /// </summary>
    public class LevelConfiguration
    {
        private readonly LoggerConfiguration _parent;

        /// <summary>
        /// Initializes a new instance of the <see cref="LevelConfiguration"/> class.
        /// </summary>
        /// <param name="parent">The parent logger configuration.</param>
        internal LevelConfiguration(LoggerConfiguration parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Sets the minimum log level to Verbose.
        /// </summary>
        /// <returns>The parent logger configuration for method chaining.</returns>
        public LoggerConfiguration Verbose()
        {
            return _parent.SetMinimumLevel(LogLevel.Verbose);
        }

        /// <summary>
        /// Sets the minimum log level to Debug.
        /// </summary>
        /// <returns>The parent logger configuration for method chaining.</returns>
        public LoggerConfiguration Debug()
        {
            return _parent.SetMinimumLevel(LogLevel.Debug);
        }

        /// <summary>
        /// Sets the minimum log level to Information.
        /// </summary>
        /// <returns>The parent logger configuration for method chaining.</returns>
        public LoggerConfiguration Information()
        {
            return _parent.SetMinimumLevel(LogLevel.Information);
        }

        /// <summary>
        /// Sets the minimum log level to Warning.
        /// </summary>
        /// <returns>The parent logger configuration for method chaining.</returns>
        public LoggerConfiguration Warning()
        {
            return _parent.SetMinimumLevel(LogLevel.Warning);
        }

        /// <summary>
        /// Sets the minimum log level to Error.
        /// </summary>
        /// <returns>The parent logger configuration for method chaining.</returns>
        public LoggerConfiguration Error()
        {
            return _parent.SetMinimumLevel(LogLevel.Error);
        }

        /// <summary>
        /// Sets the minimum log level to Fatal.
        /// </summary>
        /// <returns>The parent logger configuration for method chaining.</returns>
        public LoggerConfiguration Fatal()
        {
            return _parent.SetMinimumLevel(LogLevel.Fatal);
        }
    }

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

    /// <summary>
    /// Represents a logger that can write log events to multiple sinks.
    /// </summary>
    public class Logger
    {
        private readonly LogLevel _minimumLevel;
        internal readonly List<object> _sinks; // Changed from private to internal
        private readonly Dictionary<string, object> _enrichers;
        private readonly object _syncRoot = new object(); // Add a synchronization object

        /// <summary>
        /// Gets a read-only list of the sinks configured for this logger.
        /// </summary>
        public IReadOnlyList<object> Sinks => _sinks.AsReadOnly();

        /// <summary>
        /// Initializes a new instance of the <see cref="Logger"/> class.
        /// </summary>
        /// <param name="minimumLevel">The minimum log level this logger will process.</param>
        /// <param name="sinks">The collection of sinks to write log events to.</param>
        /// <param name="enrichers">The collection of enrichers to add properties to log events.</param>
        internal Logger(LogLevel minimumLevel, List<object> sinks, Dictionary<string, object> enrichers)
        {
            _minimumLevel = minimumLevel;
            _sinks = new List<object>(sinks);
            _enrichers = new Dictionary<string, object>(enrichers);
        }

        /// <summary>
        /// Writes a verbose log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Verbose(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Verbose, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a verbose log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Verbose(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Verbose, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a debug log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Debug(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Debug, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a debug log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Debug(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Debug, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes an information log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Information(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Information, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes an information log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Information(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Information, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a warning log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Warning(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Warning, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a warning log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Warning(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Warning, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes an error log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Error(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Error, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes an error log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Error, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a fatal log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Fatal(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Fatal, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a fatal log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Fatal, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Core method that handles writing a log event to all configured sinks.
        /// </summary>
        /// <param name="level">The level of the log event.</param>
        /// <param name="exception">The exception associated with the log event, if any.</param>
        /// <param name="messageTemplate">The message template with optional property placeholders.</param>
        /// <param name="propertyValues">The values for the property placeholders.</param>
        private void Write(LogLevel level, Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (level < _minimumLevel)
                return;

            var properties = ExtractProperties(messageTemplate, propertyValues);

            // Add enrichers
            foreach (var enricher in _enrichers)
                properties[enricher.Key] = enricher.Value;

            var renderedMessage = RenderMessage(messageTemplate, propertyValues);

            var logEvent = new LogEvent
            {
                Timestamp = DateTime.Now,
                Level = level,
                MessageTemplate = messageTemplate,
                RenderedMessage = renderedMessage,
                Exception = exception,
                Properties = properties
            };

            foreach (var sink in _sinks)
            {
                try
                {
                    // Handle each sink type explicitly
                    if (sink is ConsoleSink consoleSink)
                    {
                        consoleSink.Emit(logEvent);
                    }
                    else if (sink is FileSink fileSink)
                    {
                        fileSink.Emit(logEvent);
                    }
                    // Add other sink types here if needed
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in log sink: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Renders a message template by replacing property placeholders with values.
        /// </summary>
        /// <param name="messageTemplate">The message template with optional property placeholders.</param>
        /// <param name="propertyValues">The values for the property placeholders.</param>
        /// <returns>The rendered message with property values inserted.</returns>
        private string RenderMessage(string messageTemplate, object[] propertyValues)
        {
            if (propertyValues == null || propertyValues.Length == 0)
                return messageTemplate;

            try
            {
                // Improved regex to handle format specifiers like {PropertyName:00.00}
                var regex = new System.Text.RegularExpressions.Regex(@"\{([^:{}]+)(?:\:([^{}]+))?\}");
                var result = regex.Replace(messageTemplate, match =>
                {
                    string propertyName = match.Groups[1].Value;
                    string formatSpecifier = match.Groups[2].Success ? match.Groups[2].Value : null;

                    // Find the property value
                    int propIndex = -1;
                    for (int i = 0; i < Math.Min(propertyValues.Length, regex.Matches(messageTemplate).Count); i++)
                    {
                        if (regex.Matches(messageTemplate)[i].Groups[1].Value == propertyName)
                        {
                            propIndex = i;
                            break;
                        }
                    }

                    // If we couldn't find the property by name, try to use it by position
                    if (propIndex == -1)
                    {
                        var allMatches = regex.Matches(messageTemplate);
                        for (int i = 0; i < allMatches.Count; i++)
                        {
                            if (allMatches[i].Value == match.Value && i < propertyValues.Length)
                            {
                                propIndex = i;
                                break;
                            }
                        }
                    }

                    if (propIndex >= 0 && propIndex < propertyValues.Length)
                    {
                        object value = propertyValues[propIndex];

                        // Handle null values
                        if (value == null)
                            return "(null)";

                        // Apply format specifier if provided
                        if (!string.IsNullOrEmpty(formatSpecifier) && value is IFormattable formattable)
                        {
                            return formattable.ToString(formatSpecifier, System.Globalization.CultureInfo.CurrentCulture);
                        }

                        return value.ToString();
                    }

                    // Return the original placeholder if we couldn't resolve it
                    return match.Value;
                });

                return result;
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - log formatting shouldn't break the application
                Console.WriteLine($"Error formatting log message: {ex.Message}");
                return messageTemplate;
            }
        }

        /// <summary>
        /// Extracts named properties from a message template and corresponding values.
        /// </summary>
        /// <param name="messageTemplate">The message template with optional property placeholders.</param>
        /// <param name="propertyValues">The values for the property placeholders.</param>
        /// <returns>A dictionary mapping property names to their values.</returns>
        private Dictionary<string, object> ExtractProperties(string messageTemplate, object[] propertyValues)
        {
            var properties = new Dictionary<string, object>();

            if (propertyValues == null || propertyValues.Length == 0)
                return properties;

            // Extract property names from the template with improved regex
            var propertyNames = new List<string>();
            var regex = new System.Text.RegularExpressions.Regex(@"\{([^:{}]+)(?:\:([^{}]+))?\}");
            var matches = regex.Matches(messageTemplate);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Extract the property name (without format specifiers)
                var propertyName = match.Groups[1].Value;
                propertyNames.Add(propertyName);
            }

            // Add properties with names when available, fallback to position-based when not
            for (var i = 0; i < propertyValues.Length; i++)
            {
                string key = i < propertyNames.Count ? propertyNames[i] : $"Prop{i}";
                properties[key] = propertyValues[i];
            }

            return properties;
        }
    }

    /// <summary>
    /// Provides a fluent interface for configuring a logger.
    /// </summary>
    public class LoggerConfiguration
    {
        /// <summary>
        /// Gets the minimum log level value.
        /// </summary>
        internal LogLevel MinimumLevelValue { get; private set; } = LogLevel.Information;

        /// <summary>
        /// Gets the list of configured sinks.
        /// </summary>
        internal List<object> SinksList { get; } = new List<object>();

        /// <summary>
        /// Gets the dictionary of configured enrichers.
        /// </summary>
        internal Dictionary<string, object> Enrichers { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Sets the minimum log level that will be processed by the logger.
        /// </summary>
        /// <param name="minimumLevel">The minimum log level to set.</param>
        /// <returns>The same logger configuration for method chaining.</returns>
        public LoggerConfiguration SetMinimumLevel(LogLevel minimumLevel)
        {
            MinimumLevelValue = minimumLevel;
            return this;
        }

        /// <summary>
        /// Adds a console sink to the logger configuration.
        /// </summary>
        /// <param name="sink">The console sink to add.</param>
        /// <returns>The same logger configuration for method chaining.</returns>
        public LoggerConfiguration AddConsoleSink(ConsoleSink sink)
        {
            SinksList.Add(sink);
            return this;
        }

        /// <summary>
        /// Adds a file sink to the logger configuration.
        /// </summary>
        /// <param name="sink">The file sink to add.</param>
        /// <returns>The same logger configuration for method chaining.</returns>
        public LoggerConfiguration AddFileSink(FileSink sink)
        {
            SinksList.Add(sink);
            return this;
        }

        /// <summary>
        /// Adds an enricher property to all log events.
        /// </summary>
        /// <param name="propertyName">The name of the property to add.</param>
        /// <param name="value">The value of the property to add.</param>
        /// <returns>The same logger configuration for method chaining.</returns>
        public LoggerConfiguration Enrich(string propertyName, object value)
        {
            Enrichers[propertyName] = value;
            return this;
        }

        /// <summary>
        /// Creates a logger from the current configuration.
        /// </summary>
        /// <returns>A new logger instance with the configured settings.</returns>
        public Logger CreateLogger()
        {
            return new Logger(
                MinimumLevelValue,
                SinksList,
                Enrichers);
        }

        private SinkConfiguration _writeTo;
        /// <summary>
        /// Gets the sink configuration for fluent configuration of log sinks.
        /// </summary>
        public SinkConfiguration WriteTo => _writeTo ?? (_writeTo = new SinkConfiguration(this));

        private LevelConfiguration _minimumLevel;
        /// <summary>
        /// Gets the level configuration for fluent configuration of minimum log level.
        /// </summary>
        public LevelConfiguration MinimumLevel => _minimumLevel ?? (_minimumLevel = new LevelConfiguration(this));
    }

    /// <summary>
    /// Provides a fluent interface for configuring sinks in a logger configuration.
    /// </summary>
    public class SinkConfiguration
    {
        private readonly LoggerConfiguration _parent;

        /// <summary>
        /// Initializes a new instance of the <see cref="SinkConfiguration"/> class.
        /// </summary>
        /// <param name="parent">The parent logger configuration.</param>
        internal SinkConfiguration(LoggerConfiguration parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Adds a console sink to the logger configuration.
        /// </summary>
        /// <param name="restrictedToMinimumLevel">The minimum log level this sink will process (default: Verbose).</param>
        /// <returns>The parent logger configuration for method chaining.</returns>
        public LoggerConfiguration Console(LogLevel restrictedToMinimumLevel = LogLevel.Verbose)
        {
            return _parent.AddConsoleSink(new ConsoleSink(restrictedToMinimumLevel));
        }

        /// <summary>
        /// Adds a file sink to the logger configuration.
        /// </summary>
        /// <param name="path">The path where the log file will be written.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log level this sink will process (default: Verbose).</param>
        /// <param name="rollingInterval">The interval at which to create new log files (default: Infinite).</param>
        /// <param name="fileSizeLimitBytes">Maximum size of a log file before rolling to a new file, or null for no limit.</param>
        /// <param name="retainedFileCountLimit">Maximum number of log files to retain, or null for no limit.</param>
        /// <param name="enableBuffering">Whether to enable write buffering for better performance.</param>
        /// <param name="bufferSize">The number of log events to buffer before writing to disk.</param>
        /// <param name="flushInterval">The maximum time to wait before flushing the buffer.</param>
        /// <param name="backgroundThread">Whether to use a background thread for buffer processing.</param>
        /// <returns>The parent logger configuration for method chaining.</returns>
        public LoggerConfiguration File(
            string path,
            LogLevel restrictedToMinimumLevel = LogLevel.Verbose,
            RollingInterval rollingInterval = RollingInterval.Infinite,
            long? fileSizeLimitBytes = null,
            int? retainedFileCountLimit = null,
            bool enableBuffering = true,
            int bufferSize = 100,
            TimeSpan? flushInterval = null,
            bool backgroundThread = true)
        {
            var sink = new FileSink(
                path,
                restrictedToMinimumLevel,
                rollingInterval,
                fileSizeLimitBytes,
                retainedFileCountLimit,
                enableBuffering,
                bufferSize,
                flushInterval,
                backgroundThread);

            return _parent.AddFileSink(sink);
        }
    }
}