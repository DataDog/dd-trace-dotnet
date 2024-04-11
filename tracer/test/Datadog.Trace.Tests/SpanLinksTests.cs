// <copyright file="SpanLinksTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Castle.Core.Internal;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class SpanLinksTests
    {
        private readonly Tracer _tracer;

        public SpanLinksTests()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();

            _tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        }

        [Fact]
        public void AddLink_InCloseSpan()
        {
            var parentScope = (Scope)_tracer.StartActive("Parent");
            var childScope = (Scope)_tracer.StartActive("Child");

            var parentSpan = parentScope.Span;
            var childSpan = childScope.Span;

            childSpan.Finish();
            Assert.Null(childSpan.AddSpanLink(parentSpan));
        }

        [Fact]
        public void AddAttribute_ToLink_InCloseSpan()
        {
            var parentScope = (Scope)_tracer.StartActive("Parent");
            var childScope = (Scope)_tracer.StartActive("Child");

            var parentSpan = parentScope.Span;
            var childSpan = childScope.Span;
            var spanLink = childSpan.AddSpanLink(parentSpan);
            childSpan.Finish();
            spanLink.AddAttribute("should", "return null");
            Assert.Null(spanLink.Attributes);
        }
    }
}
