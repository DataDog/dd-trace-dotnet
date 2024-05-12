// <copyright file="HttpResponseHeadersWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Datadog.Trace.Iast.Wrappers;

internal class HttpResponseHeadersWrapper : ICollectionWrapper<KeyValuePair<string, StringValues>>, IHeaderDictionary
{
    internal HttpResponseHeadersWrapper(IHeaderDictionary originalHeaders)
        : base(() => originalHeaders)
    {
        Target = originalHeaders;
    }

    internal IHeaderDictionary Target { get; }

    public ICollection<string> Keys => Target.Keys;

    public ICollection<StringValues> Values
    {
        get { return new ICollectionWrapper<StringValues>(() => Target.Values); }
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
            return Target[key];
        }

        set
        {
            ReturnedHeadersAnalyzer.AnalyzeHeaderInjectionVulnerability(key, value, IntegrationId.AspNetCore);
            Target[key] = value;
        }
    }

    protected override IEnumerable<KeyValuePair<string, StringValues>> OnTargetEnumerable() => Target;

    public bool ContainsKey(string key) => Target.ContainsKey(key);

    public bool TryGetValue(string key, out StringValues value) => Target.TryGetValue(key, out value);

    public void Add(string key, StringValues value)
    {
        ReturnedHeadersAnalyzer.AnalyzeHeaderInjectionVulnerability(key, value, IntegrationId.AspNetCore);
        Target.Add(key, value);
    }

    public bool Remove(string key) => Target.Remove(key);
}
#endif
