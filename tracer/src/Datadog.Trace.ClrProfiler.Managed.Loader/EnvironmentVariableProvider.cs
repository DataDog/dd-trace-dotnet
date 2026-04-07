// <copyright file="EnvironmentVariableProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.Managed.Loader;

/// <summary>
/// Default implementation of <see cref="IEnvironmentVariableProvider"/> that uses System.Environment.
/// </summary>
internal readonly struct EnvironmentVariableProvider : IEnvironmentVariableProvider
{
    private readonly bool _logErrors;

    public EnvironmentVariableProvider(bool logErrors)
    {
        _logErrors = logErrors;
    }

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string key)
    {
        try
        {
            return Environment.GetEnvironmentVariable(key);
        }
        catch (Exception ex)
        {
            if (_logErrors)
            {
                StartupLogger.Log(ex, "Error reading environment variable {0}", key);
            }

            return null;
        }
    }
}
