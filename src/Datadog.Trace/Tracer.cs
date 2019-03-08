using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Datadog.Trace.Agent;
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
        private const string DefaultTraceAgentHost = "localhost";
        private const string DefaultTraceAgentPort = "8126";
        private const string EnvVariableName = "DD_ENV";
        private const string ServiceNameVariableName = "DD_SERVICE_NAME";

        private static readonly string[] TraceAgentHostEnvironmentVariableNames =
        {
            // officially documented name
            "DD_AGENT_HOST",
            // backwards compatibility for names used in the past
            "DD_TRACE_AGENT_HOSTNAME",
            "DATADOG_TRACE_AGENT_HOSTNAME"
        };

        private static readonly string[] TraceAgentPortEnvironmentVariableNames =
        {
            // officially documented name
            "DD_TRACE_AGENT_PORT",
            // backwards compatibility for names used in the past
            "DATADOG_TRACE_AGENT_PORT"
        };

        private static readonly ILog Log = LogProvider.For<Tracer>();
        private static readonly Uri DefaultAgentUri;

        private readonly AsyncLocalScopeManager _scopeManager;
        private readonly IAgentWriter _agentWriter;
        private readonly bool _isDebugEnabled;

        static Tracer()
        {
            // create Agent uri once and save it
            DefaultAgentUri = CreateAgentUri();

            // create the default global Tracer
            Instance = Create();
        }

        internal Tracer(IAgentWriter agentWriter, ISampler sampler, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            _isDebugEnabled = isDebugEnabled;
            _agentWriter = agentWriter;
            Sampler = sampler;
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
            return Create(agentEndpoint ?? DefaultAgentUri, defaultServiceName, null, isDebugEnabled);
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

            var spanContext = new SpanContext(parent, traceContext);

            var span = new Span(spanContext, startTime)
            {
                OperationName = operationName,
                ServiceName = serviceName ?? DefaultServiceName
            };

            var env = Environment.GetEnvironmentVariable(EnvVariableName);

            // automatically add the "env" tag if environment variable is defined
            if (!string.IsNullOrWhiteSpace(env))
            {
                span.SetTag(Tags.Env, env);
            }

            span.Context.TraceContext.AddSpan(span);
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
            var sampler = new RateByServiceSampler();
            var tracer = new Tracer(agentWriter, sampler, serviceName, isDebugEnabled);
            return tracer;
        }

        /// <summary>
        /// Create an Uri to the Agent using host and port from
        /// environment variables or defaults if not set.
        /// </summary>
        /// <returns>An Uri that can be used to send traces to the Agent.</returns>
        internal static Uri CreateAgentUri()
        {
            var host = TraceAgentHostEnvironmentVariableNames.Select(Environment.GetEnvironmentVariable)
                                                             .FirstOrDefault(str => !string.IsNullOrEmpty(str))
                                                            ?.Trim() ?? DefaultTraceAgentHost;

            var port = TraceAgentPortEnvironmentVariableNames.Select(Environment.GetEnvironmentVariable)
                                                             .FirstOrDefault(str => !string.IsNullOrEmpty(str))
                                                            ?.Trim() ?? DefaultTraceAgentPort;

            return new Uri($"http://{host}:{port}");
        }

        /// <summary>
        /// Determines the default service name for the executing application by looking at
        /// environment variables, hosted app name (.NET Framework on IIS only), assembly name, and process name.
        /// </summary>
        /// <returns>The default service name.</returns>
        private static string CreateDefaultServiceName()
        {
            try
            {
                // allow users to override this with an environment variable
                var serviceName = Environment.GetEnvironmentVariable(ServiceNameVariableName);

                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    return serviceName;
                }

#if !NETSTANDARD2_0
                // System.Web.dll is only available on .NET Framework
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
