// <copyright file="IHttpResponseMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient
{
    /// <summary>
    /// HttpResponseMessage interface for ducktyping
    /// </summary>
    public interface IHttpResponseMessage : IDuckType
    {
        /// <summary>
        /// Gets the status code of the http response
        /// </summary>
        int StatusCode { get; }
    }
}
