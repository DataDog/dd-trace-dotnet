// <copyright file="IOwinContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// HttpRequestMessage interface for ducktyping
    /// </summary>
    internal interface IOwinContext
    {
        /// <summary>
        /// Gets the OwinRequest object
        /// </summary>
        IOwinRequest Request { get; }
    }
}
#endif
