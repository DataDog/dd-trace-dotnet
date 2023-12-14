// <copyright file="TracerFlareRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using static Datadog.Trace.Logging.TracerFlare.EncodingHelpers;

namespace Datadog.Trace.Logging.TracerFlare;

internal class TracerFlareRequestFactory
{
    private const string Boundary = "--83CAD6AA-8A24-462C-8B3D-FF9CC683B51B";

    private const string RequestBodyPrefix =
        $$"""
          {{Boundary}}
          Content-Disposition: form-data; name="source"

          tracer_dotnet
          {{Boundary}}
          Content-Disposition: form-data; name="case_id"


          """;

    private const string RequestBodyMiddle =
        $$"""

          {{Boundary}}
          Content-Disposition: form-data; name="flare_file"; filename="debug_logs.zip"
          Content-Type: application/octet-stream


          """;

    private const string RequestBodySuffix =
        $$"""

          {{Boundary}}--
          """;

    public static async Task WriteRequestBody(Stream destination, Func<Stream, Task> writeFlareBytes, string caseId)
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

        // Use the default buffer size for the stream writer
        using var sw = new StreamWriter(destination, Utf8NoBom, bufferSize: 1024, leaveOpen: true);
        await sw.WriteAsync(RequestBodyPrefix).ConfigureAwait(false);
        await sw.WriteAsync(caseId).ConfigureAwait(false);
        await sw.WriteAsync(RequestBodyMiddle).ConfigureAwait(false);
        await sw.FlushAsync().ConfigureAwait(false);

        await writeFlareBytes(destination).ConfigureAwait(false);

        await sw.WriteAsync(RequestBodySuffix).ConfigureAwait(false);
        // explicitly flush to avoid any potential sync-over-async from the disposal
        await sw.FlushAsync().ConfigureAwait(false);
    }
}
