// <copyright file="IResponseMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// ResponseMetadata interface for ducktyping
    /// </summary>
    internal interface IResponseMetadata
    {
        /// <summary>
        /// Gets the ID of the request
        /// </summary>
        string? RequestId { get; }

        /// <summary>
        /// Gets the metadata associated with the request
        /// </summary>
        IDictionary<string, string> Metadata { get; }
    }
}
