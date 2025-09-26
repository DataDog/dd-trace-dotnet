// <copyright file="AgentlessResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports;

internal class AgentlessResponse : IApiResponse
{
    private readonly NameValueCollection _headers;
    private readonly byte[] _content;

    public AgentlessResponse(NativeAgentless.Response response)
    {
        StatusCode = response.Status;
        ContentLength = response.Body.Length;
        _headers = response.Headers;
        _content = response.Body;
    }

    public int StatusCode { get; }

    public long ContentLength { get; }

    public string? ContentTypeHeader => _headers.Get("Content-Type");

    public string? ContentEncodingHeader => _headers.Get("Content-Encoding");

    public string? GetHeader(string headerName) => _headers.Get(headerName);

    public Encoding GetCharsetEncoding() => ApiResponseExtensions.GetCharsetEncoding(ContentTypeHeader);

    public ContentEncodingType GetContentEncodingType() => ApiResponseExtensions.GetContentEncodingType(ContentEncodingHeader);

    public Task<Stream> GetStreamAsync()
    {
        return Task.FromResult<Stream>(new MemoryStream(_content));
    }

    public void Dispose()
    {
    }
}
