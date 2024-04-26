// <copyright file="OriginTagSendTraces.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class OriginTagSendTraces
    {
        private readonly Tracer _tracer;
        private readonly MockApi _testApi;

        public OriginTagSendTraces()
        {
            var settings = new TracerSettings();
            _testApi = new MockApi();
            var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: null, automaticFlush: false);
            _tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
        }

        [SkippableFact]
        public void NormalSpan()
        {
            using (_ = _tracer.StartActive("root"))
            {
                using (_ = _tracer.StartActive("child"))
                {
                }
            }

            _tracer.FlushAsync();
            var traceChunks = _testApi.Wait();

            traceChunks.SelectMany(s => s)
                       .Should()
                       .OnlyContain(s => !s.Tags.ContainsKey("_dd.origin"));
        }

        [SkippableFact]
        public void NormalOriginSpan()
        {
            const string originValue = "test-origin";

            using (var scope = (Scope)_tracer.StartActive("root"))
            {
                scope.Span.Context.TraceContext.Origin = originValue;

                using (_ = _tracer.StartActive("child"))
                {
                }
            }

            _tracer.FlushAsync();
            var traceChunks = _testApi.Wait();

            traceChunks.SelectMany(s => s)
                       .Should()
                       .OnlyContain(s => s.Tags["_dd.origin"] == originValue);
        }
    }
}
