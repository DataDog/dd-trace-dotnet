// <copyright file="ExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains exporter settings.
/// </summary>
[Obsolete("This class is obsolete and will be removed in a future version. To set the AgentUri, use the TracerSettings.AgentUri property")]
public sealed class ExporterSettings
{
    private readonly TracerSettings _tracerSettings;

    internal ExporterSettings(TracerSettings tracerSettings)
    {
        _tracerSettings = tracerSettings;
    }

    /// <summary>
    /// Gets or sets the Uri where the Tracer can connect to the Agent.
    /// Default is <c>"http://localhost:8126"</c>.
    /// </summary>
    [Obsolete("This property is obsolete and will be removed in a future version. To set the AgentUri, use the TracerSettings.AgentUri property")]
    public Uri AgentUri
    {
        get => _tracerSettings.AgentUri;
        set => _tracerSettings.AgentUri = value;
    }
}
