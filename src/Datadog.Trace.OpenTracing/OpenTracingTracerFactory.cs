using System;
using System.Net.Http;
using OpenTracing;

namespace Datadog.Trace.OpenTracing
{
    /// <summary>
    /// This class contains factory methods to instantiate an OpenTracing compatible tracer that sends data to DataDog
    /// </summary>
    public static class OpenTracingTracerFactory
    {
        /// <summary>
        /// Create a new Datadog compatible ITracer implementation with the given parameters
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint where the traces will be sent (default is http://localhost:8126).</param>
        /// <param name="defaultServiceName">Default name of the service (default is the name of the executing assembly).</param>
        /// <param name="isDebugEnabled">Turns on all debug logging (this may have an impact on application performance).</param>
        /// <returns>A Datadog compatible ITracer implementation</returns>
        public static ITracer CreateTracer(Uri agentEndpoint = null, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            return CreateTracer(agentEndpoint, defaultServiceName, null, isDebugEnabled);
        }

        /// <summary>
        /// Create a new Datadog compatible ITracer implementation using an existing Datadog Tracer instance
        /// </summary>
        /// <param name="tracer">Existing Datadog Tracer instance</param>
        /// <returns>A Datadog compatible ITracer implementation</returns>
        public static ITracer WrapTracer(Tracer tracer)
        {
            return new OpenTracingTracer(tracer);
        }

        internal static OpenTracingTracer CreateTracer(Uri agentEndpoint, string defaultServiceName, DelegatingHandler delegatingHandler, bool isDebugEnabled)
        {
            var tracer = Tracer.Create(agentEndpoint, defaultServiceName, isDebugEnabled);
            return new OpenTracingTracer(tracer);
        }
    }
}
