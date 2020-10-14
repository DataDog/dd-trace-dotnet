using System;
using System.Collections.Generic;
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
        /// A method invoked in Span.Log calls
        /// </summary>
        /// <param name="timestamp">Timestamp of the log</param>
        /// <param name="fields">Log fields</param>
        public delegate void SpanLogger(DateTimeOffset? timestamp, IEnumerable<KeyValuePair<string, object>> fields);

        /// <summary>
        /// Create a new Datadog compatible ITracer implementation with the given parameters
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint where the traces will be sent (default is http://localhost:8126).</param>
        /// <param name="defaultServiceName">Default name of the service (default is the name of the executing assembly).</param>
        /// <param name="isDebugEnabled">Turns on all debug logging (this may have an impact on application performance).</param>
        /// <param name="spanLogger">Delegate used in Span.Log calls</param>
        /// <returns>A Datadog compatible ITracer implementation</returns>
        public static ITracer CreateTracer(Uri agentEndpoint = null, string defaultServiceName = null, bool isDebugEnabled = false, SpanLogger spanLogger = null)
        {
            if (spanLogger is null)
            {
                spanLogger = (DateTimeOffset? timestamp, IEnumerable<KeyValuePair<string, object>> fields) => { };
            }

            return CreateTracer(agentEndpoint, defaultServiceName, null, isDebugEnabled, spanLogger);
        }

        /// <summary>
        /// Create a new Datadog compatible ITracer implementation using an existing Datadog Tracer instance
        /// </summary>
        /// <param name="tracer">Existing Datadog Tracer instance</param>
        /// <param name="spanLogger">Delegate used in Span.Log calls</param>
        /// <returns>A Datadog compatible ITracer implementation</returns>
        public static ITracer WrapTracer(Tracer tracer, SpanLogger spanLogger)
        {
            if (spanLogger is null)
            {
                spanLogger = (DateTimeOffset? timestamp, IEnumerable<KeyValuePair<string, object>> fields) => { };
            }

            return new OpenTracingTracer(tracer, spanLogger);
        }

        internal static OpenTracingTracer CreateTracer(Uri agentEndpoint, string defaultServiceName, DelegatingHandler delegatingHandler, bool isDebugEnabled, SpanLogger spanLogger)
        {
            var tracer = Tracer.Create(agentEndpoint, defaultServiceName, isDebugEnabled);
            return new OpenTracingTracer(tracer, spanLogger);
        }
    }
}
