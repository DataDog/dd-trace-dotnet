using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [CollectionDefinition(nameof(Datadog.Trace.Tests.Logging), DisableParallelization = true)]
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class DatadogLoggingTests : IDisposable
    {
        private readonly ILogger _logger = null;
        private readonly CollectionSink _logEventSink;

        public DatadogLoggingTests()
        {
            GlobalSettings.Reload();

            _logEventSink = new CollectionSink();
            _logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(DatadogLogging.LoggingLevelSwitch)
                .WriteTo.Sink(_logEventSink)
                // .WriteTo.Observers(obs => obs.Subscribe(logEvent => _logEvents.Add(logEvent)))
                .CreateLogger();
        }

        public void Dispose()
        {
            // On test cleanup, reload the GlobalSettings
            GlobalSettings.Reload();
        }

        [Fact]
        public void InformationLevel_EnabledBy_Default()
        {
            _logger.Information("Information level message");
            _logger.Debug("Debug level message");

            Assert.Single(_logEventSink.Events);
        }

        [Fact]
        public void DebugLevel_EnabledBy_GlobalSettings()
        {
            _logger.Information("Information level message");
            _logger.Debug("First debug level message");

            // Enable Debug-level logging
            GlobalSettings.SetDebugEnabled(true);

            _logger.Debug("Second debug level message");

            Assert.True(
                _logEventSink.Events.Count == 2,
                $"Found {_logEventSink.Events.Count} messages: \r\n{string.Join("\r\n", _logEventSink.Events.Select(l => l.RenderMessage()))}");
        }

        [Fact]
        public void MessageTemplates_ReplacesArgumentsCorrectly()
        {
            var value = 123;
            _logger.Warning("Warning level message with argument '{MyArgument}'", value);

            var log = Assert.Single(_logEventSink.Events);
            var property = Assert.Single(log.Properties);

            Assert.Equal("MyArgument", property.Key);
            Assert.Equal("123", property.Value.ToString());
            Assert.Equal("Warning level message with argument '123'", log.RenderMessage());
        }

        private class CollectionSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new List<LogEvent>();

            public void Emit(LogEvent le)
            {
                Events.Add(le);
            }
        }
    }
}
