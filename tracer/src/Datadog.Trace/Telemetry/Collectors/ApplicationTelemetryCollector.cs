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

internal sealed class ApplicationTelemetryCollector
{
    private GitMetadata? _gitMetadata = null;
    private ApplicationTelemetryData? _applicationData = null;
    private HostTelemetryData? _hostData = null;

    public void RecordTracerSettings(TracerSettings tracerSettings)
    {
        string? processTags = null;
        if (tracerSettings.PropagateProcessTags)
        {
            var pTags = ProcessTags.SerializedTags;
            if (!string.IsNullOrEmpty(processTags))
            {
                processTags = pTags;
            }
        }

        RecordMutableSettings(tracerSettings, tracerSettings.Manager.InitialMutableSettings, processTags);

        // The host properties can't change, so only need to set them the first time
        if (Volatile.Read(ref _hostData) is not null)
        {
            return;
        }

        var frameworkDescription = FrameworkDescription.Instance;
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

    public void RecordMutableSettings(TracerSettings tracerSettings, MutableSettings mutableSettings)
        => RecordMutableSettings(tracerSettings, mutableSettings, null);

    private void RecordMutableSettings(TracerSettings tracerSettings, MutableSettings mutableSettings, string? processTags)
    {
        // Try to retrieve config based Git Info
        GitMetadata? gitMetadata;
        // If explicitly provided, these values take precedence
        if (tracerSettings.GitMetadataEnabled && !StringUtil.IsNullOrEmpty(mutableSettings.GitCommitSha) && !StringUtil.IsNullOrEmpty(mutableSettings.GitRepositoryUrl))
        {
            gitMetadata = new GitMetadata(mutableSettings.GitCommitSha, mutableSettings.GitRepositoryUrl);
            Interlocked.Exchange(ref _gitMetadata, gitMetadata);
        }
        else
        {
            gitMetadata = Volatile.Read(ref _gitMetadata);
        }

        var frameworkDescription = FrameworkDescription.Instance;
        var application = new ApplicationTelemetryData(
            serviceName: mutableSettings.DefaultServiceName,
            env: mutableSettings.Environment ?? string.Empty, // required, but we don't have it
            serviceVersion: mutableSettings.ServiceVersion ?? string.Empty, // required, but we don't have it
            tracerVersion: TracerConstants.AssemblyVersion,
            languageName: TracerConstants.Language,
            languageVersion: frameworkDescription.ProductVersion,
            runtimeName: frameworkDescription.Name,
            runtimeVersion: frameworkDescription.ProductVersion,
            commitSha: gitMetadata?.CommitSha,
            repositoryUrl: gitMetadata?.RepositoryUrl,
            processTags: processTags ?? Volatile.Read(ref _applicationData)?.ProcessTags);

        Interlocked.Exchange(ref _applicationData, application);
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
            var original = Volatile.Read(ref _applicationData);
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
                repositoryUrl: gitMetadata.RepositoryUrl,
                processTags: original.ProcessTags);

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
