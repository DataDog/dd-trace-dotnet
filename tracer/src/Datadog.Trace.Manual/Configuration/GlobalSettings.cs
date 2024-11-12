// <copyright file="GlobalSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains global datadog settings.
/// </summary>
public sealed class GlobalSettings
{
    internal GlobalSettings()
    {
    }

    /// <summary>
    /// Set whether debug mode is enabled.
    /// Affects the level of logs written to file.
    /// </summary>
    /// <param name="enabled">Whether debug is enabled.</param>
    [Instrumented]
    public static void SetDebugEnabled(bool enabled)
    {
    }
}
