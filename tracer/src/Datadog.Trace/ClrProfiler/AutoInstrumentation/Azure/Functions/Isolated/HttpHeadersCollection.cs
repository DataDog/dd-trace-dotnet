// <copyright file="HttpHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using Datadog.Trace.Headers;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

internal readonly struct HttpHeadersCollection : IHeadersCollection
{
    private readonly HttpHeaders _headers;

    public HttpHeadersCollection(HttpHeaders headers)
    {
        if (headers is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(headers));
        }

        _headers = headers;
    }

    public IEnumerable<string> GetValues(string name)
    {
        if (_headers.TryGetValues(name, out var values))
        {
            return values ?? Enumerable.Empty<string>();
        }

        return Enumerable.Empty<string>();
    }

    public void Set(string name, string value)
    {
        _headers.Remove(name);
        _headers.Add(name, value);
    }

    public void Add(string name, string value)
    {
        _headers.Add(name, value);
    }

    public void Remove(string name)
    {
        _headers.Remove(name);
    }
}
#endif
