// <copyright file="SingleLineTextFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Logging.Internal.TextFormatters;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Parsing;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.TextFormatters;

public class SingleLineTextFormatterTests
{
    [Fact]
    public void Format_LogEvent_Without_Exception()
    {
        // intentionally using a non-UTC timestamp to ensure the formatter writes UTC timestamps
        var timestamp = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(-4));

        // create a log event
        var messageTemplate = new MessageTemplateParser().Parse("This is log number {LogNumber}");
        var properties = new[] { new LogEventProperty("LogNumber", new ScalarValue(1)) };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, exception: null, messageTemplate, properties);

        // format the log event
        var sw = new StringWriter();
        new SingleLineTextFormatter().Format(logEvent, sw);
        var output = sw.ToString();
        var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        // assert that SimpleTextFormatter uses UTC timestamps and emits a single line.
        lines.Should().ContainSingle()
             .Which.Should().Be($"[2000-01-01 04:00:00.000 +00:00 | DD_TRACE_DOTNET {TracerConstants.ThreePartVersion} | INF] This is log number 1")
             .And.NotContain(Environment.NewLine);
    }

    [Fact]
    public void Format_LogEvent_With_Exception()
    {
        // intentionally using a non-UTC timestamp to ensure the formatter writes UTC timestamps
        var timestamp = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        Exception exception;

        try
        {
            // throw from another method to force a longer stack trace
            ThrowException();
        }
        catch (Exception e)
        {
            exception = e;
        }

        // create a log event
        var messageTemplate = new MessageTemplateParser().Parse("This is log number {LogNumber}");
        var properties = new[] { new LogEventProperty("LogNumber", new ScalarValue(1)) };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, exception, messageTemplate, properties);

        // format the log event
        var sw = new StringWriter();
        new SingleLineTextFormatter().Format(logEvent, sw);

        var output = sw.ToString();
        var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        lines.Should().ContainSingle()
             // Expected format: [yyyy-MM-dd HH:mm:ss.fff +00:00 | DD_TRACE_DOTNET X.Y.Z | LVL] Message | ExceptionType: ExceptionMessage\nStackTrace1\nStackTrace2\n...
             .Which.Should().MatchRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| DD_TRACE_DOTNET \d+\.\d+\.\d+ \| \w{3}\] (.+) | (.+)(\\n.+)+$")
             // assert UTC timestamps
             .And.StartWith($"[2000-01-01 04:00:00.000 +00:00 | DD_TRACE_DOTNET {TracerConstants.ThreePartVersion} | INF] This is log number 1 | System.Exception: Exception message.\\n")
             // and single line
             .And.NotContain(Environment.NewLine);

        return;

        static void ThrowException()
        {
            throw new Exception("Exception message.");
        }
    }

    [Fact]
    public void ToSingleLineString()
    {
        var exception = new Exception("This is a test exception.\nIt has multiple lines.\r\nIt has multiple lines.\nIt has multiple lines.");

        var singleLineString = SingleLineTextFormatter.ToSingleLineString(exception);

        singleLineString.Should().Be(@"System.Exception: This is a test exception.\nIt has multiple lines.\nIt has multiple lines.\nIt has multiple lines.");
    }
}
