// <copyright file="IHttpContent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.AppSec.Rasp.HttpClient;

/// <summary>
/// HttpResponseMessage interface for ducktyping
/// </summary>
internal interface IHttpContent : IDuckType
{
    IHttpContentHeaders Headers { get; }

    bool TryComputeLength(out long length);

    Task LoadIntoBufferAsync();

    Task<string> ReadAsStringAsync();
}
