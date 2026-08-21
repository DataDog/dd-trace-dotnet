// <copyright file="CodeOwnersRepositoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class CodeOwnersRepositoryTests
{
    [SkippableFact]
    public void EveryTestFileHasAnOwner()
    {
        var repoRoot = GetRepositoryRoot();
        Skip.If(repoRoot is null, "Could not locate the repository root");

        var codeOwners = new CodeOwners(Path.Combine(repoRoot!, ".github", "CODEOWNERS"), CodeOwners.Platform.GitHub);
        var unownedFiles = new List<string>();
        var totalFiles = 0;

        foreach (var testRoot in new[] { "tracer/test", "profiler/test" })
        {
            var fullRoot = Path.Combine(repoRoot!, testRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(repoRoot!.Length + 1).Replace('\\', '/');
                totalFiles++;
                if (!codeOwners.Match("/" + relativePath).Any())
                {
                    unownedFiles.Add(relativePath);
                }
            }
        }

        totalFiles.Should().BeGreaterThan(0, "expected to find test files in the repository");
        unownedFiles.Should().BeEmpty("every test file should be owned by at least one team in .github/CODEOWNERS");
    }

    [SkippableTheory]
    [InlineData("/docs/development/AzureFunctions.md", new[] { "@DataDog/tracing-dotnet", "@DataDog/apm-serverless", "@DataDog/serverless-azure-and-gcp" })]
    // `**/` must also match zero directories: a file directly under tracer/test/ still matches /tracer/test/**/*Lambda*
    [InlineData("/tracer/test/FooLambdaTests.cs", new[] { "@DataDog/tracing-dotnet", "@DataDog/apm-serverless", "@DataDog/serverless-aws" })]
    // Rooted patterns must match paths passed without a leading slash too
    [InlineData("tracer/src/Datadog.Trace/Ci/CodeOwners.cs", new[] { "@DataDog/ci-app-libraries-dotnet", "@DataDog/apm-dotnet" })]
    [InlineData("/tracer/src/Datadog.Trace/Ci/CodeOwners.cs", new[] { "@DataDog/ci-app-libraries-dotnet", "@DataDog/apm-dotnet" })]
    public void RepositoryCodeOwnersMatchesExpectedTeams(string path, string[] expected)
    {
        var repoRoot = GetRepositoryRoot();
        Skip.If(repoRoot is null, "Could not locate the repository root");

        var codeOwners = new CodeOwners(Path.Combine(repoRoot!, ".github", "CODEOWNERS"), CodeOwners.Platform.GitHub);
        codeOwners.Match(path).OrderBy(o => o).Should().Equal(expected.OrderBy(o => o));
    }

    [SkippableFact]
    public void SectionDefaultOwnersDoNotApplyToUnmatchedPaths()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "dd-codeowners-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Owners listed on a section header line are not a catch-all rule for every other path.
            File.WriteAllText(filePath, "[Section] @team\n/src/ @owner\n");
            var codeOwners = new CodeOwners(filePath, CodeOwners.Platform.GitLab);

            codeOwners.Match("/src/code.cs").Should().Equal(["@owner"]);
            codeOwners.Match("/other/file.cs").Should().BeEmpty();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [SkippableFact]
    public void GitLabExclusionRuleRemovesPathFromSection()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "dd-codeowners-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(filePath, "* @global\n/docs/ @docs\n!/docs/generated/\n");
            var codeOwners = new CodeOwners(filePath, CodeOwners.Platform.GitLab);

            codeOwners.Match("/docs/a.cs").Should().Equal(["@docs"]);
            codeOwners.Match("/docs/generated/x.cs").Should().BeEmpty();
            codeOwners.Match("/other/file.cs").Should().Equal(["@global"]);
        }
        finally
        {
            File.Delete(filePath);
        }
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
}
