// <copyright file="DirectSubmissionSerilogSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Vendors.Serilog.Capturing;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Serilog
{
    public class DirectSubmissionSerilogSinkTests
    {
        [Fact]
        public void LoggerEnqueuesLogMessage()
        {
            var sink = new TestSink();
            var serilogSink = new DirectSubmissionSerilogSink(sink, DirectSubmissionLogLevel.Debug);

            var level = LogEventLevel.Error;
            GetSerilogMessageProcessor()
               .Process("This is {SomeValue}", new object[] { "someValue" }, out var parsedTemplate, out var boundProperties);
            var logEvent = new LogEvent(DateTimeOffset.Now, level, exception: null, parsedTemplate, boundProperties);

            var proxy = logEvent.DuckCast<ILogEvent>();
            serilogSink.Emit(proxy);

            sink.Events.Should().ContainSingle();
        }

        [Fact]
        public void LoggerCanRenderLogMessage()
        {
            var sink = new TestSink();
            var serilogSink = new DirectSubmissionSerilogSink(sink, DirectSubmissionLogLevel.Debug);

            var level = LogEventLevel.Error;
            var rawText = "someValue";
            GetSerilogMessageProcessor()
               .Process("This is {SomeValue}", new object[] { "someValue" }, out var parsedTemplate, out var boundProperties);
            var logEvent = new LogEvent(DateTimeOffset.Now, level, exception: null, parsedTemplate, boundProperties);

            var proxy = logEvent.DuckCast<ILogEvent>();
            serilogSink.Emit(proxy);

            var log = sink.Events.Should().ContainSingle().Subject;
            var sb = new StringBuilder();
            var formatter = new LogFormatter(SettingsHelper.GetValidSettings());
            log.Format(sb, formatter);

            var formatted = sb.ToString();
            formatted.Should()
                     .NotBeNullOrWhiteSpace()
                     .And.Contain(rawText)
                     .And.Contain($@"This is \""{rawText}\""");
        }

        [Fact]
        public void ShouldNotEmitLogWhenNotEnabled()
        {
            var sink = new TestSink();
            var serilogSink = new DirectSubmissionSerilogSink(sink, DirectSubmissionLogLevel.Warning);

            var level = LogEventLevel.Information;
            GetSerilogMessageProcessor()
               .Process("This is {SomeValue}", new object[] { "someValue" }, out var parsedTemplate, out var boundProperties);
            var logEvent = new LogEvent(DateTimeOffset.Now, level, exception: null, parsedTemplate, boundProperties);

            var proxy = logEvent.DuckCast<ILogEvent>();
            serilogSink.Emit(proxy);

            sink.Events.Should().BeEmpty();
        }

        private static MessageTemplateProcessor GetSerilogMessageProcessor()
        {
            // Use some "default" values
            return new MessageTemplateProcessor(
                new PropertyValueConverter(
                    maximumDestructuringDepth: 10,
                    maximumStringLength: int.MaxValue,
                    maximumCollectionCount: int.MaxValue,
                    additionalScalarTypes: Enumerable.Empty<Type>(),
                    additionalDestructuringPolicies: Enumerable.Empty<IDestructuringPolicy>(),
                    propagateExceptions: false));
        }

        internal class TestSink : IDatadogSink
        {
            public ConcurrentQueue<DatadogLogEvent> Events { get; } = new();

            public void Dispose()
            {
            }

            public void EnqueueLog(DatadogLogEvent logEvent)
            {
                Events.Enqueue(logEvent);
            }
        }
    }
}
