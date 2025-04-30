using System;
using System.Collections.Generic;

namespace RunLog
{
    public class Logger
    {
        private readonly LogLevel _minimumLevel;
        internal readonly List<object> _sinks; // Changed from private to internal
        private readonly Dictionary<string, object> _enrichers;
        private readonly object _syncRoot = new object(); // Add a synchronization object

        // Add a public property to access the sinks
        public IReadOnlyList<object> Sinks => _sinks.AsReadOnly();

        internal Logger(LogLevel minimumLevel, List<object> sinks, Dictionary<string, object> enrichers)
        {
            _minimumLevel = minimumLevel;
            _sinks = new List<object>(sinks);
            _enrichers = new Dictionary<string, object>(enrichers);
        }

        // Make all methods thread-safe by adding a lock
        public void Verbose(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Verbose, null, messageTemplate, propertyValues);
            }
        }

        public void Debug(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Debug, null, messageTemplate, propertyValues);
            }
        }

        public void Information(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Information, null, messageTemplate, propertyValues);
            }
        }

        public void Warning(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Warning, null, messageTemplate, propertyValues);
            }
        }

        public void Warning(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Warning, exception, messageTemplate, propertyValues);
            }
        }

        public void Error(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Error, null, messageTemplate, propertyValues);
            }
        }

        public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Error, exception, messageTemplate, propertyValues);
            }
        }

        public void Fatal(string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Fatal, null, messageTemplate, propertyValues);
            }
        }

        public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            lock (_syncRoot)
            {
                Write(LogLevel.Fatal, exception, messageTemplate, propertyValues);
            }
        }

        // Modified to handle concrete sink types
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

        // Update the ExtractProperties method to correctly handle format specifiers
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