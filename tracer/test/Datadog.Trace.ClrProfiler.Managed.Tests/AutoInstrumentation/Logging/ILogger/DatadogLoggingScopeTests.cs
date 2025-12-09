// <copyright file="DatadogLoggingScopeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.ILogger
{
    public class DatadogLoggingScopeTests
    {
        [Fact]
        public async Task OutputsJsonFormattedStringWhenNoActiveTrace()
        {
            var settings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.ServiceName, "TestService" },
                { ConfigurationKeys.ServiceVersion, "1.2.3" },
                { ConfigurationKeys.Environment, "test" },
            });

            await using var tracer = TracerHelper.Create(settings,  new Mock<IAgentWriter>().Object);

            var scope = new DatadogLoggingScope(tracer, tracer.CurrentTraceSettings.Settings);

            var actual = scope.ToString();

            actual.Should().Be(@"dd_service:""TestService"", dd_env:""test"", dd_version:""1.2.3""");
        }

        [Fact]
        public async Task OutputsJsonFormattedStringWhenActiveTrace()
        {
            var settings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.ServiceName, "TestService" },
                { ConfigurationKeys.ServiceVersion, "1.2.3" },
                { ConfigurationKeys.Environment, "test" },
            });

            await using var tracer = TracerHelper.Create(settings,  new Mock<IAgentWriter>().Object);
            using var spanScope = tracer.StartActive("test");
            var scope = new DatadogLoggingScope(tracer, tracer.CurrentTraceSettings.Settings);

            var actual = scope.ToString();

            var expected = @$"dd_service:""TestService"", dd_env:""test"", dd_version:""1.2.3"", dd_trace_id:""{((Span)spanScope.Span).TraceId128}"", dd_span_id:""{spanScope.Span.SpanId}""";
            actual.Should().Be(expected);
        }
    }
}
