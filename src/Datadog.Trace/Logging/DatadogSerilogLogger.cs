using System;
using System.Runtime.CompilerServices;

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

        public void Debug(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Debug<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property, sourceLine, sourceFile);

        public void Debug<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Debug<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Debug(string messageTemplate, object[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, args, sourceLine, sourceFile);

        public void Debug(Exception exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Debug<T>(Exception exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, property, sourceLine, sourceFile);

        public void Debug<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Debug<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Debug(Exception exception, string messageTemplate, object[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, args, sourceLine, sourceFile);

        public void Information(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Information<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property, sourceLine, sourceFile);

        public void Information<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Information<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Information(string messageTemplate, object[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, args, sourceLine, sourceFile);

        public void Information(Exception exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Information<T>(Exception exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, property, sourceLine, sourceFile);

        public void Information<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Information<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Information(Exception exception, string messageTemplate, object[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, args, sourceLine, sourceFile);

        public void Warning(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Warning<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property, sourceLine, sourceFile);

        public void Warning<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Warning<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Warning(string messageTemplate, object[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, args, sourceLine, sourceFile);

        public void Warning(Exception exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Warning<T>(Exception exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, property, sourceLine, sourceFile);

        public void Warning<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Warning<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Warning(Exception exception, string messageTemplate, object[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, args, sourceLine, sourceFile);

        public void Error(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Error<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property, sourceLine, sourceFile);

        public void Error<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Error<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Error(string messageTemplate, object[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, args, sourceLine, sourceFile);

        public void Error(Exception exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Error<T>(Exception exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property, sourceLine, sourceFile);

        public void Error<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Error<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Error(Exception exception, string messageTemplate, object[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, args, sourceLine, sourceFile);

        private void Write<T>(LogEventLevel level, Exception exception, string messageTemplate, T property, int sourceLine,  string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, new object[] { property }, sourceLine, sourceFile);
            }
        }

        private void Write<T0, T1>(LogEventLevel level, Exception exception, string messageTemplate, T0 property0, T1 property1, int sourceLine,  string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, new object[] { property0, property1 }, sourceLine, sourceFile);
            }
        }

        private void Write<T0, T1, T2>(LogEventLevel level, Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine,  string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, new object[] { property0, property1, property2 }, sourceLine, sourceFile);
            }
        }

        private void Write(LogEventLevel level, Exception exception, string messageTemplate, object[] args, int sourceLine,  string sourceFile)
        {
            _logger.Write(level, exception, messageTemplate, args);
        }
    }
}
