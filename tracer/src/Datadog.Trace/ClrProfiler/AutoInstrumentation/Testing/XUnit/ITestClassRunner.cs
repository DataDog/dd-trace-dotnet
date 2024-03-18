// <copyright file="ITestClassRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// TestClassRunner`1 interface
/// </summary>
internal interface ITestClassRunner
{
    /// <summary>
    /// Gets test class
    /// </summary>
    TestClassStruct TestClass { get; }

    /// <summary>
    /// Gets or sets the message bus
    /// </summary>
    object MessageBus { get; set; }

    /// <summary>
    /// RunAsync method
    /// </summary>
    /// <returns>Task instance</returns>
    object RunAsync();
}
