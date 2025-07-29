// <copyright file="LibDatadogAvailaibilityHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// *This class should NOT contain any direct Logger field, nor methods should log*.
/// LibDatadogAvailable factory is used when building settings for the tracer, if a logger is instantiated in this path, it creates an infinite loop as the logger itself will try to build settings
/// The Lazy will loop on itself and end up in a InvalidOperationException as Value ends up calling itself
/// Even calling a method on a class that has a Logger as a static field is enough to have it instantiated, that's why ProfilerAttached eventually call FrameworkDescription that has a Lazy`Logger`
/// </summary>
internal static class LibDatadogAvailaibilityHelper
{
    // This will never change, so we use a lazy to cache the result.
    // This confirms that we are in an automatic instrumentation environment (and so P/Invokes have been re-written)
    // and that the libdatadog library has been deployed (which is not the case in some serverless environments).
    // We should add or remove conditions from here as our deployment requirements change.
    private static readonly Lazy<LibDatadogAvailableResult> LibDatadogAvailable = new(() =>
    {
        var res = IsServerlessEnvironment();
        var isServerless = res.Item1;
        var possibleException = res.Item2;
        return new(!isServerless && Instrumentation.ProfilerAttached, possibleException);
    });

    public static LibDatadogAvailableResult IsLibDatadogAvailable => LibDatadogAvailable.Value;

    private static Tuple<bool, Exception?> IsServerlessEnvironment()
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
