// <copyright file="OriginTagSendTraces.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class OriginTagSendTraces
    {
        private readonly MockApi _testApi;

        public OriginTagSendTraces()
        {
            _testApi = new MockApi();
        }

        [SkippableFact]
        public async Task NormalSpan()
        {
            await using var tracer = GetTracer();
            using (_ = tracer.StartActive("root"))
            {
                using (_ = tracer.StartActive("child"))
                {
                }
            }

            await tracer.FlushAsync();
            var traceChunks = _testApi.Wait();

            traceChunks.SelectMany(s => s)
                       .Should()
                       .OnlyContain(s => !s.Tags.ContainsKey("_dd.origin"));
        }

        [SkippableFact]
        public async Task NormalOriginSpan()
        {
            const string originValue = "test-origin";
            await using var tracer = GetTracer();
            using (var scope = (Scope)tracer.StartActive("root"))
            {
                scope.Span.Context.TraceContext.Origin = originValue;

                using (_ = tracer.StartActive("child"))
                {
                }
            }

            await tracer.FlushAsync();
            var traceChunks = _testApi.Wait();

            traceChunks.SelectMany(s => s)
                       .Should()
                       .OnlyContain(s => s.Tags["_dd.origin"] == originValue);
        }

        private ScopedTracer GetTracer()
        {
            var settings = new TracerSettings();
            var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);
            return TracerHelper.Create(settings, agentWriter, null, null, null);
        }
    }
}
