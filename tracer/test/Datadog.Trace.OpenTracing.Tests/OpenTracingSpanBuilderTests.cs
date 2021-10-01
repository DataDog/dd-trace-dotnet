// <copyright file="OpenTracingSpanBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using NUnit.Framework;
using OpenTracing;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class OpenTracingSpanBuilderTests
    {
        private static readonly string DefaultServiceName = $"{nameof(OpenTracingSpanBuilderTests)}";

        private OpenTracingTracer _tracer;

        [SetUp]
        public void Before()
        {
            var settings = new TracerSettings
            {
                ServiceName = DefaultServiceName
            };

            var writerMock = new Mock<IAgentWriter>(MockBehavior.Strict);
            var samplerMock = new Mock<ISampler>();

            var datadogTracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
            _tracer = new OpenTracingTracer(datadogTracer);
        }

        [Test]
        public void Start_NoServiceName_DefaultServiceNameIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null).Start();

            Assert.AreEqual(DefaultServiceName, span.DDSpan.ServiceName);
        }

        [Test]
        public void Start_NoParentProvided_RootSpan()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null).Start();
            var ddSpanContext = span.Context.Context as SpanContext;

            Assert.NotNull(ddSpanContext);
            Assert.Null(ddSpanContext.ParentId);
            Assert.AreNotEqual(0, ddSpanContext.SpanId);
            Assert.AreNotEqual(0, ddSpanContext.TraceId);
        }

        [Test]
        public void Start_AsChildOfSpan_ChildReferencesParent()
        {
            var root = (OpenTracingSpan)_tracer.BuildSpan(null).Start();
            var child = (OpenTracingSpan)_tracer.BuildSpan(null)
                                                .AsChildOf(root)
                                                .Start();

            Assert.Null(root.DDSpan.Context.ParentId);
            Assert.AreNotEqual(0, root.DDSpan.Context.SpanId);
            Assert.AreNotEqual(0, root.DDSpan.Context.TraceId);
            Assert.AreEqual(root.DDSpan.Context.SpanId, child.DDSpan.Context.ParentId);
            Assert.AreEqual(root.DDSpan.Context.TraceId, child.DDSpan.Context.TraceId);
            Assert.AreNotEqual(0, child.DDSpan.Context.SpanId);
        }

        [Test]
        public void Start_AsChildOfSpanContext_ChildReferencesParent()
        {
            var root = (OpenTracingSpan)_tracer.BuildSpan(null).Start();
            var child = (OpenTracingSpan)_tracer.BuildSpan(null)
                                                .AsChildOf(root.Context)
                                                .Start();

            Assert.Null(root.DDSpan.Context.ParentId);
            Assert.AreNotEqual(0, root.DDSpan.Context.SpanId);
            Assert.AreNotEqual(0, root.DDSpan.Context.TraceId);
            Assert.AreEqual(root.DDSpan.Context.SpanId, child.DDSpan.Context.ParentId);
            Assert.AreEqual(root.DDSpan.Context.TraceId, child.DDSpan.Context.TraceId);
            Assert.AreNotEqual(0, child.DDSpan.Context.SpanId);
        }

        [Test]
        public void Start_ReferenceAsChildOf_ChildReferencesParent()
        {
            var root = (OpenTracingSpan)_tracer.BuildSpan(null).Start();
            var child = (OpenTracingSpan)_tracer.BuildSpan(null)
                                                .AddReference(References.ChildOf, root.Context)
                                                .Start();

            Assert.Null(root.DDSpan.Context.ParentId);
            Assert.AreNotEqual(0, root.DDSpan.Context.SpanId);
            Assert.AreNotEqual(0, root.DDSpan.Context.TraceId);
            Assert.AreEqual(root.DDSpan.Context.SpanId, child.DDSpan.Context.ParentId);
            Assert.AreEqual(root.DDSpan.Context.TraceId, child.DDSpan.Context.TraceId);
            Assert.AreNotEqual(0, child.DDSpan.Context.SpanId);
        }

        [Test]
        public void Start_WithTags_TagsAreProperlySet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                                               .WithTag("StringKey", "What's tracing")
                                               .WithTag("IntKey", 42)
                                               .WithTag("DoubleKey", 1.618)
                                               .WithTag("BoolKey", true)
                                               .Start();

            Assert.AreEqual("What's tracing", span.DDSpan.GetTag("StringKey"));
            Assert.AreEqual("42", span.DDSpan.GetTag("IntKey"));
            Assert.AreEqual("1.618", span.DDSpan.GetTag("DoubleKey"));
            Assert.AreEqual("True", span.DDSpan.GetTag("BoolKey"));
        }

        [Test]
        public void Start_SettingService_ServiceIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                                               .WithTag(DatadogTags.ServiceName, "MyService")
                                               .Start();

            Assert.AreEqual("MyService", span.DDSpan.ServiceName);
        }

        [Test]
        public void Start_SettingServiceInParent_ImplicitChildInheritServiceName()
        {
            IScope root = _tracer.BuildSpan(null)
                                 .WithTag(DatadogTags.ServiceName, "MyService")
                                 .StartActive(finishSpanOnDispose: true);
            IScope child = _tracer.BuildSpan(null)
                                  .StartActive(finishSpanOnDispose: true);

            Assert.AreEqual("MyService", ((OpenTracingSpan)root.Span).Span.ServiceName);
            Assert.AreEqual("MyService", ((OpenTracingSpan)child.Span).Span.ServiceName);
        }

        [Test]
        public void Start_SettingServiceInParent_ExplicitChildInheritServiceName()
        {
            IScope root = _tracer.BuildSpan(null)
                                 .WithTag(DatadogTags.ServiceName, "MyService")
                                 .StartActive(finishSpanOnDispose: true);
            IScope child = _tracer.BuildSpan(null)
                                  .AsChildOf(root.Span)
                                  .StartActive(finishSpanOnDispose: true);

            Assert.AreEqual("MyService", ((OpenTracingSpan)root.Span).Span.ServiceName);
            Assert.AreEqual("MyService", ((OpenTracingSpan)child.Span).Span.ServiceName);
        }

        [Test]
        public void Start_SettingServiceInParent_NotChildDontInheritServiceName()
        {
            ISpan span1 = _tracer.BuildSpan(null)
                                 .WithTag(DatadogTags.ServiceName, "MyService")
                                 .Start();
            IScope root = _tracer.BuildSpan(null)
                                 .StartActive(finishSpanOnDispose: true);

            Assert.AreEqual("MyService", ((OpenTracingSpan)span1).Span.ServiceName);
            Assert.AreEqual("OpenTracingSpanBuilderTests", ((OpenTracingSpan)root.Span).Span.ServiceName);
        }

        [Test]
        public void Start_SettingServiceInChild_ServiceNameOverrideParent()
        {
            var root = (OpenTracingSpan)_tracer.BuildSpan(null)
                                               .WithTag(DatadogTags.ServiceName, "MyService")
                                               .Start();
            var child = (OpenTracingSpan)_tracer.BuildSpan(null)
                                                .WithTag(DatadogTags.ServiceName, "AnotherService")
                                                .Start();

            Assert.AreEqual("MyService", root.DDSpan.ServiceName);
            Assert.AreEqual("AnotherService", child.DDSpan.ServiceName);
        }

        [Test]
        public void Start_SettingResource_ResourceIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                                               .WithTag("resource.name", "MyResource")
                                               .Start();

            Assert.AreEqual("MyResource", span.DDSpan.ResourceName);
        }

        [Test]
        public void Start_SettingType_TypeIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                                               .WithTag("span.type", "web")
                                               .Start();

            Assert.AreEqual("web", span.DDSpan.Type);
        }

        [Test]
        public void Start_SettingError_ErrorIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                                               .WithTag(global::OpenTracing.Tag.Tags.Error.Key, true)
                                               .Start();

            Assert.True(span.DDSpan.Error);
        }

        [Test]
        public void Start_WithStartTimeStamp_TimeStampProperlySet()
        {
            var startTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                                               .WithStartTimestamp(startTime)
                                               .Start();

            Assert.AreEqual(startTime, span.DDSpan.StartTime);
        }

        [Test]
        public void Start_SetOperationName_OperationNameProperlySet()
        {
            var spanBuilder = new OpenTracingSpanBuilder(_tracer, "Op1");

            var span = (OpenTracingSpan)spanBuilder.Start();

            Assert.AreEqual("Op1", span.DDSpan.OperationName);
        }
    }
}
