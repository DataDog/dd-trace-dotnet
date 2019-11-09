using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    /// <summary>
    /// Subscriber to ScopeManager events that sets/unsets correlation identifier
    /// properties in the application's logging context.
    /// </summary>
    internal class LibLogScopeEventSubscriber : IDisposable
    {
        private readonly IScopeManager _scopeManager;
        private readonly ILogProvider _logProvider;

        // Each mapped context sets a key-value pair into the logging context
        // Disposing the returned context unsets the key-value pair
        // Keep a stack to retain the history of our correlation identifier properties
        // (the stack is particularly important for Serilog, see below).
        //
        // IMPORTANT: Serilog -- The logging contexts (throughout the entire application)
        //            are maintained in a stack, as opposed to a map, and must be closed
        //            in reverse-order of opening. When operating on the stack-based model,
        //            it is only valid to add the properties once unset them once.
        private readonly ConcurrentStack<IDisposable> _contextDisposalStack = new ConcurrentStack<IDisposable>();

        public LibLogScopeEventSubscriber(IScopeManager scopeManager)
        {
            _scopeManager = scopeManager;

            _logProvider = LogProvider.CurrentLogProvider ?? LogProvider.ResolveLogProvider();
            if (_logProvider is SerilogLogProvider)
            {
                // Do not set default values for Serilog because it is unsafe to set
                // except at the application startup, but this would require auto-instrumentation
                _scopeManager.SpanOpened += StackOnSpanOpened;
                _scopeManager.SpanClosed += StackOnSpanClosed;
            }
            else
            {
                _scopeManager.SpanActivated += MapOnSpanActivated;
                _scopeManager.TraceEnded += MapOnTraceEnded;
            }
        }

        public void StackOnSpanOpened(object sender, SpanEventArgs spanEventArgs)
        {
            SetCorrelationIdentifierContext(spanEventArgs.Span.TraceId, spanEventArgs.Span.SpanId);
        }

        public void StackOnSpanClosed(object sender, SpanEventArgs spanEventArgs)
        {
            RemoveLastCorrelationIdentifierContext();
        }

        public void MapOnSpanActivated(object sender, SpanEventArgs spanEventArgs)
        {
            RemoveAllCorrelationIdentifierContexts();
            SetCorrelationIdentifierContext(spanEventArgs.Span.TraceId, spanEventArgs.Span.SpanId);
        }

        public void MapOnTraceEnded(object sender, SpanEventArgs spanEventArgs)
        {
            RemoveAllCorrelationIdentifierContexts();
            SetDefaultValues();
        }

        public void Dispose()
        {
            if (_logProvider is SerilogLogProvider)
            {
                _scopeManager.SpanOpened -= StackOnSpanOpened;
                _scopeManager.SpanClosed -= StackOnSpanClosed;
            }
            else
            {
                _scopeManager.SpanActivated -= MapOnSpanActivated;
                _scopeManager.TraceEnded -= MapOnTraceEnded;
            }

            RemoveAllCorrelationIdentifierContexts();
        }

        private void SetDefaultValues()
        {
            SetCorrelationIdentifierContext(0, 0);
        }

        private void RemoveLastCorrelationIdentifierContext()
        {
            // TODO: Debug logs
            for (int i = 0; i < 2; i++)
            {
                if (_contextDisposalStack.TryPop(out IDisposable ctxDisposable))
                {
                    ctxDisposable.Dispose();
                }
                else
                {
                    // There is nothing left to pop so do nothing.
                    // Though we are in a strange circumstance if we did not balance
                    // the stack properly
                    Debug.Fail($"{nameof(RemoveLastCorrelationIdentifierContext)} call failed. Too few items on the context stack.");
                }
            }
        }

        private void RemoveAllCorrelationIdentifierContexts()
        {
            // TODO: Debug logs
            while (_contextDisposalStack.TryPop(out IDisposable ctxDisposable))
            {
                ctxDisposable.Dispose();
            }
        }

        private void SetCorrelationIdentifierContext(ulong traceId, ulong spanId)
        {
            // TODO: Debug logs
            _contextDisposalStack.Push(
                LogProvider.OpenMappedContext(
                    CorrelationIdentifier.TraceIdKey, traceId.ToString(), destructure: false));
            _contextDisposalStack.Push(
                LogProvider.OpenMappedContext(
                    CorrelationIdentifier.SpanIdKey, spanId.ToString(), destructure: false));
        }
    }
}
