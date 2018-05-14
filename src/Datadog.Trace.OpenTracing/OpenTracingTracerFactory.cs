using System;
using System.Net.Http;
using Datadog.Trace.Agent;
using OpenTracing;

namespace Datadog.Trace.OpenTracing
{
    /// <summary>
    /// This class contains factory methods to instantiate an OpenTracing compatible tracer that sends data to DataDog
    /// </summary>
    public static class OpenTracingTracerFactory
    {
        private static Uri _defaultUri = new Uri("http://localhost:8126");

        /// <summary>
        /// Create a new Datadog compatible ITracer implementation with the given parameters
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint where the traces will be sent (default is http://localhost:8126).</param>
        /// <param name="defaultServiceName">Default name of the service (default is the name of the executing assembly).</param>
        /// <param name="isDebugEnabled">Turns on all debug logging (this may have an impact on application performance).</param>
        /// <returns>A Datadog compatible ITracer implementation</returns>
        public static ITracer CreateTracer(Uri agentEndpoint = null, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            agentEndpoint = agentEndpoint ?? _defaultUri;
            return CreateTracer(agentEndpoint, defaultServiceName, null, isDebugEnabled);
        }

        internal static OpenTracingTracer CreateTracer(Uri agentEndpoint, string defaultServiceName = null, DelegatingHandler delegatingHandler = null, bool isDebugEnabled = false)
        {
            var api = new Api(agentEndpoint, delegatingHandler);
            var agentWriter = new AgentWriter(api);
            var ddTracer = new Tracer(agentWriter, defaultServiceName, isDebugEnabled);
            var tracer = new OpenTracingTracer(ddTracer);
            return tracer;
        }
    }
}
