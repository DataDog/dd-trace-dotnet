using System;

using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal class DatadogSerilogLogger : IDatadogLogger
    {
        private static readonly object[] NoPropertyValues = new object[0];
        private readonly ILogger _logger;

        public DatadogSerilogLogger(ILogger logger)
        {
            _logger = logger;
        }

        public bool IsEnabled(LogEventLevel level) => _logger.IsEnabled(level);

        public void Debug(string messageTemplate)
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, NoPropertyValues);

        public void Debug<T>(string messageTemplate, T property)
            => Debug(exception: null, messageTemplate, property);

        public void Debug<T>(Exception exception, string messageTemplate, T property)
        {
            if (_logger.IsEnabled(LogEventLevel.Debug))
            {
                // Avoid boxing + array allocation if disabled
                Write(LogEventLevel.Debug, exception: null, messageTemplate, new object[] { property });
            }
        }

        public void Debug(string messageTemplate, params object[] args)
        {
            Write(LogEventLevel.Debug, exception: null, messageTemplate, args);
        }

        public void Information(string messageTemplate, params object[] args)
            => Write(LogEventLevel.Information, exception: null, messageTemplate, args);

        public void Information(Exception exception, string messageTemplate, params object[] args)
            => Write(LogEventLevel.Information, exception: exception, messageTemplate, args);

        public void Warning(string messageTemplate, params object[] args)
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, args);

        public void Warning(Exception ex, string messageTemplate, params object[] args)
            => Write(LogEventLevel.Warning, exception: ex, messageTemplate, args);

        public void Error(string messageTemplate, params object[] args)
            => Write(LogEventLevel.Error, exception: null, messageTemplate, args);

        public void Error(Exception ex, string messageTemplate, params object[] args)
            => Write(LogEventLevel.Error, exception: ex, messageTemplate, args);

        private void Write(LogEventLevel level, Exception exception, string messageTemplate, object[] args)
        {
            _logger.Write(level, exception, messageTemplate, args);
        }
    }
}
