// <copyright file="CorrelationIdentifierTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
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
            using (var parentScope = TracerInternal.Instance.StartActive("parent"))
            {
                using (var childScope = TracerInternal.Instance.StartActive("child"))
                {
                    Assert.Equal<ulong>(childScope.Span.SpanId, CorrelationIdentifierInternal.SpanId);
                    Assert.Equal<ulong>(childScope.Span.TraceId, CorrelationIdentifierInternal.TraceId);
                }
            }
        }

        [Fact(Skip = "This test is not compatible with the xUnit integration. Neither TraceId or SpanId are Zero.")]
        public void TraceIdSpanId_ZeroOutsideActiveSpan()
        {
            using (var parentScope = TracerInternal.Instance.StartActive("parent"))
            using (var childScope = TracerInternal.Instance.StartActive("child"))
            {
                // Do nothing
            }

            Assert.Equal<ulong>(0, CorrelationIdentifierInternal.SpanId);
            Assert.Equal<ulong>(0, CorrelationIdentifierInternal.TraceId);
        }

        [Fact]
        public void ServiceIdentifiers_MatchTracerInstanceSettings()
        {
            const string service = "unit-test";
            const string version = "1.0.0";
            const string env = "staging";

            var settings = new TracerSettingsInternal()
            {
                ServiceName = service,
                ServiceVersion = version,
                Environment = env
            };
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            TracerInternal.UnsafeSetTracerInstance(tracer);

            using (var parentScope = TracerInternal.Instance.StartActive("parent"))
            using (var childScope = TracerInternal.Instance.StartActive("child"))
            {
                Assert.Equal(service, CorrelationIdentifierInternal.Service);
                Assert.Equal(version, CorrelationIdentifierInternal.Version);
                Assert.Equal(env, CorrelationIdentifierInternal.Env);
            }

            Assert.Equal(service, CorrelationIdentifierInternal.Service);
            Assert.Equal(version, CorrelationIdentifierInternal.Version);
            Assert.Equal(env, CorrelationIdentifierInternal.Env);
        }

        [Fact]
        public void VersionAndEnv_EmptyStringIfUnset()
        {
            var settings = new TracerSettingsInternal();
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            TracerInternal.UnsafeSetTracerInstance(tracer);

            using (var parentScope = TracerInternal.Instance.StartActive("parent"))
            using (var childScope = TracerInternal.Instance.StartActive("child"))
            {
                Assert.Equal(string.Empty, CorrelationIdentifierInternal.Version);
                Assert.Equal(string.Empty, CorrelationIdentifierInternal.Env);
            }

            Assert.Equal(string.Empty, CorrelationIdentifierInternal.Version);
            Assert.Equal(string.Empty, CorrelationIdentifierInternal.Env);
        }

        [Fact]
        public void Service_DefaultServiceNameIfUnset()
        {
            var settings = new TracerSettingsInternal();
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            TracerInternal.UnsafeSetTracerInstance(tracer);

            using (var parentScope = TracerInternal.Instance.StartActive("parent"))
            using (var childScope = TracerInternal.Instance.StartActive("child"))
            {
                Assert.Equal(CorrelationIdentifierInternal.Service, TracerInternal.Instance.DefaultServiceName);
            }

            Assert.Equal(CorrelationIdentifierInternal.Service, TracerInternal.Instance.DefaultServiceName);
        }
    }
}
