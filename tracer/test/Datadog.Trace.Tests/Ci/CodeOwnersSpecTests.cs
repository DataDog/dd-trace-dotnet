// <copyright file="CodeOwnersSpecTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

/// <summary>
/// Specification tests for the CODEOWNERS parser based on the official GitHub and GitLab documentation:
/// https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners
/// https://docs.gitlab.com/user/project/codeowners/reference/
/// </summary>
public class CodeOwnersSpecTests
{
    private const string GitlabSectionsExample = """
        * @admin

        [README Owners]
        README.md @user1 @user2
        internal/README.md @user4

        [README other owners]
        README.md @user3
        """;

    [SkippableFact]
    public void GithubInlineCommentsAndEmailOwners()
    {
        var codeOwners = Create("*       @global-owner1 @global-owner2\n*.js    @js-owner #This is an inline comment.\n*.go docs@example.com\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, "/app.js").Should().Equal(["@js-owner"]);
        Match(codeOwners, "/app.go").Should().Equal(["docs@example.com"]);
        Match(codeOwners, "/file.rb").Should().Equal(["@global-owner1", "@global-owner2"]);
    }

    [SkippableFact]
    public void GithubRootedDirectoryPatternMatchesSubdirectoriesOnlyAtRoot()
    {
        // "/build/logs/" owns the root build/logs directory and all its subdirectories
        var codeOwners = Create("/build/logs/ @doctocat\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, "/build/logs/build-app/error.txt").Should().Equal(["@doctocat"]);
        Match(codeOwners, "/build/logs/error.txt").Should().Equal(["@doctocat"]);
        Match(codeOwners, "/x/build/logs/error.txt").Should().BeEmpty();
    }

    [SkippableFact]
    public void GithubWildcardSegmentDoesNotOwnNestedFiles()
    {
        // `docs/*` matches files like `docs/getting-started.md` but not deeper nested files like
        // `docs/build-app/troubleshooting.md`
        var codeOwners = Create("* @global\ndocs/* docs@example.com\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, "/docs/getting-started.md").Should().Equal(["docs@example.com"]);
        Match(codeOwners, "/docs/build-app/troubleshooting.md").Should().Equal(["@global"]);
    }

    [SkippableFact]
    public void GithubDirectoryPatternOwnsEverythingUnderneath()
    {
        var codeOwners = Create("* @global\n/docs/ @doctocat\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, "/docs/getting-started.md").Should().Equal(["@doctocat"]);
        Match(codeOwners, "/docs/build-app/troubleshooting.md").Should().Equal(["@doctocat"]);
    }

    [SkippableFact]
    public void GithubRootedVersusUnrootedPatterns()
    {
        // Unrooted patterns match anywhere in the repository, rooted ones only at the repository root
        Match(Create("/apps/ @root-apps\n", CodeOwners.Platform.GitHub), "/apps/a.go").Should().Equal(["@root-apps"]);
        Match(Create("/apps/ @root-apps\n", CodeOwners.Platform.GitHub), "/x/apps/a.go").Should().BeEmpty();
        Match(Create("apps/ @anywhere\n", CodeOwners.Platform.GitHub), "/x/apps/a.go").Should().Equal(["@anywhere"]);
    }

    [SkippableFact]
    public void GithubGlobstarDirectoryPatternOwnsDirectoryContents()
    {
        // `**/logs` owns any file in a logs directory such as `/build/logs`, `/scripts/logs`,
        // and `/deeply/nested/logs`
        var codeOwners = Create("**/logs @octocat\n*.tmp @temp-team\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, "/build/logs/error.txt").Should().Equal(["@octocat"]);
        Match(codeOwners, "/deeply/nested/logs/x.txt").Should().Equal(["@octocat"]);
        Match(codeOwners, "/logs").Should().Equal(["@octocat"]);
        Match(codeOwners, "/catalog/data.tmp").Should().Equal(["@temp-team"]); // no partial segment matches
    }

    [SkippableFact]
    public void OwnerlessEntryLeavesSubtreeUnowned()
    {
        // Example from the GitHub documentation: `/apps/github` has no owners, so any change inside
        // it can be made with the approval of any user with write access (i.e. it is unowned).
        var codeOwners = Create("* @global-owner1 @global-owner2\n*.go docs@example.com\n/apps/ @octocat\n/apps/github\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, "/apps/github").Should().BeEmpty();
        Match(codeOwners, "/apps/github/proj/main.go").Should().BeEmpty();
        Match(codeOwners, "/other.go").Should().Equal(["docs@example.com"]);
    }

    [SkippableFact]
    public void PathsAreCaseSensitive()
    {
        var codeOwners = Create("Readme.MD @docs\n*.Txt @txt\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, "/readme.md").Should().BeEmpty();
        Match(codeOwners, "/Readme.MD").Should().Equal(["@docs"]);
        Match(codeOwners, "/a.txt").Should().BeEmpty();
        Match(codeOwners, "/a.Txt").Should().Equal(["@txt"]);
    }

    [SkippableFact]
    public void LastMatchingRuleWinsGloballyForGitHub()
    {
        var codeOwners = Create("*.js @first\n*.js @second\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, "/x/y.js").Should().Equal(["@second"]);
    }

    [SkippableFact]
    public void GlobstarMatchesZeroDirectories()
    {
        var codeOwners = Create("/db/**/index.md @index-docs\n/docs/**/*.md @markdown-docs\n", CodeOwners.Platform.GitLab);
        Match(codeOwners, "/db/index.md").Should().Equal(["@index-docs"]);
        Match(codeOwners, "/db/v2/index.md").Should().Equal(["@index-docs"]);
        Match(codeOwners, "/docs/index.md").Should().Equal(["@markdown-docs"]);
        Match(codeOwners, "/docs/api/graphql/index.md").Should().Equal(["@markdown-docs"]);
        Match(codeOwners, "/docs/api/index.xml").Should().BeEmpty();
    }

    [SkippableFact]
    public void RelativePathsMatchAtAnyDepth()
    {
        // GitLab: paths without a leading slash are treated as globstar paths and match at any depth
        var codeOwners = Create("internal/README.md @user4\n", CodeOwners.Platform.GitLab);
        Match(codeOwners, "/internal/README.md").Should().Equal(["@user4"]);
        Match(codeOwners, "/docs/api/internal/README.md").Should().Equal(["@user4"]);
        Match(codeOwners, "/docs/README.md").Should().BeEmpty();
    }

    [SkippableFact]
    public void GitLabSectionsAreEvaluatedIndependentlyAndCombined()
    {
        var codeOwners = Create(GitlabSectionsExample, CodeOwners.Platform.GitLab);
        // The last matching entry in each section is used, and all sections are combined
        Match(codeOwners, "/README.md").Should().Equal(["@admin", "@user1", "@user2", "@user3"]);
        Match(codeOwners, "/internal/README.md").Should().Equal(["@admin", "@user3", "@user4"]);
    }

    [SkippableFact]
    public void SectionDefaultOwnersApplyOnlyToEntriesWithoutExplicitOwners()
    {
        // Example from the GitLab documentation
        var content = """
            [Database] @database-team @agarcia
            model/db/
            config/db/database-setup.md @docs-team
            """;
        var codeOwners = Create(content, CodeOwners.Platform.GitLab);

        Match(codeOwners, "/model/db/schema.rb").Should().Equal(["@agarcia", "@database-team"]);
        Match(codeOwners, "/config/db/database-setup.md").Should().Equal(["@docs-team"]);
        // Paths not covered by any entry of the section are not owned by the section default
        Match(codeOwners, "/other/file.txt").Should().BeEmpty();
    }

    [SkippableFact]
    public void OptionalSectionsAndApprovalCountsDoNotAffectMatching()
    {
        var codeOwners = Create("^[Go]\n*.go @go-owner\n[Big][5]\nbig/ @big-owner\n[Team] @default-team\nteam/\n", CodeOwners.Platform.GitLab);
        Match(codeOwners, "/x.go").Should().Equal(["@go-owner"]);
        Match(codeOwners, "/big/file.txt").Should().Equal(["@big-owner"]);
        Match(codeOwners, "/team/file.txt").Should().Equal(["@default-team"]); // inherits section defaults
    }

    [SkippableFact]
    public void RoleOwnersAreKeptAsOwners()
    {
        var codeOwners = Create("/config/setup.yml @@maintainer\n", CodeOwners.Platform.GitLab);
        Match(codeOwners, "/config/setup.yml").Should().Equal(["@@maintainer"]);
    }

    [SkippableFact]
    public void ExclusionsAreStickyWithinSection()
    {
        // Example from the GitLab documentation: once a path is excluded, later rules in the same
        // section cannot re-include it.
        var codeOwners = Create("* @default-owner\n!*.rb\n/special/*.rb @ruby-owner\n", CodeOwners.Platform.GitLab);
        Match(codeOwners, "/special/foo.rb").Should().BeEmpty();
        Match(codeOwners, "/code.rb").Should().BeEmpty();
        Match(codeOwners, "/other.txt").Should().Equal(["@default-owner"]);
    }

    [SkippableFact]
    public void ExclusionsApplyPerSection()
    {
        // Example from the GitLab documentation: use multiple sections to exclude with one owner set
        // and still require approval from another.
        var content = """
            [Ruby]
            *.rb @ruby-team
            !/config/**/*.rb

            [Config]
            /config/ @ops-team
            """;
        var codeOwners = Create(content, CodeOwners.Platform.GitLab);

        Match(codeOwners, "/config/routes.rb").Should().Equal(["@ops-team"]);
        Match(codeOwners, "/lib/foo.rb").Should().Equal(["@ruby-team"]);
        Match(codeOwners, "/config/other.xml").Should().Equal(["@ops-team"]);
    }

    [SkippableFact]
    public void InlineCommentsAreUnsupportedInGitLab()
    {
        var codeOwners = Create("*.rb @ruby-owner # note to self\n", CodeOwners.Platform.GitLab);
        Match(codeOwners, "/a.rb").Should().Equal(["@ruby-owner"]);
    }

    [SkippableFact]
    public void CommentLinesWithLeadingWhitespaceAreIgnored()
    {
        foreach (var platform in new[] { CodeOwners.Platform.GitHub, CodeOwners.Platform.GitLab })
        {
            var codeOwners = Create("   # indented comment\n   *.md @md-owner\n", platform);
            Match(codeOwners, "/a.md").Should().Equal(["@md-owner"]);
            // The indented comment must not be parsed as a "#" pattern entry
            Match(codeOwners, "/#").Should().BeEmpty();
        }
    }

    [SkippableFact]
    public void DirectoryPatternsMatchWithWindowsSeparators()
    {
        var codeOwners = Create("**/logs @octocat\n/build/logs/ @doctocat\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, @"\build\logs\error.txt").Should().Equal(["@doctocat"]);
        Match(codeOwners, @"\scripts\logs\x.txt").Should().Equal(["@octocat"]);
    }

    [SkippableFact]
    public void DuplicateEntriesUseLastWithinSection()
    {
        // "If an entry is duplicated in a section, the last entry is used"
        var codeOwners = Create("README.md @old\nREADME.md @new\n", CodeOwners.Platform.GitLab);
        Match(codeOwners, "/README.md").Should().Equal(["@new"]);
    }

    [SkippableFact]
    public void RealWorldHashicorpTerraformGitHubFile()
    {
        // Based on https://github.com/hashicorp/terraform/blob/main/CODEOWNERS
        const string content = """
            # The rules are evaluated in order, if a file matches multiple patterns, the last match "wins".
            * @hashicorp/terraform-core

            # Remote-state backend                           # Maintainer
            /internal/backend/remote-state/azure             @hashicorp/terraform-core @hashicorp/terraform-azure
            #/internal/backend/remote-state/consul           Unmaintained
            /internal/backend/remote-state/s3                @hashicorp/terraform-core @hashicorp/terraform-aws

            # Cloud backend
            /internal/backend/remote    @hashicorp/terraform-core @hashicorp/tf-core-cloud
            /internal/cloud             @hashicorp/terraform-core @hashicorp/tf-core-cloud

            # Provisioners
            builtin/provisioners/file               @hashicorp/terraform-core
            builtin/provisioners/local-exec         @hashicorp/terraform-core

            # Actions
            /internal/command/jsonplan/action_invocations.go @hashicorp/team-tf-actions @hashicorp/terraform-core
            """;
        var codeOwners = Create(content, CodeOwners.Platform.GitHub);

        Match(codeOwners, "/main.go").Should().Equal(["@hashicorp/terraform-core"]);
        // Several teams on one line
        Match(codeOwners, "/internal/backend/remote-state/s3/backend.go").Should().Equal(["@hashicorp/terraform-aws", "@hashicorp/terraform-core"]);
        // Commented-out entry is skipped: consul falls back to the `*` rule
        Match(codeOwners, "/internal/backend/remote-state/consul/backend.go").Should().Equal(["@hashicorp/terraform-core"]);
        // Unrooted patterns match anywhere in the repository
        Match(codeOwners, "/x/builtin/provisioners/file/resource.go").Should().Equal(["@hashicorp/terraform-core"]);
        Match(codeOwners, "/internal/command/jsonplan/action_invocations.go").Should().Equal(["@hashicorp/team-tf-actions", "@hashicorp/terraform-core"]);
        Match(codeOwners, "/internal/cloud/backend_run.go").Should().Equal(["@hashicorp/terraform-core", "@hashicorp/tf-core-cloud"]);
    }

    [SkippableFact]
    public void RealWorldGitLabSectionedFile()
    {
        // Based on https://gitlab.com/gitlab-org/gitlab/-/blob/master/.gitlab/CODEOWNERS
        const string content = """
            [Maintainers] @gl-dx/maintainers @gitlab-org/maintainers/rails-backend
            *

            /* @gitlab-org/maintainers/frontend @gitlab-org/maintainers/database
            *.rb @gitlab-org/maintainers/rails-backend
            /app/ @gitlab-org/maintainers/rails-backend
            /workhorse/ @gitlab-org/maintainers/gitlab-workhorse

            ^[Database] @gitlab-org/maintainers/database
            /spec/lib/gitlab/background_migration/

            ^[Frontend dependency patches] @markrian @xanf @thutterer
            /patches/
            """;
        var codeOwners = Create(content, CodeOwners.Platform.GitLab);

        // The bare `*` entry has no owners and inherits the [Maintainers] section defaults
        Match(codeOwners, "/random/path.txt")
            .Should().Equal(["@gitlab-org/maintainers/rails-backend", "@gl-dx/maintainers"]);
        // The /* root-level rule is defined after the bare `*` entry, so it overrides the defaults
        // for top-level files only
        Match(codeOwners, "/README.md")
            .Should().Equal(["@gitlab-org/maintainers/database", "@gitlab-org/maintainers/frontend"]);
        // Last matching entry within the section wins (/app/ is defined after *.rb)
        Match(codeOwners, "/app/models/user.rb").Should().Equal(["@gitlab-org/maintainers/rails-backend"]);
        Match(codeOwners, "/workhorse/Makefile").Should().Equal(["@gitlab-org/maintainers/gitlab-workhorse"]);
        // Optional section entries without owners inherit that section's default owners,
        // combined with the results from the [Maintainers] section where *.rb overrides
        // the bare `*` defaults
        var backgroundMigration = Match(codeOwners, "/spec/lib/gitlab/background_migration/foo_spec.rb");
        backgroundMigration.Should().Contain("@gitlab-org/maintainers/database");
        backgroundMigration.Should().Contain("@gitlab-org/maintainers/rails-backend");
        backgroundMigration.Should().HaveCount(2);
        var patchFiles = Match(codeOwners, "/patches/foo.diff");
        patchFiles.Should().Contain("@markrian");
        patchFiles.Should().Contain("@thutterer");
        patchFiles.Should().HaveCount(5);
    }

    [SkippableFact]
    public void MultipleLeadingSlashesAreNormalized()
    {
        // Callers prepend "/" to relative paths; a path that already contains leading slashes (or is
        // empty) must still normalize to a single rooted form instead of failing to match.
        var codeOwners = Create("*.md @md\n/docs/ @doctocat\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, "//docs/getting-started.md").Should().Equal(["@doctocat"]);
        Match(codeOwners, "/docs/getting-started.md").Should().Equal(["@doctocat"]);
        Match(codeOwners, string.Empty).Should().BeEmpty();
    }

    [SkippableFact]
    public void NullPathReturnsNoOwners()
    {
        // Defensive: a null path must not throw and simply has no owners.
        var codeOwners = Create("* @global\n", CodeOwners.Platform.GitHub);
        Match(codeOwners, null!).Should().BeEmpty();
    }

    [SkippableFact]
    public void DescendantMatchingIsStableAcrossRepeatedCalls()
    {
        // The descendant glob variant is compiled lazily on first use and cached; repeated matches
        // must keep returning the same results (e.g. no recompilation or caching regression).
        var codeOwners = Create("**/logs @octocat\n", CodeOwners.Platform.GitHub);
        for (var i = 0; i < 3; i++)
        {
            Match(codeOwners, "/build/logs/error.txt").Should().Equal(["@octocat"]);
            Match(codeOwners, "/build/logs/nested/error.txt").Should().Equal(["@octocat"]);
            Match(codeOwners, "/catalog/data.tmp").Should().BeEmpty();
            Match(codeOwners, "/logs").Should().Equal(["@octocat"]);
        }
    }

    private static CodeOwners Create(string content, CodeOwners.Platform platform)
    {
        var path = Path.Combine(Path.GetTempPath(), "dd-codeowners-spec-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, content);

        try
        {
            return new CodeOwners(path, platform);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string[] Match(CodeOwners codeOwners, string path)
        => codeOwners.Match(path).OrderBy(o => o, StringComparer.Ordinal).ToArray();
}
