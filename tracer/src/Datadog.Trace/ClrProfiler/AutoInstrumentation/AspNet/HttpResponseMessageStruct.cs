// <copyright file="HttpResponseMessageStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using Datadog.Trace.Internal.DuckTyping;

namespace Datadog.Trace.Internal.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// Http response struct copy target for ducktyping
    /// </summary>
    [DuckCopy]
    internal struct HttpResponseMessageStruct
    {
        public int StatusCode;
    }
}
#endif
