// <copyright file="TestApiResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;

namespace Datadog.Trace.TestHelpers.TransportHelpers;

internal class TestApiResponse : IApiResponse
{
    private readonly string _body;

    public TestApiResponse(int statusCode, string body, string contentType)
    {
        StatusCode = statusCode;
        _body = body;
        ContentTypeHeader = contentType;
    }

    public string ContentTypeHeader { get; }

    public string ContentEncodingHeader { get; set; }

    public int StatusCode { get; }

    public long ContentLength => _body?.Length ?? 0;

    public Encoding GetCharsetEncoding() => Encoding.UTF8;

    public ContentEncodingType GetContentEncodingType() => ApiResponseExtensions.GetContentEncodingType(ContentEncodingHeader);

    public void Dispose()
    {
    }

    public string GetHeader(string headerName) => throw new NotImplementedException();

    public Task<Stream> GetStreamAsync()
    {
        return Task.FromResult((Stream)new MemoryStream(GetCharsetEncoding().GetBytes(_body)));
    }

    public Task<string> ReadAsStringAsync() => Task.FromResult(_body);
}
