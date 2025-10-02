// <copyright file="IEnvironmentVariableProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.Managed.Loader;

/// <summary>
/// Interface for accessing environment variables. This abstraction allows for easier testing
/// by enabling mocking of environment variable access.
/// </summary>
internal interface IEnvironmentVariableProvider
{
    /// <summary>
    /// Gets the value of an environment variable.
    /// </summary>
    /// <param name="key">The name of the environment variable.</param>
    /// <returns>The value of the environment variable, or null if it does not exist.</returns>
    string? GetEnvironmentVariable(string key);
}

/// <summary>
/// Extension methods for <see cref="IEnvironmentVariableProvider"/>.
/// </summary>
internal static class EnvironmentVariableProviderExtensions
{
    /// <summary>
    /// Gets the boolean value of an environment variable.
    /// </summary>
    /// <typeparam name="TEnvVars">The type of environment variable provider.</typeparam>
    /// <param name="provider">The environment variable provider.</param>
    /// <param name="key">The name of the environment variable.</param>
    /// <returns>A boolean value parsed from the environment variable, or the default value if parsing is not possible.</returns>
    public static bool? GetBooleanEnvironmentVariable<TEnvVars>(this TEnvVars provider, string key)
        where TEnvVars : IEnvironmentVariableProvider
    {
        var value = provider.GetEnvironmentVariable(key);

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value!.Length == 1)
        {
            return value[0] switch
                   {
                       '1' or 'T' or 't' or 'Y' or 'y' => true,
                       '0' or 'F' or 'f' or 'N' or 'n' => false,
                       _ => null
                   };
        }

        if (string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "FALSE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "NO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }
}
