// <copyright file="IGitInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Ci.CiEnvironment;

/// <summary>
/// Git info interface
/// </summary>
internal interface IGitInfo
{
    /// <summary>
    /// Gets Source root
    /// </summary>
    string? SourceRoot { get; }

    /// <summary>
    /// Gets Repository
    /// </summary>
    string? Repository { get; }

    /// <summary>
    /// Gets Branch
    /// </summary>
    string? Branch { get; }

    /// <summary>
    /// Gets Commit
    /// </summary>
    string? Commit { get; }

    /// <summary>
    /// Gets Author Name
    /// </summary>
    string? AuthorName { get; }

    /// <summary>
    /// Gets Author Email
    /// </summary>
    string? AuthorEmail { get; }

    /// <summary>
    /// Gets Author Date
    /// </summary>
    DateTimeOffset? AuthorDate { get; }

    /// <summary>
    /// Gets Committer Name
    /// </summary>
    string? CommitterName { get; }

    /// <summary>
    /// Gets Committer Email
    /// </summary>
    string? CommitterEmail { get; }

    /// <summary>
    /// Gets Committer Date
    /// </summary>
    DateTimeOffset? CommitterDate { get; }

    /// <summary>
    /// Gets Commit Message
    /// </summary>
    string? Message { get; }

    /// <summary>
    /// Gets parsing errors
    /// </summary>
    public List<string> Errors { get; }
}
