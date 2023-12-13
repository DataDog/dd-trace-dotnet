// <copyright file="TracerFlareRequestFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Logging.TracerFlare;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.TracerFlare;

public class TracerFlareRequestFactoryTests
{
    [Fact]
    public void GetRequestBody_GeneratesExpectedRequest()
    {
        var flare = new byte[50];
        for (var i = 0; i < flare.Length; i++)
        {
            flare[i] = 43;
        }

        var caseId = "12345";

        var bytes = TracerFlareRequestFactory.GetRequestBody(new ArraySegment<byte>(flare), caseId);

        var deserializedBytes = Encoding.UTF8.GetString(bytes.Array!, bytes.Offset, bytes.Count);

        deserializedBytes.Should().Be("""
                                      --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
                                      Content-Disposition: form-data; name="source"

                                      tracer_dotnet
                                      --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
                                      Content-Disposition: form-data; name="case_id"

                                      12345
                                      --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B
                                      Content-Disposition: form-data; name="flare_file"; filename="debug_logs.zip"
                                      Content-Type: application/octet-stream

                                      ++++++++++++++++++++++++++++++++++++++++++++++++++
                                      --83CAD6AA-8A24-462C-8B3D-FF9CC683B51B--
                                      """);
    }
}
