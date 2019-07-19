using System;
using System.Collections.Concurrent;

namespace Datadog.Trace.Logging
{
    internal class LibLogScopeEventSubscriber : IDisposable
    {
        // Each mapped context sets a key-value pair into the logging context
        // Disposing the context unsets the key-value pair
        //
        // IMPORTANT: The contexts must be closed in reverse-order of opening,
        //            so by convention always open the TraceId context before
        //            opening the SpanId context, and close the contexts in
        //            the opposite order
        private readonly ConcurrentStack<IDisposable> _contextDisposalStack = new ConcurrentStack<IDisposable>();

        private LibLogScopeEventSubscriber()
        {
            DatadogScopeStack.SpanActivated += OnSpanActivated;
            DatadogScopeStack.TraceEnded += OnTraceEnded;
            SetDefaultValues();
        }

        public static LibLogScopeEventSubscriber Initialize()
        {
            return new LibLogScopeEventSubscriber();
        }

        public void OnSpanActivated(object sender, SpanEventArgs spanEventArgs)
        {
            DisposeAll();
            SetLoggingValues(spanEventArgs.Span.TraceId, spanEventArgs.Span.SpanId);
        }

        public void OnTraceEnded(object sender, SpanEventArgs spanEventArgs)
        {
            DisposeAll();
            SetDefaultValues();
        }

        public void Dispose()
        {
            DatadogScopeStack.SpanActivated -= OnSpanActivated;
            DatadogScopeStack.TraceEnded -= OnTraceEnded;
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
