// <copyright file="RpcHttpStruct.cs" company="Datadog">
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
/// </summary>
[DuckCopy]
internal struct RpcHttpStruct
{
    /// <summary>
    /// An IDictionary&lt;string, NullableString&gt;
    /// </summary>
    public IDictionary NullableHeaders;
    public IDictionary<string, string> Headers;
}

#endif
