// <copyright file="ExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

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
    /// <remarks>As of tracer version 3.27.0, this property cannot be used to set the
    /// agent URI. You must instead use a static configuration source such
    /// as environment variables or datadog.json to set the value instead. This
    /// property will be marked obsolete and removed in a future version of Datadog.Trace.
    /// </remarks>
    [Obsolete("This property is obsolete and will be removed in a future version. To set the AgentUri, use the TracerSettings.AgentUri property")]
    public Uri AgentUri
    {
        get => _tracerSettings.AgentUri;
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        set => _tracerSettings.AgentUri = value;
    }
}
