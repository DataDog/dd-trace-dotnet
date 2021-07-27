// <copyright file="LibLogScopeEventSubscriber.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LibLogScopeEventSubscriber));

#if NETFRAMEWORK
        private static readonly string NamedSlotName = "Datadog_IISPreInitStart";
        private static bool _performAppDomainFlagChecks = false;
#endif

        private static bool _executingIISPreStartInit = false;

        private readonly Tracer _tracer;
        private readonly IScopeManager _scopeManager;
        private readonly string _defaultServiceName;
        private readonly string _version;
        private readonly string _env;
        private readonly ILogProvider _logProvider;
        private readonly ILogEnricher _logEnricher;

        private readonly AsyncLocalCompat<IDisposable> _currentEnricher = new AsyncLocalCompat<IDisposable>();

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

        static LibLogScopeEventSubscriber()
        {
#if NETFRAMEWORK
            // Check if IIS automatic instrumentation has set the AppDomain property to indicate the PreStartInit state
            // If the property is not set, we must rely on a different method of determining the state
            object state = AppDomain.CurrentDomain.GetData(NamedSlotName);
            if (state is bool boolState)
            {
                _performAppDomainFlagChecks = true;
                _executingIISPreStartInit = boolState;
            }
            else
            {
                _performAppDomainFlagChecks = false;
                _executingIISPreStartInit = true;

                try
                {
                    string processName = ProcessHelpers.GetCurrentProcessName();

                    if (!processName.Equals("w3wp", StringComparison.OrdinalIgnoreCase) &&
                        !processName.Equals("iisexpress", StringComparison.OrdinalIgnoreCase))
                    {
                        // IIS is not running so we do not anticipate issues with IIS PreStartInit code execution
                        _executingIISPreStartInit = false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error obtaining the process name for quickly validating IIS PreStartInit condition.");
                }
            }

            if (_executingIISPreStartInit)
            {
                Log.Warning("Automatic logs injection detected that IIS is still initializating. The {Source} will be checked at the start of each trace to only enable automatic logs injection when IIS is finished initializing.", _performAppDomainFlagChecks ? "AppDomain" : "System.Diagnostics.StackTrace");
            }
#endif

            InitResolvers();
        }

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
        public LibLogScopeEventSubscriber(Tracer tracer, IScopeManager scopeManager, string defaultServiceName, string version, string env)
        {
            _tracer = tracer;
            _scopeManager = scopeManager;
            _defaultServiceName = defaultServiceName;
            _version = version;
            _env = env;

#if NETFRAMEWORK
            if (_executingIISPreStartInit)
            {
                _scopeManager.TraceStarted += OnTraceStarted_RefreshIISState;
            }
#endif

            try
            {
                _logProvider = LogProvider.CurrentLogProvider ?? LogProvider.ResolveLogProvider();

                if (_logProvider is ILogProviderWithEnricher logProvider)
                {
                    var enricher = logProvider.CreateEnricher();
                    enricher.Initialize(_tracer);
                    _logEnricher = enricher;

                    _scopeManager.TraceStarted += RegisterLogEnricher;
                    _scopeManager.TraceEnded += ClearLogEnricher;
                }
                else if (_logProvider is SerilogLogProvider)
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

#if NETFRAMEWORK
        public void OnTraceStarted_RefreshIISState(object sender, SpanEventArgs spanEventArgs)
        {
            // If we were previously executing in the IIS PreApp Code, refresh this evaluation
            // If not, do not waste cycles
            if (_executingIISPreStartInit)
            {
                RefreshIISPreAppState(traceId: spanEventArgs.Span.TraceId);
            }

            if (!_executingIISPreStartInit)
            {
                _scopeManager.TraceStarted -= OnTraceStarted_RefreshIISState;
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

        public void RegisterLogEnricher(object sender, SpanEventArgs spanEventArgs)
        {
            if (!_executingIISPreStartInit && _safeToAddToMdc)
            {
                try
                {
                    var currentEnricher = _currentEnricher.Get();

                    if (currentEnricher == null)
                    {
                        _currentEnricher.Set(_logEnricher.Register());
                    }
                }
                catch (Exception)
                {
                    _safeToAddToMdc = false;
                }
            }
        }

        public void ClearLogEnricher(object sender, SpanEventArgs spanEventArgs)
        {
            if (!_executingIISPreStartInit)
            {
                if (_tracer.ActiveScope == null)
                {
                    // We closed the last span
                    _currentEnricher.Get()?.Dispose();
                    _currentEnricher.Set(null);
                }
            }
        }

        public void Dispose()
        {
            if (_logProvider is ILogProviderWithEnricher)
            {
                _scopeManager.TraceStarted -= RegisterLogEnricher;
                _scopeManager.TraceEnded -= ClearLogEnricher;
            }
            else if (_logProvider is SerilogLogProvider)
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

        private static void InitResolvers()
        {
            // Note: for retrocompatibility, the order in which the providers are resolved is important
            // Make sure to respect the order:
            //  - Serilog
            //  - NLog
            //  - Log4net

            // Register the custom log4net provider
            LogProvider.LogProviderResolvers.Insert(
                0,
                Tuple.Create<LogProvider.IsLoggerAvailable, LogProvider.CreateLogProvider>(
                    CustomLog4NetLogProvider.IsLoggerAvailable,
                    () => new CustomLog4NetLogProvider()));

            // Register the custom NLog providers
            LogProvider.LogProviderResolvers.Insert(
                0,
                Tuple.Create<LogProvider.IsLoggerAvailable, LogProvider.CreateLogProvider>(
                    CustomNLogLogProvider.IsLoggerAvailable,
                    () => new CustomNLogLogProvider()));

            LogProvider.LogProviderResolvers.Insert(
                1,
                Tuple.Create<LogProvider.IsLoggerAvailable, LogProvider.CreateLogProvider>(
                    FallbackNLogLogProvider.IsLoggerAvailable,
                    () => new FallbackNLogLogProvider()));

            // Register the custom Serilog provider
            LogProvider.LogProviderResolvers.Insert(
                0,
                Tuple.Create<LogProvider.IsLoggerAvailable, LogProvider.CreateLogProvider>(
                    CustomSerilogLogProvider.IsLoggerAvailable,
                    () => new CustomSerilogLogProvider()));
        }

        private void SetDefaultValues()
        {
            SetLogContext(0, 0);
        }

        private void RemoveLastCorrelationIdentifierContext()
        {
            if (_logProvider is CustomSerilogLogProvider)
            {
                if (_tracer.ActiveScope == null)
                {
                    // We closed the last span
                    _currentEnricher.Get()?.Dispose();
                    _currentEnricher.Set(null);
                }

                return;
            }

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

            _currentEnricher.Get()?.Dispose();
            _currentEnricher.Set(null);
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
        private static void RefreshIISPreAppState(ulong traceId)
        {
            Debug.Assert(_executingIISPreStartInit, $"{nameof(_executingIISPreStartInit)} should always be true when entering {nameof(RefreshIISPreAppState)}");

            if (_performAppDomainFlagChecks)
            {
                object state = AppDomain.CurrentDomain.GetData(NamedSlotName);
                _executingIISPreStartInit = state is bool boolState && boolState;
            }
            else
            {
                var stackTrace = new StackTrace(false);
                var initialStackFrame = stackTrace.GetFrame(stackTrace.FrameCount - 1);
                var initialMethod = initialStackFrame.GetMethod();

                _executingIISPreStartInit = initialMethod.DeclaringType.FullName.Equals("System.Web.Hosting.HostingEnvironment", StringComparison.OrdinalIgnoreCase)
                                            && initialMethod.Name.Equals("Initialize", StringComparison.OrdinalIgnoreCase);
            }

            if (_executingIISPreStartInit)
            {
                Log.Warning("IIS is still initializing the application. Automatic logs injection will be disabled until the application begins processing incoming requests. Affected traceId={0}", traceId);
            }
            else
            {
                Log.Information("Automatic logs injection has resumed, starting with traceId={0}", traceId);
            }
        }
#endif
    }
}
