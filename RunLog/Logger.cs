using System;
using System.Collections.Generic;

namespace RunLog
{
    /// <summary>
    /// Represents a logger that can write log events to multiple sinks.
    /// </summary>
    public class Logger
    {
        private readonly LogLevel _minimumLevel;
        internal readonly List<object> _sinks; // Changed from private to internal
        private readonly Dictionary<string, object> _enrichers;
        private readonly object _syncRoot = new object(); // Add a synchronization object

        /// <summary>
        /// Gets a read-only list of the sinks configured for this logger.
        /// </summary>
        public IReadOnlyList<object> Sinks => _sinks.AsReadOnly();

        /// <summary>
        /// Initializes a new instance of the <see cref="Logger"/> class.
        /// </summary>
        /// <param name="minimumLevel">The minimum log level this logger will process.</param>
        /// <param name="sinks">The collection of sinks to write log events to.</param>
        /// <param name="enrichers">The collection of enrichers to add properties to log events.</param>
        internal Logger(LogLevel minimumLevel, List<object> sinks, Dictionary<string, object> enrichers)
        {
            _minimumLevel = minimumLevel;
            _sinks = new List<object>(sinks);
            _enrichers = new Dictionary<string, object>(enrichers);
        }

        /// <summary>
        /// Writes a verbose log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Verbose(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Verbose, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a verbose log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Verbose(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Verbose, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a debug log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Debug(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Debug, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a debug log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Debug(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Debug, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes an information log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Information(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Information, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes an information log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Information(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Information, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a warning log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Warning(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Warning, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a warning log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Warning(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Warning, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes an error log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Error(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Error, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes an error log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Error, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a fatal log message.
        /// </summary>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Fatal(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Fatal, null, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Writes a fatal log message with an associated exception.
        /// </summary>
        /// <param name="exception">The exception to include in the log.</param>
        /// <param name="messageTemplate">Message template with optional property placeholders.</param>
        /// <param name="propertyValues">Values for the property placeholders.</param>
        public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Fatal, exception, messageTemplate, propertyValues);
            }
        }

        /// <summary>
        /// Core method that handles writing a log event to all configured sinks.
        /// </summary>
        /// <param name="level">The level of the log event.</param>
        /// <param name="exception">The exception associated with the log event, if any.</param>
        /// <param name="messageTemplate">The message template with optional property placeholders.</param>
        /// <param name="propertyValues">The values for the property placeholders.</param>
        private void Write(LogLevel level, Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (level < _minimumLevel)
                return;

            var properties = ExtractProperties(messageTemplate, propertyValues);

            // Add enrichers
            foreach (var enricher in _enrichers)
                properties[enricher.Key] = enricher.Value;

            var renderedMessage = RenderMessage(messageTemplate, propertyValues);

            var logEvent = new LogEvent
            {
                Timestamp = DateTime.Now,
                Level = level,
                MessageTemplate = messageTemplate,
                RenderedMessage = renderedMessage,
                Exception = exception,
                Properties = properties
            };

            foreach (var sink in _sinks)
            {
                try
                {
                    // Handle each sink type explicitly
                    if (sink is ConsoleSink consoleSink)
                    {
                        consoleSink.Emit(logEvent);
                    }
                    else if (sink is FileSink fileSink)
                    {
                        fileSink.Emit(logEvent);
                    }
                    // Add other sink types here if needed
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in log sink: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Renders a message template by replacing property placeholders with values.
        /// </summary>
        /// <param name="messageTemplate">The message template with optional property placeholders.</param>
        /// <param name="propertyValues">The values for the property placeholders.</param>
        /// <returns>The rendered message with property values inserted.</returns>
        private string RenderMessage(string messageTemplate, object[] propertyValues)
        {
            if (propertyValues == null || propertyValues.Length == 0)
                return messageTemplate;

            try
            {
                // Improved regex to handle format specifiers like {PropertyName:00.00}
                var regex = new System.Text.RegularExpressions.Regex(@"\{([^:{}]+)(?:\:([^{}]+))?\}");
                var result = regex.Replace(messageTemplate, match =>
                {
                    string propertyName = match.Groups[1].Value;
                    string formatSpecifier = match.Groups[2].Success ? match.Groups[2].Value : null;

                    // Find the property value
                    int propIndex = -1;
                    for (int i = 0; i < Math.Min(propertyValues.Length, regex.Matches(messageTemplate).Count); i++)
                    {
                        if (regex.Matches(messageTemplate)[i].Groups[1].Value == propertyName)
                        {
                            propIndex = i;
                            break;
                        }
                    }

                    // If we couldn't find the property by name, try to use it by position
                    if (propIndex == -1)
                    {
                        var allMatches = regex.Matches(messageTemplate);
                        for (int i = 0; i < allMatches.Count; i++)
                        {
                            if (allMatches[i].Value == match.Value && i < propertyValues.Length)
                            {
                                propIndex = i;
                                break;
                            }
                        }
                    }

                    if (propIndex >= 0 && propIndex < propertyValues.Length)
                    {
                        object value = propertyValues[propIndex];

                        // Handle null values
                        if (value == null)
                            return "(null)";

                        // Apply format specifier if provided
                        if (!string.IsNullOrEmpty(formatSpecifier) && value is IFormattable formattable)
                        {
                            return formattable.ToString(formatSpecifier, System.Globalization.CultureInfo.CurrentCulture);
                        }

                        return value.ToString();
                    }

                    // Return the original placeholder if we couldn't resolve it
                    return match.Value;
                });

                return result;
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - log formatting shouldn't break the application
                Console.WriteLine($"Error formatting log message: {ex.Message}");
                return messageTemplate;
            }
        }

        /// <summary>
        /// Extracts named properties from a message template and corresponding values.
        /// </summary>
        /// <param name="messageTemplate">The message template with optional property placeholders.</param>
        /// <param name="propertyValues">The values for the property placeholders.</param>
        /// <returns>A dictionary mapping property names to their values.</returns>
        private Dictionary<string, object> ExtractProperties(string messageTemplate, object[] propertyValues)
        {
            var properties = new Dictionary<string, object>();

            if (propertyValues == null || propertyValues.Length == 0)
                return properties;

            // Extract property names from the template with improved regex
            var propertyNames = new List<string>();
            var regex = new System.Text.RegularExpressions.Regex(@"\{([^:{}]+)(?:\:([^{}]+))?\}");
            var matches = regex.Matches(messageTemplate);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Extract the property name (without format specifiers)
                var propertyName = match.Groups[1].Value;
                propertyNames.Add(propertyName);
            }

            // Add properties with names when available, fallback to position-based when not
            for (var i = 0; i < propertyValues.Length; i++)
            {
                string key = i < propertyNames.Count ? propertyNames[i] : $"Prop{i}";
                properties[key] = propertyValues[i];
            }

            return properties;
        }
    }
}
