// <copyright file="SymbolUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Upload
{
    internal class SymbolUploadApi : DebuggerUploadApiBase
    {
        private const int MaxRetries = 3;
        private const int StartingSleepDuration = 3;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SymbolUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly ArraySegment<byte> _eventMetadata;

        private SymbolUploadApi(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ArraySegment<byte> eventMetadata)
            : base(apiRequestFactory, gitMetadataTagsProvider)
        {
            _apiRequestFactory = apiRequestFactory;
            _eventMetadata = eventMetadata;

            discoveryService.SubscribeToChanges(c => Endpoint = c.SymbolDbEndpoint);
        }

        public static IBatchUploadApi Create(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            string serviceName)
        {
            ArraySegment<byte> GetEventMetadataAsArraySegment()
            {
                var eventMetadata = $@"{{""ddsource"": ""dd_debugger"", ""service"": ""{serviceName}"", ""runtimeId"": ""{Tracer.RuntimeId}""}}";

                var count = Encoding.UTF8.GetByteCount(eventMetadata);
                var eventAsBytes = new byte[count];
                Encoding.UTF8.GetBytes(eventMetadata, 0, eventMetadata.Length, eventAsBytes, 0);
                return new ArraySegment<byte>(eventAsBytes);
            }

            var eventMetadata = GetEventMetadataAsArraySegment();
            return new SymbolUploadApi(apiRequestFactory, discoveryService, gitMetadataTagsProvider, eventMetadata);
        }

        public override async Task<bool> SendBatchAsync(ArraySegment<byte> symbols)
        {
            var uri = BuildUri();
            if (string.IsNullOrEmpty(uri))
            {
                Log.Warning("Symbol database endpoint is not defined");
                return false;
            }

            var request = _apiRequestFactory.Create(new Uri(uri));

            var retries = 0;
            var sleepDuration = StartingSleepDuration;

            var items = new MultipartFormItem[]
            {
                new("file", MimeTypes.Json, "file.json", symbols),
                new("event", MimeTypes.Json, "event.json", _eventMetadata)
            };

            while (retries < MaxRetries)
            {
                using var response = await request.PostAsync(items).ConfigureAwait(false);
                if (response.StatusCode is >= 200 and <= 299)
                {
                    return true;
                }

                retries++;
                if (response.ShouldRetry())
                {
                    sleepDuration *= (int)Math.Pow(2, retries);
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                }
                else
                {
                    var content = await response.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Error<int, string>("Failed to upload symbol with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, content);
                    return false;
                }
            }

            return false;
        }
    }
}
