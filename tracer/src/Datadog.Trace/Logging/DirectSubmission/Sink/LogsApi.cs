// <copyright file="LogsApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal sealed class LogsApi : ILogsApi
    {
        internal const string LogIntakePath = "/api/v2/logs";

        private const string MimeType = "application/json";

        private const int MaxNumberRetries = 5;
        private const int InitialSleepDurationMs = 500;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LogsApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly Uri _logsIntakeEndpoint;
        private int _apiKeyTransportRejected;

        public LogsApi(IApiRequestFactory apiRequestFactory)
        {
            _apiRequestFactory = apiRequestFactory;
            _logsIntakeEndpoint = _apiRequestFactory.GetEndpoint(LogIntakePath);
            Log.Debug("Using logs intake endpoint {LogsIntakeEndpoint}", _logsIntakeEndpoint.ToString());
        }

        public void Dispose()
        {
        }

        public async Task<bool> SendLogsAsync(ArraySegment<byte> logs, int numberOfLogs)
        {
            if (Volatile.Read(ref _apiKeyTransportRejected) != 0)
            {
                return false;
            }

            var retriesRemaining = MaxNumberRetries - 1;
            var nextSleepDuration = InitialSleepDurationMs;

            Log.Debug<int>("Sending {Count} logs to the logs intake", numberOfLogs);

            while (true)
            {
                IApiRequest request;

                try
                {
                    request = _apiRequestFactory.Create(_logsIntakeEndpoint);
                }
                catch (ApiKeyHttpTransportException ex)
                {
                    DisableForUnsafeApiKeyTransport(ex);
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while generating request to send logs to the intake at {IntakeEndpoint}", _apiRequestFactory.Info(_logsIntakeEndpoint));
                    return false;
                }

                Exception? exception = null;
                var isFinalTry = retriesRemaining <= 0;
                var shouldRetry = true;

                try
                {
                    IApiResponse? response = null;

                    try
                    {
                        TelemetryFactory.Metrics.RecordCountDirectLogApiRequests();
                        response = await request.PostAsync(logs, MimeType).ConfigureAwait(false);

                        TelemetryFactory.Metrics.RecordCountDirectLogApiResponses(response.GetTelemetryStatusCodeMetricTag());

                        if (response.StatusCode is >= 200 and < 300)
                        {
                            Log.Debug<int>("Successfully sent {Count} logs to the intake", numberOfLogs);
                            return true;
                        }

                        TelemetryFactory.Metrics.RecordCountDirectLogApiErrors(MetricTags.ApiError.StatusCode);

                        shouldRetry = response.ShouldRetry();

                        if (!shouldRetry || isFinalTry)
                        {
                            try
                            {
                                var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                                Log.Error<int, string>("Failed to submit logs with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
                            }
                            catch (Exception ex)
                            {
                                Log.Error<int>(ex, "Unable to read response for failed request with status code {StatusCode}", response.StatusCode);
                            }
                        }
                    }
                    finally
                    {
                        response?.Dispose();
                    }
                }
                catch (ApiKeyHttpTransportException ex)
                {
                    DisableForUnsafeApiKeyTransport(ex);
                    return false;
                }
                catch (Exception ex)
                {
                    var tag = ex is TimeoutException ? MetricTags.ApiError.Timeout : MetricTags.ApiError.NetworkError;
                    TelemetryFactory.Metrics.RecordCountDirectLogApiErrors(tag);

                    exception = ex;
#if DEBUG
                    if (ex.InnerException is InvalidOperationException)
                    {
                        Log.Error<int, string>(ex, "An error occurred while sending {Count} logs to the intake at {IntakeEndpoint}", numberOfLogs, _apiRequestFactory.Info(_logsIntakeEndpoint));
                        return false;
                    }
#endif
                }

                // Error handling block
                if (!shouldRetry || isFinalTry)
                {
                    // stop retrying
                    Log.Error<int, string>(exception, "An error occurred while sending {Count} traces to the intake at {IntakeEndpoint}", numberOfLogs, _apiRequestFactory.Info(_logsIntakeEndpoint));
                    return false;
                }

                // Before retry delay
                if (exception.IsSocketException())
                {
                    Log.Debug(exception, "Unable to communicate with the logs intake at {IntakeEndpoint}", _apiRequestFactory.Info(_logsIntakeEndpoint));
                }

                // Execute retry delay
                await Task.Delay(nextSleepDuration).ConfigureAwait(false);
                retriesRemaining--;
                nextSleepDuration *= 2;
            }
        }

        private void DisableForUnsafeApiKeyTransport(ApiKeyHttpTransportException exception)
        {
            if (Interlocked.Exchange(ref _apiKeyTransportRejected, 1) == 0)
            {
                TelemetryFactory.Metrics.RecordCountDirectLogApiErrors(MetricTags.ApiError.NetworkError);
                Log.Error(exception, "Disabling direct log submission because the API-key transport is unsafe.");
            }
        }
    }
}
