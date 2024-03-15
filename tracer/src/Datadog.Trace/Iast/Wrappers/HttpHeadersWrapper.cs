// <copyright file="HttpHeadersWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// #nullable enable

#if NETCOREAPP

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Datadog.Trace.Iast.Wrappers;

internal class HttpHeadersWrapper : ICollectionWrapper<KeyValuePair<string, StringValues>>, IHeaderDictionary
{
    private readonly HttpRequest _request;

    internal HttpHeadersWrapper(HttpRequest request)
    {
        _request = request;
    }

    private IHeaderDictionary Target
    {
        get { return _request.Headers; }
    }

    public ICollection<string> Keys => Target.Keys;

    public ICollection<StringValues> Values
    {
        get
        {
            // "HeadersValues" is a placeholder for the taint key as it's mandatory to have a key
            return new ICollectionWrapper<StringValues>(() => Target.Values, o => Taint("HeadersValues", o));
        }
    }

    public long? ContentLength
    {
        get => Target.ContentLength;
        set => Target.ContentLength = value;
    }

    public StringValues this[string key]
    {
        get
        {
            var value = Target[key];
            Taint(key, value);
            return value;
        }

        set
        {
            Target[key] = value;
        }
    }

    protected override IEnumerable<KeyValuePair<string, StringValues>> OnTargetEnumerable() => Target;

    protected override void OnTaint(KeyValuePair<string, StringValues> obj)
    {
        Taint(obj.Key, obj.Value);
    }

    private void Taint(string key, StringValues values)
    {
        if (key != null)
        {
            // Todo:
            // request.Adapter.Taint(key, VulnerabilityOriginType.HEADER, values.ToArray());
        }
    }

    public bool ContainsKey(string key)
    {
        return Target.ContainsKey(key);
    }

    public bool TryGetValue(string key, out StringValues value)
    {
        var res = Target.TryGetValue(key, out value);
        if (res)
        {
            Taint(key, value);
        }

        return res;
    }

    public void Add(string key, StringValues value)
    {
        Target.Add(key, value);
    }

    public bool Remove(string key)
    {
        return Target.Remove(key);
    }
}
#endif
