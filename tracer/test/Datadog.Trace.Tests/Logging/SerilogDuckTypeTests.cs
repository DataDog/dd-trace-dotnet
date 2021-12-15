// <copyright file="SerilogDuckTypeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.LogsInjection;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Serilog.Events;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    public class SerilogDuckTypeTests
    {
        [Fact]
        public void CanCreateLogPropertyValueWithHelper()
        {
            var value = "test_value";
            var logPropertyValue = SerilogLogPropertyHelper<Serilog.Core.Logger>.CreateScalarValue(value);

            logPropertyValue.Should()
                            .NotBeNull()
                            .And.BeOfType<ScalarValue>()
                            .Subject.Value.Should()
                            .BeOfType<string>()
                            .And
                            .Be(value);
        }

        [Fact]
        public void CanDuckTypeLogEvent()
        {
            var logEvent = new LogEvent(
                DateTimeOffset.Now,
                LogEventLevel.Error,
                exception: null,
                MessageTemplate.Empty,
                properties: Enumerable.Empty<LogEventProperty>());
            var proxy = logEvent.DuckCast<LogEventProxy>();

            proxy.Properties.Should().NotBeNull();
            proxy.Properties.Should().BeEmpty();

            logEvent.AddPropertyIfAbsent(new LogEventProperty("key", new ScalarValue("value")));

            // String values are rendered with quotes unless the ':l formatter is used.
            // Since we can't enforce a format with object.ToString(), assert the result with surrounding quotes
            proxy.Properties["key"].ToString().Should().BeEquivalentTo("\"value\"");
        }
    }
}
