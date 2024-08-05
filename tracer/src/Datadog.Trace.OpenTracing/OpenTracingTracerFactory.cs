// <copyright file="OpenTracingTracerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.OpenTracing
{
    /// <summary>
    /// This class contains factory methods to instantiate an OpenTracing compatible tracer that sends data to DataDog
    /// </summary>
    public static class OpenTracingTracerFactory
    {
        private const string DeprecationMessage = "OpenTracing support has been deprecated and will be removed in a future release. We recommend using OpenTelemetry instead.";

        /// <summary>
        /// Create a new Datadog compatible ITracer implementation with the given parameters
        /// </summary>
        /// <param name="agentEndpoint">The agent endpoint where the traces will be sent (default is http://localhost:8126).</param>
        /// <param name="defaultServiceName">Default name of the service (default is the name of the executing assembly).</param>
        /// <param name="isDebugEnabled">Turns on all debug logging (this may have an impact on application performance).</param>
        /// <returns>A Datadog compatible ITracer implementation</returns>
        [PublicApi]
        [Obsolete(DeprecationMessage)]
        [Instrumented]
        public static global::OpenTracing.ITracer CreateTracer(Uri agentEndpoint = null, string defaultServiceName = null, bool isDebugEnabled = false)
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

            Tracer.Configure(configuration);
            var tracer = Tracer.Instance;
            return new OpenTracingTracer(tracer, OpenTracingTracer.CreateDefaultScopeManager(), tracer.DefaultServiceName);
        }

        /// <summary>
        /// Create a new Datadog compatible ITracer implementation using an existing Datadog Tracer instance
        /// </summary>
        /// <param name="tracer">Existing Datadog Tracer instance</param>
        /// <returns>A Datadog compatible ITracer implementation</returns>
        [PublicApi]
        [Obsolete(DeprecationMessage)]
        [Instrumented]
        public static global::OpenTracing.ITracer WrapTracer(Tracer tracer)
        {
            return new OpenTracingTracer(tracer, OpenTracingTracer.CreateDefaultScopeManager(), tracer.DefaultServiceName);
        }
    }
}
