// <copyright file="SingleLineExceptionEnricherTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging.Internal.Enrichers;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.Enrichers;

public class SingleLineExceptionEnricherTests
{
    [Fact]
    public void SingleLineExceptionEnricher_AddProperty()
    {
        Exception exception;

        try
        {
            throw new Exception("Exception message.");
        }
        catch (Exception e)
        {
            exception = e;
        }

        var properties = Array.Empty<EventProperty>();
        var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, exception, MessageTemplate.Empty, properties);

        // enrich the log event
        new SingleLineExceptionEnricher().Enrich(logEvent, new PropertyFactory());

        // assert that the SingleLineException property is present and has the expected value
        logEvent.Properties.Should().ContainKey("SingleLineException")
                .WhoseValue.Should().BeOfType<ScalarValue>()
                .Which.Value.Should().BeOfType<string>()
                .Which.Should().NotContain(Environment.NewLine);
    }

    private class PropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
