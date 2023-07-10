// <copyright file="SymbolBatchUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolBatchUploadApi : IBatchUploadApi
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SymbolBatchUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private string? _endpoint;

        private SymbolBatchUploadApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            _apiRequestFactory = apiRequestFactory;
            discoveryService.SubscribeToChanges(c => _endpoint = c.SymbolDbEndpoint);
        }

        public static SymbolBatchUploadApi Create(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            return new SymbolBatchUploadApi(apiRequestFactory, discoveryService);
        }

        public async Task<bool> SendBatchAsync(ArraySegment<byte> snapshots)
        {
            if (Volatile.Read(ref _endpoint) is not { } endpoint)
            {
                Log.Warning("Failed to upload symbol: symbol endpoint not yet retrieved from discovery service");
                return false;
            }

            var uri = _apiRequestFactory.GetEndpoint(endpoint);
            var request = _apiRequestFactory.Create(uri);

            const int maxRetries = 3;
            int retries = 0;

            while (retries < maxRetries)
            {
                using var response = await request.PostAsync(snapshots, MimeTypes.Json).ConfigureAwait(false);

                if (response.StatusCode is >= 200 and <= 299)
                {
                    return true;
                }

                retries++;
                var content = await response.ReadAsStringAsync().ConfigureAwait(false);

                if (ShouldRetry(response.StatusCode))
                {
                    await Task.Delay(GetDelayTime(retries)).ConfigureAwait(false);
                }
                else
                {
                    Log.Error<int, string>("Failed to upload symbol with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
                    return false;
                }
            }

            return false;
        }

        private static bool ShouldRetry(int statusCode)
        {
            int[] statusCodesToRetry = { 408, 425, 429, 503, 504 };
            return statusCodesToRetry.Any(code => code == statusCode);
        }

        private static TimeSpan GetDelayTime(int retryAttempt)
        {
            const int baseDelayMs = 250;
            int maxDelayMs = baseDelayMs * (int)Math.Pow(2, retryAttempt);

            Random random = new Random();
            int delayMs = random.Next(baseDelayMs, maxDelayMs);

            return TimeSpan.FromMilliseconds(delayMs);
        }
    }
}
