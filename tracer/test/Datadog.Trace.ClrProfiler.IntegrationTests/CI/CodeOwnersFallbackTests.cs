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
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
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
                [CIEnvironmentValues.Constants.GitHubSha] = CommitSha,
                [CIEnvironmentValues.Constants.GitHubWorkspace] = Path.Combine(repoRoot, "other"),
                [CIEnvironmentValues.Constants.GitHubRepository] = "DataDog/dd-trace-dotnet",
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
            Directory.CreateDirectory(srcDir);
            var sourceFile = Path.Combine(srcDir, "SpanBenchmark.cs");
            File.WriteAllText(Path.Combine(repoRoot, "CODEOWNERS"), "* @global\n/src/ @owner\n");
            File.WriteAllText(sourceFile, "class SpanBenchmark {}");

            var env = new Dictionary<string, string>
            {
                [CIEnvironmentValues.Constants.GitHubSha] = CommitSha,
                [CIEnvironmentValues.Constants.GitHubWorkspace] = srcDir,
                [CIEnvironmentValues.Constants.GitHubRepository] = "DataDog/dd-trace-dotnet",
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
                [CIEnvironmentValues.Constants.GitHubSha] = CommitSha,
                [CIEnvironmentValues.Constants.GitHubRepository] = "DataDog/dd-trace-dotnet",
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
                [CIEnvironmentValues.Constants.GitHubSha] = CommitSha,
                [CIEnvironmentValues.Constants.GitHubWorkspace] = otherRoot,
                [CIEnvironmentValues.Constants.GitHubRepository] = "DataDog/dd-trace-dotnet",
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
                [CIEnvironmentValues.Constants.GitHubSha] = CommitSha,
                [CIEnvironmentValues.Constants.GitHubWorkspace] = repoRoot,
                [CIEnvironmentValues.Constants.GitHubRepository] = "DataDog/dd-trace-dotnet",
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
                [CIEnvironmentValues.Constants.GitHubSha] = CommitSha,
                [CIEnvironmentValues.Constants.GitHubWorkspace] = repoRoot,
                [CIEnvironmentValues.Constants.GitHubRepository] = "DataDog/dd-trace-dotnet",
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
                [CIEnvironmentValues.Constants.GitHubSha] = CommitSha,
                [CIEnvironmentValues.Constants.GitHubWorkspace] = repoRoot,
                [CIEnvironmentValues.Constants.GitHubRepository] = "DataDog/dd-trace-dotnet",
            };

            var ciValues = CIEnvironmentValues.Create(env);

            Assert.False(ciValues.TryGetCodeOwnersRelativePath(relativeSourcePath, false, out _));
            Assert.Null(ciValues.CodeOwners);
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
    }
}
