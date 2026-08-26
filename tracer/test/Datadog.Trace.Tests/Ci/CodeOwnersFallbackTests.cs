// <copyright file="CodeOwnersFallbackTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(EnvironmentVariablesTestCollection))]
public class CodeOwnersFallbackTests
{
    private const string GlobalAndSourceOwners = """
        *     @global
        /src/ @owner

        """;

    private const string GitLabSectionOwner = """
        [Section] @gitlab-owner
        *.cs

        """;

    private const string OwnerAndSourceOwners = """
        *     @owner
        /src/ @src-owner

        """;

    private const string OwnerOnly = """
        * @owner

        """;

    private const string CommitSha = "3245605c3d1edc67226d725799ee969c71f7632b";

    [SkippableFact]
    public void UsesWorkspaceRootWhenSourceRootIsDifferent()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), GlobalAndSourceOwners);
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var ciValues = new TestCIEnvironmentValues(Path.Combine(repoRoot, "other"), repoRoot, "github");
        var ownership = ciValues.ResolveSourceOwnership(sourceFile, useOSSeparator: false);

        Assert.Equal("src/SpanBenchmark.cs", ownership.RepositoryRelativePath);
        Assert.Equal(["@owner"], ownership.MatchingOwners);
    }

    [SkippableFact]
    public void UsesFallbackRootWhenSourceRootIsSubdirectory()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), GlobalAndSourceOwners);
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var ciValues = CreateGitHubEnvironmentForWorkspace(srcDir);
        var ownership = ciValues.ResolveSourceOwnership(sourceFile, useOSSeparator: false);

        Assert.Equal("src/SpanBenchmark.cs", ownership.RepositoryRelativePath);
        Assert.Equal(["@owner"], ownership.MatchingOwners);
    }

    [SkippableFact]
    public void UsesGitRootInsteadOfNestedCodeOwnersFile()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @repository-owner");
        File.WriteAllText(Path.Combine(srcDir, "CODEOWNERS"), "* @nested-owner");
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var ciValues = CreateGitHubEnvironmentForWorkspace(srcDir);
        var ownership = ciValues.ResolveSourceOwnership(sourceFile, useOSSeparator: false);

        Assert.Equal("src/SpanBenchmark.cs", ownership.RepositoryRelativePath);
        Assert.Equal(["@repository-owner"], ownership.MatchingOwners);
    }

    [SkippableFact]
    public void DoesNotUseCurrentDirectoryForRelativeSourceFile()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), OwnerOnly);
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var originalDirectory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = repoRoot;
        try
        {
            var ciValues = CIEnvironmentValues.Create(env);
            var ownership = ciValues.ResolveSourceOwnership("src/SpanBenchmark.cs", useOSSeparator: false);

            Assert.Equal("src/SpanBenchmark.cs", ownership.RepositoryRelativePath);
            Assert.False(ciValues.HasCodeOwners);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    [SkippableFact]
    public void CodeOwnersDecisionDoesNotChangeAfterInitialization()
    {
        using var repoDirectory = new TemporaryDirectory();
        var repoRoot = repoDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var ciValues = CreateGitHubEnvironmentForWorkspace(repoRoot);
        Assert.False(ciValues.HasCodeOwners);

        // A test session makes one CODEOWNERS decision during initialization.
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), GlobalAndSourceOwners);
        var ownership = ciValues.ResolveSourceOwnership(sourceFile, useOSSeparator: false);

        Assert.Equal("src/SpanBenchmark.cs", ownership.RepositoryRelativePath);
        Assert.False(ownership.IsRepositoryRelative);
        Assert.False(ciValues.HasCodeOwners);
        Assert.Empty(ownership.MatchingOwners);
    }

    [SkippableFact]
    public void GitHubProviderUsesOfficialCodeOwnersLocationPriority()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".github"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs"));
        File.WriteAllText(Path.Combine(repoRoot, ".github", "CODEOWNERS"), """* @github-directory""");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), """* @repository-root""");
        File.WriteAllText(Path.Combine(repoRoot, "docs", "CODEOWNERS"), """* @docs-directory""");
        var ciValues = new ReloadingEnvironmentValues(repoRoot, "github");

        ciValues.Reload();
        Assert.Equal(["@github-directory"], ResolveOwners(ciValues, "file.cs"));

        File.Delete(Path.Combine(repoRoot, ".github", "CODEOWNERS"));
        ciValues.Reload();
        Assert.Equal(["@repository-root"], ResolveOwners(ciValues, "file.cs"));

        File.Delete(Path.Combine(repoRoot, "CODEOWNERS"));
        ciValues.Reload();
        Assert.Equal(["@docs-directory"], ResolveOwners(ciValues, "file.cs"));
    }

    [SkippableFact]
    public void GitLabProviderUsesOfficialCodeOwnersLocationPriority()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".gitlab"));
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), """* @repository-root""");
        File.WriteAllText(Path.Combine(repoRoot, "docs", "CODEOWNERS"), """* @docs-directory""");
        File.WriteAllText(Path.Combine(repoRoot, ".gitlab", "CODEOWNERS"), """* @gitlab-directory""");
        var ciValues = new ReloadingEnvironmentValues(repoRoot, "gitlab");

        ciValues.Reload();
        Assert.Equal(["@repository-root"], ResolveOwners(ciValues, "file.cs"));

        File.Delete(Path.Combine(repoRoot, "CODEOWNERS"));
        ciValues.Reload();
        Assert.Equal(["@docs-directory"], ResolveOwners(ciValues, "file.cs"));

        File.Delete(Path.Combine(repoRoot, "docs", "CODEOWNERS"));
        ciValues.Reload();
        Assert.Equal(["@gitlab-directory"], ResolveOwners(ciValues, "file.cs"));
    }

    [SkippableFact]
    public void CodeOwnersDiscoveryIgnoresLocationsFromTheOtherDialect()
    {
        using var githubDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(githubDirectory.RootPath, ".gitlab"));
        File.WriteAllText(Path.Combine(githubDirectory.RootPath, ".gitlab", "CODEOWNERS"), """* @gitlab-only""");
        var githubValues = new ReloadingEnvironmentValues(githubDirectory.RootPath, "github");

        githubValues.Reload();
        Assert.False(githubValues.HasCodeOwners);

        using var gitlabDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(gitlabDirectory.RootPath, ".github"));
        File.WriteAllText(Path.Combine(gitlabDirectory.RootPath, ".github", "CODEOWNERS"), """* @github-only""");
        var gitlabValues = new ReloadingEnvironmentValues(gitlabDirectory.RootPath, "gitlab");

        gitlabValues.Reload();
        Assert.False(gitlabValues.HasCodeOwners);
    }

    [SkippableTheory]
    [InlineData("https://gitlab.com/DataDog/dd-trace-dotnet.git")]
    [InlineData("git@gitlab.com:DataDog/dd-trace-dotnet.git")]
    [InlineData("https://gitlab.example.com/DataDog/dd-trace-dotnet.git")]
    [InlineData("git@gitlab.example.com:DataDog/dd-trace-dotnet.git")]
    public void UsesRepositoryHostToSelectCodeOwnersDialect(string repositoryUrl)
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".gitlab"));
        File.WriteAllText(Path.Combine(repoRoot, ".gitlab", "CODEOWNERS"), GitLabSectionOwner);

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Jenkins.Url] = "https://jenkins.example.com",
            [PlatformKeys.Ci.Jenkins.GitUrl] = repositoryUrl,
            [PlatformKeys.Ci.Jenkins.GitCommit] = CommitSha,
            [PlatformKeys.Ci.Jenkins.Workspace] = repoRoot,
        };

        var ciValues = CIEnvironmentValues.Create(env);

        Assert.Equal("jenkins", ciValues.Provider);
        Assert.Equal(["@gitlab-owner"], ResolveOwners(ciValues, "file.cs"));
    }

    [SkippableFact]
    public void UsesGitLabSpecificLocationWhenRepositoryHostIsUnknown()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".gitlab"));
        File.WriteAllText(Path.Combine(repoRoot, ".gitlab", "CODEOWNERS"), GitLabSectionOwner);

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Jenkins.Url] = "https://jenkins.example.com",
            [PlatformKeys.Ci.Jenkins.GitUrl] = "https://source.example.com/DataDog/dd-trace-dotnet.git",
            [PlatformKeys.Ci.Jenkins.GitCommit] = CommitSha,
            [PlatformKeys.Ci.Jenkins.Workspace] = repoRoot,
        };

        var ciValues = CIEnvironmentValues.Create(env);

        Assert.Equal(["@gitlab-owner"], ResolveOwners(ciValues, "file.cs"));
    }

    [SkippableFact]
    public void DetectsGitLabDialectAtAncestorRepositoryRoot()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var sourceRoot = Path.Combine(repoRoot, "src");
        var sourceFile = Path.Combine(sourceRoot, "SpanBenchmark.cs");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".gitlab"));
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(repoRoot, ".gitlab", "CODEOWNERS"), GitLabSectionOwner);
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Jenkins.Url] = "https://jenkins.example.com",
            [PlatformKeys.Ci.Jenkins.GitUrl] = "https://source.example.com/DataDog/dd-trace-dotnet.git",
            [PlatformKeys.Ci.Jenkins.GitCommit] = CommitSha,
            [PlatformKeys.Ci.Jenkins.Workspace] = sourceRoot,
        };

        var ciValues = CIEnvironmentValues.Create(env);
        var ownership = ciValues.ResolveSourceOwnership(sourceFile, useOSSeparator: false);

        Assert.Equal("src/SpanBenchmark.cs", ownership.RepositoryRelativePath);
        Assert.Equal(["@gitlab-owner"], ownership.MatchingOwners);
    }

    [SkippableFact]
    public void DoesNotMatchCodeOwnersForFileOutsideRoot()
    {
        using var repoDirectory = new TemporaryDirectory();
        using var externalDirectory = new TemporaryDirectory();

        var repoRoot = repoDirectory.RootPath;
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), OwnerOnly);

        var externalFile = Path.Combine(externalDirectory.RootPath, "SpanBenchmark.cs");
        File.WriteAllText(externalFile, "class SpanBenchmark {}");

        var ciValues = CreateGitHubEnvironmentForWorkspace(repoRoot);

        var ownership = ciValues.ResolveSourceOwnership(externalFile, useOSSeparator: false);

        Assert.True(ciValues.HasCodeOwners);
        Assert.False(ownership.IsRepositoryRelative);
        Assert.Empty(ownership.MatchingOwners);
        Assert.Null(ownership.CodeOwnersTag);
    }

    [SkippableFact]
    public void DoesNotMatchCodeOwnersWhenFallbackCannotResolve()
    {
        using var repoDirectory = new TemporaryDirectory();
        var repoRoot = repoDirectory.RootPath;
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), """* @global""");

        var ciValues = CreateGitHubEnvironmentForWorkspace(repoRoot);

        var externalRoot = Path.Combine(Path.GetTempPath(), "dd-ci-outside-" + Guid.NewGuid().ToString("N"));
        var sourceFile = Path.Combine(externalRoot, "tracer", "test", "Snapshots", "Snapshot.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(sourceFile, "class Snapshot {}");

        var ownership = ciValues.ResolveSourceOwnership(sourceFile, useOSSeparator: false);

        Assert.StartsWith("..", ownership.RepositoryRelativePath, StringComparison.Ordinal);
        Assert.False(ownership.IsRepositoryRelative);
        Assert.Empty(ownership.MatchingOwners);
        Assert.Null(ownership.CodeOwnersTag);
    }

    [SkippableFact]
    public void UsesWorkspaceFallbackWhenSourceRootIsDifferent()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "tracer", "test", "benchmarks", "Benchmarks.Trace");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        const string codeOwnersFile = """
            *                                          @global
            /tracer/test/benchmarks/Benchmarks.Trace/  @owner

            """;
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), codeOwnersFile);
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var ciValues = new TestCIEnvironmentValues("/go/src/github.com/DataDog/apm-reliability/dd-trace-dotnet", repoRoot);
        var ownership = ciValues.ResolveSourceOwnership(sourceFile, useOSSeparator: false);

        Assert.Equal("tracer/test/benchmarks/Benchmarks.Trace/SpanBenchmark.cs", ownership.RepositoryRelativePath);
        Assert.True(ownership.IsRepositoryRelative);
        Assert.Equal(["@owner"], ownership.MatchingOwners);
    }

    [SkippableFact]
    public void DoesNotSearchOutsideWorkspaceForRelativeSourceFile()
    {
        using var repoDirectory = new TemporaryDirectory();
        using var outsideDirectory = new TemporaryDirectory();

        var repoRoot = repoDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var outsideRoot = outsideDirectory.RootPath;
        File.WriteAllText(Path.Combine(outsideRoot, "CODEOWNERS"), OwnerOnly);
        File.WriteAllText(Path.Combine(outsideRoot, "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var outsideFolderName = Path.GetFileName(outsideRoot);
        var relativeSourcePath = Path.Combine("..", outsideFolderName, "SpanBenchmark.cs");

        var ciValues = CreateGitHubEnvironmentForWorkspace(repoRoot);

        var ownership = ciValues.ResolveSourceOwnership(relativeSourcePath, useOSSeparator: false);

        Assert.False(ownership.IsRepositoryRelative);
        Assert.False(ciValues.HasCodeOwners);
    }

    [SkippableTheory]
    [InlineData("../../../_/tracer/test/SpanBenchmark.cs")]
    [InlineData("../../../_/tracer//test/SpanBenchmark.cs")]
    public void AnchorsForeignRelativePathsToCodeOwnersRoot(string foreignRelativePath)
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "tracer", "test");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        const string codeOwnersFile = """
            *             @global
            /tracer/test/ @owner

            """;
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), codeOwnersFile);
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var ciValues = CreateGitHubEnvironmentForWorkspace(repoRoot);

        // Paths recorded against a foreign base directory (e.g. "../../../_/..." on CI agents)
        // must be anchored back to a repository-relative path.
        var ownership = ciValues.ResolveSourceOwnership(foreignRelativePath, useOSSeparator: false);
        var repeatedOwnership = ciValues.ResolveSourceOwnership(foreignRelativePath, useOSSeparator: false);

        Assert.True(ownership.IsRepositoryRelative);
        Assert.Equal("tracer/test/SpanBenchmark.cs", ownership.RepositoryRelativePath);
        Assert.Equal(["@owner"], ownership.MatchingOwners);
        Assert.Equal("[\"@owner\"]", ownership.CodeOwnersTag);
        Assert.Same(ownership.CodeOwnersTag, repeatedOwnership.CodeOwnersTag);
    }

    [SkippableFact]
    public void AnchoredPathRespectsUseOSSeparator()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "tracer", "test");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), """* @global""");
        File.WriteAllText(Path.Combine(srcDir, "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var ciValues = CreateGitHubEnvironmentForWorkspace(repoRoot);
        var foreignRelativePath = "../../../_/tracer/test/SpanBenchmark.cs";

        var forwardSlashOwnership = ciValues.ResolveSourceOwnership(foreignRelativePath, useOSSeparator: false);
        Assert.True(forwardSlashOwnership.IsRepositoryRelative);
        Assert.Equal("tracer/test/SpanBenchmark.cs", forwardSlashOwnership.RepositoryRelativePath);

        var osOwnership = ciValues.ResolveSourceOwnership(foreignRelativePath, useOSSeparator: true);
        Assert.True(osOwnership.IsRepositoryRelative);
        Assert.Equal(Path.Combine("tracer", "test", "SpanBenchmark.cs"), osOwnership.RepositoryRelativePath);
    }

    [SkippableFact]
    public void DoesNotAnchorForeignPathsWhenSuffixDoesNotExistUnderRoot()
    {
        using var repoDirectory = new TemporaryDirectory();
        using var externalDirectory = new TemporaryDirectory();

        var repoRoot = repoDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, "src"));
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), OwnerAndSourceOwners);
        File.WriteAllText(Path.Combine(repoRoot, "src", "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var externalFile = Path.Combine(externalDirectory.RootPath, "other", "SpanBenchmark.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(externalFile)!);
        File.WriteAllText(externalFile, "class SpanBenchmark {}");

        var ciValues = CreateGitHubEnvironmentForWorkspace(repoRoot);

        // A foreign path whose suffix does not exist under the repository must not be re-anchored.
        var ownership = ciValues.ResolveSourceOwnership("../other/SpanBenchmark.cs", useOSSeparator: false);
        Assert.False(ownership.IsRepositoryRelative);
    }

    [SkippableFact]
    public void AnchorsAzurePipelinesCompilerRecordedPaths()
    {
        // Reproduces the reported CI Visibility issue: on Azure Pipelines agents, compiler-recorded
        // source paths are relative to a foreign base directory (e.g.
        // "../../../../../../_/tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs"),
        // which made every test span lose its test.codeowners tag once the CODEOWNERS rules were
        // changed back to rooted patterns.
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var sourceDir = Path.Combine(repoRoot, "tracer", "test", "Datadog.Trace.DuckTyping.Tests");
        Directory.CreateDirectory(sourceDir);
        var sourceFile = Path.Combine(sourceDir, "ExceptionsTests.cs");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".github"));
        File.WriteAllText(Path.Combine(repoRoot, ".github", "CODEOWNERS"), """/tracer/test/ @DataDog/tracing-dotnet""");
        File.WriteAllText(sourceFile, "// test");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Azure.TFBuild] = "True",
            [PlatformKeys.Ci.Azure.SystemTeamFoundationServerUri] = "https://dev.azure.com/datadoghq/",
            [PlatformKeys.Ci.Azure.BuildSourcesDirectory] = repoRoot,
            [PlatformKeys.Ci.Azure.BuildSourceVersion] = CommitSha,
            [PlatformKeys.Ci.Azure.BuildRepositoryUri] = "https://github.com/DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);
        var foreignRelativePath = "../../../../../../_/tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs";
        var ownership = ciValues.ResolveSourceOwnership(foreignRelativePath, useOSSeparator: false);

        Assert.True(ciValues.HasCodeOwners);
        Assert.True(ownership.IsRepositoryRelative);
        Assert.Equal("tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs", ownership.RepositoryRelativePath);
        Assert.Equal(["@DataDog/tracing-dotnet"], ownership.MatchingOwners);
    }

    [SkippableTheory]
    [InlineData(@"D:\a\_work\1\s\src\SpanBenchmark.cs")]
    [InlineData(@"D:\a\1\s\src\SpanBenchmark.cs")]
    [InlineData("/home/vsts/work/1/s/src/SpanBenchmark.cs")]
    [InlineData("/tmp/work/1/s/src/SpanBenchmark.cs")]
    [InlineData("file:///D:/a/_work/1/s/src/SpanBenchmark.cs")]
    [InlineData("https://example.com/a/_work/1/s/src/SpanBenchmark.cs")]
    public void DoesNotAnchorAbsoluteAzurePipelinesPathsWithMatchingRepositorySuffix(string sourcePath)
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var sourceDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), """/src/ @owner""");
        File.WriteAllText(Path.Combine(sourceDir, "SpanBenchmark.cs"), "// test");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Azure.TFBuild] = "True",
            [PlatformKeys.Ci.Azure.BuildSourcesDirectory] = repoRoot,
            [PlatformKeys.Ci.Azure.BuildSourceVersion] = CommitSha,
            [PlatformKeys.Ci.Azure.BuildRepositoryUri] = "https://github.com/DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        var ownership = ciValues.ResolveSourceOwnership(sourcePath, useOSSeparator: false);
        Assert.False(ownership.IsRepositoryRelative);
    }

    [SkippableFact]
    public void DoesNotAnchorPathsWithInteriorNavigationSegments()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), OwnerAndSourceOwners);
        File.WriteAllText(Path.Combine(srcDir, "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var ciValues = CreateGitHubEnvironmentForWorkspace(repoRoot);

        // An interior navigation segment depends on the unknown base directory where the path was
        // recorded; anchoring it would produce a malformed repository-relative path.
        var ownership = ciValues.ResolveSourceOwnership("../other/src/../src/SpanBenchmark.cs", useOSSeparator: false);
        Assert.False(ownership.IsRepositoryRelative);
    }

    [SkippableTheory]
    [InlineData("file:///outside/src/SpanBenchmark.cs")]
    [InlineData("https://example.com/src/SpanBenchmark.cs")]
    [InlineData("../../C:/outside/src/SpanBenchmark.cs")]
    [InlineData("../..//outside/src/SpanBenchmark.cs")]
    [InlineData(@"..\..\\server\share\src\SpanBenchmark.cs")]
    public void DoesNotAnchorAbsoluteOrEmbeddedRootedPathsWithMatchingRepositorySuffix(string sourceFilePath)
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), OwnerAndSourceOwners);
        File.WriteAllText(Path.Combine(srcDir, "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var ciValues = CreateGitHubEnvironmentForWorkspace(repoRoot);

        var ownership = ciValues.ResolveSourceOwnership(sourceFilePath, useOSSeparator: false);
        Assert.False(ownership.IsRepositoryRelative);
    }

    private static string[] ResolveOwners(CIEnvironmentValues ciValues, string sourcePath)
        => ciValues.ResolveSourceOwnership(sourcePath, useOSSeparator: false).MatchingOwners;

    private static CIEnvironmentValues CreateGitHubEnvironmentForWorkspace(string workspace)
        => CIEnvironmentValues.Create(new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = workspace,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        });

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "dd-ci-codeowners-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch
            {
                // Cleanup failure should not fail tests.
            }
        }
    }

    private sealed class TestCIEnvironmentValues : CIEnvironmentValues
    {
        private readonly string? _sourceRoot;
        private readonly string? _workspacePath;
        private readonly string? _provider;

        public TestCIEnvironmentValues(string? sourceRoot, string? workspacePath, string? provider = null)
        {
            _sourceRoot = sourceRoot;
            _workspacePath = workspacePath;
            _provider = provider;
            ReloadEnvironmentData();
        }

        protected override void Setup(IGitInfo gitInfo)
        {
            SourceRoot = _sourceRoot;
            WorkspacePath = _workspacePath;
            Provider = _provider;
        }
    }

    private sealed class ReloadingEnvironmentValues : CIEnvironmentValues
    {
        private readonly string _sourceRoot;
        private readonly string _provider;

        public ReloadingEnvironmentValues(string sourceRoot, string provider)
        {
            _sourceRoot = sourceRoot;
            _provider = provider;
        }

        public void Reload() => ReloadEnvironmentData();

        protected override void Setup(IGitInfo gitInfo)
        {
            SourceRoot = _sourceRoot;
            WorkspacePath = _sourceRoot;
            Provider = _provider;
        }
    }
}
