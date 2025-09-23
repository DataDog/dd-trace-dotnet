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
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Util;

/// <summary>
/// Helpers to access environment variables /infos without any logging (especially static loggers) as this will be used early in the lifecycle and we don't want to enter a loop where the logger tries to configurate itself reading these again
/// </summary>
internal static class EnvironmentHelpersNoLogging
{
    private static readonly ConfigurationBuilder Builder = ConfigurationBuilder.FromEnvironmentSourceOnly();

    internal static bool IsServerlessEnvironment(out Exception? exceptionInReading)
    {
        exceptionInReading = null;
        var awsResult = Builder.WithKeys(PlatformKeys.Aws.FunctionName).AsStringResult();
        exceptionInReading = awsResult.ConfigurationResult.Exception;
        if (awsResult.ConfigurationResult.IsPresent)
        {
            return true;
        }

        var azureResult = Builder.WithKeys(PlatformKeys.AzureAppService.SiteNameKey).AsStringResult();
        var azureAppServiceContextKey = Builder.WithKeys(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey).AsStringResult();
        exceptionInReading ??= azureResult.ConfigurationResult.Exception;
        exceptionInReading ??= azureAppServiceContextKey.ConfigurationResult.Exception;

        if (azureResult.ConfigurationResult.IsPresent && !azureAppServiceContextKey.ConfigurationResult.IsPresent)
        {
            return true;
        }

        var gcpResult = Builder.WithKeys(PlatformKeys.GCPFunction.FunctionNameKey).AsStringResult();
        var gcpTargetKey = Builder.WithKeys(PlatformKeys.GCPFunction.FunctionTargetKey).AsStringResult();
        exceptionInReading ??= gcpResult.ConfigurationResult.Exception;
        exceptionInReading ??= gcpTargetKey.ConfigurationResult.Exception;
        if (gcpResult.ConfigurationResult.IsPresent && gcpTargetKey.ConfigurationResult.IsPresent)
        {
            return true;
        }

        var gcpResultDep = Builder.WithKeys(PlatformKeys.GCPFunction.DeprecatedFunctionNameKey).AsStringResult();
        var gcpTargetKeyDep = Builder.WithKeys(PlatformKeys.GCPFunction.DeprecatedProjectKey).AsStringResult();
        exceptionInReading ??= gcpResultDep.ConfigurationResult.Exception;
        exceptionInReading ??= gcpTargetKeyDep.ConfigurationResult.Exception;
        if (gcpResultDep.ConfigurationResult.IsPresent && gcpTargetKeyDep.ConfigurationResult.IsPresent)
        {
            return true;
        }

        return false;
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
