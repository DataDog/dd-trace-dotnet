// <copyright file="DatadogSerilogLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core.Pipeline;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal class DatadogSerilogLogger : IDatadogLogger
    {
        private static readonly object[] NoPropertyValues = Array.Empty<object>();
        private readonly ILogRateLimiter _rateLimiter;
        private ILogger _logger;

        public DatadogSerilogLogger(ILogger logger, ILogRateLimiter rateLimiter, string? fileLogDirectory)
        {
            _logger = logger;
            _rateLimiter = rateLimiter;
            FileLogDirectory = fileLogDirectory;
        }

        public static DatadogSerilogLogger NullLogger { get; } = new(SilentLogger.Instance, new NullLogRateLimiter(), null);

        public string? FileLogDirectory { get; }

        public bool IsEnabled(LogEventLevel level) => _logger.IsEnabled(level);

        public void Debug(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Debug<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property, sourceLine, sourceFile);

        public void Debug<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Debug<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Debug<T0, T1, T2, T3>(string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, property2, property3, sourceLine, sourceFile);

        public void Debug(string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, args, sourceLine, sourceFile);

        public void Debug(Exception? exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Debug<T>(Exception? exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, property, sourceLine, sourceFile);

        public void Debug<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Debug<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Debug(Exception? exception, string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, args, sourceLine, sourceFile);

        public void Information(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Information<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property, sourceLine, sourceFile);

        public void Information<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Information<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Information(string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, args, sourceLine, sourceFile);

        public void Information(Exception? exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Information<T>(Exception? exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, property, sourceLine, sourceFile);

        public void Information<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Information<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Information(Exception? exception, string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, args, sourceLine, sourceFile);

        public void Warning(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Warning<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property, sourceLine, sourceFile);

        public void Warning<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Warning<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Warning(string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, args, sourceLine, sourceFile);

        public void Warning(Exception? exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Warning<T>(Exception? exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, property, sourceLine, sourceFile);

        public void Warning<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Warning<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Warning(Exception? exception, string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, args, sourceLine, sourceFile);

        public void Error(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Error<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property, sourceLine, sourceFile);

        public void Error<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Error<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Error(string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, args, sourceLine, sourceFile);

        public void Error(Exception? exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile);

        public void Error<T>(Exception? exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property, sourceLine, sourceFile);

        public void Error<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1, sourceLine, sourceFile);

        public void Error<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile);

        public void Error(Exception? exception, string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, args, sourceLine, sourceFile);

        public void CloseAndFlush()
        {
            var logger = Interlocked.Exchange(ref _logger, SilentLogger.Instance);

            (logger as IDisposable)?.Dispose();
        }

        private void Write<T>(LogEventLevel level, Exception? exception, string messageTemplate, T property, int sourceLine, string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, [property], sourceLine, sourceFile);
            }
        }

        private void Write<T0, T1>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, int sourceLine, string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, [property0, property1], sourceLine, sourceFile);
            }
        }

        private void Write<T0, T1, T2>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine, string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, [property0, property1, property2], sourceLine, sourceFile);
            }
        }

        private void Write<T0, T1, T2, T3>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, int sourceLine, string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, [property0, property1, property2, property3], sourceLine, sourceFile);
            }
        }

        private void Write(LogEventLevel level, Exception? exception, string messageTemplate, object?[] args, int sourceLine, string sourceFile)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid rate limiting calculation if log level is disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, args, sourceLine, sourceFile);
            }
        }

        private void WriteIfNotRateLimited(LogEventLevel level, Exception? exception, string messageTemplate, object?[] args, int sourceLine, string sourceFile)
        {
            try
            {
                TelemetryFactory.Metrics.RecordCountLogCreated(LevelToTag(level));
                if (_rateLimiter.ShouldLog(sourceFile, sourceLine, out var skipCount))
                {
                    // RFC suggests we should always add the "messages skipped" line, but feels like unnecessary noise
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

            static MetricTags.LogLevel LevelToTag(LogEventLevel logLevel)
                => logLevel switch
                {
                    LogEventLevel.Verbose => MetricTags.LogLevel.Debug,
                    LogEventLevel.Debug => MetricTags.LogLevel.Debug,
                    LogEventLevel.Information => MetricTags.LogLevel.Information,
                    LogEventLevel.Warning => MetricTags.LogLevel.Warning,
                    _ => MetricTags.LogLevel.Error,
                };
        }
    }
}
