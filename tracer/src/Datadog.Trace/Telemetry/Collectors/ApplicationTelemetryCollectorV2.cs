// <copyright file="ApplicationTelemetryCollectorV2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Telemetry.Collectors;

internal class ApplicationTelemetryCollectorV2
{
    private ApplicationTelemetryDataV2? _applicationData = null;
    private HostTelemetryDataV2? _hostData = null;

    public void RecordTracerSettings(
        ImmutableTracerSettings tracerSettings,
        string defaultServiceName)
    {
        var frameworkDescription = FrameworkDescription.Instance;
        var application = new ApplicationTelemetryDataV2(
            serviceName: defaultServiceName,
            env: tracerSettings.Environment ?? string.Empty, // required, but we don't have it
            serviceVersion: tracerSettings.ServiceVersion ?? string.Empty, // required, but we don't have it
            tracerVersion: TracerConstants.AssemblyVersion,
            languageName: TracerConstants.Language,
            languageVersion: frameworkDescription.ProductVersion,
            runtimeName: frameworkDescription.Name,
            runtimeVersion: frameworkDescription.ProductVersion);

        Interlocked.Exchange(ref _applicationData, application);

        // The host properties can't change, so only need to set them the first time
        if (Volatile.Read(ref _hostData) is not null)
        {
            return;
        }

        var host = HostMetadata.Instance;
        _hostData = new HostTelemetryDataV2(
            hostname: host.Hostname ?? string.Empty, // this is required, but we don't have it
            os: frameworkDescription.OSPlatform,
            architecture: frameworkDescription.ProcessArchitecture)
        {
            OsVersion = Environment.OSVersion.ToString(),
            KernelName = host.KernelName,
            KernelRelease = host.KernelRelease,
            KernelVersion = host.KernelVersion
        };
    }

    /// <summary>
    /// Get the application data. Will be null if not yet initialized.
    /// </summary>
    public ApplicationTelemetryDataV2? GetApplicationData() => _applicationData;

    /// <summary>
    /// Get the host data. Will be null if not yet initialized.
    /// </summary>
    public HostTelemetryDataV2? GetHostData() => _hostData;
}
