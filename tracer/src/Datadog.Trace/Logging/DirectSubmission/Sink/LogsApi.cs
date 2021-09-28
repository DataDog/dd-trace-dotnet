// <copyright file="LogsApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent;

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal class LogsApi : ILogsApi
    {
        internal const string LogIntakePath = "v1/input";
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

        public async Task SendLogsAsync(ArraySegment<byte> logs, int numberOfLogs)
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
                    return;
                }

                // Set additional headers
                request.AddHeader(IntakeHeaderNameApiKey, _apiKey);

                Exception exception = null;
                var isFinalTry = retriesRemaining <= 0;
                var isClientError = false;

                try
                {
                    IApiResponse response = null;

                    try
                    {
                        // TODO: Metrics/Telemetry?
                        response = await request.PostAsync(logs, MimeType).ConfigureAwait(false);

                        if (response.StatusCode is >= 200 and < 300)
                        {
                            Log.Debug<int>("Successfully sent {Count} logs to the intake", numberOfLogs);
                            return;
                        }

                        isClientError = response.StatusCode is >=400 and < 500;

                        if (isClientError || isFinalTry)
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
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        Log.Error<int, string>(ex, "An error occurred while sending {Count} logs to the intake at {IntakeEndpoint}", numberOfLogs, _apiRequestFactory.Info(_logsIntakeEndpoint));
                        return;
                    }
#endif
                }

                // Error handling block
                if (isClientError || isFinalTry)
                {
                    // stop retrying
                    Log.Error<int, string>(exception, "An error occurred while sending {Count} traces to the intake at {IntakeEndpoint}", numberOfLogs, _apiRequestFactory.Info(_logsIntakeEndpoint));
                    return;
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

        private static bool IsSocketException(Exception exception)
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
