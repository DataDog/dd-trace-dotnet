// <copyright file="InstrumentationVerificationSanityCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class InstrumentationVerificationSanityCheckTests : TestHelper
{
    public InstrumentationVerificationSanityCheckTests(ITestOutputHelper output)
        : base(new EnvironmentHelper("Datadog.Tracer.Native.Checks", typeof(TestHelper), output, Path.Combine("test", "test-applications", "instrumentation"), prependSamplesToAppName: false), output)
    {
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, EnvironmentHelper.LogDirectory);
    }

    [Fact]
    [Trait("Category", "LinuxUnsupported")] // Linux support is not implemented yet
    public async Task WriteInstrumentationToDisk_IsEnabled_FilesGetWrittenToTheAppropriateFolder()
    {
        // Arrange
        SetEnvironmentVariable(InstrumentationVerification.InstrumentationVerificationEnabled, "true");

        // Act
        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent);

        // Assert
        var folderFullPath = InstrumentationVerification.FindInstrumentationLogsFolder(processResult.Process, EnvironmentHelper.LogDirectory);
        folderFullPath.Should().NotBeNull();
        Directory.Exists(folderFullPath).Should().BeTrue();
        Directory.GetFiles(folderFullPath, "*", SearchOption.AllDirectories).Length.Should().BeGreaterThan(expected: 0);
        var folderName = Path.GetFileName(folderFullPath);
        folderName.Should().Contain(processResult.Process.Id.ToString());
        folderName.Should().Contain(EnvironmentHelper.SampleName);
    }

    [Fact]
    [Trait("Category", "LinuxUnsupported")] // Linux support is not implemented yet
    public async Task WriteInstrumentationToDisk_IsDisabled_NothingGetsWrittenToDisk()
    {
        // Arrange
        SetEnvironmentVariable(InstrumentationVerification.InstrumentationVerificationEnabled, "false");

        // Act
        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent);

        // Assert
        InstrumentationVerification.FindInstrumentationLogsFolder(processResult.Process, EnvironmentHelper.LogDirectory)
                                   .Should()
                                   .BeNull();
    }

    [Fact]
    [Trait("Category", "LinuxUnsupported")] // Linux support is not implemented yet
    public async Task WriteInstrumentationToDisk_IsNotSpecified_DefaultsToFalseAndNothingGetsWrittenToDisk()
    {
        // Act
        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent);

        // Assert
        processResult.Process.StartInfo.
                      EnvironmentVariables.ContainsKey(InstrumentationVerification.InstrumentationVerificationEnabled)
                     .Should().BeFalse();

        InstrumentationVerification.FindInstrumentationLogsFolder(processResult.Process, EnvironmentHelper.LogDirectory)
                                   .Should()
                                   .BeNull();
    }
}
