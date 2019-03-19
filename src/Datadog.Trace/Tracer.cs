using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;

namespace Datadog.Trace
{
    /// <summary>
    /// The tracer is responsible for creating spans and flushing them to the Datadog agent
    /// </summary>
    public class Tracer : IDatadogTracer
    {
        private const string UnknownServiceName = "UnknownService";

        private static readonly ILog Log = LogProvider.For<Tracer>();

        private readonly AsyncLocalScopeManager _scopeManager;
        private readonly IAgentWriter _agentWriter;
        private readonly TracerConfiguration _configuration;

        static Tracer()
        {
            // create the default global Tracer
            Instance = Create();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/>
        /// class with the default <see cref="IConfigurationSource"/>.
        /// </summary>
        public Tracer()
            : this(configurationSource: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/>
        /// class using the specified <see cref="IConfigurationSource"/>.
        /// </summary>
        /// <param name="configurationSource">A <see cref="IConfigurationSource"/> instance that contains the new Tracer's configuration.</param>
        public Tracer(IConfigurationSource configurationSource)
        {
            _configuration = new TracerConfiguration(configurationSource ?? CreateDefaultConfigurationSource());

            var agentEndpoint = GetAgentUri(_configuration);
            var api = new Api(agentEndpoint);
            _agentWriter = new AgentWriter(api);

            // these are not configurable for now
            Sampler = new RateByServiceSampler();
            _scopeManager = new AsyncLocalScopeManager();

            // if not configure, try to determine an appropriate service name
            DefaultServiceName = _configuration.ServiceName ??
                                 GetApplicationName() ??
                                 UnknownServiceName;

            // Register callbacks to make sure we flush the traces before exiting
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Console.CancelKeyPress += Console_CancelKeyPress;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/> class for testing purposes only
        /// </summary>
        /// <param name="agentWriter">The <see cref="IAgentWriter"/> to use when sending traces.</param>
        /// <param name="sampler">The <see cref="ISampler"/> to use when making sampling decisions.</param>
        internal Tracer(IAgentWriter agentWriter, ISampler sampler)
        {
            _agentWriter = agentWriter;
            Sampler = sampler;
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
        bool IDatadogTracer.IsDebugEnabled => _configuration.DebugEnabled;

        /// <summary>
        /// Gets the default service name for traces where a service name is not specified.
        /// </summary>
        public string DefaultServiceName { get; }

        /// <summary>
        /// Gets the tracer's scope manager, which determines which span is currently active, if any.
        /// </summary>
        AsyncLocalScopeManager IDatadogTracer.ScopeManager => _scopeManager;

        /// <summary>
        /// Gets the <see cref="ISampler"/> instance used by this <see cref="IDatadogTracer"/> instance.
        /// </summary>
        ISampler IDatadogTracer.Sampler => Sampler;

        internal ISampler Sampler { get; }

        /// <summary>
        /// Create a new Tracer with the given parameters
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint where the traces will be sent (default is http://localhost:8126).</param>
        /// <param name="defaultServiceName">Default name of the service (default is the name of the executing assembly).</param>
        /// <param name="isDebugEnabled">Turns on all debug logging (this may have an impact on application performance).</param>
        /// <returns>The newly created tracer</returns>
        public static Tracer Create(Uri agentEndpoint = null, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            // Keep supporting this older public method by creating a default configuration source,
            // adding a few custom settings, and passing that to the constructor.
            var settings = new NameValueCollection();

            if (agentEndpoint != null)
            {
                // changing the path or the schema (http/s) is not supported, to take only the host and port
                settings[ConfigurationKeys.AgentHost] = agentEndpoint.Host;
                settings[ConfigurationKeys.AgentPort] = agentEndpoint.Port.ToString(CultureInfo.InvariantCulture);
            }

            if (defaultServiceName != null)
            {
                settings[ConfigurationKeys.ServiceName] = defaultServiceName;
            }

            if (isDebugEnabled)
            {
                settings[ConfigurationKeys.DebugEnabled] = bool.TrueString;
            }

            // insert custom configuration at first position so it has highest precedence
            var configurationSource = CreateDefaultConfigurationSource();
            configurationSource.Insert(0, new NameValueConfigurationSource(settings));
            return new Tracer(configurationSource);
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

            var env = _configuration.Environment;

            // automatically add the "env" tag if defined
            if (!string.IsNullOrWhiteSpace(env))
            {
                span.SetTag(Tags.Env, env);
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
        /// the specified <paramref name="configuration"/>.
        /// </summary>
        /// <param name="configuration">A <see cref="TracerConfiguration"/> object </param>
        /// <returns>An Uri that can be used to send traces to the Agent.</returns>
        internal static Uri GetAgentUri(Configuration.TracerConfiguration configuration)
        {
            return new Uri($"http://{configuration.AgentHost}:{configuration.AgentPort}");
        }

        private static CompositeConfigurationSource CreateDefaultConfigurationSource()
        {
            // env > AppSettings > datadog.json
            var configurationSource = new CompositeConfigurationSource
            {
                new EnvironmentConfigurationSource()
            };

#if !NETSTANDARD2_0
            // on .NET Framework only, also read from app.config/web.config
            configurationSource.Add(new NameValueConfigurationSource(System.Configuration.ConfigurationManager.AppSettings));
#endif
            // if environment variable is not set, look for default file name in the current directory
            var configurationFileName = configurationSource.GetString(ConfigurationKeys.ConfigurationFileName) ??
                                        Path.Combine(Environment.CurrentDirectory, "datadog.json");

            if (Path.GetExtension(configurationFileName).ToUpperInvariant() == ".JSON" &&
                File.Exists(configurationFileName))
            {
                configurationSource.Add(JsonConfigurationSource.LoadFile(configurationFileName));
            }

            return configurationSource;
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
                Log.ErrorException("Error creating default service name.", ex);
                return null;
            }
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
