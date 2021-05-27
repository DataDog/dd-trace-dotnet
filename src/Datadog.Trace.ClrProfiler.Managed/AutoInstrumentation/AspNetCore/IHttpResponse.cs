// <copyright file="IHttpResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// Duct type for HttpResponse
    /// </summary>
    public interface IHttpResponse
    {
        /// <summary>
        /// Gets or sets the status code
        /// </summary>
        int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets content type
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// writes aync
        /// </summary>
        /// <param name="text">some text</param>
        /// <returns>a continuation</returns>
        Task WriteAsync(string text);
    }
}
