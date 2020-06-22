using System;
using System.Linq;
using System.Net;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.HttpMessageHandlers;
using Xunit;

namespace Datadog.Trace.OpenTracing.IntegrationTests
{
    public class OpenTracingSendTracesToAgent
    {
        private readonly OpenTracingTracer _tracer;
        private readonly RecordHttpHandler _httpRecorder;

        public OpenTracingSendTracesToAgent()
        {
            var settings = new TracerSettings();

            var endpoint = new Uri("http://localhost:8126");
            _httpRecorder = new RecordHttpHandler();
            var api = new Api(endpoint, apiRequestFactory: null, statsd: null);
            var agentWriter = new AgentWriter(api, statsd: null);

            var tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
            _tracer = new OpenTracingTracer(tracer);
        }

        [Fact(Skip = "Run manually")]
        public async void MinimalSpan()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Operation")
                                               .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            Assert.Single(_httpRecorder.Requests);
            Assert.Single(_httpRecorder.Responses);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(span.DDSpan, trace.Single());
        }

        [Fact(Skip = "Run manually")]
        public async void CustomServiceName()
        {
            const string ServiceName = "MyService";

            var span = (OpenTracingSpan)_tracer.BuildSpan("Operation")
                                               .WithTag(DatadogTags.ResourceName, "This is a resource")
                                               .WithTag(DatadogTags.ServiceName, ServiceName)
                                               .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            Assert.Single(_httpRecorder.Requests);
            Assert.Single(_httpRecorder.Responses);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(span.DDSpan, trace.Single());
        }

        [Fact(Skip = "Run manually")]
        public async void Utf8Everywhere()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Aᛗᚪᚾᚾᚪ")
                                               .WithTag(DatadogTags.ResourceName, "η γλώσσα μου έδωσαν ελληνική")
                                               .WithTag(DatadogTags.ServiceName, "На берегу пустынных волн")
                                               .WithTag("யாமறிந்த", "ნუთუ კვლა")
                                               .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            Assert.Single(_httpRecorder.Requests);
            Assert.Single(_httpRecorder.Responses);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(span.DDSpan, trace.Single());
        }

        [Fact(Skip = "Run manually")]
        public void WithDefaultFactory()
        {
            // This test does not check anything it validates that this codepath runs without exceptions
            _tracer.BuildSpan("Operation")
                   .Start()
                   .Finish();
        }
    }
}
