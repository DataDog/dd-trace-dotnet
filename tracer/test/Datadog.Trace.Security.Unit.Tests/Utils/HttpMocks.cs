// <copyright file="HttpMocks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Rasp.HttpClient;
using Moq;

namespace Datadog.Trace.Security.Unit.Tests.Utils;

internal static class HttpMocks
{
    public static Mock<IHttpRequestMessage> CreateMockRequest(
        string url,
        string method,
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null)
    {
        var requestMock = new Mock<IHttpRequestMessage>();
        requestMock.Setup(r => r.RequestUri).Returns(new Uri(url));
        requestMock.Setup(r => r.Method).Returns(new ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpMethodStruct { Method = method });

        var headersMock = CreateMockHeaders(headers ?? []);
        requestMock.Setup(r => r.Headers).Returns(headersMock.Object);

        if (body is not null)
        {
            var contentMock = CreateMockContent(body, contentType ?? "application/json");
            requestMock.Setup(r => r.Content).Returns(contentMock.Object);
        }
        else
        {
            requestMock.Setup(r => r.Content).Returns((IHttpContent)null!);
        }

        return requestMock;
    }

    public static Mock<IHttpResponseMessage> CreateMockResponse(
        int statusCode,
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null)
    {
        var responseMock = new Mock<IHttpResponseMessage>();
        responseMock.Setup(r => r.StatusCode).Returns(statusCode);

        var headersMock = CreateMockHeaders(headers ?? []);
        responseMock.Setup(r => r.Headers).Returns(headersMock.Object);

        if (body is not null)
        {
            var contentMock = CreateMockContent(body, contentType ?? "application/json");
            responseMock.Setup(r => r.Content).Returns(contentMock.Object);
        }
        else
        {
            responseMock.Setup(r => r.Content).Returns((IHttpContent)null!);
        }

        return responseMock;
    }

    public static Mock<IHttpContent> CreateMockContent(string body, string contentType, long? length = null)
    {
        var contentMock = new Mock<IHttpContent>();

        length??= body.Length;
        contentMock.Setup(c => c.TryComputeLength(out It.Ref<long>.IsAny))
            .Returns(new TryComputeLengthCallback((out long len) =>
            {
                len = length.Value;
                return true;
            }));

        contentMock.Setup(c => c.LoadIntoBufferAsync()).Returns(Task.CompletedTask);
        contentMock.Setup(c => c.ReadAsStringAsync()).Returns(Task.FromResult(body));

        byte[] byteArray = Encoding.UTF8.GetBytes(body);
        var stream = new MemoryStream(byteArray);
        contentMock.Setup(c => c.ReadAsStreamAsync()).Returns(Task.FromResult((Stream)stream));

        var mediaTypeMock = new Mock<IMediaTypeHeaderValue>();
        mediaTypeMock.Setup(m => m.MediaType).Returns(contentType);

        var contentHeadersMock = new Mock<IHttpContentHeaders>();
        contentHeadersMock.Setup(h => h.ContentType).Returns(mediaTypeMock.Object);

        contentMock.Setup(c => c.Headers).Returns(contentHeadersMock.Object);

        // Setup Instance property for duck typing
        var mockObject = new object();
        contentMock.Setup(c => c.Instance).Returns(mockObject);

        return contentMock;
    }

    public static Mock<IHttpHeaders> CreateMockHeaders(Dictionary<string, string> headers)
    {
        var headersMock = new Mock<IHttpHeaders>();

        var headerPairs = headers.Select(kvp =>
            new KeyValuePair<string, IEnumerable<string>>(kvp.Key, new[] { kvp.Value })).ToList();

        headersMock.Setup(h => h.GetEnumerator())
            .Returns(headerPairs.GetEnumerator());

        return headersMock;
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    private delegate bool TryComputeLengthCallback(out long length);
#pragma warning restore SA1201 // Elements should appear in the correct order
}
