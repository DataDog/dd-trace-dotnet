// <copyright file="ConfigurationKeys.OpenTelemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Configuration
{
    internal partial class ConfigurationKeys
    {
        internal class OpenTelemetry
        {
            /// <summary>
            /// Configuration key for disabling the OpenTelemetry API's.
            /// </summary>
            public const string SdkDisabled = "OTEL_SDK_DISABLED";

            /// <summary>
            /// Configuration key for a list of key-value pairs to be set as
            /// resource attributes. We currently map these to span tags.
            /// </summary>
            public const string ResourceAttributes = "OTEL_RESOURCE_ATTRIBUTES";

            /// <summary>
            /// Configuration key for a list of tracing propagators.
            /// Datadog only supports a subset of the OpenTelemetry propagators.
            /// Also, the 'b3' OpenTelemetry propagator is mapped to the
            /// 'b3 single header' Datadog propagator.
            /// </summary>
            public const string Propagators = "OTEL_PROPAGATORS";

            /// <summary>
            /// Configuration key to set the application's default service name.
            /// </summary>
            public const string ServiceName = "OTEL_SERVICE_NAME";

            /// <summary>
            /// Configuration key to set the log level.
            /// </summary>
            public const string LogLevel = "OTEL_LOG_LEVEL";

            /// <summary>
            /// Configuration key to set the exporter for metrics.
            /// We only recognize the value 'none', which is the
            /// equivalent of setting <see cref="ConfigurationKeys.RuntimeMetricsEnabled"/>
            /// to false.
            /// </summary>
            public const string MetricsExporter = "OTEL_METRICS_EXPORTER";

            /// <summary>
            /// Configuration key to set the exporter for traces.
            /// We only recognize the value 'none', which is the
            /// equivalent of setting <see cref="ConfigurationKeys.TraceEnabled"/>
            /// to false.
            /// </summary>
            public const string TracesExporter = "OTEL_TRACES_EXPORTER";

            /// <summary>
            /// Configuration key to set the sampler for traces.
            /// to false.
            /// </summary>
            public const string TracesSampler = "OTEL_TRACES_SAMPLER";

            /// <summary>
            /// Configuration key to set an additional argument for the
            /// traces sampler.
            /// to false.
            /// </summary>
            public const string TracesSamplerArg = "OTEL_TRACES_SAMPLER_ARG";
        }
    }
}
