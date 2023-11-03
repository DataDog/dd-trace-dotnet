// <copyright file="IResponseContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IResponseContext interface for ducktyping
    /// </summary>
    internal interface IResponseContext
    {
        /// <summary>
        /// Gets the SDK response
        /// </summary>
        IAmazonWebServiceResponse? Response { get; }
    }
}
