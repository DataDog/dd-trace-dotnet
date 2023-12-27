// <copyright file="DatadogLoggingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.Internal.Configuration;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Collectors;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [CollectionDefinition(nameof(Datadog.Trace.Tests.Logging), DisableParallelization = true)]
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class DatadogLoggingTests : IDisposable
    {
        private readonly ILogger _logger = null;
        private readonly CollectionSink _logEventSink;
        private IDisposable _clockDisposable;

        public DatadogLoggingTests()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.LogFileRetentionDays, "36");

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

        [Fact]
        public void RateLimiting_WhenLoggingIsOnSeparateLines_DoesntRateLimit()
        {
            const int secondsBetweenLogs = 60;
            var rateLimiter = new LogRateLimiter(secondsBetweenLogs);
            var logger = new DatadogSerilogLogger(_logger, rateLimiter, null);

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

        [Fact]
        public void RateLimiting_WhenLoggingIsASingleLocation_AppliesRateLimiting()
        {
            const int secondsBetweenLogs = 60;
            var rateLimiter = new LogRateLimiter(secondsBetweenLogs);
            var logger = new DatadogSerilogLogger(_logger, rateLimiter, null);

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

        [Fact]
        public void RateLimiting_ConsidersLogMessagesOnSameLineToBeSame()
        {
            const int secondsBetweenLogs = 60;
            var rateLimiter = new LogRateLimiter(secondsBetweenLogs);
            var logger = new DatadogSerilogLogger(_logger, rateLimiter, null);

            var clock = new SimpleClock();
            _clockDisposable = Clock.SetForCurrentThread(clock);

#pragma warning disable SA1107 // Code must not contain multiple statements on one line
            logger.Warning("Warning level message"); logger.Warning("Warning level message");
#pragma warning restore SA1107 // Code must not contain multiple statements on one line

            Assert.True(
                _logEventSink.Events.Count == 1,
                $"Found {_logEventSink.Events.Count} messages: \r\n{string.Join("\r\n", _logEventSink.Events.Select(l => l.RenderMessage()))}");
        }

        [Fact]
        public void MessageTemplates_WhenRateLimitingEnabled_IncludesTheNumberOfSkippedMessages()
        {
            const int secondsBetweenLogs = 60;
            var rateLimiter = new LogRateLimiter(secondsBetweenLogs);
            var logger = new DatadogSerilogLogger(_logger, rateLimiter, null);
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

            Assert.Collection(
                _logEventSink.Events,
                log => log.RenderMessage().EndsWith(", 0 additional messages skipped"),
                log => log.RenderMessage().EndsWith(", 3 additional messages skipped"));
        }

        [Fact]
        public void ErrorsDuringRateLimiting_DontBubbleUp()
        {
            var rateLimiter = new FailingRateLimiter();
            var logger = new DatadogSerilogLogger(_logger, rateLimiter, null);

            // Should not throw
            logger.Warning("Warning level message");

            Assert.True(rateLimiter.WasInvoked);
            Assert.Empty(_logEventSink.Events);
        }

        [Fact]
        public void ErrorsDuringLogging_DontBubbleUp()
        {
            var mockLogger = new Mock<ILogger>();
            mockLogger
                .Setup(x => x.Write(It.IsAny<LogEventLevel>(), It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Throws(new NotImplementedException());

            var logger = new DatadogSerilogLogger(mockLogger.Object, new NullLogRateLimiter(), null);

            // Should not throw
            logger.Warning("Warning level message");

            Assert.Empty(_logEventSink.Events);
            mockLogger.Verify();
        }

        [Fact]
        public void DuringStartup_OldLogFilesGetDeleted()
        {
            var tempLogsDir = Path.Combine(Path.GetTempPath(), "Datadog .NET Tracer\\logs");
            Directory.CreateDirectory(tempLogsDir);

            // Creating log files that match expected formats
            var logPath = Path.Combine(tempLogsDir, "DD-DotNet-Profiler-Native-1.log");
            File.Create(logPath).Dispose();
            File.SetLastWriteTime(logPath, DateTime.Now.AddDays(-39));

            File.Create(Path.Combine(tempLogsDir, "dotnet-tracer-managed-2.log")).Dispose();
            File.Create(Path.Combine(tempLogsDir, "dotnet-tracer-native-3.log")).Dispose();

            for (int i = 0; i < 3; i++)
            {
                // Adding random files that don't match the pattern
                var mockFilePath = Path.Combine(tempLogsDir, $"random-file-{i}.txt");
                File.Create(mockFilePath).Dispose();
                File.SetLastWriteTime(mockFilePath, DateTime.Now.AddDays(-31 * i));
            }

            // Running method to delete the old files
            DatadogLogging.CleanLogFiles(32, tempLogsDir);

            var deletedLogFiles = Directory.EnumerateFiles(tempLogsDir, "DD-DotNet-Profiler-Native-*").Count();
            var retainedLogFiles = Directory.EnumerateFiles(tempLogsDir, "dotnet-tracer-*").Count();
            var ignoredLogFiles = Directory.EnumerateFiles(tempLogsDir, "random-file-*").Count();

            // Deleting temporary directory after test run
            Directory.Delete(tempLogsDir, true);

            // Asserting that the correct amount of files are left
            deletedLogFiles.Should().Be(0);
            retainedLogFiles.Should().Be(2);
            ignoredLogFiles.Should().Be(3);
        }

        [Fact]
        public void WritingToAnInvalidPathDoesntCauseErrors()
        {
            string directory;

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                // Windows
                directory = @"Z:\i-dont-exist";
            }
            else
            {
                // Linux
                directory = "/proc/self";
            }

            // make sure we can't write to it
            IsDirectoryWritable(directory).Should().BeFalse();

            var config = DatadogLoggingFactory.GetConfiguration(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.LogDirectory, directory } }),
                NullConfigurationTelemetry.Instance);
            var logger = DatadogLoggingFactory.CreateFromConfiguration(config, DomainMetadata.Instance);
            logger.Should().NotBeNull();

            logger!.Error("Trying to write an error");

            logger.CloseAndFlush();

            static bool IsDirectoryWritable(string dirPath)
            {
                try
                {
                    var path = Path.Combine(dirPath, Path.GetRandomFileName());
                    using (var fs = File.Create(path, bufferSize: 1, FileOptions.DeleteOnClose))
                    {
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        [Fact]
        public void RedactedErrorLogs_WritesErrorLogs()
        {
            var collector = new RedactedErrorLogCollector();

            var config = new DatadogLoggingConfiguration(
                rateLimit: 0,
                file: null,
                errorLogging: new RedactedErrorLoggingConfiguration(collector));

            var logger = DatadogLoggingFactory.CreateFromConfiguration(in config, DomainMetadata.Instance);

            logger.Should().NotBeNull();
            const string errorMessage = "This is some error";
            logger.Error(errorMessage);
            var errorLog = collector.GetLogs()
                                    .Should()
                                    .ContainSingle()
                                    .Which.Should()
                                    .ContainSingle()
                                    .Subject;
            errorLog.Level.Should().Be(TelemetryLogLevel.ERROR);
            errorLog.Message.Should().Be(errorMessage);
            errorLog.StackTrace.Should().BeNull();

            // No more logs, so should still be null
            collector.GetLogs().Should().BeNull();

            // These shouldn't be written
            logger.Debug("Just a debug");
            logger.Information("Just an info");
            logger.Warning("Just a warning");
            collector.GetLogs().Should().BeNull();

            Exception ex;
            try
            {
                throw new Exception("Some Exception");
            }
            catch (Exception e)
            {
                ex = e;
            }

            // Should write error logs that have an exception
            logger.Error(ex, errorMessage);
            errorLog = collector.GetLogs()
                                    .Should()
                                    .ContainSingle()
                                    .Which.Should()
                                    .ContainSingle()
                                    .Subject;

            errorLog.Level.Should().Be(TelemetryLogLevel.ERROR);
            errorLog.Message.Should().Be(errorMessage);
            errorLog.StackTrace.Should().NotBeNull();

            // No more logs, so should still be null
            collector.GetLogs().Should().BeNull();

            // These shouldn't be written
            logger.Debug(ex, "Just a debug");
            logger.Information(ex, "Just an info");
            logger.Warning(ex, "Just a warning");
            collector.GetLogs().Should().BeNull();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RedactedErrorLogs_ExcludesSpecificMessages(bool withException)
        {
            var collector = new RedactedErrorLogCollector();

            var config = new DatadogLoggingConfiguration(
                rateLimit: 0,
                file: null,
                errorLogging: new RedactedErrorLoggingConfiguration(collector));

            var logger = DatadogLoggingFactory.CreateFromConfiguration(in config, DomainMetadata.Instance);

            logger.Should().NotBeNull();

            // These errors should not be written
            if (withException)
            {
                logger.Error(new Exception(), Api.FailedToSendMessageTemplate, "http://localhost:8126");
            }
            else
            {
                logger.Error(Api.FailedToSendMessageTemplate, "http://localhost:8126");
            }

#if NETFRAMEWORK
            if (withException)
            {
                logger.Error(new Exception(), PerformanceCountersListener.InsufficientPermissionsMessageTemplate);
            }
            else
            {
                logger.Error(PerformanceCountersListener.InsufficientPermissionsMessageTemplate);
            }
#endif

            collector.GetLogs().Should().BeNull();
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
