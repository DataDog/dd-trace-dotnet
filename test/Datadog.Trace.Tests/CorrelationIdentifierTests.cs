using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class CorrelationIdentifierTests
    {
        [Fact]
        public void TraceIdSpanId_MatchActiveSpan()
        {
            using (var parentScope = Tracer.Instance.StartActive("parent"))
            {
                using (var childScope = Tracer.Instance.StartActive("child"))
                {
                    Assert.Equal<ulong>(childScope.Span.SpanId, CorrelationIdentifier.SpanId);
                    Assert.Equal<ulong>(childScope.Span.TraceId, CorrelationIdentifier.TraceId);
                }
            }
        }

        [Fact]
        public void TraceIdSpanId_ZeroOutsideActiveSpan()
        {
            using (var parentScope = Tracer.Instance.StartActive("parent"))
            using (var childScope = Tracer.Instance.StartActive("child"))
            {
                // Do nothing
            }

            Assert.Equal<ulong>(0, CorrelationIdentifier.SpanId);
            Assert.Equal<ulong>(0, CorrelationIdentifier.TraceId);
        }

        [Fact]
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
            Tracer.Instance = tracer;

            using (var parentScope = Tracer.Instance.StartActive("parent"))
            using (var childScope = Tracer.Instance.StartActive("child"))
            {
                Assert.Equal(service, CorrelationIdentifier.Service);
                Assert.Equal(version, CorrelationIdentifier.Version);
                Assert.Equal(env, CorrelationIdentifier.Env);
            }

            Assert.Equal(service, CorrelationIdentifier.Service);
            Assert.Equal(version, CorrelationIdentifier.Version);
            Assert.Equal(env, CorrelationIdentifier.Env);
        }

        [Fact]
        public void VersionAndEnv_EmptyStringIfUnset()
        {
            var settings = new TracerSettings();
            var tracer = new Tracer(settings);
            Tracer.Instance = tracer;

            using (var parentScope = Tracer.Instance.StartActive("parent"))
            using (var childScope = Tracer.Instance.StartActive("child"))
            {
                Assert.Equal(string.Empty, CorrelationIdentifier.Version);
                Assert.Equal(string.Empty, CorrelationIdentifier.Env);
            }

            Assert.Equal(string.Empty, CorrelationIdentifier.Version);
            Assert.Equal(string.Empty, CorrelationIdentifier.Env);
        }

        [Fact]
        public void Service_DefaultServiceNameIfUnset()
        {
            var settings = new TracerSettings();
            var tracer = new Tracer(settings);
            Tracer.Instance = tracer;

            using (var parentScope = Tracer.Instance.StartActive("parent"))
            using (var childScope = Tracer.Instance.StartActive("child"))
            {
                Assert.Equal(CorrelationIdentifier.Service, Tracer.Instance.DefaultServiceName);
            }

            Assert.Equal(CorrelationIdentifier.Service, Tracer.Instance.DefaultServiceName);
        }
    }
}
