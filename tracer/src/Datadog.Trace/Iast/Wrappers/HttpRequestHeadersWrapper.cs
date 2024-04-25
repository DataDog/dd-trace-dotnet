// <copyright file="HttpRequestHeadersWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Datadog.Trace.Iast.Wrappers;

internal class HttpRequestHeadersWrapper : ICollectionWrapper<KeyValuePair<string, StringValues>>, IHeaderDictionary
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpRequestHeadersWrapper>();

    internal HttpRequestHeadersWrapper(IHeaderDictionary originalHeaders)
    {
        Target = originalHeaders;
    }

    internal IHeaderDictionary Target { get; }

    private TaintedObjects? TaintedObjects { get; set; }

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
            Taint(key, value);
            Target[key] = value;
        }
    }

    protected override IEnumerable<KeyValuePair<string, StringValues>> OnTargetEnumerable() => Target;

    protected override void OnTaint(KeyValuePair<string, StringValues> obj)
    {
        Taint(obj.Key, obj.Value);
    }

    protected void Taint(string? key, StringValues values)
    {
        if (key == null) { return; }

        try
        {
            if (values.Count > 1)
            {
                foreach (var singleValue in values)
                {
                    TaintHeader(key, singleValue);
                }
            }
            else
            {
                TaintHeader(key, values);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error tainting header");
        }

        return;

        void TaintHeader(string name, string value)
        {
            TaintedObjects ??= IastModule.GetIastContext()?.GetTaintedObjects();
            TaintedObjects?.TaintInputString(value, new Source(SourceType.RequestHeaderValue, name, value));
            TaintedObjects?.TaintInputString(name, new Source(SourceType.RequestHeaderName, name, name));
        }
    }

    public bool ContainsKey(string key) => Target.ContainsKey(key);

    public bool TryGetValue(string key, out StringValues value)
    {
        var res = Target.TryGetValue(key, out value);
        if (res)
        {
            Taint(key, value);
        }

        return res;
    }

    public void Add(string key, StringValues value) => Target.Add(key, value);

    public bool Remove(string key) => Target.Remove(key);
}
#endif
