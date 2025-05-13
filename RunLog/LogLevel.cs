namespace RunLog
{
    /// <summary>
    /// Defines the severity level of log messages.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Verbose - most detailed level, typically used for debugging.
        /// </summary>
        Verbose,

        /// <summary>
        /// Debug - detailed information useful for debugging.
        /// </summary>
        Debug,

        /// <summary>
        /// Information - general information about application flow.
        /// </summary>
        Information,

        /// <summary>
        /// Warning - potential issues that don't prevent the application from functioning.
        /// </summary>
        Warning,

        /// <summary>
        /// Error - issues that prevent specific operations from functioning correctly.
        /// </summary>
        Error,

        /// <summary>
        /// Fatal - severe errors that cause the application to terminate.
        /// </summary>
        Fatal
    }
}
