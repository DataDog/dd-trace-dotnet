// <copyright file="CodeOwnersFallbackTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(EnvironmentVariablesTestCollection))]
public class CodeOwnersFallbackTests
{
    private const string CommitSha = "3245605c3d1edc67226d725799ee969c71f7632b";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [SkippableFact]
    public void UsesFallbackRootWhenSourceRootIsDifferent()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @global\n/src/ @owner\n");
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = Path.Combine(repoRoot, "other"),
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);
        var relative = ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false);

        Assert.Equal("src/SpanBenchmark.cs", relative);

        var owners = ciValues.CodeOwners!.Match("/" + relative).OrderBy(o => o).ToArray();
        Assert.Equal(new[] { "@owner" }, owners);
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
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @global\n/src/ @owner\n");
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = srcDir,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);
        var relative = ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false);

        Assert.Equal("src/SpanBenchmark.cs", relative);

        var owners = ciValues.CodeOwners!.Match("/" + relative).OrderBy(o => o).ToArray();
        Assert.Equal(new[] { "@owner" }, owners);
    }

    [SkippableFact]
    public void DoesNotUseCurrentDirectoryForRelativeSourceFile()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @owner\n");
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
            var relative = ciValues.MakeRelativePathFromSourceRootWithFallback("src/SpanBenchmark.cs", false);

            Assert.Equal("src/SpanBenchmark.cs", relative);
            Assert.Null(ciValues.CodeOwners);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    [SkippableFact]
    public void AllowsFallbackRetryWithDifferentStartPath()
    {
        using var repoDirectory = new TemporaryDirectory();
        using var otherDirectory = new TemporaryDirectory();

        var repoRoot = repoDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @global\n/src/ @owner\n");
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var otherRoot = otherDirectory.RootPath;
        var otherSrcDir = Path.Combine(otherRoot, "src");
        Directory.CreateDirectory(otherSrcDir);
        var otherFile = Path.Combine(otherSrcDir, "Other.cs");
        File.WriteAllText(otherFile, "class Other {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = otherRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);
        var otherRelative = ciValues.MakeRelativePathFromSourceRootWithFallback(otherFile, false);

        Assert.Equal("src/Other.cs", otherRelative);
        Assert.Null(ciValues.CodeOwners);

        var relative = ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false);

        Assert.Equal("src/SpanBenchmark.cs", relative);
        Assert.NotNull(ciValues.CodeOwners);

        Assert.True(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out var codeOwnersRelativePath));
        var owners = ciValues.CodeOwners!.Match("/" + codeOwnersRelativePath).OrderBy(o => o).ToArray();
        Assert.Equal(new[] { "@owner" }, owners);
    }

    [SkippableFact]
    public void GitRepositoryRootCodeOwnersWinsOverNestedCandidate()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var sourceDirectory = Path.Combine(repoRoot, "src", "nested");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".github"));
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(repoRoot, ".github", "CODEOWNERS"), "* @root-owner\n");
        File.WriteAllText(Path.Combine(repoRoot, "src", "CODEOWNERS"), "* @nested-decoy\n");
        var sourceFile = Path.Combine(sourceDirectory, "File.cs");
        File.WriteAllText(sourceFile, string.Empty);

        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: repoRoot);

        Assert.True(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out var relativePath));
        Assert.Equal("src/nested/File.cs", relativePath);
        Assert.Equal(repoRoot, ciValues.CodeOwnersRoot);
        Assert.Equal(["@root-owner"], ciValues.CodeOwners!.Match("/" + relativePath));
    }

    [SkippableFact]
    public void NestedCandidateIsIgnoredWhenGitRepositoryRootHasNoCodeOwners()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var sourceDirectory = Path.Combine(repoRoot, "src", "nested");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(repoRoot, "src", "CODEOWNERS"), "* @nested-decoy\n");
        var sourceFile = Path.Combine(sourceDirectory, "File.cs");
        File.WriteAllText(sourceFile, string.Empty);

        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: repoRoot);

        Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out _));
        Assert.Null(ciValues.CodeOwners);
        Assert.Null(ciValues.CodeOwnersRoot);
    }

    [SkippableFact]
    public void WorkspaceRootCodeOwnersWinsOverNestedCandidateWithoutGitMetadata()
    {
        using var tempDirectory = new TemporaryDirectory();
        var workspaceRoot = tempDirectory.RootPath;
        var sourceDirectory = Path.Combine(workspaceRoot, "src", "nested");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(workspaceRoot, "CODEOWNERS"), "* @workspace-owner\n");
        File.WriteAllText(Path.Combine(workspaceRoot, "src", "CODEOWNERS"), "* @nested-decoy\n");
        var sourceFile = Path.Combine(sourceDirectory, "File.cs");
        File.WriteAllText(sourceFile, string.Empty);

        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: workspaceRoot);

        Assert.True(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out var relativePath));
        Assert.Equal("src/nested/File.cs", relativePath);
        Assert.Equal(workspaceRoot, ciValues.CodeOwnersRoot);
        Assert.Equal(["@workspace-owner"], ciValues.CodeOwners!.Match("/" + relativePath));
    }

    [SkippableFact]
    public void DoesNotLoadCodeOwnersAboveWorkspaceWithoutGitMetadata()
    {
        using var tempDirectory = new TemporaryDirectory();
        var parentRoot = tempDirectory.RootPath;
        var workspaceRoot = Path.Combine(parentRoot, "workspace");
        var sourceDirectory = Path.Combine(workspaceRoot, "src");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(parentRoot, "CODEOWNERS"), "* @parent-decoy\n");
        var sourceFile = Path.Combine(sourceDirectory, "File.cs");
        File.WriteAllText(sourceFile, string.Empty);

        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: workspaceRoot);

        Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out _));
        Assert.Null(ciValues.CodeOwners);
        Assert.Null(ciValues.CodeOwnersRoot);
    }

    [SkippableFact]
    public void GitHubUsesOfficialCodeOwnersLocationPriority()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".github"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs"));
        File.WriteAllText(Path.Combine(repoRoot, ".github", "CODEOWNERS"), "* @github-directory\n");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @repository-root\n");
        File.WriteAllText(Path.Combine(repoRoot, "docs", "CODEOWNERS"), "* @docs-directory\n");
        var ciValues = new ReloadingGithubEnvironmentValues(repoRoot);

        ciValues.Reload();
        Assert.Equal(["@github-directory"], ciValues.CodeOwners!.Match("/file.cs"));

        File.Delete(Path.Combine(repoRoot, ".github", "CODEOWNERS"));
        ciValues.Reload();
        Assert.Equal(["@repository-root"], ciValues.CodeOwners!.Match("/file.cs"));

        File.Delete(Path.Combine(repoRoot, "CODEOWNERS"));
        ciValues.Reload();
        Assert.Equal(["@docs-directory"], ciValues.CodeOwners!.Match("/file.cs"));
    }

    [SkippableFact]
    public void GitLabUsesOfficialCodeOwnersLocationPriority()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".gitlab"));
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @repository-root\n");
        File.WriteAllText(Path.Combine(repoRoot, "docs", "CODEOWNERS"), "* @docs-directory\n");
        File.WriteAllText(Path.Combine(repoRoot, ".gitlab", "CODEOWNERS"), "* @gitlab-directory\n");
        var ciValues = new ReloadingGitlabEnvironmentValues(repoRoot);

        ciValues.Reload();
        Assert.Equal(["@repository-root"], ciValues.CodeOwners!.Match("/file.cs"));

        File.Delete(Path.Combine(repoRoot, "CODEOWNERS"));
        ciValues.Reload();
        Assert.Equal(["@docs-directory"], ciValues.CodeOwners!.Match("/file.cs"));

        File.Delete(Path.Combine(repoRoot, "docs", "CODEOWNERS"));
        ciValues.Reload();
        Assert.Equal(["@gitlab-directory"], ciValues.CodeOwners!.Match("/file.cs"));
    }

    [SkippableFact]
    public void CodeOwnersDiscoveryIgnoresOtherPlatformSpecificLocations()
    {
        using var githubDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(githubDirectory.RootPath, ".gitlab"));
        File.WriteAllText(Path.Combine(githubDirectory.RootPath, ".gitlab", "CODEOWNERS"), "* @gitlab-only\n");
        var githubValues = new ReloadingGithubEnvironmentValues(githubDirectory.RootPath);

        githubValues.Reload();
        Assert.Null(githubValues.CodeOwners);

        using var gitlabDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(gitlabDirectory.RootPath, ".github"));
        File.WriteAllText(Path.Combine(gitlabDirectory.RootPath, ".github", "CODEOWNERS"), "* @github-only\n");
        var gitlabValues = new ReloadingGitlabEnvironmentValues(gitlabDirectory.RootPath);

        gitlabValues.Reload();
        Assert.Null(gitlabValues.CodeOwners);
    }

    [SkippableFact]
    public void DoesNotMatchCodeOwnersForFileOutsideRoot()
    {
        using var repoDirectory = new TemporaryDirectory();
        using var externalDirectory = new TemporaryDirectory();

        var repoRoot = repoDirectory.RootPath;
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @owner\n");

        var externalFile = Path.Combine(externalDirectory.RootPath, "SpanBenchmark.cs");
        File.WriteAllText(externalFile, "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        Assert.NotNull(ciValues.CodeOwners);
        Assert.False(ciValues.TryGetCodeOwnersRelativePath(externalFile, false, out _));
    }

    [SkippableFact]
    public void KeepsSourceRootMatchWhenFallbackCannotResolve()
    {
        using var repoDirectory = new TemporaryDirectory();
        var repoRoot = repoDirectory.RootPath;
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @global\n");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        var externalRoot = Path.Combine(Path.GetTempPath(), "dd-ci-outside-" + Guid.NewGuid().ToString("N"));
        var sourceFile = Path.Combine(externalRoot, "tracer", "test", "Snapshots", "Snapshot.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(sourceFile, "class Snapshot {}");

        var relative = ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false);

        Assert.StartsWith("..", relative, StringComparison.Ordinal);
        Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out _));

        var owners = ciValues.CodeOwners!.Match("/" + relative).OrderBy(o => o).ToArray();
        Assert.Equal(new[] { "@global" }, owners);
    }

    [SkippableFact]
    public void UsesWorkspaceFallbackWhenSourceRootIsDifferent()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "tracer", "test", "benchmarks", "Benchmarks.Trace");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @global\n/tracer/test/benchmarks/Benchmarks.Trace/ @owner\n");
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var ciValues = new TestCIEnvironmentValues("/go/src/github.com/DataDog/apm-reliability/dd-trace-dotnet", repoRoot);
        var relative = ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false);

        Assert.Equal("tracer/test/benchmarks/Benchmarks.Trace/SpanBenchmark.cs", relative);

        Assert.True(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out var codeOwnersRelativePath));
        var owners = ciValues.CodeOwners!.Match("/" + codeOwnersRelativePath).OrderBy(o => o).ToArray();
        Assert.Equal(new[] { "@owner" }, owners);
    }

    [SkippableFact]
    public void DoesNotSearchOutsideWorkspaceForRelativeSourceFile()
    {
        using var repoDirectory = new TemporaryDirectory();
        using var outsideDirectory = new TemporaryDirectory();

        var repoRoot = repoDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var outsideRoot = outsideDirectory.RootPath;
        File.WriteAllText(Path.Combine(outsideRoot, "CODEOWNERS"), "* @owner\n");
        File.WriteAllText(Path.Combine(outsideRoot, "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var outsideFolderName = Path.GetFileName(outsideRoot);
        var relativeSourcePath = Path.Combine("..", outsideFolderName, "SpanBenchmark.cs");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        Assert.False(ciValues.TryGetCodeOwnersRelativePath(relativeSourcePath, false, out _));
        Assert.Null(ciValues.CodeOwners);
    }

    [SkippableFact]
    public void LockedCodeOwnersDoesNotBreakFallbackPathResolution()
    {
        Skip.If(Path.DirectorySeparatorChar != '\\', "FileShare.None deterministically blocks a second reader on Windows.");

        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var sourceDirectory = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(sourceDirectory);
        var sourceFile = Path.Combine(sourceDirectory, "Test.cs");
        File.WriteAllText(sourceFile, "class Test {}");
        var codeOwnersPath = Path.Combine(repoRoot, "CODEOWNERS");
        File.WriteAllText(codeOwnersPath, "*.cs @owner\n");
        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: repoRoot);
        var utcNow = DateTime.UtcNow;
        ciValues.CodeOwnersUtcNowProvider = () => utcNow;

        string[]? owners = null;
        using (new FileStream(codeOwnersPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Record.Exception(() => ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false, out owners!));

            Assert.Null(exception);
            Assert.Empty(owners!);
            Assert.Null(ciValues.CodeOwners);
        }

        // The file is readable again, but an unchanged failure is held during the backoff window.
        ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false, out owners!);
        Assert.Empty(owners);
        Assert.Null(ciValues.CodeOwners);

        utcNow += CIEnvironmentValues.CodeOwnersSearchRetryDelay;
        ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false, out owners!);
        Assert.Equal(["@owner"], owners);
        Assert.NotNull(ciValues.CodeOwners);
    }

    [SkippableFact]
    public void SecurityExceptionReadingFailureMetadataDoesNotEscape()
    {
        Skip.If(Path.DirectorySeparatorChar != '\\', "FileShare.None deterministically blocks a second reader on Windows.");

        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var sourceDirectory = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(sourceDirectory);
        var sourceFile = Path.Combine(sourceDirectory, "Test.cs");
        File.WriteAllText(sourceFile, string.Empty);
        var codeOwnersPath = Path.Combine(repoRoot, "CODEOWNERS");
        File.WriteAllText(codeOwnersPath, "*.cs @owner\n");
        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: repoRoot);
        var metadataReads = 0;
        ciValues.BeforeCodeOwnersFileMetadataRead = path =>
        {
            Assert.Equal(codeOwnersPath, path);
            metadataReads++;
            throw new SecurityException("Simulated restricted filesystem metadata access.");
        };

        string[]? owners = null;
        using (new FileStream(codeOwnersPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Record.Exception(() => ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false, out owners!));

            Assert.Null(exception);
            Assert.Empty(owners!);
            Assert.Null(ciValues.CodeOwners);
        }

        // Both failure caching and the workspace retry must handle restricted metadata access.
        Assert.Equal(2, metadataReads);
    }

    [SkippableFact]
    public void NegativeFallbackCacheExpiresAndDiscoversNewCodeOwners()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var sourceDirectory = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(sourceDirectory);
        var sourceFile = Path.Combine(sourceDirectory, "Test.cs");
        File.WriteAllText(sourceFile, string.Empty);
        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: repoRoot);
        var utcNow = DateTime.UtcNow;
        var searches = 0;
        ciValues.CodeOwnersUtcNowProvider = () => utcNow;
        ciValues.CodeOwnersFallbackSearchStarting = () => searches++;

        ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false, out var initialOwners);
        Assert.Empty(initialOwners);
        Assert.Equal(1, searches);

        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "*.cs @new-owner\n");
        ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false, out var cachedOwners);
        Assert.Empty(cachedOwners);
        Assert.Equal(1, searches);

        utcNow += CIEnvironmentValues.CodeOwnersSearchRetryDelay;
        ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false, out var discoveredOwners);

        Assert.Equal(["@new-owner"], discoveredOwners);
        Assert.Equal(2, searches);
        Assert.NotNull(ciValues.CodeOwners);
    }

    [SkippableFact]
    public void NegativeFallbackCacheIsSharedByRepositoryBoundaryBeyondItsCapacity()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: repoRoot);
        var searches = 0;
        ciValues.CodeOwnersFallbackSearchStarting = () => searches++;
        var sourceFiles = new List<string>();

        for (var i = 0; i <= 256; i++)
        {
            var sourceDirectory = Path.Combine(repoRoot, "project" + i, "src");
            Directory.CreateDirectory(sourceDirectory);
            var sourceFile = Path.Combine(sourceDirectory, "Test.cs");
            File.WriteAllText(sourceFile, string.Empty);
            sourceFiles.Add(sourceFile);
            Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out _));
        }

        foreach (var sourceFile in sourceFiles.AsEnumerable().Reverse())
        {
            Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out _));
        }

        Assert.Equal(1, searches);
    }

    [SkippableFact]
    public void ChangedCodeOwnersRetriesBeforeLoadFailureBackoffExpires()
    {
        Skip.If(Path.DirectorySeparatorChar != '\\', "FileShare.None deterministically blocks a second reader on Windows.");

        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var sourceDirectory = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(sourceDirectory);
        var sourceFile = Path.Combine(sourceDirectory, "Test.cs");
        File.WriteAllText(sourceFile, string.Empty);
        var codeOwnersPath = Path.Combine(repoRoot, "CODEOWNERS");
        File.WriteAllText(codeOwnersPath, "*.cs @old\n");
        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: repoRoot);
        var utcNow = DateTime.UtcNow;
        ciValues.CodeOwnersUtcNowProvider = () => utcNow;

        using (new FileStream(codeOwnersPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false, out var owners);
            Assert.Empty(owners);
        }

        File.WriteAllText(codeOwnersPath, "*.cs @replacement-owner\n");
        ciValues.MakeRelativePathFromSourceRootWithFallback(sourceFile, false, out var changedOwners);

        Assert.Equal(["@replacement-owner"], changedOwners);
        Assert.NotNull(ciValues.CodeOwners);
    }

    [SkippableFact]
    public void FallbackCacheEvictsOnlyTheOldestRepositoryAtCapacity()
    {
        using var tempDirectory = new TemporaryDirectory();
        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: null);
        var searches = 0;
        ciValues.CodeOwnersFallbackSearchStarting = () => searches++;
        var sourceFiles = new List<string>();

        for (var i = 0; i <= 256; i++)
        {
            var repository = Path.Combine(tempDirectory.RootPath, "repo" + i);
            Directory.CreateDirectory(Path.Combine(repository, ".git"));
            var sourceFile = Path.Combine(repository, "src", "Test.cs");
            sourceFiles.Add(sourceFile);
            Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out _));
        }

        Assert.Equal(257, searches);
        Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourceFiles[1], false, out _));
        Assert.True(searches == 257, "the second-oldest entry must survive a single FIFO eviction");

        Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourceFiles[0], false, out _));
        Assert.True(searches == 258, "only the oldest entry should have been evicted");
    }

    [SkippableFact]
    public void AnchorsForeignRelativePathsToCodeOwnersRoot()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "tracer", "test");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @global\n/tracer/test/ @owner\n");
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        // Paths recorded against a foreign base directory (e.g. "../../../_/..." on CI agents)
        // must be anchored back to a repository-relative path.
        var foreignRelativePath = "../../../_/tracer/test/SpanBenchmark.cs";
        Assert.True(ciValues.TryGetCodeOwnersRelativePath(foreignRelativePath, false, out var codeOwnersRelativePath));
        Assert.Equal("tracer/test/SpanBenchmark.cs", codeOwnersRelativePath);

        var owners = ciValues.CodeOwners!.Match("/" + codeOwnersRelativePath).OrderBy(o => o).ToArray();
        Assert.Equal(new[] { "@owner" }, owners);
    }

    [SkippableFact]
    public void AnchoredPathRespectsUseOSSeparator()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "tracer", "test");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @global\n");
        File.WriteAllText(Path.Combine(srcDir, "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);
        var foreignRelativePath = "../../../_/tracer/test/SpanBenchmark.cs";

        Assert.True(ciValues.TryGetCodeOwnersRelativePath(foreignRelativePath, useOSSeparator: false, out var forwardSlashPath));
        Assert.Equal("tracer/test/SpanBenchmark.cs", forwardSlashPath);

        Assert.True(ciValues.TryGetCodeOwnersRelativePath(foreignRelativePath, useOSSeparator: true, out var osPath));
        Assert.Equal(Path.Combine("tracer", "test", "SpanBenchmark.cs"), osPath);
    }

    [SkippableFact]
    public void MakeRelativePathFromSourceRootWithFallbackNormalizesForeignPrefixes()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "tracer", "test");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @global\n/tracer/test/ @owner\n");
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        var relative = ciValues.MakeRelativePathFromSourceRootWithFallback("../../../_/tracer/test/SpanBenchmark.cs", false);
        Assert.Equal("tracer/test/SpanBenchmark.cs", relative);
    }

    [SkippableFact]
    public void DoesNotAnchorForeignPathsWhenSuffixDoesNotExistUnderRoot()
    {
        using var repoDirectory = new TemporaryDirectory();
        using var externalDirectory = new TemporaryDirectory();

        var repoRoot = repoDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, "src"));
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @owner\n/src/ @src-owner\n");
        File.WriteAllText(Path.Combine(repoRoot, "src", "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var externalFile = Path.Combine(externalDirectory.RootPath, "other", "SpanBenchmark.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(externalFile)!);
        File.WriteAllText(externalFile, "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        // A foreign path whose suffix does not exist under the repository must not be re-anchored.
        Assert.False(ciValues.TryGetCodeOwnersRelativePath("../other/SpanBenchmark.cs", false, out _));
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
        File.WriteAllText(Path.Combine(repoRoot, ".github", "CODEOWNERS"), "/tracer/test/ @DataDog/tracing-dotnet\n");
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
        Assert.NotNull(ciValues.CodeOwners);

        var foreignRelativePath = "../../../../../../_/tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs";
        var relative = ciValues.MakeRelativePathFromSourceRootWithFallback(foreignRelativePath, false);
        Assert.Equal("tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs", relative);

        var owners = ciValues.CodeOwners!.Match("/" + relative).OrderBy(o => o).ToArray();
        Assert.Equal(new[] { "@DataDog/tracing-dotnet" }, owners);
    }

    [SkippableFact]
    public void DoesNotAnchorPathsWithInteriorNavigationSegments()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @owner\n/src/ @src-owner\n");
        File.WriteAllText(Path.Combine(srcDir, "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        // An interior navigation segment depends on the unknown base directory where the path was
        // recorded; anchoring it would produce a malformed repository-relative path.
        Assert.False(ciValues.TryGetCodeOwnersRelativePath("../other/src/../src/SpanBenchmark.cs", false, out _));
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
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @owner\n/src/ @src-owner\n");
        File.WriteAllText(Path.Combine(srcDir, "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourceFilePath, false, out _));
    }

    [SkippableFact]
    public void DoesNotAnchorEmbeddedWindowsAbsolutePathOutsideRoot()
    {
        Skip.If(Path.DirectorySeparatorChar != '\\', "This regression exercises Windows drive-rooted Path.Combine behavior.");

        using var repoDirectory = new TemporaryDirectory();
        using var externalDirectory = new TemporaryDirectory();
        var repoRoot = repoDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, "src"));
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @owner\n/src/ @src-owner\n");
        File.WriteAllText(Path.Combine(repoRoot, "src", "SpanBenchmark.cs"), "class SpanBenchmark {}");

        var externalFile = Path.Combine(externalDirectory.RootPath, "src", "SpanBenchmark.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(externalFile)!);
        File.WriteAllText(externalFile, "class ExternalSpanBenchmark {}");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);
        var embeddedAbsolutePath = "../../" + externalFile.Replace('\\', '/');

        Assert.False(ciValues.TryGetCodeOwnersRelativePath(embeddedAbsolutePath, false, out _));
    }

    [SkippableFact]
    public void ConcurrentFallbackPublishesCodeOwnersAndRootTogether()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var srcDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @owner\n/src/ @src-owner\n");
        File.WriteAllText(sourceFile, "class SpanBenchmark {}");

        var ciValues = new TestCIEnvironmentValues(sourceRoot: null, workspacePath: repoRoot);
        const int concurrency = 64;
        var results = new bool[concurrency];
        var tasks = new Task[concurrency];
        using var start = new ManualResetEventSlim(initialState: false);

        for (var i = 0; i < concurrency; i++)
        {
            var index = i;
            tasks[index] = Task.Run(() =>
            {
                Assert.True(start.Wait(TestTimeout), "concurrent fallback start signal was not received");
                results[index] = ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out var relativePath) &&
                                 relativePath == "src/SpanBenchmark.cs";
            });
        }

        start.Set();
        Assert.True(Task.WaitAll(tasks, TestTimeout), "concurrent fallback lookup must not deadlock");

        Assert.All(results, Assert.True);
        Assert.NotNull(ciValues.CodeOwners);
        Assert.Equal(repoRoot, ciValues.CodeOwnersRoot);
    }

    [SkippableFact]
    public void RelativePathAndOwnersUseTheSameSnapshotAcrossControlledReload()
    {
        using var firstRepository = new TemporaryDirectory();
        using var secondRepository = new TemporaryDirectory();
        var firstSourceDirectory = Path.Combine(firstRepository.RootPath, "layoutA", "src");
        var secondSourceDirectory = Path.Combine(secondRepository.RootPath, "src");
        Directory.CreateDirectory(firstSourceDirectory);
        Directory.CreateDirectory(secondSourceDirectory);
        File.WriteAllText(Path.Combine(firstSourceDirectory, "SpanBenchmark.cs"), string.Empty);
        File.WriteAllText(Path.Combine(secondSourceDirectory, "SpanBenchmark.cs"), string.Empty);
        File.WriteAllText(Path.Combine(firstRepository.RootPath, "CODEOWNERS"), "* @first-global\n/layoutA/src/ @first\n");
        File.WriteAllText(Path.Combine(secondRepository.RootPath, "CODEOWNERS"), "* @second-global\n/src/ @second\n");

        using var setupEntered = new ManualResetEventSlim(initialState: false);
        using var continueSetup = new ManualResetEventSlim(initialState: false);
        using var reloadWaitReached = new ManualResetEventSlim(initialState: false);
        var ciValues = new BlockingReloadCIEnvironmentValues(firstRepository.RootPath);
        ciValues.BeforeCodeOwnersReloadWait = reloadWaitReached.Set;
        ciValues.Reload();
        const string foreignSourcePath = "../../layoutA/src/SpanBenchmark.cs";

        var firstRelativePath = ciValues.MakeRelativePathFromSourceRootWithFallback(foreignSourcePath, false, out var firstOwners);
        Assert.Equal("layoutA/src/SpanBenchmark.cs", firstRelativePath);
        Assert.Equal(["@first"], firstOwners);

        ciValues.PrepareBlockedReload(secondRepository.RootPath, setupEntered, continueSetup);
        var reloadTask = Task.Run(ciValues.Reload);
        Task? matchTask = null;
        string? secondRelativePath = null;
        string[]? secondOwners = null;
        try
        {
            Assert.True(setupEntered.Wait(TestTimeout), "reload did not enter its controlled Setup phase");
            matchTask = Task.Run(() => secondRelativePath = ciValues.MakeRelativePathFromSourceRootWithFallback(foreignSourcePath, false, out secondOwners));
            Assert.True(reloadWaitReached.Wait(TestTimeout), "snapshot reader did not reach the active-reload wait");
        }
        finally
        {
            continueSetup.Set();
        }

        Assert.True(reloadTask.Wait(TestTimeout), "reload must complete after Setup is released");
        Assert.NotNull(matchTask);
        Assert.True(matchTask!.Wait(TestTimeout), "snapshot reader must not deadlock after reload");
        Assert.Equal("src/SpanBenchmark.cs", secondRelativePath);
        Assert.Equal(["@second"], secondOwners);
    }

    [SkippableFact]
    public void RelativePathUsesCompletedReloadStateWhenNewRepositoryHasNoCodeOwners()
    {
        using var firstRepository = new TemporaryDirectory();
        using var secondRepository = new TemporaryDirectory();
        var firstSourceDirectory = Path.Combine(firstRepository.RootPath, "src");
        var secondSourceDirectory = Path.Combine(secondRepository.RootPath, "src");
        Directory.CreateDirectory(firstSourceDirectory);
        Directory.CreateDirectory(secondSourceDirectory);
        File.WriteAllText(Path.Combine(firstRepository.RootPath, "CODEOWNERS"), "* @first\n");
        File.WriteAllText(Path.Combine(firstSourceDirectory, "SpanBenchmark.cs"), string.Empty);
        var secondSource = Path.Combine(secondSourceDirectory, "SpanBenchmark.cs");
        File.WriteAllText(secondSource, string.Empty);

        using var setupEntered = new ManualResetEventSlim(initialState: false);
        using var continueSetup = new ManualResetEventSlim(initialState: false);
        using var reloadWaitReached = new ManualResetEventSlim(initialState: false);
        var ciValues = new BlockingReloadCIEnvironmentValues(firstRepository.RootPath);
        ciValues.BeforeCodeOwnersReloadWait = reloadWaitReached.Set;
        ciValues.Reload();

        ciValues.PrepareBlockedReload(secondRepository.RootPath, setupEntered, continueSetup);
        var reloadTask = Task.Run(ciValues.Reload);
        Task? matchTask = null;
        string? relativePath = null;
        string[]? owners = null;
        try
        {
            Assert.True(setupEntered.Wait(TestTimeout), "reload did not enter its controlled Setup phase");
            matchTask = Task.Run(() => relativePath = ciValues.MakeRelativePathFromSourceRootWithFallback(secondSource, false, out owners));
            Assert.True(reloadWaitReached.Wait(TestTimeout), "snapshot reader did not reach the active-reload wait");
        }
        finally
        {
            continueSetup.Set();
        }

        Assert.True(reloadTask.Wait(TestTimeout), "reload must complete after Setup is released");
        Assert.NotNull(matchTask);
        Assert.True(matchTask!.Wait(TestTimeout), "snapshot reader must not deadlock after reload");
        Assert.Equal("src/SpanBenchmark.cs", relativePath);
        Assert.Empty(owners!);
        Assert.Null(ciValues.CodeOwners);
    }

    [SkippableFact]
    public void MalformedGitLabClassDoesNotAbortFallbackPublication()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var sourceDirectory = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(sourceDirectory);
        var sourceFile = Path.Combine(sourceDirectory, "file.txt");
        File.WriteAllText(sourceFile, string.Empty);
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @fallback\nfile[z-a].txt @invalid\n");

        var ciValues = new TestGitlabEnvironmentValues(sourceRoot: null, workspacePath: repoRoot);

        Assert.True(ciValues.TryGetCodeOwnersRelativePath(sourceFile, false, out var relativePath));
        Assert.Equal("src/file.txt", relativePath);
        Assert.Equal(["@fallback"], ciValues.CodeOwners!.Match("/" + relativePath));
        Assert.Equal(repoRoot, ciValues.CodeOwnersRoot);
    }

    [SkippableFact]
    public void ReloadAndFallbackDiscoveryAreSerialized()
    {
        using var firstRepository = new TemporaryDirectory();
        using var secondRepository = new TemporaryDirectory();
        var firstSource = CreateRepository(firstRepository.RootPath, "@first");
        var secondSource = CreateRepository(secondRepository.RootPath, "@second");
        using var setupEntered = new ManualResetEventSlim(initialState: false);
        using var continueSetup = new ManualResetEventSlim(initialState: false);
        using var fallbackLockReached = new ManualResetEventSlim(initialState: false);

        var ciValues = new BlockingReloadCIEnvironmentValues(firstRepository.RootPath);
        ciValues.BeforeCodeOwnersFallbackLock = fallbackLockReached.Set;
        ciValues.Reload();
        Assert.True(ciValues.TryGetCodeOwnersRelativePath(firstSource, false, out _));

        ciValues.PrepareBlockedReload(secondRepository.RootPath, setupEntered, continueSetup);
        var reloadTask = Task.Run(ciValues.Reload);
        Task? fallbackTask = null;
        var fallbackResult = false;
        try
        {
            Assert.True(setupEntered.Wait(TestTimeout), "reload did not enter its controlled Setup phase");
            fallbackTask = Task.Run(() =>
            {
                fallbackResult = ciValues.TryGetCodeOwnersRelativePath(secondSource, false, out var relativePath) &&
                                 relativePath == "src/SpanBenchmark.cs";
            });
            Assert.True(fallbackLockReached.Wait(TestTimeout), "fallback lookup did not reach the serialized discovery lock");
        }
        finally
        {
            continueSetup.Set();
        }

        Assert.True(reloadTask.Wait(TestTimeout), "reload must complete after Setup is released");
        Assert.NotNull(fallbackTask);
        Assert.True(fallbackTask!.Wait(TestTimeout), "fallback lookup must not deadlock after reload");

        Assert.True(fallbackResult);
        Assert.Equal(secondRepository.RootPath, ciValues.CodeOwnersRoot);
        Assert.Equal(["@second"], ciValues.CodeOwners!.Match("/src/SpanBenchmark.cs"));

        static string CreateRepository(string root, string owner)
        {
            var sourceDirectory = Path.Combine(root, "src");
            Directory.CreateDirectory(sourceDirectory);
            File.WriteAllText(Path.Combine(root, "CODEOWNERS"), "* @global\n/src/ " + owner + "\n");
            var sourceFile = Path.Combine(sourceDirectory, "SpanBenchmark.cs");
            File.WriteAllText(sourceFile, "class SpanBenchmark {}");
            return sourceFile;
        }
    }

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
        public TestCIEnvironmentValues(string? sourceRoot, string? workspacePath)
        {
            SourceRoot = sourceRoot;
            WorkspacePath = workspacePath;
        }

        protected override void Setup(IGitInfo gitInfo)
        {
        }
    }

    private sealed class TestGitlabEnvironmentValues : CIEnvironmentValues
    {
        public TestGitlabEnvironmentValues(string? sourceRoot, string? workspacePath)
        {
            SourceRoot = sourceRoot;
            WorkspacePath = workspacePath;
        }

        protected override void Setup(IGitInfo gitInfo)
        {
        }
    }

    private abstract class ReloadingEnvironmentValues : CIEnvironmentValues
    {
        private readonly string _sourceRoot;

        protected ReloadingEnvironmentValues(string sourceRoot)
        {
            _sourceRoot = sourceRoot;
        }

        public void Reload() => ReloadEnvironmentData();

        protected override void Setup(IGitInfo gitInfo)
        {
            SourceRoot = _sourceRoot;
            WorkspacePath = _sourceRoot;
        }
    }

    private sealed class ReloadingGithubEnvironmentValues : ReloadingEnvironmentValues
    {
        public ReloadingGithubEnvironmentValues(string sourceRoot)
            : base(sourceRoot)
        {
        }
    }

    private sealed class ReloadingGitlabEnvironmentValues : ReloadingEnvironmentValues
    {
        public ReloadingGitlabEnvironmentValues(string sourceRoot)
            : base(sourceRoot)
        {
        }
    }

    private sealed class BlockingReloadCIEnvironmentValues : CIEnvironmentValues
    {
        private string _nextSourceRoot;
        private ManualResetEventSlim? _setupEntered;
        private ManualResetEventSlim? _continueSetup;

        public BlockingReloadCIEnvironmentValues(string sourceRoot)
        {
            _nextSourceRoot = sourceRoot;
        }

        public void Reload() => ReloadEnvironmentData();

        public void PrepareBlockedReload(string sourceRoot, ManualResetEventSlim setupEntered, ManualResetEventSlim continueSetup)
        {
            _nextSourceRoot = sourceRoot;
            _setupEntered = setupEntered;
            _continueSetup = continueSetup;
        }

        protected override void Setup(IGitInfo gitInfo)
        {
            _setupEntered?.Set();
            if (_continueSetup is not null && !_continueSetup.Wait(TestTimeout))
            {
                throw new TimeoutException("Controlled reload was not released by the test.");
            }

            SourceRoot = _nextSourceRoot;
            WorkspacePath = _nextSourceRoot;
        }
    }
}
