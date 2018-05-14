using System;
using System.Linq;
using System.Net;
using Datadog.Trace.Agent;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.OpenTracing.IntegrationTests
{
    public class OpenTracingSendTracesToAgent
    {
        private OpenTracingTracer _tracer;
        private RecordHttpHandler _httpRecorder;

        public OpenTracingSendTracesToAgent()
        {
            var uri = new Uri("http://localhost:8126");
            _httpRecorder = new RecordHttpHandler();
            var api = new Api(uri, _httpRecorder);
            var agentWriter = new AgentWriter(api);
            var datadogTracer = new Tracer(agentWriter, $"{nameof(OpenTracingSendTracesToAgent)}");
            var scopeManager = new global::OpenTracing.Util.AsyncLocalScopeManager();
            _tracer = new OpenTracingTracer(datadogTracer, scopeManager);
        }

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void WithDefaultFactory()
        {
            // This test does not check anything it validates that this codepath runs without exceptions
            _tracer.BuildSpan("Operation")
                .Start()
                .Finish();
        }
    }
}
