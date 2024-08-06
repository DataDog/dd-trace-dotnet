// <copyright file="ITestClassInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Internal.DuckTyping;

namespace Datadog.Trace.Internal.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal interface ITestClassInfo : IDuckType
{
    /// <summary>
    /// Gets the class type.
    /// </summary>
    Type ClassType { get; }

    /// <summary>
    /// Gets or sets the class cleanup method.
    /// </summary>
    public MethodInfo ClassCleanupMethod { get; set; }

    /// <summary>
    /// Gets the parent <see cref="ITestAssemblyInfo"/>.
    /// </summary>
    ITestAssemblyInfo Parent { get; }
}
