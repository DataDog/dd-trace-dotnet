// <copyright file="TracerFlareRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Util;

namespace Datadog.Trace.Logging.TracerFlare;

internal class TracerFlareRequestFactory
{
    internal const string Boundary = "--83CAD6AA-8A24-462C-8B3D-FF9CC683B51B";

    private const string RequestBodyPrefix =
        $$"""
          {{Boundary}}
          Content-Disposition: form-data; name="source"

          tracer_dotnet
          {{Boundary}}
          Content-Disposition: form-data; name="case_id"


          """;

    private const string RequestBodyMiddle1 =
        $$"""

          {{Boundary}}
          Content-Disposition: form-data; name="flare_file"; filename="tracer-dotnet-
          """;

    private const string RequestBodyMiddle2 =
        """
          -debug.zip"
          Content-Type: application/octet-stream


          """;

    private const string RequestBodySuffix =
        $$"""

          {{Boundary}}--
          """;

    public static Task WriteRequestBody(Stream destination, Func<Stream, Task> writeFlareBytes, string caseId)
        => WriteRequestBody(destination, writeFlareBytes, caseId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    public static async Task WriteRequestBody(Stream destination, Func<Stream, Task> writeFlareBytes, string caseId, long timestamp)
    {
        // Need to create a body that looks something like this:

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

        // Use the default buffer size, and the UTF8NoBOM encoding for the stream writer
        // In .NET Core 3+, if you pass null for the encoding you get UTF8NoBOM, but
        // using it everywhere here for consistency
        using var sw = new StreamWriter(destination, encoding: EncodingHelpers.Utf8NoBom, bufferSize: 1024, leaveOpen: true);
        await sw.WriteAsync(RequestBodyPrefix).ConfigureAwait(false);
        await sw.WriteAsync(caseId).ConfigureAwait(false);
        await sw.WriteAsync(RequestBodyMiddle1).ConfigureAwait(false);

        // write the filename as tracer-dotnet-caseid-timestamp-debug.zip
        // - tracer-dotnet to distinguish between different languages
        // - timestamp incase multiple tracer flares are attached
        // - caseid to make it easier to distinguish between the zip files when read
        await sw.WriteAsync(caseId).ConfigureAwait(false);
        await sw.WriteAsync('-').ConfigureAwait(false);
        await sw.WriteAsync(timestamp.ToString()).ConfigureAwait(false);

        await sw.WriteAsync(RequestBodyMiddle2).ConfigureAwait(false);

        await sw.FlushAsync().ConfigureAwait(false);

        await writeFlareBytes(destination).ConfigureAwait(false);

        await sw.WriteAsync(RequestBodySuffix).ConfigureAwait(false);
        // explicitly flush to avoid any potential sync-over-async from the disposal
        await sw.FlushAsync().ConfigureAwait(false);
    }
}
