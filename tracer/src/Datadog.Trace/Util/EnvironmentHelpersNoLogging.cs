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
    /// <summary>
    /// Gets a value indicating whether Datadog's instrumentation library (aka CLR profiler) is attached to the current process.
    /// </summary>
    /// <remarks>
    /// Should not log anything. Logging here could be an issue as this is accessed before Configuration objects are built. Logging here could create a loop where Configuration building tests if profiler is attached to access libdatadog, the test wants to log, the Logger being created for the first time tried to access the Configuration object.
    /// </remarks>
    /// <value>
    ///   <c>true</c> if the profiler is currently attached; <c>false</c> otherwise.
    /// </value>
    public static bool IsClrProfilerAttached
    {
        get
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

    public static bool IsWindows()
    {
#if NETFRAMEWORK
        return true;
#else
        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
    }

    internal static Tuple<bool, Exception?> IsServerlessEnvironment()
    {
        Exception? exception = null;
        if (EnvVarExistsNoLogging(LambdaMetadata.FunctionNameEnvVar)
         || (EnvVarExistsNoLogging(ConfigurationKeys.AzureAppService.SiteNameKey) && !EnvVarExistsNoLogging(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey))
         || (EnvVarExistsNoLogging(ConfigurationKeys.GCPFunction.FunctionNameKey) && EnvVarExistsNoLogging(ConfigurationKeys.GCPFunction.FunctionTargetKey))
         || (EnvVarExistsNoLogging(ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey) && EnvVarExistsNoLogging(ConfigurationKeys.GCPFunction.DeprecatedProjectKey)))
        {
            return new Tuple<bool, Exception?>(true, exception);
        }

        return new Tuple<bool, Exception?>(false, null);

        bool EnvVarExistsNoLogging(string key)
        {
            string? value = null;
            try
            {
                value = Environment.GetEnvironmentVariable(key);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            return !string.IsNullOrEmpty(value);
        }
    }
}
