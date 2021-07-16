// <copyright file="MultiplexerData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// Multiplexer data structure for duck typing
    /// </summary>
    [DuckCopy]
    public struct MultiplexerData
    {
        /// <summary>
        /// Multiplexer configuration
        /// </summary>
        public string Configuration;
    }
}
