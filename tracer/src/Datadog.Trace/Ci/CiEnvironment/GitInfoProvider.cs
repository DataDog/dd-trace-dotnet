// <copyright file="GitInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Datadog.Trace.Ci.CiEnvironment;

internal abstract class GitInfoProvider : IGitInfoProvider
{
    public bool TryGetFrom(string folder, [NotNullWhen(true)] out IGitInfo? gitInfo)
    {
        // Try to load git metadata from the folder
        if (TryGetFrom(new DirectoryInfo(folder), out gitInfo))
        {
            return true;
        }

        // If not let's try to find the .git folder in a parent folder
        var parentGitFolder = GitInfo.GetParentGitFolder(folder);
        IGitInfo? pFolderGitInfo = null;
        if (parentGitFolder != null && TryGetFrom(parentGitFolder, out pFolderGitInfo))
        {
            gitInfo = pFolderGitInfo;
            return true;
        }

        if (gitInfo != null || pFolderGitInfo != null)
        {
            gitInfo = new GitInfo
            {
                SourceRoot = gitInfo?.SourceRoot ?? pFolderGitInfo?.SourceRoot,
            };
            gitInfo.Errors.AddRange((gitInfo?.Errors.Concat(pFolderGitInfo?.Errors ?? Enumerable.Empty<string>()) ?? pFolderGitInfo?.Errors) ?? Array.Empty<string>());
        }
        else
        {
            gitInfo = null;
        }

        return false;
    }

    public abstract bool TryGetFrom(FileSystemInfo gitDirectory, [NotNullWhen(true)] out IGitInfo? gitInfo);
}
