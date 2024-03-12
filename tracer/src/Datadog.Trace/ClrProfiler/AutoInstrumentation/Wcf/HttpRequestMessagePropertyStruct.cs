// <copyright file="HttpRequestMessagePropertyStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Net;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf
{
    /// <summary>
    /// System.ServiceModel.Channels.HttpRequestMessageProperty interface for duck-typing
    /// </summary>
    [DuckCopy]
    internal struct HttpRequestMessagePropertyStruct
    {
        /// <summary>
        /// Gets the http request headers
        /// </summary>
        public WebHeaderCollection Headers;

        /// <summary>
        /// Gets the http request method
        /// </summary>
        public string Method;
    }
}
