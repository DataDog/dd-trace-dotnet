using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
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

        private readonly IScopeManager _scopeManager;
        private readonly IAgentWriter _agentWriter;

        static Tracer()
        {
            // create the default global Tracer
            Instance = new Tracer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/> class with default settings.
        /// </summary>
        public Tracer()
            : this(settings: null, agentWriter: null, sampler: null, scopeManager: null, dogStatsdClient: null)
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
            : this(settings, agentWriter: null, sampler: null, scopeManager: null, dogStatsdClient: null)
        {
        }

        internal Tracer(TracerSettings settings, IAgentWriter agentWriter, ISampler sampler, IScopeManager scopeManager, IDogStatsd dogStatsdClient)
        {
            // fall back to default implementations of each dependency if not provided
            Settings = settings ?? TracerSettings.FromDefaultSources();

            // only set DogStatsdClient if tracer metrics are enabled
            if (Settings.TracerMetricsEnabled)
            {
                DogStatsdClient = dogStatsdClient ?? CreateDogStatsdClient(Settings);
            }

            IApi apiClient = new Api(Settings.AgentUri, delegatingHandler: null, DogStatsdClient);
            _agentWriter = agentWriter ?? new AgentWriter(apiClient, DogStatsdClient);
            _scopeManager = scopeManager ?? new AsyncLocalScopeManager();
            Sampler = sampler ?? new RuleBasedSampler(new RateLimiter(Settings.MaxTracesSubmittedPerSecond));

            if (!string.IsNullOrWhiteSpace(Settings.CustomSamplingRules))
            {
                foreach (var rule in RegexSamplingRule.BuildFromConfigurationString(Settings.CustomSamplingRules))
                {
                    Sampler.RegisterRule(rule);
                }
            }

            // if not configured, try to determine an appropriate service name
            DefaultServiceName = Settings.ServiceName ??
                                 GetApplicationName() ??
                                 UnknownServiceName;

            // Register callbacks to make sure we flush the traces before exiting
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Console.CancelKeyPress += Console_CancelKeyPress;

            // If configured, add/remove the correlation identifiers into the
            // LibLog logging context when a scope is activated/closed
            if (Settings.LogsInjectionEnabled)
            {
                InitializeLibLogScopeEventSubscriber(_scopeManager);
            }
        }

        /// <summary>
        /// Gets or sets the global tracer object
        /// </summary>
        public static Tracer Instance { get; set; }

        /// <summary>
        /// Gets the active scope
        /// </summary>
        public Scope ActiveScope => _scopeManager.Active;

        /// <summary>
        /// Gets a value indicating whether debugging mode is enabled.
        /// </summary>
        /// <value><c>true</c> is debugging is enabled, otherwise <c>false</c>.</value>
        bool IDatadogTracer.IsDebugEnabled => Settings.DebugEnabled;

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

        internal ISampler Sampler { get; }

        internal IDogStatsd DogStatsdClient { get; }

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
            configuration.DebugEnabled = isDebugEnabled;

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
        /// Make a span active and return a scope that can be disposed to close the span
        /// </summary>
        /// <param name="span">The span to activate</param>
        /// <param name="finishOnClose">If set to false, closing the returned scope will not close the enclosed span </param>
        /// <returns>A Scope object wrapping this span</returns>
        public Scope ActivateSpan(Span span, bool finishOnClose = true)
        {
            return _scopeManager.Activate(span, finishOnClose);
        }

        /// <summary>
        /// This is a shortcut for <see cref="StartSpan"/> and <see cref="ActivateSpan"/>, it creates a new span with the given parameters and makes it active.
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

            var env = Settings.Environment;

            // automatically add the "env" tag if defined
            if (!string.IsNullOrWhiteSpace(env))
            {
                span.SetTag(Tags.Env, env);
            }

            // Apply any global tags
            if (Settings.GlobalTags.Count > 0)
            {
                foreach (var entry in Settings.GlobalTags)
                {
                    span.SetTag(entry.Key, entry.Value);
                }
            }

            traceContext.AddSpan(span);
            return span;
        }

        /// <summary>
        /// Writes the specified <see cref="Span"/> collection to the agent writer.
        /// </summary>
        /// <param name="trace">The <see cref="Span"/> collection to write.</param>
        void IDatadogTracer.Write(List<Span> trace)
        {
            _agentWriter.WriteTrace(trace);
        }

        /// <summary>
        /// Create an Uri to the Agent using host and port from
        /// the specified <paramref name="settings"/>.
        /// </summary>
        /// <param name="settings">A <see cref="TracerSettings"/> object </param>
        /// <returns>An Uri that can be used to send traces to the Agent.</returns>
        internal static Uri GetAgentUri(TracerSettings settings)
        {
            return settings.AgentUri;
        }

        internal async Task FlushAsync()
        {
            await _agentWriter.FlushAndCloseAsync();
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
#if !NETSTANDARD2_0
                // System.Web.dll is only available on .NET Framework
                if (System.Web.Hosting.HostingEnvironment.IsHosted)
                {
                    // if this app is an ASP.NET application, return "SiteName/ApplicationVirtualPath".
                    // note that ApplicationVirtualPath includes a leading slash.
                    return (System.Web.Hosting.HostingEnvironment.SiteName + System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath).TrimEnd('/');
                }
#endif

                return Assembly.GetEntryAssembly()?.GetName().Name ??
                       Process.GetCurrentProcess().ProcessName;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating default service name.");
                return null;
            }
        }

        private static DogStatsdService CreateDogStatsdClient(TracerSettings settings)
        {
            var frameworkDescription = FrameworkDescription.Create();
            var tracerVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            var config = new StatsdConfig
                         {
                             StatsdServerName = settings.AgentUri.DnsSafeHost,
                             StatsdPort = settings.DogStatsdPort,
                             ConstantTags = new[]
                                            {
                                                "lang:.NET",
                                                $"lang_interpreter:{frameworkDescription.Name}",
                                                $"lang_version:{frameworkDescription.ProductVersion}",
                                                $"tracer_version:{tracerVersion}"
                                            }
                         };

            var dogStatsdClient = new DogStatsdService();
            dogStatsdClient.Configure(config);
            return dogStatsdClient;
        }

        private void InitializeLibLogScopeEventSubscriber(IScopeManager scopeManager)
        {
            new LibLogScopeEventSubscriber(scopeManager);
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _agentWriter.FlushAndCloseAsync().Wait();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _agentWriter.FlushAndCloseAsync().Wait();
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _agentWriter.FlushAndCloseAsync().Wait();
        }
    }
}
