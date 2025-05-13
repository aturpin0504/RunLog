namespace RunLog
{
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
}
