// <copyright file="TestingPlatformModuleLocatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class TestingPlatformModuleLocatorTests : IDisposable
{
    private const string TestingPlatformProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
            <OutputType>Exe</OutputType>
            <IsTestingPlatformApplication>true</IsTestingPlatformApplication>
          </PropertyGroup>
        </Project>
        """;

    private const string LibraryProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd-mtp-locator-" + Path.GetRandomFileName());

    public TestingPlatformModuleLocatorTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FindsTheOutputFolderOfEveryTargetFrameworkOfAProject()
    {
        WriteProject("Tests", TestingPlatformProject);

        var found = TestingPlatformModuleLocator.TryGetOutputFolders("dotnet", Parse("test", "--project", "Tests", "-c", "Release"), _root, out var folders);

        found.Should().BeTrue();
        Relative(folders).Should().BeEquivalentTo(Path.Combine("Tests", "bin", "Release", "net8.0"), Path.Combine("Tests", "bin", "Release", "net9.0"));
    }

    [Fact]
    public void HonorsTheFrameworkOption()
    {
        WriteProject("Tests", TestingPlatformProject);

        var found = TestingPlatformModuleLocator.TryGetOutputFolders("dotnet", Parse("test", "--project", "Tests", "-f", "net9.0"), _root, out var folders);

        found.Should().BeTrue();
        Relative(folders).Should().Equal(Path.Combine("Tests", "bin", "Debug", "net9.0"));
    }

    [Fact]
    public void FindsTheProjectsOfASolutionAndSkipsTheOnesThatAreNotTestApplications()
    {
        WriteProject("Tests1", TestingPlatformProject);
        WriteProject("Tests2", TestingPlatformProject);
        WriteProject("Library", LibraryProject);
        File.WriteAllText(Path.Combine(_root, "All.sln"), CreateSolution("Tests1", "Tests2", "Library"));

        var found = TestingPlatformModuleLocator.TryGetOutputFolders("dotnet", Parse("test", "--solution", "All.sln"), _root, out var folders);

        found.Should().BeTrue();
        Relative(folders).Should().BeEquivalentTo(
            Path.Combine("Tests1", "bin", "Debug", "net8.0"),
            Path.Combine("Tests1", "bin", "Debug", "net9.0"),
            Path.Combine("Tests2", "bin", "Debug", "net8.0"),
            Path.Combine("Tests2", "bin", "Debug", "net9.0"));
    }

    [Fact]
    public void ReportsNothingWhenNoProjectIsATestApplication()
    {
        WriteProject("Library", LibraryProject);

        TestingPlatformModuleLocator.TryGetOutputFolders("dotnet", Parse("test", "--project", "Library"), _root, out var folders).Should().BeFalse();

        folders.Should().BeEmpty();
    }

    [Fact]
    public void ReportsNothingWhenTheProjectCannotBeEvaluated()
    {
        TestingPlatformModuleLocator.TryGetOutputFolders("dotnet", Parse("test", "--project", "Missing"), _root, out var folders).Should().BeFalse();

        folders.Should().BeEmpty();
    }

    [Fact]
    public void DoesNotLeaveFilesInTheProjectFolders()
    {
        WriteProject("Tests", TestingPlatformProject);
        var before = Directory.EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToArray();

        TestingPlatformModuleLocator.TryGetOutputFolders("dotnet", Parse("test", "--project", "Tests"), _root, out _).Should().BeTrue();

        Directory.EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).Should().Equal(before);
    }

    [Fact]
    public void ExpandsTheTestModulesLikeDotnetTestDoes()
    {
        Touch("a/bin/Release/net10.0/One.Tests.dll");
        Touch("a/bin/Release/net10.0/Two.Tests.dll");
        Touch("a/bin/Release/net10.0/One.Tests.exe");
        Touch("b/bin/Release/net9.0/Three.Tests.dll");
        Touch("b/bin/Release/net9.0/Skip.Tests.dll");

        var matches = TestingPlatformModuleLocator.GetMatchedModulePaths("**/*.Tests.dll; !**/Skip.Tests.dll", _root, _root);

        matches.Select(path => Path.GetFileName(path)).Should().BeEquivalentTo("One.Tests.dll", "Two.Tests.dll", "Three.Tests.dll");
    }

    [Fact]
    public void ResolvesARelativeRootDirectoryAgainstTheWorkingDirectory()
    {
        Touch("out/One.Tests.dll");

        var found = TestingPlatformModuleLocator.TryGetOutputFolders("dotnet", Parse("test", "--test-modules", "**/*.Tests.dll", "--root-directory", "out"), _root, out var folders);

        found.Should().BeTrue();
        Relative(folders).Should().Equal("out");
    }

    [Fact]
    public void ReportsNothingWhenTheRootDirectoryDoesNotExist()
    {
        TestingPlatformModuleLocator.GetMatchedModulePaths("**/*.dll", "missing", _root).Should().BeEmpty();
    }

    private static TestingPlatformCommandLine Parse(params string[] arguments) => TestingPlatformCommandLine.Parse(arguments, Path.GetTempPath());

    private static string CreateSolution(params string[] projects)
    {
        // MSBuild only builds the projects of a solution that have a project configuration entry.
        var lines = new System.Collections.Generic.List<string> { "Microsoft Visual Studio Solution File, Format Version 12.00" };
        var guids = projects.Select((_, index) => $"{{{index + 1}0000000-0000-0000-0000-000000000000}}").ToArray();
        for (var i = 0; i < projects.Length; i++)
        {
            lines.Add($"Project(\"{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}\") = \"{projects[i]}\", \"{projects[i]}\\{projects[i]}.csproj\", \"{guids[i]}\"");
            lines.Add("EndProject");
        }

        lines.Add("Global");
        lines.Add("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        lines.Add("\t\tDebug|Any CPU = Debug|Any CPU");
        lines.Add("\t\tRelease|Any CPU = Release|Any CPU");
        lines.Add("\tEndGlobalSection");
        lines.Add("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var guid in guids)
        {
            foreach (var configuration in new[] { "Debug", "Release" })
            {
                lines.Add($"\t\t{guid}.{configuration}|Any CPU.ActiveCfg = {configuration}|Any CPU");
                lines.Add($"\t\t{guid}.{configuration}|Any CPU.Build.0 = {configuration}|Any CPU");
            }
        }

        lines.Add("\tEndGlobalSection");
        lines.Add("EndGlobal");
        return string.Join(Environment.NewLine, lines);
    }

    private string[] Relative(System.Collections.Generic.IEnumerable<string> folders)
        => folders.Select(folder => Path.GetRelativePath(_root, folder)).ToArray();

    private void WriteProject(string name, string content)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        File.WriteAllText(Path.Combine(folder, name + ".csproj"), content);
    }

    private void Touch(string relativePath)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }
}
