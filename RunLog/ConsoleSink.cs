using System;

namespace RunLog
{
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
}
