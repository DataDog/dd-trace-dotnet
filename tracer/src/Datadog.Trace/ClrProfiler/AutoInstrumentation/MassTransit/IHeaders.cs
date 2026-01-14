// <copyright file="IHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Duck-typing interface for MassTransit.Headers (for reading headers on consume side)
    /// </summary>
    internal interface IHeaders
    {
        /// <summary>
        /// Tries to get a header value
        /// </summary>
        /// <param name="key">The header key</param>
        /// <param name="value">The header value if found</param>
        /// <returns>True if header found, false otherwise</returns>
        bool TryGetHeader(string key, out object value);
    }
}
