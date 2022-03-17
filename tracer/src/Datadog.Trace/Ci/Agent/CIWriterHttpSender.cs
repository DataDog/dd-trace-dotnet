// <copyright file="CIWriterHttpSender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci.Agent
{
    internal sealed class CIWriterHttpSender : ICIAgentlessWriterSender
    {
        private const string ApiKeyHeader = "dd-api-key";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CIWriterHttpSender>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly GlobalSettings _globalSettings;

        public CIWriterHttpSender(IApiRequestFactory apiRequestFactory)
        {
            _apiRequestFactory = apiRequestFactory;
            _globalSettings = GlobalSettings.FromDefaultSources();
            Log.Information("CIWriterHttpSender Initialized.");
        }

        public async Task SendPayloadAsync(EventsPayload payload)
        {
            var numberOfTraces = payload.Count;
            var tracesEndpoint = payload.Url;

            // retry up to 5 times with exponential back-off
            const int retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds

            var payloadMimeType = MimeTypes.MsgPack;
            var payloadBytes = payload.ToArray();

            Log.Information($"Sending ({numberOfTraces} events) {payloadBytes.Length.ToString("N0")} bytes...");

            while (true)
            {
                IApiRequest request;

                try
                {
                    request = _apiRequestFactory.Create(tracesEndpoint);
                    request.AddHeader(ApiKeyHeader, CIVisibility.Settings.ApiKey);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while generating http request to send events to {AgentEndpoint}", _apiRequestFactory.Info(tracesEndpoint));
                    return;
                }

                bool success = false;
                Exception exception = null;
                bool isFinalTry = retryCount >= retryLimit;

                try
                {
                    success = await SendPayloadAsync(new ArraySegment<byte>(payloadBytes), payloadMimeType, request, isFinalTry).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exception = ex;

                    if (_globalSettings.DebugEnabled)
                    {
                        if (ex.InnerException is InvalidOperationException ioe)
                        {
                            Log.Error<int, string>(ex, "An error occurred while sending {Count} events to {AgentEndpoint}", numberOfTraces, _apiRequestFactory.Info(tracesEndpoint));
                            return;
                        }
                    }
                }

                // Error handling block
                if (!success)
                {
                    if (isFinalTry)
                    {
                        // stop retrying
                        Log.Error<int, int, string>(exception, "An error occurred while sending {Count} events after {Retries} retries to {AgentEndpoint}", numberOfTraces, retryCount, _apiRequestFactory.Info(tracesEndpoint));
                        return;
                    }

                    // Before retry delay
                    bool isSocketException = false;
                    Exception innerException = exception;

                    while (innerException != null)
                    {
                        if (innerException is SocketException)
                        {
                            isSocketException = true;
                            break;
                        }

                        innerException = innerException.InnerException;
                    }

                    if (isSocketException)
                    {
                        Log.Debug(exception, "Unable to communicate with {AgentEndpoint}", _apiRequestFactory.Info(tracesEndpoint));
                    }

                    // Execute retry delay
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    retryCount++;
                    sleepDuration *= 2;

                    continue;
                }

                Log.Debug<int, string>("Successfully sent {Count} events to {AgentEndpoint}", numberOfTraces, _apiRequestFactory.Info(tracesEndpoint));
                return;
            }
        }

        private async Task<bool> SendPayloadAsync(ArraySegment<byte> payload, string mimeType, IApiRequest request, bool finalTry)
        {
            IApiResponse response = null;

            try
            {
                try
                {
                    response = await request.PostAsync(payload, mimeType).ConfigureAwait(false);
                }
                catch
                {
                    // count only network/infrastructure errors, not valid responses with error status codes
                    // (which are handled below)
                    throw;
                }

                // Attempt a retry if the status code is not SUCCESS
                if (response.StatusCode < 200 || response.StatusCode >= 300)
                {
                    if (finalTry)
                    {
                        try
                        {
                            string responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                            Log.Error<int, string>("Failed to submit events with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
                        }
                        catch (Exception ex)
                        {
                            Log.Error<int>(ex, "Unable to read response for failed request with status code {StatusCode}", response.StatusCode);
                        }
                    }

                    return false;
                }
            }
            finally
            {
                response?.Dispose();
            }

            return true;
        }
    }
}
