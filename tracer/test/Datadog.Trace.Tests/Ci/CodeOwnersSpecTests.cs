// <copyright file="CodeOwnersSpecTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CodeOwnership;
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

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    public enum TestDialect
    {
        GitHub,
        GitLab
    }

    [SkippableTheory]
    [InlineData("/app.js", new[] { "@js-owner" })]
    [InlineData("/app.go", new[] { "docs@example.com" })]
    [InlineData("/file.rb", new[] { "@global-owner1", "@global-owner2" })]
    public void GithubInlineCommentsAndEmailOwners(string path, string[] expectedOwners)
    {
        var file = """
            *       @global-owner1 @global-owner2
            *.js    @js-owner #This is an inline comment.
            *.go    docs@example.com

            """;
        Match(Create(file, CodeOwners.Dialect.GitHub), path).Should().Equal(expectedOwners);
    }

    [SkippableFact]
    public void GithubRootedDirectoryPatternMatchesSubdirectoriesOnlyAtRoot()
    {
        // "/build/logs/" owns the root build/logs directory and all its subdirectories
        var codeOwners = Create(
            """
            /build/logs/ @doctocat

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, "/build/logs/build-app/error.txt").Should().Equal(["@doctocat"]);
        Match(codeOwners, "/build/logs/error.txt").Should().Equal(["@doctocat"]);
        Match(codeOwners, "/x/build/logs/error.txt").Should().BeEmpty();
    }

    [SkippableFact]
    public void GithubWildcardSegmentDoesNotOwnNestedFiles()
    {
        // `docs/*` matches files like `docs/getting-started.md` but not deeper nested files like
        // `docs/build-app/troubleshooting.md`
        var codeOwners = Create(
            """
            *      @global
            docs/* docs@example.com

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, "/docs/getting-started.md").Should().Equal(["docs@example.com"]);
        Match(codeOwners, "/docs/build-app/troubleshooting.md").Should().Equal(["@global"]);
    }

    [SkippableFact]
    public void GithubDirectoryPatternOwnsEverythingUnderneath()
    {
        var codeOwners = Create(
            """
            *      @global
            /docs/ @doctocat

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, "/docs/getting-started.md").Should().Equal(["@doctocat"]);
        Match(codeOwners, "/docs/build-app/troubleshooting.md").Should().Equal(["@doctocat"]);
    }

    [SkippableFact]
    public void GithubDirectoryAndTerminalGlobstarPatternsRequireDescendants()
    {
        var codeOwners = Create(
            """
            /docs/      @directory
            /archive/** @globstar

            """,
            CodeOwners.Dialect.GitHub);

        Match(codeOwners, "/docs").Should().BeEmpty("a trailing slash only denotes a directory and its contents");
        Match(codeOwners, "/docs/file.txt").Should().Equal(["@directory"]);
        Match(codeOwners, "/archive").Should().BeEmpty("a terminal /** means content inside the directory");
        Match(codeOwners, "/archive/file.txt").Should().Equal(["@globstar"]);
        Match(codeOwners, "/archive/deep/file.txt").Should().Equal(["@globstar"]);
    }

    [SkippableFact]
    public void GitlabTerminalGlobstarMatchesOnePathSegment()
    {
        var terminalGlobstar = Create(
            """
            /archive/** @single-segment

            """,
            CodeOwners.Dialect.GitLab);
        var recursiveGlobstar = Create(
            """
            /archive/**/* @recursive

            """,
            CodeOwners.Dialect.GitLab);

        Match(terminalGlobstar, "/archive").Should().BeEmpty();
        Match(terminalGlobstar, "/archive/file.txt").Should().Equal(["@single-segment"]);
        Match(terminalGlobstar, "/archive/deep/file.txt").Should().BeEmpty();
        Match(recursiveGlobstar, "/archive/deep/file.txt").Should().Equal(["@recursive"]);
    }

    [SkippableFact]
    public void GitlabGlobstarBeforeTrailingSlashMatchesImmediateAndNestedChildren()
    {
        var codeOwners = Create(
            """
            /archive/**/ @owner

            """,
            CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/archive").Should().BeEmpty();
        Match(codeOwners, "/archive/file.txt").Should().Equal(["@owner"]);
        Match(codeOwners, "/archive/deep/file.txt").Should().Equal(["@owner"]);
    }

    [SkippableTheory]
    [InlineData("/apps/ @root-apps", "/apps/a.go", new[] { "@root-apps" })]
    [InlineData("/apps/ @root-apps", "/x/apps/a.go", new string[] { })]
    [InlineData("apps/ @anywhere", "/x/apps/a.go", new[] { "@anywhere" })]
    public void GithubRootedVersusUnrootedPatterns(string rule, string path, string[] expectedOwners)
    {
        // Unrooted patterns match anywhere in the repository, rooted ones only at the repository root
        Match(Create(rule, CodeOwners.Dialect.GitHub), path).Should().Equal(expectedOwners);
    }

    [SkippableTheory]
    [InlineData(TestDialect.GitHub, "/docs/a.md", new[] { "@docs" })]
    [InlineData(TestDialect.GitHub, "/examples/docs/a.md", new[] { "@global" })]
    [InlineData(TestDialect.GitHub, "/a/b", new[] { "@globstar" })]
    [InlineData(TestDialect.GitHub, "/a/x/b", new[] { "@globstar" })]
    [InlineData(TestDialect.GitHub, "/x/a/b", new[] { "@global" })]
    [InlineData(TestDialect.GitHub, "/x/apps/a.md", new[] { "@apps" })]
    [InlineData(TestDialect.GitHub, "/x/logs/a.md", new[] { "@logs" })]
    [InlineData(TestDialect.GitLab, "/docs/a.md", new[] { "@docs" })]
    [InlineData(TestDialect.GitLab, "/examples/docs/a.md", new[] { "@docs" })]
    [InlineData(TestDialect.GitLab, "/x/a/b", new[] { "@globstar" })]
    public void PatternsWithMiddleSlashFollowDialectSemantics(TestDialect dialect, string path, string[] expectedOwners)
    {
        const string file = """
            *        @global
            docs/*   @docs
            a/**/b   @globstar
            apps/    @apps
            **/logs  @logs

            """;
        Match(Create(file, ToCodeOwnersDialect(dialect)), path).Should().Equal(expectedOwners);
    }

    [SkippableTheory]
    [InlineData("/build/logs/error.txt", new[] { "@octocat" })]
    [InlineData("/deeply/nested/logs/x.txt", new[] { "@octocat" })]
    [InlineData("/logs", new[] { "@octocat" })]
    [InlineData("/catalog/data.tmp", new[] { "@temp-team" })]
    public void GithubGlobstarDirectoryPatternOwnsDirectoryContents(string path, string[] expectedOwners)
    {
        // `**/logs` owns any file in a logs directory such as `/build/logs`, `/scripts/logs`,
        // and `/deeply/nested/logs`
        var file = """
            **/logs @octocat
            *.tmp   @temp-team

            """;
        Match(Create(file, CodeOwners.Dialect.GitHub), path).Should().Equal(expectedOwners);
    }

    [SkippableFact]
    public void OwnerlessEntryLeavesSubtreeUnowned()
    {
        // Example from the GitHub documentation: `/apps/github` has no owners, so any change inside
        // it can be made with the approval of any user with write access (i.e. it is unowned).
        var codeOwners = Create(
            """
            *            @global-owner1 @global-owner2
            *.go         docs@example.com
            /apps/       @octocat
            /apps/github

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, "/apps/github").Should().BeEmpty();
        Match(codeOwners, "/apps/github/proj/main.go").Should().BeEmpty();
        Match(codeOwners, "/other.go").Should().Equal(["docs@example.com"]);
    }

    [SkippableFact]
    public void PathsAreCaseSensitive()
    {
        var codeOwners = Create(
            """
            Readme.MD @docs
            *.Txt     @txt

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, "/readme.md").Should().BeEmpty();
        Match(codeOwners, "/Readme.MD").Should().Equal(["@docs"]);
        Match(codeOwners, "/a.txt").Should().BeEmpty();
        Match(codeOwners, "/a.Txt").Should().Equal(["@txt"]);
    }

    [SkippableFact]
    public void LastMatchingRuleWinsGloballyForGitHub()
    {
        var codeOwners = Create(
            """
            *.js @first
            *.js @second

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, "/x/y.js").Should().Equal(["@second"]);
    }

    [SkippableFact]
    public void GitLabSectionSyntaxDoesNotScopeGitHubRules()
    {
        var codeOwners = Create(
            """
            *.js          @first
            [Docs]        @ignored
            *.md          @docs
            *.js          @second

            """,
            CodeOwners.Dialect.GitHub);

        Match(codeOwners, "/x/y.js").Should().Equal(["@second"]);
        Match(codeOwners, "/README.md").Should().Equal(["@docs"]);
        codeOwners.ParsingDiagnosticsCount.Should().Be(1);
    }

    [SkippableFact]
    public void GlobstarMatchesZeroDirectories()
    {
        var codeOwners = Create(
            """
            /db/**/index.md @index-docs
            /docs/**/*.md   @markdown-docs

            """,
            CodeOwners.Dialect.GitLab);
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
        var codeOwners = Create(
            """
            internal/README.md @user4

            """,
            CodeOwners.Dialect.GitLab);
        Match(codeOwners, "/internal/README.md").Should().Equal(["@user4"]);
        Match(codeOwners, "/docs/api/internal/README.md").Should().Equal(["@user4"]);
        Match(codeOwners, "/docs/README.md").Should().BeEmpty();
    }

    [SkippableFact]
    public void GitLabSectionsAreEvaluatedIndependentlyAndCombined()
    {
        var codeOwners = Create(GitlabSectionsExample, CodeOwners.Dialect.GitLab);
        // The last matching entry in each section is used, and all sections are combined
        Match(codeOwners, "/README.md").Should().Equal(["@admin", "@user1", "@user2", "@user3"]);
        Match(codeOwners, "/internal/README.md").Should().Equal(["@admin", "@user3", "@user4"]);
    }

    [SkippableFact]
    public void GitLabSectionDefaultsApplyOnlyAfterARuleMatches()
    {
        var codeOwners = Create(
            """
            [Section] @team
            /src/ @owner

            """,
            CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/src/code.cs").Should().Equal(["@owner"]);
        Match(codeOwners, "/other/file.cs").Should().BeEmpty();
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
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/model/db/schema.rb").Should().Equal(["@agarcia", "@database-team"]);
        Match(codeOwners, "/config/db/database-setup.md").Should().Equal(["@docs-team"]);
        // Paths not covered by any entry of the section are not owned by the section default
        Match(codeOwners, "/other/file.txt").Should().BeEmpty();
    }

    [SkippableFact]
    public void OptionalSectionsAndApprovalCountsDoNotAffectMatching()
    {
        var codeOwners = Create(
            """
            ^[Go]
            *.go @go-owner
            [Big][5]
            big/ @big-owner
            [Team] @default-team
            team/

            """,
            CodeOwners.Dialect.GitLab);
        Match(codeOwners, "/x.go").Should().Equal(["@go-owner"]);
        Match(codeOwners, "/big/file.txt").Should().Equal(["@big-owner"]);
        Match(codeOwners, "/team/file.txt").Should().Equal(["@default-team"]); // inherits section defaults
    }

    [SkippableFact]
    public void RoleOwnersAreKeptAsOwners()
    {
        var codeOwners = Create(
            """
            /config/setup.yml @@maintainer

            """,
            CodeOwners.Dialect.GitLab);
        Match(codeOwners, "/config/setup.yml").Should().Equal(["@@maintainer"]);
    }

    [SkippableFact]
    public void ExclusionsAreStickyWithinSection()
    {
        // Example from the GitLab documentation: once a path is excluded, later rules in the same
        // section cannot re-include it.
        var codeOwners = Create(
            """
            *             @default-owner
            !*.rb
            /special/*.rb @ruby-owner

            """,
            CodeOwners.Dialect.GitLab);
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
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/config/routes.rb").Should().Equal(["@ops-team"]);
        Match(codeOwners, "/lib/foo.rb").Should().Equal(["@ruby-team"]);
        Match(codeOwners, "/config/other.xml").Should().Equal(["@ops-team"]);
    }

    [SkippableFact]
    public void InlineCommentsAreUnsupportedInGitLab()
    {
        var codeOwners = Create(
            """
            *.rb @ruby-owner # note to self

            """,
            CodeOwners.Dialect.GitLab);
        Match(codeOwners, "/a.rb").Should().Equal(["@ruby-owner"]);
    }

    [SkippableFact]
    public void CommentLinesWithLeadingWhitespaceAreIgnored()
    {
        foreach (var dialect in new[] { CodeOwners.Dialect.GitHub, CodeOwners.Dialect.GitLab })
        {
            var codeOwners = Create(
                """
                   # indented comment
                   *.md @md-owner

                """,
                dialect);
            Match(codeOwners, "/a.md").Should().Equal(["@md-owner"]);
            // The indented comment must not be parsed as a "#" pattern entry
            Match(codeOwners, "/#").Should().BeEmpty();
        }
    }

    [SkippableFact]
    public void DirectoryPatternsMatchWithWindowsSeparators()
    {
        var codeOwners = Create(
            """
            **/logs     @octocat
            /build/logs/ @doctocat

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, @"\build\logs\error.txt").Should().Equal(["@doctocat"]);
        Match(codeOwners, @"\scripts\logs\x.txt").Should().Equal(["@octocat"]);
    }

    [SkippableFact]
    public void DuplicateEntriesUseLastWithinSection()
    {
        // "If an entry is duplicated in a section, the last entry is used"
        var codeOwners = Create(
            """
            README.md @old
            README.md @new

            """,
            CodeOwners.Dialect.GitLab);
        Match(codeOwners, "/README.md").Should().Equal(["@new"]);
    }

    [SkippableFact]
    public void GitLabLastDuplicatePatternReplacesEarlierEntriesIncludingExclusions()
    {
        var exactDuplicates = Create(
            """
            [Ruby]
            *.rb @old
            !*.rb
            *.rb @new

            """,
            CodeOwners.Dialect.GitLab);
        Match(exactDuplicates, "/model.rb").Should().Equal(["@new"]);

        // GitLab normalizes these spellings to the same pattern key before replacing duplicates.
        var normalizedDuplicates = Create(
            """
            * @old
            !/**/*
            * @new

            """,
            CodeOwners.Dialect.GitLab);
        Match(normalizedDuplicates, "/nested/file.cs").Should().Equal(["@new"]);
    }

    [SkippableFact]
    public void GitLabDuplicateNormalizationUnescapesLeadingHashBeforeReplacement()
    {
        var laterOwner = Create(
            """
            !#file
            \#file @new

            """,
            CodeOwners.Dialect.GitLab);
        Match(laterOwner, "/#file").Should().Equal(["@new"]);

        var laterExclusion = Create(
            """
            \#file @old
            !#file

            """,
            CodeOwners.Dialect.GitLab);
        Match(laterExclusion, "/#file").Should().BeEmpty();
    }

    [SkippableFact]
    public void GitLabCharacterClassesSupportSetsRangesAndNegationWithoutCrossingDirectories()
    {
        var content = """
            digit[0-9].txt     @digit
            letter[ab].txt     @letter
            non-digit[!0-9].txt @non-digit
            path[!x]file       @same-segment

            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/digit7.txt").Should().Equal(["@digit"]);
        Match(codeOwners, "/digitx.txt").Should().BeEmpty();
        Match(codeOwners, "/lettera.txt").Should().Equal(["@letter"]);
        Match(codeOwners, "/letterc.txt").Should().BeEmpty();
        Match(codeOwners, "/non-digita.txt").Should().Equal(["@non-digit"]);
        Match(codeOwners, "/non-digit7.txt").Should().BeEmpty();
        Match(codeOwners, "/path/file").Should().BeEmpty();
    }

    [SkippableFact]
    public void GitLabMalformedCharacterClassesCannotAbortParserInitialization()
    {
        var content = """
            *               @fallback
            file[z-a].txt   @invalid-range
            broken\
            file[---!].txt  @punctuation

            """;

        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        codeOwners.ParsingDiagnosticsCount.Should().Be(2, "invalid rules should produce one aggregated parsing diagnostic count");
        Match(codeOwners, "/filex.txt").Should().Equal(["@fallback"]);
        Match(codeOwners, "/file-.txt").Should().Equal(["@punctuation"]);
        Match(codeOwners, "/file!.txt").Should().Equal(["@punctuation"]);
    }

    [SkippableFact]
    public void GitLabCharacterClassesCannotStartWithClosingBracket()
    {
        var codeOwners = Create(
            """
            *              @fallback
            file[]a].txt   @positive
            file[!]].txt   @negated

            """,
            CodeOwners.Dialect.GitLab);

        codeOwners.ParsingDiagnosticsCount.Should().Be(2);
        Match(codeOwners, "/filea.txt").Should().Equal(["@fallback"]);
        Match(codeOwners, "/file].txt").Should().Equal(["@fallback"]);
        Match(codeOwners, "/filex.txt").Should().Equal(["@fallback"]);
    }

    [SkippableFact]
    public void GitLabBackslashEscapesGlobMetacharactersAndOrdinaryCharacters()
    {
        var content = """
            file\?.txt @question
            literal\*.txt @star
            letter\a.txt @ordinary
            bracket[\]].txt @closing-bracket
            hyphen[\-].txt @hyphen
            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/file?.txt").Should().Equal(["@question"]);
        Match(codeOwners, "/fileX.txt").Should().BeEmpty();
        Match(codeOwners, "/literal*.txt").Should().Equal(["@star"]);
        Match(codeOwners, "/literal-value.txt").Should().BeEmpty();
        Match(codeOwners, "/lettera.txt").Should().Equal(["@ordinary"]);
        Match(codeOwners, "/bracket].txt").Should().Equal(["@closing-bracket"]);
        Match(codeOwners, "/hyphen-.txt").Should().Equal(["@hyphen"]);
    }

    [SkippableFact]
    public void GithubBackslashEscapesMetacharactersSpacesAndInlineCommentMarkers()
    {
        var content = """
            literal\*.txt @star
            file\?.txt @question
            bracket\[name\].txt @bracket
            path\ with\ spaces/ @spaces
            middle\#hash.txt @hash
            trailing\
            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitHub);

        Match(codeOwners, "/literal*.txt").Should().Equal(["@star"]);
        Match(codeOwners, "/literal-value.txt").Should().BeEmpty();
        Match(codeOwners, "/file?.txt").Should().Equal(["@question"]);
        Match(codeOwners, "/fileX.txt").Should().BeEmpty();
        Match(codeOwners, "/bracket[name].txt").Should().Equal(["@bracket"]);
        Match(codeOwners, "/path with spaces/file.txt").Should().Equal(["@spaces"]);
        Match(codeOwners, "/middle#hash.txt").Should().Equal(["@hash"]);
        codeOwners.ParsingDiagnosticsCount.Should().Be(1, "a trailing backslash is an invalid glob");
    }

    [SkippableFact]
    public void WhitespaceOnlyGitLabSectionHeaderStartsInvalidNamedSection()
    {
        var content = """
            [Docs] @docs
            [   ]  @blank
            README.md

            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/README.md").Should().Equal(["@blank"]);
        Match(codeOwners, "/guide.md").Should().BeEmpty();
        codeOwners.ParsingDiagnosticsCount.Should().Be(1, "GitLab accepts the header but diagnoses its missing name");
    }

    [SkippableFact]
    public void UnparsableGitLabSectionHeaderIsSkippedInsteadOfBecomingPattern()
    {
        var content = """
            * @global
            [Broken

            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/[Broken").Should().Equal(["@global"]);
        codeOwners.ParsingDiagnosticsCount.Should().Be(1);
    }

    [SkippableFact]
    public void GitLabMalformedSectionSuffixCannotLeakDefaultOwners()
    {
        var extraBracket = Create(
            """
            [Docs]] @leaked
            README.md

            """,
            CodeOwners.Dialect.GitLab);
        Match(extraBracket, "/README.md").Should().BeEmpty();
        extraBracket.ParsingDiagnosticsCount.Should().Be(2, "the malformed header and ownerless entry are diagnosed independently");

        var invalidApproval = Create(
            """
            [Docs][x] @leaked
            README.md

            """,
            CodeOwners.Dialect.GitLab);
        Match(invalidApproval, "/README.md").Should().BeEmpty();
        invalidApproval.ParsingDiagnosticsCount.Should().Be(2);
    }

    [SkippableFact]
    public void GitLabPermissiveSectionParsingKeepsOnlyTheRecognizedOwnerSpan()
    {
        var codeOwners = Create(
            """
            [Docs][2]@owner
            README.md

            """,
            CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/README.md").Should().Equal(["@owner"]);
        codeOwners.ParsingDiagnosticsCount.Should().Be(1, "the missing whitespace is diagnosed without discarding recognized defaults");
    }

    [SkippableTheory]
    [InlineData("@@developer")]
    [InlineData("@@developers")]
    [InlineData("@@maintainer")]
    [InlineData("@@maintainers")]
    [InlineData("@@owner")]
    [InlineData("@@OwNeRs")]
    public void GitLabRecognizedRolesAreKeptAsOwners(string role)
    {
        var codeOwners = Create(
            $"""
            *.cs {role}

            """,
            CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/file.cs").Should().Equal([role]);
    }

    [SkippableFact]
    public void GitLabUnknownRolesAreIgnored()
    {
        var codeOwners = Create(
            """
            *.cs @@banana @valid

            """,
            CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/file.cs").Should().Equal(["@valid"]);
        codeOwners.ParsingDiagnosticsCount.Should().Be(1, "the invalid role was discarded from an otherwise valid rule");
    }

    [SkippableFact]
    public void OwnerValidationFollowsDialectRulesAndDoesNotApplyDefaultsToMalformedExplicitOwners()
    {
        var github = Create(
            """
            *     @global
            *.cs  docs@
            *.fs  @@maintainer

            """,
            CodeOwners.Dialect.GitHub);
        Match(github, "/file.cs").Should().Equal(["@global"]);
        Match(github, "/file.fs").Should().Equal(["@global"]);
        github.ParsingDiagnosticsCount.Should().Be(2, "GitHub rejects each complete rule that contains an invalid owner");

        var gitlab = Create(
            """
            [Docs] @default malformed@
            *.md malformed@ @valid
            README.md malformed@
            GUIDE.md

            """,
            CodeOwners.Dialect.GitLab);
        Match(gitlab, "/other.md").Should().Equal(["@valid"]);
        Match(gitlab, "/README.md").Should().BeEmpty();
        Match(gitlab, "/GUIDE.md").Should().Equal(["@default"]);
        gitlab.ParsingDiagnosticsCount.Should().Be(3, "invalid owners in defaults and rules are all diagnosed while valid owners remain usable");
    }

    [SkippableFact]
    public void OwnerExtractionRejectsImpossibleGithubReferencesAndCanonicalizesGitLabReferences()
    {
        var github = Create(
            """
            *     @fallback
            *.cs  @!
            *.fs  user@example.
            *.vb  (@valid)
            *.ts  @bad+owner
            *.go  @org/team/nested

            """,
            CodeOwners.Dialect.GitHub);
        Match(github, "/file.cs").Should().Equal(["@fallback"]);
        Match(github, "/file.fs").Should().Equal(["@fallback"]);
        Match(github, "/file.vb").Should().Equal(["@fallback"]);
        Match(github, "/file.ts").Should().Equal(["@fallback"]);
        Match(github, "/file.go").Should().Equal(["@fallback"]);
        github.ParsingDiagnosticsCount.Should().Be(5);

        var gitlab = Create(
            """
            *.cs  @! @good
            *.md  (@docs)
            *.txt docs@example.
            *.go  @group/nested-team

            """,
            CodeOwners.Dialect.GitLab);
        Match(gitlab, "/file.cs").Should().Equal(["@good"]);
        Match(gitlab, "/file.md").Should().Equal(["@docs"]);
        Match(gitlab, "/file.txt").Should().Equal(["docs@example"]);
        Match(gitlab, "/file.go").Should().Equal(["@group/nested-team"]);
        gitlab.ParsingDiagnosticsCount.Should().Be(1, "only the token without an extractable reference is malformed");
    }

    [SkippableFact]
    public void GithubAcceptsEnterpriseManagedUserNames()
    {
        var codeOwners = Create(
            """
            *.cs @mona-cat_octo
            *.fs @octo_admin

            """,
            CodeOwners.Dialect.GitHub);

        Match(codeOwners, "/file.cs").Should().Equal(["@mona-cat_octo"]);
        Match(codeOwners, "/file.fs").Should().Equal(["@octo_admin"]);
        codeOwners.ParsingDiagnosticsCount.Should().Be(0);
    }

    [SkippableFact]
    public void GitLabNamespaceReferencesMayEndWithHyphens()
    {
        var codeOwners = Create(
            """
            *.cs @team-
            *.fs (@group-/subgroup-)

            """,
            CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/file.cs").Should().Equal(["@team-"]);
        Match(codeOwners, "/file.fs").Should().Equal(["@group-/subgroup-"]);
        codeOwners.ParsingDiagnosticsCount.Should().Be(0);
    }

    [SkippableFact]
    public void GitLabReferenceExtractionScansNamesRolesAndEmailsIndependently()
    {
        var content = """
            *.cs docs@example.com,@alice
            *.fs alice@example.com!alias
            *.vb (@@maintainer

            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/file.cs").Should().Equal(["@alice", "docs@example.com"]);
        Match(codeOwners, "/file.fs").Should().Equal(["alice@example.com!alias"]);
        Match(codeOwners, "/file.vb").Should().Equal(["@@maintainer"]);
        codeOwners.ParsingDiagnosticsCount.Should().Be(0);
    }

    [SkippableFact]
    public void GitLabExclusionsIgnoreOwnerTextForDiagnostics()
    {
        var codeOwners = Create(
            """
            !*.cs definitely-not-an-owner

            """,
            CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/file.cs").Should().BeEmpty();
        codeOwners.ParsingDiagnosticsCount.Should().Be(0);
    }

    [SkippableFact]
    public void GitLabOwnerlessEntriesAreDiagnosedWithoutChangingMatchingSemantics()
    {
        var codeOwners = Create(
            """
            *.md

            """,
            CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/README.md").Should().BeEmpty();
        codeOwners.ParsingDiagnosticsCount.Should().Be(1);
    }

    [SkippableTheory]
    [InlineData(TestDialect.GitHub)]
    [InlineData(TestDialect.GitLab)]
    public void EscapedSlashesRemainPathSeparators(TestDialect dialect)
    {
        var codeOwners = Create(
            """
            dir\/file.txt @file
            docs\/        @docs

            """,
            ToCodeOwnersDialect(dialect));

        Match(codeOwners, "/dir/file.txt").Should().Equal(["@file"]);
        Match(codeOwners, "/docs/guide.md").Should().Equal(["@docs"]);
        if (dialect == TestDialect.GitLab)
        {
            Match(codeOwners, "/nested/dir/file.txt").Should().Equal(["@file"]);
        }
        else
        {
            Match(codeOwners, "/nested/dir/file.txt").Should().BeEmpty("a slash roots a GitHub pattern");
        }

        codeOwners.ParsingDiagnosticsCount.Should().Be(0);
    }

    [SkippableFact]
    public void FormerGlobstarSentinelTextIsMatchedLiterally()
    {
        var codeOwners = Create(
            """
            *              @global
            §§DOUBLESTAR§§ @literal

            """,
            CodeOwners.Dialect.GitHub);

        Match(codeOwners, "/§§DOUBLESTAR§§").Should().Equal(["@literal"]);
        Match(codeOwners, "/anything-else").Should().Equal(["@global"]);
    }

    [SkippableFact]
    public void PathologicalPatternMatchingIsDeterministicBoundedAndThreadSafe()
    {
        var pathologicalPattern = string.Concat(Enumerable.Repeat("*a", 32)) + "b";
        var codeOwners = Create(
            $"""
            * @global
            {pathologicalPattern} @slow

            """,
            CodeOwners.Dialect.GitHub);
        var nonMatchingPath = "/" + new string('a', 2_000) + "c";
        var matchingPath = "/" + new string('a', 32) + "b";

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            Match(codeOwners, nonMatchingPath).Should().Equal(["@global"]);
        }

        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(TestTimeout, "glob matching has bounded, non-backtracking cost");
        Match(codeOwners, matchingPath).Should().Equal(["@slow"], "a difficult non-match must not disable the rule globally");

        const int concurrentWorkerCount = 4;
        using var startBarrier = new Barrier(concurrentWorkerCount);
        // The barrier releases all dedicated threads together without relying on the shared ThreadPool.
        var concurrentMatches = Enumerable.Range(0, concurrentWorkerCount)
                                          .Select(
                                               _ => Task.Factory.StartNew(
                                                   () =>
                                                   {
                                                       startBarrier.SignalAndWait(TestTimeout).Should().BeTrue("all workers must be ready before matching starts");
                                                       return Match(codeOwners, nonMatchingPath);
                                                   },
                                                   CancellationToken.None,
                                                   TaskCreationOptions.LongRunning,
                                                   TaskScheduler.Default))
                                          .ToArray();

        Task.WaitAll(concurrentMatches, TestTimeout).Should().BeTrue("concurrent matching must not deadlock");
        foreach (var concurrentMatch in concurrentMatches)
        {
            concurrentMatch.Result.Should().Equal(["@global"]);
        }
    }

    [SkippableFact]
    public void OverlongSegmentPatternIsRejectedBeforeMatching()
    {
        var pathologicalPattern = "*" + new string('a', 1_024) + "b";
        var codeOwners = Create(
            $"""
            * @global
            {pathologicalPattern} @slow

            """,
            CodeOwners.Dialect.GitHub);

        codeOwners.ParsingDiagnosticsCount.Should().Be(1);
        Match(codeOwners, "/" + new string('a', 2_000) + "c").Should().Equal(["@global"]);
    }

    [SkippableFact]
    public void SegmentWildcardMatchingStopsAtWorkLimit()
    {
        var repeatedSuffixPattern = "*" + new string('a', 256) + "b";
        var codeOwners = Create(
            $"""
            * @global
            {repeatedSuffixPattern} @slow

            """,
            CodeOwners.Dialect.GitHub);

        codeOwners.ParsingDiagnosticsCount.Should().Be(0, "the rule is valid and bounded at match time");
        Match(codeOwners, "/" + new string('a', 2_000) + "b").Should().Equal(["@global"]);
    }

    [SkippableFact]
    public void LargeUniqueOwnerListsHaveLinearRepeatedMatchCost()
    {
        const int ownerCount = 5_000;
        var owners = string.Join(" ", Enumerable.Range(0, ownerCount).Select(i => "@owner" + i));
        var codeOwners = Create(
            $"""
            *.cs {owners}

            """,
            CodeOwners.Dialect.GitHub);

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            codeOwners.Match("/file.cs").Count().Should().Be(ownerCount);
        }

        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(TestTimeout, "owners are deduplicated once during parsing, not quadratically on every match");
    }

    [SkippableFact]
    public void LongMalformedGitLabOwnerTokensHaveBoundedParsingCost()
    {
        var malformedOwner = new string('x', 4_000_000);
        var path = WriteTemporaryCodeOwners($"""*.cs {malformedOwner}""");
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var codeOwners = new CodeOwners(path, CodeOwners.Dialect.GitLab);
            stopwatch.Stop();

            stopwatch.Elapsed.Should().BeLessThan(TestTimeout, "the broad timeout guards against hangs without acting as a microbenchmark");
            Match(codeOwners, "/file.cs").Should().BeEmpty();
            codeOwners.ParsingDiagnosticsCount.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SkippableFact]
    public void LongMalformedGitLabSectionHeadersHaveBoundedParsingCost()
    {
        var malformedHeader = "[Docs] " + new string(' ', 1_000_000) + "!\n";
        var path = WriteTemporaryCodeOwners(malformedHeader);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var codeOwners = new CodeOwners(path, CodeOwners.Dialect.GitLab);
            stopwatch.Stop();

            stopwatch.Elapsed.Should().BeLessThan(TestTimeout, "section validation must scan malformed headers without regex backtracking");
            codeOwners.ParsingDiagnosticsCount.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SkippableFact]
    public void DuplicateOwnersAreDeduplicatedOnceInStableOrder()
    {
        var codeOwners = Create(
            """
            *.cs @first @second @first @third @second

            """,
            CodeOwners.Dialect.GitHub);

        codeOwners.Match("/file.cs").Should().Equal(["@first", "@second", "@third"]);
    }

    [SkippableFact]
    public void LargeGitLabDuplicatePatternSetsAreCompactedLinearly()
    {
        const int patternCount = 20_000;
        var firstDefinitions = Enumerable.Range(0, patternCount).Select(i => $"/path/{i}.cs @old");
        var replacements = Enumerable.Range(0, patternCount).Select(i => $"/path/{i}.cs @new");
        var content = string.Join("\n", firstDefinitions.Concat(replacements)) + "\n";

        var path = WriteTemporaryCodeOwners(content);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var codeOwners = new CodeOwners(path, CodeOwners.Dialect.GitLab);
            stopwatch.Stop();

            stopwatch.Elapsed.Should().BeLessThan(TestTimeout, "duplicate replacement is a single reverse pass instead of repeated List.Remove calls");
            Match(codeOwners, "/path/0.cs").Should().Equal(["@new"]);
            Match(codeOwners, $"/path/{patternCount - 1}.cs").Should().Equal(["@new"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SkippableFact]
    public void GithubIgnoresCodeOwnersFilesOverThreeMegabytesOnly()
    {
        var belowLimit = WriteTemporaryCodeOwnersWithLength(CodeOwners.GitHubMaximumFileSizeBytes - 1);
        var aboveLimit = WriteTemporaryCodeOwnersWithLength(CodeOwners.GitHubMaximumFileSizeBytes + 1);
        try
        {
            Match(new CodeOwners(belowLimit, CodeOwners.Dialect.GitHub), "/file.cs").Should().Equal(["@owner"]);
            Match(new CodeOwners(aboveLimit, CodeOwners.Dialect.GitHub), "/file.cs").Should().BeEmpty();
            Match(new CodeOwners(aboveLimit, CodeOwners.Dialect.GitLab), "/file.cs").Should().Equal(["@owner"]);
        }
        finally
        {
            File.Delete(belowLimit);
            File.Delete(aboveLimit);
        }
    }

    [SkippableFact]
    public void TryLoadReturnsFalseWhenCodeOwnersDisappearsBeforeOpening()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "dd-codeowners-missing-" + Guid.NewGuid().ToString("N"));

        CodeOwners.TryLoad(missingPath, CodeOwners.Dialect.GitHub, out var codeOwners).Should().BeFalse();
        codeOwners.Should().BeNull();
    }

    [SkippableFact]
    public void GitLabDuplicateSectionsAreCombinedCaseInsensitively()
    {
        var content = """
            [Docs]
            *.md @old
            [DOCS]
            README.md @new

            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/README.md").Should().Equal(["@new"]);
        Match(codeOwners, "/guide.md").Should().Equal(["@old"]);
    }

    [SkippableFact]
    public void GitLabDuplicateSectionExclusionsAreStickyAcrossOccurrences()
    {
        var content = """
            [Ruby]
            *.rb @ruby-team
            [RUBY]
            !/config/**/*.rb
            /config/routes.rb @ops

            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/lib/model.rb").Should().Equal(["@ruby-team"]);
        Match(codeOwners, "/config/routes.rb").Should().BeEmpty();
    }

    [SkippableFact]
    public void GitLabDuplicateSectionDefaultsApplyToEntriesUnderEachHeader()
    {
        var content = """
            [Docs] @old-default
            *.md
            [DOCS] @new-default
            README.md

            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

        Match(codeOwners, "/guide.md").Should().Equal(["@old-default"]);
        Match(codeOwners, "/README.md").Should().Equal(["@new-default"]);
    }

    [SkippableFact]
    public void UnsupportedGithubSyntaxIsIgnored()
    {
        var content = """
            * @global
            !secret.txt @negation
            file[0].cs @range
            \#hash.txt @hash

            """;
        var codeOwners = Create(content, CodeOwners.Dialect.GitHub);

        Match(codeOwners, "/!secret.txt").Should().Equal(["@global"]);
        Match(codeOwners, "/file[0].cs").Should().Equal(["@global"]);
        Match(codeOwners, "/#hash.txt").Should().Equal(["@global"]);
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
        var codeOwners = Create(content, CodeOwners.Dialect.GitHub);

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
        var codeOwners = Create(content, CodeOwners.Dialect.GitLab);

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
        var codeOwners = Create(
            """
            *.md   @md
            /docs/ @doctocat

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, "//docs/getting-started.md").Should().Equal(["@doctocat"]);
        Match(codeOwners, "/docs/getting-started.md").Should().Equal(["@doctocat"]);
        Match(codeOwners, string.Empty).Should().BeEmpty();
    }

    [SkippableFact]
    public void NullPathReturnsNoOwners()
    {
        // Defensive: a null path must not throw and simply has no owners.
        var codeOwners = Create(
            """
            * @global

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, null!).Should().BeEmpty();
    }

    [SkippableFact]
    public void QuestionMarkDoesNotMatchSlash()
    {
        var codeOwners = Create(
            """
            a?c @segment

            """,
            CodeOwners.Dialect.GitHub);
        Match(codeOwners, "/abc").Should().Equal(["@segment"]);
        Match(codeOwners, "/aXc").Should().Equal(["@segment"]);
        Match(codeOwners, "/a/c").Should().BeEmpty();
    }

    [SkippableFact]
    public void DoubleStarIsGlobstarOnlyAsAWholeSegment()
    {
        var inSegment = Create(
            """
            foo**bar @stars

            """,
            CodeOwners.Dialect.GitHub);
        var slashAfterSegment = Create(
            """
            /foo**/bar @component-stars

            """,
            CodeOwners.Dialect.GitHub);
        var globstar = Create(
            """
            **/index.md @index

            """,
            CodeOwners.Dialect.GitHub);

        // Adjacent asterisks inside a segment are two single-level wildcards, not a globstar.
        Match(inSegment, "/fooXbar").Should().Equal(["@stars"]);
        Match(slashAfterSegment, "/foo/bar").Should().Equal(["@component-stars"]);
        Match(slashAfterSegment, "/fooX/bar").Should().Equal(["@component-stars"]);
        Match(slashAfterSegment, "/foobar").Should().BeEmpty("the slash after an in-segment double star remains required");
        Match(slashAfterSegment, "/foo/x/bar").Should().BeEmpty("in-segment stars cannot cross a directory boundary");
        Match(globstar, "/docs/index.md").Should().Equal(["@index"]);
        Match(globstar, "/index.md").Should().Equal(["@index"]);
    }

    [SkippableFact]
    public void DescendantMatchingIsStableAcrossRepeatedCalls()
    {
        // Direct directory matches and descendants share one deterministic glob; repeated calls
        // must preserve both forms without recompilation or an ancestor walk.
        var codeOwners = Create(
            """
            **/logs @octocat

            """,
            CodeOwners.Dialect.GitHub);
        for (var i = 0; i < 3; i++)
        {
            Match(codeOwners, "/build/logs/error.txt").Should().Equal(["@octocat"]);
            Match(codeOwners, "/build/logs/nested/error.txt").Should().Equal(["@octocat"]);
            Match(codeOwners, "/catalog/data.tmp").Should().BeEmpty();
            Match(codeOwners, "/logs").Should().Equal(["@octocat"]);
        }
    }

    private static CodeOwners Create(string content, CodeOwners.Dialect dialect)
        => CodeOwners.Parse(content.Replace("\r\n", "\n").Split('\n'), dialect);

    private static CodeOwners.Dialect ToCodeOwnersDialect(TestDialect dialect)
        => dialect == TestDialect.GitLab ? CodeOwners.Dialect.GitLab : CodeOwners.Dialect.GitHub;

    private static string WriteTemporaryCodeOwners(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "dd-codeowners-spec-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, content);
        return path;
    }

    private static string WriteTemporaryCodeOwnersWithLength(long length)
    {
        var path = Path.Combine(Path.GetTempPath(), "dd-codeowners-sized-" + Guid.NewGuid().ToString("N"));
        var prefix = Encoding.ASCII.GetBytes("*.cs @owner\n#");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(prefix, 0, prefix.Length);
        stream.SetLength(length);
        return path;
    }

    private static string[] Match(CodeOwners codeOwners, string path)
        => codeOwners.Match(path).OrderBy(o => o, StringComparer.Ordinal).ToArray();
}
