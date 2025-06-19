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

    internal static void StoreTracerMetadata(TracerSettings tracerSettings)
    {
        if (FrameworkDescription.Instance.OSPlatform == OSPlatformName.Linux
         && Environment.Is64BitProcess
         && !Util.EnvironmentHelpers.IsServerlessEnvironment())
        {
            try
            {
                var result = StoreTracerMetadata(
                    1,
                    Tracer.RuntimeId,
                    TracerConstants.Language,
                    TracerConstants.ThreePartVersion,
                    Environment.MachineName,
                    tracerSettings.ServiceName,
                    tracerSettings.Environment,
                    tracerSettings.ServiceVersion);

                if (result.Tag == ResultTag.Error)
                {
                    Log.Error("Failed to store tracer metadata with message: {Error}", Error.Read(ref result.Error));
                    NativeInterop.Common.DropError(ref result.Error);
                }

                Log.Debug("Successfully stored tracer metadata with LibDatadog");
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to store tracer metadata due to an unexpected error");
            }
        }
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

        return NativeInterop.Common.StoreTracerMetadata(schemaVersion, runtimeIdCharSlice, tracerLanguageCharSlice, tracerVersionCharSlice, hostnameCharSlice, serviceNameCharSlice, serviceEnvCharSlice, serviceVersionCharSlice);
    }
}
