// <copyright file="SymbolUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Upload
{
    internal sealed class SymbolUploadApi : DebuggerUploadApiBase
    {
        private const int MaxRetries = 3;
        private const int StartingSleepDuration = 3;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SymbolUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly Lazy<ArraySegment<byte>> _eventMetadata;
        private readonly bool _enableCompression;

        private SymbolUploadApi(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            Lazy<ArraySegment<byte>> eventMetadata,
            bool enableCompression)
            : base(apiRequestFactory, gitMetadataTagsProvider)
        {
            _apiRequestFactory = apiRequestFactory;
            _eventMetadata = eventMetadata;
            _enableCompression = enableCompression;
            discoveryService.SubscribeToChanges(c =>
            {
                Endpoint = c.SymbolDbEndpoint;
                Log.Debug("SymbolUploadApi: Updated endpoint to {Endpoint}", Endpoint);
            });
        }

        internal static IBatchUploadApi Create(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            Func<string> serviceNameProvider,
            bool enableCompression)
        {
            ArraySegment<byte> GetEventMetadataAsArraySegment()
            {
                // Capture service name on first use, and keep it fixed for the lifetime of this API instance
                var serviceName = serviceNameProvider();
                return CreateEventMetadata(serviceName, Tracer.RuntimeId);
            }

            var eventMetadata = new Lazy<ArraySegment<byte>>(GetEventMetadataAsArraySegment, LazyThreadSafetyMode.ExecutionAndPublication);
            return new SymbolUploadApi(apiRequestFactory, discoveryService, gitMetadataTagsProvider, eventMetadata, enableCompression);
        }

        internal static ArraySegment<byte> CreateEventMetadata(string serviceName, string runtimeId)
        {
            const int bufferSize = 256;
            using var stream = new MemoryStream(capacity: bufferSize);
            using (var streamWriter = new StreamWriter(stream, EncodingHelpers.Utf8NoBom, bufferSize: bufferSize, leaveOpen: true))
            using (var jsonWriter = new JsonTextWriter(streamWriter) { ArrayPool = JsonArrayPool.Shared })
            {
                jsonWriter.CloseOutput = false;
                jsonWriter.Formatting = Formatting.None;
                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("ddsource");
                jsonWriter.WriteValue(DebuggerTags.DDSource);

                jsonWriter.WritePropertyName("service");
                jsonWriter.WriteValue(serviceName);

                jsonWriter.WritePropertyName("runtimeId");
                jsonWriter.WriteValue(runtimeId);

                jsonWriter.WritePropertyName("debugger.type");
                jsonWriter.WriteValue(DebuggerTags.DebuggerType.SymDb);

                jsonWriter.WriteEndObject();
                jsonWriter.Flush();
            }

            // Avoid an extra copy when possible.
            return stream.TryGetBuffer(out var buffer)
                       ? new ArraySegment<byte>(buffer.Array!, buffer.Offset, (int)stream.Length)
                       : new ArraySegment<byte>(stream.ToArray());
        }

        public override async Task<bool> SendBatchAsync(ArraySegment<byte> symbols)
        {
            if (symbols.Array == null || symbols.Count == 0)
            {
                return false;
            }

            var uri = BuildUri();
            if (string.IsNullOrEmpty(uri))
            {
                Log.Warning("Symbol database endpoint is not defined");
                return false;
            }

            var request = _apiRequestFactory.Create(new Uri(uri));

            var retries = 0;
            var sleepDuration = StartingSleepDuration;

            MultipartFormItem symbolsItem;

            if (!_enableCompression)
            {
                symbolsItem = new MultipartFormItem("file", MimeTypes.Json, "file.json", symbols);
            }
            else
            {
                var compressedSymbols = await CompressDataAsync(symbols).ConfigureAwait(false);
                if (compressedSymbols == null)
                {
                    return false;
                }

                symbolsItem = new MultipartFormItem("file", MimeTypes.Gzip, "file.gz", compressedSymbols.Value);
            }

            var items = new[] { symbolsItem, new MultipartFormItem("event", MimeTypes.Json, "event.json", _eventMetadata.Value) };

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

        internal async Task<ArraySegment<byte>?> CompressDataAsync(ArraySegment<byte> data)
        {
            using var memoryStream = new MemoryStream();

#if NETFRAMEWORK
            using (var gzipStream = new Vendors.ICSharpCode.SharpZipLib.GZip.GZipOutputStream(memoryStream))
#else
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
#endif
            {
                await gzipStream.WriteAsync(data.Array!, data.Offset, data.Count).ConfigureAwait(false);
                await gzipStream.FlushAsync().ConfigureAwait(false);
            }

            var compressedData = memoryStream.ToArray();

            // see here about the following validation: https://forensics.wiki/gzip/
            // minimum size for header + footer
            if (compressedData.Length < 18)
            {
                Log.Error("Compression produced invalid data: size {Size} bytes is below minimum valid GZip size", property: compressedData.Length);
                return null;
            }

            // header magic numbers
            if (compressedData[0] != 0x1F || compressedData[1] != 0x8B)
            {
                Log.Error(
                    "Compression produced invalid data: invalid GZip header {Header}",
                    BitConverter.ToString(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Take(compressedData, 2))));

                return null;
            }

            return new ArraySegment<byte>(compressedData);
        }
    }
}
