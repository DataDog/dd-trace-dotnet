// <copyright file="SourceLinkInformationExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Datadog.Trace.Pdb;

internal static class SourceLinkInformationExtractor
{
    public static bool TryGetSourceLinkInfo(Assembly assembly, [NotNullWhen(true)] out string? commitSha, [NotNullWhen(true)] out string? repositoryUrl)
    {
        commitSha = null;
        repositoryUrl = null;
        return false;
    }
}
