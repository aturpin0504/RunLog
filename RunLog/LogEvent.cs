using System;
using System.Collections.Generic;

namespace RunLog
{
    /// <summary>
    /// Represents a log event with all associated metadata.
    /// </summary>
    public class LogEvent
    {
        /// <summary>
        /// Gets or sets the time when the log event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the log event.
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the message template with placeholders for properties.
        /// </summary>
        public string MessageTemplate { get; set; }

        /// <summary>
        /// Gets or sets the fully rendered message with property values inserted.
        /// </summary>
        public string RenderedMessage { get; set; }

        /// <summary>
        /// Gets or sets the exception associated with this log event, if any.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of named properties associated with this log event.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
}
