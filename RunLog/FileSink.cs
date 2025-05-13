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
}
