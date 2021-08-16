// <copyright file="IRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IRequestContext interface for ducktyping
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IRequestContext
    {
        /// <summary>
        /// Gets the client config
        /// </summary>
        IClientConfig ClientConfig { get; }

        /// <summary>
        /// Gets the Request
        /// </summary>
        IRequest Request { get; }
    }
}
