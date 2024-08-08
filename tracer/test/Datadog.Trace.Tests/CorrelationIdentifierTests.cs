// <copyright file="CorrelationIdentifierTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Internal;
using Datadog.Trace.Internal.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class CorrelationIdentifierTests
    {
        [Fact]
        public void TraceIdSpanId_MatchActiveSpan()
        {
            using (var parentScope = InternalTracer.Instance.StartActive("parent"))
            {
                using (var childScope = InternalTracer.Instance.StartActive("child"))
                {
                    Assert.Equal<ulong>(childScope.Span.SpanId, InternalCorrelationIdentifier.SpanId);
                    Assert.Equal<ulong>(childScope.Span.TraceId, InternalCorrelationIdentifier.TraceId);
                }
            }
        }

        [Fact(Skip = "This test is not compatible with the xUnit integration. Neither TraceId or SpanId are Zero.")]
        public void TraceIdSpanId_ZeroOutsideActiveSpan()
        {
            using (var parentScope = InternalTracer.Instance.StartActive("parent"))
            using (var childScope = InternalTracer.Instance.StartActive("child"))
            {
                // Do nothing
            }

            Assert.Equal<ulong>(0, InternalCorrelationIdentifier.SpanId);
            Assert.Equal<ulong>(0, InternalCorrelationIdentifier.TraceId);
        }

        [Fact]
        public void ServiceIdentifiers_MatchTracerInstanceSettings()
        {
            const string service = "unit-test";
            const string version = "1.0.0";
            const string env = "staging";

            var settings = new InternalTracerSettings()
            {
                ServiceName = service,
                ServiceVersion = version,
                Environment = env
            };
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            InternalTracer.UnsafeSetTracerInstance(tracer);

            using (var parentScope = InternalTracer.Instance.StartActive("parent"))
            using (var childScope = InternalTracer.Instance.StartActive("child"))
            {
                Assert.Equal(service, InternalCorrelationIdentifier.Service);
                Assert.Equal(version, InternalCorrelationIdentifier.Version);
                Assert.Equal(env, InternalCorrelationIdentifier.Env);
            }

            Assert.Equal(service, InternalCorrelationIdentifier.Service);
            Assert.Equal(version, InternalCorrelationIdentifier.Version);
            Assert.Equal(env, InternalCorrelationIdentifier.Env);
        }

        [Fact]
        public void VersionAndEnv_EmptyStringIfUnset()
        {
            var settings = new InternalTracerSettings();
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            InternalTracer.UnsafeSetTracerInstance(tracer);

            using (var parentScope = InternalTracer.Instance.StartActive("parent"))
            using (var childScope = InternalTracer.Instance.StartActive("child"))
            {
                Assert.Equal(string.Empty, InternalCorrelationIdentifier.Version);
                Assert.Equal(string.Empty, InternalCorrelationIdentifier.Env);
            }

            Assert.Equal(string.Empty, InternalCorrelationIdentifier.Version);
            Assert.Equal(string.Empty, InternalCorrelationIdentifier.Env);
        }

        [Fact]
        public void Service_DefaultServiceNameIfUnset()
        {
            var settings = new InternalTracerSettings();
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            InternalTracer.UnsafeSetTracerInstance(tracer);

            using (var parentScope = InternalTracer.Instance.StartActive("parent"))
            using (var childScope = InternalTracer.Instance.StartActive("child"))
            {
                Assert.Equal(InternalCorrelationIdentifier.Service, InternalTracer.Instance.DefaultServiceName);
            }

            Assert.Equal(InternalCorrelationIdentifier.Service, InternalTracer.Instance.DefaultServiceName);
        }
    }
}
