using MsgPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Tracer.IntegrationTests
{
    public class RecordHttpHandler : DelegatingHandler
    {
        private Object _lock = new Object();
        private int _count = 0;
        private int _target = 0;
        private TaskCompletionSource<bool> _tcs;

        public List<HttpRequestMessage> Requests { get; set; }
        public List<IList<MessagePackObject>> Traces => Requests
            .Where(x => x.RequestUri.ToString().Contains("/v0.3/traces"))
            .Select(x => Unpacking.UnpackObject(x.Content.ReadAsByteArrayAsync().Result).Value.AsList())
            .ToList();

        public List<HttpRequestMessage> Services => Requests.Where(x => x.RequestUri.ToString().Contains("/v0.3/services")).ToList();

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
}
