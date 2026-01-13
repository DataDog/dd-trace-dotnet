// <copyright file="CIEnvironmentValuesGenerics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.CiEnvironment;

#pragma warning disable SA1649
// ReSharper disable once InconsistentNaming

internal abstract class CIEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues
    where TValueProvider : struct, IValueProvider
{
    protected TValueProvider ValueProvider { get; } = valueProvider;

    internal static CIEnvironmentValues Create(TValueProvider valueProvider)
    {
        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.Travis)))
        {
            return new TravisEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.CircleCI)))
        {
            return new CircleCiEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.JenkinsUrl)))
        {
            return new JenkinsEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.GitlabCI)))
        {
            return new GitlabEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.Appveyor)))
        {
            return new AppveyorEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.AzureTFBuild)))
        {
            return new AzurePipelinesEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.BitBucketCommit)))
        {
            return new BitbucketEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.GitHubSha)))
        {
            return new GithubActionsEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.TeamCityVersion)))
        {
            return new TeamcityEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.BuildKite)))
        {
            return new BuildkiteEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.BitriseBuildSlug)))
        {
            return new BitriseEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.Buddy)))
        {
            return new BuddyEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.CodefreshBuildId)))
        {
            return new CodefreshEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (valueProvider.GetValue(Constants.AWSCodePipelineBuildInitiator) is { Length: > 0 } initiator &&
            initiator.StartsWith("codepipeline"))
        {
            return new AWSCodePipelineEnvironmentValues<TValueProvider>(valueProvider);
        }

        if (!string.IsNullOrEmpty(valueProvider.GetValue(Constants.Drone)))
        {
            return new DroneEnvironmentValues<TValueProvider>(valueProvider);
        }

        return new UnsupportedCIEnvironmentValues<TValueProvider>(valueProvider);
    }

    protected override void Setup(IGitInfo gitInfo)
    {
        if (gitInfo.Errors.Count > 0)
        {
            var sb = StringBuilderCache.Acquire();
            sb.AppendLine();
            foreach (var err in gitInfo.Errors)
            {
                sb.AppendLine(" Error: " + err);
            }

            Log.Warning("CIEnvironmentValues: Errors detected in the local gitInfo: {Errors}", StringBuilderCache.GetStringAndRelease(sb));
        }

        OnInitialize(gitInfo);

        // **********
        // Add Author, Committer and Message from git
        // **********
        // Merge commits have a different commit hash from the one reported by the CI.
        if (gitInfo.Commit == Commit)
        {
            if (string.IsNullOrWhiteSpace(AuthorName) || string.IsNullOrWhiteSpace(AuthorEmail))
            {
                if (!string.IsNullOrWhiteSpace(gitInfo.AuthorEmail))
                {
                    AuthorEmail = gitInfo.AuthorEmail;
                }

                if (!string.IsNullOrWhiteSpace(gitInfo.AuthorName))
                {
                    AuthorName = gitInfo.AuthorName;
                }
            }

            AuthorDate ??= gitInfo.AuthorDate;

            if (string.IsNullOrWhiteSpace(CommitterName) || string.IsNullOrWhiteSpace(CommitterEmail))
            {
                if (!string.IsNullOrWhiteSpace(gitInfo.CommitterEmail))
                {
                    CommitterEmail = gitInfo.CommitterEmail;
                }

                if (!string.IsNullOrWhiteSpace(gitInfo.CommitterName))
                {
                    CommitterName = gitInfo.CommitterName;
                }
            }

            CommitterDate ??= gitInfo.CommitterDate;

            if (!string.IsNullOrWhiteSpace(gitInfo.Message))
            {
                // Some CI's (eg Azure) adds the `Merge X into Y` message to the Pull Request
                // If we have the original commit message we use that.
                if (string.IsNullOrWhiteSpace(Message) ||
                    (Message!.StartsWith("Merge", StringComparison.Ordinal) &&
                     !gitInfo.Message!.StartsWith("Merge", StringComparison.Ordinal)))
                {
                    Message = gitInfo.Message;
                }
            }
        }
        else
        {
            Log.Warning("Git commit in .git folder is different from the one in the environment variables. [{GitCommit} != {EnvVarCommit}]", gitInfo.Commit, Commit);
        }

        // **********
        // Get all head commit information.
        // **********
        if (!StringUtil.IsNullOrEmpty(HeadCommit))
        {
            // fetching commit data from head commit
            if (GitCommandHelper.FetchCommitData(WorkspacePath ?? Environment.CurrentDirectory, HeadCommit) is { } commitData &&
                commitData.CommitSha == HeadCommit)
            {
                HeadAuthorDate = commitData.AuthorDate;
                HeadAuthorEmail = commitData.AuthorEmail;
                HeadAuthorName = commitData.AuthorName;
                HeadCommitterDate = commitData.CommitterDate;
                HeadCommitterEmail = commitData.CommitterEmail;
                HeadCommitterName = commitData.CommitterName;
                HeadMessage = commitData.CommitMessage;
            }
            else
            {
                Log.Warning("Error fetching data for git commit '{HeadCommit}'", HeadCommit);
            }
        }

        // **********
        // Expand ~ in Paths
        // **********

        SourceRoot = ExpandPath(SourceRoot);
        WorkspacePath = ExpandPath(WorkspacePath);

        // **********
        // Custom environment variables.
        // **********
        Branch = GetVariableIfIsNotEmpty(Constants.DDGitBranch, Branch);
        Tag = GetVariableIfIsNotEmpty(Constants.DDGitTag, Tag);
        Repository = GetVariableIfIsNotEmpty(
            Constants.DDGitRepository,
            Repository,
            (value, defaultValue) =>
            {
                if (value is not null)
                {
                    value = value.Trim();
                    if (value.Length == 0)
                    {
                        if (string.IsNullOrEmpty(defaultValue))
                        {
                            Log.ErrorSkipTelemetry("DD_GIT_REPOSITORY_URL is set with an empty value, and the Git repository could not be automatically extracted");
                        }
                        else
                        {
                            Log.ErrorSkipTelemetry("DD_GIT_REPOSITORY_URL is set with an empty value, defaulting to '{Default}'", defaultValue);
                        }

                        return false;
                    }

                    if (Regex.Match(value, RepositoryUrlPattern).Length != value.Length)
                    {
                        if (string.IsNullOrEmpty(defaultValue))
                        {
                            Log.ErrorSkipTelemetry("DD_GIT_REPOSITORY_URL is set with an invalid value ('{Value}'), and the Git repository could not be automatically extracted", value);
                        }
                        else
                        {
                            Log.ErrorSkipTelemetry("DD_GIT_REPOSITORY_URL is set with an invalid value ('{Value}'), defaulting to '{Default}'", value, defaultValue);
                        }

                        return false;
                    }

                    // All ok!
                    return true;
                }

                if (string.IsNullOrEmpty(defaultValue))
                {
                    Log.Warning("The Git repository couldn't be automatically extracted.");
                }

                // If not set use the default value
                return false;
            });
        Commit = GetVariableIfIsNotEmpty(
            Constants.DDGitCommitSha,
            Commit,
            (value, defaultValue) =>
            {
                if (value is not null)
                {
                    value = value.Trim();
                    if (value.Length != 40 || !IsHex(value))
                    {
                        if (string.IsNullOrEmpty(defaultValue))
                        {
                            Log.Error("DD_GIT_COMMIT_SHA must be a full-length git SHA ({Value}), and the The Git commit sha couldn't be automatically extracted.", value);
                        }
                        else
                        {
                            Log.Error("DD_GIT_COMMIT_SHA must be a full-length git SHA ({Value}), defaulting to '{Default}", value, defaultValue);
                        }

                        return false;
                    }

                    // All ok!
                    return true;
                }

                if (string.IsNullOrEmpty(defaultValue))
                {
                    Log.Warning("The Git commit sha couldn't be automatically extracted.");
                }

                // If not set use the default value
                return false;
            });
        Message = GetVariableIfIsNotEmpty(Constants.DDGitCommitMessage, Message);
        AuthorName = GetVariableIfIsNotEmpty(Constants.DDGitCommitAuthorName, AuthorName);
        AuthorEmail = GetVariableIfIsNotEmpty(Constants.DDGitCommitAuthorEmail, AuthorEmail);
        AuthorDate = GetDateTimeOffsetVariableIfIsNotEmpty(Constants.DDGitCommitAuthorDate, AuthorDate);
        CommitterName = GetVariableIfIsNotEmpty(Constants.DDGitCommitCommiterName, CommitterName);
        CommitterEmail = GetVariableIfIsNotEmpty(Constants.DDGitCommitCommiterEmail, CommitterEmail);
        CommitterDate = GetDateTimeOffsetVariableIfIsNotEmpty(Constants.DDGitCommitCommiterDate, CommitterDate);
        PrBaseBranch = GetVariableIfIsNotEmpty(Constants.DDGitPullRequestBaseBranch, PrBaseBranch);
        PrBaseCommit = GetVariableIfIsNotEmpty(Constants.DDGitPullRequestBaseBranchSha, PrBaseCommit, (value, defaultValue) =>
        {
            if (value is not null)
            {
                value = value.Trim();
                if (value.Length != 40 || !IsHex(value))
                {
                    if (string.IsNullOrEmpty(defaultValue))
                    {
                        Log.Error("DD_GIT_PULL_REQUEST_BASE_BRANCH_SHA must be a full-length git SHA ({Value}), and the The Git commit sha couldn't be automatically extracted.", value);
                    }
                    else
                    {
                        Log.Error("DD_GIT_CODD_GIT_PULL_REQUEST_BASE_BRANCH_SHAMMIT_SHA must be a full-length git SHA ({Value}), defaulting to '{Default}", value, defaultValue);
                    }

                    return false;
                }

                // All ok!
                return true;
            }

            // If not set use the default value
            return false;
        });

        Message = Message?.Trim();
    }

    protected string? GetVariableIfIsNotEmpty(string key, string? defaultValue, Func<string?, string?, bool>? validator = null)
    {
        var value = ValueProvider.GetValue(key, defaultValue);
        if (validator is not null)
        {
            if (!validator.Invoke(value, defaultValue))
            {
                return defaultValue;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
        }

        return value;
    }

    protected DateTimeOffset? GetDateTimeOffsetVariableIfIsNotEmpty(string key, DateTimeOffset? defaultValue)
    {
        var value = ValueProvider.GetValue(key);
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        if (DateTimeOffset.TryParseExact(value, "yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDateTimeOffset))
        {
            return valueDateTimeOffset;
        }

        return defaultValue;
    }

    protected void SetVariablesIfNotEmpty(Dictionary<string, string?> dictionary, params string[] keys)
        => SetVariablesIfNotEmpty(dictionary, keys, null);

    protected void SetVariablesIfNotEmpty(Dictionary<string, string?>? dictionary, string[]? keys, Func<KeyValuePair<string, string?>, string?>? filter)
    {
        if (dictionary is null || keys is null)
        {
            return;
        }

        foreach (var key in keys)
        {
            if (key is null)
            {
                continue;
            }

            var value = ValueProvider.GetValue(key);
            if (!string.IsNullOrEmpty(value))
            {
                if (filter is not null)
                {
                    value = filter(new KeyValuePair<string, string?>(key, value));
                }

                dictionary[key] = value;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected string? ExpandPath(string? path)
    {
        if (path == "~" || path?.StartsWith("~/") == true)
        {
            var homePath = (Environment.OSVersion.Platform == PlatformID.Unix ||
                            Environment.OSVersion.Platform == PlatformID.MacOSX)
                               ? ValueProvider.GetValue(Constants.Home)
                               : ValueProvider.GetValue(Constants.UserProfile);
            path = homePath + path.Substring(1);
        }

        return path;
    }

    protected abstract void OnInitialize(IGitInfo gitInfo);
}

#pragma warning restore SA1649
