using System;

namespace RunLog
{
    public class SinkConfiguration
    {
        private readonly LoggerConfiguration _parent;

        internal SinkConfiguration(LoggerConfiguration parent)
        {
            _parent = parent;
        }

        public LoggerConfiguration Console(LogLevel restrictedToMinimumLevel = LogLevel.Verbose)
        {
            return _parent.AddConsoleSink(new ConsoleSink(restrictedToMinimumLevel));
        }

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
