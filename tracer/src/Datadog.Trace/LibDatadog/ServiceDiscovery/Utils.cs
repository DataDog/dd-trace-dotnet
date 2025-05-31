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
        var runtimeIdCharSlice = CharSlice.CreateCharSlice(runtimeId);
        var tracerLanguageCharSlice = CharSlice.CreateCharSlice(tracerLanguage);
        var tracerVersionCharSlice = CharSlice.CreateCharSlice(tracerVersion);
        var hostnameCharSlice = CharSlice.CreateCharSlice(hostname);
        var serviceNameCharSlice = CharSlice.CreateCharSlice(serviceName);
        var serviceEnvCharSlice = CharSlice.CreateCharSlice(serviceEnv);
        var serviceVersionCharSlice = CharSlice.CreateCharSlice(serviceVersion);

        var result = NativeInterop.Common.StoreTracerMetadata(1, runtimeIdCharSlice, tracerLanguageCharSlice, tracerVersionCharSlice, hostnameCharSlice, serviceNameCharSlice, serviceEnvCharSlice, serviceVersionCharSlice);
        CharSlice.FreeCharSlice(tracerLanguageCharSlice);
        CharSlice.FreeCharSlice(tracerVersionCharSlice);
        CharSlice.FreeCharSlice(hostnameCharSlice);
        CharSlice.FreeCharSlice(serviceNameCharSlice);
        CharSlice.FreeCharSlice(serviceEnvCharSlice);
        CharSlice.FreeCharSlice(serviceVersionCharSlice);
        return result;
    }
}
