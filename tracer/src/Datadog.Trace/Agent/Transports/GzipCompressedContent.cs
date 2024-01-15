// <copyright file="GzipCompressedContent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETCOREAPP
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent.Transports;

internal class GzipCompressedContent : HttpContent
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<GzipCompressedContent>();

    private readonly HttpContent _content;

    public GzipCompressedContent(HttpContent content)
    {
        // Copy original headers
        foreach (var header in content.Headers)
        {
            Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        Headers.ContentEncoding.Add("gzip");
        _content = content;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        Log.Debug("GZip compressing payload...");
        using var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
        await _content.CopyToAsync(gzip).ConfigureAwait(false);
        await gzip.FlushAsync().ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }
}

#endif
