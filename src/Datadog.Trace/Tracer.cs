using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace
{
    /// <summary>
    /// The tracer is responsible for creating spans and flushing them to the Datadog agent
    /// </summary>
    public class Tracer : IDatadogTracer
    {
        private const string UnknownServiceName = "UnknownService";
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<Tracer>();

        /// <summary>
        /// The number of Tracer instances that have been created and not yet destroyed.
        /// This is used in the heartbeat metrics to estimate the number of
        /// "live" Tracers that could potentially be sending traces to the Agent.
        /// </summary>
        private static int _liveTracerCount;

        /// <summary>
        /// Indicates whether we're initializing a tracer for the first time
        /// </summary>
        private static int _firstInitialization = 1;

        private static Tracer _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new object();

        private static RuntimeMetricsWriter _runtimeMetricsWriter;

        private readonly IScopeManager _scopeManager;
        private readonly Timer _heartbeatTimer;

        private IAgentWriter _agentWriter;

        static Tracer()
        {
            TracingProcessManager.Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/> class with default settings.
        /// </summary>
        public Tracer()
            : this(settings: null, agentWriter: null, sampler: null, scopeManager: null, statsd: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/>
        /// class using the specified <see cref="IConfigurationSource"/>.
        /// </summary>
        /// <param name="settings">
        /// A <see cref="TracerSettings"/> instance with the desired settings,
        /// or null to use the default configuration sources.
        /// </param>
        public Tracer(TracerSettings settings)
            : this(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null)
        {
        }

        internal Tracer(TracerSettings settings, IAgentWriter agentWriter, ISampler sampler, IScopeManager scopeManager, IDogStatsd statsd)
        {
            // update the count of Tracer instances
            Interlocked.Increment(ref _liveTracerCount);

            Settings = settings ?? TracerSettings.FromDefaultSources();
            Settings.Freeze();

            // if not configured, try to determine an appropriate service name
            DefaultServiceName = Settings.ServiceName ??
                                 GetApplicationName() ??
                                 UnknownServiceName;

            // only set DogStatsdClient if tracer metrics are enabled
            if (Settings.TracerMetricsEnabled)
            {
                // Run this first in case the port override is ready
                TracingProcessManager.SubscribeToDogStatsDPortOverride(
                    port =>
                    {
                        Log.Debug("Attempting to override dogstatsd port with {0}", port);
                        Statsd = CreateDogStatsdClient(Settings, DefaultServiceName, port);
                    });

                Statsd = statsd ?? CreateDogStatsdClient(Settings, DefaultServiceName, Settings.DogStatsdPort);
            }

            // Run this first in case the port override is ready
            TracingProcessManager.SubscribeToTraceAgentPortOverride(
                port =>
                {
                    Log.Debug("Attempting to override trace agent port with {0}", port);
                    var builder = new UriBuilder(Settings.AgentUri) { Port = port };
                    var baseEndpoint = builder.Uri;

                    if (_agentWriter == null)
                    {
                        IApi overridingApiClient = new Api(baseEndpoint, apiRequestFactory: null, Statsd);
                        _agentWriter = _agentWriter ?? new AgentWriter(overridingApiClient, Statsd, queueSize: Settings.TraceQueueSize);
                    }
                    else
                    {
                        _agentWriter.SetApiBaseEndpoint(baseEndpoint);
                    }
                });

            // fall back to default implementations of each dependency if not provided
            _agentWriter = agentWriter ?? new AgentWriter(new Api(Settings.AgentUri, apiRequestFactory: null, Statsd), Statsd, queueSize: Settings.TraceQueueSize);

            _scopeManager = scopeManager ?? new AsyncLocalScopeManager();
            Sampler = sampler ?? new RuleBasedSampler(new RateLimiter(Settings.MaxTracesSubmittedPerSecond));

            if (!string.IsNullOrWhiteSpace(Settings.CustomSamplingRules))
            {
                foreach (var rule in CustomSamplingRule.BuildFromConfigurationString(Settings.CustomSamplingRules))
                {
                    Sampler.RegisterRule(rule);
                }
            }

            if (Settings.GlobalSamplingRate != null)
            {
                var globalRate = (float)Settings.GlobalSamplingRate;

                if (globalRate < 0f || globalRate > 1f)
                {
                    Log.Warning("{0} configuration of {1} is out of range", ConfigurationKeys.GlobalSamplingRate, Settings.GlobalSamplingRate);
                }
                else
                {
                    Sampler.RegisterRule(new GlobalSamplingRule(globalRate));
                }
            }

            // Register callbacks to make sure we flush the traces before exiting
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;

            try
            {
                // Registering for the AppDomain.UnhandledException event cannot be called by a security transparent method
                // This will only happen if the Tracer is not run full-trust
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register a callback to the AppDomain.UnhandledException event.");
            }

            try
            {
                // Registering for the cancel key press event requires the System.Security.Permissions.UIPermission
                Console.CancelKeyPress += Console_CancelKeyPress;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register a callback to the Console.CancelKeyPress event.");
            }

            // start the heartbeat loop
            _heartbeatTimer = new Timer(HeartbeatCallback, state: null, dueTime: TimeSpan.Zero, period: TimeSpan.FromMinutes(1));

            // If configured, add/remove the correlation identifiers into the
            // LibLog logging context when a scope is activated/closed
            if (Settings.LogsInjectionEnabled)
            {
                InitializeLibLogScopeEventSubscriber(_scopeManager, DefaultServiceName, Settings.ServiceVersion, Settings.Environment);
            }

            if (Interlocked.Exchange(ref _firstInitialization, 0) == 1)
            {
                if (Settings.StartupDiagnosticLogEnabled)
                {
                    _ = WriteDiagnosticLog();
                }

                if (Settings.RuntimeMetricsEnabled)
                {
                    _runtimeMetricsWriter = new RuntimeMetricsWriter(Statsd ?? CreateDogStatsdClient(Settings, DefaultServiceName, Settings.DogStatsdPort), TimeSpan.FromSeconds(10));
                }
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Tracer"/> class.
        /// </summary>
        ~Tracer()
        {
            // update the count of Tracer instances
            Interlocked.Decrement(ref _liveTracerCount);
        }

        /// <summary>
        /// Gets or sets the global <see cref="Tracer"/> instance.
        /// Used by all automatic instrumentation and recommended
        /// as the entry point for manual instrumentation.
        /// </summary>
        public static Tracer Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock);
            }

            set
            {
                lock (_globalInstanceLock)
                {
                    _instance = value;
                    _globalInstanceInitialized = true;
                }
            }
        }

        /// <summary>
        /// Gets the active scope
        /// </summary>
        public Scope ActiveScope => _scopeManager.Active;

        /// <summary>
        /// Gets the default service name for traces where a service name is not specified.
        /// </summary>
        public string DefaultServiceName { get; }

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        public TracerSettings Settings { get; }

        /// <summary>
        /// Gets the tracer's scope manager, which determines which span is currently active, if any.
        /// </summary>
        IScopeManager IDatadogTracer.ScopeManager => _scopeManager;

        /// <summary>
        /// Gets the <see cref="ISampler"/> instance used by this <see cref="IDatadogTracer"/> instance.
        /// </summary>
        ISampler IDatadogTracer.Sampler => Sampler;

        internal IDiagnosticManager DiagnosticManager { get; set; }

        internal ISampler Sampler { get; }

        internal IDogStatsd Statsd { get; private set; }

        /// <summary>
        /// Create a new Tracer with the given parameters
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint where the traces will be sent (default is http://localhost:8126).</param>
        /// <param name="defaultServiceName">Default name of the service (default is the name of the executing assembly).</param>
        /// <param name="isDebugEnabled">Turns on all debug logging (this may have an impact on application performance).</param>
        /// <returns>The newly created tracer</returns>
        public static Tracer Create(Uri agentEndpoint = null, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            // Keep supporting this older public method by creating a TracerConfiguration
            // from default sources, overwriting the specified settings, and passing that to the constructor.
            var configuration = TracerSettings.FromDefaultSources();
            GlobalSettings.SetDebugEnabled(isDebugEnabled);

            if (agentEndpoint != null)
            {
                configuration.AgentUri = agentEndpoint;
            }

            if (defaultServiceName != null)
            {
                configuration.ServiceName = defaultServiceName;
            }

            return new Tracer(configuration);
        }

        /// <summary>
        /// Make a span the active span and return its new scope.
        /// </summary>
        /// <param name="span">The span to activate.</param>
        /// <returns>A Scope object wrapping this span.</returns>
        Scope IDatadogTracer.ActivateSpan(Span span)
        {
            return ActivateSpan(span);
        }

        /// <summary>
        /// Make a span the active span and return its new scope.
        /// </summary>
        /// <param name="span">The span to activate.</param>
        /// <param name="finishOnClose">Determines whether closing the returned scope will also finish the span.</param>
        /// <returns>A Scope object wrapping this span.</returns>
        public Scope ActivateSpan(Span span, bool finishOnClose = true)
        {
            return _scopeManager.Activate(span, finishOnClose);
        }

        /// <summary>
        /// This is a shortcut for <see cref="StartSpan(string, ISpanContext, string, DateTimeOffset?, bool)"/>
        /// and <see cref="ActivateSpan(Span, bool)"/>, it creates a new span with the given parameters and makes it active.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="parent">The span's parent</param>
        /// <param name="serviceName">The span's service name</param>
        /// <param name="startTime">An explicit start time for that span</param>
        /// <param name="ignoreActiveScope">If set the span will not be a child of the currently active span</param>
        /// <param name="finishOnClose">If set to false, closing the returned scope will not close the enclosed span </param>
        /// <returns>A scope wrapping the newly created span</returns>
        public Scope StartActive(string operationName, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false, bool finishOnClose = true)
        {
            var span = StartSpan(operationName, parent, serviceName, startTime, ignoreActiveScope);
            return _scopeManager.Activate(span, finishOnClose);
        }

        /// <summary>
        /// Creates a new <see cref="Span"/> with the specified parameters.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <returns>The newly created span</returns>
        Span IDatadogTracer.StartSpan(string operationName)
        {
            return StartSpan(operationName);
        }

        /// <summary>
        /// Creates a new <see cref="Span"/> with the specified parameters.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="parent">The span's parent</param>
        /// <returns>The newly created span</returns>
        Span IDatadogTracer.StartSpan(string operationName, ISpanContext parent)
        {
            return StartSpan(operationName, parent);
        }

        /// <summary>
        /// Creates a new <see cref="Span"/> with the specified parameters.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="parent">The span's parent</param>
        /// <param name="serviceName">The span's service name</param>
        /// <param name="startTime">An explicit start time for that span</param>
        /// <param name="ignoreActiveScope">If set the span will not be a child of the currently active span</param>
        /// <returns>The newly created span</returns>
        public Span StartSpan(string operationName, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false)
        {
            if (parent == null && !ignoreActiveScope)
            {
                parent = _scopeManager.Active?.Span?.Context;
            }

            ITraceContext traceContext;

            // try to get the trace context (from local spans) or
            // sampling priority (from propagated spans),
            // otherwise start a new trace context
            if (parent is SpanContext parentSpanContext)
            {
                traceContext = parentSpanContext.TraceContext ??
                               new TraceContext(this)
                               {
                                   SamplingPriority = parentSpanContext.SamplingPriority
                               };
            }
            else
            {
                traceContext = new TraceContext(this);
            }

            var finalServiceName = serviceName ?? parent?.ServiceName ?? DefaultServiceName;
            var spanContext = new SpanContext(parent, traceContext, finalServiceName);

            var span = new Span(spanContext, startTime)
            {
                OperationName = operationName,
            };

            // Apply any global tags
            if (Settings.GlobalTags.Count > 0)
            {
                foreach (var entry in Settings.GlobalTags)
                {
                    span.SetTag(entry.Key, entry.Value);
                }
            }

            // automatically add the "env" tag if defined, taking precedence over an "env" tag set from a global tag
            var env = Settings.Environment;
            if (!string.IsNullOrWhiteSpace(env))
            {
                span.SetTag(Tags.Env, env);
            }

            // automatically add the "version" tag if defined, taking precedence over an "version" tag set from a global tag
            var version = Settings.ServiceVersion;
            if (!string.IsNullOrWhiteSpace(version) && string.Equals(finalServiceName, DefaultServiceName))
            {
                span.SetTag(Tags.Version, version);
            }

            traceContext.AddSpan(span);
            return span;
        }

        /// <summary>
        /// Writes the specified <see cref="Span"/> collection to the agent writer.
        /// </summary>
        /// <param name="trace">The <see cref="Span"/> collection to write.</param>
        void IDatadogTracer.Write(Span[] trace)
        {
            if (Settings.TraceEnabled)
            {
                _agentWriter.WriteTrace(trace);
            }
        }

        internal Scope StartActiveWithTags(string operationName, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false, bool finishOnClose = true, ITags tags = null)
        {
            var span = StartSpan(operationName, tags, parent, serviceName, startTime, ignoreActiveScope);
            return _scopeManager.Activate(span, finishOnClose);
        }

        internal Span StartSpan(string operationName, ITags tags, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false)
        {
            if (parent == null && !ignoreActiveScope)
            {
                parent = _scopeManager.Active?.Span?.Context;
            }

            ITraceContext traceContext;

            // try to get the trace context (from local spans) or
            // sampling priority (from propagated spans),
            // otherwise start a new trace context
            if (parent is SpanContext parentSpanContext)
            {
                traceContext = parentSpanContext.TraceContext ??
                               new TraceContext(this)
                               {
                                   SamplingPriority = parentSpanContext.SamplingPriority
                               };
            }
            else
            {
                traceContext = new TraceContext(this);
            }

            var finalServiceName = serviceName ?? parent?.ServiceName ?? DefaultServiceName;
            var spanContext = new SpanContext(parent, traceContext, finalServiceName);

            var span = new Span(spanContext, startTime, tags)
            {
                OperationName = operationName,
            };

            // Apply any global tags
            if (Settings.GlobalTags.Count > 0)
            {
                foreach (var entry in Settings.GlobalTags)
                {
                    span.SetTag(entry.Key, entry.Value);
                }
            }

            // automatically add the "env" tag if defined, taking precedence over an "env" tag set from a global tag
            var env = Settings.Environment;
            if (!string.IsNullOrWhiteSpace(env))
            {
                span.SetTag(Tags.Env, env);
            }

            // automatically add the "version" tag if defined, taking precedence over an "version" tag set from a global tag
            var version = Settings.ServiceVersion;
            if (!string.IsNullOrWhiteSpace(version) && string.Equals(finalServiceName, DefaultServiceName))
            {
                span.SetTag(Tags.Version, version);
            }

            traceContext.AddSpan(span);
            return span;
        }

        internal Task FlushAsync()
        {
            return _agentWriter.FlushTracesAsync();
        }

        internal async Task WriteDiagnosticLog()
        {
            string agentError = null;

            try
            {
                var success = await _agentWriter.Ping().ConfigureAwait(false);

                if (!success)
                {
                    agentError = "An error occurred while sending traces to the agent";
                }
            }
            catch (Exception ex)
            {
                agentError = ex.Message;
            }

            try
            {
                var frameworkDescription = FrameworkDescription.Create();

                var stringWriter = new StringWriter();

                using (var writer = new JsonTextWriter(stringWriter))
                {
                    writer.WriteStartObject();

                    writer.WritePropertyName("date");
                    writer.WriteValue(DateTime.Now);

                    writer.WritePropertyName("os_name");
                    writer.WriteValue(frameworkDescription.OSPlatform);

                    writer.WritePropertyName("os_version");
                    writer.WriteValue(Environment.OSVersion.ToString());

                    writer.WritePropertyName("version");
                    writer.WriteValue(TracerConstants.AssemblyVersion);

                    writer.WritePropertyName("platform");
                    writer.WriteValue(frameworkDescription.ProcessArchitecture);

                    writer.WritePropertyName("lang");
                    writer.WriteValue(frameworkDescription.Name);

                    writer.WritePropertyName("lang_version");
                    writer.WriteValue(frameworkDescription.ProductVersion);

                    writer.WritePropertyName("env");
                    writer.WriteValue(Settings.Environment);

                    writer.WritePropertyName("enabled");
                    writer.WriteValue(Settings.TraceEnabled);

                    writer.WritePropertyName("service");
                    writer.WriteValue(DefaultServiceName);

                    writer.WritePropertyName("agent_url");
                    writer.WriteValue(Settings.AgentUri);

                    writer.WritePropertyName("debug");
                    writer.WriteValue(GlobalSettings.Source.DebugEnabled);

                    writer.WritePropertyName("analytics_enabled");
                    writer.WriteValue(Settings.AnalyticsEnabled);

                    writer.WritePropertyName("sample_rate");
                    writer.WriteValue(Settings.GlobalSamplingRate);

                    writer.WritePropertyName("sampling_rules");
                    writer.WriteValue(Settings.CustomSamplingRules);

                    writer.WritePropertyName("tags");

                    writer.WriteStartArray();

                    foreach (var entry in Settings.GlobalTags)
                    {
                        writer.WriteValue(string.Concat(entry.Key, ":", entry.Value));
                    }

                    writer.WriteEndArray();

                    writer.WritePropertyName("log_injection_enabled");
                    writer.WriteValue(Settings.LogsInjectionEnabled);

                    writer.WritePropertyName("runtime_metrics_enabled");
                    writer.WriteValue(Settings.RuntimeMetricsEnabled);

                    writer.WritePropertyName("disabled_integrations");
                    writer.WriteStartArray();

                    foreach (var integration in Settings.DisabledIntegrationNames)
                    {
                        writer.WriteValue(integration);
                    }

                    writer.WriteEndArray();

                    writer.WritePropertyName("netstandard_enabled");
                    writer.WriteValue(Settings.IsNetStandardFeatureFlagEnabled());

                    writer.WritePropertyName("agent_reachable");
                    writer.WriteValue(agentError == null);

                    writer.WritePropertyName("agent_error");
                    writer.WriteValue(agentError ?? string.Empty);

                    writer.WriteEndObject();
                }

                Log.Information("DATADOG TRACER CONFIGURATION - {0}", stringWriter.ToString());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DATADOG TRACER DIAGNOSTICS - Error fetching configuration");
            }
        }

        /// <summary>
        /// Gets an "application name" for the executing application by looking at
        /// the hosted app name (.NET Framework on IIS only), assembly name, and process name.
        /// </summary>
        /// <returns>The default service name.</returns>
        private static string GetApplicationName()
        {
            try
            {
                try
                {
                    if (TryLoadAspNetSiteName(out var siteName))
                    {
                        return siteName;
                    }
                }
                catch (Exception ex)
                {
                    // Unable to call into System.Web.dll
                    Log.SafeLogError(ex, "Unable to get application name through ASP.NET settings");
                }

                return Assembly.GetEntryAssembly()?.GetName().Name ??
                   ProcessHelpers.GetCurrentProcessName();
            }
            catch (Exception ex)
            {
                Log.SafeLogError(ex, "Error creating default service name.");
                return null;
            }
        }

        private static bool TryLoadAspNetSiteName(out string siteName)
        {
#if NETFRAMEWORK
            // System.Web.dll is only available on .NET Framework
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                // if this app is an ASP.NET application, return "SiteName/ApplicationVirtualPath".
                // note that ApplicationVirtualPath includes a leading slash.
                siteName = (System.Web.Hosting.HostingEnvironment.SiteName + System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath).TrimEnd('/');
                return true;
            }

#endif
            siteName = default;
            return false;
        }

        private static IDogStatsd CreateDogStatsdClient(TracerSettings settings, string serviceName, int port)
        {
            try
            {
                var frameworkDescription = FrameworkDescription.Create();

                var constantTags = new List<string>
                                   {
                                       "lang:.NET",
                                       $"lang_interpreter:{frameworkDescription.Name}",
                                       $"lang_version:{frameworkDescription.ProductVersion}",
                                       $"tracer_version:{TracerConstants.AssemblyVersion}",
                                       $"service:{serviceName}"
                                   };

                if (settings.Environment != null)
                {
                    constantTags.Add($"env:{settings.Environment}");
                }

                if (settings.ServiceVersion != null)
                {
                    constantTags.Add($"version:{settings.ServiceVersion}");
                }

                var statsd = new DogStatsdService();
                statsd.Configure(new StatsdConfig
                {
                    StatsdServerName = settings.AgentUri.DnsSafeHost,
                    StatsdPort = port,
                    ConstantTags = constantTags.ToArray()
                });

                return statsd;
            }
            catch (Exception ex)
            {
                Log.SafeLogError(ex, $"Unable to instantiate {nameof(Statsd)} client.");
                return new NoOpStatsd();
            }
        }

        private void InitializeLibLogScopeEventSubscriber(IScopeManager scopeManager, string defaultServiceName, string version, string env)
        {
            new LibLogScopeEventSubscriber(scopeManager, defaultServiceName, version ?? string.Empty, env ?? string.Empty);
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            RunShutdownTasks();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Warning("Application threw an unhandled exception: {0}", e.ExceptionObject);
            RunShutdownTasks();
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            RunShutdownTasks();
        }

        private void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            RunShutdownTasks();
        }

        private void RunShutdownTasks()
        {
            try
            {
                _agentWriter.FlushAndCloseAsync().Wait();
            }
            catch (Exception ex)
            {
                Log.SafeLogError(ex, "Error flushing traces on shutdown.");
            }

            try
            {
                TracingProcessManager.StopProcesses();
            }
            catch (Exception ex)
            {
                Log.SafeLogError(ex, "Error stopping sub processes on shutdown.");
            }
        }

        private void HeartbeatCallback(object state)
        {
            // use the count of Tracer instances as the heartbeat value
            // to estimate the number of "live" Tracers than can potentially
            // send traces to the Agent
            Statsd?.Gauge(TracerMetricNames.Health.Heartbeat, _liveTracerCount);
        }
    }
}
