// <copyright file="TracerFlareRequestFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging.TracerFlare;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.TracerFlare;

public class TracerFlareRequestFactoryTests
{
    [Fact]
    public async Task GetRequestBody_GeneratesExpectedRequest()
    {
        var flareBytes = new byte[50];
        for (var i = 0; i < flareBytes.Length; i++)
        {
            flareBytes[i] = 43;
        }

        using var flare = new MemoryStream(flareBytes);

        using var requestStream = new MemoryStream();

        var caseId = "12345";
        var hostname = "my.hostname";
        var email = "some.person@datadoghq.com";
        var timestamp = 1703093253;

        await TracerFlareRequestFactory.WriteRequestBody(
            requestStream,
            stream => flare.CopyToAsync(stream),
            caseId: caseId,
            hostname: hostname,
            email: email,
            timestamp);

        var deserializedBytes = Encoding.UTF8.GetString(requestStream.ToArray());

        // Multipart form data _must_ use CRLF, so ensure we are.
        var expected = ReplaceLineEndings(
                               """
                               --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
                               Content-Disposition: form-data; name="source"

                               tracer_dotnet
                               --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
                               Content-Disposition: form-data; name="case_id"

                               12345
                               --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
                               Content-Disposition: form-data; name="hostname"

                               my.hostname
                               --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
                               Content-Disposition: form-data; name="email"

                               some.person@datadoghq.com
                               --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
                               Content-Disposition: form-data; name="flare_file"; filename="tracer-dotnet-12345-1703093253-debug.zip"
                               Content-Type: application/octet-stream

                               ++++++++++++++++++++++++++++++++++++++++++++++++++
                               --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B--
                               """)
                          .Trim() + "\r\n"; // make sure with a crlf at the end

        deserializedBytes.Should().Be(expected);
    }

    private static string ReplaceLineEndings(string value)
    {
#if NET6_0_OR_GREATER
        return value.ReplaceLineEndings("\r\n");
#else
        return value
              .Replace("\r\n", "\n")
              .Replace("\n\r", "\n")
              .Replace("\r", "\n")
              .Replace("\n", "\r\n");
#endif
    }
}
