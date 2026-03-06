// <copyright file="IHttpResponseMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.AppSec.Rasp.HttpClient;

/// <summary>
/// HttpResponseMessage interface for ducktyping
/// </summary>
internal interface IHttpResponseMessage : IDuckType
{
    /// <summary>
    /// Gets the status code of the http response
    /// </summary>
    int StatusCode { get; }

    IHttpRequestMessage RequestMessage { get; }

    IHttpHeaders Headers { get; }

    IHttpContent Content { get; }
}
