// <copyright file="GitInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.IO;

namespace Datadog.Trace.Ci.CiEnvironment;

internal abstract class GitInfoProvider : IGitInfoProvider
{
    protected static DirectoryInfo? GetParentGitFolder(string? innerFolder)
    {
        if (string.IsNullOrEmpty(innerFolder))
        {
            return null;
        }

        var dirInfo = new DirectoryInfo(innerFolder);
        while (dirInfo != null)
        {
            var gitDirectories = dirInfo.GetDirectories(".git");
            if (gitDirectories.Length > 0)
            {
                foreach (var gitDir in gitDirectories)
                {
                    if (gitDir.Name == ".git")
                    {
                        return gitDir;
                    }
                }
            }

            dirInfo = dirInfo.Parent;
        }

        return null;
    }

    public bool TryGetFrom(string folder, out IGitInfo? gitInfo)
    {
        // Try to load git metadata from the folder
        if (TryGetFrom(new DirectoryInfo(folder), out gitInfo) && gitInfo != null)
        {
            return true;
        }

        // If not let's try to find the .git folder in a parent folder
        var parentGitFolder = GitInfo.GetParentGitFolder(folder);
        if (parentGitFolder != null && TryGetFrom(parentGitFolder, out var pFolderGitInfo) && pFolderGitInfo != null)
        {
            gitInfo = pFolderGitInfo;
            return true;
        }

        gitInfo = null;
        return false;
    }

    protected abstract bool TryGetFrom(DirectoryInfo gitDirectory, out IGitInfo? gitInfo);
}
