// <copyright file="DirectSubmissionLoggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;
using Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Serilog;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions.Internal;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.ILogger
{
    public class DirectSubmissionLoggerTests
    {
        [Fact]
        public void LoggerEnqueuesLogMessage()
        {
            var sink = new TestSink();
            var logger = GetLogger(sink);

            var level = (int)DirectSubmissionLogLevel.Error;
            logger.IsEnabled(level).Should().BeTrue();
            logger.Log(
                logLevel: level,
                eventId: 123,
                state: "someValue",
                exception: null,
                (state, ex) => $"This is {state}");

            sink.Events.Should().ContainSingle();
        }

        [Fact]
        public void LoggerCanRenderLogMessage()
        {
            var sink = new TestSink();
            var formatter = LogSettingsHelper.GetFormatter();
            var logger = GetLogger(sink);

            var level = (int)DirectSubmissionLogLevel.Error;
            logger.IsEnabled(level).Should().BeTrue();
            var state = "someValue";
            var eventId = 123;
            logger.Log(
                logLevel: level,
                eventId: eventId,
                state: state,
                exception: null,
                (s, ex) => $"This is {s}");

            var log = sink.Events.Should().ContainSingle().Subject;
            var sb = new StringBuilder();
            log.Format(sb, formatter);

            var formatted = sb.ToString();
            formatted.Should()
                     .NotBeNullOrWhiteSpace()
                     .And.Contain(state)
                     .And.Contain(eventId.ToString())
                     .And.Contain($"This is {state}");
        }

        [Fact]
        public void ShouldNotEmitLogWhenNotEnabled()
        {
            var sink = new TestSink();
            var formatter = LogSettingsHelper.GetFormatter();
            var logger = new DirectSubmissionLogger(
                name: "TestLogger",
                scopeProvider: new NullScopeProvider(),
                sink: sink,
                logFormatter: formatter,
                minimumLogLevel: DirectSubmissionLogLevel.Information);

            var level = (int)DirectSubmissionLogLevel.Debug;
            logger.IsEnabled(level).Should().BeFalse();
            logger.Log(
                logLevel: level,
                eventId: 123,
                state: "someValue",
                exception: null,
                (s, ex) => $"This is {s}");

            sink.Events.Should().BeEmpty();
        }

        private static DirectSubmissionLogger GetLogger(TestSink sink)
        {
            var settings = LogSettingsHelper.GetValidSettings();
            return new DirectSubmissionLogger(
                name: "TestLogger",
                scopeProvider: new NullScopeProvider(),
                sink: sink,
                logFormatter: LogSettingsHelper.GetFormatter(),
                minimumLogLevel: settings.MinimumLevel);
        }

        internal class TestSink : IDirectSubmissionLogSink
        {
            public ConcurrentQueue<DirectSubmissionLogEvent> Events { get; } = new();

            public void EnqueueLog(DirectSubmissionLogEvent logEvent)
            {
                Events.Enqueue(logEvent);
            }

            public void Start()
            {
            }

            public Task FlushAsync()
            {
                return Task.CompletedTask;
            }

            public Task DisposeAsync()
            {
                return Task.CompletedTask;
            }
        }

        internal class NullScopeProvider : IExternalScopeProvider
        {
            public IDisposable Push(object state)
            {
                return NullScope.Instance;
            }

            public void ForEachScope<TState>(Action<object, TState> callback, TState state)
            {
            }
        }
    }
}

#endif
