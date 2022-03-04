// <copyright file="HttpResponseStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NET461

using System.Net.Http.Headers;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcNetClient
{
    /// <summary>
    /// Duck type for HttpResponse. Required to access TrailingHeaders in .NET Core 3.0+
    /// (not available in .NET Standard 2.0)
    /// </summary>
    [DuckCopy]
    internal struct HttpResponseStruct
    {
        public HttpResponseHeaders? TrailingHeaders;
    }
}
#endif
