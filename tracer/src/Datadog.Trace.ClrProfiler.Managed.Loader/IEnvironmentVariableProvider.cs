// <copyright file="IEnvironmentVariableProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

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
    /// <returns>The value of the environment variable, or an empty string if it does not exist.</returns>
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
    /// <param name="provider">The environment variable provider.</param>
    /// <param name="key">The name of the environment variable.</param>
    /// <param name="defaultValue">The default boolean value to return if the environment variable is not set or cannot be parsed.</param>
    /// <returns>A boolean value parsed from the environment variable, or the default value if parsing is not possible.</returns>
    public static bool GetBooleanEnvironmentVariable(this IEnvironmentVariableProvider provider, string key, bool defaultValue)
    {
        var value = provider.GetEnvironmentVariable(key);

        return value switch
               {
                   "1" or "true" or "True" or "TRUE" or "t" or "T" => true,
                   "0" or "false" or "False" or "FALSE" or "f" or "F" => false,
                   _ => defaultValue
               };
    }
}
