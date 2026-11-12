// <copyright file="RemoteConfigEndpoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal sealed class RemoteConfigEndpoint
{
    // Same as the timeout of the managed transport, see Transport.RemoteConfigurationApiFactory
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public RemoteConfigEndpoint(string url, TimeSpan timeout, ExporterSettings? exporterSettings = null)
    {
        Url = url;
        TimeoutMs = (ulong)timeout.TotalMilliseconds;
        ExporterSettings = exporterSettings;
    }

    /// <summary>
    /// Gets the agent url, in the form libdatadog expects: http://host:port, unix:///path or windows:\\.\pipe\name
    /// </summary>
    public string Url { get; }

    public ulong TimeoutMs { get; }

    /// <summary>
    /// Gets the settings the endpoint was created from, which the managed client creates its transport from
    /// </summary>
    public ExporterSettings? ExporterSettings { get; }

    public static RemoteConfigEndpoint FromExporterSettings(ExporterSettings exporter)
        => new(
            exporter.TracesTransport switch
            {
                TracesTransportType.WindowsNamedPipe => $@"windows:\\.\pipe\{exporter.TracesPipeName}",
                TracesTransportType.UnixDomainSocket => $"unix://{exporter.TracesUnixDomainSocketPath}",
                _ => exporter.AgentUri.ToString(),
            },
            Timeout,
            exporter);
}
