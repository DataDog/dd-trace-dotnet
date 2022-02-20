// <copyright file="DebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger;

internal class DebuggerSettings
{
    private const int DefaultMaxDepthToSerialize = 3;
    private const int DefaultSerializationTimeThreshold = 150;
    private const int DefaultConfigurationsPollIntervalSeconds = 1;
    private const string DefaultUri = "http://localhost:8126";
    private const string DefaultProbeConfigurationBackendPath = "api/v2/debugger-cache/configurations";
    private const string DefaultProbeConfigurationAgentPath = "2/LIVE_DEBUGGING/config";

    public DebuggerSettings()
        : this(configurationSource: null)
    {
    }

    public DebuggerSettings(IConfigurationSource configurationSource)
    {
        ApiKey = configurationSource?.GetString(ConfigurationKeys.ApiKey);
        HostId = configurationSource?.GetString(ConfigurationKeys.ServiceName) ?? Guid.NewGuid().ToString();
        ServiceName = configurationSource?.GetString(ConfigurationKeys.ServiceName);

        var url = configurationSource?.GetString(ConfigurationKeys.Debugger.ProbeUrl)
               ?? configurationSource?.GetString(ConfigurationKeys.AgentUri)
               ?? DefaultUri
            ;

        url = url.EndsWith("/") ? url : url + '/';

        var probeFileLocation = configurationSource?.GetString(ConfigurationKeys.Debugger.ProbeFile);
        var isFileModeMode = !string.IsNullOrWhiteSpace(probeFileLocation);
        var isAgentMode = configurationSource?.GetBool(ConfigurationKeys.Debugger.AgentMode) ?? false;

        if (isFileModeMode)
        {
            ProbeMode = ProbeMode.File;
            ProbeConfigurationsPath = probeFileLocation;
        }
        else if (isAgentMode)
        {
            ProbeMode = ProbeMode.Agent;
            ProbeConfigurationsPath = $"{url}{DefaultProbeConfigurationBackendPath}/{ServiceName}";
        }
        else
        {
            ProbeMode = ProbeMode.Backend;
            ProbeConfigurationsPath = $"{url}{DefaultProbeConfigurationAgentPath}";
        }

        var pollInterval = configurationSource?.GetInt32(ConfigurationKeys.Debugger.PollInterval);
        ProbeConfigurationsPollIntervalSeconds = pollInterval is null or <= 0
                           ? DefaultConfigurationsPollIntervalSeconds
                           : pollInterval.Value;

        Version = configurationSource?.GetString(ConfigurationKeys.ServiceVersion);
        Environment = configurationSource?.GetString(ConfigurationKeys.Environment);

        Enabled = configurationSource?.GetBool(ConfigurationKeys.Debugger.DebuggerEnabled) ?? false;

        MaxDepthToSerialize = configurationSource?.GetInt32(ConfigurationKeys.Debugger.MaxDepthToSerialize) ?? DefaultMaxDepthToSerialize;

        SerializationTimeThreshold = configurationSource?.GetInt32(ConfigurationKeys.Debugger.SerializationTimeThreshold) ?? DefaultSerializationTimeThreshold;
    }

    public ProbeMode ProbeMode { get; set; }

    public string ApiKey { get; set; }

    public string HostId { get; set; }

    public string ServiceName { get; set; }

    public int ProbeConfigurationsPollIntervalSeconds { get; set; }

    public string ProbeConfigurationsPath { get; set; }

    public string Version { get; set; }

    public string Environment { get; set; }

    public bool Enabled { get; }

    public int SerializationTimeThreshold { get; }

    public int MaxDepthToSerialize { get; }

    public static DebuggerSettings FromDefaultSources()
    {
        var source = GlobalSettings.CreateDefaultConfigurationSource();
        return new DebuggerSettings(source);
    }
}
