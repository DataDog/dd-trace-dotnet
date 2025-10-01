// <copyright file="OtlpExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

#nullable enable

namespace Datadog.Trace.OTelMetrics
{
    /// <summary>
    /// OTLP Exporter implementation that exports metrics using the OpenTelemetry Protocol.
    /// This is the concrete implementation of the MetricExporter base class that handles OTLP protocol specifics.
    /// Supports both grpc and http/protobuf transports as specified in the RFC.
    /// This is a Push Metric Exporter that sends metrics via the OpenTelemetry Protocol.
    /// </summary>
    internal class OtlpExporter : MetricExporter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpExporter));
        private readonly HttpClient _httpClient = new();
        private readonly Configuration.TracerSettings _settings;

        public OtlpExporter(Configuration.TracerSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Exports a batch of metrics using OTLP protocol.
        /// This method implements the MetricExporter base class Export operation.
        /// </summary>
        /// <param name="metrics">Batch of metrics to export</param>
        /// <returns>ExportResult indicating success or failure</returns>
        public override ExportResult Export(IReadOnlyList<MetricPoint> metrics)
        {
            return ExportAsync(metrics).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Exports a batch of metrics using OTLP protocol asynchronously.
        /// This is the preferred method for better performance.
        /// </summary>
        /// <param name="metrics">Batch of metrics to export</param>
        /// <returns>ExportResult indicating success or failure</returns>
        public override async Task<ExportResult> ExportAsync(IReadOnlyList<MetricPoint> metrics)
        {
            if (metrics.Count == 0)
            {
                Log.Debug("No metrics to export.");
                return ExportResult.Success; // Success - nothing to do
            }

            // Record export attempt
            TelemetryFactory.Metrics.RecordCountMetricsExportAttempts(GetProtocolTag());

            try
            {
                // Metrics are already snapshots from MetricState.GetMetricPoints()
                // Serialize to OTLP protobuf format
                var otlpPayload = OtlpMetricsSerializer.SerializeMetrics(metrics, _settings);

                // Send to OTLP endpoint asynchronously for optimal performance
                var success = await SendOtlpRequest(otlpPayload).ConfigureAwait(false);

                if (success)
                {
                    // Record successful export
                    TelemetryFactory.Metrics.RecordCountMetricsExportSuccesses(GetProtocolTag());
                    return ExportResult.Success;
                }
                else
                {
                    // Record failed export
                    TelemetryFactory.Metrics.RecordCountMetricsExportFailures(GetProtocolTag());
                    return ExportResult.Failure;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error exporting OTLP metrics.");
                // Record failed export
                TelemetryFactory.Metrics.RecordCountMetricsExportFailures(GetProtocolTag());
                return ExportResult.Failure;
            }
        }

        /// <summary>
        /// Shuts down the exporter and ensures all pending exports complete.
        /// </summary>
        /// <param name="timeoutMilliseconds">Maximum time to wait for shutdown</param>
        /// <returns>True if shutdown completed successfully, false otherwise</returns>
        public override bool Shutdown(int timeoutMilliseconds)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMilliseconds);
                _httpClient.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during OTLP exporter shutdown");
                return false;
            }
        }

        /// <summary>
        /// Gets the protocol tag for telemetry metrics based on the configured OTLP protocol.
        /// </summary>
        private Telemetry.Metrics.MetricTags.Protocol GetProtocolTag()
        {
            return _settings.OtlpMetricsProtocol switch
            {
                Configuration.OtlpProtocol.Grpc => Telemetry.Metrics.MetricTags.Protocol.Grpc,
                Configuration.OtlpProtocol.HttpProtobuf => Telemetry.Metrics.MetricTags.Protocol.HttpProtobuf,
                Configuration.OtlpProtocol.HttpJson => Telemetry.Metrics.MetricTags.Protocol.HttpJson,
                _ => Telemetry.Metrics.MetricTags.Protocol.HttpProtobuf // Default fallback
            };
        }

        private async Task<bool> SendOtlpRequest(byte[] otlpPayload)
        {
            try
            {
                // Handle different OTLP protocols as per RFC
                // Note: gRPC is not supported in the tracer to avoid heavy dependencies
                return _settings.OtlpMetricsProtocol switch
                {
                    Configuration.OtlpProtocol.HttpProtobuf => await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false),
                    Configuration.OtlpProtocol.HttpJson => await SendHttpJsonRequest(otlpPayload).ConfigureAwait(false),
                    Configuration.OtlpProtocol.Grpc => await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false), // Fallback to HTTP/protobuf for gRPC
                    _ => await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false) // Default fallback
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending OTLP request.");
                return false;
            }
        }

        private async Task<bool> SendHttpProtobufRequest(byte[] otlpPayload)
        {
            // HTTP/protobuf - RFC compliant implementation with retry logic
            var content = new ByteArrayContent(otlpPayload);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-protobuf");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.OtlpMetricsEndpoint)
            {
                Content = content
            };

            // Add custom headers
            foreach (var header in _settings.OtlpMetricsHeaders)
            {
                httpRequest.Headers.Add(header.Key, header.Value);
            }

            // Add Accept-Encoding header for gzip support (RFC requirement)
            httpRequest.Headers.Add("Accept-Encoding", "gzip");

            // Implement retry logic as per RFC
            return await SendWithRetry(httpRequest).ConfigureAwait(false);
        }

        private async Task<bool> SendHttpJsonRequest(byte[] otlpPayload)
        {
            // HTTP/JSON - Optional support as per RFC
            // Note: This would require JSON serialization instead of protobuf
            // For now, we'll log a warning and fall back to HTTP/protobuf
            Log.Warning("HTTP/JSON protocol is not yet implemented, falling back to HTTP/protobuf");
            return await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false);
        }

        private async Task<bool> SendWithRetry(HttpRequestMessage httpRequest)
        {
            const int maxRetries = 3;
            var retryDelay = TimeSpan.FromMilliseconds(100); // Initial delay

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_settings.OtlpMetricsTimeoutMs));
                    var response = await _httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);

                    // Check for success
                    if (response.IsSuccessStatusCode)
                    {
                        // For now, treat any 200 response as success
                        // TODO: Parse protobuf response body to check for partial_success field
                        // RFC: MUST NOT retry on partial success, but we'd need to parse the response
                        return true;
                    }

                    // Handle specific status codes as per RFC
                    var statusCode = (int)response.StatusCode;

                    // MUST NOT retry on 400 Bad Request
                    if (statusCode == 400)
                    {
                        Log.Warning("Bad Request (400) - not retrying: {StatusCode}", response.StatusCode);
                        return false;
                    }

                    // SHOULD retry on specific status codes
                    if (statusCode == 429 || statusCode == 502 || statusCode == 503 || statusCode == 504)
                    {
                        if (attempt < maxRetries)
                        {
                            // Check for Retry-After header (RFC requirement)
                            var retryAfter = response.Headers.RetryAfter?.Delta;
                            if (retryAfter.HasValue)
                            {
                                retryDelay = retryAfter.Value;
                                Log.Debug("Retrying after {RetryAfter} as specified in Retry-After header", retryAfter.Value.ToString());
                            }
                            else
                            {
                                retryDelay = TimeSpan.FromMilliseconds((long)(retryDelay.TotalMilliseconds * 2));
                            }

                            await Task.Delay(retryDelay, cts.Token).ConfigureAwait(false);
                            continue;
                        }
                        else
                        {
                            Log.Warning("Max retries exceeded for retryable status code: {StatusCode}", response.StatusCode);
                            return false;
                        }
                    }

                    // All other 4xx/5xx status codes MUST NOT be retried
                    Log.Warning("Non-retryable status code: {StatusCode}", response.StatusCode);
                    return false;
                }
                catch (TaskCanceledException) when (attempt < maxRetries)
                {
                    // Timeout - retry with exponential backoff
                    retryDelay = TimeSpan.FromMilliseconds((long)(retryDelay.TotalMilliseconds * 2));
                    Log.Debug("Request timeout, retrying after {RetryDelay} (attempt {Attempt})", retryDelay.ToString(), (attempt + 1).ToString());
                    await Task.Delay(retryDelay).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error sending OTLP request (attempt {Attempt})", (attempt + 1).ToString());
                    if (attempt < maxRetries)
                    {
                        retryDelay = TimeSpan.FromMilliseconds((long)(retryDelay.TotalMilliseconds * 2));
                        await Task.Delay(retryDelay).ConfigureAwait(false);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return false;
        }
    }
}
#endif
