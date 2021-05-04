// <copyright file="DatadogSerilogLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal class DatadogSerilogLogger : IDatadogLogger
    {
        private static readonly object[] NoPropertyValues = Datadog.Trace.Util.ArrayHelper.Empty<object>();
        private readonly ILogger _logger;
        private readonly ILogRateLimiter _rateLimiter;

        public DatadogSerilogLogger(ILogger logger, ILogRateLimiter rateLimiter)
        {
            _logger = logger;
            _rateLimiter = rateLimiter;
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

        private void Write<T>(LogEventLevel level, Exception exception, string messageTemplate, T property, int sourceLine, string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, new object[] { property }, sourceLine, sourceFile);
            }
        }

        private void Write<T0, T1>(LogEventLevel level, Exception exception, string messageTemplate, T0 property0, T1 property1, int sourceLine, string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, new object[] { property0, property1 }, sourceLine, sourceFile);
            }
        }

        private void Write<T0, T1, T2>(LogEventLevel level, Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine, string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, new object[] { property0, property1, property2 }, sourceLine, sourceFile);
            }
        }

        private void Write(LogEventLevel level, Exception exception, string messageTemplate, object[] args, int sourceLine, string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid rate limiting calculation if log level is disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, args, sourceLine, sourceFile);
            }
        }

        private void WriteIfNotRateLimited(LogEventLevel level, Exception exception, string messageTemplate, object[] args, int sourceLine, string sourceFile)
        {
            try
            {
                if (_rateLimiter.ShouldLog(sourceFile, sourceLine, out var skipCount))
                {
                    // RFC suggests we should always add the "messages skipped" line, but feels like uneccessary noise
                    if (skipCount > 0)
                    {
                        var newArgs = new object[args.Length + 1];
                        Array.Copy(args, newArgs, args.Length);
                        newArgs[args.Length] = skipCount;

                        _logger.Write(level, exception, messageTemplate + ", {SkipCount} additional messages skipped", newArgs);
                    }
                    else
                    {
                        _logger.Write(level, exception, messageTemplate, args);
                    }
                }
            }
            catch
            {
                try
                {
                    var ex = exception is null ? string.Empty : $"; {exception}";
                    var properties = args.Length == 0
                        ? string.Empty
                        : "; " + string.Join(", ", args);

                    Console.Error.WriteLine($"{messageTemplate}{properties}{ex}");
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
