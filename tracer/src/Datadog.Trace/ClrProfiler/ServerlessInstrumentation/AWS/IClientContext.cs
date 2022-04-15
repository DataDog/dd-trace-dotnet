// <copyright file="IClientContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Net;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal interface IClientContext
    {
        /// <summary>
        /// Gets the datadog injected trace context
        /// Used with the datadog ducktyping library
        /// </summary>
        /// <returns>The trace context</returns>
        IDictionary<string, string> Custom { get; }
    }
}
