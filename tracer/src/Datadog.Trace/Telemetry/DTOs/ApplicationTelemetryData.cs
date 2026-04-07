// <copyright file="ApplicationTelemetryData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry;

internal sealed class ApplicationTelemetryData
{
    public ApplicationTelemetryData(string serviceName, string env, string serviceVersion, string tracerVersion, string languageName, string languageVersion, string runtimeName, string runtimeVersion, string? commitSha, string? repositoryUrl, string? processTags)
    {
        ServiceName = serviceName;
        Env = env;
        ServiceVersion = serviceVersion;
        TracerVersion = tracerVersion;
        LanguageName = languageName;
        LanguageVersion = languageVersion;
        RuntimeName = runtimeName;
        RuntimeVersion = runtimeVersion;
        CommitSha = commitSha;
        RepositoryUrl = repositoryUrl;
        ProcessTags = processTags;
    }

    public string ServiceName { get; set; }

    public string Env { get; set; }

    public string ServiceVersion { get; set; }

    public string TracerVersion { get; set; }

    public string LanguageName { get; set; }

    public string LanguageVersion { get; set; }

    public string RuntimeName { get; set; }

    public string RuntimeVersion { get; set; }

    public string? RuntimePatches { get; set; }

    public string? CommitSha { get; set; }

    public string? RepositoryUrl { get; set; }

    public string? ProcessTags { get; set; }
}
