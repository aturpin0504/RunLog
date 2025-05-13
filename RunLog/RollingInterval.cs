namespace RunLog
{
    /// <summary>
    /// Defines the frequency at which log files should be rotated.
    /// </summary>
    public enum RollingInterval
    {
        /// <summary>
        /// No rotation - use a single log file indefinitely.
        /// </summary>
        Infinite,

        /// <summary>
        /// Create a new log file each year.
        /// </summary>
        Year,

        /// <summary>
        /// Create a new log file each month.
        /// </summary>
        Month,

        /// <summary>
        /// Create a new log file each week.
        /// </summary>
        Week,

        /// <summary>
        /// Create a new log file each day.
        /// </summary>
        Day,

        /// <summary>
        /// Create a new log file each hour.
        /// </summary>
        Hour,

        /// <summary>
        /// Create a new log file each minute.
        /// </summary>
        Minute
    }
}
