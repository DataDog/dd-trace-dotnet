// <copyright file="ApplicationTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Telemetry.Collectors;

internal class ApplicationTelemetryCollector
{
    private ApplicationTelemetryData? _applicationData = null;
    private HostTelemetryData? _hostData = null;

    public void RecordTracerSettings(
        ImmutableTracerSettings tracerSettings,
        string defaultServiceName)
    {
        // Try to retrieve config based Git Info
        GitMetadata? gitMetadata = null;
        if (tracerSettings.GitMetadataEnabled && !string.IsNullOrEmpty(tracerSettings.GitCommitSha) && !string.IsNullOrEmpty(tracerSettings.GitRepositoryUrl))
        {
            gitMetadata = new GitMetadata(tracerSettings.GitCommitSha!, tracerSettings.GitRepositoryUrl!);
        }

        var frameworkDescription = FrameworkDescription.Instance;
        var application = new ApplicationTelemetryData(
            serviceName: defaultServiceName,
            env: tracerSettings.EnvironmentInternal ?? string.Empty, // required, but we don't have it
            serviceVersion: tracerSettings.ServiceVersionInternal ?? string.Empty, // required, but we don't have it
            tracerVersion: TracerConstants.AssemblyVersion,
            languageName: TracerConstants.Language,
            languageVersion: frameworkDescription.ProductVersion,
            runtimeName: frameworkDescription.Name,
            runtimeVersion: frameworkDescription.ProductVersion,
            commitSha: gitMetadata?.CommitSha,
            repositoryUrl: gitMetadata?.RepositoryUrl);

        Interlocked.Exchange(ref _applicationData, application);

        // The host properties can't change, so only need to set them the first time
        if (Volatile.Read(ref _hostData) is not null)
        {
            return;
        }

        var host = HostMetadata.Instance;
        var osDescription = frameworkDescription.OSArchitecture == "x86"
                                ? $"{frameworkDescription.OSDescription} (32bit)"
                                : frameworkDescription.OSDescription;

        _hostData = new HostTelemetryData(
            hostname: host.Hostname ?? string.Empty, // this is required, but we don't have it
            os: frameworkDescription.OSPlatform,
            architecture: frameworkDescription.ProcessArchitecture)
        {
            OsVersion = osDescription,
            KernelName = host.KernelName,
            KernelRelease = host.KernelRelease,
            KernelVersion = host.KernelVersion
        };
    }

    public void RecordGitMetadata(GitMetadata gitMetadata)
    {
        if (_applicationData is null || gitMetadata.IsEmpty)
        {
            return;
        }

        var application = new ApplicationTelemetryData(
            serviceName: _applicationData.ServiceName,
            env: _applicationData.Env,
            serviceVersion: _applicationData.ServiceVersion,
            tracerVersion: _applicationData.TracerVersion,
            languageName: _applicationData.LanguageName,
            languageVersion: _applicationData.LanguageVersion,
            runtimeName: _applicationData.RuntimeName,
            runtimeVersion: _applicationData.RuntimeVersion,
            commitSha: gitMetadata.CommitSha,
            repositoryUrl: gitMetadata.RepositoryUrl);

        Interlocked.Exchange(ref _applicationData, application);
    }

    /// <summary>
    /// Get the application data. Will be null if not yet initialized.
    /// </summary>
    public ApplicationTelemetryData? GetApplicationData() => _applicationData;

    /// <summary>
    /// Get the host data. Will be null if not yet initialized.
    /// </summary>
    public HostTelemetryData? GetHostData() => _hostData;
}
