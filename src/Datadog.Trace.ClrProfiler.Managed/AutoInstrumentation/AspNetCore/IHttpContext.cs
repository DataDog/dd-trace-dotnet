// <copyright file="IHttpContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// Duct type for HttpContext
    /// </summary>
    public interface IHttpContext
    {
        /// <summary>
        /// Gets the response
        /// </summary>
        IHttpResponse Response { get; }

        /// <summary>
        /// Gets or sets the items dictionary
        /// </summary>
        IDictionary<object, object> Items { get; set; }
    }
}
