// <copyright file="AsyncTextWriterSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.Internal.Sinks;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting.Display;
using Datadog.Trace.Vendors.Serilog.Parsing;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging;

public class AsyncTextWriterSinkTests
{
    [Fact]
    public void Emit_Writes_To_TextWriter()
    {
        var sw = new StringWriter();

        // create sink
        var sink = new AsyncTextWriterSink(
            formatter: new MessageTemplateTextFormatter("{Message}{NewLine}"), // output the message only
            textWriter: sw,
            queueLimit: DatadogLoggingFactory.DefaultConsoleQueueLimit);

        // create a log event
        var timestamp = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var messageTemplate = new MessageTemplateParser().Parse("This is log number {LogNumber}");
        var properties = new[] { new LogEventProperty("LogNumber", new ScalarValue(1)) };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, exception: null, messageTemplate, properties);

        // emit the log event and dispose the sink
        sink.Emit(logEvent);
        sink.Dispose();

        // assert that the sink wrote the log event to the StringWriter
        var result = sw.ToString().TrimEnd(); // remove trailing newline
        result.Should().Be("This is log number 1")
              .And.NotContain(Environment.NewLine);
    }

    [Fact]
    public void Logger_Can_Write_To_Sink()
    {
        // this is more of an integration test, ensuring that the logger
        // can write to the sink, instead using the sink directly.
        var sw = new StringWriter();

        var sink = new AsyncTextWriterSink(
            formatter: new MessageTemplateTextFormatter("{Message}{NewLine}"), // output the message only
            textWriter: sw,
            queueLimit: DatadogLoggingFactory.DefaultConsoleQueueLimit);

        var logger = new LoggerConfiguration()
                     .MinimumLevel.Debug()
                     .WriteTo.Sink(sink)
                     .CreateLogger();

        for (var i = 0; i < 100; i++)
        {
            logger.Information("This is log number {LogNumber}", i);
        }

        logger.Dispose();

        var strings = sw.ToString().Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        strings.Should()
               .HaveCount(100)
               .And.OnlyHaveUniqueItems()
               .And.OnlyContain(x => x.Contains("This is log number"));
    }
}
