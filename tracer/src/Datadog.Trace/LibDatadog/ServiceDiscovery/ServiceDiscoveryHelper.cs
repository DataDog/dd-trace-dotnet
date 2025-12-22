// <copyright file="ServiceDiscoveryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

internal static class ServiceDiscoveryHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceDiscoveryHelper));

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
        string runtimeId,
        string tracerLanguage,
        string tracerVersion,
        string? hostname,
        string? serviceName,
        string? serviceEnv,
        string? serviceVersion)
    {
        IntPtr ptr = IntPtr.Zero;
        try
        {
            ptr = NativeInterop.LibraryConfig.TracerMetadataNew();
            SetMetadata(ptr, MetadataKind.RuntimeId, runtimeId);
            SetMetadata(ptr, MetadataKind.TracerLanguage, tracerLanguage);
            SetMetadata(ptr, MetadataKind.TracerVersion, tracerVersion);
            SetMetadata(ptr, MetadataKind.Hostname, hostname);
            SetMetadata(ptr, MetadataKind.ServiceName, serviceName);
            SetMetadata(ptr, MetadataKind.ServiceEnvironment, serviceEnv);
            SetMetadata(ptr, MetadataKind.ServiceVersion, serviceVersion);

            return NativeInterop.LibraryConfig.StoreTracerMetadata(ptr);
        }
        finally
        {
            if (ptr != IntPtr.Zero)
            {
                NativeInterop.LibraryConfig.TracerMetadataFree(ptr);
            }
        }

        void SetMetadata(IntPtr ptr, MetadataKind kind, string? value)
        {
            using var valueCharSlice = new CString(value);
            NativeInterop.LibraryConfig.TracerMetadataSet(ptr, kind, valueCharSlice);
        }
    }
}
