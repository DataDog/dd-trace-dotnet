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
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(LibLogScopeEventSubscriber));
        private readonly IScopeManager _scopeManager;
        private readonly string _defaultServiceName;
        private readonly string _version;
        private readonly ILogProvider _logProvider;

        // Each mapped context sets a key-value pair into the logging context
        // Disposing the returned context unsets the key-value pair
        // Keep a stack to retain the history of our correlation identifier properties
        // (the stack is particularly important for Serilog, see below).
        //
        // IMPORTANT: Serilog -- The logging contexts (throughout the entire application)
        //            are maintained in a stack, as opposed to a map, and must be closed
        //            in reverse-order of opening. When operating on the stack-based model,
        //            it is only valid to add the properties once and unset them once.
        private readonly ConcurrentStack<IDisposable> _contextDisposalStack = new ConcurrentStack<IDisposable>();

        private bool _safeToAddToMdc = true;

        // IMPORTANT: For all logging frameworks, do not set any default values for
        //            "dd.trace_id" and "dd.span_id" when initializing the subscriber
        //            because the Tracer may be initialized at a time when it is not safe
        //            to add properties logging context of the underlying logging framework.
        //
        //            Failure to abide by this can cause a SerializationException when
        //            control is passed from one AppDomain to another where the originating
        //            AppDomain used a logging framework that stored logging context properties
        //            inside the System.Runtime.Remoting.Messaging.CallContext structure
        //            but the target AppDomain is unable to de-serialize the object --
        //            this can easily happen if the target AppDomain cannot find/load the
        //            logging framework assemblies.
        public LibLogScopeEventSubscriber(IScopeManager scopeManager, string defaultServiceName, string version)
        {
            _scopeManager = scopeManager;
            _defaultServiceName = defaultServiceName;
            _version = version;

            try
            {
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
            catch (Exception ex)
            {
                Log.Error(ex, "Could not successfully start the LibLogScopeEventSubscriber. There was an issue resolving the application logger.");
            }
        }

        public void StackOnSpanOpened(object sender, SpanEventArgs spanEventArgs)
        {
            SetCorrelationIdentifierContext(spanEventArgs.Span.ServiceName, _version, spanEventArgs.Span.TraceId, spanEventArgs.Span.SpanId);
        }

        public void StackOnSpanClosed(object sender, SpanEventArgs spanEventArgs)
        {
            RemoveLastCorrelationIdentifierContext();
        }

        public void MapOnSpanActivated(object sender, SpanEventArgs spanEventArgs)
        {
            RemoveAllCorrelationIdentifierContexts();
            SetCorrelationIdentifierContext(spanEventArgs.Span.ServiceName, _version, spanEventArgs.Span.TraceId, spanEventArgs.Span.SpanId);
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
            SetCorrelationIdentifierContext(_defaultServiceName, _version, 0, 0);
        }

        private void RemoveLastCorrelationIdentifierContext()
        {
            // TODO: Debug logs
            for (int i = 0; i < 4; i++)
            {
                if (_contextDisposalStack.TryPop(out var ctxDisposable))
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

        private void SetCorrelationIdentifierContext(string service, string version, ulong traceId, ulong spanId)
        {
            if (!_safeToAddToMdc)
            {
                return;
            }

            try
            {
                // TODO: Debug logs
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.ServiceKey, service, destructure: false));
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.VersionKey, version, destructure: false));
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.TraceIdKey, traceId.ToString(), destructure: false));
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.SpanIdKey, spanId.ToString(), destructure: false));
            }
            catch (Exception)
            {
                _safeToAddToMdc = false;
                RemoveAllCorrelationIdentifierContexts();
            }
        }
    }
}
