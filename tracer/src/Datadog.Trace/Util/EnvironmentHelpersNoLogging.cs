// <copyright file="EnvironmentHelpersNoLogging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
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

        var isServerless = CheckEnvVar(PlatformKeys.Aws.FunctionName, ref firstException)
                        || (CheckEnvVar(PlatformKeys.AzureAppService.SiteNameKey, ref firstException)
                         && !CheckEnvVar(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, ref firstException))
                        || (CheckEnvVar(PlatformKeys.GcpFunction.FunctionNameKey, ref firstException)
                         && CheckEnvVar(PlatformKeys.GcpFunction.FunctionTargetKey, ref firstException))
                        || (CheckEnvVar(PlatformKeys.GcpFunction.DeprecatedFunctionNameKey, ref firstException)
                         && CheckEnvVar(PlatformKeys.GcpFunction.DeprecatedProjectKey, ref firstException));

        exceptionInReading = firstException;
        return isServerless;

        static bool CheckEnvVar(string key, ref Exception? storedException)
        {
            try
            {
                var value = Environment.GetEnvironmentVariable(key);
                return !string.IsNullOrEmpty(value);
            }
            catch (Exception ex)
            {
                // Store only the first exception encountered
                storedException ??= ex;
                return false;
            }
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
}
