// <copyright file="AgentlessRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports;

internal class AgentlessRequest : IApiRequest
{
    private readonly string _path;
    private readonly NameValueCollection _headers;

    public AgentlessRequest(Uri endpoint, KeyValuePair<string, string>[] defaultHeaders)
    {
        _path = endpoint.AbsolutePath;
        _headers = new NameValueCollection();
        if (defaultHeaders != null)
        {
            foreach (var header in defaultHeaders)
            {
                _headers.Add(header.Key, header.Value);
            }
        }
    }

    public void AddHeader(string name, string value) => _headers.Add(name, value);

    public async Task<IApiResponse> GetAsync()
    {
        var result = await NativeAgentless.GetAsync(_path, _headers).ConfigureAwait(false);
        return new AgentlessResponse(result);
    }

    public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
    {
        var contentHeader = new NameValueCollection(_headers)
        {
            { "Content-Type", contentType }
        };

        var content = bytes.Array ?? [];
        if (content.Length != bytes.Count || bytes.Offset != 0)
        {
            content = bytes.ToArray();
        }

        var result = await NativeAgentless.PostAsync(_path, contentHeader, content).ConfigureAwait(false);
        return new AgentlessResponse(result);
    }

    public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
    {
        var contentHeader = new NameValueCollection(_headers)
        {
            { "Content-Type", contentType },
            { "Content-Encoding", contentEncoding }
        };

        var content = bytes.Array ?? [];
        if (content.Length != bytes.Count || bytes.Offset != 0)
        {
            content = bytes.ToArray();
        }

        var result = await NativeAgentless.PostAsync(_path, contentHeader, content).ConfigureAwait(false);
        return new AgentlessResponse(result);
    }

    public Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
    {
        throw new NotImplementedException();
    }
}
