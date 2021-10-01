// <copyright file="CorrelationIdentifierTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.Tests
{
    public class CorrelationIdentifierTests : TracerInstanceTestsBase
    {
        [Test]
        public void TraceIdSpanId_MatchActiveSpan()
        {
            using (var parentScope = Tracer.Instance.StartActive("parent"))
            {
                using (var childScope = Tracer.Instance.StartActive("child"))
                {
                    Assert.AreEqual(childScope.Span.SpanId, CorrelationIdentifier.SpanId);
                    Assert.AreEqual(childScope.Span.TraceId, CorrelationIdentifier.TraceId);
                }
            }
        }

        [Test]
        [Ignore("This test is not compatible with the test integration. Neither TraceId or SpanId are Zero.")]
        public void TraceIdSpanId_ZeroOutsideActiveSpan()
        {
            using (var parentScope = Tracer.Instance.StartActive("parent"))
            using (var childScope = Tracer.Instance.StartActive("child"))
            {
                // Do nothing
            }

            Assert.AreEqual(0, CorrelationIdentifier.SpanId);
            Assert.AreEqual(0, CorrelationIdentifier.TraceId);
        }

        [Test]
        public void ServiceIdentifiers_MatchTracerInstanceSettings()
        {
            const string service = "unit-test";
            const string version = "1.0.0";
            const string env = "staging";

            var settings = new TracerSettings()
            {
                ServiceName = service,
                ServiceVersion = version,
                Environment = env
            };
            var tracer = new Tracer(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            using (var parentScope = Tracer.Instance.StartActive("parent"))
            using (var childScope = Tracer.Instance.StartActive("child"))
            {
                Assert.AreEqual(service, CorrelationIdentifier.Service);
                Assert.AreEqual(version, CorrelationIdentifier.Version);
                Assert.AreEqual(env, CorrelationIdentifier.Env);
            }

            Assert.AreEqual(service, CorrelationIdentifier.Service);
            Assert.AreEqual(version, CorrelationIdentifier.Version);
            Assert.AreEqual(env, CorrelationIdentifier.Env);
        }

        [Test]
        public void VersionAndEnv_EmptyStringIfUnset()
        {
            var settings = new TracerSettings();
            var tracer = new Tracer(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            using (var parentScope = Tracer.Instance.StartActive("parent"))
            using (var childScope = Tracer.Instance.StartActive("child"))
            {
                Assert.AreEqual(string.Empty, CorrelationIdentifier.Version);
                Assert.AreEqual(string.Empty, CorrelationIdentifier.Env);
            }

            Assert.AreEqual(string.Empty, CorrelationIdentifier.Version);
            Assert.AreEqual(string.Empty, CorrelationIdentifier.Env);
        }

        [Test]
        public void Service_DefaultServiceNameIfUnset()
        {
            var settings = new TracerSettings();
            var tracer = new Tracer(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            using (var parentScope = Tracer.Instance.StartActive("parent"))
            using (var childScope = Tracer.Instance.StartActive("child"))
            {
                Assert.AreEqual(CorrelationIdentifier.Service, Tracer.Instance.DefaultServiceName);
            }

            Assert.AreEqual(CorrelationIdentifier.Service, Tracer.Instance.DefaultServiceName);
        }
    }
}
