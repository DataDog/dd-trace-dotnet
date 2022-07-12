// <copyright file="TraceTagTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class TraceTagTests
    {
        private readonly Tracer _tracer;
        private readonly TestApi _testApi;

        public TraceTagTests()
        {
            _testApi = new TestApi();

            var settings = new TracerSettings();
            var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: null);
            _tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
        }

        [Fact]
        public void SetTraceTagOnRootSpan()
        {
            using (var rootScope = _tracer.StartActive("root"))
            {
                using (var childScope = _tracer.StartActive("child1"))
                {
                    // add a trace tag using the first child span
                    ((SpanContext)childScope.Span.Context).TraceContext.Tags.SetTag("key1", "value1");
                }

                // add a trace tag using the root span
                ((SpanContext)rootScope.Span.Context).TraceContext.Tags.SetTag("key2", "value2");

                using (var childScope = _tracer.StartActive("child2"))
                {
                    // add a trace tag using the second child span
                    ((SpanContext)childScope.Span.Context).TraceContext.Tags.SetTag("key3", "value3");
                }
            }

            var traces = _testApi.Wait();
            traces.Should().HaveCount(1); // 1 trace...
            traces[0].Should().HaveCount(3); // ...with 3 spans

            // assert that root span has the trace tags
            var rootSpan = traces[0].SingleOrDefault(s => s.ParentId is null or 0)!;
            rootSpan.Should().NotBeNull();

            rootSpan.Tags.Should().Contain("key1", "value1");
            rootSpan.Tags.Should().Contain("key2", "value2");
            rootSpan.Tags.Should().Contain("key3", "value3");

            // assert that child spans do not have the trace tags
            var childSpans = traces[0].Where(s => s.ParentId is not null and not 0);

            foreach (var childSpan in childSpans)
            {
                childSpan.Tags.Should().NotContainKey("key1");
                childSpan.Tags.Should().NotContainKey("key2");
                childSpan.Tags.Should().NotContainKey("key3");
            }
        }
    }
}
