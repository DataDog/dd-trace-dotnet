// <copyright file="IThreadContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.HttpApplication interface for ducktyping
    /// </summary>
    internal interface IThreadContext
    {
        /// <summary>
        /// Gets the HttpContext of the thread context
        /// </summary>
        IHttpContext HttpContext { get; }
    }
}
