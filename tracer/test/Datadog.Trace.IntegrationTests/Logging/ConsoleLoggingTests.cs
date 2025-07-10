// <copyright file="ConsoleLoggingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.Internal.Sinks;
using Datadog.Trace.Logging.Internal.TextFormatters;
using Datadog.Trace.Vendors.Serilog;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Logging;

// These tests combine the use of SingleLineTextFormatter and AsyncTextWriterSink,
// but they still use StringWriter instead of Console.Out to capture
// the output to avoid interacting with the actual console output during tests.
public class ConsoleLoggingTests
{
    [Fact]
    public void Logger_Writes_Correctly_Formatted_Messages()
    {
        var sw = new StringWriter();

        var sink = new AsyncTextWriterSink(
            formatter: new SingleLineTextFormatter(),
            textWriter: sw,
            queueLimit: DatadogLoggingFactory.DefaultConsoleQueueLimit);

        var logger = new LoggerConfiguration()
                     .MinimumLevel.Debug()
                     .WriteTo.Sink(sink)
                     .CreateLogger();

        logger.Information("First message");
        logger.Warning("Second message with {param}", 42);
        logger.Error("Third message");
        logger.Dispose();

        var output = sw.ToString();
        var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(3);

        // Expected format: [yyyy-MM-dd HH:mm:ss.fff +00:00 | DD_TRACE_DOTNET X.Y.Z | LVL] Message
        lines[0].Should().MatchRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| DD_TRACE_DOTNET \d+\.\d+\.\d+ \| \w{3}\] First message$");
        lines[1].Should().MatchRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| DD_TRACE_DOTNET \d+\.\d+\.\d+ \| \w{3}\] Second message with 42$");
        lines[2].Should().MatchRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| DD_TRACE_DOTNET \d+\.\d+\.\d+ \| \w{3}\] Third message$");
    }

    [Fact]
    public void Logger_Writes_Correctly_Formatted_Exceptions()
    {
        var sw = new StringWriter();

        var sink = new AsyncTextWriterSink(
            formatter: new SingleLineTextFormatter(),
            textWriter: sw,
            queueLimit: DatadogLoggingFactory.DefaultConsoleQueueLimit);

        var logger = new LoggerConfiguration()
                     .MinimumLevel.Debug()
                     .WriteTo.Sink(sink)
                     .CreateLogger();

        try
        {
            ThrowException(new InvalidOperationException("First error"));
        }
        catch (Exception e)
        {
            logger.Error(e, "Error one");
        }

        try
        {
            ThrowException(new ArgumentException("Second error"));
        }
        catch (Exception e)
        {
            logger.Error(e, "Error two");
        }

        try
        {
            ThrowException(new Exception("Third error"));
        }
        catch (Exception e)
        {
            logger.Error(e, "Error three");
        }

        logger.Dispose();
        var output = sw.ToString();
        var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(3);

        // Expected format: [yyyy-MM-dd HH:mm:ss.fff +00:00 | DD_TRACE_DOTNET X.Y.Z | LVL] Message | ExceptionType: ExceptionMessage\nStackTrace1\nStackTrace2\n...
        lines[0].Should().MatchRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| DD_TRACE_DOTNET \d+\.\d+\.\d+ \| \w{3}\] Error one \| System\.InvalidOperationException: First error(\\n.+)+$");
        lines[1].Should().MatchRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| DD_TRACE_DOTNET \d+\.\d+\.\d+ \| \w{3}\] Error two \| System\.ArgumentException: Second error(\\n.+)+$");
        lines[2].Should().MatchRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| DD_TRACE_DOTNET \d+\.\d+\.\d+ \| \w{3}\] Error three \| System\.Exception: Third error(\\n.+)+$");
    }

    private static void ThrowException(Exception ex)
    {
        // This method is used to throw an exception with a longer stack trace
        // to ensure the logger captures the full stack trace in the output.
        throw ex;
    }
}
