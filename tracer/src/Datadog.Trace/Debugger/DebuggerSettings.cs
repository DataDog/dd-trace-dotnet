// <copyright file="DebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger;

internal class DebuggerSettings
{
    private const string DefaultSite = "datadoghq.com";
    private const int DefaultMaxDepthToSerialize = 1;
    private const int DefaultSerializationTimeThreshold = 150;
    private const int DefaultConfigurationsPollIntervalSeconds = 1;

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

        var probeFileLocation = configurationSource?.GetString(ConfigurationKeys.Debugger.ProbeFile);
        var isFileModeMode = !string.IsNullOrWhiteSpace(probeFileLocation);
        var isAgentMode = configurationSource?.GetBool(ConfigurationKeys.Debugger.AgentMode) ?? false;

        var agentUri = exporterSettings.AgentUri.ToString().TrimEnd('/');
        var snapshotUri = configurationSource?.GetString(ConfigurationKeys.Debugger.SnapshotUrl)?.TrimEnd('/');

        if (isFileModeMode)
        {
            ProbeMode = ProbeMode.File;
            ProbeConfigurationsPath = probeFileLocation;
            SnapshotsPath = snapshotUri ?? agentUri;
        }
        else if (isAgentMode)
        {
            ProbeMode = ProbeMode.Agent;
            ProbeConfigurationsPath = agentUri;
            SnapshotsPath = snapshotUri ?? agentUri;
        }
        else
        {
            var site = configurationSource?.GetString(ConfigurationKeys.Site) ?? DefaultSite;

            ProbeConfigurationsPath = configurationSource?.GetString(ConfigurationKeys.Debugger.ProbeUrl)?.TrimEnd('/') ?? site;
            SnapshotsPath = snapshotUri ?? site;
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

    public static DebuggerSettings FromSource(IConfigurationSource source)
    {
        return new DebuggerSettings(source);
    }

    public static DebuggerSettings FromDefaultSource()
    {
        return FromSource(GlobalSettings.CreateDefaultConfigurationSource());
    }
}
