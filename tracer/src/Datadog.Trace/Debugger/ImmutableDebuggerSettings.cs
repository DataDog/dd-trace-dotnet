// <copyright file="ImmutableDebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger;

internal class ImmutableDebuggerSettings
{
    public ImmutableDebuggerSettings(bool enabled, ProbeMode probeMode, string apiKey, string runtimeId, string serviceName, string serviceVersion, int probeConfigurationsPollIntervalSeconds, string probeConfigurationsPath, string environment, int maxSerializationTimeInMilliseconds, int maximumDepthOfMembersToCopy, string snapshotsPath, int uploadBatchSize, int diagnosticsIntervalSeconds, int uploadFlushIntervalMilliseconds)
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
        MaxSerializationTimeInMilliseconds = maxSerializationTimeInMilliseconds;
        MaximumDepthOfMembersOfMembersToCopy = maximumDepthOfMembersToCopy;
        SnapshotsPath = snapshotsPath;
        UploadBatchSize = uploadBatchSize;
        DiagnosticsIntervalSeconds = diagnosticsIntervalSeconds;
        UploadFlushIntervalMilliseconds = uploadFlushIntervalMilliseconds;
    }

    public bool Enabled { get; }

    public ProbeMode ProbeMode { get; }

    public string ApiKey { get; }

    public string RuntimeId { get; }

    public string ServiceName { get; }

    public string ServiceVersion { get; }

    public string ProbeConfigurationsPath { get; }

    public string SnapshotsPath { get; set; }

    public int ProbeConfigurationsPollIntervalSeconds { get; }

    public string Environment { get; }

    public int MaxSerializationTimeInMilliseconds { get; }

    public int MaximumDepthOfMembersOfMembersToCopy { get; }

    public int UploadBatchSize { get; }

    public int DiagnosticsIntervalSeconds { get; }

    public int UploadFlushIntervalMilliseconds { get; }

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
            debuggerSettings.MaxSerializationTimeInMilliseconds,
            debuggerSettings.MaximumDepthOfMembersToCopy,
            debuggerSettings.SnapshotsPath,
            debuggerSettings.UploadBatchSize,
            debuggerSettings.DiagnosticsIntervalSeconds,
            debuggerSettings.UploadFlushIntervalMilliseconds);

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
        int maxSerializationTimeInMilliseconds,
        int maximumDepthOfMembersOfMembersToCopy,
        string snapshotsPath,
        int uploadBatchSize,
        int diagnosticsIntervalSeconds,
        int uploadFlushIntervalMilliseconds) =>
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
            maxSerializationTimeInMilliseconds,
            maximumDepthOfMembersOfMembersToCopy,
            snapshotsPath,
            uploadBatchSize,
            diagnosticsIntervalSeconds,
            uploadFlushIntervalMilliseconds);
}
