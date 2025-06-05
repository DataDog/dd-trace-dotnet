// <copyright file="Utils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

internal class Utils
{
    internal static TracerMemfdHandleResult StoreTracerMetadata(
        byte schemaVersion,
        string runtimeId,
        string tracerLanguage,
        string tracerVersion,
        string? hostname,
        string? serviceName,
        string? serviceEnv,
        string? serviceVersion)
    {
        var runtimeIdCharSlice = new CharSlice(runtimeId);
        var tracerLanguageCharSlice = new CharSlice(tracerLanguage);
        var tracerVersionCharSlice = new CharSlice(tracerVersion);
        var hostnameCharSlice = new CharSlice(hostname);
        var serviceNameCharSlice = new CharSlice(serviceName);
        var serviceEnvCharSlice = new CharSlice(serviceEnv);
        var serviceVersionCharSlice = new CharSlice(serviceVersion);

        var result = NativeInterop.Common.StoreTracerMetadata(1, runtimeIdCharSlice, tracerLanguageCharSlice, tracerVersionCharSlice, hostnameCharSlice, serviceNameCharSlice, serviceEnvCharSlice, serviceVersionCharSlice);
        runtimeIdCharSlice.Dispose();
        tracerLanguageCharSlice.Dispose();
        tracerVersionCharSlice.Dispose();
        hostnameCharSlice.Dispose();
        serviceNameCharSlice.Dispose();
        serviceEnvCharSlice.Dispose();
        serviceVersionCharSlice.Dispose();
        return result;
    }
}
