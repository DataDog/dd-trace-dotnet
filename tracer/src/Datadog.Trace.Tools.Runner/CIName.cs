// <copyright file="CIName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner;

internal enum CIName
{
    Unknown,
    Travis,
    CircleCI,
    Jenkins,
    Gitlab,
    AppVeyor,
    AzurePipelines,
    Bitbucket,
    GithubActions,
    Teamcity,
    Buildkite
}
