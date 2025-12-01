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
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Context;
using Datadog.Trace.Vendors.Serilog.Core.Pipeline;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal class DatadogSerilogLogger : IDatadogLogger
    {
        internal const string SkipTelemetryProperty = "SkipTelemetry";
        private static readonly object[] NoPropertyValues = Array.Empty<object>();
        private ILogger _logger;

        public DatadogSerilogLogger(ILogger logger, FileLoggingConfiguration? fileLoggingConfiguration)
        {
            _logger = logger;
            FileLoggingConfiguration = fileLoggingConfiguration;
        }

        public static DatadogSerilogLogger NullLogger { get; } = new(SilentLogger.Instance, null);

        public FileLoggingConfiguration? FileLoggingConfiguration { get; }

        public bool IsEnabled(LogEventLevel level) => _logger.IsEnabled(level);

        public void Debug(string messageTemplate)
            => TryWrite(LogEventLevel.Debug, exception: null, messageTemplate, NoPropertyValues, skipTelemetry: false);

        public void Debug<T>(string messageTemplate, T property)
            => TryWrite(LogEventLevel.Debug, exception: null, messageTemplate, property, skipTelemetry: false);

        public void Debug<T0, T1>(string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, skipTelemetry: false);

        public void Debug<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, property2, skipTelemetry: false);

        public void Debug<T0, T1, T2, T3>(string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3)
            => TryWrite(LogEventLevel.Debug, exception: null, messageTemplate, property0, property1, property2, property3, skipTelemetry: false);

        public void Debug(string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Debug, exception: null, messageTemplate, args, skipTelemetry: false);

        public void Debug(Exception? exception, string messageTemplate)
            => TryWrite(LogEventLevel.Debug, exception, messageTemplate, NoPropertyValues, skipTelemetry: false);

        public void Debug<T>(Exception? exception, string messageTemplate, T property)
            => TryWrite(LogEventLevel.Debug, exception, messageTemplate, property, skipTelemetry: false);

        public void Debug<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Debug, exception, messageTemplate, property0, property1, skipTelemetry: false);

        public void Debug<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Debug, exception, messageTemplate, property0, property1, property2, skipTelemetry: false);

        public void Debug(Exception? exception, string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Debug, exception, messageTemplate, args, skipTelemetry: false);

        public void Information(string messageTemplate)
            => TryWrite(LogEventLevel.Information, exception: null, messageTemplate, NoPropertyValues, skipTelemetry: false);

        public void Information<T>(string messageTemplate, T property)
            => TryWrite(LogEventLevel.Information, exception: null, messageTemplate, property, skipTelemetry: false);

        public void Information<T0, T1>(string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, skipTelemetry: false);

        public void Information<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Information, exception: null, messageTemplate, property0, property1, property2, skipTelemetry: false);

        public void Information(string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Information, exception: null, messageTemplate, args, skipTelemetry: false);

        public void Information(Exception? exception, string messageTemplate)
            => TryWrite(LogEventLevel.Information, exception, messageTemplate, NoPropertyValues, skipTelemetry: false);

        public void Information<T>(Exception? exception, string messageTemplate, T property)
            => TryWrite(LogEventLevel.Information, exception, messageTemplate, property, skipTelemetry: false);

        public void Information<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Information, exception, messageTemplate, property0, property1, skipTelemetry: false);

        public void Information<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Information, exception, messageTemplate, property0, property1, property2, skipTelemetry: false);

        public void Information(Exception? exception, string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Information, exception, messageTemplate, args, skipTelemetry: false);

        public void Warning(string messageTemplate)
            => TryWrite(LogEventLevel.Warning, exception: null, messageTemplate, NoPropertyValues, skipTelemetry: false);

        public void Warning<T>(string messageTemplate, T property)
            => TryWrite(LogEventLevel.Warning, exception: null, messageTemplate, property, skipTelemetry: false);

        public void Warning<T0, T1>(string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1, skipTelemetry: false);

        public void Warning<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Warning, exception: null, messageTemplate, property0, property1, property2, skipTelemetry: false);

        public void Warning(string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Warning, exception: null, messageTemplate, args, skipTelemetry: false);

        public void Warning(Exception? exception, string messageTemplate)
            => TryWrite(LogEventLevel.Warning, exception, messageTemplate, NoPropertyValues, skipTelemetry: false);

        public void Warning<T>(Exception? exception, string messageTemplate, T property)
            => TryWrite(LogEventLevel.Warning, exception, messageTemplate, property, skipTelemetry: false);

        public void Warning<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Warning, exception, messageTemplate, property0, property1, skipTelemetry: false);

        public void Warning<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Warning, exception, messageTemplate, property0, property1, property2, skipTelemetry: false);

        public void Warning(Exception? exception, string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Warning, exception, messageTemplate, args, skipTelemetry: false);

        public void Error(string messageTemplate)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, NoPropertyValues, skipTelemetry: false);

        public void Error<T>(string messageTemplate, T property)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, property, skipTelemetry: false);

        public void Error<T0, T1>(string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, skipTelemetry: false);

        public void Error<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, property2, skipTelemetry: false);

        public void Error(string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, args, skipTelemetry: false);

        public void Error(Exception? exception, string messageTemplate)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, NoPropertyValues, skipTelemetry: false);

        public void Error<T>(Exception? exception, string messageTemplate, T property)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, property, skipTelemetry: false);

        public void Error<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, property0, property1, skipTelemetry: false);

        public void Error<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, property0, property1, property2, skipTelemetry: false);

        public void Error(Exception? exception, string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, args, skipTelemetry: false);

        public void ErrorSkipTelemetry(string messageTemplate)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, NoPropertyValues, skipTelemetry: true);

        public void ErrorSkipTelemetry<T>(string messageTemplate, T property)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, property, skipTelemetry: true);

        public void ErrorSkipTelemetry<T0, T1>(string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, skipTelemetry: true);

        public void ErrorSkipTelemetry<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, property0, property1, property2, skipTelemetry: true);

        public void ErrorSkipTelemetry(string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Error, exception: null, messageTemplate, args, skipTelemetry: true);

        public void ErrorSkipTelemetry(Exception? exception, string messageTemplate)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, NoPropertyValues, skipTelemetry: true);

        public void ErrorSkipTelemetry<T>(Exception? exception, string messageTemplate, T property)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, property, skipTelemetry: true);

        public void ErrorSkipTelemetry<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, property0, property1, skipTelemetry: true);

        public void ErrorSkipTelemetry<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, property0, property1, property2, skipTelemetry: true);

        public void ErrorSkipTelemetry(Exception? exception, string messageTemplate, object?[] args)
            => TryWrite(LogEventLevel.Error, exception, messageTemplate, args, skipTelemetry: true);

        public void CloseAndFlush()
        {
            var logger = Interlocked.Exchange(ref _logger, SilentLogger.Instance);

            (logger as IDisposable)?.Dispose();
        }

        private void TryWrite<T>(LogEventLevel level, Exception? exception, string messageTemplate, T property, bool skipTelemetry)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, [property], skipTelemetry);
            }
        }

        private void TryWrite<T0, T1>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, bool skipTelemetry)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, [property0, property1], skipTelemetry);
            }
        }

        private void TryWrite<T0, T1, T2>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, bool skipTelemetry)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, [property0, property1, property2], skipTelemetry);
            }
        }

        private void TryWrite<T0, T1, T2, T3>(LogEventLevel level, Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, bool skipTelemetry)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid boxing + array allocation if disabled
                Write(level, exception, messageTemplate, [property0, property1, property2, property3], skipTelemetry);
            }
        }

        private void TryWrite(LogEventLevel level, Exception? exception, string messageTemplate, object?[] args, bool skipTelemetry)
        {
            if (_logger.IsEnabled(level))
            {
                // Avoid rate limiting calculation if log level is disabled
                Write(level, exception, messageTemplate, args, skipTelemetry);
            }
        }

        private void Write(LogEventLevel level, Exception? exception, string messageTemplate, object?[] args, bool skipTelemetry)
        {
            IDisposable? logContext = null;
            try
            {
                TelemetryFactory.Metrics.RecordCountLogCreated(LevelToTag(level));
                if (skipTelemetry)
                {
                    // suppress sending this log to telemetry
                    logContext = LogContext.PushProperty(SkipTelemetryProperty, SkipTelemetryProperty);
                }

                _logger.Write(level, exception, messageTemplate, args);
            }
            catch
            {
                // ignore
            }
            finally
            {
                logContext?.Dispose();
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
