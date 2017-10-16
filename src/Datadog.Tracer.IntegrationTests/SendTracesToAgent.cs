using MsgPack;
using System;
using System.Linq;
using System.Net;
using Xunit;

namespace Datadog.Tracer.IntegrationTests
{
    public class SendTracesToAgent
    {
        private Tracer _tracer;
        private RecordHttpHandler _httpRecorder;

        public SendTracesToAgent()
        {
            _httpRecorder = new RecordHttpHandler();
            _tracer = TracerFactory.GetTracer(new Uri("http://localhost:8126"), null, null, _httpRecorder);
        }

        private void AssertSpanEqual(Span expected, MessagePackObject actual)
        {
            Assert.Equal(expected.Context.TraceId, actual.TraceId());
            Assert.Equal(expected.Context.SpanId, actual.SpanId());
            if (expected.Context.ParentId.HasValue)
            {
                Assert.Equal(expected.Context.ParentId, actual.ParentId());
            }
            Assert.Equal(expected.OperationName, actual.OperationName());
            Assert.Equal(expected.ResourceName, actual.ResourceName());
            Assert.Equal(expected.ServiceName, actual.ServiceName());
            Assert.Equal(expected.Type, actual.Type());
            Assert.Equal(expected.StartTime.ToUnixTimeNanoseconds(), actual.StartTime());
            Assert.Equal(expected.Duration.ToNanoseconds(), actual.Duration());
            if (expected.Error)
            {
                Assert.Equal("1", actual.Error());
            }
            if (expected.Tags != null)
            {
                Assert.Equal(expected.Tags, actual.Tags());
            }
        }

        [Fact]
        public async void MinimalSpan()
        {
            var span = (Span)_tracer.BuildSpan("Operation")
                .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(2);
            Assert.Equal(2, _httpRecorder.Requests.Count);
            Assert.Equal(2, _httpRecorder.Responses.Count);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            AssertSpanEqual(span, trace.Single());
        }

        [Fact]
        public async void CustomServiceName()
        {
            var span = (Span)_tracer.BuildSpan("Operation")
                .WithTag(Tags.ResourceName, "This is a resource")
                .WithTag(Tags.ServiceName, "Service1")
                .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(2);
            Assert.Equal(2, _httpRecorder.Requests.Count);
            Assert.Equal(2, _httpRecorder.Responses.Count);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            AssertSpanEqual(span, trace.Single());
        }

        [Fact]
        public async void Utf8Everywhere()
        {
            var span = (Span)_tracer.BuildSpan("Aᛗᚪᚾᚾᚪ")
                .WithTag(Tags.ResourceName, "η γλώσσα μου έδωσαν ελληνική")
                .WithTag(Tags.ServiceName, "На берегу пустынных волн")
                .WithTag("யாமறிந்த", "ნუთუ კვლა")
                .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(2);
            Assert.Equal(2, _httpRecorder.Requests.Count);
            Assert.Equal(2, _httpRecorder.Responses.Count);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            AssertSpanEqual(span, trace.Single());
        }

        [Fact]
        public void WithDefaultFactory()
        {
            // This test does not check anything it validates that this codepath runs without exceptions
            var tracer = TracerFactory.GetTracer();
            tracer.BuildSpan("Operation")
                .Start()
                .Finish();
        }
    }
}
