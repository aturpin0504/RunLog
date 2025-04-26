using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RunLog
{
    public enum LogLevel
    {
        Verbose,
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    }

    public class LogEvent
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string MessageTemplate { get; set; }
        public string RenderedMessage { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public interface ILogEventSink
    {
        void Emit(LogEvent logEvent);
    }

    public class LoggerConfiguration
    {
        internal LogLevel MinimumLevelValue { get; private set; } = LogLevel.Information;
        internal List<ILogEventSink> SinksList { get; } = new List<ILogEventSink>();
        internal Dictionary<string, object> Enrichers { get; } = new Dictionary<string, object>();

        public LoggerConfiguration SetMinimumLevel(LogLevel minimumLevel)
        {
            MinimumLevelValue = minimumLevel;
            return this;
        }

        public LoggerConfiguration AddSink(ILogEventSink sink)
        {
            SinksList.Add(sink);
            return this;
        }

        public LoggerConfiguration Enrich(string propertyName, object value)
        {
            Enrichers[propertyName] = value;
            return this;
        }

        public Logger CreateLogger()
        {
            return new Logger(
                MinimumLevelValue,
                SinksList,
                Enrichers);
        }

        public class SinkConfiguration
        {
            private readonly LoggerConfiguration _parent;

            internal SinkConfiguration(LoggerConfiguration parent)
            {
                _parent = parent;
            }

            public LoggerConfiguration Console(LogLevel restrictedToMinimumLevel = LogLevel.Verbose)
            {
                return _parent.AddSink(new ConsoleSink(restrictedToMinimumLevel));
            }

            // First File overload (simplest version)
            public LoggerConfiguration File(string path, LogLevel restrictedToMinimumLevel = LogLevel.Verbose)
            {
                // Now forwards to the full version with default buffering enabled
                return File(
                    path,
                    restrictedToMinimumLevel,
                    RollingInterval.Infinite,
                    null,
                    null,
                    enableBuffering: true,
                    bufferSize: 100,
                    flushInterval: TimeSpan.FromSeconds(10),
                    backgroundThread: true);
            }

            // Second File overload (with rolling options)
            public LoggerConfiguration File(
                string path,
                LogLevel restrictedToMinimumLevel,
                RollingInterval rollingInterval,
                long? fileSizeLimitBytes = null,
                int? retainedFileCountLimit = null)
            {
                // Now forwards to the full version with default buffering enabled
                return File(
                    path,
                    restrictedToMinimumLevel,
                    rollingInterval,
                    fileSizeLimitBytes,
                    retainedFileCountLimit,
                    enableBuffering: true,
                    bufferSize: 100,
                    flushInterval: TimeSpan.FromSeconds(10),
                    backgroundThread: true);
            }

            // New comprehensive File method that includes buffering options
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

                return _parent.AddSink(sink);
            }

            // Mark the BufferedFile methods as Obsolete
            [Obsolete("Use File method with enableBuffering parameter instead")]
            public LoggerConfiguration BufferedFile(
                string path,
                LogLevel restrictedToMinimumLevel = LogLevel.Verbose)
            {
                return File(
                    path,
                    restrictedToMinimumLevel,
                    RollingInterval.Infinite,
                    null,
                    null,
                    enableBuffering: true,
                    bufferSize: 100,
                    flushInterval: TimeSpan.FromSeconds(10),
                    backgroundThread: true);
            }

            // Mark other BufferedFile overloads as Obsolete too
            [Obsolete("Use File method with enableBuffering parameter instead")]
            public LoggerConfiguration BufferedFile(
                string path,
                LogLevel restrictedToMinimumLevel = LogLevel.Verbose,
                int bufferSize = 100,
                TimeSpan? flushInterval = null,
                bool backgroundThread = true)
            {
                return File(
                    path,
                    restrictedToMinimumLevel,
                    RollingInterval.Infinite,
                    null,
                    null,
                    enableBuffering: true,
                    bufferSize: bufferSize,
                    flushInterval: flushInterval,
                    backgroundThread: backgroundThread);
            }

            [Obsolete("Use File method with enableBuffering parameter instead")]
            public LoggerConfiguration BufferedFile(
                string path,
                LogLevel restrictedToMinimumLevel = LogLevel.Verbose,
                RollingInterval rollingInterval = RollingInterval.Infinite,
                long? fileSizeLimitBytes = null,
                int? retainedFileCountLimit = null,
                int bufferSize = 100,
                TimeSpan? flushInterval = null,
                bool backgroundThread = true)
            {
                return File(
                    path,
                    restrictedToMinimumLevel,
                    rollingInterval,
                    fileSizeLimitBytes,
                    retainedFileCountLimit,
                    enableBuffering: true,
                    bufferSize: bufferSize,
                    flushInterval: flushInterval,
                    backgroundThread: backgroundThread);
            }
        }

        private SinkConfiguration _writeTo;
        public SinkConfiguration WriteTo => _writeTo ?? (_writeTo = new SinkConfiguration(this));

        public class LevelConfiguration
        {
            private readonly LoggerConfiguration _parent;

            internal LevelConfiguration(LoggerConfiguration parent)
            {
                _parent = parent;
            }

            public LoggerConfiguration Verbose()
            {
                return _parent.SetMinimumLevel(LogLevel.Verbose);
            }

            public LoggerConfiguration Debug()
            {
                return _parent.SetMinimumLevel(LogLevel.Debug);
            }

            public LoggerConfiguration Information()
            {
                return _parent.SetMinimumLevel(LogLevel.Information);
            }

            public LoggerConfiguration Warning()
            {
                return _parent.SetMinimumLevel(LogLevel.Warning);
            }

            public LoggerConfiguration Error()
            {
                return _parent.SetMinimumLevel(LogLevel.Error);
            }

            public LoggerConfiguration Fatal()
            {
                return _parent.SetMinimumLevel(LogLevel.Fatal);
            }
        }

        private LevelConfiguration _minimumLevel;
        public LevelConfiguration MinimumLevel => _minimumLevel ?? (_minimumLevel = new LevelConfiguration(this));
    }

    public class ConsoleSink : ILogEventSink
    {
        private readonly LogLevel _restrictedToMinimumLevel;

        public ConsoleSink(LogLevel restrictedToMinimumLevel)
        {
            _restrictedToMinimumLevel = restrictedToMinimumLevel;
        }

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

    public class FileSink : ILogEventSink, IDisposable
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
                    EnsureDirectoryExists(_currentFileName);

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

        // Helper method to get the first day of the week containing the specified date
        private DateTime GetFirstDayOfWeek(DateTime dt)
        {
            // Use ISO 8601 standard (Monday is first day of week)
            int diff = dt.DayOfWeek - DayOfWeek.Monday;
            if (diff < 0)
                diff += 7;

            return dt.AddDays(-diff).Date;
        }

        // Helper method to calculate ISO 8601 week of year
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

        private void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
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

    public enum RollingInterval
    {
        Infinite,
        Year,
        Month,
        Week,
        Day,
        Hour,
        Minute
    }

    public class Logger
    {
        private readonly LogLevel _minimumLevel;
        private readonly List<ILogEventSink> _sinks;
        private readonly Dictionary<string, object> _enrichers;
        private readonly object _syncRoot = new object(); // Add a synchronization object

        internal Logger(LogLevel minimumLevel, List<ILogEventSink> sinks, Dictionary<string, object> enrichers)
        {
            _minimumLevel = minimumLevel;
            _sinks = new List<ILogEventSink>(sinks);
            _enrichers = new Dictionary<string, object>(enrichers);
        }

        // Make all methods thread-safe by adding a lock
        public void Verbose(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Verbose, null, messageTemplate, propertyValues);
            }
        }

        public void Debug(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Debug, null, messageTemplate, propertyValues);
            }
        }

        public void Information(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Information, null, messageTemplate, propertyValues);
            }
        }

        public void Warning(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Warning, null, messageTemplate, propertyValues);
            }
        }

        public void Error(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Error, null, messageTemplate, propertyValues);
            }
        }

        public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Error, exception, messageTemplate, propertyValues);
            }
        }

        public void Fatal(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Fatal, null, messageTemplate, propertyValues);
            }
        }

        public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Fatal, exception, messageTemplate, propertyValues);
            }
        }

        // The Write method doesn't need locking as it's already called from within a lock
        private void Write(LogLevel level, Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (level < _minimumLevel)
                return;

            // Rest of the method remains the same
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
                    sink.Emit(logEvent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in log sink: {ex.Message}");
                }
            }
        }

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

        // Update the ExtractProperties method to correctly handle format specifiers
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
                // Since we can't access Logger._sinks directly, we'll work with our tracked disposables
                foreach (var disposable in _disposableSinks)
                {
                    try
                    {
                        // Flush any buffered file sinks
                        if (disposable is FileSink fileSink)
                        {
                            fileSink.FlushBuffer();
                        }

                        // Then dispose the sink
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error flushing/disposing sink: {ex.Message}");
                    }
                }

                // Clear the list
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