using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RunLog
{
    public class ConsoleSink
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
}
