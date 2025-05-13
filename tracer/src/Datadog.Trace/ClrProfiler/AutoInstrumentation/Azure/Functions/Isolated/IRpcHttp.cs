// <copyright file="IRpcHttp.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Duck type for RpcHttp
/// This can't be a [DuckCopy] struct, because we set the Http property on TypedData to an instance of RpcHttp
/// and a [DuckCopy] struct is _purely_ for extracting properites
/// </summary>
internal interface IRpcHttp
{
    /// <summary>
    /// Gets an IDictionary&lt;string, NullableString&gt;
    /// </summary>
    public IDictionary NullableHeaders { get; }

    public IDictionary<string, string> Headers { get; }
}

#endif
