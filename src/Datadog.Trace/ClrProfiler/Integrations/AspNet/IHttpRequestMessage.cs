// <copyright file="IHttpRequestMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// HttpRequestMessage interface for ducktyping
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IHttpRequestMessage
    {
        /// <summary>
        /// Gets the Http Method
        /// </summary>
        HttpMethodStruct Method { get; }

        /// <summary>
        /// Gets the request uri
        /// </summary>
        Uri RequestUri { get; }

        /// <summary>
        /// Gets the request headers
        /// </summary>
        IRequestHeaders Headers { get; }
    }
}
#endif
