// <copyright file="OwinRequestStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// OwinRequest duck copy
    /// </summary>
    [DuckCopy]
    internal struct OwinRequestStruct
    {
        /// <summary>
        /// Gets the RemoteIpAddress
        /// </summary>
        public string RemoteIpAddress;

        /// <summary>
        /// Gets a value indicating whether its an encrypted connection
        /// </summary>
        public bool IsSecure;
    }
}
#endif
