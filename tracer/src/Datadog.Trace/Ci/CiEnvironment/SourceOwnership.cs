// <copyright file="SourceOwnership.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci.CiEnvironment;

internal readonly struct SourceOwnership
{
    internal SourceOwnership(string repositoryRelativePath, string[] matchingOwners, bool isRepositoryRelative)
    {
        RepositoryRelativePath = repositoryRelativePath;
        MatchingOwners = matchingOwners;
        IsRepositoryRelative = isRepositoryRelative;
    }

    internal string RepositoryRelativePath { get; }

    internal string[] MatchingOwners { get; }

    [TestingAndPrivateOnly]
    internal bool IsRepositoryRelative { get; }
}
