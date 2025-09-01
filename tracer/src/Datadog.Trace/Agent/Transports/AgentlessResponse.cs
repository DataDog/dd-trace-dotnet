// <copyright file="AgentlessResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if NETCOREAPP
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports;

internal class AgentlessResponse : IApiResponse
{
    private readonly IReadOnlyDictionary<string, string> _headers;
    private readonly Stream _stream;

    public AgentlessResponse(int statusCode, IReadOnlyDictionary<string, string> headers, Stream contentStream)
    {
        StatusCode = statusCode;
        _headers = headers ?? new Dictionary<string, string>();
        _stream = contentStream ?? Stream.Null;
        ContentTypeHeader = string.Empty;
        ContentEncoding = string.Empty;
        ContentEncodingHeader = string.Empty;
    }

    public int StatusCode { get; }

    public long ContentLength => _stream.Length;

    public string ContentTypeHeader { get; }

    public string ContentEncoding { get; }

    public string ContentEncodingHeader { get; }

    public void Dispose()
    {
    }

    public string GetHeader(string headerName)
    {
        if (_headers.TryGetValue(headerName, out var headerValue))
        {
            return headerValue;
        }

        return string.Empty;
    }

    public Encoding GetCharsetEncoding()
    {
        return Encoding.UTF8;
    }

    public ContentEncodingType GetContentEncodingType()
    {
        return ContentEncodingType.None;
    }

    public Task<Stream> GetStreamAsync()
    {
        return Task.FromResult(_stream);
    }
}
#endif
