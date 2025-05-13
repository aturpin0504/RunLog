using System.Collections.Generic;

namespace RunLog
{
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
}
