// <copyright file="SymbolBatchUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Text;
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
        private readonly ArraySegment<byte> _eventMetadata;
        private string? _endpoint;
        private IDiscoveryService? _discoveryService;

        private SymbolBatchUploadApi(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService, ArraySegment<byte> eventMetadataMetadata)
        {
            _apiRequestFactory = apiRequestFactory;
            _eventMetadata = eventMetadataMetadata;
            _discoveryService = discoveryService;
            _discoveryService.SubscribeToChanges(ConfigurationChanged);
        }

        private static ArraySegment<byte> GetEventAsArraySegment(string? serviceName)
        {
            var sb = new StringBuilder();
            sb.Append(@"{");
            sb.Append(@"""ddsource"": ""dd_debugger"",");
            sb.Append(@$"""service"": ""{serviceName}"",");
            sb.Append(@$"""runtimeId"": ""{Tracer.RuntimeId}""");
            sb.Append(@"}");
            var eventMetadata = sb.ToString();

            var count = Encoding.UTF8.GetByteCount(eventMetadata);
            var eventAsBytes = new byte[count];
            Encoding.UTF8.GetBytes(eventMetadata, 0, eventMetadata.Length, eventAsBytes, 0);
            return new ArraySegment<byte>(eventAsBytes);
        }

        public static IBatchUploadApi Create(IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService, string? serviceName)
        {
            try
            {
                var eventMetadata = GetEventAsArraySegment(serviceName);
                return new SymbolBatchUploadApi(apiRequestFactory, discoveryService, eventMetadata);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to create {Class}. Creating instead {NoOpClass}.", nameof(SymbolBatchUploadApi), nameof(NoOpSymbolBatchUploadApi));
                return new NoOpSymbolBatchUploadApi();
            }
        }

        private void ConfigurationChanged(AgentConfiguration config)
        {
            _endpoint = config.SymbolDbEndpoint;
            if (!string.IsNullOrEmpty(Volatile.Read(ref _endpoint)))
            {
                _discoveryService!.RemoveSubscription(ConfigurationChanged);
                _discoveryService = null;
            }
        }

        public async Task<bool> SendBatchAsync(ArraySegment<byte> symbols)
        {
            if (Volatile.Read(ref _endpoint) is not { } endpoint)
            {
                // Should not happen
                throw new Exception("Symbol database endpoint is not defined");
            }

            var uri = _apiRequestFactory.GetEndpoint(endpoint);
            var request = _apiRequestFactory.Create(uri);
            var multipartRequest = (IMultipartApiRequest)request;

            const int maxRetries = 3;
            int retries = 0;
            int sleepDuration = 500;

            while (retries < maxRetries)
            {
                using var response = await multipartRequest.PostAsync(
                                         new MultipartFormItem("file", MimeTypes.Json, "file.json", symbols),
                                         new MultipartFormItem("event", MimeTypes.Json, "event.json", _eventMetadata)).
                                                            ConfigureAwait(false);

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
