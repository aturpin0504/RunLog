using System;
using System.Collections.Generic;

namespace RunLog
{
    public class LogEvent
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string MessageTemplate { get; set; }
        public string RenderedMessage { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
}
