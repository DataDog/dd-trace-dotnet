// <copyright file="BundlePublishTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Bundle.ArtifactTests;

/// <summary>
/// Publishes an app referencing the built Datadog.Trace.Bundle package and checks which datadog/ directories are published.
/// The DDBUNDLE001 error (MSBuild older than 16.7) isn't tested, as it requires an old SDK.
/// </summary>
public class BundlePublishTests : IClassFixture<BundleProjectFixture>
{
    private readonly BundleProjectFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BundlePublishTests(BundleProjectFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Theory]
    [InlineData("-f net8.0")]
    [InlineData("-f netcoreapp2.0")]
    public void PublishesEverythingWhenDatadogPlatformsIsNotSet(string arguments)
    {
        var result = Publish(arguments);

        using var scope = new AssertionScope();
        result.ExitCode.Should().Be(0, result.ToString());
        result.Output.Should().NotContain("DDBUNDLE");
        // If the package gains a new directory, the publish filtering targets must be updated to handle it
        result.GetDirectories().Should().Equal(
            "linux-arm64",
            "linux-musl-arm64",
            "linux-musl-x64",
            "linux-x64",
            "net461",
            "net6.0",
            "netcoreapp3.1",
            "netstandard2.0",
            "osx",
            "win-x64",
            "win-x86");
    }

    [Theory]
    [InlineData("-f net8.0 -p:DatadogPlatforms=linux-x64", "linux-x64;net6.0")]
    [InlineData("-f net8.0 -p:DatadogPlatforms=\"linux-musl-x64;Linux-Musl-Arm64\"", "linux-musl-arm64;linux-musl-x64;net6.0")]
    [InlineData("-f net8.0 -p:DatadogPlatforms=linux-arm64%3Bwin-x64", "linux-arm64;net6.0;win-x64")]
    [InlineData("-f net8.0 -p:DatadogPlatforms=osx-arm64", "net6.0;osx")]
    [InlineData("-f net8.0 -r linux-x64 -p:DatadogPlatforms=linux-x64", "linux-x64;net6.0")]
    [InlineData("-f net8.0 -r linux-musl-x64 --self-contained -p:DatadogPlatforms=linux-musl-x64", "linux-musl-x64;net6.0")]
    [InlineData("-f net8.0 -p:DatadogPlatforms=win-x64 -p:DatadogRuntimes=\"netcoreapp3.1;net8.0\"", "net6.0;netcoreapp3.1;win-x64")]
    [InlineData("-f net5.0 -p:DatadogPlatforms=win-x64", "netcoreapp3.1;win-x64")]
    [InlineData("-f netcoreapp2.1 -p:DatadogPlatforms=win-x64", "netstandard2.0;win-x64")]
    [InlineData("-f net48 -p:DatadogPlatforms=win-x64", "net461;win-x64")]
    [InlineData("-f net48 -p:DatadogPlatforms=\"win-x64;win-x86\"", "net461;win-x64;win-x86")]
    [InlineData("-f net48 -p:DatadogPlatforms=win-x64 -p:DatadogRuntimes=net45", "net461;win-x64")]
    public void PublishesOnlyRequestedDirectories(string arguments, string expectedDirectories)
    {
        var result = Publish(arguments);

        using var scope = new AssertionScope();
        result.ExitCode.Should().Be(0, result.ToString());
        result.Output.Should().NotContain("DDBUNDLE");
        result.GetDirectories().Should().Equal(expectedDirectories.Split(';'));
    }

    [Fact]
    public void KeepsX64DdDotnetForX86()
    {
        var result = Publish("-f net48 -p:DatadogPlatforms=win-x86");

        using var scope = new AssertionScope();
        result.ExitCode.Should().Be(0, result.ToString());
        result.GetDirectories().Should().Equal("net461", "win-x64", "win-x86");
        // dd-dotnet.cmd always runs win-x64/dd-dotnet.exe, as there's no x86 build
        result.GetFiles("win-x64").Should().Equal("dd-dotnet.exe");
    }

    [Theory]
    [InlineData("-f net8.0 -r linux-x64 -p:DatadogPlatforms=win-x64", "DDBUNDLE010")]
    [InlineData("-f net8.0 -r linux-arm64 -p:DatadogPlatforms=linux-x64", "DDBUNDLE010")]
    [InlineData("-f net48 -p:DatadogPlatforms=linux-x64", "DDBUNDLE011")]
    [InlineData("-f net48 -p:DatadogPlatforms=win-x64 -p:PlatformTarget=x86", "DDBUNDLE012")]
    // Version-specific RIDs aren't mapped to a platform, so they can't be validated, even if DatadogPlatforms matches.
    // UseRidGraph is required, as the restore runs for every TargetFramework, and net8.0+ rejects version-specific RIDs
    [InlineData("-f net5.0 -r win10-x64 --no-self-contained -p:UseRidGraph=true -p:DatadogPlatforms=win-x86", "DDBUNDLE013")]
    [InlineData("-f net5.0 -r win10-x64 --no-self-contained -p:UseRidGraph=true -p:DatadogPlatforms=win-x64", "DDBUNDLE013")]
    public void WarnsOnContradictions(string arguments, string warningCode)
    {
        var result = Publish(arguments);

        using var scope = new AssertionScope();
        result.ExitCode.Should().Be(0, result.ToString());
        result.Output.Should().Contain($"warning {warningCode}");
    }

    [Theory]
    [InlineData("-f net8.0 -p:DatadogPlatforms=win10-x64", "DDBUNDLE002")]
    [InlineData("-f net8.0 -p:DatadogPlatforms=\"linux-x64;alpine-x64\"", "DDBUNDLE002")]
    [InlineData("-f net8.0 -p:DatadogPlatforms=win-x64 -p:DatadogRuntimes=netstandard2.0", "DDBUNDLE003")]
    [InlineData("-f netcoreapp2.0 -p:DatadogPlatforms=win-x64", "DDBUNDLE003")]
    public void FailsForUnsupportedValues(string arguments, string errorCode)
    {
        var result = Publish(arguments);

        using var scope = new AssertionScope();
        result.ExitCode.Should().NotBe(0, result.ToString());
        result.Output.Should().Contain($"error {errorCode}");
    }

    private PublishResult Publish(string arguments)
    {
        _output.WriteLine($"dotnet publish {arguments}");
        var result = _fixture.Publish(arguments);
        _output.WriteLine(result.ToString());
        return result;
    }
}
