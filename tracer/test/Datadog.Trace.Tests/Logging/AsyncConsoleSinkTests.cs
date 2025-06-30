// <copyright file="AsyncConsoleSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.Internal.Sinks;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Formatting.Display;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging;

public class AsyncConsoleSinkTests
{
    [Fact]
    public void CanWriteToConsoleSink()
    {
        var sw = new StringWriter();

        var consoleSink = new AsyncConsoleSink(
            formatter: new MessageTemplateTextFormatter("{Message}{NewLine}"), // output the message only
            consoleWriter: sw,
            queueLimit: DatadogLoggingFactory.DefaultConsoleQueueLimit);

        var logger = new LoggerConfiguration()
                     .MinimumLevel.Debug()
                     .WriteTo.Sink(consoleSink)
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
