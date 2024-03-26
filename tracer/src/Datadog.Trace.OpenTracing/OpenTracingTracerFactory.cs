// <copyright file="OpenTracingTracerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net.Http;
using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
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
        [PublicApi]
        public static global::OpenTracing.ITracer CreateTracer(Uri agentEndpoint = null, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.OpenTracingTracerFactory_CreateTracer);
            // Keep supporting this older public method by creating a TracerConfiguration
            // from default sources, overwriting the specified settings, and passing that to the constructor.
            var configuration = TracerSettings.FromDefaultSourcesInternal();
            GlobalSettings.SetDebugEnabledInternal(isDebugEnabled);

            if (agentEndpoint != null)
            {
                configuration.ExporterInternal.AgentUriInternal = agentEndpoint;
            }

            if (defaultServiceName != null)
            {
                configuration.ServiceNameInternal = defaultServiceName;
            }

            Tracer.ConfigureInternal(new ImmutableTracerSettings(configuration, true));
            var tracer = Tracer.Instance;
            return new OpenTracingTracer(tracer, OpenTracingTracer.CreateDefaultScopeManager(), tracer.DefaultServiceName);
        }

        /// <summary>
        /// Create a new Datadog compatible ITracer implementation using an existing Datadog Tracer instance
        /// </summary>
        /// <param name="tracer">Existing Datadog Tracer instance</param>
        /// <returns>A Datadog compatible ITracer implementation</returns>
        [PublicApi]
        public static global::OpenTracing.ITracer WrapTracer(Tracer tracer)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.OpenTracingTracerFactory_WrapTracer);
            return new OpenTracingTracer(tracer, OpenTracingTracer.CreateDefaultScopeManager(), tracer.DefaultServiceName);
        }
    }
}
