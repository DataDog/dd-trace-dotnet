// <copyright file="ServiceDiscoveryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

internal class ServiceDiscoveryHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ServiceDiscoveryHelper>();

    internal enum StoreMetadataResult
    {
        Success,
        Skipped,
        Error,
        Exception
    }

    internal static StoreMetadataResult StoreTracerMetadata(TracerSettings tracerSettings, MutableSettings mutableSettings)
    {
        var platformIsSupported = FrameworkDescription.Instance.OSPlatform == OSPlatformName.Linux && Environment.Is64BitProcess;
        var deploymentIsSupported = LibDatadogAvailabilityHelper.IsLibDatadogAvailable;
        if (platformIsSupported && deploymentIsSupported.IsAvailable)
        {
            try
            {
                var result = StoreTracerMetadata(
                    1,
                    Tracer.RuntimeId,
                    TracerConstants.Language,
                    TracerConstants.ThreePartVersion,
                    Environment.MachineName,
                    mutableSettings.DefaultServiceName,
                    mutableSettings.Environment,
                    mutableSettings.ServiceVersion);

                if (result.Tag == ResultTag.Error)
                {
                    Log.Error("Failed to store tracer metadata with message: {Error}", result.Result.Error.Message.ToUtf8String());
                    NativeInterop.Common.DropError(ref result.Result.Error);
                    return StoreMetadataResult.Error;
                }

                Log.Debug("Successfully stored tracer metadata with LibDatadog");
                return StoreMetadataResult.Success;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to store tracer metadata due to an unexpected error");
                return StoreMetadataResult.Exception;
            }
        }

        Log.Debug(
            "Skipping storage of tracer metadata with LibDatadog: Platform supported: {PlatformIsSupported}, Deployment supported: {DeploymentIsSupported}",
            platformIsSupported,
            deploymentIsSupported.IsAvailable);

        return StoreMetadataResult.Skipped;
    }

    private static TracerMemfdHandleResult StoreTracerMetadata(
        byte schemaVersion,
        string runtimeId,
        string tracerLanguage,
        string tracerVersion,
        string? hostname,
        string? serviceName,
        string? serviceEnv,
        string? serviceVersion)
    {
        using var runtimeIdCharSlice = new CharSlice(runtimeId);
        using var tracerLanguageCharSlice = new CharSlice(tracerLanguage);
        using var tracerVersionCharSlice = new CharSlice(tracerVersion);
        using var hostnameCharSlice = new CharSlice(hostname);
        using var serviceNameCharSlice = new CharSlice(serviceName);
        using var serviceEnvCharSlice = new CharSlice(serviceEnv);
        using var serviceVersionCharSlice = new CharSlice(serviceVersion);

        return NativeInterop.LibraryConfig.StoreTracerMetadata(schemaVersion, runtimeIdCharSlice, tracerLanguageCharSlice, tracerVersionCharSlice, hostnameCharSlice, serviceNameCharSlice, serviceEnvCharSlice, serviceVersionCharSlice);
    }
}
