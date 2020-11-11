using System;
using System.Threading;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class NamedPipeRequestFactory : IApiRequestFactory
    {
        private static readonly int TimeoutMs = 2500;
        private readonly string _pipeName;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public NamedPipeRequestFactory(string pipeName)
        {
            _pipeName = pipeName;
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new NamedPipesApiRequest(_pipeName, TimeoutMs, _cancellationTokenSource.Token);
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
