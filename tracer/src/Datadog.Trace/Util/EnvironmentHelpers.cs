// <copyright file="EnvironmentHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util;

/// <summary>
/// Helpers to access environment variables
/// </summary>
internal static class EnvironmentHelpers
{
    // EnvironmentHelpers is called when initialising DataDogLogging.SharedLogger
    // Using Lazy<> here avoids setting the Logger field to the "null" logger, before initialization is complete
    private static readonly Lazy<IDatadogLogger> Logger = new(() => DatadogLogging.GetLoggerFor(typeof(EnvironmentHelpers)));

    /// <summary>
    /// Safe wrapper around Environment.SetEnvironmentVariable
    /// </summary>
    /// <param name="key">Name of the environment variable to set</param>
    /// <param name="value">Value to set</param>
    public static void SetEnvironmentVariable(string key, string? value)
    {
        try
        {
            Environment.SetEnvironmentVariable(key, value);
        }
        catch (Exception ex)
        {
            Logger.Value.Error(ex, "Error setting environment variable {EnvironmentVariable}={Value}", key, value);
        }
    }

    /// <summary>
    /// Safe wrapper around Environment.MachineName
    /// </summary>
    /// <returns>The value of <see cref="Environment.MachineName"/>, or null if an error occured</returns>
    public static string? GetMachineName()
    {
        try
        {
            return Environment.MachineName;
        }
        catch (Exception ex)
        {
            Logger.Value.Warning(ex, "Error while reading machine name");
        }

        return null;
    }

    /// <summary>
    /// Safe wrapper around Environment.GetEnvironmentVariable
    /// -> Don't rename unless you adapt EnvironmentGetEnvironmentVariableAnalyzer
    /// </summary>
    /// <param name="key">Name of the environment variable to fetch</param>
    /// <param name="defaultValue">Value to return in case of error</param>
    /// <returns>The value of the environment variable, or the default value if an error occured</returns>
    public static string? GetEnvironmentVariable(string key, string? defaultValue = null)
    {
        try
        {
// this access is allowed here as it's controlled by analyzer EnvironmentGetEnvironmentVariableAnalyzer making sure it's using a key from ConfigurationKeys/PlatformKeys
#pragma warning disable RS0030
            return Environment.GetEnvironmentVariable(key);
#pragma warning restore RS0030
        }
        catch (Exception ex)
        {
            Logger.Value.Warning(ex, "Error while reading environment variable {EnvironmentVariable}", key);
        }

        return defaultValue;
    }

    /// <summary>
    /// Safe wrapper around Environment.GetEnvironmentVariables
    /// </summary>
    /// <returns>A dictionary that contains all environment variables or an empty dictionary if an error occured</returns>
    public static IDictionary GetEnvironmentVariables()
    {
        try
        {
#pragma warning disable RS0030
            return Environment.GetEnvironmentVariables();
#pragma warning restore RS0030
        }
        catch (Exception ex)
        {
            Logger.Value.Warning(ex, "Error while reading environment variables");
        }

        return new Dictionary<object, object>();
    }

    /// <summary>
    /// Checks if the specified environment variable exists in the current environment.
    /// </summary>
    internal static bool EnvironmentVariableExists(string key)
    {
// this access is allowed here as EnvironmentVariableExists is controlled by analyzer EnvironmentGetEnvironmentVariableAnalyzer making sure it's using a key from ConfigurationKeys/PlatformKeys
#pragma warning disable DD0012
        var value = GetEnvironmentVariable(key);
#pragma warning restore
        return !string.IsNullOrEmpty(value);
    }
#pragma warning restore DD0012
}
