// <copyright file="IHeader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Header interface for duck-typing
    /// </summary>
    internal interface IHeader
    {
        /// <summary>
        ///  Gets key
        /// </summary>
        string Key { get; }

        /// <summary>
        ///  Returns value bytes
        /// </summary>
        byte[] GetValueBytes();
    }
}
