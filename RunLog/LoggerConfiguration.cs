using System.Collections.Generic;

namespace RunLog
{
    public class LoggerConfiguration
    {
        internal LogLevel MinimumLevelValue { get; private set; } = LogLevel.Information;
        internal List<object> SinksList { get; } = new List<object>();
        internal Dictionary<string, object> Enrichers { get; } = new Dictionary<string, object>();

        public LoggerConfiguration SetMinimumLevel(LogLevel minimumLevel)
        {
            MinimumLevelValue = minimumLevel;
            return this;
        }

        public LoggerConfiguration AddConsoleSink(ConsoleSink sink)
        {
            SinksList.Add(sink);
            return this;
        }

        public LoggerConfiguration AddFileSink(FileSink sink)
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

        private SinkConfiguration _writeTo;
        public SinkConfiguration WriteTo => _writeTo ?? (_writeTo = new SinkConfiguration(this));

        private LevelConfiguration _minimumLevel;
        public LevelConfiguration MinimumLevel => _minimumLevel ?? (_minimumLevel = new LevelConfiguration(this));
    }
}
