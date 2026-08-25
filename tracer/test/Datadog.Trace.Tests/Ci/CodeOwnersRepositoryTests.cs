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
using System.Threading.Tasks;
using Datadog.Trace.Ci.CodeOwnership;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[UsesVerify]
public class CodeOwnersRepositoryTests
{
    private const int MaxListedFiles = 5;
    private static readonly HashSet<string> BuildOutputDirectories = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj" };

    public CodeOwnersRepositoryTests()
    {
        VerifyHelper.InitializeGlobalSettings();
    }

    [SkippableFact]
    public Task TracerTestFilesHaveExpectedOwners()
        => VerifyTestFileOwnership("tracer/test");

    [SkippableFact]
    public Task ProfilerTestFilesHaveExpectedOwners()
        => VerifyTestFileOwnership("profiler/test");

    [SkippableFact]
    public void RepositorySourceFileEnumerationSkipsNonCSharpFilesAndBuildOutputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "dd-codeowners-enumeration-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src", "nested"));
            Directory.CreateDirectory(Path.Combine(root, "bin", "Debug"));
            Directory.CreateDirectory(Path.Combine(root, "src", "obj", "Debug"));
            File.WriteAllText(Path.Combine(root, "Source.cs"), string.Empty);
            File.WriteAllText(Path.Combine(root, "src", "nested", "Nested.cs"), string.Empty);
            File.WriteAllText(Path.Combine(root, "src", "nested", "README.md"), string.Empty);
            File.WriteAllText(Path.Combine(root, "bin", "Debug", "Generated.dll"), string.Empty);
            File.WriteAllText(Path.Combine(root, "src", "obj", "Debug", "Generated.cs"), string.Empty);

            EnumerateRepositoryCSharpFiles(root)
               .Select(path => path.Substring(root.Length + 1).Replace('\\', '/'))
               .Should()
               .BeEquivalentTo(["Source.cs", "src/nested/Nested.cs"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

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

    private static async Task VerifyTestFileOwnership(string testRoot)
    {
        var repoRoot = GetRepositoryRoot();
        Skip.If(repoRoot is null, "Could not locate the repository root");

        var codeOwners = new CodeOwners(Path.Combine(repoRoot!, ".github", "CODEOWNERS"), CodeOwners.Dialect.GitHub);
        var unownedFiles = new List<string>();
        var filesByOwnerSet = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var fullRoot = Path.Combine(repoRoot!, testRoot.Replace('/', Path.DirectorySeparatorChar));

        if (Directory.Exists(fullRoot))
        {
            foreach (var file in EnumerateRepositoryCSharpFiles(fullRoot).OrderBy(path => path, StringComparer.Ordinal))
            {
                var relativePath = file.Substring(repoRoot!.Length + 1).Replace('\\', '/');
                var owners = codeOwners.Match("/" + relativePath).OrderBy(owner => owner, StringComparer.Ordinal).ToArray();
                if (owners.Length == 0)
                {
                    unownedFiles.Add(relativePath);
                }

                var ownerSet = owners.Length == 0 ? "<none>" : string.Join(", ", owners);
                if (!filesByOwnerSet.TryGetValue(ownerSet, out var files))
                {
                    files = [];
                    filesByOwnerSet.Add(ownerSet, files);
                }

                files.Add(relativePath);
            }
        }

        filesByOwnerSet.Should().NotBeEmpty("expected to find C# test files in the repository");
        unownedFiles.Should().BeEmpty("every test file should be owned by at least one team in .github/CODEOWNERS");
        var ownership = new StringBuilder();
        foreach (var ownerGroup in filesByOwnerSet.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (ownerGroup.Value.Count <= MaxListedFiles)
            {
                ownership.Append(ownerGroup.Key).AppendLine(":");
                foreach (var file in ownerGroup.Value)
                {
                    ownership.Append("  ").AppendLine(file);
                }
            }
            else
            {
                ownership.Append(ownerGroup.Key).Append(" => ").Append(ownerGroup.Value.Count).AppendLine(" files");
            }
        }

        await Verifier.Verify(ownership.ToString());
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

    private static IEnumerable<string> EnumerateRepositoryFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                yield return file;
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(child);
                if (!BuildOutputDirectories.Contains(name) &&
                    (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                {
                    pending.Push(child);
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateRepositoryCSharpFiles(string root)
        => EnumerateRepositoryFiles(root).Where(file => string.Equals(Path.GetExtension(file), ".cs", StringComparison.OrdinalIgnoreCase));
}
