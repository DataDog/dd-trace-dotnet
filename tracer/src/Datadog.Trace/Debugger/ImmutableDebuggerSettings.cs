// <copyright file="ImmutableDebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger;

internal class ImmutableDebuggerSettings
{
    private ImmutableDebuggerSettings(
        ProbeMode probeMode,
        string apiKey,
        string hostId,
        int probeConfigurationsPollIntervalSeconds,
        string probeConfigurationsPath,
        string version,
        string environment)
    {
        ProbeMode = probeMode;
        ApiKey = apiKey;
        HostId = hostId;
        ProbeConfigurationsPollIntervalSeconds = probeConfigurationsPollIntervalSeconds;
        ProbeConfigurationsPath = probeConfigurationsPath;
        Version = version;
        Environment = environment;
    }

    public ProbeMode ProbeMode { get; }

    public string ApiKey { get; }

    public string HostId { get; }

    public string ProbeConfigurationsPath { get; }

    public int ProbeConfigurationsPollIntervalSeconds { get; }

    public string Version { get; }

    public string Environment { get; }

    public static ImmutableDebuggerSettings Create(TracerSettings tracerSettings) =>
        Create(tracerSettings.DebuggerSettings);

    public static ImmutableDebuggerSettings Create(DebuggerSettings debuggerSettings) =>
        Create(
            debuggerSettings.ProbeMode,
            debuggerSettings.ApiKey,
            debuggerSettings.HostId,
            debuggerSettings.ProbeConfigurationsPollIntervalSeconds,
            debuggerSettings.ProbeConfigurationsPath,
            debuggerSettings.Version,
            debuggerSettings.Environment);

    public static ImmutableDebuggerSettings Create(
        ProbeMode probeMode,
        string apiKey,
        string hostId,
        int probeConfigurationsPollIntervalSeconds,
        string probeConfigurationsPath,
        string version,
        string environment)
    {
        return new ImmutableDebuggerSettings(probeMode, apiKey, hostId, probeConfigurationsPollIntervalSeconds, probeConfigurationsPath, version, environment);
    }
}
