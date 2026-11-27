// <copyright file="TestingPlatformCommandLineTests.cs" company="Datadog">
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

public class TestingPlatformCommandLineTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), "dd-mtp-commandline-" + Path.GetRandomFileName());

    public TestingPlatformCommandLineTests()
    {
        Directory.CreateDirectory(Path.Combine(_workingDirectory, "tests", "Sample.Tests"));
    }

    public void Dispose()
    {
        Directory.Delete(_workingDirectory, recursive: true);
    }

    [Theory]
    [InlineData("test --no-build", true, false)]
    [InlineData("test -c Release --no-build", true, false)]
    [InlineData("test", false, false)]
    [InlineData("test -c Release", false, false)]
    [InlineData("test --test-modules **/*.Tests.dll", false, true)]
    [InlineData("test --test-modules **/*.Tests.dll --no-build", true, true)]
    [InlineData("test --no-build -- --report-trx", true, false)]
    [InlineData("test -- --no-build", false, false)]
    [InlineData("test --no-build true", true, false)]
    [InlineData("test --no-build false", false, false)]
    [InlineData("test --no-build False -c Release", false, false)]
    [InlineData("test --no-build:false", false, false)]
    [InlineData("test --no-build=true", true, false)]
    public void ReportsWhetherTheAssembliesAreRunWithoutBuilding(string commandLine, bool noBuild, bool hasTestModules)
    {
        var parsed = TestingPlatformCommandLine.Parse(commandLine.Split(' '), _workingDirectory);

        parsed.NoBuild.Should().Be(noBuild);
        (parsed.TestModules is not null).Should().Be(hasTestModules);
        parsed.RunsPrebuiltAssemblies.Should().Be(noBuild || hasTestModules);
    }

    [Theory]
    [InlineData("test tests/Sample.Tests/Sample.Tests.csproj", "tests/Sample.Tests/Sample.Tests.csproj")]
    [InlineData("test tests/Sample.Tests", "tests/Sample.Tests")]
    [InlineData("test Sample.slnx", "Sample.slnx")]
    [InlineData("test --solution Sample.sln", "Sample.sln")]
    [InlineData("test --project tests/Sample.Tests", "tests/Sample.Tests")]
    [InlineData("test --project:tests/Sample.Tests", "tests/Sample.Tests")]
    [InlineData("test --project=tests/Sample.Tests", "tests/Sample.Tests")]
    [InlineData("test -c Release Sample.slnf -f net10.0", "Sample.slnf")]
    [InlineData("test --results-directory out Sample.sln", "Sample.sln")]
    [InlineData("test -e VAR=value Sample.sln", "Sample.sln")]
    [InlineData("test --filter-trait a=b", null)]
    [InlineData("test", null)]
    public void FindsTheProjectOrSolution(string commandLine, string? expected)
    {
        var parsed = TestingPlatformCommandLine.Parse(commandLine.Split(' '), _workingDirectory);

        parsed.ProjectOrSolution.Should().Be(expected);
    }

    [Fact]
    public void AnExplicitSolutionWinsOverTheProjectAndThePositionalPath()
    {
        var parsed = TestingPlatformCommandLine.Parse(["test", "tests/Sample.Tests", "--project", "a.csproj", "--solution", "b.sln"], _workingDirectory);

        parsed.ProjectOrSolution.Should().Be("b.sln");
    }

    [Theory]
    [InlineData("test -c Release", new[] { "-p:Configuration=Release" })]
    [InlineData("test --configuration:Release", new[] { "-p:Configuration=Release" })]
    [InlineData("test -f net9.0", new[] { "-p:TargetFramework=net9.0" })]
    [InlineData("test -r win-x64", new[] { "-p:RuntimeIdentifier=win-x64", "-p:_CommandLineDefinedRuntimeIdentifier=true" })]
    [InlineData("test -p:Foo=Bar", new[] { "-p:Foo=Bar" })]
    [InlineData("test /p:Foo=Bar --property:Baz=1", new[] { "-p:Foo=Bar", "-p:Baz=1" })]
    [InlineData("test -p Foo=Bar", new[] { "-p:Foo=Bar" })]
    [InlineData("test --p:Foo=Bar", new[] { "-p:Foo=Bar" })]
    [InlineData("test -property Foo=Bar", new[] { "-p:Foo=Bar" })]
    [InlineData("test --property Foo=Bar", new[] { "-p:Foo=Bar" })]
    [InlineData("test /p Foo=Bar", new[] { "-p:Foo=Bar" })]
    [InlineData("test --p Foo=Bar", new[] { "-p:Foo=Bar" })]
    [InlineData("test -c Debug -f net8.0 -p:A=1", new[] { "-p:A=1", "-p:Configuration=Debug", "-p:TargetFramework=net8.0" })]
    [InlineData("test -c Debug -p:Configuration=Release", new[] { "-p:Configuration=Release", "-p:Configuration=Debug" })]
    [InlineData("test -f net8.0 --property TargetFramework=net9.0", new[] { "-p:TargetFramework=net9.0", "-p:TargetFramework=net8.0" })]
    public void ForwardsTheOptionsThatChooseTheOutput(string commandLine, string[] expected)
    {
        var parsed = TestingPlatformCommandLine.Parse(commandLine.Split(' '), _workingDirectory);

        parsed.MsBuildArguments.Should().Equal(expected);
    }

    [Fact]
    public void ResolvesTheArtifactsPathAgainstTheWorkingDirectory()
    {
        var parsed = TestingPlatformCommandLine.Parse(["test", "--artifacts-path", "out"], _workingDirectory);

        parsed.MsBuildArguments.Should().Equal("-p:ArtifactsPath=" + Path.GetFullPath(Path.Combine(_workingDirectory, "out")));
    }

    [Theory]
    [InlineData("test -a x64", "-a")]
    [InlineData("test --arch arm64", "--arch")]
    [InlineData("test --os linux", "--os")]
    [InlineData("test --use-current-runtime", "--use-current-runtime")]
    [InlineData("test --ucr", "--ucr")]
    [InlineData("test --device Pixel", "--device")]
    public void ReportsTheOptionsThatChangeTheOutputInWaysItDoesNotReproduce(string commandLine, string expected)
    {
        var parsed = TestingPlatformCommandLine.Parse(commandLine.Split(' '), _workingDirectory);

        parsed.UnsupportedOption.Should().Be(expected);
    }

    [Theory]
    [InlineData("test --filter-namespace tests")]
    [InlineData("test --report-trx-filename tests")]
    [InlineData("test --some-future-option tests/Sample.Tests")]
    public void DoesNotTakeTheValueOfAnOptionItDoesNotKnowForTheProject(string commandLine)
    {
        var parsed = TestingPlatformCommandLine.Parse(commandLine.Split(' '), _workingDirectory);

        parsed.ProjectOrSolution.Should().BeNull();
    }

    [Theory]
    [InlineData("test --no-restore tests/Sample.Tests", "tests/Sample.Tests")]
    [InlineData("test --list-tests Sample.slnx", "Sample.slnx")]
    [InlineData("test --no-ansi --no-progress tests/Sample.Tests/Sample.Tests.csproj", "tests/Sample.Tests/Sample.Tests.csproj")]
    [InlineData("test --no-artifact-post-processing tests/Sample.Tests", "tests/Sample.Tests")]
    [InlineData("test -nologo --list-devices Sample.slnx", "Sample.slnx")]
    [InlineData("test --no-build tests/Sample.Tests", "tests/Sample.Tests")]
    [InlineData("test --no-build true tests/Sample.Tests", "tests/Sample.Tests")]
    public void KeepsTheProjectAfterOptionsThatTakeNoValue(string commandLine, string expected)
    {
        var parsed = TestingPlatformCommandLine.Parse(commandLine.Split(' '), _workingDirectory);

        parsed.ProjectOrSolution.Should().Be(expected);
    }

    [Theory]
    [InlineData("test -cRelease", "-cRelease")]
    [InlineData("test -fnet9.0", "-fnet9.0")]
    [InlineData("test -ax64", "-ax64")]
    [InlineData("test -rlinux-x64", "-rlinux-x64")]
    [InlineData("test -pFoo=Bar", "-pFoo=Bar")]
    public void DoesNotUnderstandShortOptionsWithTheValueAttached(string commandLine, string expected)
    {
        var parsed = TestingPlatformCommandLine.Parse(commandLine.Split(' '), _workingDirectory);

        parsed.UnsupportedOption.Should().Be(expected);
    }

    [Fact]
    public void ReadsTheTestModulesAndTheirRootDirectory()
    {
        var parsed = TestingPlatformCommandLine.Parse(["test", "--test-modules", "**/*.Tests.dll;!**/Skip.dll", "--root-directory", "out"], _workingDirectory);

        parsed.TestModules.Should().Be("**/*.Tests.dll;!**/Skip.dll");
        parsed.TestModulesRootDirectory.Should().Be("out");
    }

    [Fact]
    public void IgnoresWhatComesAfterTheDoubleDash()
    {
        var parsed = TestingPlatformCommandLine.Parse(["test", "--", "--project", "other.csproj", "-c", "Debug", "-a", "x64"], _workingDirectory);

        parsed.ProjectOrSolution.Should().BeNull();
        parsed.MsBuildArguments.Should().BeEmpty();
        parsed.UnsupportedOption.Should().BeNull();
    }

    [Theory]
    [InlineData('"')]
    [InlineData('\'')]
    public void ToleratesQuotedArguments(char quote)
    {
        string[] arguments = ["test", "--no-build", "-c", "Release", "Sample.slnx"];
        var parsed = TestingPlatformCommandLine.Parse(arguments.Select(argument => quote + argument + quote).ToArray(), _workingDirectory);

        parsed.NoBuild.Should().BeTrue();
        parsed.ProjectOrSolution.Should().Be("Sample.slnx");
        parsed.MsBuildArguments.Should().Equal("-p:Configuration=Release");
    }

    [Fact]
    public void SkipsTheArgumentsBeforeTheTestVerb()
    {
        var parsed = TestingPlatformCommandLine.Parse(["--diagnostics", "test", "--no-build"], _workingDirectory);

        parsed.NoBuild.Should().BeTrue();
    }
}
