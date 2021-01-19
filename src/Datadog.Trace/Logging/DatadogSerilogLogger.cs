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
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property);

        public void Debug<T0, T1>(string messageTemplate, T0 property0, T1 property1)
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1);

        public void Debug<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2)
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, property2);

        public void Debug(string messageTemplate, object[] args)
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, args);

        public void Debug(Exception exception, string messageTemplate)
            => Write(LogEventLevel.Debug, exception, messageTemplate, NoPropertyValues);

        public void Debug<T>(Exception exception, string messageTemplate, T property)
            => Write(LogEventLevel.Debug, exception, messageTemplate, property);

        public void Debug<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1)
            => Write(LogEventLevel.Debug, exception, messageTemplate, property0, property1);

        public void Debug<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
            => Write(LogEventLevel.Debug, exception, messageTemplate, property0, property1, property2);

        public void Debug(Exception exception, string messageTemplate, object[] args)
            => Write(LogEventLevel.Debug, exception, messageTemplate, args);

        public void Information(string messageTemplate)
            => Write(LogEventLevel.Information, exception: null, messageTemplate, NoPropertyValues);

        public void Information<T>(string messageTemplate, T property)
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property);

        public void Information<T0, T1>(string messageTemplate, T0 property0, T1 property1)
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1);

        public void Information<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2)
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, property2);

        public void Information(string messageTemplate, object[] args)
            => Write(LogEventLevel.Information, exception: null, messageTemplate, args);

        public void Information(Exception exception, string messageTemplate)
            => Write(LogEventLevel.Information, exception, messageTemplate, NoPropertyValues);

        public void Information<T>(Exception exception, string messageTemplate, T property)
            => Write(LogEventLevel.Information, exception, messageTemplate, property);

        public void Information<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1)
            => Write(LogEventLevel.Information, exception, messageTemplate, property0, property1);

        public void Information<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
            => Write(LogEventLevel.Information, exception, messageTemplate, property0, property1, property2);

        public void Information(Exception exception, string messageTemplate, object[] args)
            => Write(LogEventLevel.Information, exception, messageTemplate, args);

        public void Warning(string messageTemplate)
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, NoPropertyValues);

        public void Warning<T>(string messageTemplate, T property)
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property);

        public void Warning<T0, T1>(string messageTemplate, T0 property0, T1 property1)
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1);

        public void Warning<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2)
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1, property2);

        public void Warning(string messageTemplate, object[] args)
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, args);

        public void Warning(Exception exception, string messageTemplate)
            => Write(LogEventLevel.Warning, exception, messageTemplate, NoPropertyValues);

        public void Warning<T>(Exception exception, string messageTemplate, T property)
            => Write(LogEventLevel.Warning, exception, messageTemplate, property);

        public void Warning<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1)
            => Write(LogEventLevel.Warning, exception, messageTemplate, property0, property1);

        public void Warning<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
            => Write(LogEventLevel.Warning, exception, messageTemplate, property0, property1, property2);

        public void Warning(Exception exception, string messageTemplate, object[] args)
            => Write(LogEventLevel.Warning, exception, messageTemplate, args);

        public void Error(string messageTemplate)
            => Write(LogEventLevel.Error, exception: null, messageTemplate, NoPropertyValues);

        public void Error<T>(string messageTemplate, T property)
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property);

        public void Error<T0, T1>(string messageTemplate, T0 property0, T1 property1)
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1);

        public void Error<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2)
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, property2);

        public void Error(string messageTemplate, object[] args)
            => Write(LogEventLevel.Error, exception: null, messageTemplate, args);

        public void Error(Exception exception, string messageTemplate)
            => Write(LogEventLevel.Error, exception, messageTemplate, NoPropertyValues);

        public void Error<T>(Exception exception, string messageTemplate, T property)
            => Write(LogEventLevel.Error, exception, messageTemplate, property);

        public void Error<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1)
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1);

        public void Error<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1, property2);

        public void Error(Exception exception, string messageTemplate, object[] args)
            => Write(LogEventLevel.Error, exception, messageTemplate, args);

        private void Write<T>(LogEventLevel level, Exception exception, string messageTemplate, T property)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, new object[] { property });
            }
        }

        private void Write<T0, T1>(LogEventLevel level, Exception exception, string messageTemplate, T0 property0, T1 property1)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, new object[] { property0, property1 });
            }
        }

        private void Write<T0, T1, T2>(LogEventLevel level, Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, new object[] { property0, property1, property2 });
            }
        }

        private void Write(LogEventLevel level, Exception exception, string messageTemplate, object[] args)
        {
            _logger.Write(level, exception, messageTemplate, args);
        }
    }
}
