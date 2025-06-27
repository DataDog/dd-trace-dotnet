// <copyright file="UtcTimestampEnricherTests.cs" company="Datadog">
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

public class UtcTimestampEnricherTests
{
    [Fact]
    public void UtcTimestampEnricher_AddProperty()
    {
        // intentionally using a non-UTC timestamp to ensure the enricher converts it to UTC
        var timestamp = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(-4));

        var properties = Array.Empty<EventProperty>();
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, exception: null, MessageTemplate.Empty, properties);

        // enrich the log event
        new UtcTimestampEnricher().Enrich(logEvent, new PropertyFactory());

        // assert that the UtcTimestamp property is present and has the expected value
        logEvent.Properties.Should().ContainKey("UtcTimestamp")
                .WhoseValue.Should().BeOfType<ScalarValue>()
                .Which.Value.Should().BeOfType<DateTimeOffset>()
                .Which.Should().Be(timestamp.ToUniversalTime());
    }

    private class PropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
