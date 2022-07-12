// <copyright file="DebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger;

internal class DebuggerSettings
{
    private const int DefaultMaxDepthToSerialize = 1;
    private const int DefaultSerializationTimeThreshold = 150;
    private const int DefaultConfigurationsPollIntervalSeconds = 1;
    private const int DefaultUploadBatchSize = 100;
    private const int DefaultDiagnosticsIntervalSeconds = 3600;
    private const int DefaultUploadFlushIntervalMilliseconds = 0;

    public DebuggerSettings()
        : this(configurationSource: null)
    {
    }

    public DebuggerSettings(IConfigurationSource configurationSource)
    {
        ApiKey = configurationSource?.GetString(ConfigurationKeys.ApiKey);
        RuntimeId = Util.RuntimeId.Get();
        ServiceName = configurationSource?.GetString(ConfigurationKeys.ServiceName);

        var exporterSettings = new ExporterSettings(configurationSource);

        var agentUri = exporterSettings.AgentUri.ToString().TrimEnd('/');
        SnapshotsPath = configurationSource?.GetString(ConfigurationKeys.Debugger.SnapshotUrl)?.TrimEnd('/') ?? agentUri;

        var probeFileLocation = configurationSource?.GetString(ConfigurationKeys.Debugger.ProbeFile);
        var isFileModeMode = !string.IsNullOrWhiteSpace(probeFileLocation);
        if (isFileModeMode)
        {
            ProbeMode = ProbeMode.File;
            ProbeConfigurationsPath = probeFileLocation;
        }
        else
        {
            ProbeMode = ProbeMode.Agent;
            ProbeConfigurationsPath = agentUri;
        }

        var pollInterval = configurationSource?.GetInt32(ConfigurationKeys.Debugger.PollInterval);
        ProbeConfigurationsPollIntervalSeconds =
            pollInterval is null or <= 0
                ? DefaultConfigurationsPollIntervalSeconds
                : pollInterval.Value;

        ServiceVersion = configurationSource?.GetString(ConfigurationKeys.ServiceVersion);
        Environment = configurationSource?.GetString(ConfigurationKeys.Environment);

        Enabled = configurationSource?.GetBool(ConfigurationKeys.Debugger.DebuggerEnabled) ?? false;

        var maxDepth = configurationSource?.GetInt32(ConfigurationKeys.Debugger.MaxDepthToSerialize);
        MaximumDepthOfMembersToCopy =
            maxDepth is null or <= 0
                ? DefaultMaxDepthToSerialize
                : maxDepth.Value;

        var serializationTimeThreshold = configurationSource?.GetInt32(ConfigurationKeys.Debugger.MaxTimeToSerialize);
        MaxSerializationTimeInMilliseconds =
            serializationTimeThreshold is null or <= 0
                ? DefaultSerializationTimeThreshold
                : serializationTimeThreshold.Value;

        var batchSize = configurationSource?.GetInt32(ConfigurationKeys.Debugger.UploadBatchSize);
        UploadBatchSize =
            batchSize is null or <= 0
                ? DefaultUploadBatchSize
                : batchSize.Value;

        var interval = configurationSource?.GetInt32(ConfigurationKeys.Debugger.DiagnosticsInterval);
        DiagnosticsIntervalSeconds =
            interval is null or <= 0
                ? DefaultDiagnosticsIntervalSeconds
                : interval.Value;

        var flushInterval = configurationSource?.GetInt32(ConfigurationKeys.Debugger.UploadFlushInterval);
        UploadFlushIntervalMilliseconds =
            flushInterval is null or < 0
                ? DefaultUploadFlushIntervalMilliseconds
                : flushInterval.Value;
    }

    public ProbeMode ProbeMode { get; set; }

    public string ApiKey { get; set; }

    public string RuntimeId { get; set; }

    public string ServiceName { get; set; }

    public string ServiceVersion { get; set; }

    public int ProbeConfigurationsPollIntervalSeconds { get; set; }

    public string ProbeConfigurationsPath { get; set; }

    public string SnapshotsPath { get; set; }

    public string Environment { get; set; }

    public bool Enabled { get; }

    public int MaxSerializationTimeInMilliseconds { get; }

    public int MaximumDepthOfMembersToCopy { get; }

    public int UploadBatchSize { get; }

    public int DiagnosticsIntervalSeconds { get; }

    public int UploadFlushIntervalMilliseconds { get; }

    public static DebuggerSettings FromSource(IConfigurationSource source)
    {
        return new DebuggerSettings(source);
    }

    public static DebuggerSettings FromDefaultSource()
    {
        return FromSource(GlobalSettings.CreateDefaultConfigurationSource());
    }
}
