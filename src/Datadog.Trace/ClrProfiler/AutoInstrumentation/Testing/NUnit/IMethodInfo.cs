// <copyright file="IMethodInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Interfaces.IMethodInfo
    /// </summary>
    public interface IMethodInfo
    {
        /// <summary>
        /// Gets the MethodInfo for this method.
        /// </summary>
        MethodInfo MethodInfo { get; }
    }
}
