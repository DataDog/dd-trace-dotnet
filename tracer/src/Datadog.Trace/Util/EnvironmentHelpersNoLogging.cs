// <copyright file="EnvironmentHelpersNoLogging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
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

        var isServerless = TryCheckEnvVar(PlatformKeys.Aws.FunctionName, ref firstException)
                        || (TryCheckEnvVar(PlatformKeys.AzureAppService.SiteNameKey, ref firstException)
                         && !TryCheckEnvVar(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, ref firstException))
                        || (TryCheckEnvVar(PlatformKeys.GcpFunction.FunctionNameKey, ref firstException)
                         && TryCheckEnvVar(PlatformKeys.GcpFunction.FunctionTargetKey, ref firstException))
                        || (TryCheckEnvVar(PlatformKeys.GcpFunction.DeprecatedFunctionNameKey, ref firstException)
                         && TryCheckEnvVar(PlatformKeys.GcpFunction.DeprecatedProjectKey, ref firstException));
        exceptionInReading = firstException;
        return isServerless;
    }

    private static bool TryCheckEnvVar(string key, ref Exception? storedException)
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

    public static string? InjectionEnabled() => GetEnvironmentVariable(ConfigurationKeys.SsiDeployed);

    public static string? ProgramData() => GetEnvironmentVariable(PlatformKeys.ProgramData);

#pragma warning disable RS0030
    private static string? GetEnvironmentVariable(string key) => Environment.GetEnvironmentVariable(key);
#pragma warning restore RS0030
}
