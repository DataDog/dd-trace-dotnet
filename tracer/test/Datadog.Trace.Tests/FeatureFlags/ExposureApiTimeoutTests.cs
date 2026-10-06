// <copyright file="ExposureApiTimeoutTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

[Collection(nameof(WebRequestCollection))]
public class ExposureApiTimeoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyExposureRequestsAbortStalledConnection(bool unixSocket)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var listener = new Socket(unixSocket ? AddressFamily.Unix : AddressFamily.InterNetwork, SocketType.Stream, unixSocket ? ProtocolType.Unspecified : ProtocolType.Tcp);
        listener.Bind(unixSocket ? new UnixDomainSocketEndPoint(path) : new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var uri = new Uri(unixSocket ? "http://localhost" : "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndPoint!).Port);
        IApiRequestFactory factory = unixSocket
                                         ? new HttpStreamRequestFactory(new UnixDomainSocketStreamFactory(path), new DatadogHttpClient(EventPlatformHeaderHelper.Instance), uri, requestTimeout: TimeSpan.FromSeconds(5))
                                         : new ApiWebRequestFactory(uri, EventPlatformHeaderHelper.Instance.DefaultHeaders, asyncTimeout: TimeSpan.FromSeconds(5));
        try
        {
            var send = factory.Create(factory.GetEndpoint(ExposureApi.ExposurePath)).PostAsJsonAsync(Observation("first"), MultipartCompression.GZip, ExposureApi.SerializerSettings);
            using var connection = await Within(listener.AcceptAsync(), 10);
            using var stream = new NetworkStream(connection);
            (await Within(ReadExposure(stream), 10)).Should().Contain("\"id\":\"first\"");
            (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(15)))).Should().BeSameAs(send);
            await Assert.ThrowsAnyAsync<Exception>(async () => await send);
            (await Within(stream.ReadAsync(new byte[1], 0, 1), 5)).Should().Be(0, "the timeout must close the real connection");
        }
        finally
        {
            if (unixSocket)
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StalledAgentConnectionIsClosedAndNextExposureCanBeSent(bool unixSocket)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var listener = new Socket(unixSocket ? AddressFamily.Unix : AddressFamily.InterNetwork, SocketType.Stream, unixSocket ? ProtocolType.Unspecified : ProtocolType.Tcp);
        listener.Bind(unixSocket ? new UnixDomainSocketEndPoint(path) : new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(2);
        var uri = unixSocket ? "unix://" + path : "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndPoint!).Port;
        using var api = new ExposureApi(new TracerSettings(new NameValueConfigurationSource(new NameValueCollection { { "DD_TRACE_AGENT_URL", uri } })));
        try
        {
            api.SendExposure(Observation("first"));
            using var first = await Within(listener.AcceptAsync(), 10);
            using var stream = new NetworkStream(first);
            (await Within(ReadExposure(stream), 10)).Should().Contain("\"id\":\"first\"");
            api.SendExposure(Observation("next"));

            // Read after the complete request body, but never send a response. EOF proves
            // the client cancelled the real I/O, rather than abandoning an awaiting task.
            (await Within(stream.ReadAsync(new byte[1], 0, 1), 15)).Should().Be(0);
            using var next = await Within(listener.AcceptAsync(), 15);
            using var nextStream = new NetworkStream(next);
            (await Within(ReadExposure(nextStream), 10)).Should().Contain("\"id\":\"next\"").And.NotContain("\"id\":\"first\"", "a timed-out exposure must not be replayed");
            var response = Encoding.ASCII.GetBytes("HTTP/1.1 202 Accepted\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await nextStream.WriteAsync(response, 0, response.Length);
        }
        finally
        {
            if (unixSocket)
            {
                File.Delete(path);
            }
        }
    }

    private static async Task<string> ReadExposure(Stream stream)
    {
        var request = await MockHttpParser.ReadRequest(stream);
        request.PathAndQuery.Should().Be("/" + ExposureApi.ExposurePath);
        var remaining = request.ContentLength;
        var buffer = new byte[4096];
        using var compressed = new MemoryStream();
        while (remaining is null || remaining > 0)
        {
            var read = await request.Body.ReadAsync(buffer, 0, remaining is { } length ? (int)Math.Min(length, buffer.Length) : buffer.Length);
            if (read == 0)
            {
                break;
            }

            compressed.Write(buffer, 0, read);
            if (remaining is not null)
            {
                remaining -= read;
            }
        }

        compressed.Position = 0;
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        return await reader.ReadToEndAsync();
    }

    private static async Task<T> Within<T>(Task<T> task, int seconds)
    {
        (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds)))).Should().BeSameAs(task);
        return await task;
    }

    private static ExposureEvent Observation(string subject) => new(
        1755000000000,
        new Allocation("allocation"),
        new Flag("flag"),
        new Variant("variant"),
        new Subject(subject, new Dictionary<string, object?>()),
        null);
}
#endif
