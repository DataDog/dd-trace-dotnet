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
    private GitMetadata? _gitMetadata = null;
    private ApplicationTelemetryData? _applicationData = null;
    private HostTelemetryData? _hostData = null;

    public void RecordTracerSettings(
        ImmutableTracerSettings tracerSettings,
        string defaultServiceName)
    {
        // Try to retrieve config based Git Info
        // If explicitly provided, these values take precedence
        GitMetadata? gitMetadata = _gitMetadata;
        if (tracerSettings.GitMetadataEnabled && !string.IsNullOrEmpty(tracerSettings.GitCommitSha) && !string.IsNullOrEmpty(tracerSettings.GitRepositoryUrl))
        {
            gitMetadata = new GitMetadata(tracerSettings.GitCommitSha!, tracerSettings.GitRepositoryUrl!);
            Interlocked.Exchange(ref _gitMetadata, gitMetadata);
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
        if (gitMetadata.IsEmpty)
        {
            return;
        }

        Interlocked.Exchange(ref _gitMetadata, gitMetadata);

        if (_applicationData is null)
        {
            return;
        }

        while (true)
        {
            var original = _applicationData;
            var application = new ApplicationTelemetryData(
                serviceName: original.ServiceName,
                env: original.Env,
                serviceVersion: original.ServiceVersion,
                tracerVersion: original.TracerVersion,
                languageName: original.LanguageName,
                languageVersion: original.LanguageVersion,
                runtimeName: original.RuntimeName,
                runtimeVersion: original.RuntimeVersion,
                commitSha: gitMetadata.CommitSha,
                repositoryUrl: gitMetadata.RepositoryUrl);

            var updated = Interlocked.Exchange(ref _applicationData, application);
            if (updated == original)
            {
                // nothing changed in the background, so jump out
                break;
            }
        }
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
