// <copyright file="EnvironmentHelpersNoLogging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Util;

/// <summary>
/// Helpers to access environment variables /infos without any logging (especially static loggers) as this will be used early in the lifecycle and we don't want to enter a loop where the logger tries to configurate itself reading these again
/// </summary>
internal static class EnvironmentHelpersNoLogging
{
    internal static bool IsServerlessEnvironment(out Exception? exceptionInReading)
    {
        // Track the first exception encountered while reading env vars
        Exception? firstException = null;

        var isServerless = EnvironmentVariableExists(PlatformKeys.Aws.FunctionName, ref firstException)
                        || (EnvironmentVariableExists(PlatformKeys.AzureAppService.SiteNameKey, ref firstException)
                         && !EnvironmentVariableExists(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, ref firstException))
                        || (EnvironmentVariableExists(PlatformKeys.GcpFunction.FunctionNameKey, ref firstException)
                         && EnvironmentVariableExists(PlatformKeys.GcpFunction.FunctionTargetKey, ref firstException))
                        || (EnvironmentVariableExists(PlatformKeys.GcpFunction.DeprecatedFunctionNameKey, ref firstException)
                         && EnvironmentVariableExists(PlatformKeys.GcpFunction.DeprecatedProjectKey, ref firstException));
        exceptionInReading = firstException;
        return isServerless;
    }

    private static bool EnvironmentVariableExists(string key, ref Exception? storedException)
    {
        try
        {
// this access is allowed here as it's controlled by analyzer EnvironmentGetEnvironmentVariableAnalyzer making sure it's using a key from ConfigurationKeys/PlatformKeys
#pragma warning disable DD0012
            var value = GetEnvironmentVariable(key);
#pragma warning restore DD0012
            return !string.IsNullOrEmpty(value);
        }
        catch (Exception ex)
        {
            // Store only the first exception encountered
            storedException ??= ex;
            return false;
        }
    }

    public static bool IsClrProfilerAttachedSafe()
    {
        try
        {
            return NativeMethods.IsProfilerAttached();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    public static string? SsiDeployedEnvVar() => GetEnvironmentVariable(ConfigurationKeys.SsiDeployed);

    public static string? ProgramData() => GetEnvironmentVariable(PlatformKeys.ProgramData);

#pragma warning disable RS0030
// this access is allowed here as it's controlled by analyzer EnvironmentGetEnvironmentVariableAnalyzer making sure it's using a key from ConfigurationKeys/PlatformKeys
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? GetEnvironmentVariable(string key) => Environment.GetEnvironmentVariable(key);
#pragma warning restore RS0030
}
