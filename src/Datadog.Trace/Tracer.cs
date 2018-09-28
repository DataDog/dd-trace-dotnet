using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    /// <summary>
    /// The tracer is responsible for creating spans and flushing them to the Datadog agent
    /// </summary>
    public class Tracer : IDatadogTracer
    {
        private const string UnknownServiceName = "UnknownService";
        private static readonly ILog _log = LogProvider.For<Tracer>();
        private static readonly string _defaultTraceAgentHost = "localhost";
        private static readonly string _defaultTraceAgentPort = "8126";

        private AsyncLocalScopeManager _scopeManager;
        private IAgentWriter _agentWriter;
        private bool _isDebugEnabled;

        static Tracer()
        {
            Instance = Create();
        }

        internal Tracer(IAgentWriter agentWriter, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            IConfigurationSource configurationSource = CreateConfigurationSource();

            _isDebugEnabled = isDebugEnabled;
            _agentWriter = agentWriter;
            DefaultServiceName = defaultServiceName ?? CreateDefaultServiceName() ?? UnknownServiceName;

            // Register callbacks to make sure we flush the traces before exiting
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Console.CancelKeyPress += Console_CancelKeyPress;
            _scopeManager = new AsyncLocalScopeManager();
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
        bool IDatadogTracer.IsDebugEnabled => _isDebugEnabled;

        /// <summary>
        /// Gets the default service name for traces where a service name is not specified.
        /// </summary>
        public string DefaultServiceName { get; }

        /// <summary>
        /// Gets the tracer's scope manager, which determines which span is currently active, if any.
        /// </summary>
        AsyncLocalScopeManager IDatadogTracer.ScopeManager => _scopeManager;

        /// <summary>
        /// Create a new Tracer with the given parameters
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint where the traces will be sent (default is http://localhost:8126).</param>
        /// <param name="defaultServiceName">Default name of the service (default is the name of the executing assembly).</param>
        /// <param name="isDebugEnabled">Turns on all debug logging (this may have an impact on application performance).</param>
        /// <returns>The newly created tracer</returns>
        public static Tracer Create(Uri agentEndpoint = null, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            return Create(agentEndpoint ?? DefaultAgentUri(), defaultServiceName, null, isDebugEnabled);
        }

        /// <summary>
        /// Make a span active and return a scope that can be disposed to desactivate the span
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
        /// <param name="childOf">The span's parent</param>
        /// <param name="serviceName">The span's service name</param>
        /// <param name="startTime">An explicit start time for that span</param>
        /// <param name="ignoreActiveScope">If set the span will not be a child of the currently active span</param>
        /// <param name="finishOnClose">If set to false, closing the returned scope will not close the enclosed span </param>
        /// <returns>A scope wrapping the newly created span</returns>
        public Scope StartActive(string operationName, SpanContext childOf = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false, bool finishOnClose = true)
        {
            var span = StartSpan(operationName, childOf, serviceName, startTime, ignoreActiveScope);
            return _scopeManager.Activate(span, finishOnClose);
        }

        /// <summary>
        /// This create a Span with the given parameters
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="childOf">The span's parent</param>
        /// <param name="serviceName">The span's service name</param>
        /// <param name="startTime">An explicit start time for that span</param>
        /// <param name="ignoreActiveScope">If set the span will not be a child of the currently active span</param>
        /// <returns>The newly created span</returns>
        public Span StartSpan(string operationName, SpanContext childOf = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false)
        {
            if (childOf == null && !ignoreActiveScope)
            {
                childOf = _scopeManager.Active?.Span?.Context;
            }

            var span = new Span(this, childOf, operationName, serviceName, startTime);
            span.TraceContext.AddSpan(span);
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

        internal static Tracer Create(Uri agentEndpoint, string serviceName, DelegatingHandler delegatingHandler = null, bool isDebugEnabled = false)
        {
            var api = new Api(agentEndpoint, delegatingHandler);
            var agentWriter = new AgentWriter(api);
            var tracer = new Tracer(agentWriter, serviceName, isDebugEnabled);
            return tracer;
        }

        internal static IConfigurationSource CreateConfigurationSource()
        {
            // app.config (app local) > datadog.json (app local) > env (system-wide)
            var configurationSource = new AggregateConfigurationSource();

#if NET45 || NET46
            configurationSource.AddSource(new NameValueConfigurationSource(System.Configuration.ConfigurationManager.AppSettings));
#endif

            string jsonConfigurationFileName = configurationSource.GetString("DD_DOTNET_TRACER_CONFIGURATION_FILE") ??
                                               Path.Combine(Environment.CurrentDirectory, "datadog.json");

            if (File.Exists(jsonConfigurationFileName))
            {
                configurationSource.AddSource(JsonConfigurationSource.LoadFile(jsonConfigurationFileName));
            }

            configurationSource.AddSource(new EnvironmentConfigurationSource());
            return configurationSource;
        }

        private static Uri DefaultAgentUri()
        {
            var prefixes = new string[] { "DD", "DATADOG" };

            var host = prefixes.
                Select(prefix => Environment.GetEnvironmentVariable($"{prefix}_TRACE_AGENT_HOSTNAME")).
                Where(str => !string.IsNullOrEmpty(str)).
                FirstOrDefault() ?? _defaultTraceAgentHost;
            var port = prefixes.
                Select(prefix => Environment.GetEnvironmentVariable($"{prefix}_TRACE_AGENT_PORT")).
                Where(str => !string.IsNullOrEmpty(str)).
                FirstOrDefault() ?? _defaultTraceAgentPort;

            return new Uri($"http://{host}:{port}");
        }

        private static string CreateDefaultServiceName()
        {
            try
            {
#if !NETSTANDARD2_0
                if (System.Web.Hosting.HostingEnvironment.IsHosted)
                {
                    // if we are hosted as an ASP.NET application, return "SiteName/ApplicationVirtualPath".
                    // note that ApplicationVirtualPath includes a leading slash.
                    return (System.Web.Hosting.HostingEnvironment.SiteName + System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath).TrimEnd('/');
                }
#endif
                return Assembly.GetEntryAssembly()?.GetName().Name ??
                       Process.GetCurrentProcess().ProcessName;
            }
            catch (Exception ex)
            {
                _log.ErrorException("Error creating default service name.", ex);
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
