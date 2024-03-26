// <copyright file="RpcHttpHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

internal readonly struct RpcHttpHeadersCollection<T> : IHeadersCollection
{
    private readonly RpcHttpStruct _rpcHttp;
    private readonly bool _useNullableHeaders;

    public RpcHttpHeadersCollection(RpcHttpStruct rpcHttp, bool useNullableHeaders)
    {
        _rpcHttp = rpcHttp;
        _useNullableHeaders = useNullableHeaders;
    }

    public void Set(string name, string value)
    {
        if (_useNullableHeaders)
        {
            // this conversion is a bit annoying, but necessary to avoid duplication
            _rpcHttp.NullableHeaders[name.ToLowerInvariant()] = NullableStringHelper<T>.CreateNullableString(value);
        }
        else
        {
            _rpcHttp.Headers[name.ToLowerInvariant()] = value;
        }
    }

    // These aren't used
    public IEnumerable<string> GetValues(string name) => throw new NotImplementedException();

    public void Add(string name, string value) => throw new NotImplementedException();

    public void Remove(string name) => throw new NotImplementedException();
}
#endif
