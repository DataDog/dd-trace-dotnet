// <copyright file="IRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IRequest interface for ducktyping
    /// </summary>
    public interface IRequest
    {
        /// <summary>
        /// Gets the HTTP method
        /// </summary>
        string HttpMethod { get; }
    }
}
