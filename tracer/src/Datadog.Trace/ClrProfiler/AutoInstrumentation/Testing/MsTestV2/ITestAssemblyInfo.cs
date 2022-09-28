// <copyright file="ITestAssemblyInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal interface ITestAssemblyInfo : IDuckType
{
    /// <summary>
    /// Gets the <see cref="Assembly"/> this class represents.
    /// </summary>
    Assembly Assembly { get; }

    /// <summary>
    /// Gets a value indicating whether <c>AssemblyInitialize</c> has been executed.
    /// </summary>
    bool IsAssemblyInitializeExecuted { get; }

    /// <summary>
    /// Gets or sets <c>AssemblyCleanup</c> method for the assembly.
    /// </summary>
    MethodInfo AssemblyCleanupMethod { get; set; }
}
