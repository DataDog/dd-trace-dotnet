// <copyright file="GitMetadataPrecedenceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci.CiEnvironment;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class GitMetadataPrecedenceTests
{
    [Theory]
    [InlineData("git")]
    [InlineData("provider")]
    [InlineData("override")]
    [InlineData("partial")]
    [InlineData("credentials")]
    [InlineData("sha-only")]
    public void SelectsGitMetadataAsOneSource(string source)
    {
        var variables = new Dictionary<string, string>();
        if (source == "override")
        {
            variables["DD_GIT_COMMIT_SHA"] = new string('c', 40);
            variables["DD_GIT_REPOSITORY_URL"] = "https://example.com/user.git";
            variables["DD_GIT_BRANCH"] = "user-branch";
            variables["DD_GIT_TAG"] = "user-tag";
            variables["DD_GIT_COMMIT_MESSAGE"] = "user-message";
            variables["DD_GIT_COMMIT_AUTHOR_NAME"] = "user-author";
            variables["DD_GIT_COMMIT_AUTHOR_EMAIL"] = "user-author@example.com";
            variables["DD_GIT_COMMIT_AUTHOR_DATE"] = "2025-01-01T01:02:03+00:00";
            variables["DD_GIT_COMMIT_COMMITTER_NAME"] = "user-committer";
            variables["DD_GIT_COMMIT_COMMITTER_EMAIL"] = "user-committer@example.com";
            variables["DD_GIT_COMMIT_COMMITTER_DATE"] = "2025-01-02T01:02:03+00:00";
        }

        if (source == "sha-only")
        {
            variables["DD_GIT_COMMIT_SHA"] = new string('c', 40);
        }

        if (source == "credentials")
        {
            variables["DD_GIT_REPOSITORY_URL"] = "invalid URL";
        }

        var local = new GitInfo();
        if (source != "provider")
        {
            local.Commit = new string('a', 40);
            local.SourceRoot = "local-source-root";
            if (source != "partial")
            {
                local.Repository = source == "credentials" ? "https://username:password@example.com/local.git" : "https://example.com/local.git";
                local.Branch = "local-branch";
                local.Tag = "local-tag";
                local.Message = "local-message";
                local.AuthorName = "local-author";
                local.AuthorEmail = "local-author@example.com";
                local.AuthorDate = DateTimeOffset.FromUnixTimeSeconds(1);
                local.CommitterName = "local-committer";
                local.CommitterEmail = "local-committer@example.com";
                local.CommitterDate = DateTimeOffset.FromUnixTimeSeconds(2);
            }
        }

        var values = new TestValues(variables);
        values.Load(local);

        var prefix = source switch { "provider" => "provider", "override" => "user", _ => "local" };
        var expectedSha = source switch { "provider" => new string('b', 40), "override" or "sha-only" => new string('c', 40), _ => new string('a', 40) };
        values.Commit.Should().Be(expectedSha);
        values.Repository.Should().Be(source == "partial" ? null : $"https://example.com/{prefix}.git");
        values.Branch.Should().Be(source == "partial" ? null : $"{prefix}-branch");
        values.Tag.Should().Be(source == "partial" ? null : $"{prefix}-tag");
        values.Message.Should().Be(source is "partial" or "sha-only" ? null : $"{prefix}-message");
        values.AuthorName.Should().Be(source is "partial" or "sha-only" ? null : $"{prefix}-author");
        values.AuthorEmail.Should().Be(source is "partial" or "sha-only" ? null : $"{prefix}-author@example.com");
        values.CommitterName.Should().Be(source is "partial" or "sha-only" ? null : $"{prefix}-committer");
        values.CommitterEmail.Should().Be(source is "partial" or "sha-only" ? null : $"{prefix}-committer@example.com");
        values.AuthorDate.Should().Be(source == "sha-only" ? null : source == "provider" ? DateTimeOffset.FromUnixTimeSeconds(3) : source == "override" ? new DateTimeOffset(2025, 1, 1, 1, 2, 3, TimeSpan.Zero) : local.AuthorDate);
        values.CommitterDate.Should().Be(source == "sha-only" ? null : source == "provider" ? DateTimeOffset.FromUnixTimeSeconds(4) : source == "override" ? new DateTimeOffset(2025, 1, 2, 1, 2, 3, TimeSpan.Zero) : local.CommitterDate);
        values.PipelineId.Should().Be("pipeline");
        values.PrBaseBranch.Should().Be("main");
        values.SourceRoot.Should().Be(source == "provider" ? "provider-source-root" : "local-source-root");
        values.WorkspacePath.Should().Be("provider-workspace");
    }

    private sealed class TestValues(Dictionary<string, string> variables) : CIEnvironmentValues<DictionaryValuesProvider>(new DictionaryValuesProvider(variables))
    {
        public void Load(IGitInfo gitInfo) => Setup(gitInfo);

        protected override void OnInitialize(IGitInfo gitInfo)
        {
            Commit = new string('b', 40);
            Repository = "https://example.com/provider.git";
            Branch = "provider-branch";
            Tag = "provider-tag";
            Message = "provider-message";
            AuthorName = "provider-author";
            AuthorEmail = "provider-author@example.com";
            AuthorDate = DateTimeOffset.FromUnixTimeSeconds(3);
            CommitterName = "provider-committer";
            CommitterEmail = "provider-committer@example.com";
            CommitterDate = DateTimeOffset.FromUnixTimeSeconds(4);
            PipelineId = "pipeline";
            SourceRoot = "provider-source-root";
            WorkspacePath = "provider-workspace";
            PrBaseBranch = "main";
        }
    }
}
