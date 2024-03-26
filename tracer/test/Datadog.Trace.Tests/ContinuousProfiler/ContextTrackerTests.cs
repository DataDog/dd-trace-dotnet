// <copyright file="ContextTrackerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ContinuousProfiler
{
    [Collection(nameof(ContextTrackerTests))]
    [CollectionDefinition(nameof(ContextTrackerTests), DisableParallelization = true)]
    public class ContextTrackerTests
    {
        [Fact]
        public void SetEndpointOnRootWebSpans()
        {
            const string expectedEndpoint = "My endpoint";

            var contextTracker = new Mock<IContextTracker>();

            var invocations = new List<(ulong SpanId, string Endpoint)>();

            contextTracker.Setup(c => c.SetEndpoint(It.IsAny<ulong>(), It.IsAny<string>()))
                          .Callback<ulong, string>((i, e) => invocations.Add((i, e)));

            try
            {
                Profiler.SetInstanceForTests(new Profiler(contextTracker.Object, new Mock<IProfilerStatus>().Object));

                var tracer = CreateTracer();

                ulong expectedSpanId;

                using (var rootWebScope = tracer.StartActive("Root"))
                {
                    expectedSpanId = rootWebScope.Span.SpanId;
                    rootWebScope.Span.Type = SpanTypes.Web;
                    rootWebScope.Span.ResourceName = "Wrong endpoint";

                    // The resource name of this scope shouldn't be propagated because it's not root
                    using (tracer.StartActive("child"))
                    {
                    }

                    // Only the latest value of the resource name should be propagated
                    rootWebScope.Span.ResourceName = expectedEndpoint;
                }

                // The resource name of this scope shouldn't be propagated because it's not web
                using (var rootOtherScope = tracer.StartActive("Root2"))
                {
                    rootOtherScope.Span.Type = SpanTypes.Http;
                    rootOtherScope.Span.ResourceName = "Wrong endpoint";
                }

                invocations.Should().BeEquivalentTo(new[] { (expectedSpanId, expectedEndpoint) });
            }
            finally
            {
                Profiler.SetInstanceForTests(null);
            }
        }

        private static Tracer CreateTracer()
        {
            return new Tracer(new TracerSettings(), Mock.Of<IAgentWriter>(), Mock.Of<ITraceSampler>(), null, null);
        }
    }
}
