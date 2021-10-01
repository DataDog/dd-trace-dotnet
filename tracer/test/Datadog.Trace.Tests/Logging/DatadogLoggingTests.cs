// <copyright file="DatadogLoggingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Logging
{
    [NonParallelizable]
    public class DatadogLoggingTests : IDisposable
    {
        private ILogger _logger = null;
        private CollectionSink _logEventSink;
        private IDisposable _clockDisposable;

        [SetUp]
        public void Setup()
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
            _clockDisposable?.Dispose();
        }

        [Test]
        public void InformationLevel_EnabledBy_Default()
        {
            _logger.Information("Information level message");
            _logger.Debug("Debug level message");

            _logEventSink.Events.Count.Should().Be(1);
        }

        [Test]
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

        [Test]
        public void MessageTemplates_ReplacesArgumentsCorrectly()
        {
            var value = 123;
            _logger.Warning("Warning level message with argument '{MyArgument}'", value);

            var log = _logEventSink.Events.FirstOrDefault();

            Assert.IsNotNull(log);

            var property = log.Properties.FirstOrDefault();

            Assert.IsNotNull(property);

            Assert.AreEqual("MyArgument", property.Key);
            Assert.AreEqual("123", property.Value.ToString());
            Assert.AreEqual("Warning level message with argument '123'", log.RenderMessage());
        }

        [Test]
        public void RateLimiting_WhenLoggingIsOnSeparateLines_DoesntRateLimit()
        {
            const int secondsBetweenLogs = 60;
            var rateLimiter = new LogRateLimiter(secondsBetweenLogs);
            var logger = new DatadogSerilogLogger(_logger, rateLimiter);

            var clock = new SimpleClock();
            _clockDisposable = Clock.SetForCurrentThread(clock);

            logger.Warning("Warning level message");
            logger.Warning("Warning level message");
            logger.Warning("Warning level message");

            clock.UtcNow = clock.UtcNow.AddSeconds(secondsBetweenLogs);
            logger.Warning("Warning level message");

            Assert.True(
                _logEventSink.Events.Count == 4,
                $"Found {_logEventSink.Events.Count} messages: \r\n{string.Join("\r\n", _logEventSink.Events.Select(l => l.RenderMessage()))}");
        }

        [Test]
        public void RateLimiting_WhenLoggingIsASingleLocation_AppliesRateLimiting()
        {
            const int secondsBetweenLogs = 60;
            var rateLimiter = new LogRateLimiter(secondsBetweenLogs);
            var logger = new DatadogSerilogLogger(_logger, rateLimiter);

            var clock = new SimpleClock();
            _clockDisposable = Clock.SetForCurrentThread(clock);

            WriteRateLimitedLogMessage(logger, "Warning level message");
            WriteRateLimitedLogMessage(logger, "Warning level message");
            WriteRateLimitedLogMessage(logger, "Warning level message");

            clock.UtcNow = clock.UtcNow.AddSeconds(secondsBetweenLogs);
            WriteRateLimitedLogMessage(logger, "Warning level message");

            Assert.True(
                _logEventSink.Events.Count == 2,
                $"Found {_logEventSink.Events.Count} messages: \r\n{string.Join("\r\n", _logEventSink.Events.Select(l => l.RenderMessage()))}");
        }

        [Test]
        public void RateLimiting_ConsidersLogMessagesOnSameLineToBeSame()
        {
            const int secondsBetweenLogs = 60;
            var rateLimiter = new LogRateLimiter(secondsBetweenLogs);
            var logger = new DatadogSerilogLogger(_logger, rateLimiter);

            var clock = new SimpleClock();
            _clockDisposable = Clock.SetForCurrentThread(clock);

#pragma warning disable SA1107 // Code must not contain multiple statements on one line
            logger.Warning("Warning level message"); logger.Warning("Warning level message");
#pragma warning restore SA1107 // Code must not contain multiple statements on one line

            Assert.True(
                _logEventSink.Events.Count == 1,
                $"Found {_logEventSink.Events.Count} messages: \r\n{string.Join("\r\n", _logEventSink.Events.Select(l => l.RenderMessage()))}");
        }

        [Test]
        public void MessageTemplates_WhenRateLimitingEnabled_IncludesTheNumberOfSkippedMessages()
        {
            const int secondsBetweenLogs = 60;
            var rateLimiter = new LogRateLimiter(secondsBetweenLogs);
            var logger = new DatadogSerilogLogger(_logger, rateLimiter);
            var clock = new SimpleClock();
            _clockDisposable = Clock.SetForCurrentThread(clock);

            WriteRateLimitedLogMessage(logger, "Warning level message");

            // these aren't written, as rate limited
            WriteRateLimitedLogMessage(logger, "Warning level message");
            WriteRateLimitedLogMessage(logger, "Warning level message");
            WriteRateLimitedLogMessage(logger, "Warning level message");

            clock.UtcNow = clock.UtcNow.AddSeconds(secondsBetweenLogs);
            WriteRateLimitedLogMessage(logger, "Warning level message");

            Assert.True(
                _logEventSink.Events.Count == 2,
                $"Found {_logEventSink.Events.Count} messages: \r\n{string.Join("\r\n", _logEventSink.Events.Select(l => l.RenderMessage()))}");

            _logEventSink.Events.Should()
                         .SatisfyRespectively(
                              log => log.RenderMessage().EndsWith(", 0 additional messages skipped"),
                              log => log.RenderMessage().EndsWith(", 3 additional messages skipped"));
        }

        [Test]
        public void ErrorsDuringRateLimiting_DontBubbleUp()
        {
            var rateLimiter = new FailingRateLimiter();
            var logger = new DatadogSerilogLogger(_logger, rateLimiter);

            // Should not throw
            logger.Warning("Warning level message");

            Assert.True(rateLimiter.WasInvoked);
            _logEventSink.Events.Should().BeEmpty();
        }

        [Test]
        public void ErrorsDuringLogging_DontBubbleUp()
        {
            var mockLogger = new Mock<ILogger>();
            mockLogger
                .Setup(x => x.Write(It.IsAny<LogEventLevel>(), It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Throws(new NotImplementedException());

            var logger = new DatadogSerilogLogger(mockLogger.Object, new NullLogRateLimiter());

            // Should not throw
            logger.Warning("Warning level message");

            _logEventSink.Events.Should().BeEmpty();
            mockLogger.Verify();
        }

        private void WriteRateLimitedLogMessage(IDatadogLogger logger, string message)
            => logger.Warning(message);

        private class CollectionSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new List<LogEvent>();

            public void Emit(LogEvent le)
            {
                Events.Add(le);
            }
        }

        private class FailingRateLimiter : ILogRateLimiter
        {
            public bool WasInvoked { get; private set; }

            public bool ShouldLog(string filePath, int lineNumber, out uint skipCount)
            {
                WasInvoked = true;
                throw new NotImplementedException();
            }
        }
    }
}
