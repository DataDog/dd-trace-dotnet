using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Logging.LogProviders;
using Datadog.Trace.Util;

namespace Datadog.Trace.Logging
{
    /// <summary>
    /// Subscriber to ScopeManager events that sets/unsets correlation identifier
    /// properties in the application's logging context.
    /// </summary>
    internal class LibLogScopeEventSubscriber : IDisposable
    {
        private const int _numPropertiesSetOnSpanEvent = 5;
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(LibLogScopeEventSubscriber));
        private static readonly string NamedSlotName = "Datadog_IISPreInitStart";

        private static bool _executingIISPreStartInit = false;

        private readonly IScopeManager _scopeManager;
        private readonly string _defaultServiceName;
        private readonly string _version;
        private readonly string _env;
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

#if NETFRAMEWORK
        static LibLogScopeEventSubscriber()
        {
            RefreshIISPreAppState(traceId: null);
        }
#endif

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
        public LibLogScopeEventSubscriber(IScopeManager scopeManager, string defaultServiceName, string version, string env)
        {
            _scopeManager = scopeManager;
            _defaultServiceName = defaultServiceName;
            _version = version;
            _env = env;

#if NETFRAMEWORK
            if (_executingIISPreStartInit)
            {
                _scopeManager.TraceStarted += OnTraceStarted_RefreshIISState;
                RefreshIISPreAppState(traceId: null);
            }
#endif

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
                Log.SafeLogError(ex, "Could not successfully start the LibLogScopeEventSubscriber. There was an issue resolving the application logger.");
            }
        }

#if NETFRAMEWORK
        public void OnTraceStarted_RefreshIISState(object sender, SpanEventArgs spanEventArgs)
        {
            // If we were previously executing in the IIS PreApp Code, refresh this evaluation
            // If not, do not waste cycles
            if (_executingIISPreStartInit)
            {
                RefreshIISPreAppState(traceId: spanEventArgs.Span.TraceId);
            }
        }
#endif

        public void StackOnSpanOpened(object sender, SpanEventArgs spanEventArgs)
        {
            if (!_executingIISPreStartInit)
            {
                SetSerilogCompatibleLogContext(spanEventArgs.Span.TraceId, spanEventArgs.Span.SpanId);
            }
        }

        public void StackOnSpanClosed(object sender, SpanEventArgs spanEventArgs)
        {
            if (!_executingIISPreStartInit)
            {
                RemoveLastCorrelationIdentifierContext();
            }
        }

        public void MapOnSpanActivated(object sender, SpanEventArgs spanEventArgs)
        {
            if (!_executingIISPreStartInit)
            {
                RemoveAllCorrelationIdentifierContexts();
                SetLogContext(spanEventArgs.Span.TraceId, spanEventArgs.Span.SpanId);
            }
        }

        public void MapOnTraceEnded(object sender, SpanEventArgs spanEventArgs)
        {
            if (!_executingIISPreStartInit)
            {
                RemoveAllCorrelationIdentifierContexts();
                SetDefaultValues();
            }
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
            SetLogContext(0, 0);
        }

        private void RemoveLastCorrelationIdentifierContext()
        {
            // TODO: Debug logs
            for (int i = 0; i < _numPropertiesSetOnSpanEvent; i++)
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

        private void SetLogContext(ulong traceId, ulong spanId)
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
                        CorrelationIdentifier.ServiceKey, _defaultServiceName, destructure: false));
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.VersionKey, _version, destructure: false));
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.EnvKey, _env, destructure: false));
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

        private void SetSerilogCompatibleLogContext(ulong traceId, ulong spanId)
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
                        CorrelationIdentifier.SerilogServiceKey, _defaultServiceName, destructure: false));
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.SerilogVersionKey, _version, destructure: false));
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.SerilogEnvKey, _env, destructure: false));
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.SerilogTraceIdKey, traceId.ToString(), destructure: false));
                _contextDisposalStack.Push(
                    LogProvider.OpenMappedContext(
                        CorrelationIdentifier.SerilogSpanIdKey, spanId.ToString(), destructure: false));
            }
            catch (Exception)
            {
                _safeToAddToMdc = false;
                RemoveAllCorrelationIdentifierContexts();
            }
        }

#if NETFRAMEWORK
#pragma warning disable SA1202 // Elements must be ordered by access
#pragma warning disable SA1204 // Static elements must appear before instance elements
        private static void RefreshIISPreAppState(ulong? traceId)
        {
            Debug.Assert(!_executingIISPreStartInit, $"{nameof(_executingIISPreStartInit)} should always be false when entering {nameof(RefreshIISPreAppState)}");

            object state = AppDomain.CurrentDomain.GetData(NamedSlotName);
            _executingIISPreStartInit = state is bool boolState && boolState;

            if (_executingIISPreStartInit && traceId != null)
            {
                Log.Warning("IIS is still initializing the application. Automatic logs injection will be disabled until the application begins processing incoming requests. Affected traceId={0}", traceId);
            }
        }
#endif
    }
}
