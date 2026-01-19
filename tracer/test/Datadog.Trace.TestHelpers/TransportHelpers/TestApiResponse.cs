// <copyright file="TestApiResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;

namespace Datadog.Trace.TestHelpers.TransportHelpers;

internal class TestApiResponse : IApiResponse
{
    private readonly string _body;
    private readonly Dictionary<string, string> _headers;

    public TestApiResponse(int statusCode, string body, string contentType, Dictionary<string, string> headers = null)
    {
        StatusCode = statusCode;
        _body = body;
        _headers = headers;
        ContentTypeHeader = contentType;
        _headers = headers ?? new Dictionary<string, string>();
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

    public string GetHeader(string headerName)
        => _headers?.TryGetValue(headerName, out var headerValue) == true ? headerValue : null;

    public Task<Stream> GetStreamAsync()
    {
        return Task.FromResult((Stream)new MemoryStream(GetCharsetEncoding().GetBytes(_body)));
    }

    public Task<string> ReadAsStringAsync() => Task.FromResult(_body);
}
