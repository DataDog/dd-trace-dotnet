using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class NamedPipesApiRequest : IApiRequest
    {
        private readonly string _pipeName;
        private readonly TraceRequest _traceRequest;

        // `SpinLock` is a struct. A struct marked as `readonly` is copied each time a mutating function is called.
        // When calling `_lock.Enter` and `_lock.Exit()` the `SpinLock` instance is copied. Calling `_lock.Exit()` raises an
        // error as the instance does not hold the lock (System.Threading.SynchronizationLockException : The calling
        // thread does not hold the lock.)
        // For this reason, `_lock` is not marked as `readonly`
        private SpinLock _lock = new SpinLock(enableThreadOwnerTracking: true);

        public NamedPipesApiRequest(string pipeName)
        {
            _pipeName = pipeName;
            _traceRequest = new TraceRequest();
        }

        public void AddHeader(string name, string value)
        {
            _traceRequest.Headers.Add(name, value);
        }

        public async Task<IApiResponse> PostAsync(Span[][] traces, FormatterResolverWrapper formatterResolver)
        {
            using (var namedPipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                _traceRequest.Traces = traces;
                await CachedSerializer.Instance.SerializeAsync(namedPipe, _traceRequest, formatterResolver);
                // await namedPipe.FlushAsync();
                // TODO: Request response
                return new NamedPipesResponse() { Content = "{}", StatusCode = 200, ContentLength = 2 };
            }
        }
    }
}
