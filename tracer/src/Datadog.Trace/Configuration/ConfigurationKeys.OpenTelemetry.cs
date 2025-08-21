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

            /// <summary>
            /// Configuration key to set the exporter for metrics.
            /// We only recognize the values of 'otlp' and 'none', a value of
            /// 'none' disables the emission of metrics which is the
            /// equivalent of setting <see cref="ConfigurationKeys.RuntimeMetricsEnabled"/>
            /// to false.
            /// </summary>
            public const string MetricsExporter = "OTEL_METRICS_EXPORTER";

            /// <summary>
            /// Configuration key to set the export interval for metrics in milliseconds.
            /// Specifies the time interval between the start of two export attempts.
            /// Default value is 10000ms (10s) for Datadog.
            /// This deviates from OpenTelemetry specification default of 60000ms (60s).
            /// </summary>
            public const string MetricExportIntervalMs = "OTEL_METRIC_EXPORT_INTERVAL";

            /// <summary>
            /// Configuration key to set the export timeout for metrics in milliseconds.
            /// Specifies the maximum time allowed for collecting and exporting metrics.
            /// Default value is 7500ms (7.5s) for Datadog.
            /// This deviates from OpenTelemetry specification default of 30000ms (30s).
            /// </summary>
            public const string MetricExportTimeoutMs = "OTEL_METRIC_EXPORT_TIMEOUT";

            /// <summary>
            /// Configuration key to set the OTLP protocol for metrics export.
            /// Takes precedence over <see cref="ExporterOtlpProtocol"/>.
            /// Valid values: grpc, http/protobuf, http/json, defaults to http/protobuf.
            /// </summary>
            public const string ExporterOtlpMetricsProtocol = "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL";

            /// <summary>
            /// Configuration key to set the OTLP endpoint URL for metrics.
            /// Takes precedence over <see cref="ExporterOtlpEndpoint"/>.
            /// This value typically ends with v1/metrics when using OTLP/HTTP.
            /// Expects values like `unix:///path/to/socket.sock` for UDS, `\\.\pipename\` for Windows Named Pipes.
            /// Default values: gRPC: http://localhost:4317, HTTP: http://localhost:4318/v1/metrics
            /// </summary>
            public const string ExporterOtlpMetricsEndpoint = "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT";

            /// <summary>
            /// Configuration key to set custom headers for OTLP metrics export.
            /// Takes precedence over <see cref="ExporterOtlpHeaders"/>.
            /// Format: api-key=key,other=value.
            /// </summary>
            public const string ExporterOtlpMetricsHeaders = "OTEL_EXPORTER_OTLP_METRICS_HEADERS";

            /// <summary>
            /// Configuration key to set the request timeout for OTLP metrics export in milliseconds.
            /// Takes precedence over <see cref="ExporterOtlpTimeoutMs"/>.
            /// Default value is 10000ms.
            /// </summary>
            public const string ExporterOtlpMetricsTimeoutMs = "OTEL_EXPORTER_OTLP_METRICS_TIMEOUT";

            /// <summary>
            /// Configuration key to set the temporality preference for OTLP metrics export.
            /// Supported values: delta, cumulative, lowmemory.
            /// Default value is delta for Datadog.
            /// This deviates from OpenTelemetry specification default of cumulative.
            /// </summary>
            public const string ExporterOtlpMetricsTemporalityPreference = "OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE";

            /// <summary>
            /// Configuration key to set the OTLP protocol (fallback for metrics-specific protocol).
            /// Used when <see cref="ExporterOtlpMetricsProtocol"/> is not set.
            /// Valid values: grpc, http/protobuf, http/json, defaults to http/protobuf.
            /// </summary>
            public const string ExporterOtlpProtocol = "OTEL_EXPORTER_OTLP_PROTOCOL";

            /// <summary>
            /// Configuration key to set the OTLP endpoint URL (fallback for metrics-specific endpoint).
            /// Used when <see cref="ExporterOtlpMetricsEndpoint"/> is not set.
            /// Expects values like `unix:///path/to/socket.sock` for UDS, `\\.\pipename\` for Windows Named Pipes.
            /// Default values: gRPC: http://localhost:4317, HTTP: http://localhost:4318
            /// </summary>
            public const string ExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";

            /// <summary>
            /// Configuration key to set custom headers for OTLP export (fallback for metrics-specific headers).
            /// Used when <see cref="ExporterOtlpMetricsHeaders"/> is not set.
            /// Format: api-key=key,other=value.
            /// </summary>
            public const string ExporterOtlpHeaders = "OTEL_EXPORTER_OTLP_HEADERS";

            /// <summary>
            /// Configuration key to set the request timeout for OTLP export (fallback for metrics-specific timeout).
            /// Used when <see cref="ExporterOtlpMetricsTimeoutMs"/> is not set.
            /// Default value is 10000ms.
            /// </summary>
            public const string ExporterOtlpTimeoutMs = "OTEL_EXPORTER_OTLP_TIMEOUT";
        }
    }
}
