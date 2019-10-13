using System;
using System.Collections.Concurrent;
using Datadog.Trace.Vendors.Serilog.Capturing;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal class RateLimitedLogger : Datadog.Trace.Vendors.Serilog.Core.Logger
    {
        private const string LogIntervalKey = "DD_LOGGING_RATE";

        private readonly ConcurrentDictionary<string, int> _logBuckets = new ConcurrentDictionary<string, int>();
        private DateTime _intervalStarted = DateTime.UtcNow;
        private int _rateLimitInterval = 30;

        internal RateLimitedLogger(MessageTemplateProcessor messageTemplateProcessor, LogEventLevel minimumLevel, ILogEventSink sink, ILogEventEnricher enricher, Action dispose = null, LevelOverrideMap overrideMap = null)
            : base(messageTemplateProcessor, minimumLevel, sink, enricher, dispose, overrideMap)
        {
            SetLoggingRate();
        }

        internal RateLimitedLogger(MessageTemplateProcessor messageTemplateProcessor, LoggingLevelSwitch levelSwitch, ILogEventSink sink, ILogEventEnricher enricher, Action dispose = null, LevelOverrideMap overrideMap = null)
            : base(messageTemplateProcessor, levelSwitch, sink, enricher, dispose, overrideMap)
        {
            SetLoggingRate();
        }

        public override void Write(LogEventLevel level, Exception exception, string messageTemplate, params object[] propertyValues)
        {
            // If debug enabled, ALWAYS log
            if (Tracer.Instance?.Settings.DebugEnabled != true)
            {
                var now = DateTime.UtcNow;
                if (_intervalStarted.AddSeconds(_rateLimitInterval) >= now)
                {
                    // Clear the buckets and reset the time
                    _intervalStarted = now;
                    _logBuckets.Clear();
                }
                else if (ShouldSkip(level, exception, messageTemplate, propertyValues))
                {
                    // Rate limiting has been triggered
                    return;
                }
            }

            base.Write(level, exception, messageTemplate, propertyValues);
        }

        public bool ShouldSkip(LogEventLevel level, Exception exception, string messageTemplate, params object[] propertyValues)
        {
            var key = $"{(int)level}_{ExceptionSource(exception)}_{messageTemplate}";
            if (_logBuckets.ContainsKey(key))
            {
                return true;
            }

            _logBuckets.TryAdd(key, 1);
            return false;
        }

        private string ExceptionSource(Exception exception)
        {
            if (exception == null)
            {
                return "NULL";
            }

            return $"{exception.TargetSite.Module.ModuleVersionId}_{exception.TargetSite.MetadataToken}";
        }

        private void SetLoggingRate()
        {
            var logIntervalVariable = Environment.GetEnvironmentVariable(LogIntervalKey);
            _rateLimitInterval = int.TryParse(logIntervalVariable, out var loggingInterval) ? loggingInterval : 30;
        }
    }
}
