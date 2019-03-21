using System;
using System.Linq;
using System.Net;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.HttpMessageHandlers;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class SendTracesToAgent
    {
        private Tracer _tracer;
        private RecordHttpHandler _httpRecorder;

        public SendTracesToAgent()
        {
            _httpRecorder = new RecordHttpHandler();
            _tracer = Tracer.Create(new Uri("http://localhost:8126"), null, _httpRecorder);
        }

        [Fact]
        public async void MinimalSpan()
        {
            var scope = _tracer.StartActive("Operation");
            scope.Dispose();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            Assert.Single(_httpRecorder.Requests);
            Assert.Single(_httpRecorder.Responses);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(scope.Span, trace.Single());
        }

        [Fact]
        public async void CustomServiceName()
        {
            const string ServiceName = "MyService";
            _httpRecorder = new RecordHttpHandler();
            _tracer = Tracer.Create(new Uri("http://localhost:8126"), null, _httpRecorder);

            var scope = _tracer.StartActive("Operation", serviceName: ServiceName);
            scope.Span.ResourceName = "This is a resource";
            scope.Dispose();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            Assert.Single(_httpRecorder.Requests);
            Assert.Single(_httpRecorder.Responses);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(scope.Span, trace.Single());
        }

        [Fact]
        public async void Utf8Everywhere()
        {
            var scope = _tracer.StartActive("Aᛗᚪᚾᚾᚪ", serviceName: "На берегу пустынных волн");
            scope.Span.ResourceName = "η γλώσσα μου έδωσαν ελληνική";
            scope.Span.SetTag("யாமறிந்த", "ნუთუ კვლა");
            scope.Dispose();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(1);
            Assert.Single(_httpRecorder.Requests);
            Assert.Single(_httpRecorder.Responses);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(scope.Span, trace.Single());
        }

        [Fact]
        public async void SubmitsOutOfOrderSpans()
        {
            var scope1 = _tracer.StartActive("op1");
            var scope2 = _tracer.StartActive("op2");
            scope1.Close();
            scope2.Close();

            await _httpRecorder.WaitForCompletion(1);
            Assert.Single(_httpRecorder.Requests);
            Assert.Single(_httpRecorder.Responses);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            MsgPackHelpers.AssertSpanEqual(scope1.Span, trace[0].AsList()[0]);
            MsgPackHelpers.AssertSpanEqual(scope2.Span, trace[0].AsList()[1]);
        }

        [Fact]
        public void WithDefaultFactory()
        {
            // This test does not check anything it validates that this codepath runs without exceptions
            var tracer = Tracer.Create();
            tracer.StartActive("Operation")
                .Dispose();
        }

        [Fact]
        public void WithGlobalTracer()
        {
            // This test does not check anything it validates that this codepath runs without exceptions
            Tracer.Instance.StartActive("Operation")
                .Dispose();
        }
    }
}
