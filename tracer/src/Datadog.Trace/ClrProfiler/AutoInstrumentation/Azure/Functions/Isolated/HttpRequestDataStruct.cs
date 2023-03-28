// <copyright file="HttpRequestDataStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System.Net.Http.Headers;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Duck type for https://github.com/Azure/azure-functions-dotnet-worker/blob/0fd7bf6aef005e4b8a14874506bf7a8ad7ad73ef/src/DotNetWorker.Core/Http/HttpHeadersCollection.cs#L13
/// </summary>
[DuckCopy]
internal struct HttpRequestDataStruct
{
    public HttpHeaders Headers;
}
#endif
