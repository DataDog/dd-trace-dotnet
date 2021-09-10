// <copyright file="HttpMethodStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient
{
    /// <summary>
    /// Http method struct copy target for ducktyping
    /// </summary>
    [DuckCopy]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct HttpMethodStruct
    {
        /// <summary>
        /// Gets the http method in string
        /// </summary>
        public string Method;
    }
}
