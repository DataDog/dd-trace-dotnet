// <copyright file="ProfilerPathSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Checks;

internal enum ProfilerPathSource
{
    Unknown = 0,
    EnvironmentVariable = 1,
    WindowsRegistry = 2,
}
