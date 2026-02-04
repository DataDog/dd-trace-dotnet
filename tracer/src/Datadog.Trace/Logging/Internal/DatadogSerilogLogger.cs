// <copyright file="DatadogSerilogLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Logging.Internal.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Context;
using Datadog.Trace.Vendors.Serilog.Core.Pipeline;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal sealed class DatadogSerilogLogger : IDatadogLogger
    {
        internal const string SkipTelemetryProperty = "SkipTelemetry";
        private static readonly object[] NoPropertyValues = [];
        private readonly ILogRateLimiter _rateLimiter;
        private ILogger _logger;

        public DatadogSerilogLogger(ILogger logger, ILogRateLimiter rateLimiter, FileLoggingConfiguration? fileLoggingConfiguration)
        {
            _logger = logger;
            _rateLimiter = rateLimiter;
            FileLoggingConfiguration = fileLoggingConfiguration;
        }

        public static DatadogSerilogLogger NullLogger { get; } = new(SilentLogger.Instance, new NullLogRateLimiter(), null);

        public FileLoggingConfiguration? FileLoggingConfiguration { get; }

        public bool IsEnabled(LogEventLevel level) => _logger.IsEnabled(level);

        public void Debug(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug<T0, T1, T2, T3>(string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, property2, property3, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug<T0, T1, T2, T3, T4>(string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, T4 property4, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, property2, property3, property4, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug(string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception: null, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug(Exception? exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug<T>(Exception? exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: false);

        public void Debug(Exception? exception, string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Debug, exception, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: false);

        public void Information(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: false);

        public void Information<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: false);

        public void Information<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: false);

        public void Information<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: false);

        public void Information<T0, T1, T2, T3>(string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, property2, property3, sourceLine, sourceFile, skipTelemetry: false);

        public void Information<T0, T1, T2, T3, T4>(string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, T4 property4, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, property2, property3, property4, sourceLine, sourceFile, skipTelemetry: false);

        public void Information(string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception: null, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: false);

        public void Information(Exception? exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: false);

        public void Information<T>(Exception? exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: false);

        public void Information<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: false);

        public void Information<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: false);

        public void Information(Exception? exception, string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Information, exception, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning(string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception: null, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning(Exception? exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning<T>(Exception? exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: false);

        public void Warning(Exception? exception, string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Warning, exception, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: false);

        public void Error(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: false);

        public void Error<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: false);

        public void Error<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: false);

        public void Error<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: false);

        public void Error(string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: false);

        public void Error(Exception? exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: false);

        public void Error<T>(Exception? exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: false);

        public void Error<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: false);

        public void Error<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: false);

        public void Error(Exception? exception, string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: false);

        public void ErrorSkipTelemetry(string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: true);

        public void ErrorSkipTelemetry<T>(string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: true);

        public void ErrorSkipTelemetry<T0, T1>(string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: true);

        public void ErrorSkipTelemetry<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: true);

        public void ErrorSkipTelemetry(string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception: null, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: true);

        public void ErrorSkipTelemetry(Exception? exception, string messageTemplate, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, NoPropertyValues, sourceLine, sourceFile, skipTelemetry: true);

        public void ErrorSkipTelemetry<T>(Exception? exception, string messageTemplate, T property, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property, sourceLine, sourceFile, skipTelemetry: true);

        public void ErrorSkipTelemetry<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1, sourceLine, sourceFile, skipTelemetry: true);

        public void ErrorSkipTelemetry<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, property0, property1, property2, sourceLine, sourceFile, skipTelemetry: true);

        public void ErrorSkipTelemetry(Exception? exception, string messageTemplate, object?[] args, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
            => Write(LogEventLevel.Error, exception, messageTemplate, args, sourceLine, sourceFile, skipTelemetry: true);

        public void CloseAndFlush()
        {
            var logger = Interlocked.Exchange(ref _logger, SilentLogger.Instance);

            (logger as IDisposable)?.Dispose();
        }

        private void Write<T>(LogEventLevel level, Exception? exception, string messageTemplate, T property, int sourceLine, string sourceFile, bool skipTelemetry)
        {
            // Avoid boxing + array allocation if disabled
            if (!_logger.IsEnabled(level))
            {
                return;
            }

            using var array = FixedSizeArrayPool<object?>.OneItemPool.Get();
            array.Array[0] = property;
            WriteIfNotRateLimited(level, exception, messageTemplate, array.Array, sourceLine, sourceFile, skipTelemetry);
        }

        private void Write<T0, T1>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, int sourceLine, string sourceFile, bool skipTelemetry)
        {
            // Avoid boxing + array allocation if disabled
            if (!_logger.IsEnabled(level))
            {
                return;
            }

            using var array = FixedSizeArrayPool<object?>.TwoItemPool.Get();
            array.Array[0] = property0;
            array.Array[1] = property1;
            WriteIfNotRateLimited(level, exception, messageTemplate, array.Array, sourceLine, sourceFile, skipTelemetry);
        }

        private void Write<T0, T1, T2>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine, string sourceFile, bool skipTelemetry)
        {
            // Avoid boxing + array allocation if disabled
            if (!_logger.IsEnabled(level))
            {
                return;
            }

            using var array = FixedSizeArrayPool<object?>.ThreeItemPool.Get();
            array.Array[0] = property0;
            array.Array[1] = property1;
            array.Array[2] = property2;
            WriteIfNotRateLimited(level, exception, messageTemplate, array.Array, sourceLine, sourceFile, skipTelemetry);
        }

        private void Write<T0, T1, T2, T3>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, int sourceLine, string sourceFile, bool skipTelemetry)
        {
            // Avoid boxing + array allocation if disabled
            if (!_logger.IsEnabled(level))
            {
                return;
            }

            using var array = FixedSizeArrayPool<object?>.FourItemPool.Get();
            array.Array[0] = property0;
            array.Array[1] = property1;
            array.Array[2] = property2;
            array.Array[3] = property3;
            WriteIfNotRateLimited(level, exception, messageTemplate, array.Array, sourceLine, sourceFile, skipTelemetry);
        }

        private void Write<T0, T1, T2, T3, T4>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, T4 property4, int sourceLine, string sourceFile, bool skipTelemetry)
        {
            // Avoid boxing + array allocation if disabled
            if (!_logger.IsEnabled(level))
            {
                return;
            }

            using var array = FixedSizeArrayPool<object?>.FiveItemPool.Get();
            array.Array[0] = property0;
            array.Array[1] = property1;
            array.Array[2] = property2;
            array.Array[3] = property3;
            array.Array[4] = property4;
            WriteIfNotRateLimited(level, exception, messageTemplate, array.Array, sourceLine, sourceFile, skipTelemetry);
        }

        private void Write(LogEventLevel level, Exception? exception, string messageTemplate, object?[] args, int sourceLine, string sourceFile, bool skipTelemetry)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid rate limiting calculation if log level is disabled
                WriteIfNotRateLimited(level, exception, messageTemplate, args, sourceLine, sourceFile, skipTelemetry);
            }
        }

        private void WriteIfNotRateLimited(LogEventLevel level, Exception? exception, string messageTemplate, object?[] args, int sourceLine, string sourceFile, bool skipTelemetry)
        {
            try
            {
                TelemetryFactory.Metrics.RecordCountLogCreated(LevelToTag(level));
                if (_rateLimiter.ShouldLog(sourceFile, sourceLine, out var skipCount))
                {
                    IDisposable? logContext = null;
                    try
                    {
                        if (skipTelemetry)
                        {
                            // suppress sending this log to telemetry
                            logContext = LogContext.PushProperty(SkipTelemetryProperty, SkipTelemetryProperty);
                        }

                        if (skipCount > 0)
                        {
                            // RFC suggests we should always add the "messages skipped" line, but feels like unnecessary noise
                            _logger.Write(level, exception, messageTemplate + ", {SkipCount} additional messages skipped", [.. args, skipCount]);
                        }
                        else
                        {
                            _logger.Write(level, exception, messageTemplate, args);
                        }
                    }
                    finally
                    {
                        logContext?.Dispose();
                    }
                }
            }
            catch
            {
                WriteToStdErr(exception, messageTemplate, args);
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

            static void WriteToStdErr(Exception? e, string template, object?[] objects)
            {
                try
                {
                    var ex = e is null ? string.Empty : $"; {e}";
                    var properties = objects.Length == 0
                                         ? string.Empty
                                         : "; " + string.Join(", ", objects);

                    Console.Error.WriteLine($"{template}{properties}{ex}");
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
