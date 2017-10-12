using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.Tracer.IntegrationTests
{
    public class RecordHttpHandler : DelegatingHandler
    {
        private Object _lock = new Object();
        private int _count = 0;
        private int _target = 0;
        private TaskCompletionSource<bool> _tcs;

        public List<HttpRequestMessage> Requests { get; set; }

        public List<HttpResponseMessage> Responses { get; set;  }

        public RecordHttpHandler()
        {
            InnerHandler = new HttpClientHandler();
            Requests = new List<HttpRequestMessage>();
            Responses = new List<HttpResponseMessage>();
        }

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response =  await base.SendAsync(request, cancellationToken);
            lock(_lock)
            {
                Requests.Add(request);
                Responses.Add(response);
                _count++;
                if(_tcs != null && _count >= _target)
                {
                    _tcs.SetResult(true);
                    _tcs = null;
                }
            }
            return response;
        }

        public Task<bool> WaitForCompletion(int target, TimeSpan? timeout = null)
        {
            timeout = timeout ?? TimeSpan.FromSeconds(3);
            lock (_lock)
            {
                if (_count >= target)
                {
                    return Task.FromResult(true);
                }
                if (_tcs == null)
                {
                    _target = target;
                    _tcs = new TaskCompletionSource<bool>();
                    var cancelationSource = new CancellationTokenSource(timeout.Value);
                    cancelationSource.Token.Register(() => _tcs?.SetException(new TimeoutException()));
                    return _tcs.Task;
                }
                else
                {
                    throw new InvalidOperationException("This method should not be called twice on the same instance");
                }
            }
        }
    }

    public class SendTracesToAgent
    {
        private Tracer _tracer;
        private RecordHttpHandler _httpRecorder;
        private List<List<Span>> _traces;
        private List<ServiceInfo> _services;

        public SendTracesToAgent()
        {
            _httpRecorder = new RecordHttpHandler();
            _tracer = TracerFactory.GetTracer(new Uri("http://localhost:8126"), null, null, _httpRecorder);
        }

        [Fact]
        public async void MinimalSpan()
        {
            _tracer.BuildSpan("Operation")
                .Start()
                .Finish();

            // Check that the tracer sends the proper traces
            var trace = _traces.Single();
            Assert.Equal(1, trace.Count);
            Assert.Equal(1, _services.Count);

            // Check that the HTTP call went as expected
            await _httpRecorder.WaitForCompletion(2);
            Assert.Equal(2, _httpRecorder.Requests.Count);
            Assert.Equal(2, _httpRecorder.Responses.Count);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        }

        [Fact]
        public async void CustomServiceName()
        {
            _tracer.BuildSpan("Operation")
                .WithTag(Tags.ResourceName, "This is a resource")
                .WithTag(Tags.ServiceName, "Service1")
                .Start()
                .Finish();

            // Check that the tracer sends the proper traces
            var trace = _traces.Single();
            Assert.Equal(1, trace.Count);
            Assert.Equal(1, _services.Count);

            // Check that the HTTP call went as expected
            await _httpRecorder.WaitForCompletion(2);
            Assert.Equal(2, _httpRecorder.Requests.Count);
            Assert.Equal(2, _httpRecorder.Responses.Count);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        }

        [Fact]
        public async void Utf8Everywhere()
        {
            _tracer.BuildSpan("Aᛗᚪᚾᚾᚪ")
                .WithTag(Tags.ResourceName, "η γλώσσα μου έδωσαν ελληνική")
                .WithTag(Tags.ServiceName, "На берегу пустынных волн")
                .WithTag("யாமறிந்த", "ნუთუ კვლა")
                .Start()
                .Finish();

            // Check that the tracer sends the proper traces
            var trace = _traces.Single();
            Assert.Equal(1, trace.Count);
            Assert.Equal(1, _services.Count);

            // Check that the HTTP call went as expected
            await _httpRecorder.WaitForCompletion(2);
            Assert.Equal(2, _httpRecorder.Requests.Count);
            Assert.Equal(2, _httpRecorder.Responses.Count);
            Assert.All(_httpRecorder.Responses, (x) => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        }

        [Fact]
        public void WithDefaultFactory()
        {
            var tracer = TracerFactory.GetTracer();
            tracer.BuildSpan("Operation")
                .Start()
                .Finish();

        }
    }
}
