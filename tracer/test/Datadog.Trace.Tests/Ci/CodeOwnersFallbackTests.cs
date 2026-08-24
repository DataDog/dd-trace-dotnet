// <copyright file="CodeOwnersFallbackTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(EnvironmentVariablesTestCollection))]
public class CodeOwnersFallbackTests
{
    private const string CommitSha = "3245605c3d1edc67226d725799ee969c71f7632b";

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
    public void GitHubProviderUsesOfficialCodeOwnersLocationPriority()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".github"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs"));
        File.WriteAllText(Path.Combine(repoRoot, ".github", "CODEOWNERS"), "* @github-directory\n");
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @repository-root\n");
        File.WriteAllText(Path.Combine(repoRoot, "docs", "CODEOWNERS"), "* @docs-directory\n");
        var ciValues = new ReloadingEnvironmentValues(repoRoot, "github");

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
    public void GitLabProviderUsesOfficialCodeOwnersLocationPriority()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".gitlab"));
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @repository-root\n");
        File.WriteAllText(Path.Combine(repoRoot, "docs", "CODEOWNERS"), "* @docs-directory\n");
        File.WriteAllText(Path.Combine(repoRoot, ".gitlab", "CODEOWNERS"), "* @gitlab-directory\n");
        var ciValues = new ReloadingEnvironmentValues(repoRoot, "gitlab");

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
        var githubValues = new ReloadingEnvironmentValues(githubDirectory.RootPath, "github");

        githubValues.Reload();
        Assert.Null(githubValues.CodeOwners);

        using var gitlabDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(gitlabDirectory.RootPath, ".github"));
        File.WriteAllText(Path.Combine(gitlabDirectory.RootPath, ".github", "CODEOWNERS"), "* @github-only\n");
        var gitlabValues = new ReloadingEnvironmentValues(gitlabDirectory.RootPath, "gitlab");

        gitlabValues.Reload();
        Assert.Null(gitlabValues.CodeOwners);
    }

    [SkippableTheory]
    [InlineData("https://gitlab.com/DataDog/dd-trace-dotnet.git")]
    [InlineData("git@gitlab.com:DataDog/dd-trace-dotnet.git")]
    [InlineData("https://gitlab.example.com/DataDog/dd-trace-dotnet.git")]
    [InlineData("git@gitlab.example.com:DataDog/dd-trace-dotnet.git")]
    public void UsesRepositoryHostToSelectCodeOwnersPlatform(string repositoryUrl)
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".gitlab"));
        File.WriteAllText(Path.Combine(repoRoot, ".gitlab", "CODEOWNERS"), "[Section] @gitlab-owner\n*.cs\n");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Jenkins.Url] = "https://jenkins.example.com",
            [PlatformKeys.Ci.Jenkins.GitUrl] = repositoryUrl,
            [PlatformKeys.Ci.Jenkins.GitCommit] = CommitSha,
            [PlatformKeys.Ci.Jenkins.Workspace] = repoRoot,
        };

        var ciValues = CIEnvironmentValues.Create(env);

        Assert.Equal("jenkins", ciValues.Provider);
        Assert.Equal(["@gitlab-owner"], ciValues.CodeOwners!.Match("/file.cs"));
    }

    [SkippableFact]
    public void UsesGitLabSpecificLocationWhenRepositoryHostIsUnknown()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".gitlab"));
        File.WriteAllText(Path.Combine(repoRoot, ".gitlab", "CODEOWNERS"), "[Section] @gitlab-owner\n*.cs\n");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Jenkins.Url] = "https://jenkins.example.com",
            [PlatformKeys.Ci.Jenkins.GitUrl] = "https://source.example.com/DataDog/dd-trace-dotnet.git",
            [PlatformKeys.Ci.Jenkins.GitCommit] = CommitSha,
            [PlatformKeys.Ci.Jenkins.Workspace] = repoRoot,
        };

        var ciValues = CIEnvironmentValues.Create(env);

        Assert.Equal(["@gitlab-owner"], ciValues.CodeOwners!.Match("/file.cs"));
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

    [SkippableTheory]
    [InlineData(@"D:\a\_work\1\s\tracer\test\Datadog.Trace.DuckTyping.Tests\ExceptionsTests.cs", "tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs")]
    [InlineData(@"D:\a\1\s\tracer\test\Datadog.Trace.DuckTyping.Tests\ExceptionsTests.cs", "tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs")]
    [InlineData("/home/vsts/work/1/s/tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs", "tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs")]
    [InlineData(@"D:\a\1\s\Program.cs", "Program.cs")]
    public void AnchorsAzurePipelinesCompilerPathsFromAnotherOperatingSystem(string compilerPath, string expectedRelativePath)
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var sourceFile = Path.Combine(repoRoot, expectedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".github"));
        File.WriteAllText(Path.Combine(repoRoot, ".github", "CODEOWNERS"), $"/{expectedRelativePath} @DataDog/tracing-dotnet\n");
        File.WriteAllText(sourceFile, "// test");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Azure.TFBuild] = "True",
            [PlatformKeys.Ci.Azure.BuildSourcesDirectory] = repoRoot,
            [PlatformKeys.Ci.Azure.BuildSourceVersion] = CommitSha,
            [PlatformKeys.Ci.Azure.BuildRepositoryUri] = "https://github.com/DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);
        var relative = ciValues.MakeRelativePathFromSourceRootWithFallback(compilerPath, false);

        Assert.Equal(expectedRelativePath, relative);
        Assert.Equal(["@DataDog/tracing-dotnet"], ciValues.CodeOwners!.Match("/" + relative));
    }

    [SkippableFact]
    public void DoesNotAnchorAzureStyleAbsolutePathsForOtherProviders()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var sourceDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "/src/ @owner\n");
        File.WriteAllText(Path.Combine(sourceDir, "SpanBenchmark.cs"), "// test");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.GitHub.Sha] = CommitSha,
            [PlatformKeys.Ci.GitHub.Workspace] = repoRoot,
            [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        Assert.False(ciValues.TryGetCodeOwnersRelativePath(@"D:\a\_work\1\s\src\SpanBenchmark.cs", false, out _));
    }

    [SkippableTheory]
    [InlineData("file:///D:/a/_work/1/s/src/SpanBenchmark.cs")]
    [InlineData("https://example.com/a/_work/1/s/src/SpanBenchmark.cs")]
    public void DoesNotAnchorUrisThatResembleAzureCheckoutPaths(string sourcePath)
    {
        using var tempDirectory = new TemporaryDirectory();
        var repoRoot = tempDirectory.RootPath;
        var sourceDir = Path.Combine(repoRoot, "src");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "/src/ @owner\n");
        File.WriteAllText(Path.Combine(sourceDir, "SpanBenchmark.cs"), "// test");

        var env = new Dictionary<string, string>
        {
            [PlatformKeys.Ci.Azure.TFBuild] = "True",
            [PlatformKeys.Ci.Azure.BuildSourcesDirectory] = repoRoot,
            [PlatformKeys.Ci.Azure.BuildSourceVersion] = CommitSha,
            [PlatformKeys.Ci.Azure.BuildRepositoryUri] = "https://github.com/DataDog/dd-trace-dotnet",
        };

        var ciValues = CIEnvironmentValues.Create(env);

        Assert.False(ciValues.TryGetCodeOwnersRelativePath(sourcePath, false, out _));
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
        public TestCIEnvironmentValues(string? sourceRoot, string? workspacePath, string? provider = null)
        {
            SourceRoot = sourceRoot;
            WorkspacePath = workspacePath;
            Provider = provider;
        }

        protected override void Setup(IGitInfo gitInfo)
        {
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
