using System;
using System.Collections.Concurrent;

namespace Datadog.Trace.Logging
{
    internal class LibLogScopeEventSubscriber : IDisposable
    {
        private readonly IScopeManager _scopeManager;

        // Each mapped context sets a key-value pair into the logging context
        // Disposing the context unsets the key-value pair
        //
        // IMPORTANT: The contexts must be closed in reverse-order of opening,
        //            so by convention always open the TraceId context before
        //            opening the SpanId context, and close the contexts in
        //            the opposite order
        private readonly ConcurrentStack<IDisposable> _contextDisposalStack = new ConcurrentStack<IDisposable>();

        public LibLogScopeEventSubscriber(IScopeManager scopeManager)
        {
            _scopeManager = scopeManager;
            _scopeManager.SpanActivated += OnSpanActivated;
            _scopeManager.TraceEnded += OnTraceEnded;
            SetDefaultValues();
        }

        public void OnSpanActivated(object sender, SpanEventArgs spanEventArgs)
        {
            DisposeAll();
            SetLoggingValues(spanEventArgs.Span.TraceId, spanEventArgs.Span.SpanId);
        }

        public void OnTraceEnded(object sender, SpanEventArgs spanEventArgs)
        {
            SetDefaultValues();
        }

        public void Dispose()
        {
            _scopeManager.SpanActivated -= OnSpanActivated;
            _scopeManager.TraceEnded -= OnTraceEnded;
            DisposeAll();
        }

        private void SetDefaultValues()
        {
            SetLoggingValues(0, 0);
        }

        private void DisposeAll()
        {
            while (_contextDisposalStack.TryPop(out IDisposable ctxDisposable))
            {
                ctxDisposable.Dispose();
            }
        }

        private void SetLoggingValues(ulong traceId, ulong spanId)
        {
            _contextDisposalStack.Push(
                LogProvider.OpenMappedContext(
                    CorrelationIdentifier.TraceIdKey, traceId, destructure: false));
            _contextDisposalStack.Push(
                LogProvider.OpenMappedContext(
                    CorrelationIdentifier.SpanIdKey, spanId, destructure: false));
        }
    }
}
