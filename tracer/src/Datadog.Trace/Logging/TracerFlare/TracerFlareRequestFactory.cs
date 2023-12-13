// <copyright file="TracerFlareRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
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

    public static ArraySegment<byte> GetRequestBody(ArraySegment<byte> flare, string caseId)
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

        // We could calculate _most_ of this byte count (and even the encoding itself)
        // ahead of time to save some overhead if we wanted
        var contentSize = Utf8.GetByteCount(RequestBodyPrefix)
                        + Utf8.GetByteCount(caseId)
                        + Utf8.GetByteCount(RequestBodyMiddle)
                        + flare.Count
                        + Utf8.GetByteCount(RequestBodySuffix);

        // TODO: this could be very big, should we use the unmanaged heap or something?
        var buffer = new byte[contentSize];
        using var ms = new MemoryStream(buffer);
        using var sw = new StreamWriter(ms, Utf8, bufferSize: contentSize, leaveOpen: true);
        sw.Write(RequestBodyPrefix);
        sw.Write(caseId);
        sw.Write(RequestBodyMiddle);
        sw.Flush();
        ms.Write(flare.Array!, flare.Offset, flare.Count); // right directly to the memory stream here
        sw.Write(RequestBodySuffix);

        return new ArraySegment<byte>(buffer);
    }
}
