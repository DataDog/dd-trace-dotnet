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
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger.Upload
{
    internal sealed class SymbolUploadApi : DebuggerUploadApiBase, ISymbolUploadApi
    {
        private const int MaxRetries = 3;
        private const int FailureLogInterval = 10;
        private const int StaticMultipartBufferSize = 256;
        private static readonly TimeSpan StartingSleepDuration = TimeSpan.FromSeconds(3);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SymbolUploadApi>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly IDiscoveryService _discoveryService;
        private readonly Action<AgentConfiguration> _discoveryCallback;
        private readonly string _runtimeId;
        private readonly bool _enableCompression;
        private readonly Func<TimeSpan, Task> _delayAsync;
        private int _uploadFailureCount;

        private SymbolUploadApi(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            string runtimeId,
            bool enableCompression,
            Func<TimeSpan, Task>? delayAsync = null)
            : base(apiRequestFactory, gitMetadataTagsProvider)
        {
            _apiRequestFactory = apiRequestFactory;
            _discoveryService = discoveryService;
            _runtimeId = runtimeId;
            _enableCompression = enableCompression;
            _delayAsync = delayAsync ?? Task.Delay;
            _discoveryCallback = OnDiscoveryServiceChanged;
            discoveryService.SubscribeToChanges(_discoveryCallback);
        }

        private static ReadOnlySpan<byte> InitialBoundaryBytes => "--faa0a896-8bc8-48f3-b46d-016f2b15a884\r\n"u8;

        private static ReadOnlySpan<byte> BoundaryBytes => "\r\n--faa0a896-8bc8-48f3-b46d-016f2b15a884\r\n"u8;

        private static ReadOnlySpan<byte> FinalBoundaryBytes => "\r\n--faa0a896-8bc8-48f3-b46d-016f2b15a884--\r\n"u8;

        private static ReadOnlySpan<byte> FileJsonHeaderBytes => "Content-Type: application/json\r\nContent-Disposition: form-data; name=\"file\"; filename=\"file.json\"\r\n\r\n"u8;

        private static ReadOnlySpan<byte> FileGzipHeaderBytes => "Content-Type: application/gzip\r\nContent-Disposition: form-data; name=\"file\"; filename=\"file.gz\"\r\n\r\n"u8;

        private static ReadOnlySpan<byte> EventHeaderBytes => "Content-Type: application/json\r\nContent-Disposition: form-data; name=\"event\"; filename=\"event.json\"\r\n\r\n"u8;

        internal static ISymbolUploadApi Create(
            IApiRequestFactory apiRequestFactory,
            IDiscoveryService discoveryService,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            bool enableCompression,
            Func<TimeSpan, Task>? delayAsync = null)
        {
            return new SymbolUploadApi(
                apiRequestFactory,
                discoveryService,
                gitMetadataTagsProvider,
                Tracer.RuntimeId,
                enableCompression,
                delayAsync);
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

        public async Task<bool> SendBatchAsync<TState>(Func<Stream, TState, Task> writeSymbols, TState state, SymDbUploadMetadata metadata)
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

            var retries = 0;
            var endpoint = new Uri(uri);

            while (retries < MaxRetries)
            {
                var request = _apiRequestFactory.Create(endpoint);
                using (var response = await request
                           .PostAsync(
                                stream => WriteMultipartFormData(stream, writeSymbols, state, metadata),
                                MimeTypes.MultipartFormData,
                                contentEncoding: null,
                                DatadogHttpValues.Boundary)
                           .ConfigureAwait(false))
                {
                    if (response.StatusCode is >= 200 and <= 299)
                    {
                        return true;
                    }

                    retries++;
                    var shouldRetry = response.ShouldRetry();
                    if (!shouldRetry)
                    {
                        var failureCount = Interlocked.Increment(ref _uploadFailureCount);
                        await LogUploadFailureAsync(response, endpoint, metadata, failureCount).ConfigureAwait(false);

                        return false;
                    }

                    if (retries >= MaxRetries)
                    {
                        var failureCount = Interlocked.Increment(ref _uploadFailureCount);
                        await LogUploadFailureAsync(response, endpoint, metadata, failureCount).ConfigureAwait(false);

                        return false;
                    }

                    Log.Debug<int, int, Uri, Guid, long>("Retrying symbol database upload after retryable response status code {StatusCode}; attempt {Attempt}, endpoint {Endpoint}, uploadId {UploadId}, batchNum {BatchNum}", response.StatusCode, retries, endpoint, metadata.UploadId, metadata.BatchNum);
                    await _delayAsync(GetRetryDelay(retries)).ConfigureAwait(false);
                }
            }

            return false;
        }

        private static bool ShouldLogFailureAsError(int failureCount)
        {
            return failureCount == 1 || failureCount % FailureLogInterval == 0;
        }

        private static Task LogUploadFailureAsync(IApiResponse response, Uri endpoint, SymDbUploadMetadata metadata, int failureCount)
        {
            var shouldLogError = ShouldLogFailureAsError(failureCount);
            var isDebugEnabled = Log.IsEnabled(LogEventLevel.Debug);
            if (!shouldLogError && !isDebugEnabled)
            {
                return Task.CompletedTask;
            }

            return LogUploadFailureCoreAsync(response, endpoint, metadata, failureCount, shouldLogError);
        }

        private static async Task LogUploadFailureCoreAsync(IApiResponse response, Uri endpoint, SymDbUploadMetadata metadata, int failureCount, bool shouldLogError)
        {
            var content = await response.ReadAsStringAsync().ConfigureAwait(false);
            if (shouldLogError)
            {
                Log.Error<int, string, int>("Symbol database upload failed with status code {StatusCode} and message: {ResponseContent}; failure count {FailureCount}", response.StatusCode, content, failureCount);
            }
            else
            {
                Log.Debug<int, string, Uri, Guid, long>("Symbol database upload failed with status code {StatusCode} and message: {ResponseContent}; endpoint {Endpoint}, uploadId {UploadId}, batchNum {BatchNum}", response.StatusCode, content, endpoint, metadata.UploadId, metadata.BatchNum);
            }
        }

        private static TimeSpan GetRetryDelay(int retryAttempt)
        {
            return TimeSpan.FromSeconds(StartingSleepDuration.TotalSeconds * Math.Pow(2, retryAttempt - 1));
        }

        private static Task WriteStaticBytesAsync(Stream destination, ReadOnlySpan<byte> source, byte[] buffer)
        {
            source.CopyTo(buffer);
            return destination.WriteAsync(buffer, 0, source.Length);
        }

        private void OnDiscoveryServiceChanged(AgentConfiguration configuration)
        {
            if (string.IsNullOrEmpty(configuration.SymbolDbEndpoint))
            {
                return;
            }

            Endpoint = configuration.SymbolDbEndpoint;
            _discoveryService.RemoveSubscription(_discoveryCallback);
            Log.Debug("SymbolUploadApi: Updated endpoint to {Endpoint}", Endpoint);
        }

        private async Task WriteMultipartFormData<TState>(Stream destination, Func<Stream, TState, Task> writeSymbols, TState state, SymDbUploadMetadata metadata)
        {
            var staticBuffer = ArrayPool<byte>.Shared.Rent(StaticMultipartBufferSize);
            try
            {
                await WriteStaticBytesAsync(destination, InitialBoundaryBytes, staticBuffer).ConfigureAwait(false);
                await WriteStaticBytesAsync(destination, _enableCompression ? FileGzipHeaderBytes : FileJsonHeaderBytes, staticBuffer).ConfigureAwait(false);

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
                        await writeSymbols(gzipStream, state).ConfigureAwait(false);
                        await gzipStream.FlushAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    await writeSymbols(countingStream, state).ConfigureAwait(false);
                    await countingStream.FlushAsync().ConfigureAwait(false);
                }

                var eventMetadata = CreateEventMetadata(metadata, _runtimeId, checked((int)countingStream.BytesWritten));

                await WriteStaticBytesAsync(destination, BoundaryBytes, staticBuffer).ConfigureAwait(false);
                await WriteStaticBytesAsync(destination, EventHeaderBytes, staticBuffer).ConfigureAwait(false);
                await destination.WriteAsync(eventMetadata.Array!, eventMetadata.Offset, eventMetadata.Count).ConfigureAwait(false);
                await WriteStaticBytesAsync(destination, FinalBoundaryBytes, staticBuffer).ConfigureAwait(false);
                await destination.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(staticBuffer);
            }
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
