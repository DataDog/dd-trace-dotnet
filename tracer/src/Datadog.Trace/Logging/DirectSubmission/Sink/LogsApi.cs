// <copyright file="LogsApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent;

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal class LogsApi : ILogsApi
    {
        internal const string LogIntakePath = "api/v2/logs";
        internal const string IntakeHeaderNameApiKey = "DD-API-KEY";

        private const string MimeType = "application/json";

        private const int MaxNumberRetries = 5;
        private const int InitialSleepDurationMs = 500;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LogsApi>();

        private readonly string _apiKey;
        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly Uri _logsIntakeEndpoint;

        public LogsApi(Uri baseEndpoint, string apiKey, IApiRequestFactory apiRequestFactory)
        {
            var builder = new UriBuilder(baseEndpoint);
            builder.Path = builder.Path.EndsWith("/")
                               ? builder.Path + LogIntakePath
                               : builder.Path + "/" + LogIntakePath;

            _logsIntakeEndpoint = builder.Uri;
            _apiKey = apiKey;
            _apiRequestFactory = apiRequestFactory;
        }

        public void Dispose()
        {
        }

        public async Task<bool> SendLogsAsync(ArraySegment<byte> logs, int numberOfLogs)
        {
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
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while generating request to send logs to the intake at {IntakeEndpoint}", _apiRequestFactory.Info(_logsIntakeEndpoint));
                    return false;
                }

                // Set additional headers
                request.AddHeader(IntakeHeaderNameApiKey, _apiKey);

                Exception? exception = null;
                var isFinalTry = retriesRemaining <= 0;
                var shouldRetry = true;

                try
                {
                    IApiResponse? response = null;

                    try
                    {
                        // TODO: Metrics/Telemetry?
                        response = await request.PostAsync(logs, MimeType).ConfigureAwait(false);

                        if (response.StatusCode is >= 200 and < 300)
                        {
                            Log.Debug<int>("Successfully sent {Count} logs to the intake", numberOfLogs);
                            return true;
                        }

                        shouldRetry = response.StatusCode switch
                        {
                            400 => false, // Bad request (likely an issue in the payload formatting)
                            401 => false, // Unauthorized (likely a missing API Key)
                            403 => false, // Permission issue (likely using an invalid API Key)
                            408 => true, // Request Timeout, request should be retried after some time
                            413 => false, // Payload too large (batch is above 5MB uncompressed)
                            429 => true, // Too Many Requests, request should be retried after some time
                            >= 400 and < 500 => false, // generic "client" error, don't retry
                            _ => true // Something else, probably server error, do retry
                        };

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
                catch (Exception ex)
                {
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
                if (IsSocketException(exception))
                {
                    Log.Debug(exception, "Unable to communicate with the logs intake at {IntakeEndpoint}", _apiRequestFactory.Info(_logsIntakeEndpoint));
                }

                // Execute retry delay
                await Task.Delay(nextSleepDuration).ConfigureAwait(false);
                retriesRemaining--;
                nextSleepDuration *= 2;
            }
        }

        private static bool IsSocketException(Exception? exception)
        {
            while (exception is not null)
            {
                if (exception is SocketException)
                {
                    return true;
                }

                exception = exception.InnerException;
            }

            return false;
        }
    }
}
