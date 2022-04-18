// <copyright file="IOriginalRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IOriginalRequest interface for ducktyping
    /// </summary>
    internal interface IOriginalRequest
    {
        /// <summary>
        /// Gets or sets the original request
        /// </summary>
        string ClientContext { get; set; }

        /// <summary>
        /// Gets or sets the original request in b64
        /// </summary>
        string ClientContextBase64 { get; set; }

        /// <summary>
        /// Gets the invocation type
        /// </summary>
        IInvocationType InvocationType { get; }
    }
}
