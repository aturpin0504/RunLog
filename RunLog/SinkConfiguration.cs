using System;

namespace RunLog
{
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
