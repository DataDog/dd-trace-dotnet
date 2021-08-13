// <copyright file="IConnectionMultiplexer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// Connection multiplexer ducktype structure
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IConnectionMultiplexer
    {
        /// <summary>
        /// Gets the conection configuration
        /// </summary>
        string Configuration { get; }
    }
}
