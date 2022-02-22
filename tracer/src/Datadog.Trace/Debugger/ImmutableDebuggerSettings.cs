// <copyright file="ImmutableDebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger;

internal class ImmutableDebuggerSettings
{
    private ImmutableDebuggerSettings(
        bool enabled,
        ProbeMode probeMode,
        string apiKey,
        string trackingId,
        int probeConfigurationsPollIntervalSeconds,
        string probeConfigurationsPath,
        string version,
        string environment,
        int maximumDepthOfMembersToCopy,
        int millisecondsToCancel)
    {
        Enabled = enabled;
        ProbeMode = probeMode;
        ApiKey = apiKey;
        TrackingId = trackingId;
        ProbeConfigurationsPollIntervalSeconds = probeConfigurationsPollIntervalSeconds;
        ProbeConfigurationsPath = probeConfigurationsPath;
        Version = version;
        Environment = environment;
        MaximumDepthOfMembersToCopy = maximumDepthOfMembersToCopy;
        MillisecondsToCancel = millisecondsToCancel;
    }

    public bool Enabled { get; }

    public ProbeMode ProbeMode { get; }

    public string ApiKey { get; }

    public string TrackingId { get; }

    public string ProbeConfigurationsPath { get; }

    public int ProbeConfigurationsPollIntervalSeconds { get; }

    public string Version { get; }

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
            debuggerSettings.TrackingId,
            debuggerSettings.ProbeConfigurationsPollIntervalSeconds,
            debuggerSettings.ProbeConfigurationsPath,
            debuggerSettings.Version,
            debuggerSettings.Environment,
            debuggerSettings.MaxDepthToSerialize,
            debuggerSettings.SerializationTimeThreshold);

    public static ImmutableDebuggerSettings Create(
        bool enabled,
        ProbeMode probeMode,
        string apiKey,
        string trackingId,
        int probeConfigurationsPollIntervalSeconds,
        string probeConfigurationsPath,
        string version,
        string environment,
        int maximumDepthOfMembersToCopy,
        int millisecondsToCancel)
    {
        return new ImmutableDebuggerSettings(enabled, probeMode, apiKey, trackingId, probeConfigurationsPollIntervalSeconds, probeConfigurationsPath, version, environment, maximumDepthOfMembersToCopy, millisecondsToCancel);
    }
}
