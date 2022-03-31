// <copyright file="BindingSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// duck copy of bindingsource
    /// </summary>
    [DuckTyping.DuckCopy]
    internal struct BindingSource
    {
        /// <summary>
        /// Gets the id
        /// </summary>
        public string Id;
    }
}
#endif
