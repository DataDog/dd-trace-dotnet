using MsgPack;
using System;
using System.Collections.Generic;
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

            var serviceInfo = _httpRecorder.Services.Single().ServiceInfos().Single();
            Assert.Equal("Datadog.Tracer", serviceInfo.ServiceName);
            Assert.Equal(Constants.UnkownApp, serviceInfo.App);
            Assert.Equal(Constants.WebAppType, serviceInfo.AppType);
        }

        [Fact]
        public async void CustomServiceName()
        {
            const string App = "MyApp";
            const string AppType = "db";
            const string ServiceName = "MyService";
            var serviceList = new List<ServiceInfo> { new ServiceInfo { App = App, AppType = AppType, ServiceName = ServiceName } };
            _httpRecorder = new RecordHttpHandler();
            _tracer = TracerFactory.GetTracer(new Uri("http://localhost:8126"), serviceList, null, _httpRecorder);

            var span = (Span)_tracer.BuildSpan("Operation")
                .WithTag(Tags.ResourceName, "This is a resource")
                .WithTag(Tags.ServiceName, ServiceName)
                .Start();
            span.Finish();

            // Check that the HTTP calls went as expected
            await _httpRecorder.WaitForCompletion(3);
            Assert.Equal(3, _httpRecorder.Requests.Count);
            Assert.Equal(3, _httpRecorder.Responses.Count);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

            var trace = _httpRecorder.Traces.Single();
            AssertSpanEqual(span, trace.Single());

            var serviceInfos = _httpRecorder.Services.Select(x => x.ServiceInfos().Single()).ToList();
            Assert.Equal(2, serviceInfos.Count);
            var serviceInfo = serviceInfos.Single(x => x.ServiceName == ServiceName);
            Assert.Equal(ServiceName, serviceInfo.ServiceName);
            Assert.Equal(App, serviceInfo.App);
            Assert.Equal(AppType, serviceInfo.AppType);
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
