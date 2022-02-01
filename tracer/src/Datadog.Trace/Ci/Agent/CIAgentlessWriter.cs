// <copyright file="CIAgentlessWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Ci.Agent
{
    internal class CIAgentlessWriter : CIWriter
    {
        private readonly IApiRequestFactory _apiRequestFactory;

        public CIAgentlessWriter(IApiRequestFactory apiRequestFactory, ImmutableTracerSettings settings, ISampler sampler)
            : base(settings, sampler)
        {
            _apiRequestFactory = apiRequestFactory;
            Log.Information("CIHttpWriter Initialized.");
        }

        public override Task<bool> Ping()
        {
            return Task.FromResult(true);
        }

        protected override async Task SendPayloadAsync(EventsPayload payload)
        {
            var numberOfTraces = payload.Count;
            var tracesEndpoint = payload.Url;

            // retry up to 5 times with exponential back-off
            var retryLimit = 5;
            var retryCount = 1;
            var sleepDuration = 100; // in milliseconds

            Log.Debug<int>("Sending {Count} traces to the DD agent", numberOfTraces);

            while (true)
            {
                IApiRequest request;

                try
                {
                    request = _apiRequestFactory.Create(tracesEndpoint);
                    request.AddHeader("dd-api-key", CIVisibility.Settings.ApiKey);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while generating http request to send traces to the agent at {AgentEndpoint}", _apiRequestFactory.Info(tracesEndpoint));
                    return;
                }

                bool success = false;
                Exception exception = null;
                bool isFinalTry = retryCount >= retryLimit;

                var msgPackBytes = payload.ToArray();

                Log.Information("Sending ({numberOfTraces} traces) {count} bytes...", numberOfTraces, msgPackBytes.Length.ToString("N"));

                try
                {
                    success = await SendPayloadAsync(new ArraySegment<byte>(msgPackBytes), numberOfTraces, request, isFinalTry).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exception = ex;
#if DEBUG
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        Log.Error<int, string>(ex, "An error occurred while sending {Count} traces to the agent at {AgentEndpoint}", numberOfTraces, _apiRequestFactory.Info(tracesEndpoint));
                        return;
                    }
#endif
                }

                // Error handling block
                if (!success)
                {
                    if (isFinalTry)
                    {
                        // stop retrying
                        Log.Error<int, string>(exception, "An error occurred while sending {Count} traces to the agent at {AgentEndpoint}", numberOfTraces, _apiRequestFactory.Info(tracesEndpoint));
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
                        Log.Debug(exception, "Unable to communicate with the trace agent at {AgentEndpoint}", _apiRequestFactory.Info(tracesEndpoint));
                    }

                    // Execute retry delay
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    retryCount++;
                    sleepDuration *= 2;

                    continue;
                }

                Log.Debug<int>("Successfully sent {Count} traces to the DD agent", numberOfTraces);
                return;
            }
        }

        private async Task<bool> SendPayloadAsync(ArraySegment<byte> payload, int numberOfTraces, IApiRequest request, bool finalTry)
        {
            IApiResponse response = null;

            try
            {
                try
                {
                    response = await request.PostAsync(payload, MimeTypes.Json).ConfigureAwait(false);
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
                            Log.Error<int, string>("Failed to submit traces with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
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
