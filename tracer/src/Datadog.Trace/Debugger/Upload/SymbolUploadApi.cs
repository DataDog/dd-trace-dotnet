// <copyright file="SymbolUploadApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Upload
{
    internal sealed class SymbolUploadApi : DebuggerUploadApiBase, ISymbolUploadApi
    {
        private const int MaxRetries = 3;
        private const int StartingSleepDuration = 3;

        private static readonly byte[] InitialBoundaryBytes = EncodingHelpers.Utf8NoBom.GetBytes("--" + DatadogHttpValues.Boundary + DatadogHttpValues.CrLf);
        private static readonly byte[] BoundaryBytes = EncodingHelpers.Utf8NoBom.GetBytes(DatadogHttpValues.CrLf + "--" + DatadogHttpValues.Boundary + DatadogHttpValues.CrLf);
        private static readonly byte[] FinalBoundaryBytes = EncodingHelpers.Utf8NoBom.GetBytes(DatadogHttpValues.CrLf + "--" + DatadogHttpValues.Boundary + "--" + DatadogHttpValues.CrLf);
        private static readonly byte[] FileJsonHeaderBytes = EncodingHelpers.Utf8NoBom.GetBytes("Content-Type: " + MimeTypes.Json + DatadogHttpValues.CrLf + "Content-Disposition: form-data; name=\"file\"; filename=\"file.json\"" + DatadogHttpValues.CrLf + DatadogHttpValues.CrLf);
        private static readonly byte[] FileGzipHeaderBytes = EncodingHelpers.Utf8NoBom.GetBytes("Content-Type: " + MimeTypes.Gzip + DatadogHttpValues.CrLf + "Content-Disposition: form-data; name=\"file\"; filename=\"file.gz\"" + DatadogHttpValues.CrLf + DatadogHttpValues.CrLf);
        private static readonly byte[] EventHeaderBytes = EncodingHelpers.Utf8NoBom.GetBytes("Content-Type: " + MimeTypes.Json + DatadogHttpValues.CrLf + "Content-Disposition: form-data; name=\"event\"; filename=\"event.json\"" + DatadogHttpValues.CrLf + DatadogHttpValues.CrLf);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SymbolUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly string _runtimeId;
        private readonly bool _enableCompression;

        private SymbolUploadApi(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            string runtimeId,
            bool enableCompression)
            : base(apiRequestFactory, gitMetadataTagsProvider)
        {
            _apiRequestFactory = apiRequestFactory;
            _runtimeId = runtimeId;
            _enableCompression = enableCompression;
            discoveryService.SubscribeToChanges(c =>
            {
                Endpoint = c.SymbolDbEndpoint;
                Log.Debug("SymbolUploadApi: Updated endpoint to {Endpoint}", Endpoint);
            });
        }

        internal static ISymbolUploadApi Create(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            bool enableCompression)
        {
            return new SymbolUploadApi(
                apiRequestFactory,
                discoveryService,
                gitMetadataTagsProvider,
                Tracer.RuntimeId,
                enableCompression);
        }

        internal static ArraySegment<byte> CreateEventMetadata(
            SymDbUploadMetadata metadata,
            string runtimeId,
            int attachmentSize)
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
                jsonWriter.WriteValue(metadata.Service);

                jsonWriter.WritePropertyName("version");
                jsonWriter.WriteValue(metadata.Version);

                jsonWriter.WritePropertyName("language");
                jsonWriter.WriteValue("dotnet");

                jsonWriter.WritePropertyName("runtimeId");
                jsonWriter.WriteValue(runtimeId);

                jsonWriter.WritePropertyName("type");
                jsonWriter.WriteValue(DebuggerTags.DebuggerType.SymDb);

                jsonWriter.WritePropertyName("uploadId");
                jsonWriter.WriteValue(metadata.UploadId.ToString());

                jsonWriter.WritePropertyName("batchNum");
                jsonWriter.WriteValue(metadata.BatchNum);

                jsonWriter.WritePropertyName("final");
                jsonWriter.WriteValue(metadata.Final);

                jsonWriter.WritePropertyName("attachmentSize");
                jsonWriter.WriteValue(attachmentSize);

                jsonWriter.WriteEndObject();
                jsonWriter.Flush();
            }

            // Avoid an extra copy when possible.
            return stream.TryGetBuffer(out var buffer)
                       ? new ArraySegment<byte>(buffer.Array!, buffer.Offset, (int)stream.Length)
                       : new ArraySegment<byte>(stream.ToArray());
        }

        public override Task<bool> SendBatchAsync(ArraySegment<byte> symbols)
        {
            // SymbolsUploader is the only caller and uses the typed overload
            // below (so that metadata matches what's stamped into the
            // attachment Root). The IBatchUploadApi.SendBatchAsync method is
            // retained for interface compatibility only.
            throw new NotSupportedException(
                "Use SendBatchAsync(symbols, metadata) for SymDB uploads.");
        }

        public async Task<bool> SendBatchAsync(ArraySegment<byte> symbols, SymDbUploadMetadata metadata)
        {
            if (symbols.Array == null || symbols.Count == 0)
            {
                return false;
            }

            return await SendBatchAsync(
                       stream => stream.WriteAsync(symbols.Array, symbols.Offset, symbols.Count),
                       metadata)
                  .ConfigureAwait(false);
        }

        public async Task<bool> SendBatchAsync(Func<Stream, Task> writeSymbols, SymDbUploadMetadata metadata)
        {
            if (writeSymbols == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(writeSymbols));
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

            while (retries < MaxRetries)
            {
                using var response = await request
                                           .PostAsync(
                                                stream => WriteMultipartFormData(stream, writeSymbols, metadata),
                                                MimeTypes.MultipartFormData,
                                                contentEncoding: null,
                                                DatadogHttpValues.Boundary)
                                           .ConfigureAwait(false);
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

        private async Task WriteMultipartFormData(Stream destination, Func<Stream, Task> writeSymbols, SymDbUploadMetadata metadata)
        {
            await destination.WriteAsync(InitialBoundaryBytes, 0, InitialBoundaryBytes.Length).ConfigureAwait(false);
            var fileHeaderBytes = _enableCompression ? FileGzipHeaderBytes : FileJsonHeaderBytes;
            await destination.WriteAsync(fileHeaderBytes, 0, fileHeaderBytes.Length).ConfigureAwait(false);

            var countingStream = new CountingWriteStream(destination);
            if (_enableCompression)
            {
#if NETFRAMEWORK
                using (var gzipStream = new Vendors.ICSharpCode.SharpZipLib.GZip.GZipOutputStream(countingStream) { IsStreamOwner = false })
#elif NETCOREAPP
                var gzipStream = new GZipStream(countingStream, CompressionMode.Compress, leaveOpen: true);
                await using (gzipStream.ConfigureAwait(false))
#else
                using (var gzipStream = new GZipStream(countingStream, CompressionMode.Compress, leaveOpen: true))
#endif
                {
                    await writeSymbols(gzipStream).ConfigureAwait(false);
                    await gzipStream.FlushAsync().ConfigureAwait(false);
                }
            }
            else
            {
                await writeSymbols(countingStream).ConfigureAwait(false);
                await countingStream.FlushAsync().ConfigureAwait(false);
            }

            var eventMetadata = CreateEventMetadata(metadata, _runtimeId, checked((int)countingStream.BytesWritten));

            await destination.WriteAsync(BoundaryBytes, 0, BoundaryBytes.Length).ConfigureAwait(false);
            await destination.WriteAsync(EventHeaderBytes, 0, EventHeaderBytes.Length).ConfigureAwait(false);
            await destination.WriteAsync(eventMetadata.Array!, eventMetadata.Offset, eventMetadata.Count).ConfigureAwait(false);
            await destination.WriteAsync(FinalBoundaryBytes, 0, FinalBoundaryBytes.Length).ConfigureAwait(false);
            await destination.FlushAsync().ConfigureAwait(false);
        }

        private sealed class CountingWriteStream : Stream
        {
            private readonly Stream _inner;

            public CountingWriteStream(Stream inner)
            {
                _inner = inner;
            }

            public long BytesWritten { get; private set; }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => _inner.CanWrite;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
                _inner.Flush();
            }

            public override Task FlushAsync(System.Threading.CancellationToken cancellationToken)
            {
                return _inner.FlushAsync(cancellationToken);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count);
                BytesWritten += count;
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
            {
                await _inner.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                BytesWritten += count;
            }

#if NETCOREAPP
            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, System.Threading.CancellationToken cancellationToken = default)
            {
                await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
                BytesWritten += buffer.Length;
            }
#endif
        }
    }
}
