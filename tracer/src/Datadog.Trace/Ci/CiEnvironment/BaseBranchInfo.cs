// <copyright file="BaseBranchInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.CiEnvironment;

/// <summary>
/// Contains information about the detected base branch.
/// <see cref="GitCommandHelper.DetectBaseBranch"/>
/// </summary>
public sealed class BaseBranchInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseBranchInfo"/> class.
    /// </summary>
    /// <param name="baseBranch">The name of the detected base branch.</param>
    /// <param name="mergeBaseSha">The SHA of the merge-base commit.</param>
    /// <param name="behind">Number of commits the target branch is behind the base branch.</param>
    /// <param name="ahead">Number of commits the target branch is ahead of the base branch.</param>
    /// <param name="isDefaultBranch">Whether the base branch is the default branch.</param>
    public BaseBranchInfo(string baseBranch, string mergeBaseSha, int behind, int ahead, bool isDefaultBranch)
    {
        BaseBranch = baseBranch;
        MergeBaseSha = mergeBaseSha;
        Behind = behind;
        Ahead = ahead;
        IsDefaultBranch = isDefaultBranch;
    }

    /// <summary>
    /// Gets the name of the detected base branch.
    /// </summary>
    public string BaseBranch { get; }

    /// <summary>
    /// Gets the SHA of the merge-base commit between the target branch and base branch.
    /// </summary>
    public string MergeBaseSha { get; }

    /// <summary>
    /// Gets the number of commits the target branch is behind the base branch.
    /// </summary>
    public int Behind { get; }

    /// <summary>
    /// Gets the number of commits the target branch is ahead of the base branch.
    /// </summary>
    public int Ahead { get; }

    /// <summary>
    /// Gets a value indicating whether the base branch is the default branch of the repository.
    /// </summary>
    public bool IsDefaultBranch { get; }
}
