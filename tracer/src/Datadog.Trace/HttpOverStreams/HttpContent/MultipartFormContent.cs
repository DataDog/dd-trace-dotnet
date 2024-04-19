// <copyright file="MultipartFormContent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using static Datadog.Trace.HttpOverStreams.DatadogHttpValues;

namespace Datadog.Trace.HttpOverStreams.HttpContent;

/// <summary>
/// Provides an <see cref="HttpContent"/> implementation that exposes an output <see cref="Stream"/>
/// which can be written to directly. The ability to push data to the output stream differs from the
/// StreamContent where data is pulled and not pushed.
/// </summary>
internal class MultipartFormContent : IHttpContent
{
    private const string Header = $"""--{Boundary}{CrLf}""";
    private const string Footer = $"""--{Boundary}--{CrLf}""";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MultipartFormContent));

    private readonly MultipartFormItem[] _items;
    private readonly MultipartCompression _multipartCompression;

    public MultipartFormContent(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
    {
        _items = items;
        _multipartCompression = multipartCompression;
    }

    public long? Length => null;

    public async Task CopyToAsync(Stream destination)
    {
        // Note the callee must not close or dispose the stream because they don't own it
        // We _could_ use a wrapper stream to enforce that, but then it _requires_ the
        // callee to call close/dispose which seems weird

        // We're doing chunked encoding, so we need to wrap the destination stream
        // to ensure we add the required chunked encoding headers
        using var chunkedStream = new ChunkedEncodingWriteStream(destination);

        using (var compressionStream = GetCompressionStream(_multipartCompression, chunkedStream))
        {
            var innerStream = compressionStream ?? (Stream)chunkedStream;
            await WriteMultiPartForm(innerStream).ConfigureAwait(false);

            // in .NET Framework, calling Flush() doesn't flush the underlying stream
            // the only way to force it to flush is to dispose
        }

        await chunkedStream.FinishAsync().ConfigureAwait(false); // write the terminator block
        await chunkedStream.FlushAsync().ConfigureAwait(false); // flush the final chunk

        return;

        static GZipStream? GetCompressionStream(MultipartCompression compress, Stream innerStream)
            => compress switch
            {
                MultipartCompression.None => null,
                MultipartCompression.GZip => new GZipStream(innerStream, CompressionMode.Compress, leaveOpen: true),
                _ => throw new InvalidOperationException($"Unknown compression type: {compress}"),
            };
    }

    public Task CopyToAsync(byte[] buffer)
    {
        // This CopyToAsync overload is only used to read responses
        // And we never use PushStreamContent for that
        throw new NotImplementedException();
    }

    private async Task WriteMultiPartForm(Stream wrappedStream)
    {
        // Need to create a body that looks something like this:
        // (Plus potential GZip compression + then chunked encoding)

        // --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
        // Content-Disposition: form-data; name="source"
        //
        // tracer_dotnet
        // --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
        // Content-Disposition: form-data; name="case_id"
        //
        // 1234567
        // --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
        // Content-Disposition: form-data; name="flare_file"; filename="debug_logs.zip"
        // Content-Type: application/octet-stream
        //
        // <binary data>
        //
        // --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B--
        using var sw = new StreamWriter(wrappedStream, encoding: EncodingHelpers.Utf8NoBom, bufferSize: 1024, leaveOpen: true);

        var haveValidItem = false;
        foreach (var item in _items)
        {
            if (!item.IsValid(Log))
            {
                continue;
            }

            haveValidItem = true;
            await sw.WriteAsync(Header).ConfigureAwait(false);

            string partHeader;
            if (item.FileName is not null)
            {
                // Content-Type: text/plain"
                // Content-Disposition: form-data; name="flare_file"; filename="debug_logs.zip"
                partHeader =
                    $"""Content-Type: {item.ContentType}{CrLf}"""
                  + $"""Content-Disposition: form-data; name="{item.Name}"; filename="{item.FileName}"{CrLf}{CrLf}""";
            }
            else
            {
                // Content-Type: text/plain"
                // Content-Disposition: form-data; name="flare_file"
                partHeader =
                    $"""Content-Type: {item.ContentType}{CrLf}"""
                  + $"""Content-Disposition: form-data; name="{item.Name}"{CrLf}{CrLf}""";
            }

            await sw.WriteAsync(partHeader).ConfigureAwait(false);
            // Flush the part header to the underlying stream
            await sw.FlushAsync().ConfigureAwait(false);

            // Write the item content
            if (item.IsStream)
            {
                await item.ContentInStream!.CopyToAsync(wrappedStream).ConfigureAwait(false);
            }
            else
            {
                var arraySegment = item.ContentInBytes!.Value;
                await wrappedStream.WriteAsync(arraySegment.Array!, arraySegment.Offset, arraySegment.Count).ConfigureAwait(false);
            }

            await wrappedStream.FlushAsync().ConfigureAwait(false);
            // After the content, one more CRLF
            await sw.WriteAsync(CrLf).ConfigureAwait(false);
        }

        if (!haveValidItem)
        {
            // Add a boundary to make sure we have a valid multipart body, even though it won't have anything
            await sw.WriteAsync($"{Header}{CrLf}").ConfigureAwait(false);
        }

        // all done
        await sw.WriteAsync(Footer).ConfigureAwait(false);
        // explicitly flush to avoid any potential sync-over-async from the disposal
        await sw.FlushAsync().ConfigureAwait(false);
        await wrappedStream.FlushAsync().ConfigureAwait(false); // flush the underlying stream
    }
}
