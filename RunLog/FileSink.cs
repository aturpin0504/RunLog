using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RunLog
{
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

        private string FormatLogLine(LogEvent logEvent)
        {
            var logLine = $"[{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss}] [{logEvent.Level}] {logEvent.RenderedMessage}";
            if (logEvent.Exception != null)
                logLine += $"{Environment.NewLine}Exception: {logEvent.Exception}";

            return logLine + Environment.NewLine;
        }

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

        private DateTime GetFirstDayOfWeek(DateTime dt)
        {
            // Use ISO 8601 standard (Monday is first day of week)
            int diff = dt.DayOfWeek - DayOfWeek.Monday;
            if (diff < 0)
                diff += 7;
            return dt.AddDays(-diff).Date;
        }

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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

        ~FileSink()
        {
            Dispose(false);
        }
    }
}