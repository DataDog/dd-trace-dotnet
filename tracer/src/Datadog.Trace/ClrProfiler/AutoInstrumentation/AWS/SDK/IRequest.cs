// <copyright file="IRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IRequest interface for ducktyping
    /// </summary>
    internal interface IRequest
    {
        /// <summary>
        /// Gets the service endpoint
        /// </summary>
        Uri? Endpoint { get; }

        /// <summary>
        /// Gets the HTTP method
        /// </summary>
        string? HttpMethod { get; }

        /// <summary>
        /// Gets the resource path
        /// </summary>
        string? ResourcePath { get; }
    }
}
