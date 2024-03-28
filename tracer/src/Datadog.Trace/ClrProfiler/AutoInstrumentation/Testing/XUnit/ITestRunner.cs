// <copyright file="ITestRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

internal interface ITestRunner
{
    string DisplayName { get; }

    string? SkipReason { get; set; }

    object MessageBus { get; set; }

    object RunAsync();
}
