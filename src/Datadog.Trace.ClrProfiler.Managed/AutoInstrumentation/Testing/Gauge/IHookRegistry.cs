// <copyright file="IHookRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Gauge
{
    /// <summary>
    /// HookRegistry Ducktype interface
    /// </summary>
    public interface IHookRegistry
    {
        /// <summary>
        /// Adds hooks of a type
        /// </summary>
        /// <param name="hookType">Hook type instance</param>
        /// <param name="hooks">IEnumerable of handlers</param>
        void AddHookOfType(LibType hookType, IEnumerable<MethodInfo> hooks);
    }
}
