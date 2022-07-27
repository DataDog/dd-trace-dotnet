// <copyright file="OwinContextStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// OwinContext duck copy
    /// </summary>
    [DuckCopy]
    internal struct OwinContextStruct
    {
        /// <summary>
        /// Gets the OwinRequest object
        /// </summary>
        public OwinRequestStruct Request;
    }
}
#endif
