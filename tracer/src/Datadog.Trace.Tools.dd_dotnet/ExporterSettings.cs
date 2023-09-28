// <copyright file="ExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Tools.dd_dotnet;

// ReSharper disable once CheckNamespace - Needed for compatiblity with linked files
namespace Datadog.Trace.Configuration;

/// <summary>
/// Lightweight version of ExporterSettings for the tool
/// </summary>
public partial class ExporterSettings
{
    private readonly Func<string, bool> _fileExists = File.Exists;
    private readonly DummyTelemetry _telemetry = new();

    private Uri _agentUri;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExporterSettings"/> class.
    /// </summary>
    /// <param name="configuration">Configuration source</param>
    public ExporterSettings(IConfigurationSource? configuration)
    {
        var agentUri = GetValue(configuration, ConfigurationKeys.AgentUri);
        var tracePipeName = GetValue(configuration, ConfigurationKeys.TracesPipeName);
        var agentHost = GetValue(configuration, ConfigurationKeys.AgentHost, "DD_TRACE_AGENT_HOSTNAME", "DATADOG_TRACE_AGENT_HOSTNAME");
        var agentPortStr = GetValue(configuration, ConfigurationKeys.AgentPort, "DATADOG_TRACE_AGENT_PORT");
        var unixDomainSocketPath = GetValue(configuration, ConfigurationKeys.TracesUnixDomainSocketPath);

        int.TryParse(agentPortStr, out var agentPort);

        ConfigureTraceTransport(agentUri, tracePipeName, agentHost, agentPort, unixDomainSocketPath);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExporterSettings"/> class.
    /// </summary>
    /// <param name="agentUri">Agent URI</param>
    public ExporterSettings(string agentUri)
    {
        ConfigureTraceTransport(agentUri, null, null, null, null);
    }

    internal enum TelemetryErrorCode
    {
        PotentiallyInvalidUdsPath
    }

    internal Uri AgentUri => _agentUri;

    private List<string> ValidationWarnings { get; set; } = new();

    internal TracesTransportType TracesTransport { get; private set; }

    internal string? TracesPipeNameInternal { get; private set; }

    internal string? TracesUnixDomainSocketPathInternal { get; private set; }

    private static string? GetValue(IConfigurationSource? configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration?.GetString(key);

            if (value != null)
            {
                return value;
            }
        }

        return null;
    }

    private void RecordTraceTransport(string transport, ConfigurationOrigins origin = ConfigurationOrigins.Default)
    {
    }

    private class DummyTelemetry
    {
        public void Record(string key, string? value, bool recordValue, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
        {
        }
    }
}
