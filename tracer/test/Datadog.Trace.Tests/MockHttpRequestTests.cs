// <copyright file="MockHttpRequestTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Compression;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class MockHttpRequestTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void GzipDiagnosticsPreserveTheBodyAndFailure(bool validGzip, bool contentLengthKnown)
    {
        byte[] message = [1, 2, 3, 4];
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(message, 0, message.Length);
        }

        var body = validGzip ? compressed.ToArray() : message;
        using var stream = new MemoryStream(body);
        var headers = new MockHttpRequest.MockHeaders();
        headers.Add("Content-Encoding", "gzip");
        headers.Add("Authorization", "must-not-be-captured");
        var request = new MockHttpRequest { Body = stream, Headers = headers, ContentLength = contentLengthKnown ? body.Length : null, PathAndQuery = "/evp_proxy/v4/api/v2/citestcycle" };
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            if (validGzip)
            {
                request.ReadStreamBody(directory).Should().Equal(message);
                Directory.Exists(directory).Should().BeFalse();
            }
            else
            {
                Action read = () => request.ReadStreamBody(directory);
                read.Should().Throw<InvalidDataException>();
                File.ReadAllBytes(Assert.Single(Directory.GetFiles(directory, "*.bin"))).Should().Equal(body);
                var metadata = File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "*.txt")));
                metadata.Should().Contain("Content-Encoding: gzip").And.Contain("Received bytes: 4").And.NotContain("must-not-be-captured");
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
