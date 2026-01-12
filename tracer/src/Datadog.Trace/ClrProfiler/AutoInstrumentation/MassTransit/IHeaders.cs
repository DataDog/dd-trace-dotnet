// <copyright file="IHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Duck-typing interface for MassTransit.Headers
    /// </summary>
    internal interface IHeaders : IEnumerable<KeyValuePair<string, object>>
    {
        /// <summary>
        /// Gets or sets a header value
        /// </summary>
        object? this[string key] { get; set; }

        /// <summary>
        /// Tries to get a header value
        /// </summary>
        bool TryGetHeader(string key, out object? value);
    }
}
