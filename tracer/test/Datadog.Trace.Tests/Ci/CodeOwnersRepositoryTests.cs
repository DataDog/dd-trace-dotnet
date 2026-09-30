// <copyright file="CodeOwnersRepositoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.Ci.CodeOwnership;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Ci;

public class CodeOwnersRepositoryTests
{
    private readonly ITestOutputHelper _output;

    public CodeOwnersRepositoryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableTheory]
    [InlineData("tracer/test")]
    [InlineData("profiler/test")]
    public void EveryTrackedCSharpTestFileHasAnOwner(string testRoot)
        => ValidateTestFileOwnership(testRoot, _output);

    [SkippableTheory]
    [InlineData("/docs/development/AzureFunctions.md", new[] { "@DataDog/tracing-dotnet", "@DataDog/apm-serverless", "@DataDog/serverless-azure-and-gcp" })]
    // `**/` must also match zero directories: a file directly under tracer/test/ still matches /tracer/test/**/*Lambda*
    [InlineData("/tracer/test/FooLambdaTests.cs", new[] { "@DataDog/tracing-dotnet", "@DataDog/apm-serverless", "@DataDog/serverless-aws" })]
    // Rooted patterns must match paths passed without a leading slash too
    [InlineData("tracer/src/Datadog.Trace/Ci/CodeOwnership/CodeOwners.cs", new[] { "@DataDog/ci-app-libraries-dotnet", "@DataDog/apm-dotnet" })]
    [InlineData("/tracer/src/Datadog.Trace/Ci/CodeOwnership/CodeOwners.cs", new[] { "@DataDog/ci-app-libraries-dotnet", "@DataDog/apm-dotnet" })]
    public void RepositoryCodeOwnersMatchesExpectedTeams(string path, string[] expected)
    {
        var repoRoot = GetRepositoryRoot();
        Skip.If(repoRoot is null, "Could not locate the repository root");

        var codeOwners = new CodeOwners(Path.Combine(repoRoot!, ".github", "CODEOWNERS"), CodeOwners.Dialect.GitHub);
        codeOwners.Match(path).OrderBy(o => o).Should().Equal(expected.OrderBy(o => o));
    }

    private static void ValidateTestFileOwnership(string testRoot, ITestOutputHelper output)
    {
        var repoRoot = GetRepositoryRoot();
        Skip.If(repoRoot is null, "Could not locate the repository root");

        var codeOwners = new CodeOwners(Path.Combine(repoRoot!, ".github", "CODEOWNERS"), CodeOwners.Dialect.GitHub);
        var trackedFiles = GetTrackedCSharpFiles(repoRoot!, testRoot).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var unownedFiles = new List<string>();
        var report = new StringBuilder();
        report.Append("CODEOWNERS report for ").AppendLine(testRoot);
        report.Append("Tracked C# files: ").Append(trackedFiles.Length).AppendLine();
        report.AppendLine();

        foreach (var relativePath in trackedFiles)
        {
            var owners = codeOwners.Match("/" + relativePath).OrderBy(owner => owner, StringComparer.Ordinal).ToArray();
            if (owners.Length == 0)
            {
                unownedFiles.Add(relativePath);
            }

            report.Append(relativePath)
                  .Append(" => ")
                  .AppendLine(owners.Length == 0 ? "<none>" : string.Join(", ", owners));
        }

        var reportPath = Path.GetTempFileName();
        var reportContents = report.ToString();
        File.WriteAllText(reportPath, reportContents, Encoding.UTF8);

        if (trackedFiles.Length == 0 || unownedFiles.Count > 0)
        {
            output.WriteLine($"Full CODEOWNERS report: {reportPath}");
            output.WriteLine(reportContents);
        }

        trackedFiles.Should().NotBeEmpty("expected to find tracked C# test files in the repository");
        unownedFiles.Should().BeEmpty("every tracked C# test file should be owned by at least one team in .github/CODEOWNERS");
    }

    private static string? GetRepositoryRoot()
    {
        // The solution directory is the repository root in this repo, but walk up as a safety net
        // in case the solution is ever moved into a subdirectory.
        var current = new DirectoryInfo(EnvironmentTools.GetSolutionDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".github", "CODEOWNERS")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static IEnumerable<string> GetTrackedCSharpFiles(string repositoryRoot, string testRoot)
    {
        var git = ProcessHelpers.RunCommand(
            new ProcessHelpers.Command(
                "git",
                $"-c safe.directory=* ls-files -z -- {testRoot}",
                repositoryRoot,
                outputEncoding: Encoding.UTF8,
                errorEncoding: Encoding.UTF8,
                useWhereIsIfFileNotFound: true,
                timeout: TimeSpan.FromSeconds(30)));

        git.Should().NotBeNull("git is required to enumerate the repository files");
        git!.ExitCode.Should().Be(0, git.Error);

        return git.Output
                  .Split(['\0'], StringSplitOptions.RemoveEmptyEntries)
                  .Where(path => string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase));
    }
}
