// <copyright file="ImmutableDebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger;

internal class ImmutableDebuggerSettings
{
    public ImmutableDebuggerSettings(bool enabled, ProbeMode probeMode, string apiKey, string runtimeId, string serviceName, string serviceVersion, int probeConfigurationsPollIntervalSeconds, string probeConfigurationsPath, string environment, int serializationTimeThreshold, int maxDepthToSerialize)
    {
        Enabled = enabled;
        ProbeMode = probeMode;
        ApiKey = apiKey;
        RuntimeId = runtimeId;
        ServiceName = serviceName;
        ServiceVersion = serviceVersion;
        ProbeConfigurationsPollIntervalSeconds = probeConfigurationsPollIntervalSeconds;
        ProbeConfigurationsPath = probeConfigurationsPath;
        Environment = environment;
        MillisecondsToCancel = serializationTimeThreshold;
        MaximumDepthOfMembersToCopy = maxDepthToSerialize;
    }

    private ImmutableDebuggerSettings(bool enabled)
    {
        Enabled = enabled;
    }

    public bool Enabled { get; }

    public ProbeMode ProbeMode { get; }

    public string ApiKey { get; }

    public string RuntimeId { get; }

    public string ServiceName { get; }

    public string ServiceVersion { get; }

    public string ProbeConfigurationsPath { get; }

    public int ProbeConfigurationsPollIntervalSeconds { get; }

    public string Environment { get; }

    public int MillisecondsToCancel { get; }

    public int MaximumDepthOfMembersToCopy { get; }

    public static ImmutableDebuggerSettings Create(TracerSettings tracerSettings) =>
        Create(tracerSettings.DebuggerSettings);

    public static ImmutableDebuggerSettings Create(DebuggerSettings debuggerSettings) =>
        Create(
            debuggerSettings.Enabled,
            debuggerSettings.ProbeMode,
            debuggerSettings.ApiKey,
            debuggerSettings.RuntimeId,
            debuggerSettings.ServiceName,
            debuggerSettings.ServiceVersion,
            debuggerSettings.ProbeConfigurationsPollIntervalSeconds,
            debuggerSettings.ProbeConfigurationsPath,
            debuggerSettings.Environment,
            debuggerSettings.SerializationTimeThreshold,
            debuggerSettings.MaxDepthToSerialize);

    public static ImmutableDebuggerSettings Create(
        bool enabled,
        ProbeMode probeMode,
        string apiKey,
        string runtimeId,
        string serviceName,
        string serviceVersion,
        int probeConfigurationsPollIntervalSeconds,
        string probeConfigurationsPath,
        string environment,
        int serializationTimeThreshold,
        int maxDepthToSerialize) =>
        new ImmutableDebuggerSettings(
            enabled,
            probeMode,
            apiKey,
            runtimeId,
            serviceName,
            serviceVersion,
            probeConfigurationsPollIntervalSeconds,
            probeConfigurationsPath,
            environment,
            serializationTimeThreshold,
            maxDepthToSerialize);
}
