// <copyright file="IHttpRequestMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.AppSec.Rasp.HttpClient;

/// <summary>
/// HttpRequestMessage interface for ducktyping
/// </summary>
internal interface IHttpRequestMessage : IDuckType
{
    HttpMethodStruct Method { get; }

    Uri RequestUri { get; }

    IHttpHeaders Headers { get; }

    IHttpContent Content { get; }
}
