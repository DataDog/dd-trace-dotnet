// <copyright file="LibLogScopeEventSubscriber.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
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

        private readonly IScopeManager _scopeManager;
        private readonly string _defaultServiceName;
        private readonly string _version;
        private readonly string _env;
        private readonly ILogProvider _logProvider;
        private readonly ILogEnricher _logEnricher;

        private readonly AsyncLocal<IDisposable> _currentEnricher = new();
        private readonly AsyncLocal<Context> _currentContext = new();

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
            }
#endif

            try
            {
                _logProvider = LogProvider.CurrentLogProvider ?? LogProvider.ResolveLogProvider();

                if (_logProvider is ILogProviderWithEnricher logProvider)
                {
                    var enricher = logProvider.CreateEnricher();
                    enricher.Initialize(defaultServiceName, version, env);
                    _logEnricher = enricher;

                    _scopeManager.TraceStarted += RegisterLogEnricher;
                    _scopeManager.TraceEnded += ClearLogEnricher;
                }
                else if (_logProvider is FallbackNLogLogProvider)
                {
                    _scopeManager.SpanOpened += PushContext;
                    _scopeManager.SpanClosed += PopContext;
                }
                else
                {
                    // Do not subcribe to events. This is a proactive step to avoid issues
                    // when the manual and automatic versions do not match.
                    //
                    // The issue is that two separate Datadog.Trace assemblies will be loaded
                    // and each will create their own LibLogScopeEventSubscriber to modify
                    // the static state of the logging libraries. Each has an instance-level stack to
                    // set/unset properties (using the IDisposable pattern), but the two sides
                    // cannot communicate so they may clear state out of the order in which it was set.
                    //
                    // Thus, we will be proactive and disable usage for unknown log providers.
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
                RefreshIISPreAppState(traceId: spanEventArgs.TraceId);
            }

            if (!_executingIISPreStartInit)
            {
                _scopeManager.TraceStarted -= OnTraceStarted_RefreshIISState;
            }
        }
#endif

        public void PushContext(object sender, SpanEventArgs spanEventArgs)
        {
            if (!_executingIISPreStartInit && _safeToAddToMdc)
            {
                try
                {
                    Context previousContext = _currentContext.Value;
                    _currentContext.Value = new Context(previousContext, spanEventArgs, _logProvider, _env, _version, _defaultServiceName, spanEventArgs.TraceId.ToString(), spanEventArgs.SpanId.ToString());
                }
                catch (Exception)
                {
                    _safeToAddToMdc = false;
                }
            }
        }

        public void PopContext(object sender, SpanEventArgs spanEventArgs)
        {
            if (!_executingIISPreStartInit
                && _currentContext.Value is Context context)
            {
                if (spanEventArgs.SpanId == context.SpanId
                    && spanEventArgs.TraceId == context.TraceId)
                {
                    context.Dispose();
                    _currentContext.Value = context.PreviousContext;
                }
            }
        }

        public void RegisterLogEnricher(object sender, SpanEventArgs spanEventArgs)
        {
            if (!_executingIISPreStartInit && _safeToAddToMdc)
            {
                try
                {
                    var currentEnricher = _currentEnricher.Value;
                    var distributedTraceStarted = Tracer.Instance.DistributedSpanContext is not null;

                    if (currentEnricher == null && !distributedTraceStarted)
                    {
                        _currentEnricher.Value = _logEnricher.Register();
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
                if (_scopeManager.Active == null)
                {
                    // We closed the last span
                    _currentEnricher.Value?.Dispose();
                    _currentEnricher.Value = null;
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
            else if (_logProvider is FallbackNLogLogProvider)
            {
                _scopeManager.SpanOpened -= PushContext;
                _scopeManager.SpanClosed -= PopContext;
            }
            else
            {
                // No unsubscribing needed
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

            // Register the custom Serilog provider
            LogProvider.LogProviderResolvers.Insert(
                0,
                Tuple.Create<LogProvider.IsLoggerAvailable, LogProvider.CreateLogProvider>(
                    CustomSerilogLogProvider.IsLoggerAvailable,
                    () => new CustomSerilogLogProvider()));

            // Register the custom NLog providers
            LogProvider.LogProviderResolvers.Insert(
                2,
                Tuple.Create<LogProvider.IsLoggerAvailable, LogProvider.CreateLogProvider>(
                    CustomNLogLogProvider.IsLoggerAvailable,
                    () => new CustomNLogLogProvider()));

            LogProvider.LogProviderResolvers.Insert(
                3,
                Tuple.Create<LogProvider.IsLoggerAvailable, LogProvider.CreateLogProvider>(
                    FallbackNLogLogProvider.IsLoggerAvailable,
                    () => new FallbackNLogLogProvider()));
        }

        private void RemoveAllCorrelationIdentifierContexts()
        {
            Context currentContext = _currentContext.Value;
            while (currentContext != null)
            {
                currentContext.Dispose();
                currentContext = currentContext.PreviousContext;
            }

            _currentEnricher.Value?.Dispose();
            _currentEnricher.Value = null;
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

        /// <summary>
        /// Wraps all the individual context objects in a single instance, that can be stored in an AsyncLocal
        /// </summary>
        private class Context : IDisposable
        {
            private readonly Context _previousContext;
            private readonly ulong _spanIdValue;
            private readonly ulong _traceIdValue;
            private readonly IDisposable _environment;
            private readonly IDisposable _version;
            private readonly IDisposable _service;
            private readonly IDisposable _traceId;
            private readonly IDisposable _spanId;

            public Context(Context previousContext, SpanEventArgs span, ILogProvider logProvider, string environmentProperty, string versionProperty, string serviceProperty, string traceIdProperty, string spanIdProperty)
            {
                _previousContext = previousContext;
                _spanIdValue = span.SpanId;
                _traceIdValue = span.TraceId;

                try
                {
                    _environment = logProvider.OpenMappedContext(CorrelationIdentifier.EnvKey, environmentProperty);
                    _version = logProvider.OpenMappedContext(CorrelationIdentifier.VersionKey, versionProperty);
                    _service = logProvider.OpenMappedContext(CorrelationIdentifier.ServiceKey, serviceProperty);
                    _traceId = logProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, traceIdProperty);
                    _spanId = logProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, spanIdProperty);
                }
                catch
                {
                    // Clear the properties that are already mapped
                    Dispose();
                    throw;
                }
            }

            public Context PreviousContext => _previousContext;

            public ulong SpanId => _spanIdValue;

            public ulong TraceId => _traceIdValue;

            public void Dispose()
            {
                _environment?.Dispose();
                _version?.Dispose();
                _service?.Dispose();
                _traceId?.Dispose();
                _spanId?.Dispose();
            }
        }
    }
}
