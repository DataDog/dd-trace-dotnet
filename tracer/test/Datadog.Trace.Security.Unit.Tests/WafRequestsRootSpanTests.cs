// <copyright file="WafRequestsRootSpanTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

using static Datadog.Trace.Security.Unit.Tests.SecurityReporterWafTelemetryTests;

namespace Datadog.Trace.Security.Unit.Tests;

// replaces the global metrics collector
[Collection(nameof(SecuritySequentialTests))]
public class WafRequestsRootSpanTests
{
    [Theory]
    [InlineData(SpanTypes.Web)]
    [InlineData(SpanTypes.Serverless)] // Azure Functions retypes the HTTP root span
    public async Task GivenARequestWithAWafRun_WhenItsRootSpanCloses_ThenOneWafRequestsPointIsReported(string spanType)
    {
        await using var tracer = TracerHelper.Create(new());
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);

        try
        {
            using (var scope = (Scope)tracer.StartActive("request"))
            {
                scope.Span.Type = spanType;
                SecurityReporter.RecordWafTelemetry(CreateResult(ruleTriggered: true), isRasp: false, scope.Span.Context.TraceContext.AppSecRequestContext, collector);

                using (var child = (Scope)tracer.StartActive("child"))
                {
                    child.Span.Type = spanType;
                }
            }
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(previousMetrics);
        }

        await collector.DisposeAsync();
        var points = collector.GetMetrics().Metrics?.Where(m => m.Metric == "waf.requests").Sum(m => m.Points.Sum(p => (int)p.Value));
        points.Should().Be(1);
    }
}
