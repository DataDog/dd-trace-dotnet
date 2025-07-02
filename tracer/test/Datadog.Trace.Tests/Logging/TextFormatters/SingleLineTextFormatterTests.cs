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
        var result = sw.ToString().TrimEnd(); // remove trailing newline

        // assert that SimpleTextFormatter uses UTC timestamps and emits a single line.
        result.Should().Be($"[2000-01-01 04:00:00.000 +00:00 | DD_TRACE_DOTNET {TracerConstants.ThreePartVersion} | INF] This is log number 1")
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
            throw new Exception("Exception message.");
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
        var result = sw.ToString().TrimEnd(); // remove trailing newline

        // assert that SimpleTextFormatter uses UTC timestamps and emits a single line.
        result.Should().StartWith($"[2000-01-01 04:00:00.000 +00:00 | DD_TRACE_DOTNET {TracerConstants.ThreePartVersion} | INF] This is log number 1 | System.Exception: Exception message.\\n")
              .And.NotContain(Environment.NewLine);
    }
}
