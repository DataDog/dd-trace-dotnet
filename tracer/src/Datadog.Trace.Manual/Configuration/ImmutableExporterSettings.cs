// <copyright file="ImmutableExporterSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains exporter related settings.
/// </summary>
[Obsolete("This class is obsolete and will be removed in a future version. To fetch the current AgentUri, use the ImmutableTracerSettings.AgentUri property")]
public sealed class ImmutableExporterSettings
{
    private readonly ImmutableTracerSettings _tracerSettings;

    internal ImmutableExporterSettings(ImmutableTracerSettings tracerSettings)
    {
        _tracerSettings = tracerSettings;
    }

    /// <summary>
    /// Gets the Uri where the Tracer can connect to the Agent.
    /// </summary>
    [Obsolete("This property is obsolete and will be removed in a future version. To fetch the current AgentUri, use the ImmutableTracerSettings.AgentUri property")]
    public Uri AgentUri => _tracerSettings.AgentUri;
}
